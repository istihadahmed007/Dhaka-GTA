using System.Collections.Generic;
using DhakaGta.WorldGen;
using UnityEngine;

namespace DhakaGta.Environment
{
    /// <summary>
    /// Populates the Gulshan-Hatirjheel district with articulated hero skyscrapers/commercial blocks,
    /// interactive Dhaka Tong tea stalls, promenade benches, billboards, roadside utility poles with sagging overhead wires,
    /// and GPU-instanced Krishnachura/tropical trees along real OSM street splines.
    /// </summary>
    public sealed class GulshanHatirjheelEnvironmentPopulator : MonoBehaviour
    {
        public struct EnvironmentPopulateStats
        {
            public int HeroTowersPlaced;
            public int TeaStallsPlaced;
            public int BenchesPlaced;
            public int BillboardsPlaced;
            public int ElectricPolesPlaced;
            public int TreesInstanced;
        }

        public static EnvironmentPopulateStats PopulateGulshanHatirjheel(
            RoadSplineSystem splineSystem,
            IntersectionGenerator intersectionGen,
            GpuInstancedPropBatcher gpuBatcher,
            DhakaHeroArchitectureBuilder.MaterialPack mats,
            Transform environmentRoot,
            Vector3 heroZoneCenter,
            Vector3 playerSpawnPos)
        {
            var stats = new EnvironmentPopulateStats();
            if (splineSystem == null || environmentRoot == null || mats == null)
            {
                return stats;
            }

            Transform towersRoot = GetOrCreateChild(environmentRoot, "Hero_GulshanBanani_Towers");
            Transform propsRoot = GetOrCreateChild(environmentRoot, "Props_TeaStalls_Benches_Billboards");
            Transform polesRoot = GetOrCreateChild(environmentRoot, "Infrastructure_ElectricPoles_Wires");

            // 1. Place Signature Interactive Tea Stall & Hatirjheel Bench immediately reachable from player spawn!
            DhakaHeroArchitectureBuilder.CreateTeaStall(
                playerSpawnPos + new Vector3(4.5f, 0.16f, 2.5f),
                -35f,
                "Gulshan_Mama_Tea_Stall",
                mats,
                propsRoot);
            stats.TeaStallsPlaced++;

            DhakaHeroArchitectureBuilder.CreatePromenadeBench(
                playerSpawnPos + new Vector3(-4.2f, 0.16f, 4.0f),
                20f,
                "Hatirjheel_Promenade_Bench_1",
                mats,
                propsRoot);
            stats.BenchesPlaced++;

            // 2. Shared GPU-instanced tropical tree meshes
            (Mesh trunkMesh, Mesh canopyMesh) = DhakaHeroArchitectureBuilder.BuildSharedTropicalTreeMeshes();

            var poleBasePositions = new List<Vector3>();
            int roadCounter = 0;

            foreach (RoadSplineSegment seg in splineSystem.Segments)
            {
                if (seg == null || seg.gradeKind != RoadGradeKind.Surface || seg.totalLengthMeters < 35f) continue;
                float distToHero = Vector3.Distance(seg.StartPosition, heroZoneCenter);
                if (distToHero > 1400f) continue;

                roadCounter++;
                bool isMajorBoulevard = seg.highwayClass == "primary" || seg.highwayClass == "trunk" || seg.highwayClass == "secondary";
                float roadsideOffset = seg.widthMeters * 0.5f + 2.4f;

                // Electric poles & catenary wires along primary/secondary boulevards
                if (isMajorBoulevard)
                {
                    var localPoles = new List<Vector3>();
                    for (float d = 12f; d < seg.totalLengthMeters - 10f; d += 44f)
                    {
                        seg.SampleAtDistance(d, out Vector3 p, out Vector3 tan, out Vector3 right);
                        Vector3 polePos = p + right * (roadsideOffset + 0.6f);
                        float yaw = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg;
                        DhakaHeroArchitectureBuilder.CreateElectricPole(polePos, yaw, mats, polesRoot);
                        localPoles.Add(polePos);
                        poleBasePositions.Add(polePos);
                        stats.ElectricPolesPlaced++;
                    }

                    OverheadWireCatenaryBuilder.ConnectPolesWithCatenaryWires(
                        localPoles,
                        polesRoot,
                        mats.DarkSteelMat,
                        wireAttachHeightMeters: 7.9f,
                        maxSpanMeters: 56f);
                }

                // Tropical Krishnachura / Coconut Palm trees batched cleanly on GPU
                float treeSpacing = isMajorBoulevard ? 26f : 42f;
                for (float d = 18f; d < seg.totalLengthMeters - 12f; d += treeSpacing)
                {
                    seg.SampleAtDistance(d, out Vector3 p, out _, out Vector3 right);
                    float side = ((int)d % 2 == 0) ? 1f : -1f;
                    Vector3 treePos = p + right * (side * (roadsideOffset + 1.2f));
                    if (gpuBatcher != null)
                    {
                        float scaleVar = 0.85f + ((roadCounter + (int)d) % 5) * 0.12f;
                        Matrix4x4 trunkM = Matrix4x4.TRS(
                            treePos + Vector3.up * (2.1f * scaleVar),
                            Quaternion.identity,
                            new Vector3(0.45f * scaleVar, 2.1f * scaleVar, 0.45f * scaleVar));
                        Matrix4x4 canopyM = Matrix4x4.TRS(
                            treePos + Vector3.up * (5.1f * scaleVar),
                            Quaternion.identity,
                            new Vector3(5.2f * scaleVar, 3.2f * scaleVar, 5.2f * scaleVar));

                        gpuBatcher.RegisterInstance("TropicalTree_Trunks", trunkMesh, mats.TrunkBarkMat, trunkM);
                        Material canopyMat = ((roadCounter + (int)d) % 4 == 0 && mats.BlossomRedMat != null)
                            ? mats.BlossomRedMat
                            : mats.FoliageGreenMat;
                        gpuBatcher.RegisterInstance("TropicalTree_Canopies", canopyMesh, canopyMat, canopyM);
                        stats.TreesInstanced++;
                    }
                }

                // Articulated hero buildings flanking major avenues (set back behind sidewalks, non-uniform seeds)
                if (isMajorBoulevard && roadCounter % 2 == 0)
                {
                    float lotStep = 65f;
                    for (float d = 30f; d < seg.totalLengthMeters - 30f && stats.HeroTowersPlaced < 42; d += lotStep)
                    {
                        seg.SampleAtDistance(d, out Vector3 p, out Vector3 tan, out Vector3 right);
                        float side = (stats.HeroTowersPlaced % 2 == 0) ? 1f : -1f;
                        Vector3 lotPos = p + right * (side * (roadsideOffset + 19f));

                        // Guard against stepping inside an adjacent road
                        RoadSplineSegment clash = splineSystem.FindClosestSegment(lotPos, out float clearance, out _, out _);
                        if (clash != null && clearance < 14f) continue;

                        int seed = roadCounter * 31 + stats.HeroTowersPlaced;
                        DistrictArchitecturalStyle style = (seed % 3 == 0)
                            ? DistrictArchitecturalStyle.BananiCommercialBoutique
                            : DistrictArchitecturalStyle.GulshanGlassHighrise;

                        float w = 18f + (seed % 4) * 3f;
                        float dep = 16f + ((seed + 2) % 3) * 3f;
                        float h = style == DistrictArchitecturalStyle.GulshanGlassHighrise
                            ? 36f + (seed % 5) * 8f
                            : 20f + (seed % 4) * 4f;
                        float yaw = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg;

                        DhakaHeroArchitectureBuilder.CreateArticulatedBuildingPrefab(
                            $"HeroTower_{style}_{stats.HeroTowersPlaced + 1}",
                            style,
                            w, dep, h,
                            lotPos,
                            yaw,
                            seed,
                            mats,
                            towersRoot);
                        stats.HeroTowersPlaced++;
                    }
                }

                // Distributed corner Tea Stalls & Lakeside Benches along secondary avenues
                if (roadCounter % 4 == 0 && stats.TeaStallsPlaced < 12)
                {
                    seg.SampleAtDistance(seg.totalLengthMeters * 0.5f, out Vector3 p, out Vector3 tan, out Vector3 right);
                    float yaw = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg;
                    DhakaHeroArchitectureBuilder.CreateTeaStall(
                        p + right * (roadsideOffset + 1.5f) + Vector3.up * 0.16f,
                        yaw + 90f,
                        $"Dhaka_Tong_TeaStall_{stats.TeaStallsPlaced + 1}",
                        mats,
                        propsRoot);
                    stats.TeaStallsPlaced++;
                }
            }

            // 3. Place Billboards beside major signalized intersections
            if (intersectionGen?.Junctions != null)
            {
                int bIdx = 0;
                foreach (RoadJunctionNode j in intersectionGen.Junctions)
                {
                    if (!j.hasTrafficSignal || Vector3.Distance(j.centerPosition, heroZoneCenter) > 1300f) continue;
                    bIdx++;
                    if (bIdx > 12) break;

                    Vector3 bbPos = j.centerPosition + new Vector3(j.radiusMeters + 6.5f, 0f, j.radiusMeters + 6.5f);
                    DhakaHeroArchitectureBuilder.CreateDhakaBillboard(
                        bbPos,
                        bIdx * 37f,
                        $"Gulshan_Billboard_{bIdx}",
                        mats,
                        propsRoot);
                    stats.BillboardsPlaced++;
                }
            }

            return stats;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                for (int i = child.childCount - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(child.GetChild(i).gameObject);
                    else
#endif
                    Destroy(child.GetChild(i).gameObject);
                }
                return child;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
