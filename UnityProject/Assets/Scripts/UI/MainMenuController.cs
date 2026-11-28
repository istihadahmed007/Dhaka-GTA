using DhakaGta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DhakaGta.UI
{
    /// <summary>
    /// Controls MainMenu.unity live HDRP Dhaka backdrop camera orbit, quality tier selector,
    /// and transition into the TestCity vertical slice foundation.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Camera Backdrop")]
        [SerializeField] private Transform orbitPivot;
        [SerializeField] private float orbitDegreesPerSec = 5f;

        [Header("uGUI Widgets")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button qualityButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TextMeshProUGUI qualityButtonLabel;
        [SerializeField] private TextMeshProUGUI buildInfoLabel;

        private HdrpQualityTier _selectedTier = HdrpQualityTier.Balanced;

        public void BindWidgets(
            Transform cameraPivot,
            Button playBtn,
            Button qualityBtn,
            Button quitBtn,
            TextMeshProUGUI qualityLabel,
            TextMeshProUGUI infoLabel)
        {
            orbitPivot = cameraPivot;
            playButton = playBtn;
            qualityButton = qualityBtn;
            quitButton = quitBtn;
            qualityButtonLabel = qualityLabel;
            buildInfoLabel = infoLabel;
            WireButtons();
        }

        private void Start()
        {
            GameBootstrap.SetState(GameState.MainMenu);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            int currentLevel = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 2);
            _selectedTier = (HdrpQualityTier)currentLevel;

            WireButtons();
            RefreshLabels();
        }

        private void Update()
        {
            if (orbitPivot != null)
            {
                orbitPivot.Rotate(Vector3.up, orbitDegreesPerSec * Time.deltaTime, Space.World);
            }
        }

        public void OnPlayPressed()
        {
            GameBootstrap.LoadTestCity();
        }

        public void OnCycleQualityPressed()
        {
            int next = ((int)_selectedTier + 1) % 3;
            _selectedTier = (HdrpQualityTier)next;
            GameBootstrap.ApplyQualityTier(_selectedTier);
            RefreshLabels();
        }

        public void OnQuitPressed()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void WireButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayPressed);
                playButton.onClick.AddListener(OnPlayPressed);
            }

            if (qualityButton != null)
            {
                qualityButton.onClick.RemoveListener(OnCycleQualityPressed);
                qualityButton.onClick.AddListener(OnCycleQualityPressed);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitPressed);
                quitButton.onClick.AddListener(OnQuitPressed);
            }
        }

        private void RefreshLabels()
        {
            if (qualityButtonLabel != null)
            {
                qualityButtonLabel.text = $"HDRP Quality: {_selectedTier}";
            }

            if (buildInfoLabel != null)
            {
                buildInfoLabel.text = $"Unity 6 HDRP Foundation | GPU: {SystemInfo.graphicsDeviceName}";
            }
        }
    }
}
