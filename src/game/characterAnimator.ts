import * as THREE from 'three';
import { WeaponId } from './types';

export type VehicleAnimationState = 'none' | 'entering' | 'driving' | 'exiting';

export interface LocomotionBlend {
  idleWeight: number;
  walkWeight: number;
  jogWeight: number;
  sprintWeight: number;
  runWeight: number; // backward compatibility: jogWeight + sprintWeight
  walkTimeScale: number;
  jogTimeScale: number;
  runTimeScale: number;
  forwardLeanRad: number;
  bankingRad: number;
  phaseVelocity: number;
}

export interface CharacterAnimationInput {
  speed: number;
  velocity?: { x: number; y: number; z: number };
  heading?: number;
  turnRate?: number;
  maxWalkSpeed?: number;
  maxJogSpeed?: number;
  maxRunSpeed?: number;
  isGrounded?: boolean;
  isJumping?: boolean;
  isFalling?: boolean;
  isLanding?: boolean;
  verticalVelocity?: number;
  altitude?: number;
  isAiming?: boolean;
  aimPitch?: number;
  aimYaw?: number;
  isShooting?: boolean;
  weapon?: WeaponId;
  attackTriggered?: boolean;
  hitTriggered?: boolean;
  isKo?: boolean;
  isWindup?: boolean;
  vehicleState?: VehicleAnimationState;
  vehicleKind?: string | null;
  groundHeight?: number;
  isSwimming?: boolean;
}

export interface ProceduralLimbTargets {
  leftLeg: THREE.Object3D;
  rightLeg: THREE.Object3D;
  leftArm: THREE.Object3D;
  rightArm: THREE.Object3D;
  torso?: THREE.Object3D;
  head?: THREE.Object3D;
  bodyRoot: THREE.Object3D;
}

export interface TwoBoneIKSolution {
  upperAngle: number;
  lowerAngle: number;
  bendAngle: number;
  valid: boolean;
}

/**
 * Analytical Two-Bone Inverse Kinematics solver (law of cosines).
 * Solves hip-knee-ankle or shoulder-elbow-wrist joint angles given upper length, lower length,
 * and target Euclidean distance.
 */
export function solveTwoBoneIK(
  distanceToTarget: number,
  lenUpper: number,
  lenLower: number,
): TwoBoneIKSolution {
  const d = THREE.MathUtils.clamp(distanceToTarget, 0.01, (lenUpper + lenLower) * 0.999);
  const cosUpper = (lenUpper * lenUpper + d * d - lenLower * lenLower) / (2 * lenUpper * d);
  const cosLower = (lenUpper * lenUpper + lenLower * lenLower - d * d) / (2 * lenUpper * lenLower);

  const upperAngle = Math.acos(THREE.MathUtils.clamp(cosUpper, -1, 1));
  const lowerAngle = Math.acos(THREE.MathUtils.clamp(cosLower, -1, 1));
  const bendAngle = Math.PI - lowerAngle;

  return {
    upperAngle,
    lowerAngle,
    bendAngle,
    valid: true,
  };
}

export interface FootContacts {
  leftContact: number;
  rightContact: number;
  leftHeight: number;
  rightHeight: number;
}

/**
 * Derives left/right foot contact signals [0, 1] and swing clearance heights
 * directly from the continuous cyclic motion phase phi in [0, 2pi), inspired by
 * AI4Animation PFNN / deep phase manifold formulations.
 */
export function computeFootContactsFromPhase(phase: number): FootContacts {
  const cosL = Math.cos(phase);
  const cosR = Math.cos(phase - Math.PI);
  // Stance contact when cos is positive (heel-strike to toe-off)
  const leftContact = THREE.MathUtils.clamp((cosL - 0.05) / 0.65, 0, 1);
  const rightContact = THREE.MathUtils.clamp((cosR - 0.05) / 0.65, 0, 1);
  const leftHeight = Math.max(0, -cosL);
  const rightHeight = Math.max(0, -cosR);
  return { leftContact, rightContact, leftHeight, rightHeight };
}

/**
 * Pure, testable continuous multi-gait locomotion blend solver.
 * Blends Idle -> Walk -> Jog -> Sprint with smooth Hermite cubic interpolation,
 * computes cadence-matched timeScales, forward lean, trajectory banking, and phase velocity.
 */
