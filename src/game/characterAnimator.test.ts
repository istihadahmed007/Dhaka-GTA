import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import * as THREE from 'three';
import {
  computeLocomotionWeights,
  computeFootContactsFromPhase,
  solveTwoBoneIK,
  CharacterAnimator,
  ProceduralLimbTargets,
} from './characterAnimator';
import { resolveGraphicsQualityConfig } from './graphicsQuality';

test('computeLocomotionWeights smoothly blends Idle -> Walk -> Run with normalized weights', () => {
  const idle = computeLocomotionWeights(0, 5.8, 9.6, true);
  assert.equal(idle.idleWeight, 1);
  assert.equal(idle.walkWeight, 0);
  assert.equal(idle.runWeight, 0);

  const slowWalk = computeLocomotionWeights(2.8, 5.8, 9.6, true);
  assert.ok(slowWalk.idleWeight > 0 && slowWalk.idleWeight < 1);
  assert.ok(slowWalk.walkWeight > 0 && slowWalk.walkWeight < 1);
  assert.equal(slowWalk.runWeight, 0);
  assert.ok(Math.abs(slowWalk.idleWeight + slowWalk.walkWeight + slowWalk.runWeight - 1) < 1e-6);

  const fullWalk = computeLocomotionWeights(5.8, 5.8, 9.6, true);
  assert.equal(fullWalk.idleWeight, 0);
  assert.equal(fullWalk.walkWeight, 1);
  assert.equal(fullWalk.runWeight, 0);

  const jog = computeLocomotionWeights(7.7, 5.8, 9.6, true);
  assert.equal(jog.idleWeight, 0);
  assert.ok(jog.walkWeight > 0 && jog.walkWeight < 1);
  assert.ok(jog.runWeight > 0 && jog.runWeight < 1);
  assert.ok(Math.abs(jog.walkWeight + jog.runWeight - 1) < 1e-6);
  assert.ok(jog.jogWeight > 0);

  const sprint = computeLocomotionWeights(9.8, 5.8, 9.6, true);
  assert.equal(sprint.idleWeight, 0);
  assert.equal(sprint.walkWeight, 0);
  assert.equal(sprint.runWeight, 1);
  assert.ok(sprint.sprintWeight > 0.9);
  assert.ok(sprint.forwardLeanRad > 0.08);
});

test('computeLocomotionWeights slows stride cadence when airborne', () => {
  const air = computeLocomotionWeights(6.5, 5.8, 9.6, false);
  assert.ok(air.walkTimeScale < 0.5);
  assert.ok(air.runTimeScale < 0.5);
  assert.ok(Math.abs(air.idleWeight + air.walkWeight + air.runWeight - 1) < 1e-6);
});

test('computeLocomotionWeights computes trajectory banking roll and dynamic phase velocity', () => {
  const straight = computeLocomotionWeights(8.0, 5.8, 9.6, true, 0);
  assert.equal(straight.bankingRad, 0);

  const turningRight = computeLocomotionWeights(8.0, 5.8, 9.6, true, 1.5);
  assert.ok(turningRight.bankingRad < 0, 'Turning right banks roll to negative rad');

  const turningLeft = computeLocomotionWeights(8.0, 5.8, 9.6, true, -1.5);
  assert.ok(turningLeft.bankingRad > 0, 'Turning left banks roll to positive rad');

  assert.ok(straight.phaseVelocity > 8.0, 'Locomotion phase velocity scales with speed');
});

test('computeFootContactsFromPhase derives alternating foot strike signals', () => {
  // Phase 0: Left foot heel strike/stance
  const contact0 = computeFootContactsFromPhase(0);
  assert.ok(contact0.leftContact > 0.8, 'Left foot planted at phase 0');
  assert.equal(contact0.rightContact, 0, 'Right foot swing at phase 0');
  assert.ok(contact0.rightHeight > 0.5, 'Right foot has swing clearance');

  // Phase PI: Right foot heel strike/stance
  const contactPi = computeFootContactsFromPhase(Math.PI);
  assert.equal(contactPi.leftContact, 0, 'Left foot swing at phase PI');
  assert.ok(contactPi.rightContact > 0.8, 'Right foot planted at phase PI');
  assert.ok(contact0.leftHeight < 0.1);
});

