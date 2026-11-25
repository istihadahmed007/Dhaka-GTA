using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Procedural Web/Desktop-safe audio synthesizer and volume controller.
    /// Generates envelope-shaped AudioClips at runtime and drives Music + SFX sources.
    /// </summary>
    public class AudioBank : MonoBehaviour
    {
        public const string MusicVolPref = "Dhaka_MusicVol";
        public const string SfxVolPref = "Dhaka_SfxVol";

        public static AudioBank Instance { get; private set; }

        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource sfxSource;

        public float MusicVolume { get; private set; } = 0.65f;
        public float SfxVolume { get; private set; } = 0.85f;
        public bool IsPlayingAudio => (sfxSource != null && sfxSource.isPlaying) || (musicSource != null && musicSource.isPlaying);

        AudioClip ambientLoopClip;
        AudioClip pickupClip;
        AudioClip dashClip;
        AudioClip jumpClip;
        AudioClip hitClip;
        AudioClip winClip;
        AudioClip loseClip;
        AudioClip uiClickClip;

        void Awake()
        {
            Instance = this;
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolPref, 0.65f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolPref, 0.85f));
            EnsureSources();
            BuildClips();
            ApplyVolumes();
            StartAmbientBed();
        }

        void EnsureSources()
        {
            if (musicSource == null)
            {
                var go = new GameObject("MusicSource");
                go.transform.SetParent(transform, false);
                musicSource = go.AddComponent<AudioSource>();
            }
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            if (sfxSource == null)
            {
                var go = new GameObject("SfxSource");
                go.transform.SetParent(transform, false);
                sfxSource = go.AddComponent<AudioSource>();
            }
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        void BuildClips()
        {
            ambientLoopClip = CreateAmbientBedClip();
            pickupClip = CreateArpeggioClip("DhakaPickup", new[] { 587.33f, 783.99f, 987.77f }, 0.24f, 0.28f);
            dashClip = CreateNoiseWhooshClip("DhakaDash", 0.20f, 0.25f);
            jumpClip = CreateSweepClip("DhakaJump", 240f, 520f, 0.16f, 0.24f);
            hitClip = CreateImpactClip("DhakaHit", 0.30f, 0.38f);
            winClip = CreateArpeggioClip("DhakaWin", new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.56f, 0.32f);
            loseClip = CreateSweepClip("DhakaLose", 310f, 110f, 0.45f, 0.30f);
            uiClickClip = CreateSweepClip("DhakaUiClick", 640f, 880f, 0.07f, 0.18f);
        }

        void StartAmbientBed()
        {
            if (musicSource != null && ambientLoopClip != null)
            {
                musicSource.clip = ambientLoopClip;
                musicSource.Play();
            }
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolPref, MusicVolume);
            ApplyVolumes();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolPref, SfxVolume);
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            if (musicSource != null) musicSource.volume = MusicVolume * 0.42f;
            if (sfxSource != null) sfxSource.volume = SfxVolume;
        }

        public void PlayPickup(int streak)
        {
            if (sfxSource == null || pickupClip == null) return;
            sfxSource.pitch = Mathf.Clamp(1f + streak * 0.05f, 1f, 1.35f);
            sfxSource.PlayOneShot(pickupClip, Mathf.Max(0.25f, SfxVolume));
        }

        public void PlayDash()
        {
            if (sfxSource == null || dashClip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(dashClip, SfxVolume);
        }

        public void PlayJump()
        {
            if (sfxSource == null || jumpClip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(jumpClip, SfxVolume);
        }

        public void PlayHit()
        {
            if (sfxSource == null || hitClip == null) return;
            sfxSource.pitch = 0.96f;
            sfxSource.PlayOneShot(hitClip, SfxVolume);
        }

        public void PlayWin()
        {
            if (sfxSource == null || winClip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(winClip, SfxVolume);
        }

        public void PlayLose()
        {
            if (sfxSource == null || loseClip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(loseClip, SfxVolume);
        }

        public void PlayUiClick()
        {
            if (sfxSource == null || uiClickClip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(uiClickClip, SfxVolume * 0.8f);
        }

        static AudioClip CreateAmbientBedClip()
        {
            const int sampleRate = 22050;
            const float duration = 4.0f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];
            float[] notes = { 146.83f, 220.00f, 293.66f, 349.23f }; // D3, A3, D4, F4 warm evening pad
            uint rng = 0x51A7F9u;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float u = (float)i / samples;
                float pad = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    // Integer cycle multiplier ensures seamless looping across 4 seconds.
                    float cycles = Mathf.Round(notes[n] * duration);
                    pad += Mathf.Sin(2f * Mathf.PI * cycles * u) * (n == 0 ? 0.42f : 0.22f);
                }
                float swell = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * u);
                rng = rng * 1664525u + 1013904223u;
                float rain = (((rng >> 16) & 0xFFFF) / 32768f - 1f) * 0.045f;
                data[i] = (pad * 0.14f * swell) + rain;
            }
            ApplyEdgeFade(data, sampleRate, 0.01f);
            var clip = AudioClip.Create("DhakaAmbientBed", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateArpeggioClip(string name, float[] freqs, float duration, float gain)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];
            float step = duration / freqs.Length;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                int idx = Mathf.Min(freqs.Length - 1, Mathf.FloorToInt(t / step));
                float localT = t - idx * step;
                float f = freqs[idx];
                float env = Mathf.Exp(-localT * 9f);
                float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * t);
                data[i] = tone * env * gain;
            }
            ApplyEdgeFade(data, sampleRate, 0.005f);
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateSweepClip(string name, float startFreq, float endFreq, float duration, float gain)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float u = (float)i / samples;
                float f = Mathf.Lerp(startFreq, endFreq, u);
                phase += (2f * Mathf.PI * f) / sampleRate;
                float env = Mathf.Pow(1f - u, 1.3f);
                data[i] = Mathf.Sin(phase) * env * gain;
            }
            ApplyEdgeFade(data, sampleRate, 0.005f);
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateNoiseWhooshClip(string name, float duration, float gain)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];
            uint rng = 0x9E3779B9u;
            float lp = 0f;

            for (int i = 0; i < samples; i++)
            {
                float u = (float)i / samples;
                rng = rng * 1664525u + 1013904223u;
                float white = (((rng >> 16) & 0xFFFF) / 32768f) - 1f;
                lp = Mathf.Lerp(lp, white, 0.25f);
                float env = Mathf.Sin(u * Mathf.PI);
                data[i] = lp * env * gain;
            }
            ApplyEdgeFade(data, sampleRate, 0.005f);
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateImpactClip(string name, float duration, float gain)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];
            uint rng = 0xA3C59AC3u;
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float u = (float)i / samples;
                float f = Mathf.Lerp(175f, 52f, u);
                phase += (2f * Mathf.PI * f) / sampleRate;
                rng = rng * 1664525u + 1013904223u;
                float crunch = ((((rng >> 16) & 0xFFFF) / 32768f) - 1f) * 0.32f * Mathf.Exp(-t * 16f);
                float thud = Mathf.Sin(phase) * Mathf.Exp(-t * 8f);
                data[i] = (thud + crunch) * gain;
            }
            ApplyEdgeFade(data, sampleRate, 0.005f);
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void ApplyEdgeFade(float[] data, int sampleRate, float fadeSeconds)
        {
            int fadeSamples = Mathf.Min(data.Length / 2, Mathf.Max(1, Mathf.RoundToInt(sampleRate * fadeSeconds)));
            for (int i = 0; i < fadeSamples; i++)
            {
                float w = (float)i / fadeSamples;
                data[i] *= w;
                data[data.Length - 1 - i] *= w;
            }
        }
    }
}
