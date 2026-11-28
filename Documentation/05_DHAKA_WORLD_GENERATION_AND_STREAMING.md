# Dhaka-GTA — Realistic 1:1 Scale Dhaka World Generation & Streaming (Part 3)

## 1. Non-Grid OpenStreetMap Topology Architecture

Instead of a synthetic rectangular street grid, the world-generation pipeline builds directly from connected OpenStreetMap polylines, internal/endpoint OSM node IDs, and 1:1 UTM Zone 46N (`EPSG:32646`) polygons from [dhaka_gulshan_hatirjheel.geojson](../GeographicData/dhaka_gulshan_hatirjheel.geojson) (`12,720` features) and [dhaka_osm_extract.geojson](../GeographicData/dhaka_osm_extract.geojson) (`2,638` features).

| Component | File | Responsibilities |
| :--- | :--- | :--- |
| `RoadSplineSystem` | [RoadSplineSystem.cs](../UnityProject/Assets/Scripts/WorldGen/RoadSplineSystem.cs) | Converts real OSM polylines into C1 Catmull-Rom splines; preserves lane count, speed limits, one-way polarity, left-hand traffic lane centerlines, and smooth elevation envelopes for bridges (`+4.2m`), flyovers (`+8.5m`), and underpasses (`-4.6m`). |
| `IntersectionGenerator` | [IntersectionGenerator.cs](../UnityProject/Assets/Scripts/WorldGen/IntersectionGenerator.cs) | Matches shared OSM vertex IDs and same-grade proximity to classify 3-way, 4-way, multi-leg junctions, and circular roundabouts (`junction=roundabout\|circular` including Gulshan Circle 1/2); generates drivable intersection aprons, stop lines, crosswalks, roundabout turf islands, corner signal rigs, and `NavGraphEdge` routing links. |
| `RoadGenerator` | [RoadGenerator.cs](../UnityProject/Assets/Scripts/WorldGen/RoadGenerator.cs) | Extrudes carriageway `MeshCollider` decks (`Ground` layer), batched white dashed lane dividers & yellow two-way centerlines, raised concrete curbs & pedestrian sidewalks, parapet walls + piers for bridges/flyovers, trench walls for underpasses, and triangulated lakes/rivers/canals (`Water` trigger layer). |
| `BuildingPlacementSystem` | [BuildingPlacementSystem.cs](../UnityProject/Assets/Scripts/WorldGen/BuildingPlacementSystem.cs) | Extrudes real OSM building polygons at authentic heights (`building:levels * 3.4m` / `height`), performs road-clearance filtering against `RoadSplineSystem`, and assigns HDRP material variants (`CommercialGlass`, `ModernConcrete`, `DhakaBrick`, `CivicPlaster`) into spatial chunks. |
| `WorldChunkManager` | [WorldChunkManager.cs](../UnityProject/Assets/Scripts/WorldGen/WorldChunkManager.cs) | Partitions 1:1 UTM space into `500m x 500m` grid cells (`Vector2Int`) and activates/deactivates chunk hierarchies within per-frame toggle budgets. |
| `WorldRegionLoader` | [WorldRegionLoader.cs](../UnityProject/Assets/Scripts/WorldGen/WorldRegionLoader.cs) | Defines all **12 Dhaka districts** (`Old Dhaka`, `Chawkbazar`, `Sadarghat`, `Motijheel`, `Shapla Chottor`, `Dhanmondi`, `Kawran Bazar`, `Gulshan`, `Banani`, `Hatirjheel`, `Mirpur`, `Uttara`) with async additive scene loading (`SceneManager.LoadSceneAsync`), async GeoJSON database streaming, and Addressables hooks. |
| `WorldStreamingController` | [WorldStreamingController.cs](../UnityProject/Assets/Scripts/WorldGen/WorldStreamingController.cs) | Evaluates focus UTM coordinates continuously, emits district changes, and drives `WorldChunkManager` + `WorldRegionLoader` hysteresis preloading/unloading. |

---

## 2. First Milestone — Gulshan–Hatirjheel Region & Driveability Test

- Scene Builder: [GulshanHatirjheelWorldBuilder.cs](../UnityProject/Assets/Scripts/Editor/GulshanHatirjheelWorldBuilder.cs) -> generates `Assets/Scenes/GulshanHatirjheel_Milestone.unity` using `1,177` road ways, `49` bridges/flyovers, `1` underpass, `60` water bodies/channels (Hatirjheel/Gulshan/Banani lakes), `22` traffic signal sites, and OSM buildings.
- Driveable Vehicle: [DhakaWheelVehicleController.cs](../UnityProject/Assets/Scripts/Vehicles/DhakaWheelVehicleController.cs) uses 4x `UnityEngine.WheelCollider` (`PhysicsLayers.VehicleWheel`) + chassis `Rigidbody` (`PhysicsLayers.Vehicle`) + anti-roll stabilization.
- Telemetry HUD: [GulshanHatirjheelHud.cs](../UnityProject/Assets/Scripts/UI/GulshanHatirjheelHud.cs) shows active district, nearest street metadata, speed (`km/h`), grounded tire count, and UTM Zone 46N + WGS84 readout.
- Automated Verification Suites (pending Unity Editor restore on `D:`):
  - EditMode: [WorldGenerationEditTests.cs](../UnityProject/Assets/Tests/EditMode/WorldGenerationEditTests.cs)
  - PlayMode: [GulshanHatirjheelDrivingPlayTests.cs](../UnityProject/Assets/Tests/PlayMode/GulshanHatirjheelDrivingPlayTests.cs) (spawns `DhakaWheelVehicleController` directly onto generated Gulshan–Hatirjheel `RoadDeck` `MeshCollider` and verifies `WheelHit` contact, `> 8 km/h` acceleration, and multi-meter road traversal without falling through seams).