export function computeLocomotionWeights(
  speed: number,
  maxWalkSpeed = 5.4,
  maxRunSpeed = 9.4,
  isGrounded = true,
  turnRate = 0,
): LocomotionBlend {
  const s = Math.max(0, Number.isFinite(speed) ? speed : 0);
  const walkRef = Math.max(0.5, maxWalkSpeed);
  const runRef = Math.max(walkRef + 0.5, maxRunSpeed);
  const jogRef = walkRef + (runRef - walkRef) * 0.45;

  if (!isGrounded) {
    // Airborne stride freeze with slight athletic tuck
    return {
      idleWeight: 0.15,
      walkWeight: 0.25,
      jogWeight: 0.35,
      sprintWeight: 0.25,
      runWeight: 0.6,
      walkTimeScale: 0.35,
      jogTimeScale: 0.35,
      runTimeScale: 0.35,
      forwardLeanRad: 0.08,
      bankingRad: 0,
      phaseVelocity: 1.8,
    };
  }

  let idleWeight = 0;
  let walkWeight = 0;
  let runWeight = 0;

  if (s <= 0.12) {
    idleWeight = 1;
  } else if (s < walkRef) {
    const t = THREE.MathUtils.clamp((s - 0.12) / (walkRef - 0.12), 0, 1);
    const smooth = t * t * (3 - 2 * t);
    idleWeight = 1 - smooth;
    walkWeight = smooth;
  } else if (s < runRef) {
    const t = THREE.MathUtils.clamp((s - walkRef) / (runRef - walkRef), 0, 1);
    const smooth = t * t * (3 - 2 * t);
    walkWeight = 1 - smooth;
    runWeight = smooth;
  } else {
    runWeight = 1;
  }

  const sum = idleWeight + walkWeight + runWeight || 1;
  idleWeight /= sum;
  walkWeight /= sum;
  runWeight /= sum;

  // Seamlessly subdivide runWeight into Jog and Sprint components
  let jogWeight = 0;
  let sprintWeight = 0;
  if (runWeight > 0) {
    if (s <= jogRef) {
      const t = THREE.MathUtils.clamp((s - walkRef) / Math.max(0.1, jogRef - walkRef), 0, 1);
      jogWeight = runWeight * t;
      sprintWeight = 0;
    } else {
      const t = THREE.MathUtils.clamp((s - jogRef) / Math.max(0.1, runRef - jogRef), 0, 1);
      const smooth = t * t * (3 - 2 * t);
      jogWeight = runWeight * (1 - smooth);
      sprintWeight = runWeight * smooth;
    }
  }

  // Cadence matching avoids foot sliding
  const walkTimeScale = THREE.MathUtils.clamp(0.75 + (s / walkRef) * 0.72, 0.72, 1.55);
  const jogTimeScale = THREE.MathUtils.clamp(0.8 + (s / jogRef) * 0.6, 0.8, 1.5);
  const runTimeScale = THREE.MathUtils.clamp(0.85 + (s / runRef) * 0.58, 0.85, 1.5);
  const forwardLeanRad = THREE.MathUtils.clamp((s / runRef) * 0.11, 0, 0.12);
  const rawBanking = (-turnRate * s) / 28;
  const bankingRad = Math.abs(turnRate) < 1e-5 ? 0 : THREE.MathUtils.clamp(rawBanking, -0.16, 0.16);

  // Phase velocity (rad/s) for continuous motion phase accumulation
  const phaseVelocity =
    idleWeight * 2.6 +
    walkWeight * (6.28 * walkTimeScale * 1.05) +
    jogWeight * (6.28 * jogTimeScale * 1.35) +
    sprintWeight * (6.28 * runTimeScale * 1.65);

  return {
    idleWeight,
    walkWeight,
    jogWeight,
    sprintWeight,
    runWeight,
    walkTimeScale,
    jogTimeScale,
    runTimeScale,
    forwardLeanRad,
    bankingRad,
    phaseVelocity,
  };
}

/**
 * Unified animation controller for Dhaka characters.
 * Implements AI4Animation motion phase synchronization, multi-gait locomotion blending,
 * inertia transitions (start/stop), turning/banking, vertical dynamics (jump/fall/land),
 * upper-body aiming/shooting with recoil spring, vehicle transitions, and analytical Two-Bone IK.
 * Provides full compatibility with rigged GLB models and maintains an identical procedural fallback.
 */
export class CharacterAnimator {
  private mixer: THREE.AnimationMixer | null = null;

  // Core Locomotion Actions
  private idleAction: THREE.AnimationAction | null = null;
  private walkAction: THREE.AnimationAction | null = null;
  private jogAction: THREE.AnimationAction | null = null;
  private runAction: THREE.AnimationAction | null = null;

  // Turning & Inertia Actions
  private turnLeftAction: THREE.AnimationAction | null = null;
  private turnRightAction: THREE.AnimationAction | null = null;
  private startAction: THREE.AnimationAction | null = null;
  private stopAction: THREE.AnimationAction | null = null;

  // Vertical Dynamics Actions
  private jumpAction: THREE.AnimationAction | null = null;
  private fallAction: THREE.AnimationAction | null = null;
  private landAction: THREE.AnimationAction | null = null;

  // Combat & Upper-Body Actions
  private aimAction: THREE.AnimationAction | null = null;
  private shootAction: THREE.AnimationAction | null = null;
  private attackAction: THREE.AnimationAction | null = null;
  private hitAction: THREE.AnimationAction | null = null;
  private koAction: THREE.AnimationAction | null = null;

  // Vehicle Actions
  private vehicleEnterAction: THREE.AnimationAction | null = null;
  private vehicleExitAction: THREE.AnimationAction | null = null;
  private vehicleDriveAction: THREE.AnimationAction | null = null;

  // Skinned Humanoid Bones
  private hipsBone: THREE.Object3D | null = null;
  private spineBone: THREE.Object3D | null = null;
  private chestBone: THREE.Object3D | null = null;
  private headBone: THREE.Object3D | null = null;
  private leftUpperArmBone: THREE.Object3D | null = null;
  private leftForearmBone: THREE.Object3D | null = null;
  private rightUpperArmBone: THREE.Object3D | null = null;
  private rightForearmBone: THREE.Object3D | null = null;
  private leftThighBone: THREE.Object3D | null = null;
  private leftShinBone: THREE.Object3D | null = null;
  private rightThighBone: THREE.Object3D | null = null;
  private rightShinBone: THREE.Object3D | null = null;

  // Procedural Limb Fallback Hierarchy
  private procedural: ProceduralLimbTargets | null = null;

