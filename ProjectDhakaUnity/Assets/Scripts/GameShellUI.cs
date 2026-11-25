using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectDhaka
{
    /// <summary>
    /// Full Game Shell UI controller: Title overlay over live orbital Dhaka plaza,
    /// Gameplay HUD with animated filled progress bar, Pause/Audio Settings overlay,
    /// End-of-run summary with Cloud Save & Leaderboard rank, and unscaled 0.3s transitions.
    /// </summary>
    public class GameShellUI : MonoBehaviour
    {
        public static GameShellUI Instance { get; private set; }

        [Header("Screen Panels")]
        [SerializeField] CanvasGroup titleGroup;
        [SerializeField] CanvasGroup hudGroup;
        [SerializeField] CanvasGroup pauseGroup;
        [SerializeField] CanvasGroup endGroup;

        [Header("Title Elements")]
        [SerializeField] TextMeshProUGUI titlePromptText;
        [SerializeField] TextMeshProUGUI titleCloudText;

        [Header("HUD Elements")]
        [SerializeField] TextMeshProUGUI hudIntelText;
        [SerializeField] TextMeshProUGUI hudTimerText;
        [SerializeField] TextMeshProUGUI hudScoreText;
        [SerializeField] Image hudProgressFill;
        [SerializeField] Image[] healthPips;

        [Header("Milestone Banner")]
        [SerializeField] CanvasGroup bannerGroup;
        [SerializeField] TextMeshProUGUI bannerText;

        [Header("Pause Sliders")]
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;

        [Header("End Screen")]
        [SerializeField] TextMeshProUGUI endHeadingText;
        [SerializeField] TextMeshProUGUI endBodyText;
        [SerializeField] TextMeshProUGUI endRankText;

        float displayedProgress;
        Coroutine transitionCoroutine;
        Coroutine bannerCoroutine;

        public Image HudProgressFill => hudProgressFill;

        public void BindUI(
            CanvasGroup title,
            CanvasGroup hud,
            CanvasGroup pause,
            CanvasGroup end,
            TextMeshProUGUI promptTxt,
            TextMeshProUGUI cloudTxt,
            TextMeshProUGUI intelTxt,
            TextMeshProUGUI timerTxt,
            TextMeshProUGUI scoreTxt,
            Image progressFill,
            Image[] pips,
            CanvasGroup milestoneGrp,
            TextMeshProUGUI milestoneTxt,
            Slider music,
            Slider sfx,
            TextMeshProUGUI endHead,
            TextMeshProUGUI endBody,
            TextMeshProUGUI endRank)
        {
            titleGroup = title;
            hudGroup = hud;
            pauseGroup = pause;
            endGroup = end;
            titlePromptText = promptTxt;
            titleCloudText = cloudTxt;
            hudIntelText = intelTxt;
            hudTimerText = timerTxt;
            hudScoreText = scoreTxt;
            hudProgressFill = progressFill;
            healthPips = pips;
            bannerGroup = milestoneGrp;
            bannerText = milestoneTxt;
            musicSlider = music;
            sfxSlider = sfx;
            endHeadingText = endHead;
            endBodyText = endBody;
            endRankText = endRank;
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (musicSlider != null && AudioBank.Instance != null)
            {
                musicSlider.SetValueWithoutNotify(AudioBank.Instance.MusicVolume);
                musicSlider.onValueChanged.AddListener(v => AudioBank.Instance?.SetMusicVolume(v));
            }
            if (sfxSlider != null && AudioBank.Instance != null)
            {
                sfxSlider.SetValueWithoutNotify(AudioBank.Instance.SfxVolume);
                sfxSlider.onValueChanged.AddListener(v => AudioBank.Instance?.SetSfxVolume(v));
            }

            if (bannerGroup != null) bannerGroup.alpha = 0f;
            displayedProgress = 0f;
            if (hudProgressFill != null) hudProgressFill.fillAmount = 0f;
        }

        void Update()
        {
            GameLoop loop = GameLoop.Instance;
            if (loop == null) return;

            if (loop.CurrentState == GameLoop.PlayState.Title && titlePromptText != null)
            {
                float pulse = 0.62f + 0.38f * Mathf.Sin(Time.unscaledTime * 3.8f);
                Color c = Palette.AccentBright;
                c.a = pulse;
                titlePromptText.color = c;
                float scale = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 3.8f);
                titlePromptText.transform.localScale = Vector3.one * scale;
            }

            if (titleCloudText != null && LiveServicesBridge.Instance != null)
            {
                var bridge = LiveServicesBridge.Instance;
                string ops = bridge.LiveOpsEnabled ? "LIVE OPS ON" : "LIVE OPS PAUSED";
                string best = bridge.CareerSave != null ? $"BEST {bridge.CareerSave.bestScore}" : "BEST 0";
                titleCloudText.text = bridge.IsSignedIn
                    ? $"CLOUD LINKED • {ops} • {best}"
                    : $"LOCAL SESSION • {best}";
            }

            if (loop.CurrentState == GameLoop.PlayState.Playing || loop.CurrentState == GameLoop.PlayState.Paused)
            {
                UpdateHudValues(loop);
            }
            else if (loop.CurrentState == GameLoop.PlayState.Ended && endRankText != null && LiveServicesBridge.Instance != null)
            {
                var bridge = LiveServicesBridge.Instance;
                int best = bridge.CareerSave != null ? bridge.CareerSave.bestScore : loop.Score;
                endRankText.text = $"CLOUD BEST: {best}  •  {bridge.RankStatus}";
            }
        }

        void UpdateHudValues(GameLoop loop)
        {
            if (hudIntelText != null) hudIntelText.text = $"INTEL  {loop.IntelCollected} / {loop.TargetIntel}";
            if (hudScoreText != null) hudScoreText.text = $"SCORE  {loop.Score}";

            if (hudTimerText != null)
            {
                hudTimerText.text = $"CURFEW  {loop.TimeRemaining:0.0}s";
                hudTimerText.color = loop.TimeRemaining <= 12f ? Palette.Danger : Palette.TextLight;
            }

            if (hudProgressFill != null && loop.TargetIntel > 0)
            {
                float targetFill = Mathf.Clamp01((float)loop.IntelCollected / loop.TargetIntel);
                displayedProgress = Mathf.MoveTowards(displayedProgress, targetFill, Time.unscaledDeltaTime * 2.4f);
                hudProgressFill.fillAmount = displayedProgress;
                hudProgressFill.color = targetFill >= 1f ? Palette.CharacterSecondary : Palette.Accent;
            }

            if (healthPips != null)
            {
                for (int i = 0; i < healthPips.Length; i++)
                {
                    if (healthPips[i] != null)
                    {
                        healthPips[i].color = i < loop.Health ? Palette.CharacterSecondary : Palette.Bounds;
                    }
                }
            }
        }

        public void ApplyState(GameLoop.PlayState state, bool animate)
        {
            if (state == GameLoop.PlayState.Ended || state == GameLoop.PlayState.Title)
            {
                if (bannerCoroutine != null)
                {
                    StopCoroutine(bannerCoroutine);
                    bannerCoroutine = null;
                }
                if (bannerGroup != null) bannerGroup.alpha = 0f;
            }
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            if (!animate)
            {
                SetGroupImmediate(titleGroup, state == GameLoop.PlayState.Title);
                SetGroupImmediate(hudGroup, state == GameLoop.PlayState.Playing || state == GameLoop.PlayState.Paused);
                SetGroupImmediate(pauseGroup, state == GameLoop.PlayState.Paused);
                SetGroupImmediate(endGroup, state == GameLoop.PlayState.Ended);
                return;
            }

            if (state == GameLoop.PlayState.Ended && GameLoop.Instance != null)
            {
                PopulateEndPanel(GameLoop.Instance);
            }

            transitionCoroutine = StartCoroutine(CrossfadeRoutine(state));
        }

        void PopulateEndPanel(GameLoop loop)
        {
            if (endHeadingText != null)
            {
                endHeadingText.text = loop.Won ? "DISPATCH COMPLETE" : "CURFEW INTERCEPTED";
                endHeadingText.color = loop.Won ? Palette.AccentBright : Palette.Danger;
            }
            if (endBodyText != null)
            {
                endBodyText.text = $"{loop.EndSummary}\n\nFINAL SCORE: {loop.Score}   •   INTEL: {loop.IntelCollected}/{loop.TargetIntel}";
            }
            if (endRankText != null)
            {
                var bridge = LiveServicesBridge.Instance;
                endRankText.text = bridge != null ? $"Syncing Cloud Save & Rank... ({bridge.RankStatus})" : "Local run complete";
            }
        }

        public void ShowMilestoneBanner(string message)
        {
            if (bannerGroup == null || bannerText == null) return;
            if (bannerCoroutine != null) StopCoroutine(bannerCoroutine);
            bannerText.text = message;
            bannerCoroutine = StartCoroutine(MilestoneRoutine());
        }

        IEnumerator MilestoneRoutine()
        {
            yield return Ease.FadeCanvasGroup(bannerGroup, 0f, 1f, 0.22f);
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Ease.FadeCanvasGroup(bannerGroup, 1f, 0f, 0.35f);
            bannerCoroutine = null;
        }

        IEnumerator CrossfadeRoutine(GameLoop.PlayState state)
        {
            SetGroupInteractable(titleGroup, state == GameLoop.PlayState.Title);
            SetGroupInteractable(hudGroup, state == GameLoop.PlayState.Playing);
            SetGroupInteractable(pauseGroup, state == GameLoop.PlayState.Paused);
            SetGroupInteractable(endGroup, state == GameLoop.PlayState.Ended);

            float duration = 0.28f;
            float elapsed = 0f;

            float aTitle = titleGroup != null ? titleGroup.alpha : 0f;
            float aHud = hudGroup != null ? hudGroup.alpha : 0f;
            float aPause = pauseGroup != null ? pauseGroup.alpha : 0f;
            float aEnd = endGroup != null ? endGroup.alpha : 0f;

            float tTitle = state == GameLoop.PlayState.Title ? 1f : 0f;
            float tHud = (state == GameLoop.PlayState.Playing || state == GameLoop.PlayState.Paused) ? 1f : 0f;
            float tPause = state == GameLoop.PlayState.Paused ? 1f : 0f;
            float tEnd = state == GameLoop.PlayState.Ended ? 1f : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Ease.InOutSine(Mathf.Clamp01(elapsed / duration));
                if (titleGroup != null) titleGroup.alpha = Mathf.Lerp(aTitle, tTitle, u);
                if (hudGroup != null) hudGroup.alpha = Mathf.Lerp(aHud, tHud, u);
                if (pauseGroup != null) pauseGroup.alpha = Mathf.Lerp(aPause, tPause, u);
                if (endGroup != null) endGroup.alpha = Mathf.Lerp(aEnd, tEnd, u);
                yield return null;
            }

            SetGroupImmediate(titleGroup, state == GameLoop.PlayState.Title);
            SetGroupImmediate(hudGroup, state == GameLoop.PlayState.Playing || state == GameLoop.PlayState.Paused);
            SetGroupImmediate(pauseGroup, state == GameLoop.PlayState.Paused);
            SetGroupImmediate(endGroup, state == GameLoop.PlayState.Ended);
            transitionCoroutine = null;
        }

        static void SetGroupImmediate(CanvasGroup grp, bool visible)
        {
            if (grp == null) return;
            grp.alpha = visible ? 1f : 0f;
            grp.interactable = visible;
            grp.blocksRaycasts = visible;
        }

        static void SetGroupInteractable(CanvasGroup grp, bool interactable)
        {
            if (grp == null) return;
            grp.interactable = interactable;
            grp.blocksRaycasts = interactable;
        }

        public void OnClickStart() => GameLoop.Instance?.StartRun();
        public void OnClickResume() => GameLoop.Instance?.ResumeRun();
        public void OnClickRestart() => GameLoop.Instance?.RestartRun();
        public void OnClickTitle() => GameLoop.Instance?.ReturnToTitle();
    }
}
