using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.GIS;
using UnityEngine;

namespace DhakaGta.WorldGen
{
    /// <summary>
    /// Generates drivable 1:1 scale HDRP road carriageways (with MeshCollider), lane dividers, curbs, sidewalks,
    /// bridges with guard rails & piers, elevated flyover viaducts, recessed underpasses, and Dhaka rivers/canals/lakes
    /// directly from RoadSplineSystem & GeoFeatureDatabase.
    /// </summary>
    public sealed class RoadGenerator : MonoBehaviour
    {
        public sealed class RoadVisualMaterials
        {
            public Material AsphaltMaterial;
            public Material BridgeDeckMaterial;
            public Material FlyoverConcreteMaterial;
            public Material UnderpassWallMaterial;
            public Material CurbConcreteMaterial;
            public Material SidewalkTileMaterial;
            public Material WhiteMarkingMaterial;
            public Material YellowMarkingMaterial;
            public Material WaterSurfaceMaterial;
        }

        public struct RoadGenStats
        {
            public int RoadMeshCount;
            public int BridgeCount;
            public int FlyoverCount;
            public int UnderpassCount;
            public int SidewalkSegments;
            public int WaterMeshesCount;
        }

        public RoadGenStats BuildNetworkAndWater(
            RoadSplineSystem splineSystem,
            GeoFeatureDatabase db,
            WorldCoordinateManager coordManager,
            Transform parentRoot,
            RoadVisualMaterials materials = null,
            double waterMaxRadiusMeters = 0.0)
        {
            var stats = new RoadGenStats();
            if (splineSystem == null || parentRoot == null)
            {
                return stats;
            }

            materials ??= new RoadVisualMaterials();

            Transform roadsRoot = CreateCleanChild(parentRoot, "Generated_Roads_Carriageways");
            Transform structuresRoot = CreateCleanChild(parentRoot, "Generated_Bridges_Flyovers_Underpasses");
            Transform sidewalksRoot = CreateCleanChild(parentRoot, "Generated_Curbs_Sidewalks");
            Transform markingsRoot = CreateCleanChild(parentRoot, "Generated_RoadMarkings");
            Transform waterRoot = CreateCleanChild(parentRoot, "Generated_Rivers_Canals_Lakes");

            var whiteMarkVerts = new List<Vector3>();
            var whiteMarkTris = new List<int>();
            var yellowMarkVerts = new List<Vector3>();
            var yellowMarkTris = new List<int>();

            var sidewalkVerts = new List<Vector3>();
            var sidewalkTris = new List<int>();
            var curbVerts = new List<Vector3>();
            var curbTris = new List<int>();

            foreach (RoadSplineSegment seg in splineSystem.Segments)
            {
                if (seg == null || seg.sampledPointsLocal.Length < 2) continue;

                // 1. Drivable Road Carriageway Deck (Ground layer with MeshCollider)
                Mesh deckMesh = BuildCarriagewayDeckMesh(seg);
                Material deckMat = seg.gradeKind switch
                {
                    RoadGradeKind.Bridge => materials.BridgeDeckMaterial ?? materials.AsphaltMaterial,
                    RoadGradeKind.Flyover => materials.FlyoverConcreteMaterial ?? materials.AsphaltMaterial,
                    _ => materials.AsphaltMaterial,
                };

                CreateColliderMeshObj(
                    roadsRoot,
                    $"RoadDeck_{seg.gradeKind}_{Sanitize(seg.roadName)}",
                    deckMesh,
                    deckMat,
                    PhysicsLayers.Ground,
                    isTrigger: false);
                stats.RoadMeshCount++;

                // 2. Lane Boundaries & Road Markings (Dashed lanes + Edge lines + Yellow centerline median)
                AppendRoadMarkings(seg, whiteMarkVerts, whiteMarkTris, yellowMarkVerts, yellowMarkTris);

                // 3. Curbs & Sidewalks on Surface Urban Roads vs Bridges/Flyovers/Underpasses Structural Geometry
                if (seg.gradeKind == RoadGradeKind.Surface && seg.hasSidewalk)
                {
                    AppendCurbsAndSidewalks(seg, curbVerts, curbTris, sidewalkVerts, sidewalkTris);
                    stats.SidewalkSegments++;
                }
                else if (seg.gradeKind == RoadGradeKind.Bridge)
                {
                    BuildBridgeStructure(seg, structuresRoot, materials);
                    stats.BridgeCount++;
                }
                else if (seg.gradeKind == RoadGradeKind.Flyover)
                {
                    BuildFlyoverStructure(seg, structuresRoot, materials);
                    stats.FlyoverCount++;
                }
                else if (seg.gradeKind == RoadGradeKind.Underpass)
                {
                    BuildUnderpassRetainingWalls(seg, structuresRoot, materials);
                    stats.UnderpassCount++;
                }
            }

            // Bake batched Road Markings (White + Yellow)
            BakeBatchedMesh(markingsRoot, "RoadMarkings_White", whiteMarkVerts, whiteMarkTris, materials.WhiteMarkingMaterial, PhysicsLayers.Ground, addCollider: false);
            BakeBatchedMesh(markingsRoot, "RoadMarkings_Yellow", yellowMarkVerts, yellowMarkTris, materials.YellowMarkingMaterial, PhysicsLayers.Ground, addCollider: false);

            // Bake batched Sidewalks & Curbs (Ground collider so player/tires feel real curb step!)
            BakeBatchedMesh(sidewalksRoot, "Sidewalks_Batched", sidewalkVerts, sidewalkTris, materials.SidewalkTileMaterial, PhysicsLayers.Ground, addCollider: true);
            BakeBatchedMesh(sidewalksRoot, "Curbs_Batched", curbVerts, curbTris, materials.CurbConcreteMaterial, PhysicsLayers.Ground, addCollider: true);

            // 4. Rivers, Canals, Lakes from OpenStreetMap
            if (db?.waterBodies != null && coordManager != null)
            {
                UtmCoord origin = coordManager.BaseOriginUtm;
                foreach (GeoWaterFeature water in db.waterBodies)
                {
                    if (water?.pointsUtm == null || water.pointsUtm.Length < 2) continue;
                    if (waterMaxRadiusMeters > 0.0 && water.pointsUtm[0].Distance2D(origin) > waterMaxRadiusMeters) continue;

                    Mesh waterMesh = water.isPolygon && water.pointsUtm.Length >= 3
                        ? BuildWaterPolygonMesh(water.pointsUtm, coordManager, -0.35f)
                        : BuildWaterRibbonMesh(water.pointsUtm, coordManager, water.channelWidthMeters, -0.35f);

                    if (waterMesh == null) continue;
                    CreateColliderMeshObj(
                        waterRoot,
                        $"Water_{water.waterKind}_{Sanitize(water.name)}",
                        waterMesh,
                        materials.WaterSurfaceMaterial,
                        PhysicsLayers.Water,
                        isTrigger: true);
                    stats.WaterMeshesCount++;
                }
            }

            return stats;
        }

