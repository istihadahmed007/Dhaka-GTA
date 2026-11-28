# PART 7 — UNITY CHARACTER SYSTEM AND AAA ANIMATION (TARIQ & MAYA)

## Overview
Part 7 replaces placeholder capsule geometry and procedural/TS limb loops with a production Humanoid character architecture built natively in **Unity 6 HDRP C#** (`DhakaGta.Character`, `DhakaGta.Data`, `DhakaGta.Core`, `DhakaGta.Editor`) paired with a **Blender anatomical FBX pipeline** (`BlenderPipeline/generate_dhaka_characters.py`).

Inspired directly by **Sebastian Starke's `ai4animation` research** (DeepPhase, Local Motion Phases, Foot Contact Labeling, and Trajectory-Conditioned Codebook Matching), player motion drives continuous stride-locked Humanoid animation blend trees, world-space IK foot locking, upper-body weapon aiming IK, water buoyancy swimming transitions, expressive dialogue gestures, and protagonist abilities.

---

## Protagonists Preserved & Upgraded

| Protagonist | ID | Height & Silhouette | Signature Wardrobe (HDRP PBR) | Health / Armor / Stamina | Signature Ability (`[Q]`) |
|---|---|---|---|---|---|
| **Tariq Hasan** (`বাঘা তারেক`) | `tariq` | `1.80 m` Athletic Male Build | Waxed Indigo/Charcoal Jacket (`#1E3A5F`), Brass Zipper Trim (`#D4A017`), Dark Twill Cargo Trousers, Combat Boots, Tapered Fade Hair | `135 / 100 / 115` | **Street Overdrive**: `+35–45%` combat/melee surge, speed boost (`+14%`), stamina burst, drift grip bonus |
| **Maya Rahman** (`মায়া`) | `maya` | `1.70 m` Sleek Tactical Build | Crimson Moto Jacket (`#8A1C27`), Cyber-Cyan Collar Trim (`#00D2D3`), Tapered Cargo Pants, Tactical Boots, Layered Swept Bob | `110 / 120 / 140` | **Focus Reflex**: Time-dilation reflex (`0.58x` world speed with unscaled ability timer) + `1.65x` aim precision multiplier |

Live protagonist switching (`ProtagonistSwitcher.cs`) is bound to `[Tab]` in the Gulshan–Hatirjheel showcase (`GulshanHatirjheelWorldBuilder.cs`).

---

## Folder & Class Architecture

### 1. Core Contracts (`DhakaGta.Core` — `Assets/Scripts/Core/`)
- `CharacterAbilityEvents.cs`: Event bus (`AbilityActivationState`, `CharacterGestureKind`, `CharacterAimTargetState`, `VehicleBoardingEventData`) allowing Combat, Vehicles, Graphics, and UI to subscribe cleanly without creating circular `.asmdef` dependencies back into `DhakaGta.Character`.
- `InputActionRouter.cs`: Exposes optional `Aim`, `SpecialAbility`, `DialogueGesture`, and `SwitchProtagonist` actions alongside core `Move`, `Jump`, `Sprint`, `Crouch`, and `Interact`.

### 2. Data & AI4Animation Codebook (`DhakaGta.Data` — `Assets/Scripts/Data/`)
- `ProtagonistProfileSO.cs`: Upgraded with `maxArmor`, `meleeDamageMultiplier`, `aimPrecisionMultiplier`, `driftHandlingBonus`, `collisionResistance`, `specialAbility`, `abilityDurationSeconds`, `abilityCooldownSeconds`, `isFeminineBuild`, `characterHeightMeters`, and silhouette/clothing PBR colors.
- `MotionCodebookSO.cs`: Stores 15 trajectory- & phase-indexed motion codebook states (`Idle_Loop`, `Walk_Fwd`, `Jog_Fwd`, `Sprint_Fwd`, `Crouch_Idle`, `Crouch_Walk`, `Turn_90`, `Turn_180`, `Jump_Start`, `Fall_Loop`, `Land_Recovery`, `Aim_Strafe`, `Swim_Idle`, `Swim_Forward`, `Vehicle_Drive`), pairing each clip with its canonical displacement speed, `strideLengthMeters`, and normalized `[0..1)` left/right foot stance windows (`leftStanceStart/End`, `rightStanceStart/End`).

### 3. Character Locomotion, Trajectory & IK (`DhakaGta.Character` — `Assets/Scripts/Character/`)
- `MotionTrajectoryPredictor.cs`: Maintains a 6-sample past root history ring buffer (`-0.6s..0s`) and rolls out smoothed 5-sample future root trajectory positions/headings (`+0.2s, +0.4s, +0.6s, +0.8s, +1.0s`) via exponential damping, computing signed `+0.4s` predicted turn angles (`PredictedTurnDegrees04`).
- `MotionMatchingController.cs`:
  - Matches current trajectory velocity, turn angle, groundedness, crouch, sprint, aim, swimming, and vehicle occupancy against `MotionCodebookSO`.
  - Advances a continuous cyclic gait phase $\phi \in [0, 1)$ strictly locked to `horizontalSpeed / strideLengthMeters` and scales `Animator.speed` proportionally so feet never slide across pavement.
  - Exposes `IsLeftFootPlanted` / `IsRightFootPlanted` stance flags from the matched entry's contact windows and writes damped parameters (`MoveSpeed`, `MoveX`, `MoveZ`, `TurnDelta`, `GaitPhase`, `GaitPhaseSin`, `GaitPhaseCos`, `IsGrounded`, `IsSprinting`, `IsCrouching`, `IsSwimming`, `IsAiming`, `InVehicle`, `LeftFootContact`, `RightFootContact`).
