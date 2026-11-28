# Dhaka-GTA — HDRP Photorealistic Graphics, Weather & 5-Mode Camera System (Part 5)

## 1. Visual Target Transparency Note
No reference driving screenshot file was attached in the Part 5 prompt or workspace artifact directory. The HDRP pipeline is tuned for cinematic tropical Dhaka driving across **Golden Hour Dusk**, **Humid Midday Sun**, **Monsoon Downpour**, and **Wet Night Street Reflections**, and can be calibrated immediately against your screenshot once shared or attached.

---

## 2. Implemented HDRP Systems (`DhakaGta.Graphics`)

| Component | File | Features |
| :--- | :--- | :--- |
| Dynamic Time-of-Day & Volume Controller | [DhakaTimeOfDayAndWeatherController.cs](../UnityProject/Assets/Scripts/Graphics/DhakaTimeOfDayAndWeatherController.cs) | 24-hour Dhaka solar elevation/azimuth & Kelvin color temperature, moonlight transition, `PhysicallyBasedSky` tropical aerosols, drifting `VolumetricClouds` (`Sparse`/`Cloudy`/`Overcast`/`Stormy`), `Fog` (`enableVolumetricFog`), `ScreenSpaceReflection`, `Exposure` (`Automatic`), `Tonemapping` (`ACES`), `ColorAdjustments`, `Bloom` (`anamorphic`), and automatic dusk/rain toggle for streetlamps & headlights. |
| Wet Surface Puddle Shader & Driver | [DhakaWetSurface.hlsl](../UnityProject/Assets/Shaders/DhakaWetSurface.hlsl) & [WetSurfacePuddleDriver.cs](../UnityProject/Assets/Scripts/Graphics/WetSurfacePuddleDriver.cs) | Shader Graph Custom Function (`ApplyDhakaWetPuddles_float`) for world-space puddle accumulation, asphalt darkening, smoothness escalation (`0.25 -> 0.90+`), clear-coat film sheen (`_CoatMask`), and animated raindrop ripple normals, plus runtime pavement & car-paint clear-coat modulation. |
| Rain & Road Spray VFX | [DhakaWeatherVfxController.cs](../UnityProject/Assets/Scripts/Graphics/DhakaWeatherVfxController.cs) | 3D stretched monsoon rain streaks following the camera dome, ground droplet splashes, and speed/wetness-scaled rear-wheel tire mist spray behind `DhakaWheelVehicleController`. |
| Reflection Probe Rig | [DhakaReflectionProbeRig.cs](../UnityProject/Assets/Scripts/Graphics/DhakaReflectionProbeRig.cs) | Mobile `HDAdditionalReflectionData` (`Realtime`, `OnDemand`) refreshing as the player/vehicle traverses >45m or shifts weather presets. |
| 5-Mode Cinemachine Camera Rig | [DhakaCinemachineCameraRig.cs](../UnityProject/Assets/Scripts/Graphics/DhakaCinemachineCameraRig.cs) | Implements `DrivingChase`, `DrivingClose`, `DrivingWideCinematic`, `FirstPersonCockpit` (Bangladesh Right-Hand Drive), and `OnFootThirdPerson`, with `Physics.SphereCast` (`CameraOcclusionMask`), speed-sensitive FOV widening (`54°–75°`), lateral cornering roll banking, mouse orbit with auto-recenter, and `[B]` look-back. |
| Quality Preset Scaler | [DhakaGraphicsQualityManager.cs](../UnityProject/Assets/Scripts/Graphics/DhakaGraphicsQualityManager.cs) | Scales volumetric clouds, volumetric fog, SSR, and particle budgets across `HighFidelity`, `Balanced`, and `Performant` tiers. |
| Headlights & Corridor Lamps | [DhakaGraphicsShowcaseRig.cs](../UnityProject/Assets/Scripts/Graphics/DhakaGraphicsShowcaseRig.cs) | Vehicle `SpotLight` headlights, emissive brake/taillights, and amber streetlamp pools along the Gulshan–Hatirjheel driving loop. |

---

## 3. Runtime Controls in `GulshanHatirjheel_Milestone.unity`

- `WASD` — Move on foot / Drive (`Throttle` + `Steer`)
- `E` — Interact (`Tong Tea Stall` healing/stamina restore, lakeside benches)
- `F` — Enter / Exit `GulshanGT_DrivableCar`
- `V` / `B` / Right Mouse — Cycle camera modes (`DrivingChase`, `DrivingClose`, `DrivingWideCinematic`, `FirstPersonCockpit`) / Look Back / Free Orbit
- `F6` — Cycle Weather (`GoldenHourClear`, `MiddayHumidSun`, `MonsoonDownpour`, `WetNightReflections`)
- `F7` — Advance Time of Day `+3h`
- `F8` — Cycle Quality Tier (`HighFidelity`, `Balanced`, `Performant`)
