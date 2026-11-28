using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DhakaGta.Core;
using DhakaGta.GIS;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DhakaGta.WorldGen
{
    public enum RegionLoadStatus
    {
        Unloaded = 0,
        Loading = 1,
        Loaded = 2,
        Failed = 3,
    }

    [Serializable]
    public sealed class DhakaDistrictDescriptor
    {
        public string districtId;
        public string displayName;
        public string banglaName;
        public double centerLat;
        public double centerLon;
        public float radiusMeters;
        public string additiveSceneName;
        public string addressableGroupKey;
        public string geoJsonExtractRelativePath;
    }

    /// <summary>
    /// Canonical catalog & asynchronous loader for all 12 Dhaka open-world districts.
    /// Supports Unity Additive Scene loading, async GeoJSON database streaming, and Addressables adapter hooks
    /// (enabled automatically when com.unity.addressables is installed).
    /// </summary>
    public sealed class WorldRegionLoader : MonoBehaviour
    {
        private readonly Dictionary<string, RegionLoadStatus> _regionStates = new Dictionary<string, RegionLoadStatus>(StringComparer.Ordinal);
        private readonly Dictionary<string, GeoFeatureDatabase> _loadedDatabases = new Dictionary<string, GeoFeatureDatabase>(StringComparer.Ordinal);

        public static readonly DhakaDistrictDescriptor[] AllTwelveDistricts =
        {
            new DhakaDistrictDescriptor
            {
                districtId = "old_dhaka",
                displayName = "Old Dhaka",
                banglaName = "পুরান ঢাকা",
                centerLat = 23.7175, centerLon = 90.4025, radiusMeters = 1350f,
                additiveSceneName = "District_OldDhaka", addressableGroupKey = "dhaka_district_old_dhaka",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "chawkbazar",
                displayName = "Chawkbazar",
                banglaName = "চকবাজার",
                centerLat = 23.7168, centerLon = 90.3952, radiusMeters = 900f,
                additiveSceneName = "District_Chawkbazar", addressableGroupKey = "dhaka_district_chawkbazar",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "sadarghat",
                displayName = "Sadarghat",
                banglaName = "সদরঘাট",
                centerLat = 23.7052, centerLon = 90.4135, radiusMeters = 1050f,
                additiveSceneName = "District_Sadarghat", addressableGroupKey = "dhaka_district_sadarghat",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "motijheel",
                displayName = "Motijheel",
                banglaName = "মতিঝিল",
                centerLat = 23.7315, centerLon = 90.4190, radiusMeters = 1100f,
                additiveSceneName = "District_Motijheel", addressableGroupKey = "dhaka_district_motijheel",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "shapla_chottor",
                displayName = "Shapla Chottor",
                banglaName = "শাপলা চত্বর",
                centerLat = 23.733015, centerLon = 90.417014, radiusMeters = 600f,
                additiveSceneName = "District_ShaplaChottor", addressableGroupKey = "dhaka_district_shapla",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "dhanmondi",
                displayName = "Dhanmondi",
                banglaName = "ধানমন্ডি",
                centerLat = 23.7465, centerLon = 90.3765, radiusMeters = 1650f,
                additiveSceneName = "District_Dhanmondi", addressableGroupKey = "dhaka_district_dhanmondi",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "kawran_bazar",
                displayName = "Kawran Bazar",
                banglaName = "কারওয়ান বাজার",
                centerLat = 23.7512, centerLon = 90.3934, radiusMeters = 1150f,
                additiveSceneName = "District_KawranBazar", addressableGroupKey = "dhaka_district_kawran_bazar",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "gulshan",
                displayName = "Gulshan",
                banglaName = "গুলশান",
                centerLat = 23.7855, centerLon = 90.4158, radiusMeters = 1750f,
                additiveSceneName = "District_Gulshan", addressableGroupKey = "dhaka_district_gulshan",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_gulshan_hatirjheel.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "banani",
                displayName = "Banani",
                banglaName = "বনানী",
                centerLat = 23.7937, centerLon = 90.4048, radiusMeters = 1450f,
                additiveSceneName = "District_Banani", addressableGroupKey = "dhaka_district_banani",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_gulshan_hatirjheel.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "hatirjheel",
                displayName = "Hatirjheel",
                banglaName = "হাতিরঝিল",
                centerLat = 23.7678, centerLon = 90.4110, radiusMeters = 1550f,
                additiveSceneName = "District_Hatirjheel", addressableGroupKey = "dhaka_district_hatirjheel",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_gulshan_hatirjheel.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "mirpur",
                displayName = "Mirpur",
                banglaName = "মিরপুর",
                centerLat = 23.8072, centerLon = 90.3685, radiusMeters = 2400f,
                additiveSceneName = "District_Mirpur", addressableGroupKey = "dhaka_district_mirpur",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
            new DhakaDistrictDescriptor
            {
                districtId = "uttara",
                displayName = "Uttara",
                banglaName = "উত্তরা",
                centerLat = 23.8725, centerLon = 90.3955, radiusMeters = 2600f,
                additiveSceneName = "District_Uttara", addressableGroupKey = "dhaka_district_uttara",
                geoJsonExtractRelativePath = "../GeographicData/dhaka_osm_extract.geojson",
            },
        };

        public event Action<string, RegionLoadStatus> OnRegionStatusChanged;

        public RegionLoadStatus GetStatus(string districtId)
        {
            return _regionStates.TryGetValue(districtId, out RegionLoadStatus s) ? s : RegionLoadStatus.Unloaded;
        }

        public static DhakaDistrictDescriptor FindDescriptor(string districtId)
        {
            foreach (DhakaDistrictDescriptor d in AllTwelveDistricts)
            {
                if (string.Equals(d.districtId, districtId, StringComparison.OrdinalIgnoreCase))
                {
                    return d;
                }
            }
            return null;
        }

        /// <summary>
        /// Determines the active Dhaka district for a given double-precision UTM coordinate using 1:1 metric radii.
        /// </summary>
        public static DhakaDistrictDescriptor ResolveDistrictAtUtm(in UtmCoord queryUtm)
        {
            DhakaDistrictDescriptor best = null;
            double bestScore = double.MaxValue;

            foreach (DhakaDistrictDescriptor d in AllTwelveDistricts)
            {
                UtmCoord center = GeographicCoordinateConverter.LatLonToUtm(d.centerLat, d.centerLon, GeographicCoordinateConverter.DhakaUtmZone);
                double dist = center.Distance2D(queryUtm);
                if (dist <= d.radiusMeters)
                {
                    // Normalized radial proximity favors specific tight districts (e.g. Shapla Chottor over Motijheel,
                    // Chawkbazar over Old Dhaka, Hatirjheel over Gulshan).
                    double normalized = dist / Math.Max(1.0, d.radiusMeters);
                    if (normalized < bestScore)
                    {
                        bestScore = normalized;
                        best = d;
                    }
                }
            }

            if (best != null) return best;

            // Nearest fallback outside strict radius
            double nearestDist = double.MaxValue;
            foreach (DhakaDistrictDescriptor d in AllTwelveDistricts)
            {
                UtmCoord center = GeographicCoordinateConverter.LatLonToUtm(d.centerLat, d.centerLon, GeographicCoordinateConverter.DhakaUtmZone);
                double dist = center.Distance2D(queryUtm);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    best = d;
                }
            }
            return best;
        }

        public async Task<bool> LoadRegionAsync(string districtId)
        {
            DhakaDistrictDescriptor desc = FindDescriptor(districtId);
            if (desc == null) return false;

            if (GetStatus(districtId) == RegionLoadStatus.Loaded || GetStatus(districtId) == RegionLoadStatus.Loading)
            {
                return true;
            }

            SetStatus(districtId, RegionLoadStatus.Loading);

            // Path 1: Additive Scene loading via GameBootstrap.StreamingService (or Addressables once enabled)
            IWorldStreamingService service = GameBootstrap.StreamingService;
            if (service != null && !string.IsNullOrEmpty(desc.additiveSceneName))
            {
                bool loadedScene = await service.LoadAdditiveDistrictAsync(desc.additiveSceneName);
                if (loadedScene)
                {
                    SetStatus(districtId, RegionLoadStatus.Loaded);
                    return true;
                }
            }

            // Path 2: Asynchronous GeoJSON extraction into runtime GeoFeatureDatabase
            if (!string.IsNullOrEmpty(desc.geoJsonExtractRelativePath))
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, desc.geoJsonExtractRelativePath));
                if (File.Exists(fullPath))
                {
                    string jsonText = await File.ReadAllTextAsync(fullPath);
                    var db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
                    db.name = $"RuntimeGeoDb_{districtId}";
                    bool parsed = DhakaGeoImporter.ImportGeoJsonTextIntoDatabase(jsonText, db);
                    if (parsed)
                    {
                        _loadedDatabases[districtId] = db;
                        SetStatus(districtId, RegionLoadStatus.Loaded);
                        return true;
                    }
                    Destroy(db);
                }
            }

            SetStatus(districtId, RegionLoadStatus.Failed);
            return false;
        }

        public async Task<bool> UnloadRegionAsync(string districtId)
        {
            DhakaDistrictDescriptor desc = FindDescriptor(districtId);
            if (desc == null) return false;

            IWorldStreamingService service = GameBootstrap.StreamingService;
            if (service != null && !string.IsNullOrEmpty(desc.additiveSceneName) && service.IsSceneLoaded(desc.additiveSceneName))
            {
                await service.UnloadAdditiveDistrictAsync(desc.additiveSceneName);
            }

            if (_loadedDatabases.TryGetValue(districtId, out GeoFeatureDatabase db))
            {
                _loadedDatabases.Remove(districtId);
                if (db != null) Destroy(db);
            }

            SetStatus(districtId, RegionLoadStatus.Unloaded);
            return true;
        }

        private void SetStatus(string districtId, RegionLoadStatus status)
        {
            _regionStates[districtId] = status;
            OnRegionStatusChanged?.Invoke(districtId, status);
        }
    }
}
