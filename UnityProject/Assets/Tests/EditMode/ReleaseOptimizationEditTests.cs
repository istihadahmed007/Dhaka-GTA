using DhakaGta.Core;
using DhakaGta.Graphics;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class ReleaseOptimizationEditTests
    {
        [Test]
        public void GraphicsQualityManager_AppliesAllThreeConfigurablePcPresets()
        {
            var go = new GameObject("QualityPresetTest");
            var mgr = go.AddComponent<DhakaGraphicsQualityManager>();

            try
            {
                mgr.ApplyQualityPreset(HdrpQualityTier.Performant);
                Assert.AreEqual(HdrpQualityTier.Performant, mgr.CurrentTier);
                Assert.IsTrue(QualitySettings.streamingMipmapsActive);
                Assert.AreEqual(256f, QualitySettings.streamingMipmapsMemoryBudget, 0.5f);
                Assert.AreEqual(0.70f, QualitySettings.lodBias, 0.01f);
                Assert.AreEqual(40f, QualitySettings.shadowDistance, 0.1f);
                Assert.IsFalse(mgr.ActiveConfig.volumetricCloudsEnabled);

                mgr.ApplyQualityPreset(HdrpQualityTier.HighFidelity);
                Assert.AreEqual(HdrpQualityTier.HighFidelity, mgr.CurrentTier);
                Assert.AreEqual(512f, QualitySettings.streamingMipmapsMemoryBudget, 0.5f);
                Assert.AreEqual(1.25f, QualitySettings.lodBias, 0.01f);
                Assert.AreEqual(120f, QualitySettings.shadowDistance, 0.1f);
                Assert.IsTrue(mgr.ActiveConfig.screenSpaceReflectionsEnabled);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PerformanceProfiler_SamplesValidFrameAndMemoryTelemetry()
        {
            var go = new GameObject("ProfilerUnitTest");
            var profiler = go.AddComponent<DhakaPerformanceProfiler>();

            try
            {
                profiler.StartRecorders();
                PerformanceTelemetrySample sample = profiler.SampleNow(0.0167f);
                for (int i = 0; i < 10; i++)
                {
                    sample = profiler.SampleNow(0.0167f);
                }

                Assert.Greater(sample.RollingAvgFps, 50f);
                Assert.Greater(sample.SystemMemoryMb, 0f);
                Assert.IsFalse(float.IsNaN(sample.MainThreadMs));

                string json = profiler.ExportJsonReport();
                Assert.IsTrue(json.Contains("RollingAvgFps"));
                Assert.IsTrue(json.Contains("SystemMemoryMb"));
            }
            finally
            {
                profiler.StopRecorders();
                Object.DestroyImmediate(go);
            }
        }
    }
}