        private static Mesh BuildCarriagewayDeckMesh(RoadSplineSegment seg)
        {
            Vector3[] pts = seg.sampledPointsLocal;
            float halfW = seg.widthMeters * 0.5f;
            var verts = new List<Vector3>(pts.Length * 2);
            var uvs = new List<Vector2>(pts.Length * 2);
            var tris = new List<int>((pts.Length - 1) * 6);

            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 tan = seg.sampledTangents[i];
                Vector3 right = new Vector3(tan.z, 0f, -tan.x).normalized;
                float uDist = seg.cumulativeDistances[i] * 0.1f;

                verts.Add(pts[i] - right * halfW);
                verts.Add(pts[i] + right * halfW);
                uvs.Add(new Vector2(0f, uDist));
                uvs.Add(new Vector2(1f, uDist));

                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    tris.Add(b);
                    tris.Add(b + 2);
                    tris.Add(b + 1);

                    tris.Add(b + 1);
                    tris.Add(b + 2);
                    tris.Add(b + 3);
                }
            }

            var mesh = new Mesh { name = $"Deck_{seg.osmId}" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendRoadMarkings(
            RoadSplineSegment seg,
            List<Vector3> whiteVerts, List<int> whiteTris,
            List<Vector3> yellowVerts, List<int> yellowTris)
        {
            float halfW = seg.widthMeters * 0.5f;
            float laneW = seg.widthMeters / Mathf.Max(1, seg.lanes);

            // White Outer Edge Lines
            AppendMarkingRibbon(seg, -halfW + 0.25f, 0.14f, dashed: false, whiteVerts, whiteTris);
            AppendMarkingRibbon(seg, halfW - 0.25f, 0.14f, dashed: false, whiteVerts, whiteTris);

            // Yellow Double Centerline on 2-way roads with >= 2 lanes
            if (!seg.isOneWay && seg.lanes >= 2)
            {
                AppendMarkingRibbon(seg, -0.14f, 0.11f, dashed: false, yellowVerts, yellowTris);
                AppendMarkingRibbon(seg, 0.14f, 0.11f, dashed: false, yellowVerts, yellowTris);
            }

            // Dashed White Lane Boundaries between interior lanes
            for (int lane = 1; lane < seg.lanes; lane++)
            {
                float lat = -halfW + lane * laneW;
                if (!seg.isOneWay && Mathf.Abs(lat) < 0.5f) continue; // Don't overwrite yellow centerline
                AppendMarkingRibbon(seg, lat, 0.12f, dashed: true, whiteVerts, whiteTris);
            }
        }

        private static void AppendMarkingRibbon(
            RoadSplineSegment seg,
            float lateralOffset,
            float stripeWidth,
            bool dashed,
            List<Vector3> verts,
            List<int> tris)
        {
            Vector3[] pts = seg.sampledPointsLocal;
            float hw = stripeWidth * 0.5f;
            const float yOffset = 0.025f;

            for (int i = 0; i < pts.Length - 1; i++)
            {
                if (dashed && (i % 2 == 1))
                {
                    continue; // Alternates dash segments along spline
                }

                Vector3 p0 = pts[i];
                Vector3 p1 = pts[i + 1];
                Vector3 t0 = seg.sampledTangents[i];
                Vector3 t1 = seg.sampledTangents[i + 1];
                Vector3 r0 = new Vector3(t0.z, 0f, -t0.x).normalized;
                Vector3 r1 = new Vector3(t1.z, 0f, -t1.x).normalized;

                Vector3 c0 = p0 + r0 * lateralOffset + Vector3.up * yOffset;
                Vector3 c1 = p1 + r1 * lateralOffset + Vector3.up * yOffset;

                int b = verts.Count;
                verts.Add(c0 - r0 * hw);
                verts.Add(c0 + r0 * hw);
                verts.Add(c1 - r1 * hw);
                verts.Add(c1 + r1 * hw);

                tris.Add(b);
                tris.Add(b + 2);
                tris.Add(b + 1);

                tris.Add(b + 1);
                tris.Add(b + 2);
                tris.Add(b + 3);
            }
        }

        private static void AppendCurbsAndSidewalks(
            RoadSplineSegment seg,
            List<Vector3> curbVerts, List<int> curbTris,
            List<Vector3> swVerts, List<int> swTris)
        {
            float halfW = seg.widthMeters * 0.5f;
            float curbW = 0.22f;
            float swW = seg.highwayClass == "primary" || seg.highwayClass == "trunk" ? 2.6f : 1.7f;
            const float curbHeight = 0.16f;

            // Left and right sides
            foreach (float sign in new[] { -1f, 1f })
            {
                float innerCurb = sign * halfW;
                float outerCurb = sign * (halfW + curbW);
                float outerSw = sign * (halfW + curbW + swW);

                AppendSideBand(seg, innerCurb, outerCurb, curbHeight, curbVerts, curbTris, invertFlip: sign < 0f);
                AppendSideBand(seg, outerCurb, outerSw, curbHeight, swVerts, swTris, invertFlip: sign < 0f);
            }
        }

        private static void AppendSideBand(
            RoadSplineSegment seg,
            float offsetInner,
            float offsetOuter,
            float yHeight,
            List<Vector3> verts,
            List<int> tris,
            bool invertFlip)
        {
            Vector3[] pts = seg.sampledPointsLocal;
            float minLat = Mathf.Min(offsetInner, offsetOuter);
            float maxLat = Mathf.Max(offsetInner, offsetOuter);
            _ = invertFlip;

            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 tan = seg.sampledTangents[i];
                Vector3 right = new Vector3(tan.z, 0f, -tan.x).normalized;
                Vector3 p = pts[i] + Vector3.up * yHeight;

                verts.Add(p + right * minLat);
                verts.Add(p + right * maxLat);

                if (i > 0)
                {
                    int b = verts.Count - 4;
                    tris.Add(b);
                    tris.Add(b + 2);
                    tris.Add(b + 1);

                    tris.Add(b + 1);
                    tris.Add(b + 2);
                    tris.Add(b + 3);
                }
            }
        }

