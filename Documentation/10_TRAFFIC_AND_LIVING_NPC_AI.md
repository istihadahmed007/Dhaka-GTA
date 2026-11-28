# PART 8 — UNITY DHAKA TRAFFIC AND LIVING NPC AI

## Overview
Part 8 brings generated Dhaka roads, intersections, flyovers, and sidewalks to life inside **Unity 6 HDRP C#** (`DhakaGta.TrafficAI`, `DhakaGta.Core`, `DhakaGta.WorldGen`, `DhakaGta.Editor`) with autonomous multi-class traffic, intersection signals, NavMesh + sidewalk pedestrian navigation, authentic Bangladeshi wardrobes, street activities, and DMP police response.

---

## Traffic Architecture (Actual OSM Roads & Intersections)
- **[DhakaRoadNetworkRouter.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/DhakaRoadNetworkRouter.cs)**:
  - Builds directional spline connectivity across real OpenStreetMap `RoadSplineSegment` polylines and `IntersectionGenerator.Junctions` (preserves roundabouts, one-ways, flyovers, and smooth endpoint reversals — zero artificial grid).
  - Enforces **Bangladesh Left-Hand Traffic (LHT)** lane positioning (`ComputeLhtLanePose`), keeping slow cycle rickshaws near left shoulders while shifting overtaking vehicles rightward toward the median.
- **[TrafficIntersectionNode.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/TrafficIntersectionNode.cs)**:
  - Binds timed North–South / East–West signal cycles directly to `SignalRig_*` lamp renderers, providing stop-line clearance queries (`IsApproachGreen`) and crosswalk timing (`CanPedestrianCrossAxis`).
- **[DhakaTrafficDensityProfile.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/DhakaTrafficDensityProfile.cs)**:
  - Adjusts vehicle and pedestrian spawn density dynamically by **District** (`old_dhaka`, `chawkbazar`, `sadarghat`, `motijheel`, `shapla_crossing`, `gulshan`, `banani`, `hatirjheel`, etc.) and **24-hour clock** (`WorldSurfaceState.CurrentTimeOfDayHours`: morning/evening Dhaka rush vs quiet late-night).
- **[DhakaAiVehicleAgent.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/DhakaAiVehicleAgent.cs)**:
  - Controls pooled `CNG`, `CycleRickshaw`, `CityBus`, `IntercityBus`, `CompactSedan`, `SUV`, `Motorcycle`, and `DMP Police Cruiser` vehicles built from `DhakaVehicleFactory`.
  - Features signal stopping, `Physics.SphereCastNonAlloc` headway spacing, congestion queueing, brake-light illumination, acoustic horns on lane obstruction/stalls, multi-lane LHT overtaking, motorcycle lane filtering, pedestrian emergency braking, curbside parked vehicles, and seamless handoff to manual `DhakaVehicleController` if hijacked (`[F]`).

---

## Living Bangladeshi Pedestrians, NavMesh & Police Response
- **[DhakaNavMeshRuntimeBaker.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/DhakaNavMeshRuntimeBaker.cs)**:
  - Dynamically constructs `UnityEngine.AI.NavMeshData` via built-in `NavMeshBuilder.BuildNavMeshData` (marking `PhysicsLayers.Ground` walkable and `Building` obstacles unwalkable) with zero external navigation package dependency, and extracts sidewalk waypoints, zebra crosswalk links, and world `InteractiveEnvironmentProp` activity POIs (`TeaStall`, `ShopStall`, `StreetVendorSpot`, `BenchRest`).
- **[BangladeshiPedestrianVisualBuilder.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/BangladeshiPedestrianVisualBuilder.cs)**:
  - Builds 7 culturally authentic articulated clothing archetypes: `PanjabiAndPajama` (with Prayer Cap/Tupi), `LungiAndGamcha`, `SareeTraditional` (pleats + pallu drape), `SalwarKameezAndDupatta`, `DhakaOfficeFormal`, `StreetVendorChaiwala` (with basket tray), and `DmpPoliceOfficer` (visor cap + utility belt).
- **[DhakaPedestrianAgent.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/DhakaPedestrianAgent.cs)**:
  - State machine covering `WalkingSidewalk`, `WaitingAtCrosswalk` (checking intersection signals & oncoming vehicle speed), `CrossingRoad` (hurrying if traffic approaches), `ShoppingAtStall`, `TeaStallHangout`, `StreetVending`, spontaneous `ConversingPair` encounters, `AvoidingVehicle` sidesteps, `FleeingDanger`, and `PoliceRespond`.
  - Drives both Unity Humanoid `Animator` parameters and lightweight articulated limb swing / dialogue gestures.
- **[CityDangerEvents.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Core/CityDangerEvents.cs)** & **[DhakaPoliceDispatchController.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/DhakaPoliceDispatchController.cs)**:
  - Broadcasts urban hazard events (`Gunshot`, `Explosion`, `VehicleCollision`, `Assault`) and wanted stars (0–5), triggering civilian evacuation and dispatching DMP Police Cruisers (with active alternating Red/Blue sirens) and foot constables.

---

## Performance Pooling & Scene Integration
- **[DhakaCityAiSimulationManager.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/TrafficAI/DhakaCityAiSimulationManager.cs)**:
  - Maintains reusable Object Pools for Vehicles & Pedestrians with distance-tiered update rates (`Near <= 48 m` every frame with full forward ray/sphere sensors & limb animation; `Mid 48–135 m` every 2–3 frames; `Far > 135 m` every 5–6 frames; `Recycle > 240 m`).
- **[DhakaTrafficAiPipelineBuilder.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Editor/DhakaTrafficAiPipelineBuilder.cs)** & **[GulshanHatirjheelWorldBuilder.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Editor/GulshanHatirjheelWorldBuilder.cs)**:
  - Serializes `RoadSplineSystem._segments` & `IntersectionGenerator._junctions` cleanly for Play Mode startup and attaches `DhakaCityAiSimulationManager` to `GulshanHatirjheel_Milestone.unity`.

---

## Verification & Offline Blockers
- **Tests Written**: [TrafficAndNpcAiEditTests.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Tests/EditMode/TrafficAndNpcAiEditTests.cs) & [TrafficAndNpcAiPlayTests.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Tests/PlayMode/TrafficAndNpcAiPlayTests.cs).
- **Pending Live Runtime Execution**: Unity 6 (`6000.6.10f1`) is offline due to missing Drive `D:`; EditMode/PlayMode NUnit tests and live scene playback await Editor availability.
