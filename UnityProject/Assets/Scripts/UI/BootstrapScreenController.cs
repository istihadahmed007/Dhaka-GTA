using DhakaGta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DhakaGta.UI
{
    /// <summary>
    /// Drives the Bootstrap scene uGUI splash overlay with HDRP verification status.
    /// </summary>
    public sealed class BootstrapScreenController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider progressBar;

        private void Update()
        {
            if (statusText != null && GameBootstrap.Instance != null)
            {
                statusText.text = GameBootstrap.Instance.StatusMessage;
            }

            if (progressBar != null)
            {
                progressBar.value = Mathf.Clamp01(Time.timeSinceLevelLoad / 0.6f);
            }
        }

        public void BindWidgets(TextMeshProUGUI statusLabel, Slider progressSlider)
        {
            statusText = statusLabel;
            progressBar = progressSlider;
        }
    }
}
