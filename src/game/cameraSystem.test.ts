import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { CameraRigController, CAMERA_PRESETS } from './cameraSystem';
import { createInitialCharacterSwitchState } from './protagonists';

describe('cameraSystem', () => {
  it('provides well-formed configurations across all camera view presets', () => {
    assert.ok(CAMERA_PRESETS.third_person_close.distance > 0);
    assert.ok(CAMERA_PRESETS.third_person_far.distance > CAMERA_PRESETS.third_person_close.distance);
    assert.ok(CAMERA_PRESETS.first_person.distance < 0.5);
    assert.ok(CAMERA_PRESETS.first_person.fov > CAMERA_PRESETS.third_person_close.fov);
  });

  it('steps smoothly in third-person mode with shoulder offset and target damping', () => {
    const rig = new CameraRigController(new THREE.Vector3(0, 3, 5), new THREE.Vector3(0, 1.5, 0));
    const result = rig.step({
      dt: 0.016,
      playerPos: { x: 10, y: 0, z: 20 },
      playerHeading: 0,
      playerVelocity: { x: 0, y: 0, z: 0 },
      camYaw: 0,
      camPitch: 0.3,
      viewMode: 'third_person_close',
      inVehicle: false,
    });

    assert.ok(Number.isFinite(result.camPos.x));
    assert.ok(Number.isFinite(result.camPos.y));
    assert.ok(Number.isFinite(result.camPos.z));
    assert.ok(result.fov > 50 && result.fov < 65);
  });

  it('executes 3-phase character switch sky-cam transition', () => {
    const rig = new CameraRigController();
    const sw = createInitialCharacterSwitchState();
    sw.active = true;
    sw.fromPos = { x: 0, y: 0, z: 0 };
    sw.toPos = { x: 150, y: 0, z: -200 };
    sw.skyAltitude = 60;
    sw.duration = 2.0;

    // Ascend phase at t = 0.2
    const res1 = rig.step({
      dt: 0.2,
      playerPos: sw.fromPos,
      playerHeading: 0,
      playerVelocity: { x: 0, y: 0, z: 0 },
      camYaw: 0,
      camPitch: 0.3,
      viewMode: 'third_person_close',
      inVehicle: false,
      characterSwitch: sw,
    });
    assert.equal(sw.phase, 'ascend');
    assert.ok(res1.camPos.y > 5);

    // Pan phase at t = 1.0
    sw.timer = 1.0;
    const res2 = rig.step({
      dt: 0.016,
      playerPos: sw.fromPos,
      playerHeading: 0,
      playerVelocity: { x: 0, y: 0, z: 0 },
      camYaw: 0,
      camPitch: 0.3,
      viewMode: 'third_person_close',
      inVehicle: false,
      characterSwitch: sw,
    });
    assert.equal(sw.phase, 'pan');
    assert.ok(res2.camPos.y >= 60);

    // Descend phase at t = 1.8
    sw.timer = 1.8;
    const res3 = rig.step({
      dt: 0.016,
      playerPos: sw.toPos,
      playerHeading: 0,
      playerVelocity: { x: 0, y: 0, z: 0 },
      camYaw: 0,
      camPitch: 0.3,
      viewMode: 'third_person_close',
      inVehicle: false,
      characterSwitch: sw,
    });
    assert.equal(sw.phase, 'descend');
  });

  it('adjusts FOV dynamically for high-speed driving and weapon aiming', () => {
    const rig = new CameraRigController();
    const aimRes = rig.step({
      dt: 0.5,
      playerPos: { x: 0, y: 0, z: 0 },
      playerHeading: 0,
      playerVelocity: { x: 0, y: 0, z: 0 },
      camYaw: 0,
      camPitch: 0.3,
      viewMode: 'third_person_close',
      inVehicle: false,
      isAiming: true,
    });
    assert.ok(aimRes.fov < 55); // Zoomed in

    const driveRes = rig.step({
      dt: 0.5,
      playerPos: { x: 0, y: 0, z: 0 },
      playerHeading: 0,
      playerVelocity: { x: 0, y: 0, z: 25 },
      camYaw: 0,
      camPitch: 0.3,
      viewMode: 'third_person_close',
      inVehicle: true,
      vehicleSpeed: 25,
    });
    assert.ok(driveRes.fov > 60); // Wide angle boost
  });
});