  // Motion Synthesis State
  private phase = 0;
  private smoothedSpeed = 0;
  private previousSpeed = 0;
  private smoothedTurnRate = 0;
  private startTimer = 0;
  private stopTimer = 0;
  private wasGrounded = true;
  private verticalVel = 0;
  private jumpTimer = 0;
  private fallTimer = 0;
  private landTimer = 0;

  // Upper Body Recoil Spring
  private recoilAmount = 0;
  private recoilVelocity = 0;

  // Combat One-Shots
  private attackTimer = 0;
  private hitTimer = 0;
  private koProgress = 0;
  private koStarted = false;

  // Vehicle Transition State
  private currentVehicleState: VehicleAnimationState = 'none';
  private vehicleTimer = 0;
  private vehicleDuration = 0.6;

  private isGlbBacked = false;

  constructor(procedural: ProceduralLimbTargets) {
    this.procedural = procedural;
  }

  public isUsingGlb(): boolean {
    return this.isGlbBacked;
  }

  public getMotionPhase(): number {
    return this.phase;
  }

  public getFootContacts(): FootContacts {
    return computeFootContactsFromPhase(this.phase);
  }

  public getVehicleAnimationState(): VehicleAnimationState {
    return this.currentVehicleState;
  }

  /**
   * Binds a cloned rigged GLB root + its AnimationClips onto this animator.
   */
  public bindGlbRig(glbRoot: THREE.Object3D, clips: THREE.AnimationClip[]): void {
    this.disposeMixer();

    const mixer = new THREE.AnimationMixer(glbRoot);
    this.mixer = mixer;
    this.isGlbBacked = true;
    this.koStarted = false;

    const findClip = (name: string): THREE.AnimationClip | undefined =>
      clips.find((c) => c.name.toLowerCase() === name.toLowerCase());

    const initLoop = (clipName: string): THREE.AnimationAction | null => {
      const clip = findClip(clipName);
      if (!clip) return null;
      const action = mixer.clipAction(clip);
      action.setLoop(THREE.LoopRepeat, Infinity);
      action.enabled = true;
      action.setEffectiveWeight(0);
      action.play();
      return action;
    };

    const initOneShot = (clipName: string, clamp = false): THREE.AnimationAction | null => {
      const clip = findClip(clipName);
      if (!clip) return null;
      const action = mixer.clipAction(clip);
      action.setLoop(THREE.LoopOnce, 1);
      action.clampWhenFinished = clamp;
      return action;
    };

    // Locomotion
    this.idleAction = initLoop('Idle');
    if (this.idleAction) {
      this.idleAction.setEffectiveWeight(1);
    }
    this.walkAction = initLoop('Walk');
    this.jogAction = initLoop('Jog');
    this.runAction = initLoop('Run');

    // Turning & Inertia
    this.turnLeftAction = initLoop('TurnLeft');
    this.turnRightAction = initLoop('TurnRight');
    this.startAction = initOneShot('Start', false);
    this.stopAction = initOneShot('Stop', false);

    // Vertical Dynamics
    this.jumpAction = initOneShot('Jump', true);
    this.fallAction = initLoop('Fall');
    this.landAction = initOneShot('Land', false);

    // Combat & Upper-Body
    this.aimAction = initLoop('Aim');
    this.shootAction = initOneShot('Shoot', false);
    this.attackAction = initOneShot('Attack', false);
    this.hitAction = initOneShot('Hit', false);
    this.koAction = initOneShot('KO', true);

    // Vehicles
    this.vehicleEnterAction = initOneShot('VehicleEnter', true);
    this.vehicleExitAction = initOneShot('VehicleExit', true);
    this.vehicleDriveAction = initLoop('VehicleDrive');

    // Bone Map
    this.hipsBone = glbRoot.getObjectByName('Hips') ?? null;
    this.spineBone = glbRoot.getObjectByName('Spine') ?? null;
    this.chestBone = glbRoot.getObjectByName('Chest') ?? null;
    this.headBone = glbRoot.getObjectByName('Head') ?? null;
    this.leftUpperArmBone = glbRoot.getObjectByName('LeftUpperArm') ?? null;
    this.leftForearmBone = glbRoot.getObjectByName('LeftForearm') ?? null;
    this.rightUpperArmBone = glbRoot.getObjectByName('RightUpperArm') ?? null;
    this.rightForearmBone = glbRoot.getObjectByName('RightForearm') ?? null;
    this.leftThighBone = glbRoot.getObjectByName('LeftThigh') ?? null;
    this.leftShinBone = glbRoot.getObjectByName('LeftShin') ?? null;
    this.rightThighBone = glbRoot.getObjectByName('RightThigh') ?? null;
    this.rightShinBone = glbRoot.getObjectByName('RightShin') ?? null;
  }

  public triggerAttack(): void {
    this.attackTimer = 0.38;
    if (this.attackAction) {
      this.attackAction.reset();
      this.attackAction.enabled = true;
      this.attackAction.setEffectiveWeight(1);
      this.attackAction.fadeIn(0.04);
      this.attackAction.play();
    }
  }

  public triggerHit(): void {
    this.hitTimer = 0.32;
    if (this.hitAction) {
      this.hitAction.reset();
      this.hitAction.enabled = true;
      this.hitAction.setEffectiveWeight(0.95);
      this.hitAction.fadeIn(0.04);
      this.hitAction.play();
    }
  }

