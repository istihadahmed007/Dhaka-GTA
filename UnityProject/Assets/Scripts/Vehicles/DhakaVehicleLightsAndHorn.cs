using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Controls functional HDRP vehicle headlights, brake lights, reverse backup lights,
    /// alternating Red/Blue DMP Police sirens, and acoustic horn signals.
    /// </summary>
    public sealed class DhakaVehicleLightsAndHorn : MonoBehaviour
    {
        [SerializeField] private Light[] headlights = System.Array.Empty<Light>();
        [SerializeField] private Renderer[] brakeLightRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] reverseLightRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Light policeRedBeacon;
        [SerializeField] private Light policeBlueBeacon;

        private bool _headlightsUserEnabled;
        private bool _policeSirenActive;
        private float _sirenTimer;

        public bool AreHeadlightsOn { get; private set; }
        public bool AreBrakeLightsOn { get; private set; }
        public bool AreReverseLightsOn { get; private set; }
        public bool IsPoliceSirenActive => _policeSirenActive;
        public int HornHonkCount { get; private set; }

        public event System.Action OnHornTriggered;

        public void Configure(
            Light[] frontLights,
            Renderer[] rearBrakeRenderers,
            Renderer[] rearReverseRenderers = null,
            Light policeRed = null,
            Light policeBlue = null)
        {
            headlights = frontLights ?? System.Array.Empty<Light>();
            brakeLightRenderers = rearBrakeRenderers ?? System.Array.Empty<Renderer>();
            reverseLightRenderers = rearReverseRenderers ?? System.Array.Empty<Renderer>();
            policeRedBeacon = policeRed;
            policeBlueBeacon = policeBlue;
        }

        public void ToggleHeadlights()
        {
            _headlightsUserEnabled = !_headlightsUserEnabled;
            SyncHeadlights();
        }

        public void TogglePoliceSiren()
        {
            if (policeRedBeacon != null || policeBlueBeacon != null)
            {
                _policeSirenActive = !_policeSirenActive;
                if (!_policeSirenActive)
                {
                    if (policeRedBeacon != null) policeRedBeacon.enabled = false;
                    if (policeBlueBeacon != null) policeBlueBeacon.enabled = false;
                }
            }
        }

        public void SoundHorn()
        {
            HornHonkCount++;
            OnHornTriggered?.Invoke();
        }

        public void UpdateLightStates(bool braking, bool reversing)
        {
            SyncHeadlights();
            AreBrakeLightsOn = braking;
            AreReverseLightsOn = reversing;

            SetEmissiveState(brakeLightRenderers, braking, new Color(3.5f, 0.15f, 0.12f), new Color(0.25f, 0.02f, 0.02f));
            SetEmissiveState(reverseLightRenderers, reversing, new Color(2.8f, 2.8f, 2.8f), new Color(0.15f, 0.15f, 0.15f));

            if (_policeSirenActive && (policeRedBeacon != null || policeBlueBeacon != null))
            {
                _sirenTimer += Time.deltaTime * 8f;
                bool phase = Mathf.FloorToInt(_sirenTimer) % 2 == 0;
                if (policeRedBeacon != null) policeRedBeacon.enabled = phase;
                if (policeBlueBeacon != null) policeBlueBeacon.enabled = !phase;
            }
        }

        private void SyncHeadlights()
        {
            bool shouldBeOn = _headlightsUserEnabled || WorldSurfaceState.IsNightOrLowVisibility;
            AreHeadlightsOn = shouldBeOn;
            foreach (Light l in headlights)
            {
                if (l != null) l.enabled = shouldBeOn;
            }
        }

        private static void SetEmissiveState(Renderer[] renderers, bool on, Color onCol, Color offCol)
        {
            if (renderers == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", on ? Color.white : Color.gray);
                mpb.SetColor("_EmissiveColor", on ? onCol : offCol);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
