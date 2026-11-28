using System;
using UnityEngine;

namespace DhakaGta.Networking
{
    [Serializable]
    public struct DhakaNetSnapshot
    {
        public int tick;
        public string playerId;
        public Vector3 position;
        public Quaternion rotation;
        public float speedMetersPerSec;
    }

    /// <summary>
    /// Transport abstraction targeting fixed-tick server reconciliation in Phase 6.
    /// </summary>
    public interface IMultiplayerSessionTransport
    {
        bool IsConnected { get; }
        int LocalTick { get; }
        void SendPlayerSnapshot(in DhakaNetSnapshot snapshot);
    }
}