  public triggerShoot(): void {
    this.recoilAmount = 0.22;
    this.recoilVelocity = 1.9;
    if (this.shootAction) {
      this.shootAction.reset();
      this.shootAction.enabled = true;
      this.shootAction.setEffectiveWeight(1);
      this.shootAction.fadeIn(0.02);
      this.shootAction.play();
    }
  }

  public triggerStart(): void {
    this.startTimer = 0.44;
    if (this.startAction) {
      this.startAction.reset();
      this.startAction.enabled = true;
      this.startAction.setEffectiveWeight(0.85);
      this.startAction.fadeIn(0.06);
      this.startAction.play();
    }
  }

  public triggerStop(): void {
    this.stopTimer = 0.42;
    if (this.stopAction) {
      this.stopAction.reset();
      this.stopAction.enabled = true;
      this.stopAction.setEffectiveWeight(0.9);
      this.stopAction.fadeIn(0.05);
      this.stopAction.play();
    }
  }

  public triggerJump(): void {
    this.jumpTimer = 0.48;
    this.fallTimer = 0;
    this.landTimer = 0;
    if (this.jumpAction) {
      this.jumpAction.reset();
      this.jumpAction.enabled = true;
      this.jumpAction.setEffectiveWeight(1);
      this.jumpAction.fadeIn(0.05);
      this.jumpAction.play();
    }
  }

  public triggerLand(): void {
    this.landTimer = 0.36;
    this.jumpTimer = 0;
    this.fallTimer = 0;
    if (this.landAction) {
      this.landAction.reset();
      this.landAction.enabled = true;
      this.landAction.setEffectiveWeight(1);
      this.landAction.fadeIn(0.04);
      this.landAction.play();
    }
  }

  public enterVehicle(duration = 0.6): void {
    this.currentVehicleState = 'entering';
    this.vehicleDuration = duration;
    this.vehicleTimer = duration;
    if (this.vehicleEnterAction) {
      this.vehicleEnterAction.reset();
      this.vehicleEnterAction.enabled = true;
      this.vehicleEnterAction.setEffectiveWeight(1);
      this.vehicleEnterAction.fadeIn(0.06);
      this.vehicleEnterAction.play();
    }
  }

  public exitVehicle(duration = 0.55): void {
    this.currentVehicleState = 'exiting';
    this.vehicleDuration = duration;
    this.vehicleTimer = duration;
    if (this.vehicleExitAction) {
      this.vehicleExitAction.reset();
      this.vehicleExitAction.enabled = true;
      this.vehicleExitAction.setEffectiveWeight(1);
      this.vehicleExitAction.fadeIn(0.05);
      this.vehicleExitAction.play();
    }
  }

  public resetPose(): void {
    this.smoothedSpeed = 0;
    this.previousSpeed = 0;
    this.smoothedTurnRate = 0;
    this.phase = 0;
    this.attackTimer = 0;
    this.hitTimer = 0;
    this.startTimer = 0;
    this.stopTimer = 0;
    this.jumpTimer = 0;
    this.fallTimer = 0;
    this.landTimer = 0;
    this.recoilAmount = 0;
    this.recoilVelocity = 0;
    this.koProgress = 0;
    this.koStarted = false;
    this.currentVehicleState = 'none';
    this.vehicleTimer = 0;

    if (this.koAction) this.koAction.stop();
    if (this.attackAction) this.attackAction.stop();
    if (this.hitAction) this.hitAction.stop();
    if (this.startAction) this.startAction.stop();
    if (this.stopAction) this.stopAction.stop();
    if (this.jumpAction) this.jumpAction.stop();
    if (this.fallAction) this.fallAction.stop();
    if (this.landAction) this.landAction.stop();
    if (this.vehicleEnterAction) this.vehicleEnterAction.stop();
    if (this.vehicleExitAction) this.vehicleExitAction.stop();
    if (this.vehicleDriveAction) this.vehicleDriveAction.stop();

    if (this.procedural) {
      this.procedural.bodyRoot.rotation.set(0, 0, 0);
      this.procedural.bodyRoot.position.set(0, 0, 0);
      this.procedural.leftLeg.rotation.set(0, 0, 0);
      this.procedural.rightLeg.rotation.set(0, 0, 0);
      this.procedural.leftArm.rotation.set(0, 0, 0);
      this.procedural.rightArm.rotation.set(0, 0, 0);
      if (this.procedural.torso) this.procedural.torso.rotation.set(0, 0, 0);
      if (this.procedural.head) this.procedural.head.rotation.set(0, 0, 0);
    }
  }

