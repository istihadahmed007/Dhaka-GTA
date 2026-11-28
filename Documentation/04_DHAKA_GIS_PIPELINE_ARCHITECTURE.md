# Dhaka-GTA — Real-World Dhaka GIS Pipeline Architecture (Part 2)

## 1. Core Geographic Standards

- **Source Licensing Policy:**
  - All permanent road, building footprint, waterway, transit, and landmark assets originate from **OpenStreetMap (ODbL)** extracts converted into GeoJSON (`GeographicData/dhaka_osm_extract.geojson`).
  - Google Maps and Google Earth are restricted strictly to manual visual research and verification. No Google imagery, elevation tiles, or 3D Tiles are scraped, cached, or converted into Unity meshes/textures.
- **Coordinate Reference System & Projection:**
  - Input Geographic Datum: **WGS84 (`EPSG:4326`, Latitude / Longitude)**.
  - Projected Planar CRS: **WGS84 / UTM Zone 46N (`EPSG:32646`, Central Meridian `93.0° E`, Scale Factor `k0 = 0.9996`, False Easting `500,000 m`)**, covering all longitudes in `[90.0° E, 96.0° E]` including Greater Dhaka (`~90.35° E`–`90.45° E`).
- **True 1:1 Unity Scale (Zero Compression):**
  - **1.0 Unity world unit = 1.0 real-world meter.**
  - Unlike the legacy Three.js prototype (`src/game/dhakaGeoData.ts` which compressed ~6 km into 510 units at `0.085x`), `UnityProject` maintains real metropolitan distances (e.g., Shapla Chottor to Ahsan Manzil ≈ `2.93 km`, Shapla Chottor to Uttara North MRT-6 ≈ `15.2 km`).
- **Unity Axis Convention (`East-Up-North`):**
  - `+X` = **East** ($\Delta E = E - E_{\text{origin}}$ in meters)
  - `+Y` = **Up / Elevation** ($\Delta h = h - h_{\text{origin}}$ in meters)
  - `+Z` = **North** ($\Delta N = N - N_{\text{origin}}$ in meters; forward `0°` yaw faces due North)

---

## 2. Large-World Float32 Precision & Floating Origin

Raw UTM Zone 46N Northings in Dhaka exceed `2,626,800 m`. Storing raw UTM coordinates directly inside Unity `Transform.position` (`float32`) introduces `~0.25 m` mantissa quantization steps, causing shader Z-fighting and character controller jitter.

**Architecture (`WorldCoordinateManager.cs` + `GeographicCoordinateConverter.cs`):**
1. All authoritative feature coordinates (`UtmCoord`) and dataset bounds inside `GeoFeatureDatabase` are stored in **64-bit `double` precision**.
2. `WorldCoordinateManager` anchors `BaseOriginUtm` at Shapla Chottor (`23.733015° N, 90.417014° E` -> UTM Zone 46N).
3. Whenever the tracked player/camera drifts past `floatingOriginThresholdMeters` (default `2,000 m`), `ShiftOriginByLocalDelta` updates `_shiftEastingMeters` / `_shiftNorthingMeters` in `double` precision, shifts registered `Transform` roots back near `(0, y, 0)` cleanly, and calls `Physics.SyncTransforms()`. `UnityPositionToUtm` and `UnityPositionToLatLon` automatically incorporate the cumulative shift so geographic lookups remain exact.

---

## 3. Pipeline Modules & Extracted Dataset

| Module | File | Responsibility |
| :--- | :--- | :--- |
| WGS84 / UTM 46N Projection | [GeographicCoordinateConverter.cs](../UnityProject/Assets/Scripts/GIS/GeographicCoordinateConverter.cs) | WGS84 forward/inverse Transverse Mercator equations, geodesic Haversine distances, East-Up-North Unity mapping |
| JSON Parser | [GeoJsonParser.cs](../UnityProject/Assets/Scripts/GIS/GeoJsonParser.cs) | Zero-dependency RFC 8259 / GeoJSON nested coordinate array parser |
| GIS Feature Asset | [GeoFeatureDatabase.cs](../UnityProject/Assets/Scripts/GIS/GeoFeatureDatabase.cs) | `ScriptableObject` holding `GeoRoadFeature`, `GeoBuildingFeature`, `GeoWaterFeature`, `GeoLandmarkFeature`, `GeoMetroRailFeature`, OSM metadata tags, and spatial queries |
| Floating-Origin Runtime | [WorldCoordinateManager.cs](../UnityProject/Assets/Scripts/GIS/WorldCoordinateManager.cs) | Double-precision UTM origin anchor, automatic floating-origin rebasing, local/global coordinate transforms |
| Reusable Importer | [DhakaGeoImporter.cs](../UnityProject/Assets/Scripts/GIS/DhakaGeoImporter.cs) | Imports any GeoJSON `FeatureCollection` and procedurally generates real-scale roads, ear-clipped extruded building footprints, waterways/lakes, elevated MRT-6/rail ribbons, and `GeoLandmarkAnchor` objects |
| Scene & Asset Menu | [DhakaGeoEditorTools.cs](../UnityProject/Assets/Scripts/Editor/DhakaGeoEditorTools.cs) | MenuItems building `DhakaOsmFeatureDatabase.asset` and `GeographicTestScene.unity` with HDRP lighting & telemetry HUD |
| Test Suite | [GeographicValidationTests.cs](../UnityProject/Assets/Tests/EditMode/GeographicValidationTests.cs) | NUnit validation of UTM round-trip precision, 1:1 Dhaka distances, floating-origin invariance, and OSM GeoJSON ingestion |

- **Current OSM Extract (`GeographicData/dhaka_osm_extract.geojson`):** `2,564` real OpenStreetMap features (`908` roads, `1,283` building footprints, `140` water features, `147` metro/railway ways & stations including elevated MRT Line 6, and `86` landmarks including Ahsan Manzil, Lalbagh Fort, Sadarghat BIWTA Port, and Hatirjheel).
