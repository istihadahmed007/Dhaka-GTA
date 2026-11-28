# Dhaka-GTA (Project Dhaka) — Unity 6 HDRP Architecture & Technical Audit

## 1. Engine Lock & Technical Stack Summary

Unity 6 is the sole production engine for **Dhaka-GTA**, hosted in [UnityProject/](../UnityProject/) while preserving the original React + Three.js browser game at the repository root as a behavioral and geographic reference.

| Layer | Mandatory Technology | Configuration / Version Verified |
| :--- | :--- | :--- |
| Engine | Unity 6 (`6000.6.10f1`) | Installed locally with `Windows Standalone (x86_64)` build support. Note: Official LTS stream (`6000.3.29f1`) can be targeted if strict LTS pinning is desired; `6000.6.10f1` unlocks Unity 6.6 Animation Rigging, Pipeline tools, and `com.unity.render-pipelines.high-definition` 17.6.0. |
| Render Pipeline | **HDRP** (`com.unity.render-pipelines.high-definition@17.6.0`) | Configured via `HDRPDefaultResources` and `SkyandFogSettingsProfile`. Never downgraded to URP. |
| Language & Assemblies | C# modular `.asmdef` architecture | Strict DAG isolating `Core`, `Data`, `World`, `Character`, `Vehicles`, `TrafficAI`, `Missions`, `Audio`, `UI`, `Networking`, `Editor`, `Tests.EditMode`, `Tests.PlayMode`. |
| Physics | Unity Physics (PhysX) + explicit Collision Matrix | 11 gameplay physics layers (`Ground`, `Building`, `Water`, `Player`, `Vehicle`, `VehicleWheel`, `NPC`, `TrafficVehicle`, `Projectile`, `TriggerZone`, `CameraOccluder`). |
| Input | Unity Input System (`com.unity.inputsystem@1.20.1`) | Conflict-free action maps (`Player`, `Vehicle`, `Camera`, `UI`, `Debug`) stored in `Assets/InputSystem_Actions.inputactions`. |
| Animation | Unity Animator + `com.unity.animation.rigging` | Humanoid retargeting + 2-Bone Leg IK, Pelvis drop, Torso banking, MultiAim head tracking. |
| Camera | Cinemachine 3 (`com.unity.cinemachine@3.1.8`) | Priority-driven Title Orbit, 3rd-Person Close/Wide Follow, Cockpit, and Impulse shake. |
| Streaming | `com.unity.addressables` + Additive Scenes | District-chunk additive scene & asset groups keyed off Dhaka GIS bounds. |
| AI Navigation | `com.unity.ai.navigation` | Baked sidewalk/plaza NavMesh surfaces + intersection links. |
| UI Stack | uGUI + TextMeshPro (`com.unity.ugui@2.6.0`) | CanvasScaler (`1920x1080`, Match `0.5`) with palette-driven HUD/Menus. |
| 3D Content Pipeline | Blender -> `.fbx` / `.glb` | Dedicated authoring pipeline documented cleanly; Unity procedural placeholders bridge until Blender assets are imported. |
| Tests & Tooling | `com.unity.test-framework` + `com.unity.pipeline@1.0.1` | EditMode & PlayMode automated validation suites. |

---

## 2. Folder & Namespace Architecture

### Repository Root (`Dhaka-GTA/`)
- `UnityProject/` — Primary Unity 6 HDRP Windows PC project.
- `GeographicData/` — Offline Dhaka GIS extracts (`dhaka_world_geo.json`) sourced from OpenStreetMap (ODbL).
- `Documentation/` — Technical architecture, migration roadmap, and Git LFS standards.
- `src/`, `public/`, `.github/` — Preserved original Three.js browser game (reference only; GitHub Pages deploy untouched).
- `ProjectDhakaUnity/` — Preserved legacy URP prototype archive (textures/animations salvaged via re-authoring only with HDRP shaders; never mixed back into `UnityProject`).