- `CharacterIkAndFootLocker.cs`:
  - Uses Humanoid `OnAnimatorIK` (`iKPass = true` on Base & UpperBody layers):
  - **Foot Locking & Slope Alignment**: During stance phase windows, `Physics.SphereCast` probes `Ground | Building` layers and latches planted foot XZ/Y/surface-normal rotation until swing release.
  - **Pelvis Drop Compensation**: Dynamically shifts `_animator.bodyPosition.y` down by `PelvisOffsetY` on stairs/curbs so the lower foot keeps contact without hyper-extending legs.
  - **Upper-Body Aim & Reach IK**: Drives `AvatarIKGoal.RightHand`, `AvatarIKGoal.LeftHand` (two-handed weapon foregrip), `SetLookAtPosition`, and bell-curved right-hand interaction reach targets (`[E]` tea stalls, ATMs, vehicle door handles).
  - *(Note: Direct `com.unity.animation.rigging` multi-constraint rig graphs are deferred until `com.unity.animation.rigging` is added to `Packages/manifest.json` online; `OnAnimatorIK` requires zero extra UPM packages.)*
- `CharacterAbilityController.cs`: Manages **Street Overdrive** & **Focus Reflex** timers on unscaled time and guarantees `Time.timeScale` & `Time.fixedDeltaTime` restoration in `OnDisable`.
- `CharacterSwimAndDialogueController.cs`: Probes `PhysicsLayers.Water` volumes (`Hatirjheel`, `Buriganga`), engages chest-depth buoyancy & `Swim_Idle`/`Swim_Forward` state at `>= 1.12 m` submersion, cycles 6 Dhaka dialogue gestures (`[G]`), and executes `IInteractable` hand-reach triggers (`[E]`).
- `ProtagonistController.cs`: Unified coordinator tying `CharacterController` physics cleanly to trajectory prediction, motion matching, IK foot locking, ability multipliers, swimming buoyancy, and automatic vehicle driver-seat sync (integrated with `DhakaVehicleSeatsAndInteract.cs`).

### 4. Blender & Unity Editor Asset Pipeline (`BlenderPipeline/` & `Assets/Scripts/Editor/`)
- **16 HDRP PBR Character Textures** (`Assets/Textures/Characters/*.png`, `512x512`):
  - Skin (`T_Char_Skin_Albedo/Normal/MaskMap`)
  - Tariq Waxed Indigo Jacket (`T_Char_Tariq_Jacket_*`), Maya Crimson Moto Jacket (`T_Char_Maya_Jacket_*`)
  - Dark Cargo Denim (`T_Char_DenimCargo_*`), Directional Hair (`T_Char_Hair_*`), Footwear Leather (`T_Char_Footwear_*`)
- `BlenderPipeline/generate_dhaka_characters.py`: Builds anatomical lofted 26-bone Humanoid FBX models (`CHAR_Tariq.fbx`, `CHAR_Maya.fbx`) with sculpted facial structures (brow, nose bridge, jaw, eyeballs), hair volume, collared jackets, trousers, boots, UVs, and `ARMATURE_AUTO` skinning. Registered in `build_all_dhaka_fbx.py`.
- `Assets/Scripts/Editor/DhakaCharacterPipelineBuilder.cs`:
  - Menu: `Dhaka-GTA -> Part 7 -> Build Tariq, Maya & Humanoid Animator Assets`
  - Auto-imports Blender `.fbx` models as Humanoid when present, or builds full 25-bone anatomically lofted `SkinnedMeshRenderer` Humanoid prefabs (`PF_Protagonist_Tariq.prefab`, `PF_Protagonist_Maya.prefab`) + Humanoid `Avatar` assets.
  - Bakes **20 Humanoid `.anim` muscle-curve clips** (`Idle_Loop`, `Walk_Fwd/Back/Strafe_L/R`, `Jog_Fwd`, `Sprint_Fwd`, `Crouch_Idle/Walk`, `Turn_90/180`, `Jump_Start`, `Fall_Loop`, `Land_Recovery`, `Swim_Idle/Forward`, `Aim_UpperBody`, `Interact_Reach`, `Dialogue_Gesture`, `Vehicle_DriveSeat`), `UpperBodyAimMask.mask`, and `AC_DhakaHumanoid.controller` (Base 2D Freeform Directional Blend Tree + Masked UpperBody Override Layer).
  - Replaces legacy capsule visual in `GulshanHatirjheelWorldBuilder.cs` with `DhakaCharacterPipelineBuilder.AttachProtagonistRigsAndSwitcher`.

---

## Verification & Offline Blockers
- **Tests Created**:
  - `Assets/Tests/EditMode/CharacterAnimationEditTests.cs`: Verifies Tariq/Maya dynamic attributes & abilities, `MotionTrajectoryPredictor` future arc & turn estimation, `MotionCodebookSO` matching across Walk/Jog/Sprint/Swim/Aim/Vehicle, and gait phase stance windows.
  - `Assets/Tests/PlayMode/CharacterLocomotionSyncPlayTests.cs`: Verifies real-time `ProtagonistController` sprint velocity sync, `StreetOverdrive`, Aim IK weight ramp, `PhysicsLayers.Water` swim transitions, and Maya `FocusReflex` `Time.timeScale` dilation & cleanup.
- **Pending Live Runtime Verification**:
  - Requires restoring Drive `D:` (`Unity 6000.6.10f1`) and installing Blender to compile C#, bake Humanoid `.anim` / `.controller` / `.prefab` / `.fbx` binaries, optionally install `com.unity.animation.rigging`, and execute EditMode/PlayMode test suites inside Unity Test Framework.
