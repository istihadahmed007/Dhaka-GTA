using System;
using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.GIS;
using UnityEngine;

namespace DhakaGta.WorldGen
{
    /// <summary>
    /// Places geographically accurate Dhaka buildings extruded directly from OSM building footprint polygons,
    /// enforcing road-clearance checks so no building blocks drivable streets and varying architectural materials.
    /// </summary>
    public sealed class BuildingPlacementSystem : MonoBehaviour
    {
        [SerializeField] private float minimumRoadClearanceMeters = 1.5f;
        [SerializeField] private int maxBuildingsToGenerate = 4500;

        public sealed class BuildingMaterialPalette
        {
            public Material CommercialGlassMaterial;
            public Material ModernConcreteMaterial;
            public Material DhakaBrickMaterial;
            public Material CivicPlasterMaterial;
        }

        public struct BuildingGenStats
        {
            public int PlacedCount;
            public int SkippedRoadOverlap;
            public int SkippedInvalidGeometry;
        }

        public BuildingGenStats PlaceBuildings(
            GeoFeatureDatabase db,
            WorldCoordinateManager coordManager,
            RoadSplineSystem roadSystem,
            Transform parentRoot,
            BuildingMaterialPalette materials = null,
            double maxDistanceFromOriginMeters = 0.0,
            WorldChunkManager chunkManager = null)
        {
            var stats = new BuildingGenStats();
            if (db?.buildings == null || coordManager == null || parentRoot == null)
            {
                return stats;
            }

            materials ??= new BuildingMaterialPalette();
            Transform buildingsRoot = CreateCleanChild(parentRoot, "Generated_Buildings");
            UtmCoord origin = coordManager.BaseOriginUtm;

            foreach (GeoBuildingFeature bldg in db.buildings)
            {
                if (stats.PlacedCount >= maxBuildingsToGenerate) break;
                if (bldg?.footprintUtm == null || bldg.footprintUtm.Length < 3)
                {
                    stats.SkippedInvalidGeometry++;
                    continue;
                }

                if (maxDistanceFromOriginMeters > 0.0 && bldg.centroidUtm.Distance2D(origin) > maxDistanceFromOriginMeters)
                {
                    continue;
                }

                Vector3 centroidLocal = coordManager.UtmToUnityPosition(bldg.centroidUtm, 0f);
                if (OverlapsAnyRoad(roadSystem, centroidLocal, minimumRoadClearanceMeters))
                {
                    stats.SkippedRoadOverlap++;
                    continue;
                }

                Mesh bldgMesh = BuildBuildingMesh(bldg, coordManager);
                if (bldgMesh == null)
                {
                    stats.SkippedInvalidGeometry++;
                    continue;
                }

                Transform targetParent = buildingsRoot;
                if (chunkManager != null)
                {
                    targetParent = chunkManager.GetOrCreateChunkContainer(centroidLocal, buildingsRoot);
                }

                var go = new GameObject($"Bldg_{Sanitize(bldg.osmId)}_{bldg.levels}F");
                go.layer = PhysicsLayers.Building;
                go.isStatic = true;
                go.transform.SetParent(targetParent, false);
                go.AddComponent<MeshFilter>().sharedMesh = bldgMesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = SelectArchitecturalMaterial(bldg, materials);

                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = bldgMesh;
                stats.PlacedCount++;
            }

            return stats;
        }

        private static bool OverlapsAnyRoad(RoadSplineSystem roadSystem, Vector3 buildingCentroid, float extraClearance)
        {
            if (roadSystem == null || roadSystem.Segments == null || roadSystem.Segments.Count == 0)
            {
                return false;
            }

            RoadSplineSegment closest = roadSystem.FindClosestSegment(buildingCentroid, out float distMeters, out _, out _);
            if (closest == null) return false;

            float requiredClearance = closest.widthMeters * 0.5f + extraClearance;
            return distMeters < requiredClearance;
        }

        private static Material SelectArchitecturalMaterial(GeoBuildingFeature bldg, BuildingMaterialPalette mats)
        {
            if (bldg.heightMeters >= 24f || bldg.buildingType == "commercial" || bldg.buildingType == "office")
            {
                return mats.CommercialGlassMaterial ?? mats.ModernConcreteMaterial;
            }
            if (bldg.buildingType == "mosque" || bldg.buildingType == "civic" || bldg.buildingType == "public")
            {
                return mats.CivicPlasterMaterial ?? mats.ModernConcreteMaterial;
            }

            int hash = Math.Abs((bldg.osmId ?? string.Empty).GetHashCode()) % 3;
            return hash switch
            {
                0 => mats.ModernConcreteMaterial,
                1 => mats.DhakaBrickMaterial ?? mats.ModernConcreteMaterial,
                _ => mats.CivicPlasterMaterial ?? mats.ModernConcreteMaterial,
            };
        }