### Unity Project Layout (`UnityProject/Assets/`)
```
Assets/
├── Animations/          # Animator Controllers, Avatar masks, Humanoid clips
├── Audio/               # AudioMixer (Master/Music/SFX/Ambience) & clips
├── Materials/           # HDRP/Lit, HDRP/LitTessellation, Decal, Water materials
├── Models/              # Blender exports (.fbx / .glb) organized by Characters, Vehicles, Landmarks
├── Prefabs/             # Modular prefabs (Player, Vehicles, StreetFurniture, DistrictChunks)
├── Resources/           # Bootstrap-critical assets only (minimized for Addressables)
├── Scenes/
│   ├── Bootstrap.unity  # Entry scene: validates HDRP, loads services & MainMenu
│   ├── MainMenu.unity   # Live HDRP Dhaka flyover backdrop + uGUI shell
│   └── TestCity.unity   # Playable HDRP vertical slice foundation (Shapla Plaza)
├── ScriptableObjects/   # Authored .asset configs (Districts, Vehicles, Weapons, Weather, Missions)
├── Scripts/
│   ├── Core/            # Namespace: DhakaGta.Core
│   ├── Data/            # Namespace: DhakaGta.Data (ScriptableObjects)
│   ├── World/           # Namespace: DhakaGta.World
│   ├── Streaming/       # Namespace: DhakaGta.Streaming
│   ├── Character/       # Namespace: DhakaGta.Character
│   ├── Vehicles/        # Namespace: DhakaGta.Vehicles
│   ├── TrafficAI/       # Namespace: DhakaGta.TrafficAI
│   ├── Combat/          # Namespace: DhakaGta.Combat
│   ├── Missions/        # Namespace: DhakaGta.Missions
│   ├── Audio/           # Namespace: DhakaGta.Audio
│   ├── UI/              # Namespace: DhakaGta.UI
│   ├── Networking/      # Namespace: DhakaGta.Networking
│   └── Editor/          # Namespace: DhakaGta.Editor
├── Settings/            # HDRP Pipeline Assets, Volume Profiles, Quality Tiers
├── Tests/
│   ├── EditMode/        # Namespace: DhakaGta.Tests.EditMode
│   └── PlayMode/        # Namespace: DhakaGta.Tests.PlayMode
└── World/               # Lighting data, NavMesh bakes, additive District scenes
```

---

## 3. Assembly Definition (`.asmdef`) Dependency DAG

No circular references are permitted; gameplay modules communicate cleanly through `DhakaGta.Core` interfaces/events and `DhakaGta.Data` ScriptableObjects:

```
DhakaGta.Core           (Unity.InputSystem, Unity.Addressables, Unity.ResourceManager, Unity.RenderPipelines.HighDefinition.Runtime)
    ▲
DhakaGta.Data           (References: Core)
    ▲
┌───┴──────────┬──────────────┬───────────────┬────────────────┐
▼              ▼              ▼               ▼                ▼
World      Character      Vehicles         Combat          Audio / UI
(Streaming, (Animator,     (WheelCollider, (Weapons,       (TMP, uGUI)
 HDRP Sky)   Rigging)       Watercraft)     Health)
    ▲              ▲              ▲               ▲
    └──────────────┴──────┬───────┴───────────────┘
                          ▼
               TrafficAI / Missions / Networking
                          ▲
               Editor & Tests (EditMode / PlayMode)
```

---

## 4. Physics Layers & Collision Matrix Design

