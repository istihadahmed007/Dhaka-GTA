using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DhakaGta.UI
{
    /// <summary>
    /// Minimal GTA-inspired vertical slice HUD for TestCity.unity:
    /// displays verified District & Road from DhakaDistrictRegistry, Health/Stamina caps, HDRP tier, and Pause panel.
    /// </summary>
    public sealed class TestCityHudController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private InputActionRouter inputRouter;
        [SerializeField] private DhakaDistrictRegistry districtRegistry;
        [SerializeField] private ProtagonistController protagonist;
        [SerializeField] private HealthComponent healthComponent;

        [Header("uGUI Widgets")]
        [SerializeField] private TextMeshProUGUI districtLabel;
        [SerializeField] private TextMeshProUGUI roadLabel;
        [SerializeField] private TextMeshProUGUI statusBadgeLabel;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;

        private bool _isPaused;
        private bool _hudVisible = true;
        private float _fpsSmoothed = 60f;

        public void BindWidgets(
            InputActionRouter router,
            DhakaDistrictRegistry registry,
            ProtagonistController playerController,
            HealthComponent playerHealth,
            TextMeshProUGUI districtText,
            TextMeshProUGUI roadText,
            TextMeshProUGUI badgeText,
            Slider hpBar,
            Slider staminaBar,
            GameObject pauseRoot,
            Button resumeBtn,
            Button menuBtn)
        {
            inputRouter = router;
            districtRegistry = registry;
            protagonist = playerController;
            healthComponent = playerHealth;
            districtLabel = districtText;
            roadLabel = roadText;
            statusBadgeLabel = badgeText;
            healthSlider = hpBar;
            staminaSlider = staminaBar;
            pausePanel = pauseRoot;
            resumeButton = resumeBtn;
            mainMenuButton = menuBtn;
            WirePauseButtons();
        }

        private void Start()
        {
            GameBootstrap.SetState(GameState.InCityPlayable);
            inputRouter?.SetMode(InputContextMode.OnFoot);
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            WirePauseButtons();
            RefreshLocationLabels();
        }

        private void OnEnable()
        {
            GameEvents.OnDistrictChanged += HandleDistrictChanged;
            GameEvents.OnNearestRoadChanged += HandleRoadChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnDistrictChanged -= HandleDistrictChanged;
            GameEvents.OnNearestRoadChanged -= HandleRoadChanged;
        }

        private void Update()
        {
            if (inputRouter != null && inputRouter.PauseCancelAction != null && inputRouter.PauseCancelAction.WasPressedThisFrame())
            {
                SetPaused(!_isPaused);
            }

            if (inputRouter != null && inputRouter.CycleQualityTierAction != null && inputRouter.CycleQualityTierAction.WasPressedThisFrame())
            {
                int next = (QualitySettings.GetQualityLevel() + 1) % 3;
                GameBootstrap.ApplyQualityTier((HdrpQualityTier)next);
            }

            if (inputRouter != null && inputRouter.ToggleHudOverlayAction != null && inputRouter.ToggleHudOverlayAction.WasPressedThisFrame())
            {
                _hudVisible = !_hudVisible;
                if (statusBadgeLabel != null)
                {
                    statusBadgeLabel.gameObject.SetActive(_hudVisible);
                }
            }

            UpdateVitalsAndFps();
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (pausePanel != null)
            {
                pausePanel.SetActive(paused);
            }

            GameBootstrap.SetState(paused ? GameState.Paused : GameState.InCityPlayable);
            inputRouter?.SetMode(paused ? InputContextMode.MenuUi : InputContextMode.OnFoot);
        }

        private void OnResumeClicked() => SetPaused(false);

        private void OnReturnToMenuClicked()
        {
            Time.timeScale = 1f;
            _isPaused = false;
            GameBootstrap.LoadMainMenu();
        }

        private void HandleDistrictChanged(string districtId, string districtName) => RefreshLocationLabels();
        private void HandleRoadChanged(string roadName) => RefreshLocationLabels();

        private void RefreshLocationLabels()
        {
            if (districtRegistry == null)
            {
                return;
            }

            if (districtLabel != null)
            {
                districtLabel.text = districtRegistry.CurrentDistrictName.ToUpperInvariant();
            }

            if (roadLabel != null)
            {
                roadLabel.text = districtRegistry.CurrentRoadName;
            }
        }

        private void UpdateVitalsAndFps()
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.08f);

            if (healthSlider != null && healthComponent != null)
            {
                healthSlider.value = healthComponent.MaxHealth > 0f
                    ? healthComponent.CurrentHealth / healthComponent.MaxHealth
                    : 0f;
            }

            if (staminaSlider != null && protagonist != null)
            {
                staminaSlider.value = protagonist.MaxStamina > 0f
                    ? protagonist.CurrentStamina / protagonist.MaxStamina
                    : 0f;
            }

            if (statusBadgeLabel != null)
            {
                string tierName = QualitySettings.names.Length > QualitySettings.GetQualityLevel()
                    ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                    : "HDRP";
                string hero = protagonist != null && protagonist.Profile != null
                    ? protagonist.Profile.fullName
                    : "Tariq Hasan";
                statusBadgeLabel.text = $"{hero} | HDRP {tierName} | {Mathf.RoundToInt(_fpsSmoothed)} FPS";
            }
        }

        private void WirePauseButtons()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(OnResumeClicked);
                resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(OnReturnToMenuClicked);
                mainMenuButton.onClick.AddListener(OnReturnToMenuClicked);
            }
        }
    }
}
