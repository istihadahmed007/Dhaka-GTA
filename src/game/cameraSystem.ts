import * as THREE from 'three';
import { CharacterSwitchState } from './protagonists';

export type CameraViewMode = 'third_person_close' | 'third_person_far' | 'first_person' | 'cinematic';

export interface CameraRigConfig {
  fov: number;
  distance: number;
  height: number;
  shoulderOffset: number;
  pitchMin: number;
  pitchMax: number;
  dampPosition: number;
  dampTarget: number;
}

export const CAMERA_PRESETS: Record<CameraViewMode, CameraRigConfig> = {
  third_person_close: {
    fov: 56,
    distance: 3.4,
    height: 1.45,
    shoulderOffset: 0.42,
    pitchMin: 0.08,
    pitchMax: 0.75,
    dampPosition: 11.0,
    dampTarget: 14.0,
  },
  third_person_far: {
    fov: 62,
    distance: 6.8,
    height: 2.2,
    shoulderOffset: 0.0,
    pitchMin: 0.05,
    pitchMax: 0.72,
    dampPosition: 8.5,
    dampTarget: 12.0,
  },
  first_person: {
    fov: 72,
    distance: 0.1,
    height: 1.62,
    shoulderOffset: 0.0,
    pitchMin: -0.65,
    pitchMax: 0.85,
    dampPosition: 24.0,
    dampTarget: 26.0,
  },
  cinematic: {
    fov: 52,
    distance: 9.5,
    height: 1.1,
    shoulderOffset: 0.6,
    pitchMin: 0.02,
    pitchMax: 0.65,
    dampPosition: 5.5,
    dampTarget: 7.0,
  },
};

export interface StepCameraInput {
  dt: number;
  playerPos: { x: number; y: number; z: number };
  playerHeading: number;
  playerVelocity: { x: number; y: number; z: number };
  camYaw: number;
  camPitch: number;
  viewMode: CameraViewMode;
  inVehicle: boolean;
  vehicleSpeed?: number;
  vehicleSteerAngle?: number;
  vehicleKind?: string | null;
  isAiming?: boolean;
  trauma?: number;
  occluders?: THREE.Object3D[];
  characterSwitch?: CharacterSwitchState | null;
  focusReflexActive?: boolean;
}

export interface StepCameraResult {
  camPos: THREE.Vector3;
  camTarget: THREE.Vector3;
  fov: number;
  rollAngle: number;
}

export class CameraRigController {
  private currentPos = new THREE.Vector3();
  private currentTarget = new THREE.Vector3();
  private currentFov = 58;
  private currentRoll = 0;
  private raycaster = new THREE.Raycaster();
  private vecA = new THREE.Vector3();
  private vecB = new THREE.Vector3();
  private vecC = new THREE.Vector3();

  constructor(initialPos = new THREE.Vector3(0, 5, 10), initialTarget = new THREE.Vector3(0, 1.5, 0)) {
    this.currentPos.copy(initialPos);
    this.currentTarget.copy(initialTarget);
  }

  public getPosition(): THREE.Vector3 {
    return this.currentPos;
  }

  public getTarget(): THREE.Vector3 {
    return this.currentTarget;
  }

  public getFov(): number {
    return this.currentFov;
  }

  public getRoll(): number {
    return this.currentRoll;
  }

  public setImmediate(pos: THREE.Vector3, target: THREE.Vector3): void {
    this.currentPos.copy(pos);
    this.currentTarget.copy(target);
  }

