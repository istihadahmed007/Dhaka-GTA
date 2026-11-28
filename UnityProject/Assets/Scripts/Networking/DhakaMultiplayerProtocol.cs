using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DhakaGta.Networking
{
    public static class DhakaNetProtocolConstants
    {
        public const int ServerTickRateHz = 20;
        public const float InterpolationDelaySec = 0.10f; // 100 ms snapshot buffer
        public const string DefaultLocalServerUrl = "ws://127.0.0.1:8080";
        public const string DefaultProductionWssUrl = "wss://dhaka-gta-server.onrender.com";
        public const int MaxPlayersPerRoom = 16;
    }

    // ==================== CLIENT -> SERVER PACKETS ====================

    [Serializable]
    public sealed class CAuthPacket
    {
        public string type = "c_auth";
        public string name;
        public string token;
        public string pin;
        public string outfit = "street";
        public int color = 0x26466d;
        public string protagonistId = "tariq";
        public string clientEngine = "unity";
    }

    [Serializable]
    public sealed class CInputPacket
    {
        public string type = "c_input";
        public int seq;
        public float dt;
        public float moveX;
        public float moveZ;
        public float heading;
        public bool sprint;
        public bool jump;
    }

    [Serializable]
    public sealed class CVehicleActionPacket
    {
        public string type = "c_vehicle_action";
        public string action; // "enter" | "exit"
        public string vehicleId;
    }

    [Serializable]
    public sealed class CVehicleInputPacket
    {
        public string type = "c_vehicle_input";
        public int seq;
        public float throttle;
        public float steer;
        public bool handbrake;
        public bool horn;
    }

    [Serializable]
    public sealed class CAttackPacket
    {
        public string type = "c_attack";
        public int seq;
        public string weapon;
        public float aimHeading;
        public string targetId;
        public string targetKind; // "player" | "vehicle"
    }

    [Serializable]
    public sealed class CChatPacket
    {
        public string type = "c_chat";
        public string text;
    }

    [Serializable]
    public sealed class CBuyItemPacket
    {
        public string type = "c_buy_item";
        public string itemId;
    }

    [Serializable]
    public sealed class CUseItemPacket
    {
        public string type = "c_use_item";
        public string itemId;
    }

    [Serializable]
    public sealed class CBuyVehiclePacket
    {
        public string type = "c_buy_vehicle";
        public string vehicleKind;
    }

    [Serializable]
    public sealed class CVehicleLockPacket
    {
        public string type = "c_vehicle_lock";
        public string vehicleId;
        public bool locked;
    }

    [Serializable]
    public sealed class CSwitchProtagonistPacket
    {
        public string type = "c_switch_protagonist";
        public string protagonistId;
    }

    [Serializable]
    public sealed class CProgressionSyncPacket
    {
        public string type = "c_progression_sync";
        public string completedMissionId;
    }

    [Serializable]
    public sealed class CPingPacket
    {
        public string type = "c_ping";
        public long clientTime;
    }

    // ==================== SERVER -> CLIENT PACKETS ====================

    [Serializable]
    public sealed class BasePacketEnvelope
    {
        public string type;
    }

    [Serializable]
    public sealed class NetTransformDto
    {
        public float x, y, z, vx, vy, vz, heading;
    }

    [Serializable]
    public sealed class NetPlayerStateDto
    {
        public string id;
        public string name;
        public string protagonistId;
        public string outfit;
        public int color;
        public NetTransformDto transform;
        public float hp;
        public float maxHp;
        public float armor;
        public float maxArmor;
        public float stamina;
        public int money;
        public int wantedLevel;
        public string weapon;
        public string[] ownedVehicleIds;
        public string[] completedMissionIds;
        public string vehicleId;
        public string seat;
        public string state;
        public int kills;
        public int deaths;
        public int ping;
    }

    [Serializable]
    public sealed class NetPlayerSnapshotDto
    {
        public string id;
        public string name;
        public string protagonistId;
        public float x, y, z, vx, vz, heading;
        public float hp, armor, stamina;
        public string weapon;
        public string vehicleId;
        public string state;
        public int ping;
    }

    [Serializable]
    public sealed class NetVehicleSnapshotDto
    {
        public string id;
        public string kind;
        public float x, y, z, vx, vz, heading, speed, steerAngle, hp, fuel;
        public int color;
        public string driverId;
        public string ownerId;
        public bool locked;
        public bool sirenOn;
        public bool honkActive;
    }

    [Serializable]
    public sealed class NetWorldStateDto
    {
        public float timeOfDay;
        public string weather;
        public int serverUptimeSec;
        public int connectedCount;
    }

    [Serializable]
    public sealed class SAuthOkPacket
    {
        public string type;
        public string playerId;
        public string token;
        public bool isNewAccount;
        public NetPlayerStateDto player;
        public NetWorldStateDto world;
        public NetVehicleSnapshotDto[] vehicles;
    }

    [Serializable]
    public sealed class SAuthFailPacket
    {
        public string type;
        public string reason;
    }

    [Serializable]
    public sealed class SWorldSnapshotPacket
    {
        public string type;
        public int serverTick;
        public long timestamp;
        public NetPlayerSnapshotDto[] players;
        public NetVehicleSnapshotDto[] vehicles;
        public NetWorldStateDto world;
    }

    [Serializable]
    public sealed class SReconcilePacket
    {
        public string type;
        public int lastProcessedInput;
        public float x, y, z, vx, vz, heading;
        public float hp, armor;
        public int money;
        public int wantedLevel;
        public string vehicleId;
    }

    [Serializable]
    public sealed class SCombatEventPacket
    {
        public string type;
        public string attackerId, attackerName, victimId, victimName, victimKind, weapon;
        public float damage, hitX, hitY, hitZ;
        public bool lethal;
    }

    [Serializable]
    public sealed class SChatPacket
    {
        public string type;
        public string senderId, senderName, text;
        public long timestamp;
        public bool isSystem;
    }

    [Serializable]
    public sealed class SPongPacket
    {
        public string type;
        public long clientTime;
        public long serverTime;
    }

    [Serializable]
    public sealed class SPlayerJoinedPacket
    {
        public string type;
        public NetPlayerStateDto player;
    }

    [Serializable]
    public sealed class SPlayerLeftPacket
    {
        public string type;
        public string playerId, playerName, reason;
    }

    [Serializable]
    public sealed class STransactionResultPacket
    {
        public string type;
        public bool ok;
        public int money;
        public string itemId, message;
    }

    [Serializable]
    public sealed class SAccountSyncPacket
    {
        public string type;
        public int money;
        public string protagonistId;
        public string[] ownedVehicleIds;
        public string[] completedMissionIds;
        public string[] unlockedWeapons;
    }

    [Serializable]
    public sealed class SAntiCheatAlertPacket
    {
        public string type;
        public int strikes;
        public string code, message;
    }

    [Serializable]
    public sealed class SErrorPacket
    {
        public string type;
        public string code, message;
    }

    public static class ProtocolJsonMapHelper
    {
        private static readonly Regex KeyIntRegex = new Regex("\"([^\"]+)\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Extracts nested dictionary objects (such as server "inventory": {"cha_flask": 2}) cleanly
        /// alongside Unity JsonUtility without third-party JSON dependencies.
        /// </summary>
        public static Dictionary<string, int> ExtractIntDictionary(string rawJson, string objectFieldName)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(rawJson) || string.IsNullOrEmpty(objectFieldName))
            {
                return result;
            }

            string token = $"\"{objectFieldName}\"";
            int keyIdx = rawJson.IndexOf(token, StringComparison.Ordinal);
            if (keyIdx < 0) return result;

            int braceOpen = rawJson.IndexOf('{', keyIdx + token.Length);
            if (braceOpen < 0) return result;

            int braceClose = rawJson.IndexOf('}', braceOpen + 1);
            if (braceClose <= braceOpen) return result;

            string slice = rawJson.Substring(braceOpen, braceClose - braceOpen + 1);
            foreach (Match m in KeyIntRegex.Matches(slice))
            {
                if (int.TryParse(m.Groups[2].Value, out int qty))
                {
                    result[m.Groups[1].Value] = qty;
                }
            }
            return result;
        }
    }
}
