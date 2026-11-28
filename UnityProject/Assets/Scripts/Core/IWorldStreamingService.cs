using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DhakaGta.Core
{
    /// <summary>
    /// Abstraction over additive scene and district asset streaming.
    /// Uses built-in SceneManager fallback until com.unity.addressables is installed and bound.
    /// </summary>
    public interface IWorldStreamingService
    {
        bool IsSceneLoaded(string sceneKey);
        IReadOnlyCollection<string> LoadedScenes { get; }
        Task<bool> LoadAdditiveDistrictAsync(string sceneKey);
        Task<bool> UnloadAdditiveDistrictAsync(string sceneKey);
    }

    /// <summary>
    /// Built-in SceneManager additive streaming implementation active during Foundation Phase 1.
    /// Replaced or extended by AddressableWorldStreamingAdapter once com.unity.addressables lands.
    /// </summary>
    public sealed class SceneStreamingFallbackService : IWorldStreamingService
    {
        private readonly HashSet<string> _loadedScenes = new HashSet<string>();

        public IReadOnlyCollection<string> LoadedScenes => _loadedScenes;

        public bool IsSceneLoaded(string sceneKey)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                return false;
            }

            if (_loadedScenes.Contains(sceneKey))
            {
                return true;
            }

            Scene scene = SceneManager.GetSceneByName(sceneKey);
            return scene.IsValid() && scene.isLoaded;
        }

        public async Task<bool> LoadAdditiveDistrictAsync(string sceneKey)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                return false;
            }

            if (IsSceneLoaded(sceneKey))
            {
                return true;
            }

            if (!ApplicationCanLoadSceneByName(sceneKey))
            {
                return false;
            }

            var op = SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Additive);
            if (op == null)
            {
                return false;
            }

            while (!op.isDone)
            {
                await Task.Yield();
            }

            _loadedScenes.Add(sceneKey);
            return true;
        }

        public async Task<bool> UnloadAdditiveDistrictAsync(string sceneKey)
        {
            if (string.IsNullOrWhiteSpace(sceneKey) || !IsSceneLoaded(sceneKey))
            {
                return false;
            }

            var op = SceneManager.UnloadSceneAsync(sceneKey);
            if (op == null)
            {
                return false;
            }

            while (!op.isDone)
            {
                await Task.Yield();
            }

            _loadedScenes.Remove(sceneKey);
            return true;
        }

        private static bool ApplicationCanLoadSceneByName(string sceneKey)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, sceneKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
