using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace DhakaGta.Core
{
    [Serializable]
    public struct PerformanceTelemetrySample
    {
        public float InstantFps;
        public float RollingAvgFps;
        public float MinObservedFps;
        public float MainThreadMs;
        public float SystemMemoryMb;
        public float GcMemoryMb;
        public long DrawCalls;
        public long SetPassCalls;
        public long TriangleCount;
        public int Gen0Collections;
        public int SampleFrames;
    }

    /// <summary>
    /// Built-in Unity.Profiling.ProfilerRecorder harness monitoring CPU frame time, draw calls,
    /// triangles, SetPass calls, and native/GC memory across long gameplay sessions without external tools.
    /// </summary>
    public sealed class DhakaPerformanceProfiler : MonoBehaviour
    {
        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _sysMemRecorder;
        private ProfilerRecorder _gcMemRecorder;
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _trisRecorder;

        private bool _recordersActive;
        private int _sampleFrames;
        private float _accumFps;
        private float _minFps = float.MaxValue;
        private int _baselineGen0;

        public PerformanceTelemetrySample LatestSample { get; private set; }

        private void OnEnable() => StartRecorders();
        private void OnDisable() => StopRecorders();

        public void StartRecorders()
        {
            if (_recordersActive) return;

            _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            _sysMemRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _gcMemRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _trisRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");

            _baselineGen0 = GC.CollectionCount(0);
            _sampleFrames = 0;
            _accumFps = 0f;
            _minFps = float.MaxValue;
            _recordersActive = true;
        }

        public void StopRecorders()
        {
            if (!_recordersActive) return;
            _mainThreadRecorder.Dispose();
            _sysMemRecorder.Dispose();
            _gcMemRecorder.Dispose();
            _drawCallsRecorder.Dispose();
            _setPassRecorder.Dispose();
            _trisRecorder.Dispose();
            _recordersActive = false;
        }

        private void Update()
        {
            SampleNow(Time.unscaledDeltaTime);
        }

        public PerformanceTelemetrySample SampleNow(float unscaledDt)
        {
            if (!_recordersActive) StartRecorders();

            float dt = Mathf.Max(0.0005f, unscaledDt);
            float fps = 1f / dt;
            _sampleFrames++;
            _accumFps += fps;
            if (_sampleFrames > 3 && fps < _minFps)
            {
                _minFps = fps;
            }

            float mainMs = _mainThreadRecorder.Valid && _mainThreadRecorder.LastValue > 0
                ? (float)(_mainThreadRecorder.LastValue * 1e-6)
                : dt * 1000f;

            float sysMb = _sysMemRecorder.Valid && _sysMemRecorder.LastValue > 0
                ? _sysMemRecorder.LastValue / (1024f * 1024f)
                : GC.GetTotalMemory(false) / (1024f * 1024f);

            float gcMb = _gcMemRecorder.Valid && _gcMemRecorder.LastValue > 0
                ? _gcMemRecorder.LastValue / (1024f * 1024f)
                : GC.GetTotalMemory(false) / (1024f * 1024f);

            LatestSample = new PerformanceTelemetrySample
            {
                InstantFps = fps,
                RollingAvgFps = _accumFps / _sampleFrames,
                MinObservedFps = _minFps == float.MaxValue ? fps : _minFps,
                MainThreadMs = mainMs,
                SystemMemoryMb = sysMb,
                GcMemoryMb = gcMb,
                DrawCalls = _drawCallsRecorder.Valid ? _drawCallsRecorder.LastValue : 0,
                SetPassCalls = _setPassRecorder.Valid ? _setPassRecorder.LastValue : 0,
                TriangleCount = _trisRecorder.Valid ? _trisRecorder.LastValue : 0,
                Gen0Collections = Mathf.Max(0, GC.CollectionCount(0) - _baselineGen0),
                SampleFrames = _sampleFrames
            };

            return LatestSample;
        }

        public string ExportJsonReport(string optionalOutputPath = null)
        {
            string json = JsonUtility.ToJson(LatestSample, true);
            if (!string.IsNullOrWhiteSpace(optionalOutputPath))
            {
                string dir = Path.GetDirectoryName(optionalOutputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(optionalOutputPath, json);
            }
            return json;
        }
    }
}
