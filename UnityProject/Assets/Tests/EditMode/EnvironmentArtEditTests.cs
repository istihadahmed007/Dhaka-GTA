using System.IO;
using DhakaGta.Core;
using DhakaGta.Environment;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class EnvironmentArtEditTests
    {
        [Test]
        public void PbrTextureMaps_AllHdrpSetsExistOnDisk()
        {
            string texDir = Path.Combine(Application.dataPath, "Textures", "Environment");
            Assert.IsTrue(Directory.Exists(texDir), "Missing Textures/Environment folder.");

            string[] requiredPrefixes =
            {
                "GulshanCurtainGlass",
                "OldDhakaBrickPlaster",
                "MotijheelConcrete",
                "CorrugatedTin",
                "WetMonsoonAsphalt",
                "DhakaSidewalkPaver",
            };

            foreach (string prefix in requiredPrefixes)
            {
                Assert.IsTrue(File.Exists(Path.Combine(texDir, $"T_{prefix}_BaseColor.png")), $"Missing BaseColor for {prefix}");
                Assert.IsTrue(File.Exists(Path.Combine(texDir, $"T_{prefix}_Normal.png")), $"Missing Normal for {prefix}");
                Assert.IsTrue(File.Exists(Path.Combine(texDir, $"T_{prefix}_MaskMap.png")), $"Missing MaskMap for {prefix}");
            }
        }

        [Test]
        public void HeroArchitectureBuilder_ProducesMultiPartLodGroupBuildingsAndTeaStalls()
        {
            var root = new GameObject("TestEnvRoot");
            var mats = new DhakaHeroArchitectureBuilder.MaterialPack
            {
                GlassTowerMat = new Material(Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Standard")),
                MotijheelConcreteMat = new Material(Shader.Find("Standard")),
                CorrugatedTinMat = new Material(Shader.Find("Standard")),
                WoodMat = new Material(Shader.Find("Standard")),
                DarkSteelMat = new Material(Shader.Find("Standard")),
                TankBlueMat = new Material(Shader.Find("Standard")),
            };

            try
            {
                GameObject tower = DhakaHeroArchitectureBuilder.CreateArticulatedBuildingPrefab(
                    "TestGulshanTower", DistrictArchitecturalStyle.GulshanGlassHighrise,
                    22f, 18f, 44f, Vector3.zero, 0f, 1, mats, root.transform);

                LODGroup lodGroup = tower.GetComponent<LODGroup>();
                Assert.NotNull(lodGroup, "Articulated building must include LODGroup.");
                Assert.GreaterOrEqual(lodGroup.lodCount, 2, "Expected multi-tier LODGroup.");
                Assert.NotNull(tower.GetComponent<BoxCollider>(), "Expected optimized BoxCollider on hero building.");
                Assert.Greater(tower.GetComponentsInChildren<MeshRenderer>(true).Length, 8, "Facade must use multi-part articulated mullions/podium/rooftop tanks, not a flat box.");

                GameObject stall = DhakaHeroArchitectureBuilder.CreateTeaStall(
                    new Vector3(10f, 0f, 0f), 0f, "TestTongStall", mats, root.transform);
                IInteractable interactable = stall.GetComponent<InteractiveEnvironmentProp>();
                Assert.NotNull(interactable);
                Assert.AreEqual(InteractableKind.TeaStall, interactable.Kind);

                GameObject wires = OverheadWireCatenaryBuilder.ConnectPolesWithCatenaryWires(
                    new[] { Vector3.zero, new Vector3(0f, 0f, 35f), new Vector3(0f, 0f, 70f) },
                    root.transform,
                    mats.DarkSteelMat);
                Assert.NotNull(wires, "Expected overhead catenary wire mesh connecting poles.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GpuInstancedBatcher_EnablesGpuInstancingAndGroupsMatrices()
        {
            var host = new GameObject("TestBatcherHost");
            try
            {
                var batcher = host.AddComponent<GpuInstancedPropBatcher>();
                var mesh = new Mesh();
                var mat = new Material(Shader.Find("Standard"));
                mat.enableInstancing = false;

                batcher.RegisterInstance("Trees", mesh, mat, Matrix4x4.identity);
                batcher.RegisterInstance("Trees", mesh, mat, Matrix4x4.Translate(new Vector3(15f, 0f, 0f)));

                Assert.IsTrue(mat.enableInstancing, "GpuInstancedPropBatcher must enforce enableInstancing on shared material.");
                Assert.AreEqual(1, batcher.Layers.Count);
                Assert.AreEqual(2, batcher.Layers[0].matrices.Count);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(mat);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
