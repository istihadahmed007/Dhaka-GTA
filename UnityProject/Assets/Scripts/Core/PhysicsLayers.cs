using System;
using System.Collections.Generic;
using UnityEngine;

namespace DhakaGta.Core
{
    /// <summary>
    /// Canonical physics layer assignments (Layers 6-16) and deterministic collision matrix helpers
    /// matching Documentation/01_UNITY6_ARCHITECTURE_AND_AUDIT.md.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int WaterBuiltin = 4;

        public const int Ground = 6;
        public const int Building = 7;
        public const int Water = 8;
        public const int Player = 9;
        public const int Vehicle = 10;
        public const int VehicleWheel = 11;
        public const int Npc = 12;
        public const int TrafficVehicle = 13;
        public const int Projectile = 14;
        public const int TriggerZone = 15;
        public const int CameraOccluder = 16;

        public const int FirstCustomLayer = Ground;
        public const int LastCustomLayer = CameraOccluder;

        public static readonly IReadOnlyDictionary<int, string> LayerNames = new Dictionary<int, string>
        {
            { Ground, "Ground" },
            { Building, "Building" },
            { Water, "Water" },
            { Player, "Player" },
            { Vehicle, "Vehicle" },
            { VehicleWheel, "VehicleWheel" },
            { Npc, "NPC" },
            { TrafficVehicle, "TrafficVehicle" },
            { Projectile, "Projectile" },
            { TriggerZone, "TriggerZone" },
            { CameraOccluder, "CameraOccluder" },
        };

        public static int GroundMask => 1 << Ground;
        public static int WorldStaticMask => (1 << Ground) | (1 << Building);
        public static int CameraOcclusionMask => (1 << Ground) | (1 << Building);
        public static int DrivableSurfaceMask => (1 << Ground) | (1 << Building) | (1 << Water);
        public static int ProjectileHitMask =>
            (1 << Ground) | (1 << Building) | (1 << Player) | (1 << Vehicle) | (1 << Npc) | (1 << TrafficVehicle);

        private static readonly HashSet<(int, int)> AllowedCustomPairs = BuildAllowedPairs();

        /// <summary>
        /// Returns whether two layers in [6..16] should collide with each other in the PhysX layer collision matrix.
        /// </summary>
        public static bool ShouldLayersCollide(int layerA, int layerB)
        {
            int a = Mathf.Min(layerA, layerB);
            int b = Mathf.Max(layerA, layerB);
            return AllowedCustomPairs.Contains((a, b));
        }

        /// <summary>
        /// Applies all custom collision matrix rules for layers 6..16 via Physics.IgnoreLayerCollision.
        /// Also validates mutual collision matrix pairs against Default (0) where appropriate.
        /// </summary>
        public static void ApplyCollisionMatrix()
        {
            for (int a = FirstCustomLayer; a <= LastCustomLayer; a++)
            {
                for (int b = a; b <= LastCustomLayer; b++)
                {
                    bool collide = ShouldLayersCollide(a, b);
                    Physics.IgnoreLayerCollision(a, b, !collide);
                }

                // TriggerZone, VehicleWheel, and CameraOccluder should not physically collide with Default layer clutter.
                bool collideWithDefault = a != TriggerZone && a != VehicleWheel && a != CameraOccluder;
                Physics.IgnoreLayerCollision(Default, a, !collideWithDefault);
                Physics.IgnoreLayerCollision(IgnoreRaycast, a, false);
            }
        }

        private static HashSet<(int, int)> BuildAllowedPairs()
        {
            var pairs = new HashSet<(int, int)>();

            void Allow(int a, int b)
            {
                pairs.Add((Mathf.Min(a, b), Mathf.Max(a, b)));
            }

            // Ground collides with Player, Vehicle, VehicleWheel, NPC, TrafficVehicle, Projectile, CameraOccluder.
            Allow(Ground, Player);
            Allow(Ground, Vehicle);
            Allow(Ground, VehicleWheel);
            Allow(Ground, Npc);
            Allow(Ground, TrafficVehicle);
            Allow(Ground, Projectile);
            Allow(Ground, CameraOccluder);

            // Building collides with Player, Vehicle, VehicleWheel, NPC, TrafficVehicle, Projectile, CameraOccluder.
            Allow(Building, Player);
            Allow(Building, Vehicle);
            Allow(Building, VehicleWheel);
            Allow(Building, Npc);
            Allow(Building, TrafficVehicle);
            Allow(Building, Projectile);
            Allow(Building, CameraOccluder);

            // Water interacts with Player, Vehicle, and TriggerZone (river docks / buoyancy triggers).
            Allow(Water, Player);
            Allow(Water, Vehicle);
            Allow(Water, TriggerZone);

            // Player collides with Vehicle, NPC, TrafficVehicle, Projectile, TriggerZone (and static layers above).
            Allow(Player, Vehicle);
            Allow(Player, Npc);
            Allow(Player, TrafficVehicle);
            Allow(Player, Projectile);
            Allow(Player, TriggerZone);

            // Vehicle chassis collides with Vehicle, NPC, TrafficVehicle, Projectile, TriggerZone (ignores VehicleWheel).
            Allow(Vehicle, Vehicle);
            Allow(Vehicle, Npc);
            Allow(Vehicle, TrafficVehicle);
            Allow(Vehicle, Projectile);
            Allow(Vehicle, TriggerZone);

            // NPCs collide with NPC, TrafficVehicle, Projectile, TriggerZone.
            Allow(Npc, Npc);
            Allow(Npc, TrafficVehicle);
            Allow(Npc, Projectile);
            Allow(Npc, TriggerZone);

            // Traffic vehicles collide with TrafficVehicle, Projectile, TriggerZone.
            Allow(TrafficVehicle, TrafficVehicle);
            Allow(TrafficVehicle, Projectile);
            Allow(TrafficVehicle, TriggerZone);

            return pairs;
        }
    }
}