  public update(dt: number, input: CharacterAnimationInput): void {
    const safeDt = Math.min(Math.max(dt, 0), 0.06);
    const actualDt = Math.max(0, dt);

    // Edge triggers
    if (input.attackTriggered) this.triggerAttack();
    if (input.hitTriggered) this.triggerHit();
    if (input.isShooting) this.triggerShoot();

    // Recoil spring decay: critically damped 2nd order harmonic oscillator
    if (this.recoilAmount > 0.001 || Math.abs(this.recoilVelocity) > 0.001) {
      const springFreq = 22;
      const dampingRatio = 1.0;
      const accel = -springFreq * springFreq * this.recoilAmount - 2 * dampingRatio * springFreq * this.recoilVelocity;
      this.recoilVelocity += accel * safeDt;
      this.recoilAmount += this.recoilVelocity * safeDt;
      if (this.recoilAmount < 0) {
        this.recoilAmount = 0;
        this.recoilVelocity = 0;
      }
    }

    if (this.attackTimer > 0) this.attackTimer = Math.max(0, this.attackTimer - actualDt);
    if (this.hitTimer > 0) this.hitTimer = Math.max(0, this.hitTimer - actualDt);
    if (this.startTimer > 0) this.startTimer = Math.max(0, this.startTimer - actualDt);
    if (this.stopTimer > 0) this.stopTimer = Math.max(0, this.stopTimer - actualDt);

    // Critically damped speed & turn rate smoothing eliminates sudden gait pops
    const speedDamp = 1 - Math.exp(-12 * safeDt);
    const prevSmoothed = this.smoothedSpeed;
    this.smoothedSpeed += (input.speed - this.smoothedSpeed) * speedDamp;

    const turnRate = input.turnRate ?? 0;
    const turnDamp = 1 - Math.exp(-10 * safeDt);
    this.smoothedTurnRate += (turnRate - this.smoothedTurnRate) * turnDamp;

    // Detect inertial start push-off or stop braking transitions
    const speedAccel = (this.smoothedSpeed - prevSmoothed) / Math.max(1e-4, safeDt);
    if (speedAccel > 4.2 && prevSmoothed < 0.6 && this.startTimer <= 0) {
      this.triggerStart();
    } else if (speedAccel < -5.0 && this.smoothedSpeed < 0.5 && prevSmoothed > 1.2 && this.stopTimer <= 0) {
      this.triggerStop();
    }
    this.previousSpeed = this.smoothedSpeed;

    // Vertical state tracking (Airborne, Jumping, Falling, Landing)
    const isGrounded = input.isGrounded ?? true;
    this.verticalVel = input.verticalVelocity ?? (isGrounded ? 0 : this.verticalVel);

    if (input.isJumping || (!isGrounded && this.wasGrounded && this.verticalVel > 1.0)) {
      this.triggerJump();
    } else if (!isGrounded && this.verticalVel < -1.8) {
      this.fallTimer = 0.5;
    } else if (isGrounded && !this.wasGrounded) {
      this.triggerLand();
    }
    this.wasGrounded = isGrounded;

    if (this.jumpTimer > 0) this.jumpTimer = Math.max(0, this.jumpTimer - actualDt);
    if (this.fallTimer > 0) this.fallTimer = Math.max(0, this.fallTimer - actualDt);
    if (this.landTimer > 0) this.landTimer = Math.max(0, this.landTimer - actualDt);

    // Vehicle state progression
    if (input.vehicleState && input.vehicleState !== this.currentVehicleState) {
      if (input.vehicleState === 'entering') {
        this.enterVehicle(0.64);
      } else if (input.vehicleState === 'exiting') {
        this.exitVehicle(0.56);
      } else {
        this.currentVehicleState = input.vehicleState;
      }
    }

    if (this.vehicleTimer > 0) {
      this.vehicleTimer = Math.max(0, this.vehicleTimer - actualDt);
      if (this.vehicleTimer <= 0) {
        if (this.currentVehicleState === 'entering') {
          this.currentVehicleState = 'driving';
        } else if (this.currentVehicleState === 'exiting') {
          this.currentVehicleState = 'none';
        }
      }
    }

    // Solve continuous locomotion blend weights and phase velocity
    const blend = computeLocomotionWeights(
      this.smoothedSpeed,
      input.maxWalkSpeed ?? 5.4,
      input.maxRunSpeed ?? 9.4,
      isGrounded,
      this.smoothedTurnRate,
    );

    // Continuous motion phase accumulator: wraps at 2*PI
    this.phase = (this.phase + blend.phaseVelocity * safeDt) % (Math.PI * 2);

    if (this.isGlbBacked && this.mixer) {
      this.updateGlbMixer(safeDt, input, blend);
    } else if (this.procedural) {
      this.updateProceduralFallback(safeDt, input, blend);
    }
  }