  public step(input: StepCameraInput): StepCameraResult {
    const dt = Math.min(0.1, Math.max(0.001, input.dt));

    // 1. Check for Active GTA VI Character Switch Sky-Cam Transition
    if (input.characterSwitch && input.characterSwitch.active) {
      return this.stepCharacterSwitch(input.characterSwitch, dt);
    }

    // 2. Standard Gameplay Camera Rig
    const preset = CAMERA_PRESETS[input.viewMode] || CAMERA_PRESETS.third_person_close;
    const isFirstPerson = input.viewMode === 'first_person';

    let dist = preset.distance;
    let height = preset.height;
    let shoulder = preset.shoulderOffset;

    if (input.inVehicle) {
      if (isFirstPerson) {
        // Cockpit view: driver eye level inside cabin
        height = input.vehicleKind === 'launch' ? 2.8 : input.vehicleKind === 'bus' ? 2.4 : 1.15;
        dist = 0.05;
        shoulder = input.vehicleKind === 'bike' ? 0 : -0.32; // Left-hand drive seat
      } else {
        // Vehicle chase cam adjusts for vehicle dimensions
        dist = input.vehicleKind === 'launch' ? 12.0 : input.vehicleKind === 'bus' ? 9.8 : preset.distance * 1.35;
        height = input.vehicleKind === 'launch' ? 4.2 : input.vehicleKind === 'bus' ? 3.2 : preset.height * 1.25;
      }
    }

    const yawCos = Math.cos(input.camYaw);
    const yawSin = Math.sin(input.camYaw);
    const pitchCos = Math.cos(input.camPitch);
    const pitchSin = Math.sin(input.camPitch);

    // Dynamic shoulder offset perpendicular to camera heading
    const shoulderX = -yawCos * shoulder;
    const shoulderZ = yawSin * shoulder;

    // High-speed velocity lead
    const speed = input.vehicleSpeed || Math.hypot(input.playerVelocity.x, input.playerVelocity.z);
    const leadFactor = input.inVehicle ? Math.min(2.5, speed * 0.08) : Math.min(1.2, speed * 0.06);
    const leadX = input.playerVelocity.x * leadFactor * dt;
    const leadZ = input.playerVelocity.z * leadFactor * dt;

    const targetX = input.playerPos.x + shoulderX + leadX;
    const targetY = input.playerPos.y + height;
    const targetZ = input.playerPos.z + shoulderZ + leadZ;

    const desiredX = targetX + yawSin * pitchCos * dist;
    const desiredY = targetY + pitchSin * dist;
    const desiredZ = targetZ + yawCos * pitchCos * dist;

    this.vecA.set(targetX, targetY, targetZ);
    this.vecB.set(desiredX, desiredY, desiredZ);

    // Collision raycasting against camera occluders to prevent clipping through buildings
    if (!isFirstPerson && input.occluders && input.occluders.length > 0) {
      this.vecC.subVectors(this.vecB, this.vecA);
      const desiredLen = this.vecC.length();
      if (desiredLen > 0.2) {
        this.vecC.divideScalar(desiredLen);
        this.raycaster.set(this.vecA, this.vecC);
        this.raycaster.far = desiredLen;
        const hits = this.raycaster.intersectObjects(input.occluders, false);
        if (hits.length > 0 && hits[0].distance < desiredLen) {
          const safeDist = Math.max(0.8, hits[0].distance - 0.45);
          this.vecB.copy(this.vecA).addScaledVector(this.vecC, safeDist);
        }
      }
    }

    // Exponential smoothing
    const dampPos = 1 - Math.exp(-preset.dampPosition * dt);
    const dampTgt = 1 - Math.exp(-preset.dampTarget * dt);
    this.currentPos.lerp(this.vecB, dampPos);
    this.currentTarget.lerp(this.vecA, dampTgt);

    // Trauma camera shake
    const trauma = input.trauma || 0;
    if (trauma > 0.01) {
      const shakeAmt = trauma * trauma * 0.75;
      this.currentPos.x += (Math.random() - 0.5) * shakeAmt;
      this.currentPos.y += (Math.random() - 0.5) * shakeAmt * 0.6;
      this.currentPos.z += (Math.random() - 0.5) * shakeAmt;
    }

    // Dynamic FOV calculation
    let targetFov = preset.fov;
    if (input.isAiming) {
      targetFov = 46; // Telephoto aim zoom
    } else if (input.focusReflexActive) {
      targetFov = 50; // Focus reflex cinematic tunnel
    } else if (input.inVehicle) {
      const speedBoost = Math.min(18, Math.abs(speed) * 0.65);
      targetFov = preset.fov + speedBoost;
    }

    this.currentFov += (targetFov - this.currentFov) * (1 - Math.exp(-8 * dt));

    // Dynamic banking roll in high-speed turns
    let targetRoll = 0;
    if (input.inVehicle && input.vehicleSteerAngle && Math.abs(input.vehicleSteerAngle) > 0.02) {
      const bank = -input.vehicleSteerAngle * Math.min(1.0, Math.abs(speed) / 16) * 0.055;
      targetRoll = bank;
    }
    this.currentRoll += (targetRoll - this.currentRoll) * (1 - Math.exp(-10 * dt));

    return {
      camPos: this.currentPos,
      camTarget: this.currentTarget,
      fov: this.currentFov,
      rollAngle: this.currentRoll,
    };
  }

