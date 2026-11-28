using System.IO;
using DhakaGta.Core;
using DhakaGta.Graphics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DhakaGta.Tests.EditMode
{
    public sealed class GraphicsSystemEditTests
    {
        [Test]
        public void WetSurfaceHlslInclude_ExistsAndDeclaresApplyDhakaWetPuddles()
        {
            string hlslPath = Path.Combine(Application.dataPath, "Shaders", "DhakaWetSurface.hlsl");
            Assert.IsTrue(File.Exists(hlslPath), "Expected HLSL wetness include at: " + hlslPath);
            string contents = File.ReadAllText(hlslPath);
            Assert.IsTrue(contents.Contains("ApplyDhakaWetPuddles_float"), "Shader Graph custom function symbol must exist.");
        }

        [Test]
        public void TimeOfDayAndWeather_SolarArcAndHdrpOverrides_UpdateAcrossDayNightAndMonsoon()
        {
            DhakaTimeOfDayAndWeatherController.ComputeSolarTrajectory(12.2f, out float noonElev, out _, out float noonDay);
            Assert.Greater(noonElev, 65f);
            Assert.Greater(noonDay, 0.95f);

            DhakaTimeOfDayAndWeatherController.ComputeSolarTrajectory(22.0f, out float nightElev, out _, out float nightDay);
            Assert.Less(nightElev, 0f);
            Assert.AreEqual(0f, nightDay, 0.001f);

            var host = new GameObject("TestGraphicsHost");
            var sunGo = new GameObject("TestSun");
            var volGo = new GameObject("TestVolume");
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                var volume = volGo.AddComponent<Volume>();
                volume.profile = profile;

                var wet = host.AddComponent<WetSurfacePuddleDriver>();
                var asphaltMat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
                asphaltMat.SetColor("_BaseColor", new Color(0.20f, 0.20f, 0.22f));
                asphaltMat.SetFloat("_Smoothness", 0.25f);
                wet.RegisterPavementMaterial(asphaltMat);

                var ctrl = host.AddComponent<DhakaTimeOfDayAndWeatherController>();
                ctrl.Configure(sun, volume, wet, null, null, DhakaWeatherCondition.MonsoonDownpour);

                Assert.Greater(wet.CurrentWetness, 0.8f);
                Assert.IsTrue(ctrl.AreNightLightsActive);
                Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds) && clouds.enable.value);
                Assert.IsTrue(profile.TryGet(out ScreenSpaceReflection ssr) && ssr.enabled.value);
                Assert.IsTrue(profile.TryGet(out Tonemapping tone) && tone.mode.value == TonemappingMode.ACES);

                // Quality scaler drops expensive clouds & SSR on Performant tier
                var qualityMgr = host.AddComponent<DhakaGraphicsQualityManager>();
                qualityMgr.Configure(ctrl, null, HdrpQualityTier.Performant);
                Assert.IsFalse(clouds.enable.value);
                Assert.IsFalse(ssr.enabled.value);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(sunGo);
                Object.DestroyImmediate(volGo);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
