# Dhaka-GTA — TypeScript / Three.js to Unity 6 HDRP Migration Roadmap

This document maps every module in the legacy Three.js reference (`src/` and `scripts/`) to its redesigned Unity 6 C# / HDRP architecture. Systems are **re-engineered for native Unity physics, Animator, Cinemachine, HDRP, and Addressables** rather than translated 1:1 from TypeScript.

---

## 1. System-by-System Mapping Matrix

| Legacy TypeScript Reference (`src/`) | Known Legacy Audit Gaps | Unity 6 C# / HDRP Redesign Target | Namespace / Assembly |
| :--- | :--- | :--- | :--- |
| `src/game/engine.ts` (`4,654 LOC`) | Monolithic update loop, `KeyT`/`KeyV`/`KeyQ` conflicts, camera `rotation.z` flip, invisible vehicle drop | Split into `GameSessionController`, `TimeOfDayController`, `InputActionRouter`, `SpawnService`, decoupled via C# events | `DhakaGta.Core`, `DhakaGta.World` |
| `src/game/types.ts` & `saveSystem.ts` | Mixed runtime & static tables; `saveSystem` clamped HP/Stamina at 100 breaking Tariq/Maya | Strongly typed ScriptableObjects (`VehicleSpecSO`, `WeaponSpecSO`, `ProtagonistProfileSO`, etc.) + JSON `SaveDataService` respecting dynamic stat caps | `DhakaGta.Data`, `DhakaGta.Core` |
| `src/game/dhakaGeoData.ts` & `worldData.ts` | Hardcoded `worldX`/`worldZ` mismatched OSM projection; Shapla spawn mislabeled Old Dhaka; viaduct gaps | `GeographicData/dhaka_world_geo.json` imported into `DistrictDefinitionSO`, `LandmarkDefinitionSO`, `RoadCorridorSO`; spline/prefab viaducts | `DhakaGta.Data`, `DhakaGta.World` |
| `src/game/worldChunks.ts` & `worldBuilder.ts` | Manual mesh visibility toggles, placeholder box buildings, orphaned Ahsan Manzil GLB | `com.unity.addressables` async loader + additive `District_*` scene streaming (`DistrictStreamingManager`) + HDRP HLOD/LODGroups | `DhakaGta.Streaming`, `DhakaGta.World` |
| `src/game/textures.ts` & `graphicsQuality.ts` | 36 canvas textures, rainbow Krishnachura noise, abrupt quality downgrade | PBR `HDRP/Lit` materials + ORM `_MaskMap`, Volumetric Fog, `PhysicallyBasedSky`, HDRP Quality Tiers (`Performant`, `Balanced`, `High Fidelity`) | `DhakaGta.World`, `Assets/Settings` |
| `src/game/models.ts` & `assetLoader.ts` | Wheel gimbal wobble, body pitch applied to wheels, boat prop array collision | Blender-authored `.fbx` models imported as Unity Prefabs with separate chassis, suspension, wheel, steering-wheel, and light transforms | `DhakaGta.Vehicles`, `DhakaGta.Character` |
| `src/game/characterAnimator.ts` & `protagonists.ts` | Head bone drift, unused `aimAction`/swim clips, unused Tariq/Maya stat multipliers | Unity Humanoid `Animator` 2D Blend Trees + `com.unity.animation.rigging` (`TwoBoneIKConstraint`, `MultiAimConstraint`, `OverrideTransform`) + `ProtagonistProfileSO` applied to speed/combat | `DhakaGta.Character` |
| `src/game/cameraSystem.ts` | Euler `lookAt` + `rotation.z` flip; obstructed LHD cockpit vs RHD Sedan GLB | Cinemachine 3 (`CinemachineCamera`, `CinemachineThirdPersonFollow`, `CinemachineImpulseSource`) + dedicated `CameraOccluder` layer | `DhakaGta.Character`, `DhakaGta.Core` |
| `src/game/physics.ts` & `waterSystem.ts` | Kinematic bicycle model, un-dt-scaled overdrive, frozen `timeSec=0` water waves | Unity `Rigidbody` + `WheelCollider` drivetrain/suspension + HDRP Water/Buoyancy solver driven cleanly by `Time.fixedDeltaTime` | `DhakaGta.Vehicles` |
| `src/game/trafficSystem.ts` & `pedestrianSystem.ts` | Interior pedestrian spots, waypoint teleporting, unpooled traffic | `com.unity.ai.navigation` (`NavMeshAgent`, `NavMeshSurface`) + intersection signal state machine + obstacle ray sensors | `DhakaGta.TrafficAI` |
| `src/game/audio.ts` | WebAudio oscillation synths | `AudioMixer` (`Master -> Music, SFX, Ambience`), 3D spatialized `AudioSource` pools, dynamic engine RPM pitch curves | `DhakaGta.Audio` |
| `src/components/*.tsx` | Unscaled narrow HUD collisions, Pause modal overflow, fake phone `DhakaCam` | uGUI + TextMeshPro responsive anchored `Canvas` (`GameShellController`, Weapon Wheel radial selector, real `RenderTexture` DhakaCam, Minimap camera/radar) | `DhakaGta.UI` |
| `src/game/multiplayer/*` & `src/server/*` | Unreconciled client transform, missing PvP targetId, uncollided server vehicles | Unity Netcode (`Netcode for GameObjects`) or authoritative C# state protocol using fixed-tick server physics reconciliation | `DhakaGta.Networking` |

---

## 2. Phase-by-Phase Unity Delivery Roadmap

- **Phase 1 (Current — Foundation & Architecture):** Unity 6 (`6000.6.10f1`) HDRP project setup, package verification, folder/namespace & `.asmdef` DAG, Input System maps, 11-layer Physics Collision Matrix, ScriptableObject definitions, `Bootstrap`/`MainMenu`/`TestCity` scenes, EditMode & PlayMode foundation tests, Git LFS spec, first Git checkpoint.
- **Phase 2 (Locomotion, Camera & First Blender Vehicles):** Retarget/author Tariq & Maya HDRP Humanoid prefabs with Animation Rigging (`TwoBoneIK`, `MultiAim`), Cinemachine 3 camera modes (Close/Wide/RHD Cockpit/Cinematic), and Blender-authored Dhaka CNG Auto & Gulshan GT Sedan using Unity `WheelCollider` physics.
- **Phase 3 (World Streaming & Dhaka District Core):** Convert `GeographicData/dhaka_world_geo.json` into additive Addressable District scenes (`ShaplaCrossing`, `Motijheel`, `OldDhaka`, `Sadarghat`) with HDRP wet monsoon shaders, MRT-6 continuous viaducts, Shapla Monument, and Buriganga HDRP water.
- **Phase 4 (Living Dhaka AI, Combat & Wanted System):** NavMesh pedestrian crowds + crosswalk signals, intersection-aware traffic AI, 4-weapon combat + hitboxes + DMP Police search perimeter & wanted stars.
- **Phase 5 (Missions, Dual-Protagonist Switching & Full UI Shell):** Story missions, Fare Hustles, Safehouse deeds, Sky-Cam character switching, Weapon Wheel, Smartphone + live `RenderTexture` camera, Minimap & AudioMixer polish.
- **Phase 6 (Windows PC Optimization & Authoritative Multiplayer):** HDRP frame-budget profiling, LODGroups, HLODs, Automated PlayMode soak tests, Windows PC Installer/Build verification, and Multiplayer state synchronization.