  /**
   * 3-Stage Cinematic Sky-Cam Transition (Ascend -> Pan across Dhaka -> Descend)
   */
  private stepCharacterSwitch(sw: CharacterSwitchState, dt: number): StepCameraResult {
    sw.timer += dt;
    const progress = Math.min(1, sw.timer / sw.duration);

    // Stage 1: Ascend (0.0 to 0.32)
    // Stage 2: Pan across Dhaka skyline (0.32 to 0.68)
    // Stage 3: Descend behind target protagonist (0.68 to 1.0)
    let pX = sw.fromPos.x;
    let pY = sw.fromPos.y;
    let pZ = sw.fromPos.z;
    let tX = sw.fromPos.x;
    let tY = 1.6;
    let tZ = sw.fromPos.z;
    let fov = 58;

    if (progress < 0.32) {
      sw.phase = 'ascend';
      const t = progress / 0.32;
      const smooth = t * t * (3 - 2 * t);
      pX = sw.fromPos.x;
      pZ = sw.fromPos.z + 8 * (1 - smooth);
      pY = 4.5 + (sw.skyAltitude - 4.5) * smooth;
      tX = sw.fromPos.x;
      tY = 1.6 * (1 - smooth);
      tZ = sw.fromPos.z;
      fov = 58 + 18 * smooth;
    } else if (progress < 0.68) {
      sw.phase = 'pan';
      const t = (progress - 0.32) / 0.36;
      const smooth = t * t * (3 - 2 * t);
      pX = THREE.MathUtils.lerp(sw.fromPos.x, sw.toPos.x, smooth);
      pZ = THREE.MathUtils.lerp(sw.fromPos.z, sw.toPos.z, smooth);
      pY = sw.skyAltitude + Math.sin(smooth * Math.PI) * 12.0; // Arching panoramic flight path
      tX = THREE.MathUtils.lerp(sw.fromPos.x, sw.toPos.x, smooth);
      tY = 0;
      tZ = THREE.MathUtils.lerp(sw.fromPos.z, sw.toPos.z, smooth);
      fov = 76;
    } else {
      sw.phase = 'descend';
      const t = (progress - 0.68) / 0.32;
      const smooth = t * t * (3 - 2 * t);
      pX = sw.toPos.x;
      pZ = sw.toPos.z + 5.5 * smooth;
      pY = sw.skyAltitude - (sw.skyAltitude - 2.8) * smooth;
      tX = sw.toPos.x;
      tY = 1.6 * smooth;
      tZ = sw.toPos.z;
      fov = 76 - 18 * smooth;
    }

    if (progress >= 1.0) {
      sw.active = false;
      sw.phase = 'none';
    }

    this.currentPos.set(pX, pY, pZ);
    this.currentTarget.set(tX, tY, tZ);
    this.currentFov = fov;
    this.currentRoll = 0;

    return {
      camPos: this.currentPos,
      camTarget: this.currentTarget,
      fov: this.currentFov,
      rollAngle: this.currentRoll,
    };
  }
}
