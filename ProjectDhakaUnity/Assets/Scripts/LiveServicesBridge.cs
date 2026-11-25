using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Bridges Project Dhaka gameplay with Unity Gaming Services:
    /// PlayerAuthentication status, LiveOps Remote Config re-fetch, Cloud Save progress slot,
    /// and graceful Leaderboard submission/lookup.
    /// </summary>
    public class LiveServicesBridge : MonoBehaviour
    {
        public const string LeaderboardId = "dhaka_courier_score";

        [Serializable]
        public class RunSaveData
        {
            public int bestScore;
            public float bestTimeRemaining;
            public int totalIntelCollected;
            public int runsCompleted;
        }

        public static LiveServicesBridge Instance { get; private set; }

        public bool IsSignedIn { get; private set; }
        public string PlayerId { get; private set; } = string.Empty;
        public bool LiveOpsEnabled { get; private set; } = true;
        public RunSaveData CareerSave { get; private set; } = new RunSaveData();
        public string RankStatus { get; private set; } = "Rank: checking...";
        public bool LastSaveSucceeded { get; private set; }

        float nextRefreshUnscaledTime;

        void Awake()
        {
            Instance = this;
        }

        async void Start()
        {
            await InitializeAsync();
        }

        void Update()
        {
            if (Time.unscaledTime >= nextRefreshUnscaledTime && nextRefreshUnscaledTime > 0f)
            {
                nextRefreshUnscaledTime = Time.unscaledTime + 30f;
                _ = RefreshLiveOpsAsync();
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && IsSignedIn)
            {
                _ = RefreshLiveOpsAsync();
            }
        }

        public async Task InitializeAsync()
        {
            bool signedIn = await PlayerAuthentication.SignedIn;
            IsSignedIn = signedIn &&
                         UnityServices.State == ServicesInitializationState.Initialized &&
                         AuthenticationService.Instance.IsSignedIn &&
                         !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId);
            PlayerId = IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

            await RefreshLiveOpsAsync();
            nextRefreshUnscaledTime = Time.unscaledTime + 30f;

            if (IsSignedIn && LiveOpsEnabled)
            {
                CareerSave = await PlayerSave.LoadAsync(new RunSaveData()) ?? new RunSaveData();
                await FetchRankGracefullyAsync();
            }
            else if (!LiveOpsEnabled)
            {
                RankStatus = "Live Ops paused by Remote Config";
            }
            else
            {
                RankStatus = "Offline session";
            }
        }

        public async Task<bool> RefreshLiveOpsAsync()
        {
            LiveOpsEnabled = await LiveOps.IsEnabledAsync();
            return LiveOpsEnabled;
        }

        public async Task SubmitRunAsync(int runScore, int intelCollected, float timeRemaining, bool won)
        {
            if (CareerSave == null) CareerSave = new RunSaveData();
            CareerSave.runsCompleted++;
            CareerSave.totalIntelCollected += intelCollected;
            if (runScore > CareerSave.bestScore) CareerSave.bestScore = runScore;
            if (won && timeRemaining > CareerSave.bestTimeRemaining) CareerSave.bestTimeRemaining = timeRemaining;

            if (!IsSignedIn)
            {
                bool ready = await PlayerAuthentication.SignedIn;
                IsSignedIn = ready &&
                             UnityServices.State == ServicesInitializationState.Initialized &&
                             AuthenticationService.Instance.IsSignedIn &&
                             !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId);
                PlayerId = IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;
            }

            if (!IsSignedIn || !LiveOpsEnabled)
            {
                RankStatus = !LiveOpsEnabled ? "Live Ops disabled" : "Offline local best";
                return;
            }

            LastSaveSucceeded = await PlayerSave.SaveAsync(CareerSave);
            await SubmitLeaderboardGracefullyAsync(runScore);
        }

        async Task SubmitLeaderboardGracefullyAsync(int score)
        {
            if (UnityServices.State != ServicesInitializationState.Initialized ||
                !AuthenticationService.Instance.IsSignedIn ||
                string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
            {
                RankStatus = "Rank: unavailable";
                return;
            }

            try
            {
                LeaderboardEntry entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, score);
                RankStatus = $"Global Rank #{entry.Rank + 1}";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Leaderboard submit unavailable until deployed: {e.Message}");
                RankStatus = "Rank: unavailable";
            }
        }

        async Task FetchRankGracefullyAsync()
        {
            if (!IsSignedIn)
            {
                RankStatus = "Rank: unavailable";
                return;
            }

            try
            {
                LeaderboardEntry entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId);
                RankStatus = $"Global Rank #{entry.Rank + 1}";
            }
            catch
            {
                RankStatus = "Rank: unavailable";
            }
        }
    }
}