        private static Mesh BuildBuildingMesh(GeoBuildingFeature bldg, WorldCoordinateManager mgr)
        {
            UtmCoord[] ring = bldg.footprintUtm;
            int rawCount = ring.Length;
            if (rawCount > 3 && ring[0].Distance2D(ring[rawCount - 1]) < 0.05) rawCount--;

            var clean = new List<Vector3>(rawCount);
            for (int i = 0; i < rawCount; i++)
            {
                Vector3 pos = mgr.UtmToUnityPosition(ring[i], 0f);
                if (clean.Count == 0 || Vector3.SqrMagnitude(pos - clean[clean.Count - 1]) > 0.09f)
                {
                    clean.Add(pos);
                }
            }
            if (clean.Count > 2 && Vector3.SqrMagnitude(clean[0] - clean[clean.Count - 1]) <= 0.09f)
            {
                clean.RemoveAt(clean.Count - 1);
            }
            if (clean.Count < 3) return null;

            var poly2D = new Vector2[clean.Count];
            for (int i = 0; i < clean.Count; i++) poly2D[i] = new Vector2(clean[i].x, clean[i].z);

            if (SignedArea(poly2D) < 0f)
            {
                Array.Reverse(poly2D);
                clean.Reverse();
            }

            List<int> roofTris = TriangulateEarClip(poly2D);
            if (roofTris.Count < 3) return null;

            float height = Mathf.Clamp(bldg.heightMeters, 3.5f, 180f);
            int n = clean.Count;
            var verts = new List<Vector3>(n * 5);
            var uvs = new List<Vector2>(n * 5);
            var tris = new List<int>(roofTris.Count + n * 6);

            for (int i = 0; i < n; i++)
            {
                verts.Add(clean[i] + Vector3.up * height);
                uvs.Add(new Vector2(clean[i].x * 0.12f, clean[i].z * 0.12f));
            }
            for (int i = 0; i < roofTris.Count; i += 3)
            {
                tris.Add(roofTris[i]);
                tris.Add(roofTris[i + 2]);
                tris.Add(roofTris[i + 1]);
            }

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                int vStart = verts.Count;
                Vector3 b0 = clean[i], b1 = clean[next];
                Vector3 t0 = b0 + Vector3.up * height, t1 = b1 + Vector3.up * height;
                float len = Vector3.Distance(b0, b1) * 0.15f;
                float vh = height * 0.15f;

                verts.Add(b0); verts.Add(b1); verts.Add(t1); verts.Add(t0);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(len, 0f));
                uvs.Add(new Vector2(len, vh)); uvs.Add(new Vector2(0f, vh));

                tris.Add(vStart); tris.Add(vStart + 2); tris.Add(vStart + 1);
                tris.Add(vStart); tris.Add(vStart + 3); tris.Add(vStart + 2);
            }

            var mesh = new Mesh { name = $"BldgMesh_{Sanitize(bldg.osmId)}" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float SignedArea(Vector2[] poly)
        {
            double a = 0.0;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Length];
                a += (double)p.x * q.y - (double)q.x * p.y;
            }
            return (float)(a * 0.5);
        }

        private static List<int> TriangulateEarClip(Vector2[] poly)
        {
            var remaining = new List<int>(poly.Length);
            for (int i = 0; i < poly.Length; i++) remaining.Add(i);

            var tris = new List<int>((poly.Length - 2) * 3);
            int guard = 0, idx = 0;
            while (remaining.Count > 2 && guard < poly.Length * poly.Length)
            {
                guard++;
                int n = remaining.Count;
                int prev = remaining[(idx + n - 1) % n];
                int curr = remaining[idx % n];
                int next = remaining[(idx + 1) % n];

                if (IsEar(poly, remaining, prev, curr, next))
                {
                    tris.Add(prev); tris.Add(curr); tris.Add(next);
                    remaining.RemoveAt(idx % n);
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
            if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-5f) return false;

            foreach (int pIdx in remaining)
            {
                if (pIdx == prev || pIdx == curr || pIdx == next) continue;
                Vector2 p = poly[pIdx];
                if (((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x)) >= 0f &&
                    ((c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x)) >= 0f &&
                    ((a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x)) >= 0f)
                {
                    return false;
                }
            }
            return true;
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
            if (string.IsNullOrWhiteSpace(raw)) return "Bldg";
            return raw.Replace('/', '_').Replace('\\', '_').Trim();
        }
    }
}
