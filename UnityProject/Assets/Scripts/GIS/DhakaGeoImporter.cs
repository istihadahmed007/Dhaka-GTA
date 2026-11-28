using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DhakaGta.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DhakaGta.GIS
{
    /// <summary>
    /// Reusable OpenStreetMap GeoJSON importer targeting UTM Zone 46N (EPSG:32646) at 1 Unity meter = 1 real meter.
    /// Parses roads, building footprints, waterways/lakes, metro/rail lines, and landmarks into GeoFeatureDatabase,
    /// and instantiates real-scale procedural geometry under WorldCoordinateManager.
    /// </summary>
    public static class DhakaGeoImporter
    {
        private const int MaxSceneFeaturesPerLayer = 2000;

        public sealed class SceneBuildMaterials
        {
            public Material RoadMaterial;
            public Material BuildingMaterial;
            public Material WaterMaterial;
            public Material MetroMaterial;
            public Material RailMaterial;
            public Material LandmarkMaterial;
        }

        public struct SceneBuildSummary
        {
            public int RoadMeshesCreated;
            public int BuildingMeshesCreated;
            public int WaterMeshesCreated;
            public int MetroRailMeshesCreated;
            public int LandmarksSpawned;
        }

        /// <summary>
        /// Parses an OpenStreetMap GeoJSON file on disk into a GeoFeatureDatabase using exact UTM Zone 46N projection.
        /// </summary>
        public static bool ImportGeoJsonFileIntoDatabase(string geoJsonPath, GeoFeatureDatabase targetDatabase)
        {
            if (targetDatabase == null || string.IsNullOrWhiteSpace(geoJsonPath) || !File.Exists(geoJsonPath))
            {
                Debug.LogError("[DhakaGeoImporter] Missing GeoJSON file or target GeoFeatureDatabase: " + geoJsonPath);
                return false;
            }

            string jsonText = File.ReadAllText(geoJsonPath);
            return ImportGeoJsonTextIntoDatabase(jsonText, targetDatabase);
        }

        /// <summary>
        /// Parses RFC 7946 GeoJSON text into double-precision UTM 46N features inside targetDatabase.
        /// </summary>
        public static bool ImportGeoJsonTextIntoDatabase(string geoJsonText, GeoFeatureDatabase targetDatabase)
        {
            if (targetDatabase == null || string.IsNullOrWhiteSpace(geoJsonText))
            {
                return false;
            }

            if (!(GeoJsonParser.Parse(geoJsonText) is Dictionary<string, object> root))
            {
                Debug.LogError("[DhakaGeoImporter] GeoJSON root must be a JSON object.");
                return false;
            }

            targetDatabase.ClearAll();
            targetDatabase.utmZone = GeographicCoordinateConverter.DhakaUtmZone;
            targetDatabase.projectionEpsg = "EPSG:32646 (WGS 84 / UTM Zone 46N)";

            if (root.TryGetValue("attribution", out object attrObj) && attrObj is string attrStr && !string.IsNullOrEmpty(attrStr))
            {
                targetDatabase.sourceAttribution = attrStr;
            }

            double originLat = GeographicCoordinateConverter.ShaplaOriginLat;
            double originLon = GeographicCoordinateConverter.ShaplaOriginLon;
            if (root.TryGetValue("originWgs84", out object origObj) && origObj is Dictionary<string, object> origDict)
            {
                originLat = ReadDouble(origDict, "lat", originLat);
                originLon = ReadDouble(origDict, "lon", originLon);
            }

            targetDatabase.worldOriginWgs84 = new GeoLatLon(originLat, originLon, 0.0);
            targetDatabase.worldOriginUtm = GeographicCoordinateConverter.LatLonToUtm(originLat, originLon, GeographicCoordinateConverter.DhakaUtmZone);

            if (!root.TryGetValue("features", out object featsObj) || !(featsObj is List<object> featureList))
            {
                Debug.LogError("[DhakaGeoImporter] GeoJSON contains no 'features' array.");
                return false;
            }

            foreach (object item in featureList)
            {
                if (!(item is Dictionary<string, object> feat))
                {
                    continue;
                }

                IngestFeature(feat, targetDatabase);
            }

            if (targetDatabase.TotalFeatureCount == 0)
            {
                targetDatabase.minEasting = targetDatabase.worldOriginUtm.easting;
                targetDatabase.maxEasting = targetDatabase.worldOriginUtm.easting;
                targetDatabase.minNorthing = targetDatabase.worldOriginUtm.northing;
                targetDatabase.maxNorthing = targetDatabase.worldOriginUtm.northing;
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(targetDatabase);
#endif
            return true;
        }

        /// <summary>
        /// Spawns 1:1 scale roads, extruded buildings, waterways/lakes, MRT-6/rail tracks, and landmarks into the active scene,
        /// anchored cleanly to WorldCoordinateManager's double-precision UTM datum.
        /// </summary>
        public static SceneBuildSummary InstantiateDatabaseInScene(
            GeoFeatureDatabase db,
            WorldCoordinateManager coordManager,
            Transform rootTransform,
            double maxDistanceFromOriginMeters = 0.0,
            SceneBuildMaterials materials = null)
        {
            var summary = new SceneBuildSummary();
            if (db == null || coordManager == null || rootTransform == null)
            {
                return summary;
            }

            coordManager.RegisterFloatingRoot(rootTransform);
            UtmCoord originUtm = coordManager.BaseOriginUtm;
            materials ??= new SceneBuildMaterials();

            Transform roadsRoot = CreateOrClearChild(rootTransform, "GIS_Roads");
            Transform buildingsRoot = CreateOrClearChild(rootTransform, "GIS_Buildings");
            Transform waterRoot = CreateOrClearChild(rootTransform, "GIS_WaterBodies");
            Transform metroRoot = CreateOrClearChild(rootTransform, "GIS_MetroAndRailways");
            Transform landmarksRoot = CreateOrClearChild(rootTransform, "GIS_Landmarks");

            foreach (GeoRoadFeature road in db.roads)
            {
                if (summary.RoadMeshesCreated >= MaxSceneFeaturesPerLayer) break;
                if (road?.pointsUtm == null || road.pointsUtm.Length < 2) continue;
                if (maxDistanceFromOriginMeters > 0 && road.pointsUtm[0].Distance2D(originUtm) > maxDistanceFromOriginMeters) continue;

                Mesh mesh = BuildPolylineRibbonMesh(road.pointsUtm, coordManager, road.widthMeters, 0.05f, 0.08f);
                if (mesh == null) continue;

                CreateMeshObject(
                    roadsRoot,
                    $"Road_{SanitizeName(string.IsNullOrEmpty(road.name) ? road.highwayClass : road.name)}",
                    mesh,
                    materials.RoadMaterial,
                    PhysicsLayers.Ground,
                    addCollider: true,
                    isTrigger: false);
                summary.RoadMeshesCreated++;
            }

            foreach (GeoBuildingFeature bldg in db.buildings)
            {
                if (summary.BuildingMeshesCreated >= MaxSceneFeaturesPerLayer) break;
                if (bldg?.footprintUtm == null || bldg.footprintUtm.Length < 3) continue;
                if (maxDistanceFromOriginMeters > 0 && bldg.centroidUtm.Distance2D(originUtm) > maxDistanceFromOriginMeters) continue;

                Mesh mesh = BuildExtrudedPolygonMesh(bldg.footprintUtm, coordManager, bldg.heightMeters, 0f);
                if (mesh == null) continue;

                CreateMeshObject(
                    buildingsRoot,
                    $"Bldg_{SanitizeName(string.IsNullOrEmpty(bldg.name) ? bldg.osmId : bldg.name)}",
                    mesh,
                    materials.BuildingMaterial,
                    PhysicsLayers.Building,
                    addCollider: true,
                    isTrigger: false);
                summary.BuildingMeshesCreated++;
            }

            foreach (GeoWaterFeature water in db.waterBodies)
            {
                if (summary.WaterMeshesCreated >= MaxSceneFeaturesPerLayer) break;
                if (water?.pointsUtm == null || water.pointsUtm.Length < 2) continue;
                if (maxDistanceFromOriginMeters > 0 && water.pointsUtm[0].Distance2D(originUtm) > maxDistanceFromOriginMeters) continue;

                Mesh mesh = water.isPolygon && water.pointsUtm.Length >= 3
                    ? BuildExtrudedPolygonMesh(water.pointsUtm, coordManager, 0.4f, -0.45f)
                    : BuildPolylineRibbonMesh(water.pointsUtm, coordManager, water.channelWidthMeters, -0.15f, 0.2f);
                if (mesh == null) continue;

                CreateMeshObject(
                    waterRoot,
                    $"Water_{SanitizeName(string.IsNullOrEmpty(water.name) ? water.waterKind : water.name)}",
                    mesh,
                    materials.WaterMaterial,
                    PhysicsLayers.Water,
                    addCollider: true,
                    isTrigger: true);
                summary.WaterMeshesCreated++;
            }

            foreach (GeoMetroRailFeature rail in db.metroAndRailways)
            {
                if (summary.MetroRailMeshesCreated >= MaxSceneFeaturesPerLayer) break;
                if (rail?.pointsUtm == null || rail.pointsUtm.Length == 0) continue;
                if (maxDistanceFromOriginMeters > 0 && rail.pointsUtm[0].Distance2D(originUtm) > maxDistanceFromOriginMeters) continue;

                if (rail.isStationPoint || rail.pointsUtm.Length < 2)
                {
                    Vector3 stationPos = coordManager.UtmToUnityPosition(rail.pointsUtm[0], rail.elevationMeters);
                    GameObject stationMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stationMarker.name = $"Station_{SanitizeName(string.IsNullOrEmpty(rail.name) ? rail.osmId : rail.name)}";
                    stationMarker.layer = PhysicsLayers.Building;
                    stationMarker.transform.SetParent(metroRoot, false);
                    stationMarker.transform.position = stationPos + Vector3.up * 2.5f;
                    stationMarker.transform.localScale = new Vector3(14f, 5f, 24f);
                    if (materials.MetroMaterial != null)
                    {
                        stationMarker.GetComponent<MeshRenderer>().sharedMaterial = materials.MetroMaterial;
                    }
                    summary.MetroRailMeshesCreated++;
                    continue;
                }

                bool isElevated = rail.elevationMeters > 2f;
                Mesh mesh = BuildPolylineRibbonMesh(
                    rail.pointsUtm,
                    coordManager,
                    rail.trackWidthMeters,
                    rail.elevationMeters,
                    isElevated ? 1.8f : 0.35f);
                if (mesh == null) continue;

                CreateMeshObject(
                    metroRoot,
                    $"Track_{SanitizeName(string.IsNullOrEmpty(rail.name) ? rail.railwayType : rail.name)}",
                    mesh,
                    isElevated ? materials.MetroMaterial : materials.RailMaterial,
                    isElevated ? PhysicsLayers.Building : PhysicsLayers.Ground,
                    addCollider: true,
                    isTrigger: false);
                summary.MetroRailMeshesCreated++;
            }

            foreach (GeoLandmarkFeature lm in db.landmarks)
            {
                if (lm == null) continue;
                if (maxDistanceFromOriginMeters > 0 && lm.coordinateUtm.Distance2D(originUtm) > maxDistanceFromOriginMeters) continue;

                Vector3 worldPos = coordManager.UtmToUnityPosition(lm.coordinateUtm, lm.coordinateWgs84.elevationMeters);
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Landmark_{SanitizeName(string.IsNullOrEmpty(lm.name) ? lm.osmId : lm.name)}";
                marker.layer = PhysicsLayers.Building;
                marker.transform.SetParent(landmarksRoot, false);
                marker.transform.position = worldPos + Vector3.up * 18f;
                marker.transform.localScale = new Vector3(5f, 18f, 5f);
                if (materials.LandmarkMaterial != null)
                {
                    marker.GetComponent<MeshRenderer>().sharedMaterial = materials.LandmarkMaterial;
                }

                var anchor = marker.AddComponent<GeoLandmarkAnchor>();
                anchor.Initialize(lm);
                summary.LandmarksSpawned++;
            }

            return summary;
        }

        private static void IngestFeature(Dictionary<string, object> feat, GeoFeatureDatabase db)
        {
            if (!feat.TryGetValue("geometry", out object geomObj) || !(geomObj is Dictionary<string, object> geom))
            {
                return;
            }

            feat.TryGetValue("properties", out object propsObj);
            var props = propsObj as Dictionary<string, object> ?? new Dictionary<string, object>();

            string osmId = ReadString(props, "osm_id", ReadString(feat, "id", string.Empty));
            string name = ResolveDisplayName(props);
            string banglaName = ReadString(props, "name_bn", ReadString(props, "name:bn", string.Empty));
            string geomType = ReadString(geom, "type", string.Empty);

            if (!geom.TryGetValue("coordinates", out object coordsObj) || !(coordsObj is List<object> coordsList))
            {
                return;
            }

            GeoTagPair[] metadata = ExtractMetadata(props);
            GeoFeatureCategory category = ResolveCategory(props, geomType);

            switch (category)
            {
                case GeoFeatureCategory.Road:
                    IngestRoad(osmId, name, banglaName, props, geomType, coordsList, metadata, db);
                    break;
                case GeoFeatureCategory.Building:
                    IngestBuilding(osmId, name, banglaName, props, geomType, coordsList, metadata, db);
                    break;
                case GeoFeatureCategory.Water:
                    IngestWater(osmId, name, banglaName, props, geomType, coordsList, metadata, db);
                    break;
                case GeoFeatureCategory.MetroRailway:
                    IngestMetroRailway(osmId, name, banglaName, props, geomType, coordsList, metadata, db);
                    break;
                default:
                    if (ReadString(props, "category", "") == "traffic_signal" || ReadString(props, "highway", "") == "traffic_signals")
                    {
                        IngestTrafficSignal(osmId, name, props, geomType, coordsList, metadata, db);
                    }
                    else
                    {
                        IngestLandmark(osmId, name, banglaName, props, geomType, coordsList, metadata, db);
                    }
                    break;
            }
        }

        private static GeoFeatureCategory ResolveCategory(Dictionary<string, object> props, string geomType)
        {
            string explicitCategory = ReadString(props, "category", string.Empty).ToLowerInvariant();
            switch (explicitCategory)
            {
                case "road": return GeoFeatureCategory.Road;
                case "building": return GeoFeatureCategory.Building;
                case "water": return GeoFeatureCategory.Water;
                case "metro_railway": return GeoFeatureCategory.MetroRailway;
                case "landmark": return GeoFeatureCategory.Landmark;
            }

            if (!string.IsNullOrEmpty(ReadString(props, "railway", "")) || ReadString(props, "station", "") == "subway")
            {
                return GeoFeatureCategory.MetroRailway;
            }
            if (!string.IsNullOrEmpty(ReadString(props, "waterway", "")) || ReadString(props, "natural", "") == "water")
            {
                return GeoFeatureCategory.Water;
            }
            if (!string.IsNullOrEmpty(ReadString(props, "building", "")) && (geomType == "Polygon" || geomType == "MultiPolygon"))
            {
                return GeoFeatureCategory.Building;
            }
            if (!string.IsNullOrEmpty(ReadString(props, "highway", "")))
            {
                return GeoFeatureCategory.Road;
            }
            return GeoFeatureCategory.Landmark;
        }

        private static void IngestRoad(
            string osmId, string name, string banglaName, Dictionary<string, object> props,
            string geomType, List<object> coordsList, GeoTagPair[] metadata, GeoFeatureDatabase db)
        {
            List<GeoLatLon[]> lines = ExtractLineStrings(geomType, coordsList);
            if (lines.Count == 0) return;

            string highwayClass = ReadString(props, "highway", "unclassified");
            int lanes = ParseIntTag(ReadString(props, "lanes", string.Empty), InferLanes(highwayClass));
            float width = Math.Max(4.5f, lanes * 3.5f);
            string junctionType = ReadString(props, "junction", string.Empty).ToLowerInvariant();
            string bridgeTag = ReadString(props, "bridge", string.Empty).ToLowerInvariant();
            string tunnelTag = ReadString(props, "tunnel", string.Empty).ToLowerInvariant();
            int osmLayer = ParseSignedIntTag(ReadString(props, "layer", string.Empty), 0);
            bool isBridge = !string.IsNullOrEmpty(bridgeTag) && bridgeTag != "no" || osmLayer > 0;
            bool isUnderpass = !string.IsNullOrEmpty(tunnelTag) && tunnelTag != "no" || osmLayer < 0;
            bool isFlyover = isBridge && (osmLayer >= 2 ||
                highwayClass == "trunk" || highwayClass == "motorway" || highwayClass == "trunk_link" || highwayClass == "motorway_link" ||
                name.IndexOf("flyover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("overpass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Contains("ফ্লাইওভার"));
            string onewayTag = ReadString(props, "oneway", string.Empty).ToLowerInvariant();
            bool isOneWay = onewayTag == "yes" || onewayTag == "1" || onewayTag == "true" ||
                            junctionType == "roundabout" || junctionType == "circular";
            float maxSpeedKph = ParseFloatMeters(ReadString(props, "maxspeed", string.Empty), InferSpeedLimitKph(highwayClass));
            string sidewalkTag = ReadString(props, "sidewalk", string.Empty).ToLowerInvariant();
            bool hasSidewalk = sidewalkTag != "no" && sidewalkTag != "none" && !isBridge && !isUnderpass && highwayClass != "motorway";
            long[] nodeIds = ParseNodeIds(ReadString(props, "osm_node_ids", string.Empty));

            foreach (GeoLatLon[] wgsLine in lines)
            {
                UtmCoord[] utmLine = ProjectRingOrLine(wgsLine, db);
                if (utmLine.Length < 2) continue;

                db.roads.Add(new GeoRoadFeature
                {
                    osmId = osmId,
                    name = name,
                    banglaName = banglaName,
                    highwayClass = highwayClass,
                    junctionType = junctionType,
                    surface = ReadString(props, "surface", "asphalt"),
                    isBridge = isBridge,
                    isFlyover = isFlyover,
                    isUnderpass = isUnderpass,
                    isOneWay = isOneWay,
                    hasSidewalk = hasSidewalk,
                    osmLayer = osmLayer,
                    lanes = lanes,
                    widthMeters = width,
                    maxSpeedKph = maxSpeedKph,
                    osmNodeIds = nodeIds,
                    pointsUtm = utmLine,
                    pointsWgs84 = wgsLine,
                    metadata = metadata,
                });
            }
        }

        private static void IngestBuilding(
            string osmId, string name, string banglaName, Dictionary<string, object> props,
            string geomType, List<object> coordsList, GeoTagPair[] metadata, GeoFeatureDatabase db)
        {
            GeoLatLon[] wgsRing = ExtractOuterRing(geomType, coordsList);
            if (wgsRing.Length < 3) return;

            UtmCoord[] utmRing = ProjectRingOrLine(wgsRing, db);
            UtmCoord centroid = ComputeCentroid(utmRing);

            int levels = ParseIntTag(ReadString(props, "building_levels", ReadString(props, "building:levels", "")), 3);
            float heightMeters = ParseFloatMeters(ReadString(props, "height", ""), Mathf.Clamp(levels, 1, 60) * 3.4f);

            db.buildings.Add(new GeoBuildingFeature
            {
                osmId = osmId,
                name = name,
                banglaName = banglaName,
                buildingType = ReadString(props, "building", "yes"),
                levels = levels,
                heightMeters = heightMeters,
                centroidUtm = centroid,
                footprintUtm = utmRing,
                footprintWgs84 = wgsRing,
                metadata = metadata,
            });
        }

        private static void IngestWater(
            string osmId, string name, string banglaName, Dictionary<string, object> props,
            string geomType, List<object> coordsList, GeoTagPair[] metadata, GeoFeatureDatabase db)
        {
            bool isPolygon = geomType == "Polygon" || geomType == "MultiPolygon";
            if (isPolygon)
            {
                GeoLatLon[] wgsRing = ExtractOuterRing(geomType, coordsList);
                if (wgsRing.Length < 3) return;
                UtmCoord[] utmRing = ProjectRingOrLine(wgsRing, db);
                db.waterBodies.Add(new GeoWaterFeature
                {
                    osmId = osmId,
                    name = name,
                    banglaName = banglaName,
                    waterKind = ReadString(props, "natural", ReadString(props, "waterway", "water")),
                    isPolygon = true,
                    pointsUtm = utmRing,
                    pointsWgs84 = wgsRing,
                    metadata = metadata,
                });
                return;
            }

            List<GeoLatLon[]> lines = ExtractLineStrings(geomType, coordsList);
            string kind = ReadString(props, "waterway", "river");
            float width = kind == "river" ? 85f : 20f;
            foreach (GeoLatLon[] line in lines)
            {
                UtmCoord[] utmLine = ProjectRingOrLine(line, db);
                if (utmLine.Length < 2) continue;
                db.waterBodies.Add(new GeoWaterFeature
                {
                    osmId = osmId,
                    name = name,
                    banglaName = banglaName,
                    waterKind = kind,
                    isPolygon = false,
                    channelWidthMeters = width,
                    pointsUtm = utmLine,
                    pointsWgs84 = line,
                    metadata = metadata,
                });
            }
        }

        private static void IngestMetroRailway(
            string osmId, string name, string banglaName, Dictionary<string, object> props,
            string geomType, List<object> coordsList, GeoTagPair[] metadata, GeoFeatureDatabase db)
        {
            string railwayType = ReadString(props, "railway", ReadString(props, "station", "rail"));
            bool isElevatedMrt = railwayType == "subway" || railwayType == "monorail" || railwayType == "light_rail"
                                 || name.IndexOf("metro", StringComparison.OrdinalIgnoreCase) >= 0
                                 || name.Contains("মেট্রো");
            float elevMeters = isElevatedMrt ? 11.5f : 0.25f;

            if (geomType == "Point" && TryReadPoint(coordsList, out GeoLatLon pt))
            {
                UtmCoord utm = ProjectPoint(pt, db);
                db.metroAndRailways.Add(new GeoMetroRailFeature
                {
                    osmId = osmId,
                    name = name,
                    banglaName = banglaName,
                    railwayType = railwayType,
                    isStationPoint = true,
                    elevationMeters = elevMeters,
                    pointsUtm = new[] { utm },
                    pointsWgs84 = new[] { pt },
                    metadata = metadata,
                });
                return;
            }

            List<GeoLatLon[]> lines = ExtractLineStrings(geomType, coordsList);
            foreach (GeoLatLon[] line in lines)
            {
                UtmCoord[] utmLine = ProjectRingOrLine(line, db);
                if (utmLine.Length < 2) continue;
                db.metroAndRailways.Add(new GeoMetroRailFeature
                {
                    osmId = osmId,
                    name = name,
                    banglaName = banglaName,
                    railwayType = railwayType,
                    isStationPoint = false,
                    elevationMeters = elevMeters,
                    trackWidthMeters = isElevatedMrt ? 8.5f : 5.5f,
                    pointsUtm = utmLine,
                    pointsWgs84 = line,
                    metadata = metadata,
                });
            }
        }

        private static void IngestLandmark(
            string osmId, string name, string banglaName, Dictionary<string, object> props,
            string geomType, List<object> coordsList, GeoTagPair[] metadata, GeoFeatureDatabase db)
        {
            GeoLatLon anchorWgs;
            UtmCoord anchorUtm;

            if (geomType == "Point" && TryReadPoint(coordsList, out anchorWgs))
            {
                anchorUtm = ProjectPoint(anchorWgs, db);
            }
            else
            {
                GeoLatLon[] ring = ExtractOuterRing(geomType, coordsList);
                if (ring.Length == 0) return;
                UtmCoord[] utmPts = ProjectRingOrLine(ring, db);
                anchorUtm = ComputeCentroid(utmPts);
                anchorWgs = GeographicCoordinateConverter.UtmToLatLon(anchorUtm, 0.0);
            }

            string kind = ReadString(props, "historic", ReadString(props, "amenity", ReadString(props, "place", "landmark")));
            db.landmarks.Add(new GeoLandmarkFeature
            {
                osmId = osmId,
                name = string.IsNullOrEmpty(name) ? osmId : name,
                banglaName = banglaName,
                kind = kind,
                coordinateWgs84 = anchorWgs,
                coordinateUtm = anchorUtm,
                metadata = metadata,
            });
        }

        private static void IngestTrafficSignal(
            string osmId, string name, Dictionary<string, object> props,
            string geomType, List<object> coordsList, GeoTagPair[] metadata, GeoFeatureDatabase db)
        {
            if (geomType == "Point" && TryReadPoint(coordsList, out GeoLatLon pt))
            {
                UtmCoord utm = ProjectPoint(pt, db);
                long[] ids = ParseNodeIds(ReadString(props, "osm_node_ids", string.Empty));
                db.trafficSignals.Add(new GeoTrafficSignalFeature
                {
                    osmId = osmId,
                    name = name,
                    osmNodeId = ids.Length > 0 ? ids[0] : 0,
                    coordinateWgs84 = pt,
                    coordinateUtm = utm,
                    metadata = metadata,
                });
            }
        }

        private static long[] ParseNodeIds(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<long>();
            string[] parts = csv.Split(',');
            var ids = new long[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out ids[i]);
            }
            return ids;
        }

        private static int ParseSignedIntTag(string text, int fallback)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        }

        private static float InferSpeedLimitKph(string highwayClass)
        {
            return highwayClass switch
            {
                "motorway" or "trunk" => 70f,
                "primary" => 60f,
                "secondary" => 50f,
                "tertiary" => 40f,
                _ => 30f,
            };
        }

        private static UtmCoord ProjectPoint(GeoLatLon pt, GeoFeatureDatabase db)
        {
            UtmCoord utm = GeographicCoordinateConverter.LatLonToUtm(pt.latitude, pt.longitude, GeographicCoordinateConverter.DhakaUtmZone);
            db.ExpandBounds(utm);
            return utm;
        }

        private static UtmCoord[] ProjectRingOrLine(GeoLatLon[] pts, GeoFeatureDatabase db)
        {
            var result = new UtmCoord[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                result[i] = ProjectPoint(pts[i], db);
            }
            return result;
        }

        private static List<GeoLatLon[]> ExtractLineStrings(string geomType, List<object> coordsList)
        {
            var result = new List<GeoLatLon[]>();
            if (geomType == "LineString")
            {
                GeoLatLon[] line = ParseCoordArray(coordsList);
                if (line.Length >= 2) result.Add(line);
            }
            else if (geomType == "MultiLineString")
            {
                foreach (object sub in coordsList)
                {
                    if (sub is List<object> subList)
                    {
                        GeoLatLon[] line = ParseCoordArray(subList);
                        if (line.Length >= 2) result.Add(line);
                    }
                }
            }
            return result;
        }

        private static GeoLatLon[] ExtractOuterRing(string geomType, List<object> coordsList)
        {
            if (geomType == "Polygon" && coordsList.Count > 0 && coordsList[0] is List<object> ringList)
            {
                return ParseCoordArray(ringList);
            }
            if (geomType == "MultiPolygon" && coordsList.Count > 0 && coordsList[0] is List<object> poly && poly.Count > 0 && poly[0] is List<object> outerRing)
            {
                return ParseCoordArray(outerRing);
            }
            if (geomType == "LineString")
            {
                return ParseCoordArray(coordsList);
            }
            return Array.Empty<GeoLatLon>();
        }

        private static GeoLatLon[] ParseCoordArray(List<object> rawList)
        {
            var pts = new List<GeoLatLon>(rawList.Count);
            foreach (object item in rawList)
            {
                if (item is List<object> pair && TryReadPoint(pair, out GeoLatLon ll))
                {
                    pts.Add(ll);
                }
            }
            return pts.ToArray();
        }

        private static bool TryReadPoint(List<object> pair, out GeoLatLon pt)
        {
            pt = default;
            if (pair == null || pair.Count < 2)
            {
                return false;
            }

            double lon = Convert.ToDouble(pair[0], CultureInfo.InvariantCulture);
            double lat = Convert.ToDouble(pair[1], CultureInfo.InvariantCulture);
            double elev = pair.Count >= 3 ? Convert.ToDouble(pair[2], CultureInfo.InvariantCulture) : 0.0;
            pt = new GeoLatLon(lat, lon, elev);
            return true;
        }

        private static UtmCoord ComputeCentroid(UtmCoord[] pts)
        {
            if (pts == null || pts.Length == 0)
            {
                return default;
            }

            int count = pts.Length;
            if (count > 1 && pts[0].Distance2D(pts[count - 1]) < 0.01)
            {
                count--;
            }

            double sumE = 0.0, sumN = 0.0;
            for (int i = 0; i < count; i++)
            {
                sumE += pts[i].easting;
                sumN += pts[i].northing;
            }

            return new UtmCoord(sumE / count, sumN / count, pts[0].zone, pts[0].isNorthernHemisphere);
        }

        private static Mesh BuildPolylineRibbonMesh(
            UtmCoord[] pointsUtm,
            WorldCoordinateManager coordManager,
            float widthMeters,
            float baseElevationY,
            float thicknessY)
        {
            if (pointsUtm == null || pointsUtm.Length < 2) return null;

            float halfW = Mathf.Max(0.5f, widthMeters * 0.5f);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            float distAccum = 0f;
            for (int i = 0; i < pointsUtm.Length; i++)
            {
                Vector3 p = coordManager.UtmToUnityPosition(pointsUtm[i], baseElevationY);
                Vector3 pPrev = i > 0 ? coordManager.UtmToUnityPosition(pointsUtm[i - 1], baseElevationY) : p;
                Vector3 pNext = i < pointsUtm.Length - 1 ? coordManager.UtmToUnityPosition(pointsUtm[i + 1], baseElevationY) : p;

                if (i > 0) distAccum += Vector3.Distance(pPrev, p);

                Vector3 dir = (pNext - pPrev);
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
                dir.Normalize();

                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                vertices.Add(p - right * halfW + Vector3.up * thicknessY);
                vertices.Add(p + right * halfW + Vector3.up * thicknessY);
                uvs.Add(new Vector2(0f, distAccum * 0.1f));
                uvs.Add(new Vector2(1f, distAccum * 0.1f));

                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    triangles.Add(b);
                    triangles.Add(b + 2);
                    triangles.Add(b + 1);

                    triangles.Add(b + 1);
                    triangles.Add(b + 2);
                    triangles.Add(b + 3);
                }
            }

            var mesh = new Mesh { name = "GeoRibbonMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildExtrudedPolygonMesh(
            UtmCoord[] ringUtm,
            WorldCoordinateManager coordManager,
            float heightMeters,
            float baseY)
        {
            if (ringUtm == null || ringUtm.Length < 3) return null;

            int count = ringUtm.Length;
            if (count > 3 && ringUtm[0].Distance2D(ringUtm[count - 1]) < 0.05)
            {
                count--;
            }
            if (count < 3) return null;

            var cleanPos = new System.Collections.Generic.List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = coordManager.UtmToUnityPosition(ringUtm[i], baseY);
                if (cleanPos.Count == 0 || Vector3.SqrMagnitude(pos - cleanPos[cleanPos.Count - 1]) > 0.04f)
                {
                    cleanPos.Add(pos);
                }
            }
            if (cleanPos.Count > 2 && Vector3.SqrMagnitude(cleanPos[0] - cleanPos[cleanPos.Count - 1]) <= 0.04f)
            {
                cleanPos.RemoveAt(cleanPos.Count - 1);
            }
            if (cleanPos.Count < 3) return null;

            count = cleanPos.Count;
            var poly2D = new Vector2[count];
            var baseVerts = cleanPos.ToArray();
            for (int i = 0; i < count; i++)
            {
                poly2D[i] = new Vector2(baseVerts[i].x, baseVerts[i].z);
            }

            // Ensure counter-clockwise winding in (x, z) so upward normal is +Y.
            if (ComputeSignedArea2D(poly2D) < 0f)
            {
                Array.Reverse(poly2D);
                Array.Reverse(baseVerts);
            }

            List<int> roofTris = TriangulateSimplePolygon2D(poly2D);
            if (roofTris.Count < 3) return null;

            var vertices = new List<Vector3>(count * 5);
            var uvs = new List<Vector2>(count * 5);
            var triangles = new List<int>(roofTris.Count + count * 6);

            // Roof cap (indices 0..count-1)
            for (int i = 0; i < count; i++)
            {
                vertices.Add(baseVerts[i] + Vector3.up * heightMeters);
                uvs.Add(new Vector2(baseVerts[i].x * 0.1f, baseVerts[i].z * 0.1f));
            }
            for (int i = 0; i < roofTris.Count; i += 3)
            {
                triangles.Add(roofTris[i]);
                triangles.Add(roofTris[i + 2]);
                triangles.Add(roofTris[i + 1]);
            }

            // Vertical walls
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int vStart = vertices.Count;
                Vector3 b0 = baseVerts[i];
                Vector3 b1 = baseVerts[next];
                Vector3 t0 = b0 + Vector3.up * heightMeters;
                Vector3 t1 = b1 + Vector3.up * heightMeters;
                float segLen = Vector3.Distance(b0, b1) * 0.1f;
                float uHeight = heightMeters * 0.1f;

                vertices.Add(b0);
                vertices.Add(b1);
                vertices.Add(t1);
                vertices.Add(t0);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(segLen, 0f));
                uvs.Add(new Vector2(segLen, uHeight));
                uvs.Add(new Vector2(0f, uHeight));

                triangles.Add(vStart);
                triangles.Add(vStart + 2);
                triangles.Add(vStart + 1);

                triangles.Add(vStart);
                triangles.Add(vStart + 3);
                triangles.Add(vStart + 2);
            }

            if (triangles.Count < 3) return null;

            var mesh = new Mesh { name = "GeoBuildingExtrusion" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float ComputeSignedArea2D(Vector2[] poly)
        {
            double area = 0.0;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                area += (double)a.x * b.y - (double)b.x * a.y;
            }
            return (float)(area * 0.5);
        }

        private static List<int> TriangulateSimplePolygon2D(Vector2[] poly)
        {
            var indices = new List<int>(poly.Length);
            for (int i = 0; i < poly.Length; i++) indices.Add(i);

            var tris = new List<int>((poly.Length - 2) * 3);
            int guard = 0;
            int idx = 0;
            while (indices.Count > 2 && guard < poly.Length * poly.Length)
            {
                guard++;
                int n = indices.Count;
                int prev = indices[(idx + n - 1) % n];
                int curr = indices[idx % n];
                int next = indices[(idx + 1) % n];

                if (IsEar(poly, indices, prev, curr, next))
                {
                    tris.Add(prev);
                    tris.Add(curr);
                    tris.Add(next);
                    indices.RemoveAt(idx % n);
                    idx = 0;
                }
                else
                {
                    idx = (idx + 1) % n;
                }
            }

            return tris;
        }

        private static bool IsEar(Vector2[] poly, List<int> remaining, int prev, int curr, int next)
        {
            Vector2 a = poly[prev], b = poly[curr], c = poly[next];
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (cross <= 1e-5f) return false;

            foreach (int pIdx in remaining)
            {
                if (pIdx == prev || pIdx == curr || pIdx == next) continue;
                if (PointInTriangle(poly[pIdx], a, b, c)) return false;
            }
            return true;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            float c2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
            float c3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
            return c1 >= 0f && c2 >= 0f && c3 >= 0f;
        }

        private static void CreateMeshObject(
            Transform parent, string name, Mesh mesh, Material mat, int layer, bool addCollider, bool isTrigger)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (mat != null)
            {
                mr.sharedMaterial = mat;
            }
            if (addCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = isTrigger;
                mc.isTrigger = isTrigger;
            }
        }

        private static Transform CreateOrClearChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                for (int i = existing.childCount - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(existing.GetChild(i).gameObject);
                    else
#endif
                    UnityEngine.Object.Destroy(existing.GetChild(i).gameObject);
                }
                return existing;
            }

            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static string ResolveDisplayName(Dictionary<string, object> props)
        {
            string en = ReadString(props, "name_en", ReadString(props, "name:en", string.Empty));
            string def = ReadString(props, "name", string.Empty);
            return !string.IsNullOrWhiteSpace(en) ? en : def;
        }

        private static string ReadString(Dictionary<string, object> dict, string key, string fallback)
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val != null)
            {
                string s = val.ToString();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            return fallback;
        }

        private static double ReadDouble(Dictionary<string, object> dict, string key, double fallback)
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val != null)
            {
                if (double.TryParse(val.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    return d;
                }
            }
            return fallback;
        }

        private static int ParseIntTag(string text, int fallback)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0)
            {
                return v;
            }
            return fallback;
        }

        private static float ParseFloatMeters(string text, float fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            string cleaned = text.Replace("m", "").Replace(" ", "").Trim();
            return float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out float h) && h > 1f
                ? h
                : fallback;
        }

        private static int InferLanes(string highwayClass)
        {
            return highwayClass switch
            {
                "motorway" or "trunk" => 6,
                "primary" => 4,
                "secondary" => 3,
                "tertiary" => 2,
                _ => 2,
            };
        }

        private static string SanitizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Unnamed";
            return input.Replace('/', '_').Replace('\\', '_').Trim();
        }

        private static GeoTagPair[] ExtractMetadata(Dictionary<string, object> props)
        {
            var pairs = new List<GeoTagPair>(props.Count);
            foreach (var kvp in props)
            {
                string v = kvp.Value != null ? kvp.Value.ToString() : string.Empty;
                if (!string.IsNullOrEmpty(v))
                {
                    pairs.Add(new GeoTagPair { key = kvp.Key, value = v });
                }
            }
            return pairs.ToArray();
        }
    }
}
