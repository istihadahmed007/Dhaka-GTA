using System;
using System.Collections.Generic;
using DhakaGta.GIS;
using UnityEngine;

namespace DhakaGta.WorldGen
{
    public enum ChunkLoadState
    {
        Unloaded = 0,
        Active = 1,
    }

    [Serializable]
    public sealed class WorldChunkRecord
    {
        public Vector2Int gridCoord;
        public string chunkKey;
        public GameObject container;
        public ChunkLoadState state;
        public int objectCount;
    }

    /// <summary>
    /// Partitions real-scale (1:1 meter) Dhaka geometry into fixed-size spatial grid chunks (default 500m x 500m)
    /// and activates/deactivates chunks smoothly around the tracked vehicle or camera focus within frame budgets.
    /// </summary>
    public sealed class WorldChunkManager : MonoBehaviour
    {
        [SerializeField] private float chunkSizeMeters = 500f;
        [SerializeField] private float activeRadiusMeters = 1800f;
        [SerializeField] private int maxChunkTogglesPerFrame = 4;

        private readonly Dictionary<Vector2Int, WorldChunkRecord> _chunks = new Dictionary<Vector2Int, WorldChunkRecord>();
        private readonly Queue<WorldChunkRecord> _pendingActivate = new Queue<WorldChunkRecord>();
        private readonly Queue<WorldChunkRecord> _pendingDeactivate = new Queue<WorldChunkRecord>();

        public float ChunkSizeMeters => chunkSizeMeters;
        public IReadOnlyDictionary<Vector2Int, WorldChunkRecord> Chunks => _chunks;
        public int ActiveChunkCount { get; private set; }

        public Vector2Int PositionToChunkCoord(Vector3 localWorldPos)
        {
            float size = Mathf.Max(50f, chunkSizeMeters);
            int cx = Mathf.FloorToInt(localWorldPos.x / size);
            int cz = Mathf.FloorToInt(localWorldPos.z / size);
            return new Vector2Int(cx, cz);
        }

        public Vector2Int UtmToChunkCoord(in UtmCoord utm, in UtmCoord originUtm)
        {
            float size = Mathf.Max(50f, chunkSizeMeters);
            double dx = utm.easting - originUtm.easting;
            double dz = utm.northing - originUtm.northing;
            return new Vector2Int((int)Math.Floor(dx / size), (int)Math.Floor(dz / size));
        }

        public Transform GetOrCreateChunkContainer(Vector3 localPos, Transform rootParent)
        {
            Vector2Int coord = PositionToChunkCoord(localPos);
            if (_chunks.TryGetValue(coord, out WorldChunkRecord rec) && rec.container != null)
            {
                rec.objectCount++;
                return rec.container.transform;
            }

            var go = new GameObject($"Chunk_{coord.x}_{coord.y}");
            if (rootParent != null)
            {
                go.transform.SetParent(rootParent, false);
            }

            var newRec = new WorldChunkRecord
            {
                gridCoord = coord,
                chunkKey = $"chunk_{coord.x}_{coord.y}",
                container = go,
                state = ChunkLoadState.Active,
                objectCount = 1,
            };
            _chunks[coord] = newRec;
            ActiveChunkCount++;
            return go.transform;
        }

        public void EvaluateStreamingAround(Vector3 focusLocalPosition)
        {
            float size = Mathf.Max(50f, chunkSizeMeters);
            float activeRadiusSq = activeRadiusMeters * activeRadiusMeters;

            _pendingActivate.Clear();
            _pendingDeactivate.Clear();

            foreach (var kvp in _chunks)
            {
                WorldChunkRecord rec = kvp.Value;
                if (rec?.container == null) continue;

                Vector3 chunkCenter = new Vector3((kvp.Key.x + 0.5f) * size, focusLocalPosition.y, (kvp.Key.y + 0.5f) * size);
                float dx = chunkCenter.x - focusLocalPosition.x;
                float dz = chunkCenter.z - focusLocalPosition.z;
                bool shouldBeActive = (dx * dx + dz * dz) <= activeRadiusSq;

                if (shouldBeActive && rec.state != ChunkLoadState.Active)
                {
                    _pendingActivate.Enqueue(rec);
                }
                else if (!shouldBeActive && rec.state == ChunkLoadState.Active)
                {
                    _pendingDeactivate.Enqueue(rec);
                }
            }

            FlushFrameBudget();
        }

        private void FlushFrameBudget()
        {
            int ops = 0;
            while (_pendingActivate.Count > 0 && ops < maxChunkTogglesPerFrame)
            {
                WorldChunkRecord rec = _pendingActivate.Dequeue();
                if (rec.container != null && !rec.container.activeSelf)
                {
                    rec.container.SetActive(true);
                    rec.state = ChunkLoadState.Active;
                    ActiveChunkCount++;
                }
                ops++;
            }

            while (_pendingDeactivate.Count > 0 && ops < maxChunkTogglesPerFrame)
            {
                WorldChunkRecord rec = _pendingDeactivate.Dequeue();
                if (rec.container != null && rec.container.activeSelf)
                {
                    rec.container.SetActive(false);
                    rec.state = ChunkLoadState.Unloaded;
                    ActiveChunkCount = Mathf.Max(0, ActiveChunkCount - 1);
                }
                ops++;
            }
        }
    }
}
