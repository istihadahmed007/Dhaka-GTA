using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace DhakaGta.Core
{
    /// <summary>
    /// Entry-point bootstrapper in Bootstrap.unity.
    /// Validates HDRP pipeline configuration, applies custom physics matrix, selects a safe initial
    /// quality tier for the active GPU, and transitions into MainMenu.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private float splashMinimumSeconds = 0.6f;
        [SerializeField] private bool autoTransitionToMainMenu = true;
        [SerializeField] private string nextSceneName = SceneNames.MainMenu;

        public static GameBootstrap Instance { get; private set; }
        public static IWorldStreamingService StreamingService { get; private set; }
        public static GameState CurrentState { get; private set; } = GameState.Bootstrapping;

        public bool IsHdrpActive { get; private set; }
        public string ActiveRendererDevice { get; private set; }
        public string StatusMessage { get; private set; } = "Initializing Dhaka-GTA Engine...";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeStaticSystems()
        {
            PhysicsLayers.ApplyCollisionMatrix();
            StreamingService ??= new SceneStreamingFallbackService();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            StreamingService ??= new SceneStreamingFallbackService();
            PhysicsLayers.ApplyCollisionMatrix();
            ValidateHdrpPipeline();
            ConfigureHardwareSafeQualityTier();
        }

        private IEnumerator Start()
        {
            SetState(GameState.Bootstrapping);
            StatusMessage = IsHdrpActive
                ? $"HDRP Ready ({ActiveRendererDevice})"
                : "WARNING: Active pipeline is not HDRenderPipelineAsset!";

            if (!autoTransitionToMainMenu)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, splashMinimumSeconds));
            LoadMainMenu();
        }

        public static void SetState(GameState state)
        {
            if (CurrentState == state)
            {
                return;
            }

            CurrentState = state;
            GameEvents.RaiseGameStateChanged(state);
        }

        public static void LoadMainMenu()
        {
            SetState(GameState.MainMenu);
            if (CanLoadScene(SceneNames.MainMenu))
            {
                SceneManager.LoadScene(SceneNames.MainMenu, LoadSceneMode.Single);
            }
        }

        public static void LoadTestCity()
        {
            SetState(GameState.LoadingCity);
            if (CanLoadScene(SceneNames.TestCity))
            {
                SceneManager.LoadScene(SceneNames.TestCity, LoadSceneMode.Single);
            }
        }

        public static void ApplyQualityTier(HdrpQualityTier tier)
        {
            int targetIndex = (int)tier;
            if (targetIndex >= 0 && targetIndex < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(targetIndex, applyExpensiveChanges: true);
                GameEvents.RaiseQualityTierChanged(tier);
            }
        }

        private void ValidateHdrpPipeline()
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            IsHdrpActive = rp is HDRenderPipelineAsset;
            ActiveRendererDevice = $"{SystemInfo.graphicsDeviceType} / {SystemInfo.graphicsDeviceName}";

            if (!IsHdrpActive)
            {
                Debug.LogError("[DhakaGta.Bootstrap] Expected HDRenderPipelineAsset, found: " + (rp != null ? rp.GetType().Name : "null"));
            }
            else
            {
                Debug.Log($"[DhakaGta.Bootstrap] Verified HDRP Asset '{rp.name}' on {ActiveRendererDevice} ({SystemInfo.graphicsMemorySize} MB VRAM).");
            }
        }

        private static void ConfigureHardwareSafeQualityTier()
        {
            // Keep Balanced or Performant on constrained integrated GPUs so editor/PC play stays responsive.
            if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 512)
            {
                ApplyQualityTier(HdrpQualityTier.Performant);
            }
        }

        private static bool CanLoadScene(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), sceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
