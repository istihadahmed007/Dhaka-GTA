using System;
using UnityEngine;

namespace DhakaGta.Missions
{
    /// <summary>
    /// Sequential street-race checkpoint ring tracker used by Street Race stories and open-world challenges.
    /// </summary>
    public sealed class StreetRaceController : MonoBehaviour
    {
        private Vector3[] _checkpoints = Array.Empty<Vector3>();
        private float _passRadiusMeters = 8.5f;
        private int _currentIndex;
        private GameObject _activeBeacon;

        public int CurrentCheckpointIndex => _currentIndex;
        public int TotalCheckpoints => _checkpoints.Length;
        public bool IsRaceFinished => _checkpoints.Length > 0 && _currentIndex >= _checkpoints.Length;
        public Vector3 CurrentCheckpointPosition =>
            _checkpoints.Length > 0 && _currentIndex < _checkpoints.Length ? _checkpoints[_currentIndex] : Vector3.zero;

        public void BeginRace(Vector3[] worldCheckpoints, float passRadiusMeters = 9.0f)
        {
            _checkpoints = worldCheckpoints ?? Array.Empty<Vector3>();
            _passRadiusMeters = Mathf.Max(3f, passRadiusMeters);
            _currentIndex = 0;
            UpdateBeaconVisual();
        }

        public bool EvaluateDriverPosition(Vector3 driverWorldPos)
        {
            if (IsRaceFinished || _checkpoints.Length == 0) return true;

            Vector3 cp = _checkpoints[_currentIndex];
            float dx = driverWorldPos.x - cp.x;
            float dz = driverWorldPos.z - cp.z;
            if (dx * dx + dz * dz <= _passRadiusMeters * _passRadiusMeters)
            {
                _currentIndex++;
                UpdateBeaconVisual();
                return IsRaceFinished;
            }
            return false;
        }

        private void UpdateBeaconVisual()
        {
            if (_activeBeacon != null)
            {
                Destroy(_activeBeacon);
                _activeBeacon = null;
            }

            if (IsRaceFinished || _checkpoints.Length == 0) return;

            _activeBeacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _activeBeacon.name = $"RaceCheckpoint_{_currentIndex + 1}";
            var col = _activeBeacon.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _activeBeacon.transform.SetParent(transform, false);
            _activeBeacon.transform.position = _checkpoints[_currentIndex] + Vector3.up * 0.1f;
            _activeBeacon.transform.localScale = new Vector3(_passRadiusMeters * 1.1f, 0.12f, _passRadiusMeters * 1.1f);

            var mr = _activeBeacon.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                var mat = new Material(sh);
                Color gateCol = _currentIndex == _checkpoints.Length - 1
                    ? new Color(1.0f, 0.25f, 0.25f)
                    : new Color(0.15f, 0.88f, 1.0f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gateCol);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", gateCol * 2.5f);
                mr.sharedMaterial = mat;
            }
        }

        private void OnDestroy()
        {
            if (_activeBeacon != null) Destroy(_activeBeacon);
        }
    }
}
