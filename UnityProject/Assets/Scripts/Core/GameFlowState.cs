using System;

namespace DhakaGta.Core
{
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string MainMenu = "MainMenu";
        public const string TestCity = "TestCity";
    }

    public enum GameState
    {
        Bootstrapping = 0,
        MainMenu = 1,
        LoadingCity = 2,
        InCityPlayable = 3,
        Paused = 4,
    }

    public enum HdrpQualityTier
    {
        HighFidelity = 0,
        Balanced = 1,
        Performant = 2,
    }

    /// <summary>
    /// Decoupled C# event bus replacing legacy polling in DhakaGameEngine.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<GameState> OnGameStateChanged;
        public static event Action<string, string> OnDistrictChanged;
        public static event Action<string> OnNearestRoadChanged;
        public static event Action<HdrpQualityTier> OnQualityTierChanged;
        public static event Action<string> OnProtagonistChanged;

        public static void RaiseGameStateChanged(GameState state) => OnGameStateChanged?.Invoke(state);

        public static void RaiseDistrictChanged(string districtId, string displayName) =>
            OnDistrictChanged?.Invoke(districtId, displayName);

        public static void RaiseNearestRoadChanged(string roadName) =>
            OnNearestRoadChanged?.Invoke(roadName);

        public static void RaiseQualityTierChanged(HdrpQualityTier tier) =>
            OnQualityTierChanged?.Invoke(tier);

        public static void RaiseProtagonistChanged(string protagonistId) =>
            OnProtagonistChanged?.Invoke(protagonistId);
    }
}