test('solveTwoBoneIK accurately calculates joint flexion using law of cosines', () => {
  const lenUpper = 0.45;
  const lenLower = 0.45;

  // Fully extended (distance 0.9)
  const straight = solveTwoBoneIK(0.89, lenUpper, lenLower);
  assert.ok(straight.valid);
  assert.ok(straight.bendAngle < 0.35, 'Almost zero knee bend when fully extended');

  // Mid-flexion (distance 0.6)
  const flexed = solveTwoBoneIK(0.6, lenUpper, lenLower);
  assert.ok(flexed.valid);
  assert.ok(flexed.bendAngle > 1.2 && flexed.bendAngle < 1.8, 'Expected natural knee flexion');

  // Deep crouch (distance 0.3)
  const crouch = solveTwoBoneIK(0.3, lenUpper, lenLower);
  assert.ok(crouch.valid);
  assert.ok(crouch.bendAngle > flexed.bendAngle, 'Deep flexion for crouched stance');
});

test('CharacterAnimator procedural fallback executes all animation states cleanly', () => {
  const procedural: ProceduralLimbTargets = {
    leftLeg: new THREE.Object3D(),
    rightLeg: new THREE.Object3D(),
    leftArm: new THREE.Object3D(),
    rightArm: new THREE.Object3D(),
    torso: new THREE.Object3D(),
    head: new THREE.Object3D(),
    bodyRoot: new THREE.Object3D(),
  };

  const animator = new CharacterAnimator(procedural);
  assert.equal(animator.isUsingGlb(), false);

  // 1. Idle state
  animator.update(0.016, { speed: 0, isGrounded: true });
  assert.ok(Math.abs(procedural.leftLeg.rotation.x) < 0.05);

  // 2. Walking / Locomotion state (advances phase and swings limbs)
  const initialPhase = animator.getMotionPhase();
  animator.update(0.1, { speed: 4.2, isGrounded: true });
  assert.ok(animator.getMotionPhase() !== initialPhase, 'Phase advanced during movement');
  assert.ok(procedural.leftLeg.rotation.x !== 0, 'Legs swing rhythmically');

  // 3. In-place turning state
  animator.update(0.05, { speed: 0, turnRate: 1.8, isGrounded: true });
  assert.ok(procedural.torso!.rotation.y !== 0, 'Torso rotates with turn rate');

  // 4. Jump state (airborne launch)
  animator.update(0.05, { speed: 5.0, isGrounded: false, isJumping: true, verticalVelocity: 4.5 });
  assert.ok(procedural.bodyRoot.position.y > 0, 'Tuck / launch elevation during jump');

  // 5. Fall state (airborne descent)
  animator.update(0.05, { speed: 3.0, isGrounded: false, verticalVelocity: -3.5 });
  assert.ok(procedural.leftArm.rotation.z < -0.2, 'Arms spread for balance during fall');

  // 6. Landing state (compression squash)
  animator.update(0.05, { speed: 2.0, isGrounded: true, verticalVelocity: 0, isLanding: true });
  assert.ok(procedural.bodyRoot.position.y < 0, 'Landing compression drops hips to absorb impact');

  // 7. Aiming and Shooting with recoil spring
  animator.update(0.016, { speed: 0, isAiming: true, weapon: 'pistol', aimPitch: 0.15, isShooting: true });
  assert.ok(procedural.rightArm.rotation.x < -1.0, 'Right arm raises to firearm aim pose');

  // 8. Vehicle states: enter -> drive -> exit
  animator.enterVehicle(0.6);
  assert.equal(animator.getVehicleAnimationState(), 'entering');
  animator.update(0.65, { speed: 0, isGrounded: true });
  assert.equal(animator.getVehicleAnimationState(), 'driving');
  assert.ok(procedural.bodyRoot.position.y < -0.3, 'Character hips seated inside vehicle');
  assert.ok(procedural.leftLeg.rotation.x < -1.2, 'Legs bent 90 degrees at hips and knees');

  animator.exitVehicle(0.5);
  assert.equal(animator.getVehicleAnimationState(), 'exiting');
  animator.update(0.55, { speed: 0, isGrounded: true });
  assert.equal(animator.getVehicleAnimationState(), 'none');
  assert.ok(Math.abs(procedural.bodyRoot.position.y) < 1e-4, 'Character stood up on pavement after exit');
});

