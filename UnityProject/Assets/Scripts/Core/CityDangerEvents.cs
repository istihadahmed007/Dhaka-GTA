using System;
using System.Collections.Generic;
using UnityEngine;

namespace DhakaGta.Core
{
    public enum CityDangerKind
    {
        None = 0,
        Gunshot = 1,
        Explosion = 2,
        VehicleCollision = 3,
        RecklessSpeeding = 4,
        Assault = 5
    }

    [Serializable]
    public struct CityDangerEvent
    {
        public CityDangerKind Kind;
        public Vector3 WorldPosition;
        public float RadiusMeters;
        public float Severity; // 0..1+
        public GameObject Instigator;
        public float UnscaledTimestamp;
    }

    /// <summary>
    /// Decoupled event bus for urban hazards (gunfire, explosions, traffic crashes, wanted crimes)
    /// consumed by pedestrians (fleeing/cowering) and DMP police cruisers without assembly cycles.
    /// </summary>
    public static class CityDangerBus
    {
        private const int MaxStoredEvents = 16;
        private static readonly List<CityDangerEvent> RecentEvents = new List<CityDangerEvent>(MaxStoredEvents);

        public static event Action<CityDangerEvent> OnDangerReported;
        public static event Action<int, Vector3> OnWantedLevelChanged;

        public static int CurrentWantedLevel { get; private set; }
        public static Vector3 LastCrimePosition { get; private set; }
        public static float WantedHeatScore { get; private set; }

        public static IReadOnlyList<CityDangerEvent> ActiveDangerEvents => RecentEvents;

        public static void RaiseDangerEvent(
            CityDangerKind kind,
            Vector3 worldPosition,
            float radiusMeters = 32f,
            float severity = 1.0f,
            GameObject instigator = null,
            bool escalateWanted = false)
        {
            var evt = new CityDangerEvent
            {
                Kind = kind,
                WorldPosition = worldPosition,
                RadiusMeters = Mathf.Max(4f, radiusMeters),
                Severity = Mathf.Clamp(severity, 0.1f, 3f),
                Instigator = instigator,
                UnscaledTimestamp = Time.unscaledTime
            };

            if (RecentEvents.Count >= MaxStoredEvents)
            {
                RecentEvents.RemoveAt(0);
            }
            RecentEvents.Add(evt);

            if (escalateWanted)
            {
                AddWantedHeat(Mathf.Max(0.45f, severity * 0.7f), worldPosition);
            }

            OnDangerReported?.Invoke(evt);
        }

        public static void AddWantedHeat(float heatDelta, Vector3 crimeWorldPos)
        {
            LastCrimePosition = crimeWorldPos;
            WantedHeatScore = Mathf.Clamp(WantedHeatScore + Mathf.Max(0f, heatDelta), 0f, 5.0f);
            int nextStars = Mathf.Clamp(Mathf.CeilToInt(WantedHeatScore - 0.15f), 0, 5);
            if (WantedHeatScore >= 0.4f && nextStars == 0)
            {
                nextStars = 1;
            }

            if (nextStars != CurrentWantedLevel)
            {
                CurrentWantedLevel = nextStars;
                OnWantedLevelChanged?.Invoke(CurrentWantedLevel, LastCrimePosition);
            }
        }

        public static void StepDecay(float dt, float maxLifetimeSeconds = 10f)
        {
            float now = Time.unscaledTime;
            for (int i = RecentEvents.Count - 1; i >= 0; i--)
            {
                if (now - RecentEvents[i].UnscaledTimestamp > maxLifetimeSeconds)
                {
                    RecentEvents.RemoveAt(i);
                }
            }

            if (WantedHeatScore > 0f)
            {
                WantedHeatScore = Mathf.Max(0f, WantedHeatScore - dt * 0.045f);
                int nextStars = WantedHeatScore < 0.25f ? 0 : Mathf.Clamp(Mathf.CeilToInt(WantedHeatScore - 0.15f), 0, 5);
                if (nextStars != CurrentWantedLevel)
                {
                    CurrentWantedLevel = nextStars;
                    OnWantedLevelChanged?.Invoke(CurrentWantedLevel, LastCrimePosition);
                }
            }
        }

        public static void ClearAllForReset()
        {
            RecentEvents.Clear();
            WantedHeatScore = 0f;
            CurrentWantedLevel = 0;
            LastCrimePosition = Vector3.zero;
        }
    }
}
