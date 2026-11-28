using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Procedural Web/PCM-buffer AudioSource controller generating dynamic RPM-modulated engine harmonics,
    /// tire drift/skid hiss, horn tone, and impact crash audio without external .wav dependency blocks.
    /// </summary>
    public sealed class DhakaVehicleAudioController : MonoBehaviour
    {
        [SerializeField] private VehicleSpecSO spec;
        private AudioSource _engineSource;
        private AudioSource _skidSource;
        private AudioSource _fxSource;
        private AudioClip _hornClip;
        private AudioClip _crashClip;
        private bool _initialized;

        public float NormalizedRpm { get; private set; }

        public void Initialize(VehicleSpecSO vehicleSpec)
        {
            spec = vehicleSpec;
            EnsureAudioSources();
        }

        public void PlayHorn()
        {
            EnsureAudioSources();
            if (_fxSource != null && _hornClip != null)
            {
                _fxSource.PlayOneShot(_hornClip, 0.65f);
            }
        }

        public void PlayCollisionCrash(float impactIntensity)
        {
            EnsureAudioSources();
            if (_fxSource != null && _crashClip != null)
            {
                _fxSource.PlayOneShot(_crashClip, Mathf.Clamp(impactIntensity * 0.1f, 0.2f, 0.9f));
            }
        }

        public void UpdateEngineAndSkidAudio(float speedKph, float throttle, bool isSkidding, bool isWrecked)
        {
            EnsureAudioSources();
            float maxSpeed = spec != null ? Mathf.Max(20f, spec.maxSpeedKph) : 110f;
            float speedRatio = Mathf.Clamp01(Mathf.Abs(speedKph) / maxSpeed);
            float gearMod = (speedRatio * 3.8f) % 1.0f;
            float targetRpm = isWrecked ? 0f : Mathf.Clamp01(0.18f + speedRatio * 0.45f + gearMod * 0.25f + Mathf.Abs(throttle) * 0.22f);
            NormalizedRpm = Mathf.Lerp(NormalizedRpm, targetRpm, Time.deltaTime * 8f);

            if (_engineSource != null)
            {
                float minFreq = spec != null ? spec.idleEngineFreqHz : 55f;
                float maxFreq = spec != null ? spec.maxEngineFreqHz : 200f;
                _engineSource.pitch = Mathf.Lerp(0.75f, maxFreq / Mathf.Max(20f, minFreq) * 0.7f, NormalizedRpm);
                _engineSource.volume = isWrecked ? 0f : Mathf.Lerp(0.10f, 0.36f, NormalizedRpm);
            }

            if (_skidSource != null)
            {
                float targetSkidVol = isSkidding ? 0.35f : 0f;
                _skidSource.volume = Mathf.MoveTowards(_skidSource.volume, targetSkidVol, Time.deltaTime * 3.5f);
            }
        }

        private void EnsureAudioSources()
        {
            if (_initialized) return;
            _initialized = true;

            float idleFreq = spec != null ? spec.idleEngineFreqHz : 55f;
            float hornFreq = spec != null ? spec.hornFreqHz : 410f;

            _engineSource = CreateLoopSource("Audio_Engine", BuildEngineWaveClip(idleFreq), 0.12f);
            _skidSource = CreateLoopSource("Audio_Skid", BuildNoiseClip("SkidNoise", 0.5f), 0f);
            _fxSource = CreateOneShotSource("Audio_FX");

            _hornClip = BuildToneClip("HornTone", hornFreq, 0.28f);
            _crashClip = BuildNoiseClip("CrashImpact", 0.22f);
        }

        private AudioSource CreateLoopSource(string childName, AudioClip clip, float initVol)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 1f;
            src.minDistance = 4f;
            src.maxDistance = 65f;
            src.volume = initVol;
            if (clip != null) src.Play();
            return src;
        }

        private AudioSource CreateOneShotSource(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.minDistance = 5f;
            src.maxDistance = 75f;
            return src;
        }

        private static AudioClip BuildEngineWaveClip(float baseHz)
        {
            const int sr = 22050;
            int len = sr;
            var samples = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sr;
                samples[i] = Mathf.Sin(t * baseHz * Mathf.PI * 2f) * 0.55f
                           + Mathf.Sin(t * baseHz * 2f * Mathf.PI * 2f) * 0.28f
                           + Mathf.Sin(t * baseHz * 3f * Mathf.PI * 2f) * 0.12f;
            }
            var clip = AudioClip.Create("ProcEngine", len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildToneClip(string name, float hz, float seconds)
        {
            const int sr = 22050;
            int len = Mathf.Max(128, Mathf.RoundToInt(sr * seconds));
            var samples = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sr;
                float env = 1f - (float)i / len;
                samples[i] = (Mathf.Sin(t * hz * Mathf.PI * 2f) * 0.6f + Mathf.Sin(t * hz * 1.25f * Mathf.PI * 2f) * 0.4f) * env;
            }
            var clip = AudioClip.Create(name, len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildNoiseClip(string name, float seconds)
        {
            const int sr = 22050;
            int len = Mathf.Max(128, Mathf.RoundToInt(sr * seconds));
            var samples = new float[len];
            var rng = new System.Random(42);
            for (int i = 0; i < len; i++)
            {
                float env = 1f - (float)i / len * 0.3f;
                samples[i] = ((float)rng.NextDouble() * 2f - 1f) * 0.5f * env;
            }
            var clip = AudioClip.Create(name, len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
