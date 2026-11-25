using System.Collections.Generic;
using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Lightweight ambient Dhaka street life manager driving moving CNG auto-rickshaws,
    /// red-green Dhaka city buses, commuter motorcycles, sedans, and walking/idle pedestrians
    /// along boulevard lanes without blocking core courier pickup/patrol routes.
    /// </summary>
    public class DhakaTrafficAndPedestrians : MonoBehaviour
    {
        [System.Serializable]
        public class VehicleAgent
        {
            public Transform root;
            public Vector3[] waypoints;
            public int nextIdx;
            public float speed;
            public Transform[] wheels;
        }

        [System.Serializable]
        public class PedestrianAgent
        {
            public Transform root;
            public Transform leftLeg;
            public Transform rightLeg;
            public Transform leftArm;
            public Transform rightArm;
            public Vector3[] waypoints;
            public int nextIdx;
            public float walkSpeed;
            public float phase;
            public bool stationaryIdle;
        }

        [SerializeField] List<VehicleAgent> vehicles = new();
        [SerializeField] List<PedestrianAgent> pedestrians = new();

        public void RegisterVehicle(Transform root, Vector3[] waypoints, float speed, Transform[] wheels)
        {
            if (root == null || waypoints == null || waypoints.Length < 2) return;
            vehicles.Add(new VehicleAgent
            {
                root = root,
                waypoints = waypoints,
                nextIdx = 1 % waypoints.Length,
                speed = speed,
                wheels = wheels
            });
        }

        public void RegisterPedestrian(Transform root, Transform leftLeg, Transform rightLeg, Transform leftArm, Transform rightArm, Vector3[] waypoints, float speed, bool idleOnly = false)
        {
            if (root == null) return;
            pedestrians.Add(new PedestrianAgent
            {
                root = root,
                leftLeg = leftLeg,
                rightLeg = rightLeg,
                leftArm = leftArm,
                rightArm = rightArm,
                waypoints = waypoints,
                nextIdx = waypoints != null && waypoints.Length > 1 ? 1 : 0,
                walkSpeed = speed,
                phase = Random.Range(0f, Mathf.PI * 2f),
                stationaryIdle = idleOnly
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            GameLoop loop = GameLoop.Instance;
            if (loop != null && loop.CurrentState == GameLoop.PlayState.Paused) return;

            UpdateVehicles(dt);
            UpdatePedestrians(dt);
        }

        void UpdateVehicles(float dt)
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v.root == null || v.waypoints == null || v.waypoints.Length < 2) continue;

                Vector3 target = v.waypoints[v.nextIdx];
                Vector3 toTarget = target - v.root.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist < 0.9f)
                {
                    v.nextIdx = (v.nextIdx + 1) % v.waypoints.Length;
                    target = v.waypoints[v.nextIdx];
                    toTarget = target - v.root.position;
                    toTarget.y = 0f;
                    dist = toTarget.magnitude;
                }

                if (dist > 0.05f)
                {
                    Vector3 dir = toTarget / dist;
                    Quaternion goalRot = Quaternion.LookRotation(dir, Vector3.up);
                    v.root.rotation = Quaternion.Slerp(v.root.rotation, goalRot, 1f - Mathf.Exp(-6.5f * dt));
                    v.root.position += dir * v.speed * dt;
                }

                if (v.wheels != null)
                {
                    float wheelSpin = (v.speed / 0.34f) * Mathf.Rad2Deg * dt;
                    for (int w = 0; w < v.wheels.Length; w++)
                    {
                        if (v.wheels[w] != null) v.wheels[w].Rotate(wheelSpin, 0f, 0f, Space.Self);
                    }
                }
            }
        }

        void UpdatePedestrians(float dt)
        {
            for (int i = 0; i < pedestrians.Count; i++)
            {
                var p = pedestrians[i];
                if (p.root == null) continue;

                if (p.stationaryIdle || p.waypoints == null || p.waypoints.Length < 2)
                {
                    p.phase += dt * 2.2f;
                    float sway = Mathf.Sin(p.phase) * 3f;
                    if (p.leftArm != null) p.leftArm.localRotation = Quaternion.Euler(sway, 0f, -6f);
                    if (p.rightArm != null) p.rightArm.localRotation = Quaternion.Euler(-sway, 0f, 6f);
                    continue;
                }

                Vector3 target = p.waypoints[p.nextIdx];
                Vector3 toTarget = target - p.root.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist < 0.45f)
                {
                    p.nextIdx = (p.nextIdx + 1) % p.waypoints.Length;
                    target = p.waypoints[p.nextIdx];
                    toTarget = target - p.root.position;
                    toTarget.y = 0f;
                    dist = toTarget.magnitude;
                }

                if (dist > 0.04f)
                {
                    Vector3 dir = toTarget / dist;
                    p.root.rotation = Quaternion.Slerp(p.root.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-7f * dt));
                    p.root.position += dir * p.walkSpeed * dt;
                }

                p.phase += dt * (p.walkSpeed * 4.4f);
                float legSwing = Mathf.Sin(p.phase) * 26f;
                if (p.leftLeg != null) p.leftLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
                if (p.rightLeg != null) p.rightLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
                if (p.leftArm != null) p.leftArm.localRotation = Quaternion.Euler(-legSwing * 0.65f, 0f, -6f);
                if (p.rightArm != null) p.rightArm.localRotation = Quaternion.Euler(legSwing * 0.65f, 0f, 6f);
            }
        }
    }
}
