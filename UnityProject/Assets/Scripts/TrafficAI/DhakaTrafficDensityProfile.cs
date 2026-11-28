using System;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.TrafficAI
{
    /// <summary>
    /// Computes authentic Dhaka district and time-of-day traffic & pedestrian density profiles
    /// (rush-hour Gridlock at Motijheel/Kawran/Old Dhaka vs late-night clear avenues, plus district vehicle mix).
    /// </summary>
    public static class DhakaTrafficDensityProfile
    {
        public struct DistrictAiMix
        {
            public float TrafficDensityMultiplier;
            public float PedestrianDensityMultiplier;
            public float CycleRickshawShare;
            public float CngAutoShare;
            public float BusShare;
            public float SedanSuvShare;
            public float MotorcycleShare;
            public float PolicePatrolShare;
        }

        public static float ComputeTimeOfDayMultiplier(float hour24)
        {
            float h = Mathf.Repeat(hour24, 24f);
            // Morning Office Rush (08:00 - 11:00)
            if (h >= 8f && h <= 11f)
            {
                return Mathf.Lerp(1.18f, 1.42f, Mathf.Sin((h - 8f) / 3f * Mathf.PI));
            }
            // Midday Active Commerce (11:00 - 16:30)
            if (h > 11f && h < 16.5f)
            {
                return 1.05f;
            }
            // Evening Dhaka Gridlock Rush (16:30 - 21:00)
            if (h >= 16.5f && h <= 21f)
            {
                return Mathf.Lerp(1.15f, 1.48f, Mathf.Sin((h - 16.5f) / 4.5f * Mathf.PI));
            }
            // Late Evening Wind-down (21:00 - 23:30)
            if (h > 21f && h < 23.5f)
            {
                return 0.65f;
            }
            // Quiet Late Night / Pre-Dawn (23:30 - 06:00)
            return h >= 23.5f || h < 6f ? 0.30f : 0.82f;
        }

        public static DistrictAiMix GetDistrictProfile(string districtId, float hour24)
        {
            float timeScale = ComputeTimeOfDayMultiplier(hour24);
            string id = string.IsNullOrEmpty(districtId) ? "gulshan" : districtId.ToLowerInvariant();

            DistrictAiMix mix = id switch
            {
                "old_dhaka" or "chawkbazar" or "sadarghat" => new DistrictAiMix
                {
                    TrafficDensityMultiplier = 1.32f * timeScale,
                    PedestrianDensityMultiplier = 1.45f * Mathf.Clamp(timeScale, 0.38f, 1.35f),
                    CycleRickshawShare = 0.36f,
                    CngAutoShare = 0.28f,
                    BusShare = 0.06f,
                    SedanSuvShare = 0.12f,
                    MotorcycleShare = 0.14f,
                    PolicePatrolShare = 0.04f
                },
                "motijheel" or "shapla_crossing" or "kawran_bazar" => new DistrictAiMix
                {
                    TrafficDensityMultiplier = 1.38f * timeScale,
                    PedestrianDensityMultiplier = 1.35f * Mathf.Clamp(timeScale, 0.32f, 1.35f),
                    CycleRickshawShare = 0.14f,
                    CngAutoShare = 0.28f,
                    BusShare = 0.22f,
                    SedanSuvShare = 0.20f,
                    MotorcycleShare = 0.11f,
                    PolicePatrolShare = 0.05f
                },
                "gulshan" or "banani" or "dhanmondi" => new DistrictAiMix
                {
                    TrafficDensityMultiplier = 1.10f * timeScale,
                    PedestrianDensityMultiplier = 1.05f * Mathf.Clamp(timeScale, 0.35f, 1.30f),
                    CycleRickshawShare = 0.12f,
                    CngAutoShare = 0.22f,
                    BusShare = 0.08f,
                    SedanSuvShare = 0.38f,
                    MotorcycleShare = 0.12f,
                    PolicePatrolShare = 0.08f
                },
                "hatirjheel" => new DistrictAiMix
                {
                    TrafficDensityMultiplier = 0.92f * timeScale,
                    PedestrianDensityMultiplier = 1.15f * Mathf.Clamp(timeScale, 0.35f, 1.25f),
                    CycleRickshawShare = 0.04f,
                    CngAutoShare = 0.20f,
                    BusShare = 0.06f,
                    SedanSuvShare = 0.44f,
                    MotorcycleShare = 0.20f,
                    PolicePatrolShare = 0.06f
                },
                _ => new DistrictAiMix
                {
                    TrafficDensityMultiplier = 1.0f * timeScale,
                    PedestrianDensityMultiplier = 1.0f * Mathf.Clamp(timeScale, 0.35f, 1.25f),
                    CycleRickshawShare = 0.20f,
                    CngAutoShare = 0.26f,
                    BusShare = 0.14f,
                    SedanSuvShare = 0.22f,
                    MotorcycleShare = 0.13f,
                    PolicePatrolShare = 0.05f
                }
            };

            return mix;
        }

        public static DhakaVehicleClass SampleVehicleClass(DistrictAiMix mix, float roll01)
        {
            float r = Mathf.Clamp01(roll01);
            float acc = mix.CycleRickshawShare;
            if (r < acc) return DhakaVehicleClass.CycleRickshaw;

            acc += mix.CngAutoShare;
            if (r < acc) return DhakaVehicleClass.CngAutoRickshaw;

            acc += mix.BusShare;
            if (r < acc) return r < acc - mix.BusShare * 0.35f ? DhakaVehicleClass.CityBus : DhakaVehicleClass.IntercityBus;

            acc += mix.SedanSuvShare;
            if (r < acc) return (r * 100f % 3f) < 1.8f ? DhakaVehicleClass.CompactSedan : DhakaVehicleClass.Suv;

            acc += mix.MotorcycleShare;
            if (r < acc) return DhakaVehicleClass.Motorcycle;

            return DhakaVehicleClass.DmpPoliceCruiser;
        }
    }
}
