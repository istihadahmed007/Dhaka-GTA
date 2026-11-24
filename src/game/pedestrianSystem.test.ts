import * as THREE from 'three';
import test from 'node:test';
import assert from 'node:assert/strict';
import {
  DHAKA_CROSSWALKS,
  DHAKA_PEDESTRIAN_SPOTS,
  isVehicleApproachingCrosswalk,
  updateIntelligentPedestrian,
  updatePedestrianAnimator,
} from './pedestrianSystem';
import { NpcState, VehicleState } from './types';
import { CharacterAnimator, ProceduralLimbTargets } from './characterAnimator';

test('pedestrianSystem: designated crosswalks and activity spots are placed across Dhaka', () => {
  assert.ok(DHAKA_CROSSWALKS.length >= 8);
  for (const cw of DHAKA_CROSSWALKS) {
    assert.ok(cw.id.length > 0);
    assert.ok(cw.name.length > 0);
    assert.ok(cw.roadHeading === 'NS' || cw.roadHeading === 'EW');
    assert.ok(Math.hypot(cw.x1 - cw.x2, cw.z1 - cw.z2) > 5);
  }

  assert.ok(DHAKA_PEDESTRIAN_SPOTS.length >= 8);
  for (const spot of DHAKA_PEDESTRIAN_SPOTS) {
    assert.ok(spot.name.length > 0);
    assert.ok(['tea_stall', 'shop', 'bus_stop', 'riverfront'].includes(spot.kind));
  }
});

test('pedestrianSystem: isVehicleApproachingCrosswalk detects fast incoming vehicles', () => {
  const cw = DHAKA_CROSSWALKS[0]; // Shapla North: z = 12, EW
  const incomingVeh: VehicleState = {
    id: 'veh_approach',
    kind: 'sedan',
    x: 0,
    y: 0,
    z: 2,
    heading: 0, // facing +Z towards z = 12
    speed: 7,
    steerAngle: 0,
    hp: 100,
    maxHp: 100,
    color: 0xffffff,
    driver: 'traffic',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };
  assert.strictEqual(isVehicleApproachingCrosswalk(incomingVeh, cw), true);

  const parkedVeh: VehicleState = {
    ...incomingVeh,
    speed: 0,
  };
  assert.strictEqual(isVehicleApproachingCrosswalk(parkedVeh, cw), false);

  const recedingVeh: VehicleState = {
    ...incomingVeh,
    heading: Math.PI, // facing away (-Z)
  };
  assert.strictEqual(isVehicleApproachingCrosswalk(recedingVeh, cw), false);
});

test('pedestrianSystem: danger reaction triggers panic sprint away from danger source', () => {
  const ped: NpcState = {
    id: 'npc_danger_test',
    role: 'civilian',
    policy: 'chaser',
    x: 10,
    y: 0,
    z: 10,
    vx: 0,
    vz: 0,
    heading: 0,
    hp: 45,
    maxHp: 45,
    speed: 2.1,
    state: 'walk',
    stateTimer: 2,
    windupDuration: 0.4,
    attackCooldown: 0.6,
    hitFlashTimer: 0,
    koTimer: 0,
    shirtColor: 0xffffff,
    pantsColor: 0x000000,
    targetX: 10,
    targetZ: 10,
    hasAttackToken: false,
    strafeDir: 1,
    dangerSourceX: 5, // Danger source to the left and back
    dangerSourceZ: 5,
  };

  updateIntelligentPedestrian(ped, 0.1, [ped], [], 0, 0, []);
  assert.strictEqual(ped.state, 'flee');
  assert.ok(ped.stateTimer > 4);
  // Velocity must point AWAY from danger (towards positive X and Z)
  assert.ok(ped.vx > 0);
  assert.ok(ped.vz > 0);
  assert.ok(Math.hypot(ped.vx, ped.vz) > 4.5); // full sprint
});

test('pedestrianSystem: pedestrian dodges incoming barreling vehicle laterally', () => {
  const ped: NpcState = {
    id: 'npc_dodge_test',
    role: 'civilian',
    policy: 'chaser',
    x: 0.8,
    y: 0,
    z: 10,
    vx: 0,
    vz: 0,
    heading: 0,
    hp: 45,
    maxHp: 45,
    speed: 2.1,
    state: 'walk',
    stateTimer: 2,
    windupDuration: 0.4,
    attackCooldown: 0.6,
    hitFlashTimer: 0,
    koTimer: 0,
    shirtColor: 0xffffff,
    pantsColor: 0x000000,
    targetX: 0.8,
    targetZ: 10,
    hasAttackToken: false,
    strafeDir: 1,
  };

  const barrelingCar: VehicleState = {
    id: 'fast_car',
    kind: 'sedan',
    x: 0,
    y: 0,
    z: 7, // 3m behind pedestrian, speeding towards +Z
    heading: 0,
    speed: 9,
    steerAngle: 0,
    hp: 100,
    maxHp: 100,
    color: 0xffffff,
    driver: 'traffic',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  updateIntelligentPedestrian(ped, 0.1, [ped], [barrelingCar], 0, 0, []);
  // Pedestrian should execute lateral evasion dodge
  assert.ok(Math.abs(ped.vx) > 2.0);
});

test('pedestrianSystem: updatePedestrianAnimator updates locomotion and gait without error', () => {
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
  const ped: NpcState = {
    id: 'anim_test',
    role: 'civilian',
    policy: 'chaser',
    x: 0,
    y: 0,
    z: 0,
    vx: 1.5,
    vz: 0,
    heading: 0,
    hp: 45,
    maxHp: 45,
    speed: 2.1,
    state: 'walk',
    stateTimer: 2,
    windupDuration: 0.4,
    attackCooldown: 0.6,
    hitFlashTimer: 0,
    koTimer: 0,
    shirtColor: 0xffffff,
    pantsColor: 0x000000,
    targetX: 0,
    targetZ: 0,
    hasAttackToken: false,
    strafeDir: 1,
  };

  assert.doesNotThrow(() => {
    updatePedestrianAnimator(animator, ped, 0.033);
  });
  assert.ok(animator.getMotionPhase() > 0);
});
