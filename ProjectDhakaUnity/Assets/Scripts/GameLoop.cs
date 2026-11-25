using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectDhaka
{
    /// <summary>
    /// Single authoritative gameplay state owner: Title -> Playing <-> Paused -> Ended (Won/Lost).
    /// Handles clean SceneManager reload restarts, hit-stop, invulnerability, and scoring.
    /// </summary>
    public class GameLoop : MonoBehaviour
    {
        public enum PlayState
        {
            Title,
            Playing,
            Paused,
            Ended
        }

        public static GameLoop Instance { get; private set; }
        static bool autoStartOnSceneLoad;

        [SerializeField] int targetIntel = 6;
        [SerializeField] float runDurationSeconds = 60f;
        [SerializeField] int maxHealth = 3;
        [SerializeField] PlayerController player;
        [SerializeField] DhakaCourierVisual courierVisual;

        public PlayState CurrentState { get; private set; } = PlayState.Title;
        public bool Won { get; private set; }
        public string EndSummary { get; private set; } = string.Empty;
        public int TargetIntel => targetIntel;
        public float RunDurationSeconds => runDurationSeconds;
        public int MaxHealth => maxHealth;
        public int IntelCollected { get; private set; }
        public int Score { get; private set; }
        public int Health { get; private set; }
        public float TimeRemaining { get; private set; }
        public int Streak { get; private set; }

        float invulnerableTimer;
        float titleGraceTimer = 0.25f;
        Coroutine hitStopCoroutine;
        bool milestoneShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            autoStartOnSceneLoad = false;
            Time.timeScale = 1f;
        }

        public void BindPlayer(PlayerController controller, DhakaCourierVisual visual)
        {
            player = controller;
            courierVisual = visual;
        }

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            Health = maxHealth;
            TimeRemaining = runDurationSeconds;
        }

        void Start()
        {
            if (autoStartOnSceneLoad)
            {
                autoStartOnSceneLoad = false;
                StartRun();
            }
            else
            {
                CurrentState = PlayState.Title;
                GameShellUI.Instance?.ApplyState(CurrentState, false);
            }
        }

        void Update()
        {
            if (CurrentState == PlayState.Title)
            {
                if (titleGraceTimer > 0f)
                {
                    titleGraceTimer -= Time.unscaledDeltaTime;
                    return;
                }
                if (ReadTitleStartInput())
                {
                    StartRun();
                }
                return;
            }

            if (ReadPauseToggleInput())
            {
                if (CurrentState == PlayState.Playing) PauseRun();
                else if (CurrentState == PlayState.Paused) ResumeRun();
                return;
            }

            if (CurrentState != PlayState.Playing) return;

            if (invulnerableTimer > 0f)
            {
                invulnerableTimer = Mathf.Max(0f, invulnerableTimer - Time.deltaTime);
                if (invulnerableTimer <= 0f)
                {
                    courierVisual?.SetInvulnerableVisual(false);
                }
            }

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                EndRun(false, "Curfew siren sounded before all intel was secured.");
            }
        }

        public void StartRun()
        {
            if (CurrentState == PlayState.Playing) return;
            AudioBank.Instance?.PlayUiClick();
            CurrentState = PlayState.Playing;
            Won = false;
            EndSummary = string.Empty;
            IntelCollected = 0;
            Score = 0;
            Streak = 0;
            Health = maxHealth;
            TimeRemaining = runDurationSeconds;
            invulnerableTimer = 0f;
            milestoneShown = false;
            Time.timeScale = 1f;
            player?.ResetVelocity();
            courierVisual?.SetInvulnerableVisual(false);
            DhakaCameraRig.Instance?.ResetForRunStart();
            GameShellUI.Instance?.ApplyState(CurrentState, true);
        }

        public void PauseRun()
        {
            if (CurrentState != PlayState.Playing) return;
            AudioBank.Instance?.PlayUiClick();
            CurrentState = PlayState.Paused;
            Time.timeScale = 0f;
            GameShellUI.Instance?.ApplyState(CurrentState, true);
        }

        public void ResumeRun()
        {
            if (CurrentState != PlayState.Paused) return;
            AudioBank.Instance?.PlayUiClick();
            CurrentState = PlayState.Playing;
            Time.timeScale = 1f;
            GameShellUI.Instance?.ApplyState(CurrentState, true);
        }

        public void RestartRun()
        {
            AudioBank.Instance?.PlayUiClick();
            Time.timeScale = 1f;
            autoStartOnSceneLoad = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToTitle()
        {
            AudioBank.Instance?.PlayUiClick();
            Time.timeScale = 1f;
            autoStartOnSceneLoad = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void OnIntelCollected(IntelPickup pickup)
        {
            if (CurrentState != PlayState.Playing) return;

            IntelCollected++;
            Streak++;
            int points = 150 + (Streak - 1) * 35;
            Score += points;

            Vector3 pos = pickup != null ? pickup.transform.position : (player != null ? player.transform.position : Vector3.zero);
            AudioBank.Instance?.PlayPickup(Streak);
            VfxManager.Instance?.SpawnPickupBurst(pos);
            FloatingPopup.Spawn(pos + Vector3.up * 1.1f, $"+{points} INTEL", Palette.AccentBright, 1.05f);
            courierVisual?.TriggerPickupPop();
            DhakaCameraRig.Instance?.TriggerPickupImpulse();
            TriggerHitStop(0.045f);

            int halfway = Mathf.Max(1, targetIntel / 2);
            if (!milestoneShown && IntelCollected == halfway && IntelCollected < targetIntel)
            {
                milestoneShown = true;
                VfxManager.Instance?.SpawnMilestoneConfetti(pos);
                GameShellUI.Instance?.ShowMilestoneBanner($"HALFWAY CHECKPOINT • {IntelCollected}/{targetIntel}");
            }

            if (IntelCollected >= targetIntel)
            {
                int timeBonus = Mathf.RoundToInt(TimeRemaining * 15f);
                Score += timeBonus;
                VfxManager.Instance?.SpawnMilestoneConfetti(pos);
                EndRun(true, $"Delivered {IntelCollected}/{targetIntel} Dhaka dispatches with {TimeRemaining:0.0}s left (+{timeBonus} time bonus)!");
            }
        }

        public void OnPlayerHitHazard(Vector3 hazardPos)
        {
            if (CurrentState != PlayState.Playing || invulnerableTimer > 0f) return;

            Health = Mathf.Max(0, Health - 1);
            Streak = 0;
            invulnerableTimer = 1.25f;

            Vector3 playerPos = player != null ? player.transform.position : hazardPos;
            AudioBank.Instance?.PlayHit();
            VfxManager.Instance?.SpawnHitBurst(playerPos);
            FloatingPopup.Spawn(playerPos + Vector3.up * 1.3f, "BLOCKADE HIT! -1 HP", Palette.Danger, 1.15f);
            courierVisual?.TriggerHitWobble();
            courierVisual?.SetInvulnerableVisual(true);
            DhakaCameraRig.Instance?.TriggerHitImpulse();
            TriggerHitStop(0.065f);

            if (Health <= 0)
            {
                EndRun(false, "Curfew patrol disabled your courier rig.");
            }
        }

        public void TriggerLossForTest()
        {
            if (CurrentState == PlayState.Playing)
            {
                EndRun(false, "Curfew patrol disabled your courier rig.");
            }
        }

        void EndRun(bool won, string summary)
        {
            if (CurrentState == PlayState.Ended) return;
            CurrentState = PlayState.Ended;
            Won = won;
            EndSummary = summary;
            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
                hitStopCoroutine = null;
            }
            Time.timeScale = 1f;
            courierVisual?.SetInvulnerableVisual(false);

            if (won) AudioBank.Instance?.PlayWin();
            else AudioBank.Instance?.PlayLose();

            GameShellUI.Instance?.ApplyState(CurrentState, true);
            if (LiveServicesBridge.Instance != null)
            {
                _ = LiveServicesBridge.Instance.SubmitRunAsync(Score, IntelCollected, TimeRemaining, won);
            }
        }

        void TriggerHitStop(float realSeconds)
        {
            if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = StartCoroutine(HitStopRoutine(realSeconds));
        }

        IEnumerator HitStopRoutine(float realSeconds)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = CurrentState == PlayState.Paused ? 0f : 1f;
            hitStopCoroutine = null;
        }

        static bool ReadTitleStartInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
            {
                // Avoid triggering Play on standalone modifier presses
                if (!kb.leftShiftKey.wasPressedThisFrame &&
                    !kb.rightShiftKey.wasPressedThisFrame &&
                    !kb.leftCtrlKey.wasPressedThisFrame &&
                    !kb.escapeKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
            Gamepad pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            {
                return true;
            }
            return false;
        }

        static bool ReadPauseToggleInput()
        {
            bool kbPause = Keyboard.current != null &&
                           (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame);
            bool padPause = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            return kbPause || padPause;
        }
    }
}
