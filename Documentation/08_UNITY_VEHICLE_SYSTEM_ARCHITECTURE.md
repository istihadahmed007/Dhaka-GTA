# Dhaka-GTA — AAA Unity C# Vehicle & Watercraft Architecture (Part 6)

## 1. Wheel Physics Evaluation: Built-In `WheelCollider` vs. Custom PhysX Raycast Wheel System

**Decision:** Standardize all 9 land vehicle classes on a **Custom PhysX SphereCast Raycast Wheel Simulation (`DhakaPhysxWheel` + `DhakaVehicleController`)**, paired with a multi-pontoon **Archimedes Hydrodynamic Buoyancy Solver (`DhakaWatercraftController`)** for the 2 marine classes (`Speedboat`, `Buriganga Launch`).

- **Why not Unity `WheelCollider` across the full fleet?**
  - Dhaka's signature fleet relies heavily on **2-wheel motorcycles** (dynamic lean angles) and **3-wheel CNG auto-rickshaws & cycle rickshaws**. Unity `WheelCollider` uses a thin single-point raycast that often snags on multi-segment procedural road triangles and lacks clean roll-torque stability for 2- and 3-wheeled vehicles.
- **Advantages of our `Physics.SphereCast` Raycast Wheel Architecture:**
  - Seamless crossing across generated OSM road triangle boundaries and curbs (`SphereCast` contact radius `0.42 * R`).
  - Identical solver drives 2-wheelers (with PD cornering lean gyro), 3-wheelers, 4-wheelers, and heavy buses.
  - Direct integration with `WorldSurfaceState.GlobalRoadWetness` (`WetSurfacePuddleDriver`), controlled handbrake rear-slip drifting, reverse-gear engagement, and `MonsoonRainTread` tire upgrades.

---

## 2. Complete 11-Vehicle Fleet (`VehicleSpecSO` + Blender `.fbx` + Unity Prefabs)

| Category | ID | Wheels | Drive | Seats | Key Dhaka Behaviour |
| :--- | :--- | :---: | :---: | :---: | :--- |
| CNG | `cng_auto` | 3 | RWD | 3 | Tight `38°` turn radius, narrow urban filterer, roll damper |
| Cycle Rickshaw | `cycle_rickshaw` | 3 | Pedal | 3 | Slow `22 km/h` speed cap, painted hood, bicycle bell horn |
| Motorcycle | `motorcycle` | 2 | RWD | 2 | Active cornering lean up to `26°` + upright gyro stabilization |
| City Bus | `city_bus` | 4 | RWD | 8 | Heavy `9,800 kg` mass, wide turning envelope, deep diesel pitch |
| Intercity Bus | `intercity_bus` | 4 | RWD | 8 | `13,500 kg` highway coach with `108 km/h` cruise speed |
| Sedan | `sedan` | 4 | AWD | 4 | Balanced `148 km/h` RHD saloon with smooth controllable drift |
| SUV | `suv` | 4 | AWD | 4 | High `0.32m` suspension travel for rough curbs & flooded streets |
| Police Vehicle | `police_cruiser` | 4 | AWD | 4 | DMP patrol pickup/cruiser with `[Y]` Red/Blue siren lightbars |
| Pickup | `pickup` | 4 | AWD | 4 | Heavy utility truck (`2,300 kg`) with strong low-end torque |
| Speedboat | `speedboat` | — | Marine | 4 | High-speed planing V-hull over Hatirjheel Lake & rivers |
| Launch | `buriganga_launch` | — | Marine | 8 | `42,000 kg` double-deck Sadarghat river ferry |

---

## 3. Core C# Modules (`Assets/Scripts/Vehicles/`)

- **[DhakaPhysxWheel.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaPhysxWheel.cs):** `Physics.SphereCast` spring-damper compression, longitudinal drive/brake forces, lateral Pacejka-inspired slip, wet-road traction modulation, handbrake lockup, and visual wheel rotation.
- **[DhakaVehicleController.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaVehicleController.cs):** Accelerates, brakes, engages Reverse under `1.8 km/h`, rotates front steering & cockpit wheel, stabilizes 2-wheel lean / 3-wheel roll, calculates collision damage above `4.5 m/s`, cuts drive torque at `0 HP`.
- **[DhakaWatercraftController.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaWatercraftController.cs):** 4-pontoon Archimedes buoyancy & rudder propulsion.
- **[DhakaVehicleSeatsAndInteract.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaVehicleSeatsAndInteract.cs):** Smooth lerped enter/exit transition animation (`0.35s`), RHD driver seat, passenger seats, first-person cockpit eye anchor.
- **[DhakaVehicleLightsAndHorn.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaVehicleLightsAndHorn.cs):** `[L]` / night auto headlights, brake lights, reverse lamps, `[Y]` DMP police beacons, `[H]` horn.
- **[DhakaVehicleAudioController.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaVehicleAudioController.cs):** Procedural waveform engine RPM pitch/volume, tire skid hiss, horn tone, crash impact audio.
- **[DhakaVehicleTuningAndDamage.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaVehicleTuningAndDamage.cs) & [DhakaGarageManager.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaGarageManager.cs):** Structural damage, repair, clear-coat custom paint, Engine Stages `1–3`, Tire Compounds (`StreetAllSeason`, `SportDrySlick`, `MonsoonRainTread`), license-plate garage ownership & JSON persistence.
- **[DhakaVehicleFactory.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaVehicleFactory.cs) & [DhakaVehicleCatalogBuilder.cs](../UnityProject/Assets/Scripts/Editor/DhakaVehicleCatalogBuilder.cs):** Builds all 11 `VehicleSpecSO` assets & reusable `PF_Vehicle_*.prefab` assets, binding Blender exports from [generate_dhaka_vehicles.py](../BlenderPipeline/generate_dhaka_vehicles.py) when exported.