  private updateGlbMixer(dt: number, input: CharacterAnimationInput, blend: LocomotionBlend): void {
    if (!this.mixer) return;

    if (input.isKo) {
      if (!this.koStarted && this.koAction) {
        this.koStarted = true;
        this.idleAction?.fadeOut(0.12);
        this.walkAction?.fadeOut(0.12);
        this.jogAction?.fadeOut(0.12);
        this.runAction?.fadeOut(0.12);
        this.attackAction?.stop();
        this.hitAction?.stop();
        this.koAction.reset();
        this.koAction.enabled = true;
        this.koAction.setEffectiveWeight(1);
        this.koAction.fadeIn(0.08);
        this.koAction.play();
      }
      this.mixer.update(dt);
      return;
    }

    // 1. Vehicle Animation Overrides
    if (this.currentVehicleState === 'entering') {
      this.idleAction?.setEffectiveWeight(0);
      this.walkAction?.setEffectiveWeight(0);
      this.runAction?.setEffectiveWeight(0);
      this.vehicleEnterAction?.setEffectiveWeight(1);
      this.mixer.update(dt);
      return;
    } else if (this.currentVehicleState === 'driving') {
      this.idleAction?.setEffectiveWeight(0);
      this.walkAction?.setEffectiveWeight(0);
      this.runAction?.setEffectiveWeight(0);
      this.vehicleEnterAction?.setEffectiveWeight(0);
      this.vehicleDriveAction?.setEffectiveWeight(1);
      this.mixer.update(dt);
      return;
    } else if (this.currentVehicleState === 'exiting') {
      this.idleAction?.setEffectiveWeight(0);
      this.walkAction?.setEffectiveWeight(0);
      this.runAction?.setEffectiveWeight(0);
      this.vehicleDriveAction?.setEffectiveWeight(0);
      this.vehicleExitAction?.setEffectiveWeight(1);
      this.mixer.update(dt);
      return;
    } else {
      this.vehicleEnterAction?.setEffectiveWeight(0);
      this.vehicleDriveAction?.setEffectiveWeight(0);
      this.vehicleExitAction?.setEffectiveWeight(0);
    }

    // 2. Vertical Dynamics (Airborne / Landing overrides)
    const isAirborne = !(input.isGrounded ?? true);
    if (isAirborne) {
      if (this.verticalVel > 0.5 && this.jumpAction) {
        this.jumpAction.setEffectiveWeight(0.9);
        this.fallAction?.setEffectiveWeight(0);
      } else if (this.fallAction) {
        this.jumpAction?.setEffectiveWeight(0);
        this.fallAction.setEffectiveWeight(0.9);
      }
    } else {
      this.jumpAction?.setEffectiveWeight(0);
      this.fallAction?.setEffectiveWeight(0);
      if (this.landTimer > 0 && this.landAction) {
        this.landAction.setEffectiveWeight(Math.min(1, this.landTimer / 0.2));
      } else {
        this.landAction?.setEffectiveWeight(0);
      }
    }

    // 3. Multi-Gait Locomotion Blending & Phase Synchronization
    const oneShotActive =
      (this.attackAction && this.attackAction.isRunning()) ||
      (this.hitAction && this.hitAction.isRunning()) ||
      this.landTimer > 0;
    const locoScale = oneShotActive ? 0.45 : isAirborne ? 0.25 : 1.0;

    // AI4Animation Phase Synchronization: synchronizes clip playback timestamps to shared phase phi
    const syncActionPhase = (action: THREE.AnimationAction | null): void => {
      if (!action) return;
      const dur = action.getClip().duration;
      action.time = (this.phase / (Math.PI * 2)) * dur;
    };

    if (this.idleAction) {
      this.idleAction.setEffectiveWeight(blend.idleWeight * locoScale);
    }
    if (this.walkAction) {
      this.walkAction.setEffectiveWeight(blend.walkWeight * locoScale);
      this.walkAction.setEffectiveTimeScale(blend.walkTimeScale);
      syncActionPhase(this.walkAction);
    }
    if (this.jogAction) {
      this.jogAction.setEffectiveWeight(blend.jogWeight * locoScale);
      this.jogAction.setEffectiveTimeScale(blend.jogTimeScale);
      syncActionPhase(this.jogAction);
    }
    if (this.runAction) {
      // If Jog clip is bound, runAction plays sprintWeight; otherwise full runWeight for fallback
      const sprintW = this.jogAction ? blend.sprintWeight : blend.runWeight;
      this.runAction.setEffectiveWeight(sprintW * locoScale);
      this.runAction.setEffectiveTimeScale(blend.runTimeScale);
      syncActionPhase(this.runAction);
    }

    // 4. In-Place Turning Actions
    if (!isAirborne && this.smoothedSpeed < 0.35 && Math.abs(this.smoothedTurnRate) > 0.45) {
      const turnW = Math.min(0.85, Math.abs(this.smoothedTurnRate) * 0.4);
      if (this.smoothedTurnRate > 0 && this.turnRightAction) {
        this.turnRightAction.setEffectiveWeight(turnW);
        this.turnLeftAction?.setEffectiveWeight(0);
      } else if (this.smoothedTurnRate < 0 && this.turnLeftAction) {
        this.turnLeftAction.setEffectiveWeight(turnW);
        this.turnRightAction?.setEffectiveWeight(0);
      }
    } else {
      this.turnLeftAction?.setEffectiveWeight(0);
      this.turnRightAction?.setEffectiveWeight(0);
    }

    // 5. Inertia Actions (Start push-off & Stop braking)
    if (this.startTimer > 0 && this.startAction) {
      this.startAction.setEffectiveWeight(Math.min(1, this.startTimer / 0.2));
    }
    if (this.stopTimer > 0 && this.stopAction) {
      this.stopAction.setEffectiveWeight(Math.min(1, this.stopTimer / 0.2));
    }

    this.mixer.update(dt);

    // 6. Procedural Bone Overlays: Banking, Aiming, Recoil & Two-Bone Leg IK
    if (this.hipsBone) {
      this.hipsBone.rotation.z = blend.bankingRad;
      if (this.landTimer > 0) {
        const landRatio = this.landTimer / 0.36;
        this.hipsBone.position.y = 0.9 - Math.sin(landRatio * Math.PI) * 0.16;
      }
    }

    // Aiming & Shooting look-at orientation
    const isAiming = Boolean(input.isAiming || (input.weapon && input.weapon !== 'fists'));
    const aimPitch = (input.aimPitch ?? 0) + this.recoilAmount;
    const aimYaw = input.aimYaw ?? 0;

    if (isAiming) {
      if (this.spineBone) {
        this.spineBone.rotation.y += aimYaw * 0.35;
        this.spineBone.rotation.x += aimPitch * 0.35;
      }
      if (this.chestBone) {
        this.chestBone.rotation.y += aimYaw * 0.45;
        this.chestBone.rotation.x += aimPitch * 0.45;
      }
      if (this.headBone) {
        this.headBone.rotation.y += aimYaw * 0.2;
        this.headBone.rotation.x += aimPitch * 0.2;
      }
      if (this.rightUpperArmBone && this.attackTimer <= 0.02) {
        this.rightUpperArmBone.rotation.x = -1.1 + aimPitch * 0.25;
        this.rightUpperArmBone.rotation.y = -0.12 + aimYaw * 0.2;
        if (this.rightForearmBone) {
          this.rightForearmBone.rotation.x = -0.22 - this.recoilAmount * 0.6;
        }
      }
      if (this.leftUpperArmBone && this.attackTimer <= 0.02) {
        this.leftUpperArmBone.rotation.x = -0.8 + aimPitch * 0.2;
        this.leftUpperArmBone.rotation.y = 0.35 + aimYaw * 0.2;
        if (this.leftForearmBone) {
          this.leftForearmBone.rotation.x = -0.9;
        }
      }
    } else if (input.isWindup) {
      if (this.rightUpperArmBone) this.rightUpperArmBone.rotation.x = -1.45;
      if (this.rightForearmBone) this.rightForearmBone.rotation.x = -0.35;
      if (this.spineBone) this.spineBone.rotation.y += 0.25;
    }

    // Two-Bone IK Foot Ground Clamping (adapts knees to ground without sliding)
    if (!isAirborne && (this.leftShinBone || this.rightShinBone)) {
      const contacts = computeFootContactsFromPhase(this.phase);
      const thLen = 0.43;
      const shLen = 0.39;
      if (this.leftShinBone && contacts.leftContact > 0.4) {
        const ik = solveTwoBoneIK(0.8 - contacts.leftHeight * 0.12, thLen, shLen);
        this.leftShinBone.rotation.x = ik.bendAngle * 0.65;
      }
      if (this.rightShinBone && contacts.rightContact > 0.4) {
        const ik = solveTwoBoneIK(0.8 - contacts.rightHeight * 0.12, thLen, shLen);
        this.rightShinBone.rotation.x = ik.bendAngle * 0.65;
      }
    }
  }