test('resolveGraphicsQualityConfig protects software WebGL renderers across all presets', () => {
  for (const preset of ['low', 'medium', 'high'] as const) {
    const sw = resolveGraphicsQualityConfig(preset, true, 2);
    assert.equal(sw.shadowsEnabled, false);
    assert.equal(sw.pixelRatio, 1);
  }

  const hwLow = resolveGraphicsQualityConfig('low', false, 2);
  assert.equal(hwLow.shadowsEnabled, false);
  assert.equal(hwLow.pixelRatio, 1);
  assert.equal(hwLow.lampHalosEnabled, false);

  const hwMed = resolveGraphicsQualityConfig('medium', false, 2);
  assert.equal(hwMed.shadowsEnabled, true);
  assert.equal(hwMed.shadowMapSize, 1024);
  assert.equal(hwMed.pixelRatio, 1.5);

  const hwHigh = resolveGraphicsQualityConfig('high', false, 2);
  assert.equal(hwHigh.shadowsEnabled, true);
  assert.equal(hwHigh.shadowMapSize, 2048);
  assert.equal(hwHigh.pixelRatio, 2);
});

test('public/models/dhaka_character.glb is a valid compact glTF 2.0 rigged character with 19 retargeted clips', () => {
  const glbPath = path.resolve(process.cwd(), 'public/models/dhaka_character.glb');
  assert.ok(fs.existsSync(glbPath), 'Expected public/models/dhaka_character.glb to exist');

  const buf = fs.readFileSync(glbPath);
  assert.ok(buf.byteLength > 20_000 && buf.byteLength < 350_000, `Expected compact GLB, got ${buf.byteLength} bytes`);

  // Check GLB magic header ("glTF", version 2)
  assert.equal(buf.toString('ascii', 0, 4), 'glTF');
  assert.equal(buf.readUInt32LE(4), 2);

  // Parse JSON chunk (starts at offset 12: chunkLength u32, chunkType u32 = 0x4E4F534A "JSON")
  const jsonLen = buf.readUInt32LE(12);
  const chunkType = buf.toString('ascii', 16, 20);
  assert.equal(chunkType, 'JSON');
  const gltf = JSON.parse(buf.toString('utf8', 20, 20 + jsonLen)) as {
    skins?: Array<{ joints: number[] }>;
    animations?: Array<{ name: string }>;
    nodes?: Array<{ name?: string }>;
  };

  assert.ok(Array.isArray(gltf.skins) && gltf.skins.length >= 1, 'Expected at least 1 skin');
  assert.equal(gltf.skins[0].joints.length, 17, 'Expected 17 humanoid skeleton joints');

  const nodeNames = new Set((gltf.nodes ?? []).map((n) => n.name));
  for (const requiredBone of ['Hips', 'Spine', 'Chest', 'Head', 'RightHand', 'LeftThigh', 'RightShin']) {
    assert.ok(nodeNames.has(requiredBone), `Missing required bone: ${requiredBone}`);
  }

  const clipNames = new Set((gltf.animations ?? []).map((a) => a.name));
  const expectedClips = [
    'Idle',
    'Walk',
    'Jog',
    'Run',
    'TurnLeft',
    'TurnRight',
    'Start',
    'Stop',
    'Jump',
    'Fall',
    'Land',
    'Aim',
    'Shoot',
    'VehicleEnter',
    'VehicleExit',
    'VehicleDrive',
    'Attack',
    'Hit',
    'KO',
  ];

  for (const requiredClip of expectedClips) {
    assert.ok(clipNames.has(requiredClip), `Missing required AnimationClip: ${requiredClip}`);
  }
  assert.equal(clipNames.size, 19, 'Expected exactly 19 retargeted animation clips');
});
