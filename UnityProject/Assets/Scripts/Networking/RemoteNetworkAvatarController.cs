using System;
using System.Collections.Generic;
using UnityEngine;

namespace DhakaGta.Networking
{
    /// <summary>
    /// Spawns, interpolates (100ms buffer), animates, and removes Humanoid remote character avatars
    /// strictly for real connected multiplayer peers (never spawns fake bots).
    /// </summary>
    public sealed class RemoteNetworkAvatarController : MonoBehaviour
    {
        [Header("Optional Humanoid Prefabs (Part 7 Tariq & Maya)")]
        [SerializeField] private GameObject tariqRemotePrefab;
        [SerializeField] private GameObject mayaRemotePrefab;

        private sealed class TimedPlayerSample
        {
            public double ClientReceiveTime;
            public NetPlayerSnapshotDto Snapshot;
        }

        private sealed class RemoteAvatarInstance
        {
            public string PlayerId;
            public GameObject RootGo;
            public Animator HumanoidAnimator;
            public Renderer[] Renderers;
            public TextMesh NameplateText;
            public readonly List<TimedPlayerSample> Buffer = new List<TimedPlayerSample>(16);
        }

        private readonly Dictionary<string, RemoteAvatarInstance> _peers = new Dictionary<string, RemoteAvatarInstance>();

        public int ActiveRemotePeerCount => _peers.Count;

        public void ConfigurePrefabs(GameObject tariqPrefab, GameObject mayaPrefab)
        {
            tariqRemotePrefab = tariqPrefab;
            mayaRemotePrefab = mayaPrefab;
        }

        public void IngestSnapshot(NetPlayerSnapshotDto[] players, string localPlayerId, long _)
        {
            if (players == null) return;

            double now = Time.realtimeSinceStartupAsDouble;
            var presentIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < players.Length; i++)
            {
                NetPlayerSnapshotDto dto = players[i];
                if (dto == null || string.IsNullOrEmpty(dto.id) || dto.id == localPlayerId) continue;

                presentIds.Add(dto.id);
                if (!_peers.TryGetValue(dto.id, out RemoteAvatarInstance inst))
                {
                    inst = SpawnPeerAvatar(dto);
                    _peers[dto.id] = inst;
                }

                inst.Buffer.Add(new TimedPlayerSample { ClientReceiveTime = now, Snapshot = dto });
                while (inst.Buffer.Count > 12) inst.Buffer.RemoveAt(0);
            }

            var toRemove = new List<string>();
            foreach (var kv in _peers)
            {
                if (!presentIds.Contains(kv.Key)) toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                if (_peers[toRemove[i]]?.RootGo != null) Destroy(_peers[toRemove[i]].RootGo);
                _peers.Remove(toRemove[i]);
            }
        }

        public void ClearAllPeers()
        {
            foreach (var kv in _peers)
            {
                if (kv.Value?.RootGo != null) Destroy(kv.Value.RootGo);
            }
            _peers.Clear();
        }

