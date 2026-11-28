using UnityEngine;
using UnityEngine.Audio;

namespace DhakaGta.Audio
{
    /// <summary>
    /// AudioMixer bus registry replacing legacy Three.js WebAudio oscillator synths.
    /// </summary>
    public sealed class DhakaAudioRouter : MonoBehaviour
    {
        [SerializeField] private AudioMixer masterMixer;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;
        [SerializeField] private AudioMixerGroup musicGroup;

        public AudioMixer MasterMixer => masterMixer;
        public AudioMixerGroup SfxGroup => sfxGroup;
        public AudioMixerGroup AmbienceGroup => ambienceGroup;
        public AudioMixerGroup MusicGroup => musicGroup;
    }
}
