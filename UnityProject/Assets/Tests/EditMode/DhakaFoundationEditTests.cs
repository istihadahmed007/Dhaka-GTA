using System.IO;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DhakaGta.Tests.EditMode
{
    public sealed class DhakaFoundationEditTests
    {
        [Test]
        public void GeoProjection_OriginAndRoundTrip_AreAccurate()
        {
            Vector2 originXZ = DhakaGeoProjection.LatLonToMetersXZ(
                DhakaGeoProjection.OriginLatitude,
                DhakaGeoProjection.OriginLongitude);

            Assert.That(originXZ.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(originXZ.y, Is.EqualTo(0f).Within(1e-4f));

            var sample = new Vector2(-1172.6f, -2684.4f); // Ahsan Manzil 1:1 meters from Shapla
            (double lat, double lon) = DhakaGeoProjection.MetersXZToLatLon(sample);
            Vector2 roundTrip = DhakaGeoProjection.LatLonToMetersXZ(lat, lon);

            Assert.That(roundTrip.x, Is.EqualTo(sample.x).Within(1e-2f));
            Assert.That(roundTrip.y, Is.EqualTo(sample.y).Within(1e-2f));
            Assert.IsTrue(DhakaGeoProjection.IsInRiverZone(new Vector3(0f, 0f, -2950f)));
            Assert.IsFalse(DhakaGeoProjection.IsInRiverZone(new Vector3(0f, 0f, 35.2f)));
        }

        [Test]
        public void DistrictRegistry_ShaplaSpawn_ResolvesShaplaNotOldDhaka()
        {
            var shapla = ScriptableObject.CreateInstance<DistrictDefinitionSO>();
            shapla.districtId = "shapla_crossing";
            shapla.displayName = "Shapla Crossing";
            shapla.boundsMinXZ = new Vector2(-650f, -650f);
            shapla.boundsMaxXZ = new Vector2(650f, 650f);
            shapla.priority = 30;

            var oldDhaka = ScriptableObject.CreateInstance<DistrictDefinitionSO>();
            oldDhaka.districtId = "old_dhaka";
            oldDhaka.displayName = "Old Dhaka · Chawkbazar";
            oldDhaka.boundsMinXZ = new Vector2(-4200f, -2950f);
            oldDhaka.boundsMaxXZ = new Vector2(-650f, -750f);
            oldDhaka.priority = 15;

            var go = new GameObject("TestDistrictRegistry");
            try
            {
                var registry = go.AddComponent<DhakaDistrictRegistry>();
                registry.SetCatalog(new[] { oldDhaka, shapla }, null, null);

                DistrictDefinitionSO atSpawn = registry.ResolveDistrict(new Vector3(-2.2f, 0.1f, 35.2f));
                Assert.NotNull(atSpawn);
                Assert.AreEqual("shapla_crossing", atSpawn.districtId);

                DistrictDefinitionSO atOldDhaka = registry.ResolveDistrict(new Vector3(-1172f, 0f, -2684f));
                Assert.NotNull(atOldDhaka);
                Assert.AreEqual("old_dhaka", atOldDhaka.districtId);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(shapla);
                Object.DestroyImmediate(oldDhaka);
            }
        }

        [Test]
        public void PhysicsLayers_MatrixContract_IsolatesWheelsAndCameraOccluders()
        {
            Assert.IsTrue(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.Ground, PhysicsLayers.Player));
            Assert.IsTrue(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.Ground, PhysicsLayers.VehicleWheel));
            Assert.IsFalse(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.Vehicle, PhysicsLayers.VehicleWheel));
            Assert.IsFalse(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.Player, PhysicsLayers.VehicleWheel));

            Assert.IsTrue(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.CameraOccluder, PhysicsLayers.Ground));
            Assert.IsTrue(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.CameraOccluder, PhysicsLayers.Building));
            Assert.IsFalse(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.CameraOccluder, PhysicsLayers.Player));
            Assert.IsFalse(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.CameraOccluder, PhysicsLayers.Vehicle));
            Assert.IsFalse(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.CameraOccluder, PhysicsLayers.Npc));

            Assert.IsTrue(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.Water, PhysicsLayers.Player));
            Assert.IsTrue(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.Water, PhysicsLayers.Vehicle));
            Assert.IsTrue(PhysicsLayers.ShouldLayersCollide(PhysicsLayers.Water, PhysicsLayers.TriggerZone));
        }

        [Test]
        public void HdrpPipeline_IsLockedAsActiveRenderPipeline()
        {
            Assert.IsNotNull(GraphicsSettings.currentRenderPipeline, "Current RenderPipelineAsset must be configured.");
            Assert.IsInstanceOf<HDRenderPipelineAsset>(
                GraphicsSettings.currentRenderPipeline,
                "Dhaka-GTA is locked to Unity HDRP; active pipeline is not HDRenderPipelineAsset.");
        }

        [Test]
        public void InputActions_ContainsAllFiveConflictFreeActionMaps()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.NotNull(asset, "InputSystem_Actions.inputactions must exist at Assets/InputSystem_Actions.inputactions.");
            Assert.NotNull(asset.FindActionMap("Player", false));
            Assert.NotNull(asset.FindActionMap("Vehicle", false));
            Assert.NotNull(asset.FindActionMap("Camera", false));
            Assert.NotNull(asset.FindActionMap("UI", false));
            Assert.NotNull(asset.FindActionMap("Debug", false));
        }

        [Test]
        public void HealthComponent_HonorsProtagonistProfileStatsAbove100()
        {
            var tariq = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            tariq.protagonistId = "tariq";
            tariq.maxHealth = 135f;
            tariq.maxStamina = 115f;

            var go = new GameObject("TestProtagonistHealth");
            try
            {
                var health = go.AddComponent<HealthComponent>();
                health.InitializeFromProfile(tariq);

                Assert.AreEqual(135f, health.MaxHealth, 0.001f);
                Assert.AreEqual(135f, health.CurrentHealth, 0.001f);
                health.ApplyDamage(25f, Vector3.zero);
                Assert.AreEqual(110f, health.CurrentHealth, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(tariq);
            }
        }

        [Test]
        public void Phase1FoundationAssets_TagManagerAndScenes_AreConfigured()
        {
            foreach (var kvp in PhysicsLayers.LayerNames)
            {
                Assert.AreEqual(
                    kvp.Value,
                    LayerMask.LayerToName(kvp.Key),
                    $"Expected Physics layer {kvp.Key} to be named '{kvp.Value}'. Run Dhaka-GTA/Foundation/Setup first.");
            }

            Assert.IsTrue(File.Exists("Assets/Scenes/Bootstrap.unity"), "Missing Assets/Scenes/Bootstrap.unity");
            Assert.IsTrue(File.Exists("Assets/Scenes/MainMenu.unity"), "Missing Assets/Scenes/MainMenu.unity");
            Assert.IsTrue(File.Exists("Assets/Scenes/TestCity.unity"), "Missing Assets/Scenes/TestCity.unity");
        }
    }
}
