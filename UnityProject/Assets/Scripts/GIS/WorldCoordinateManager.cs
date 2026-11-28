using System;
using System.Collections.Generic;
using UnityEngine;

namespace DhakaGta.GIS
{
    /// <summary>
    /// Maintains 1 Unity meter = 1 real-world meter across wide Dhaka expanses (~25km+) without float32 jitter
    /// by keeping an authoritative double-precision UTM Zone 46N anchor and dynamically rebasing active world roots
    /// via Floating Origin shifting.
    /// </summary>
    public sealed class WorldCoordinateManager : MonoBehaviour
    {
        [Header("Geographic Datum")]
        [SerializeField] private GeoFeatureDatabase featureDatabase;
        [SerializeField] private double originLatitude = GeographicCoordinateConverter.ShaplaOriginLat;
        [SerializeField] private double originLongitude = GeographicCoordinateConverter.ShaplaOriginLon;
        [SerializeField] private double originElevationMeters;

        [Header("Floating Origin Settings")]
        [SerializeField] private Transform trackedFocusTransform;
        [SerializeField] private float floatingOriginThresholdMeters = 2000f;
        [SerializeField] private bool enableRuntimeOriginShift = true;

        private readonly List<Transform> _floatingRoots = new List<Transform>();

        private UtmCoord _baseOriginUtm;
        private double _shiftEastingMeters;
        private double _shiftNorthingMeters;
        private bool _initialized;

        public event Action<Vector3> OnFloatingOriginShifted;

        public GeoFeatureDatabase FeatureDatabase => featureDatabase;
        public UtmCoord BaseOriginUtm => EnsureInitializedAndGetBaseOrigin();
        public UtmCoord ActiveUnityOriginUtm => new UtmCoord(
            EnsureInitializedAndGetBaseOrigin().easting + _shiftEastingMeters,
            _baseOriginUtm.northing + _shiftNorthingMeters,
            _baseOriginUtm.zone,
            _baseOriginUtm.isNorthernHemisphere);

        public double CumulativeShiftEastingMeters => _shiftEastingMeters;
        public double CumulativeShiftNorthingMeters => _shiftNorthingMeters;

        public void Configure(
            GeoFeatureDatabase database,
            double originLat = GeographicCoordinateConverter.ShaplaOriginLat,
            double originLon = GeographicCoordinateConverter.ShaplaOriginLon,
            double originElev = 0.0,
            Transform focus = null)
        {
            featureDatabase = database;
            originLatitude = originLat;
            originLongitude = originLon;
            originElevationMeters = originElev;
            trackedFocusTransform = focus;
            _shiftEastingMeters = 0.0;
            _shiftNorthingMeters = 0.0;
            _initialized = false;
            EnsureInitializedAndGetBaseOrigin();
        }

        public void RegisterFloatingRoot(Transform root)
        {
            if (root != null && !_floatingRoots.Contains(root))
            {
                _floatingRoots.Add(root);
            }
        }

        public void UnregisterFloatingRoot(Transform root)
        {
            _floatingRoots.Remove(root);
        }

        /// <summary>
        /// Converts a real WGS84 coordinate to exact 1:1 Unity local world space taking active floating-origin shift into account.
        /// </summary>
        public Vector3 LatLonToUnityPosition(double latitudeDeg, double longitudeDeg, double elevationMeters = 0.0)
        {
            UtmCoord utm = GeographicCoordinateConverter.LatLonToUtm(latitudeDeg, longitudeDeg, GeographicCoordinateConverter.DhakaUtmZone);
            return UtmToUnityPosition(utm, elevationMeters);
        }

        /// <summary>
        /// Converts a double-precision UTM 46N coordinate into 1:1 Unity scene coordinates relative to the active floating origin.
        /// </summary>
        public Vector3 UtmToUnityPosition(in UtmCoord utm, double elevationMeters = 0.0)
        {
            return GeographicCoordinateConverter.UtmToUnityLocal(
                utm,
                ActiveUnityOriginUtm,
                elevationMeters,
                originElevationMeters);
        }

        /// <summary>
        /// Converts a Unity scene coordinate back into exact double-precision UTM 46N coordinate regardless of origin shifts.
        /// </summary>
        public UtmCoord UnityPositionToUtm(Vector3 unityPosition)
        {
            return GeographicCoordinateConverter.UnityLocalToUtm(unityPosition, ActiveUnityOriginUtm);
        }

        /// <summary>
        /// Converts a Unity scene coordinate back into WGS84 latitude/longitude.
        /// </summary>
        public GeoLatLon UnityPositionToLatLon(Vector3 unityPosition)
        {
            UtmCoord utm = UnityPositionToUtm(unityPosition);
            return GeographicCoordinateConverter.UtmToLatLon(utm, originElevationMeters + unityPosition.y);
        }

        /// <summary>
        /// Manually triggers floating-origin check or executes an explicit origin rebase when threshold is exceeded.
        /// </summary>
        public bool EvaluateFloatingOrigin()
        {
            if (!enableRuntimeOriginShift || trackedFocusTransform == null)
            {
                return false;
            }

            Vector3 pos = trackedFocusTransform.position;
            float horizDistSq = pos.x * pos.x + pos.z * pos.z;
            float threshold = Mathf.Max(250f, floatingOriginThresholdMeters);
            if (horizDistSq < threshold * threshold)
            {
                return false;
            }

            ShiftOriginByLocalDelta(pos.x, pos.z);
            return true;
        }

        /// <summary>
        /// Shifts the active double-precision origin anchor by (deltaX, deltaZ) meters and moves all registered roots
        /// plus the tracked target in the opposite direction so real-world UTM coordinates remain invariant.
        /// </summary>
        public void ShiftOriginByLocalDelta(double deltaEastMeters, double deltaNorthMeters)
        {
            EnsureInitializedAndGetBaseOrigin();
            _shiftEastingMeters += deltaEastMeters;
            _shiftNorthingMeters += deltaNorthMeters;

            var worldDelta = new Vector3((float)-deltaEastMeters, 0f, (float)-deltaNorthMeters);

            for (int i = _floatingRoots.Count - 1; i >= 0; i--)
            {
                Transform root = _floatingRoots[i];
                if (root == null)
                {
                    _floatingRoots.RemoveAt(i);
                    continue;
                }

                root.position += worldDelta;
            }

            if (trackedFocusTransform != null && !IsUnderRegisteredRoot(trackedFocusTransform))
            {
                trackedFocusTransform.position += worldDelta;
            }

            Physics.SyncTransforms();
            OnFloatingOriginShifted?.Invoke(worldDelta);
        }

        private void Awake()
        {
            EnsureInitializedAndGetBaseOrigin();
        }

        private void LateUpdate()
        {
            EvaluateFloatingOrigin();
        }

        private UtmCoord EnsureInitializedAndGetBaseOrigin()
        {
            if (!_initialized)
            {
                if (featureDatabase != null && featureDatabase.worldOriginUtm.zone == GeographicCoordinateConverter.DhakaUtmZone)
                {
                    _baseOriginUtm = featureDatabase.worldOriginUtm;
                }
                else
                {
                    _baseOriginUtm = GeographicCoordinateConverter.LatLonToUtm(
                        originLatitude,
                        originLongitude,
                        GeographicCoordinateConverter.DhakaUtmZone);
                }

                _initialized = true;
            }

            return _baseOriginUtm;
        }

        private bool IsUnderRegisteredRoot(Transform target)
        {
            foreach (Transform root in _floatingRoots)
            {
                if (root != null && target.IsChildOf(root))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