  private updateProceduralFallback(
    dt: number,
    input: CharacterAnimationInput,
    blend: LocomotionBlend,
  ): void {
    const p = this.procedural;
    if (!p) return;

    if (input.isKo) {
      this.koProgress = Math.min(1, this.koProgress + dt / 0.48);
      p.bodyRoot.rotation.x = this.koProgress * (Math.PI / 2);
      p.bodyRoot.position.y = -this.koProgress * 0.45;
      return;
    }

    // Vehicle procedural states
    if (this.currentVehicleState === 'driving') {
      if (input.vehicleKind === 'launch') {
        // Standing at the wheelhouse bridge helm
        p.bodyRoot.position.y = -0.05;
        p.bodyRoot.rotation.set(0, 0, 0);
        p.leftLeg.rotation.set(0, 0, -0.08);
        p.rightLeg.rotation.set(0, 0, 0.08);
        p.leftArm.rotation.set(-0.75, 0.22, 0);
        p.rightArm.rotation.set(-0.75, -0.22, 0);
        if (p.torso) {
          p.torso.rotation.x = 0.04;
          p.torso.rotation.z = blend.bankingRad * 0.35;
        }
      } else if (input.vehicleKind === 'jetski') {
        // Athletic forward crouch gripping handlebars
        p.bodyRoot.position.y = -0.32;
        p.bodyRoot.rotation.set(0, 0, 0);
        p.leftLeg.rotation.set(-1.15, 0, -0.15);
        p.rightLeg.rotation.set(-1.15, 0, 0.15);
        p.leftArm.rotation.set(-0.95, 0.32, 0);
        p.rightArm.rotation.set(-0.95, -0.32, 0);
        if (p.torso) {
          p.torso.rotation.x = 0.32;
          p.torso.rotation.z = blend.bankingRad * 0.85;
        }
      } else {
        p.bodyRoot.position.y = -0.44;
        p.bodyRoot.rotation.set(0, 0, 0);
        p.leftLeg.rotation.set(-1.42, 0, -0.12);
        p.rightLeg.rotation.set(-1.42, 0, 0.12);
        p.leftArm.rotation.set(-0.72, 0.18, 0);
        p.rightArm.rotation.set(-0.72, -0.18, 0);
        if (p.torso) {
          p.torso.rotation.x = 0.08 + Math.sin(this.phase * 2) * 0.015;
          p.torso.rotation.z = blend.bankingRad * 0.7;
        }
      }
      return;
    } else if (this.currentVehicleState === 'entering') {
      const prog = Math.min(1, (this.vehicleDuration - this.vehicleTimer) / this.vehicleDuration);
      p.bodyRoot.position.y = -prog * 0.44;
      p.leftLeg.rotation.x = -prog * 1.42;
      p.rightLeg.rotation.x = -prog * 1.42;
      p.rightArm.rotation.x = -0.5 - prog * 0.22;
      return;
    } else if (this.currentVehicleState === 'exiting') {
      const prog = Math.min(1, (this.vehicleDuration - this.vehicleTimer) / this.vehicleDuration);
      p.bodyRoot.position.y = -(1 - prog) * 0.44;
      p.leftLeg.rotation.x = -(1 - prog) * 1.42;
      p.rightLeg.rotation.x = -(1 - prog) * 1.42;
      return;
    }

    // Swimming in river water
    if (input.isSwimming) {
      p.bodyRoot.position.y = -0.48; // Submerged to chest
      p.bodyRoot.rotation.x = 0.72; // Prone water angle
      const strokeL = Math.sin(this.phase) * 0.75;
      const strokeR = Math.sin(this.phase + Math.PI) * 0.75;
      p.leftArm.rotation.set(-1.05 + strokeL * 0.45, 0.35, 0);
      p.rightArm.rotation.set(-1.05 + strokeR * 0.45, -0.35, 0);
      p.leftLeg.rotation.set(-0.25 + strokeR * 0.35, 0, 0);
      p.rightLeg.rotation.set(-0.25 + strokeL * 0.35, 0, 0);
      if (p.torso) p.torso.rotation.set(0.12, 0, 0);
      return;
    }

    // Airborne / Vertical dynamics procedural handling
    const isAirborne = !(input.isGrounded ?? true);
    if (isAirborne) {
      if (this.verticalVel > 0.5) {
        // Jump takeoff stretch & athletic tuck
        p.bodyRoot.position.y = 0.08;
        p.leftLeg.rotation.x = -0.45;
        p.rightLeg.rotation.x = -0.45;
        p.leftArm.rotation.x = 0.55;
        p.rightArm.rotation.x = 0.55;
      } else {
        // Fall descent: limbs extend, arms spread sideways
        p.bodyRoot.position.y = 0.02;
        p.leftLeg.rotation.x = -0.22;
        p.rightLeg.rotation.x = -0.22;
        p.leftArm.rotation.z = -0.48;
        p.rightArm.rotation.z = 0.48;
      }
      if (p.torso) p.torso.rotation.x = -0.06;
      return;
    }

    // Landing squash & recovery
    let landingCompression = 0;
    if (this.landTimer > 0) {
      const landRatio = this.landTimer / 0.36;
      landingCompression = Math.sin(landRatio * Math.PI) * 0.18;
    }
    p.bodyRoot.position.y = landingCompression > 1e-5 ? -landingCompression : 0;
    p.bodyRoot.rotation.z = blend.bankingRad;

    // Phase-driven rhythmic locomotion swing
    const strideAmp = blend.walkWeight * 0.62 + blend.jogWeight * 0.78 + blend.sprintWeight * 1.05;
    const swing = Math.sin(this.phase) * strideAmp;
    const idleBreath = Math.sin(this.phase * 0.8) * 0.025 * blend.idleWeight;

    // In-place turn step swing modulation
    let turnStepL = 0;
    let turnStepR = 0;
    if (this.smoothedSpeed < 0.3 && Math.abs(this.smoothedTurnRate) > 0.4) {
      const stepPhase = Math.sin(this.phase * 1.4) * 0.32;
      if (this.smoothedTurnRate > 0) turnStepR = stepPhase;
      else turnStepL = stepPhase;
    }

    p.leftLeg.rotation.x = swing + turnStepL;
    p.rightLeg.rotation.x = -swing + turnStepR;
    p.leftArm.rotation.x = -swing * 0.85 + idleBreath;

    if (p.torso) {
      const inertiaPitch =
        this.startTimer > 0 ? 0.16 : this.stopTimer > 0 ? -0.12 : this.hitTimer > 0 ? -0.22 : 0;
      p.torso.rotation.x = blend.forwardLeanRad + inertiaPitch + landingCompression * 0.8;
      p.torso.rotation.y = Math.sin(this.phase) * strideAmp * 0.14 + (this.smoothedTurnRate * 0.12);
      p.torso.rotation.z = blend.bankingRad;
    }

    // Aiming & Shooting upper body overlay
    const isAiming = Boolean(input.isAiming || (input.weapon && input.weapon !== 'fists'));
    const aimPitch = (input.aimPitch ?? 0) + this.recoilAmount;

    if (input.isWindup) {
      p.rightArm.rotation.x = -Math.PI * 0.62;
    } else if (this.attackTimer > 0) {
      const atkRatio = this.attackTimer / 0.38;
      p.rightArm.rotation.x = -Math.PI * 0.48 - atkRatio * 0.55;
    } else if (isAiming) {
      p.rightArm.rotation.x = -Math.PI * 0.38 - aimPitch * 0.5;
      p.leftArm.rotation.x = -Math.PI * 0.28 - aimPitch * 0.4;
    } else {
      p.rightArm.rotation.x = swing * 0.85 - idleBreath;
    }
  }

  public disposeMixer(): void {
    if (this.mixer) {
      this.mixer.stopAllAction();
      this.mixer.uncacheRoot(this.mixer.getRoot());
      this.mixer = null;
    }
    this.idleAction = null;
    this.walkAction = null;
    this.jogAction = null;
    this.runAction = null;
    this.turnLeftAction = null;
    this.turnRightAction = null;
    this.startAction = null;
    this.stopAction = null;
    this.jumpAction = null;
    this.fallAction = null;
    this.landAction = null;
    this.aimAction = null;
    this.shootAction = null;
    this.attackAction = null;
    this.hitAction = null;
    this.koAction = null;
    this.vehicleEnterAction = null;
    this.vehicleExitAction = null;
    this.vehicleDriveAction = null;

    this.hipsBone = null;
    this.spineBone = null;
    this.chestBone = null;
    this.headBone = null;
    this.leftUpperArmBone = null;
    this.leftForearmBone = null;
    this.rightUpperArmBone = null;
    this.rightForearmBone = null;
    this.leftThighBone = null;
    this.leftShinBone = null;
    this.rightThighBone = null;
    this.rightShinBone = null;
    this.isGlbBacked = false;
  }
}
