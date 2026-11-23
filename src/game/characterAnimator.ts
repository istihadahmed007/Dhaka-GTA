import * as THREE from 'three';
import { WeaponId } from './types';

export interface LocomotionBlend {
  idleWeight: number;
  walkWeight: number;
  runWeight: number;
  walkTimeScale: number;
  runTimeScale: number;
  forwardLeanRad: number;
}

export interface CharacterAnimationInput {
  speed: number;
  maxWalkSpeed?: number;
  maxRunSpeed?: number;
  isGrounded?: boolean;
  isAiming?: boolean;
  weapon?: WeaponId;
  attackTriggered?: boolean;
  hitTriggered?: boolean;
  isKo?: boolean;
  isWindup?: boolean;
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

/**
 * Pure, testable continuous locomotion blend solver (inspired by velocity-driven
 * phase blending: smooth Idle -> Walk -> Run weight transitions + cadence-matched timeScale).
 */
export function computeLocomotionWeights(
  speed: number,
  maxWalkSpeed = 5.4,
  maxRunSpeed = 9.4,
  isGrounded = true,
): LocomotionBlend {
  const s = Math.max(0, Number.isFinite(speed) ? speed : 0);
  const walkRef = Math.max(0.5, maxWalkSpeed);
  const runRef = Math.max(walkRef + 0.5, maxRunSpeed);

  if (!isGrounded) {
    // Airborne stride freeze with slight athletic tuck
    return {
      idleWeight: 0.15,
      walkWeight: 0.25,
      runWeight: 0.6,
      walkTimeScale: 0.35,
      runTimeScale: 0.35,
      forwardLeanRad: 0.08,
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

  // Scale clip cadence smoothly to actual horizontal displacement so feet avoid sliding
  const walkTimeScale = THREE.MathUtils.clamp(0.75 + (s / walkRef) * 0.72, 0.72, 1.55);
  const runTimeScale = THREE.MathUtils.clamp(0.85 + (s / runRef) * 0.58, 0.85, 1.5);
  const forwardLeanRad = THREE.MathUtils.clamp((s / runRef) * 0.11, 0, 0.12);

  return {
    idleWeight,
    walkWeight,
    runWeight,
    walkTimeScale,
    runTimeScale,
    forwardLeanRad,
  };
}

/**
 * Unified animation controller for Dhaka characters.
 * Drives THREE.AnimationMixer with smooth Idle/Walk/Run blending + crossfaded Attack/Hit/KO clips
 * when a rigged GLB is attached, and smoothly animates procedural fallback limbs otherwise.
 */
export class CharacterAnimator {
  private mixer: THREE.AnimationMixer | null = null;
  private idleAction: THREE.AnimationAction | null = null;
  private walkAction: THREE.AnimationAction | null = null;
  private runAction: THREE.AnimationAction | null = null;
  private attackAction: THREE.AnimationAction | null = null;
  private hitAction: THREE.AnimationAction | null = null;
  private koAction: THREE.AnimationAction | null = null;

  private rightUpperArmBone: THREE.Object3D | null = null;
  private rightForearmBone: THREE.Object3D | null = null;
  private spineBone: THREE.Object3D | null = null;

  private procedural: ProceduralLimbTargets | null = null;
  private phase = 0;
  private smoothedSpeed = 0;
  private attackTimer = 0;
  private hitTimer = 0;
  private koProgress = 0;
  private koStarted = false;
  private isGlbBacked = false;

  constructor(procedural: ProceduralLimbTargets) {
    this.procedural = procedural;
  }

  public isUsingGlb(): boolean {
    return this.isGlbBacked;
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

    const idleClip = findClip('Idle');
    const walkClip = findClip('Walk');
    const runClip = findClip('Run');
    const attackClip = findClip('Attack');
    const hitClip = findClip('Hit');
    const koClip = findClip('KO');

    if (idleClip) {
      this.idleAction = mixer.clipAction(idleClip);
      this.idleAction.setLoop(THREE.LoopRepeat, Infinity);
      this.idleAction.enabled = true;
      this.idleAction.setEffectiveWeight(1);
      this.idleAction.play();
    }
    if (walkClip) {
      this.walkAction = mixer.clipAction(walkClip);
      this.walkAction.setLoop(THREE.LoopRepeat, Infinity);
      this.walkAction.enabled = true;
      this.walkAction.setEffectiveWeight(0);
      this.walkAction.play();
    }
    if (runClip) {
      this.runAction = mixer.clipAction(runClip);
      this.runAction.setLoop(THREE.LoopRepeat, Infinity);
      this.runAction.enabled = true;
      this.runAction.setEffectiveWeight(0);
      this.runAction.play();
    }
    if (attackClip) {
      this.attackAction = mixer.clipAction(attackClip);
      this.attackAction.setLoop(THREE.LoopOnce, 1);
      this.attackAction.clampWhenFinished = false;
    }
    if (hitClip) {
      this.hitAction = mixer.clipAction(hitClip);
      this.hitAction.setLoop(THREE.LoopOnce, 1);
      this.hitAction.clampWhenFinished = false;
    }
    if (koClip) {
      this.koAction = mixer.clipAction(koClip);
      this.koAction.setLoop(THREE.LoopOnce, 1);
      this.koAction.clampWhenFinished = true;
    }

    this.rightUpperArmBone = glbRoot.getObjectByName('RightUpperArm') ?? null;
    this.rightForearmBone = glbRoot.getObjectByName('RightForearm') ?? null;
    this.spineBone = glbRoot.getObjectByName('Spine') ?? null;
  }

  public triggerAttack(): void {
    this.attackTimer = 0.36;
    if (this.attackAction) {
      this.attackAction.reset();
      this.attackAction.enabled = true;
      this.attackAction.setEffectiveWeight(1);
      this.attackAction.fadeIn(0.05);
      this.attackAction.play();
    }
  }

  public triggerHit(): void {
    this.hitTimer = 0.28;
    if (this.hitAction) {
      this.hitAction.reset();
      this.hitAction.enabled = true;
      this.hitAction.setEffectiveWeight(0.9);
      this.hitAction.fadeIn(0.04);
      this.hitAction.play();
    }
  }

  public resetPose(): void {
    this.smoothedSpeed = 0;
    this.attackTimer = 0;
    this.hitTimer = 0;
    this.koProgress = 0;
    this.koStarted = false;
    if (this.koAction) {
      this.koAction.stop();
    }
    if (this.attackAction) {
      this.attackAction.stop();
    }
    if (this.hitAction) {
      this.hitAction.stop();
    }
    if (this.procedural) {
      this.procedural.bodyRoot.rotation.set(0, 0, 0);
      this.procedural.bodyRoot.position.y = 0;
    }
  }

  public update(dt: number, input: CharacterAnimationInput): void {
    const safeDt = Math.min(Math.max(dt, 0), 0.06);
    if (input.attackTriggered) this.triggerAttack();
    if (input.hitTriggered) this.triggerHit();

    if (this.attackTimer > 0) this.attackTimer = Math.max(0, this.attackTimer - safeDt);
    if (this.hitTimer > 0) this.hitTimer = Math.max(0, this.hitTimer - safeDt);

    // Critically damped speed smoothing eliminates sudden gait pops
    const damp = 1 - Math.exp(-12 * safeDt);
    this.smoothedSpeed += (input.speed - this.smoothedSpeed) * damp;

    if (this.isGlbBacked && this.mixer) {
      this.updateGlbMixer(safeDt, input);
    } else if (this.procedural) {
      this.updateProceduralFallback(safeDt, input);
    }
  }

  private updateGlbMixer(dt: number, input: CharacterAnimationInput): void {
    if (!this.mixer) return;

    if (input.isKo) {
      if (!this.koStarted && this.koAction) {
        this.koStarted = true;
        this.idleAction?.fadeOut(0.12);
        this.walkAction?.fadeOut(0.12);
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

    const blend = computeLocomotionWeights(
      this.smoothedSpeed,
      input.maxWalkSpeed ?? 5.4,
      input.maxRunSpeed ?? 9.4,
      input.isGrounded ?? true,
    );

    // Smoothly attenuate base locomotion during full-body Hit or Attack snap
    const oneShotActive =
      (this.attackAction && this.attackAction.isRunning()) || (this.hitAction && this.hitAction.isRunning());
    const locoScale = oneShotActive ? 0.55 : 1.0;

    if (this.idleAction) {
      this.idleAction.setEffectiveWeight(blend.idleWeight * locoScale);
    }
    if (this.walkAction) {
      this.walkAction.setEffectiveWeight(blend.walkWeight * locoScale);
      this.walkAction.setEffectiveTimeScale(blend.walkTimeScale);
    }
    if (this.runAction) {
      this.runAction.setEffectiveWeight(blend.runWeight * locoScale);
      this.runAction.setEffectiveTimeScale(blend.runTimeScale);
    }

    // Fade out one-shot actions smoothly near end of clip
    if (this.attackAction && this.attackAction.isRunning()) {
      const rem = this.attackAction.getClip().duration - this.attackAction.time;
      if (rem < 0.08) {
        this.attackAction.setEffectiveWeight(Math.max(0, rem / 0.08));
      }
    }
    if (this.hitAction && this.hitAction.isRunning()) {
      const rem = this.hitAction.getClip().duration - this.hitAction.time;
      if (rem < 0.08) {
        this.hitAction.setEffectiveWeight(Math.max(0, rem / 0.08));
      }
    }

    this.mixer.update(dt);

    // Crisp procedural upper-body overlay for aiming firearm or heavy attack windup
    const holdingGun = input.weapon && input.weapon !== 'fists';
    if (this.rightUpperArmBone && (holdingGun || input.isAiming || input.isWindup) && this.attackTimer <= 0.02) {
      this.rightUpperArmBone.rotation.x = input.isWindup ? -1.45 : -1.08;
      if (this.rightForearmBone) {
        this.rightForearmBone.rotation.x = input.isWindup ? -0.35 : -0.18;
      }
    }
    if (this.spineBone && input.isWindup) {
      this.spineBone.rotation.y += 0.25;
    }
  }

  private updateProceduralFallback(dt: number, input: CharacterAnimationInput): void {
    const p = this.procedural;
    if (!p) return;

    if (input.isKo) {
      this.koProgress = Math.min(1, this.koProgress + dt / 0.48);
      p.bodyRoot.rotation.x = this.koProgress * (Math.PI / 2);
      p.bodyRoot.position.y = -this.koProgress * 0.45;
      return;
    }

    const blend = computeLocomotionWeights(
      this.smoothedSpeed,
      input.maxWalkSpeed ?? 5.4,
      input.maxRunSpeed ?? 9.4,
      input.isGrounded ?? true,
    );

    const cadence = 1.6 + blend.walkWeight * 5.8 + blend.runWeight * 9.8;
    this.phase += dt * cadence;

    const strideAmp = blend.walkWeight * 0.62 + blend.runWeight * 0.96;
    const swing = Math.sin(this.phase) * strideAmp;
    const idleBreath = Math.sin(this.phase * 1.1) * 0.025 * blend.idleWeight;

    p.leftLeg.rotation.x = swing;
    p.rightLeg.rotation.x = -swing;
    p.leftArm.rotation.x = -swing * 0.85 + idleBreath;

    if (p.torso) {
      p.torso.rotation.x = blend.forwardLeanRad + (this.hitTimer > 0 ? -0.22 : 0);
      p.torso.rotation.y = Math.sin(this.phase) * strideAmp * 0.14;
    }

    if (input.isWindup) {
      p.rightArm.rotation.x = -Math.PI * 0.62;
    } else if (this.attackTimer > 0) {
      const atkRatio = this.attackTimer / 0.36;
      p.rightArm.rotation.x = -Math.PI * 0.48 - atkRatio * 0.55;
    } else if ((input.weapon && input.weapon !== 'fists') || input.isAiming) {
      p.rightArm.rotation.x = -Math.PI * 0.36;
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
    this.runAction = null;
    this.attackAction = null;
    this.hitAction = null;
    this.koAction = null;
    this.rightUpperArmBone = null;
    this.rightForearmBone = null;
    this.spineBone = null;
    this.isGlbBacked = false;
  }
}