        private void Update()
        {
            double renderTime = Time.realtimeSinceStartupAsDouble - DhakaNetProtocolConstants.InterpolationDelaySec;

            foreach (var kv in _peers)
            {
                RemoteAvatarInstance peer = kv.Value;
                if (peer?.RootGo == null || peer.Buffer.Count == 0) continue;

                TimedPlayerSample a = peer.Buffer[0];
                TimedPlayerSample b = peer.Buffer[peer.Buffer.Count - 1];
                float t = 1f;

                for (int i = 0; i < peer.Buffer.Count - 1; i++)
                {
                    if (peer.Buffer[i].ClientReceiveTime <= renderTime && peer.Buffer[i + 1].ClientReceiveTime >= renderTime)
                    {
                        a = peer.Buffer[i];
                        b = peer.Buffer[i + 1];
                        double span = Math.Max(0.001, b.ClientReceiveTime - a.ClientReceiveTime);
                        t = Mathf.Clamp01((float)((renderTime - a.ClientReceiveTime) / span));
                        break;
                    }
                }

                Vector3 posA = new Vector3(a.Snapshot.x, a.Snapshot.y, a.Snapshot.z);
                Vector3 posB = new Vector3(b.Snapshot.x, b.Snapshot.y, b.Snapshot.z);
                peer.RootGo.transform.position = Vector3.Lerp(posA, posB, t);

                float yawA = a.Snapshot.heading * Mathf.Rad2Deg;
                float yawB = b.Snapshot.heading * Mathf.Rad2Deg;
                peer.RootGo.transform.rotation = Quaternion.Slerp(Quaternion.Euler(0f, yawA, 0f), Quaternion.Euler(0f, yawB, 0f), t);

                float speed = Mathf.Sqrt(b.Snapshot.vx * b.Snapshot.vx + b.Snapshot.vz * b.Snapshot.vz);
                bool inVehicle = !string.IsNullOrEmpty(b.Snapshot.vehicleId);
                if (peer.HumanoidAnimator != null && peer.HumanoidAnimator.runtimeAnimatorController != null)
                {
                    peer.HumanoidAnimator.SetFloat("MoveSpeed", inVehicle ? 0f : speed);
                    peer.HumanoidAnimator.SetFloat("MoveZ", inVehicle ? 0f : speed);
                    peer.HumanoidAnimator.SetBool("IsGrounded", !inVehicle);
                    peer.HumanoidAnimator.SetBool("InVehicle", inVehicle);
                }

                if (peer.NameplateText != null)
                {
                    string display = string.IsNullOrEmpty(b.Snapshot.name) ? peer.PlayerId : b.Snapshot.name;
                    peer.NameplateText.text = $"{display} ({b.Snapshot.hp:F0} HP)";
                    if (Camera.main != null)
                    {
                        peer.NameplateText.transform.rotation = Quaternion.LookRotation(
                            peer.NameplateText.transform.position - Camera.main.transform.position, Vector3.up);
                    }
                }
            }
        }

        private RemoteAvatarInstance SpawnPeerAvatar(NetPlayerSnapshotDto dto)
        {
            bool isMaya = dto.protagonistId == "maya";
            GameObject prefab = isMaya ? mayaRemotePrefab : tariqRemotePrefab;
            GameObject go;

            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(transform, false);
                BuildArticulatedRemoteVisual(go.transform, isMaya);
            }

            go.name = $"RemotePlayer_{(string.IsNullOrEmpty(dto.name) ? dto.id : dto.name)}";
            go.transform.position = new Vector3(dto.x, dto.y, dto.z);

            // Overhead Nameplate
            var tagGo = new GameObject("Nameplate");
            tagGo.transform.SetParent(go.transform, false);
            tagGo.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            var tm = tagGo.AddComponent<TextMesh>();
            tm.fontSize = 32;
            tm.characterSize = 0.04f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = isMaya ? new Color(0.25f, 0.92f, 0.94f) : new Color(0.98f, 0.85f, 0.25f);
            tm.text = string.IsNullOrEmpty(dto.name) ? dto.id : dto.name;

            return new RemoteAvatarInstance
            {
                PlayerId = dto.id,
                RootGo = go,
                HumanoidAnimator = go.GetComponentInChildren<Animator>(true),
                Renderers = go.GetComponentsInChildren<Renderer>(true),
                NameplateText = tm
            };
        }

        private static void BuildArticulatedRemoteVisual(Transform root, bool isMaya)
        {
            Color jacket = isMaya ? new Color(0.54f, 0.11f, 0.15f) : new Color(0.12f, 0.23f, 0.37f);
            Color skin = new Color(0.76f, 0.54f, 0.40f);
            Shader sh = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");

            void Part(string n, Vector3 lp, Vector3 sc, Color col)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = n;
                Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(root, false);
                p.transform.localPosition = lp;
                p.transform.localScale = sc;
                var mr = p.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                    else mat.color = col;
                    mr.sharedMaterial = mat;
                }
            }

            Part("Torso", new Vector3(0f, 1.25f, 0f), new Vector3(0.36f, 0.56f, 0.22f), jacket);
            Part("Head", new Vector3(0f, 1.66f, 0f), new Vector3(0.18f, 0.22f, 0.20f), skin);
            Part("LeftLeg", new Vector3(-0.10f, 0.46f, 0f), new Vector3(0.13f, 0.88f, 0.14f), new Color(0.11f, 0.13f, 0.17f));
            Part("RightLeg", new Vector3(0.10f, 0.46f, 0f), new Vector3(0.13f, 0.88f, 0.14f), new Color(0.11f, 0.13f, 0.17f));
        }
    }
}