        private static void BuildBridgeStructure(RoadSplineSegment seg, Transform root, RoadVisualMaterials mats)
        {
            BuildParapetWallsAndPiers(
                seg, root, $"Bridge_{Sanitize(seg.roadName)}",
                parapetHeight: 1.05f, deckThickness: 0.9f, pierIntervalMeters: 24f, pierBaseY: -4f,
                mats.BridgeDeckMaterial ?? mats.FlyoverConcreteMaterial);
        }

        private static void BuildFlyoverStructure(RoadSplineSegment seg, Transform root, RoadVisualMaterials mats)
        {
            BuildParapetWallsAndPiers(
                seg, root, $"Flyover_{Sanitize(seg.roadName)}",
                parapetHeight: 1.15f, deckThickness: 1.45f, pierIntervalMeters: 30f, pierBaseY: -1f,
                mats.FlyoverConcreteMaterial);
        }

        private static void BuildUnderpassRetainingWalls(RoadSplineSegment seg, Transform root, RoadVisualMaterials mats)
        {
            float halfW = seg.widthMeters * 0.5f + 0.35f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3[] pts = seg.sampledPointsLocal;

            foreach (float sign in new[] { -1f, 1f })
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector3 r0 = new Vector3(seg.sampledTangents[i].z, 0f, -seg.sampledTangents[i].x).normalized;
                    Vector3 r1 = new Vector3(seg.sampledTangents[i + 1].z, 0f, -seg.sampledTangents[i + 1].x).normalized;

                    Vector3 b0 = pts[i] + r0 * (sign * halfW);
                    Vector3 b1 = pts[i + 1] + r1 * (sign * halfW);
                    Vector3 t0 = new Vector3(b0.x, 0.3f, b0.z);
                    Vector3 t1 = new Vector3(b1.x, 0.3f, b1.z);
                    if (t0.y <= b0.y + 0.1f && t1.y <= b1.y + 0.1f) continue;

                    int v = verts.Count;
                    verts.Add(b0);
                    verts.Add(b1);
                    verts.Add(t1);
                    verts.Add(t0);

                    if (sign > 0f)
                    {
                        tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
                        tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                    }
                    else
                    {
                        tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                        tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
                    }
                }
            }

            BakeBatchedMesh(root, $"UnderpassWalls_{Sanitize(seg.roadName)}", verts, tris, mats.UnderpassWallMaterial ?? mats.CurbConcreteMaterial, PhysicsLayers.Building, addCollider: true);
        }

        private static void BuildParapetWallsAndPiers(
            RoadSplineSegment seg,
            Transform parent,
            string groupName,
            float parapetHeight,
            float deckThickness,
            float pierIntervalMeters,
            float pierBaseY,
            Material concreteMat)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(parent, false);

            float halfW = seg.widthMeters * 0.5f;
            Vector3[] pts = seg.sampledPointsLocal;

            // Side safety parapet walls
            var wallVerts = new List<Vector3>();
            var wallTris = new List<int>();
            foreach (float sign in new[] { -1f, 1f })
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector3 r0 = new Vector3(seg.sampledTangents[i].z, 0f, -seg.sampledTangents[i].x).normalized;
                    Vector3 r1 = new Vector3(seg.sampledTangents[i + 1].z, 0f, -seg.sampledTangents[i + 1].x).normalized;

                    Vector3 b0 = pts[i] + r0 * (sign * halfW) - Vector3.up * deckThickness;
                    Vector3 b1 = pts[i + 1] + r1 * (sign * halfW) - Vector3.up * deckThickness;
                    Vector3 t0 = pts[i] + r0 * (sign * halfW) + Vector3.up * parapetHeight;
                    Vector3 t1 = pts[i + 1] + r1 * (sign * halfW) + Vector3.up * parapetHeight;

                    int v = wallVerts.Count;
                    wallVerts.Add(b0); wallVerts.Add(b1); wallVerts.Add(t1); wallVerts.Add(t0);
                    if (sign > 0f)
                    {
                        wallTris.Add(v); wallTris.Add(v + 2); wallTris.Add(v + 1);
                        wallTris.Add(v); wallTris.Add(v + 3); wallTris.Add(v + 2);
                    }
                    else
                    {
                        wallTris.Add(v); wallTris.Add(v + 1); wallTris.Add(v + 2);
                        wallTris.Add(v); wallTris.Add(v + 2); wallTris.Add(v + 3);
                    }
                }
            }

            BakeBatchedMesh(group.transform, "ParapetRails", wallVerts, wallTris, concreteMat, PhysicsLayers.Building, addCollider: true);

            // Concrete support piers where deck rises above 2.0m
            for (float d = 14f; d < seg.totalLengthMeters - 10f; d += pierIntervalMeters)
            {
                seg.SampleAtDistance(d, out Vector3 pos, out _, out _);
                if (pos.y < 2.2f) continue;

                float colHeight = pos.y - deckThickness - pierBaseY;
                if (colHeight <= 0.5f) continue;

                GameObject pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pier.name = $"Pier_{Mathf.RoundToInt(d)}";
                pier.layer = PhysicsLayers.Building;
                pier.transform.SetParent(group.transform, false);
                pier.transform.position = new Vector3(pos.x, pierBaseY + colHeight * 0.5f, pos.z);
                pier.transform.localScale = new Vector3(2.2f, colHeight, 2.2f);
                if (concreteMat != null)
                {
                    pier.GetComponent<MeshRenderer>().sharedMaterial = concreteMat;
                }
            }
        }

        private static Mesh BuildWaterPolygonMesh(UtmCoord[] ringUtm, WorldCoordinateManager mgr, float elevationY)
        {
            int count = ringUtm.Length;
            if (count > 3 && ringUtm[0].Distance2D(ringUtm[count - 1]) < 0.05) count--;
            if (count < 3) return null;

            var verts3D = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = mgr.UtmToUnityPosition(ringUtm[i], elevationY);
                if (verts3D.Count == 0 || Vector3.SqrMagnitude(p - verts3D[verts3D.Count - 1]) > 0.25f)
                {
                    verts3D.Add(p);
                }
            }
            if (verts3D.Count < 3) return null;

            // Compute centroid fan for robust water triangulation even on large irregular lakes
            // alongside signed area orientation check.
            var poly2D = new Vector2[verts3D.Count];
            Vector3 center = Vector3.zero;
            for (int i = 0; i < verts3D.Count; i++)
            {
                center += verts3D[i];
                poly2D[i] = new Vector2(verts3D[i].x, verts3D[i].z);
            }
            center /= verts3D.Count;

            List<int> tris = TriangulateWaterEarClip(poly2D);
            if (tris.Count < 3)
            {
                // Fallback simple fan for self-intersecting OSM water ways
                verts3D.Add(center);
                int cIdx = verts3D.Count - 1;
                tris.Clear();
                for (int i = 0; i < verts3D.Count - 1; i++)
                {
                    int next = (i + 1) % (verts3D.Count - 1);
                    tris.Add(cIdx);
                    tris.Add(next);
                    tris.Add(i);
                }
            }

            var mesh = new Mesh { name = "WaterPolygon" };
            mesh.SetVertices(verts3D);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int> TriangulateWaterEarClip(Vector2[] poly)
        {
            double area = 0.0;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                area += (double)a.x * b.y - (double)b.x * a.y;
            }
            bool flipped = area < 0.0;
            if (flipped) System.Array.Reverse(poly);

            var indices = new List<int>(poly.Length);
            for (int i = 0; i < poly.Length; i++) indices.Add(i);
            var tris = new List<int>((poly.Length - 2) * 3);
            int guard = 0, idx = 0;

            while (indices.Count > 2 && guard < poly.Length * 4)
            {
                guard++;
                int n = indices.Count;
                int p = indices[(idx + n - 1) % n], c = indices[idx % n], nx = indices[(idx + 1) % n];
                Vector2 a = poly[p], b = poly[c], d = poly[nx];
                if (((b.x - a.x) * (d.y - a.y) - (b.y - a.y) * (d.x - a.x)) > 1e-4f)
                {
                    // Unity LH CW winding facing +Y
                    tris.Add(p); tris.Add(nx); tris.Add(c);
                    indices.RemoveAt(idx % n);
                    idx = 0;
                }
                else
                {
                    idx = (idx + 1) % n;
                }
            }

            return indices.Count == 2 ? tris : new List<int>();
        }

        private static Mesh BuildWaterRibbonMesh(UtmCoord[] lineUtm, WorldCoordinateManager mgr, float widthMeters, float elevY)
        {
            float hw = Mathf.Max(4f, widthMeters * 0.5f);
            var verts = new List<Vector3>(lineUtm.Length * 2);
            var tris = new List<int>((lineUtm.Length - 1) * 6);

            for (int i = 0; i < lineUtm.Length; i++)
            {
                Vector3 p = mgr.UtmToUnityPosition(lineUtm[i], elevY);
                Vector3 prev = i > 0 ? mgr.UtmToUnityPosition(lineUtm[i - 1], elevY) : p;
                Vector3 next = i < lineUtm.Length - 1 ? mgr.UtmToUnityPosition(lineUtm[i + 1], elevY) : p;
                Vector3 dir = next - prev;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
                dir.Normalize();

                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                verts.Add(p - right * hw);
                verts.Add(p + right * hw);

                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }

            var mesh = new Mesh { name = "WaterChannel" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BakeBatchedMesh(
            Transform parent, string objName, List<Vector3> verts, List<int> tris, Material mat, int layer, bool addCollider)
        {
            if (verts == null || verts.Count < 3 || tris == null || tris.Count < 3) return;

            var mesh = new Mesh
            {
                name = objName,
                indexFormat = verts.Count > 60000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            CreateColliderMeshObj(parent, objName, mesh, mat, layer, isTrigger: false, attachCollider: addCollider);
        }

        private static void CreateColliderMeshObj(
            Transform parent, string objName, Mesh mesh, Material mat, int layer, bool isTrigger, bool attachCollider = true)
        {
            var go = new GameObject(objName);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            if (attachCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = isTrigger;
                mc.isTrigger = isTrigger;
            }
        }

        private static Transform CreateCleanChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                for (int i = existing.childCount - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(existing.GetChild(i).gameObject);
                    else
#endif
                    Destroy(existing.GetChild(i).gameObject);
                }
                return existing;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Segment";
            return raw.Replace('/', '_').Replace('\\', '_').Trim();
        }
    }
}