| Layer Index | Layer Name | Collides With | Purpose |
| :---: | :--- | :--- | :--- |
| `0–5` | Unity Built-in | Standard (`Ignore Raycast` stays non-colliding) | Engine defaults |
| `6` | `Ground` | Player, Vehicle, VehicleWheel, NPC, TrafficVehicle, Projectile, CameraOccluder | Asphalt roads, sidewalks, terrain, plaza slabs |
| `7` | `Building` | Player, Vehicle, NPC, TrafficVehicle, Projectile, CameraOccluder | Static Dhaka architecture, flyover piers, curbs |
| `8` | `Water` | Player, Vehicle, TriggerZone | Buriganga River, Dhanmondi Lake, Hatirjheel buoyancy triggers |
| `9` | `Player` | Ground, Building, Water, Vehicle, NPC, TrafficVehicle, Projectile, TriggerZone | Active protagonist capsule / controller (ignores `VehicleWheel` & `CameraOccluder`) |
| `10` | `Vehicle` | Ground, Building, Water, Player, NPC, TrafficVehicle, Projectile, TriggerZone | Player & drivable vehicle rigidbody chassis (ignores own `VehicleWheel`) |
| `11` | `VehicleWheel` | Ground, Building | WheelCollider raycasts / tire contact only |
| `12` | `NPC` | Ground, Building, Player, Vehicle, NPC, TrafficVehicle, Projectile, TriggerZone | Pedestrians, police, syndicate fighters |
| `13` | `TrafficVehicle` | Ground, Building, Player, Vehicle, NPC, TrafficVehicle, Projectile, TriggerZone | AI traffic vehicles (CNG, bus, rickshaw, sedan) |
| `14` | `Projectile` | Ground, Building, Player, Vehicle, NPC, TrafficVehicle | Raycast / ballistic combat layers |
| `15` | `TriggerZone` | Player, Vehicle, NPC | Mission checkpoints, POI rings, district streaming volumes, river docks |
| `16` | `CameraOccluder` | Ground, Building | Dedicated Cinemachine Deoccluder mask (ignores characters, vehicles, triggers, foliage) |

---

## 5. Key Architectural Decisions & Hardware Notes

1. **Decoupling `DhakaGameEngine` Monolith:** In the Three.js reference (`src/game/engine.ts`, 4,654 lines), rendering, input, UI polling, mission scripts, AI, and multiplayer lived in one file, causing keybind shadowing (`KeyT`/`KeyV`/`KeyQ`/`KeyH`), invisible vehicle drops, and camera Euler flip bugs. Unity splits these cleanly into single-responsibility C# managers coordinated via `GameSessionController` and ScriptableObject configs.
2. **HDRP Hardware Compatibility on Development Workstation:** Verified workstation GPU is `Intel(R) HD Graphics 4000` (Direct3D 11.0, feature level 11.0, 128 MB VRAM; Direct3D 12 creation fails with `0x887a0004`). HDRP runs via DX11 fallback using `HDRP Performant` / tuned DX11 quality defaults for workstation verification while preserving `HDRP High Fidelity` / `HDRP Balanced` assets for target Windows PC GPUs.
3. **Blender 3D Pipeline Readiness:** Original Dhaka vehicles (CNG Auto, Rickshaw, Sadarghat Launch, Metro MRT-6 Train) and hero architecture (Ahsan Manzil, Lalbagh Fort, Shapla Monument) are designated for `.blend -> .fbx` authoring with PBR ORM/Mask textures (`R=Metallic, G=AO, B=DetailMask, A=Smoothness` matching HDRP Lit `_MaskMap`). Blender is not yet installed on this development workstation and should be installed before 3D hero asset production in Phase 2.
4. **Correcting Reference Geographic Bugs:** The Three.js audit revealed `getDistrictAt` tagged Shapla spawn (`-2.2, 35.2`) as `OLD DHAKA · CHAWKBAZAR` on `Kazi Nazrul Islam Ave` and conflicted with `projectOsmLatLonToWorld`. Unity `GeographicData/dhaka_world_geo.json` and `DistrictDefinitionSO` use explicit polygonal/bound definitions separating Shapla Crossing, Motijheel Commercial, Old Dhaka Chawkbazar, Sadarghat, Kawran Bazar, Dhanmondi, Gulshan, Mirpur, and Uttara North cleanly.
