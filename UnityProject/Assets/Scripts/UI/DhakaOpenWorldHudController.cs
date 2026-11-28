using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DhakaGta.UI
{
    /// <summary>
    /// Live Open-World Overlay HUD rendering Active Protagonist & Ability cooldown, BDT Wallet,
    /// Tariq-Maya Bond tier, Wanted Stars, Mission Tracker Card, and Bilingual Bengali + English Dialogues.
    /// </summary>
    public sealed class DhakaOpenWorldHudController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statsWalletLabel;
        [SerializeField] private TextMeshProUGUI missionTrackerLabel;
        [SerializeField] private TextMeshProUGUI dialogueBannerLabel;
        [SerializeField] private GameObject dialogueBannerPanel;

        private ProtagonistController _protagonist;
        private HealthComponent _health;
        private PlayerEconomyAndInventory _economy;
        private DhakaMissionEngine _missionEngine;

        private float _dialogueRemainingTimer;
        private MissionProgressSnapshot _latestMissionSnap;

        public void ConfigureAndBuildHud(
            ProtagonistController protagonist,
            HealthComponent hp,
            PlayerEconomyAndInventory economy,
            DhakaMissionEngine missionEngine)
        {
            _protagonist = protagonist;
            _health = hp;
            _economy = economy;
            _missionEngine = missionEngine;

            if (statsWalletLabel == null || missionTrackerLabel == null || dialogueBannerLabel == null)
            {
                BuildHudCanvas();
            }
            if (dialogueBannerPanel != null) dialogueBannerPanel.SetActive(false);
        }

        private void OnEnable()
        {
            MissionObjectiveEvents.OnMissionUpdated += HandleMissionUpdated;
            MissionObjectiveEvents.OnDialogueShown += HandleDialogueShown;
        }

        private void OnDisable()
        {
            MissionObjectiveEvents.OnMissionUpdated -= HandleMissionUpdated;
            MissionObjectiveEvents.OnDialogueShown -= HandleDialogueShown;
        }

        private void HandleMissionUpdated(MissionProgressSnapshot snap)
        {
            _latestMissionSnap = snap;
        }

        private void HandleDialogueShown(BilingualDialogueLine line)
        {
            _dialogueRemainingTimer = Mathf.Max(2.5f, line.DisplaySeconds);
            if (dialogueBannerPanel != null) dialogueBannerPanel.SetActive(true);
            if (dialogueBannerLabel != null)
            {
                dialogueBannerLabel.text =
                    $"<b><color=#FACC15>{line.Speaker}</color></b>\n" +
                    $"{line.TextBangla}\n" +
                    $"<size=15><color=#CBD5E1>\"{line.TextEnglish}\"</color></size>";
            }
        }

        private void Update()
        {
            if (_dialogueRemainingTimer > 0f)
            {
                _dialogueRemainingTimer -= Time.unscaledDeltaTime;
                if (_dialogueRemainingTimer <= 0f && dialogueBannerPanel != null)
                {
                    dialogueBannerPanel.SetActive(false);
                }
            }

            if (statsWalletLabel != null)
            {
                string who = _protagonist != null && _protagonist.Profile != null
                    ? $"{_protagonist.Profile.fullName} ({_protagonist.Profile.banglaCallsign})"
                    : "Tariq Hasan";
                float hp = _health != null ? _health.CurrentHealth : 100f;
                float maxHp = _health != null ? _health.MaxHealth : 135f;
                int bdt = _economy != null ? _economy.CurrentBdt : 0;
                int stars = CityDangerBus.CurrentWantedLevel;
                string starStr = stars > 0 ? $"<color=#F87171>WANTED: {new string('★', stars)}</color>" : "<color=#4ADE80>CLEAR</color>";
                AbilityActivationState ab = CharacterAbilityEvents.CurrentAbilityState;
                string abStatus = ab.IsActive
                    ? $"[Q] {ab.AbilityKind} ACTIVE ({ab.RemainingDurationSeconds:F1}s)"
                    : "[Q] Ability Ready | [Tab] Switch | [P] Phone";

                statsWalletLabel.text =
                    $"<b>{who}</b>\n" +
                    $"HP: {hp:F0}/{maxHp:F0}  |  ৳{bdt:N0} BDT  |  {starStr}\n" +
                    $"<size=13>{abStatus}</size>";
            }

            if (missionTrackerLabel != null)
            {
                if (_missionEngine != null && _missionEngine.IsMissionActive && !_latestMissionSnap.IsCompleted && !_latestMissionSnap.IsFailed)
                {
                    float dist = _protagonist != null
                        ? Vector3.Distance(_protagonist.transform.position, _latestMissionSnap.TargetWorldPosition)
                        : 0f;
                    string timerPart = _latestMissionSnap.RemainingTimerSeconds > 0f
                        ? $" | TIME: {_latestMissionSnap.RemainingTimerSeconds:F0}s"
                        : "";
                    missionTrackerLabel.text =
                        $"<b><color=#FBBF24>{_latestMissionSnap.TitleEnglish}</color></b> ({_latestMissionSnap.TitleBangla})\n" +
                        $"Step {_latestMissionSnap.CurrentStepIndex + 1}/{Mathf.Max(1, _latestMissionSnap.TotalSteps)}: {_latestMissionSnap.StepPrompt}\n" +
                        $"<size=13>Target: {dist:F0} m | Progress: {_latestMissionSnap.ProgressCount}/{_latestMissionSnap.RequiredCount}{timerPart}</size>";
                }
                else
                {
                    string bond = _economy != null ? _economy.TariqMayaBondTierLabel : "";
                    missionTrackerLabel.text = $"<b>FREE ROAM · DHAKA</b>\nPress [P] Smartphone for Missions, CNG Fares, Deliveries & Races\n<size=13>Partnership: {bond}</size>";
                }
            }
        }

        private void BuildHudCanvas()
        {
            var canvasGo = new GameObject("OpenWorldStoryHudCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            statsWalletLabel = CreatePanelText(canvasGo.transform, "StatsWalletBox",
                new Vector2(0.65f, 0.85f), new Vector2(0.98f, 0.98f), TextAlignmentOptions.TopRight, 16);

            missionTrackerLabel = CreatePanelText(canvasGo.transform, "MissionTrackerBox",
                new Vector2(0.02f, 0.56f), new Vector2(0.38f, 0.70f), TextAlignmentOptions.TopLeft, 15);

            var dlgPanel = new GameObject("DialogueBannerBox");
            dlgPanel.transform.SetParent(canvasGo.transform, false);
            var img = dlgPanel.AddComponent<Image>();
            img.color = new Color(0.03f, 0.04f, 0.07f, 0.85f);
            img.rectTransform.anchorMin = new Vector2(0.18f, 0.04f);
            img.rectTransform.anchorMax = new Vector2(0.82f, 0.16f);
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;
            dialogueBannerPanel = dlgPanel;

            var txtGo = new GameObject("DialogueText");
            txtGo.transform.SetParent(dlgPanel.transform, false);
            dialogueBannerLabel = txtGo.AddComponent<TextMeshProUGUI>();
            dialogueBannerLabel.fontSize = 18;
            dialogueBannerLabel.alignment = TextAlignmentOptions.Center;
            dialogueBannerLabel.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) dialogueBannerLabel.font = TMP_Settings.defaultFontAsset;
            dialogueBannerLabel.rectTransform.anchorMin = new Vector2(0.03f, 0.06f);
            dialogueBannerLabel.rectTransform.anchorMax = new Vector2(0.97f, 0.94f);
            dialogueBannerLabel.rectTransform.offsetMin = Vector2.zero;
            dialogueBannerLabel.rectTransform.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreatePanelText(
            Transform parent, string name, Vector2 aMin, Vector2 aMax, TextAlignmentOptions align, float fontSize)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0.04f, 0.05f, 0.08f, 0.74f);
            img.rectTransform.anchorMin = aMin;
            img.rectTransform.anchorMax = aMax;
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(panel.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.rectTransform.anchorMin = new Vector2(0.04f, 0.08f);
            tmp.rectTransform.anchorMax = new Vector2(0.96f, 0.92f);
            tmp.rectTransform.offsetMin = Vector2.zero;
            tmp.rectTransform.offsetMax = Vector2.zero;
            return tmp;
        }
    }
}
