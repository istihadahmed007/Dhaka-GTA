using System.Collections;
using DhakaGta.Core;
using DhakaGta.Graphics;
using DhakaGta.Vehicles;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class GraphicsCameraPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_FiveCameraModesCollisionBankingAndSpeedFov_AllOperateCleanly()
        {
            PhysicsLayers.ApplyCollisionMatrix();

            GameObject roadFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roadFloor.layer = PhysicsLayers.Ground;
            roadFloor.transform.position = new Vector3(0f, -0.5f, 0f);
            roadFloor.transform.localScale = new Vector3(120f, 1f, 300f);

            var carGo = new GameObject("PlayTestCar");
            carGo.layer = PhysicsLayers.Vehicle;
            carGo.transform.position = new Vector3(0f, 0.7f, 0f);
            carGo.AddComponent<Rigidbody>().mass = 1420f;
            var box = carGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.65f, 0f);
            box.size = new Vector3(1.8f, 0.9f, 4.2f);

            WheelCollider fl = MakeWheel(carGo.transform, new Vector3(-0.82f, 0.36f, 1.35f));
            WheelCollider fr = MakeWheel(carGo.transform, new Vector3(0.82f, 0.36f, 1.35f));
            WheelCollider rl = MakeWheel(carGo.transform, new Vector3(-0.82f, 0.36f, -1.35f));
            WheelCollider rr = MakeWheel(carGo.transform, new Vector3(0.82f, 0.36f, -1.35f));

            var vehicle = carGo.AddComponent<DhakaWheelVehicleController>();
            vehicle.ConfigureWheels(null, fl, fr, rl, rr);
            vehicle.SetSyntheticDriveInput(throttleBrake: 1.0f, steer: 0.4f);

            var camGo = new GameObject("PlayTestCamera");
            var cam = camGo.AddComponent<Camera>();
            var vcamGo = new GameObject("PlayTestCinemachineCam");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();

            var rig = camGo.AddComponent<DhakaCinemachineCameraRig>();
            rig.Configure(vcam, cam, vehicle, null, null, DhakaCameraMode.DrivingChase);

            GameObject wallOccluder = null;
            try
            {
                // Verify all 5 modes select cleanly and update pose
                foreach (DhakaCameraMode mode in new[]
                {
                    DhakaCameraMode.DrivingClose,
                    DhakaCameraMode.DrivingWideCinematic,
                    DhakaCameraMode.FirstPersonCockpit,
                    DhakaCameraMode.OnFootThirdPerson,
                    DhakaCameraMode.DrivingChase,
                })
                {
                    rig.SetCameraMode(mode);
                    rig.UpdateCameraTransform(0.05f);
                    Assert.AreEqual(mode, rig.CurrentMode);
                }

                // Drive forward with steering to build speed and cornering bank roll
                float baseFov = rig.CurrentFieldOfView;
                yield return new WaitForSeconds(0.85f);

                Assert.Greater(vehicle.ForwardSpeedKph, 10f);
                Assert.Greater(rig.CurrentFieldOfView, baseFov + 0.5f, "Speed-sensitive FOV should widen dynamically with car velocity.");
                Assert.Greater(Mathf.Abs(rig.CurrentBankRollDegrees), 0.01f, "Camera banking roll should react to cornering yaw rate.");

                // Place a Building collider directly behind the car and verify SphereCast occlusion pull-in
                wallOccluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallOccluder.layer = PhysicsLayers.Building;
                wallOccluder.transform.position = carGo.transform.position - carGo.transform.forward * 2.5f + Vector3.up * 2f;
                wallOccluder.transform.localScale = new Vector3(10f, 6f, 0.8f);
                Physics.SyncTransforms();

                rig.SetCameraMode(DhakaCameraMode.DrivingWideCinematic);
                rig.UpdateCameraTransform(0.05f);
                Assert.Less(rig.LastOcclusionFraction, 0.7f, "Camera collision avoidance must pull forward in front of Building collider.");
            }
            finally
            {
                if (wallOccluder != null) Object.Destroy(wallOccluder);
                Object.Destroy(carGo);
                Object.Destroy(vcamGo);
                Object.Destroy(camGo);
                Object.Destroy(roadFloor);
            }
        }

        private static WheelCollider MakeWheel(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Wheel");
            go.layer = PhysicsLayers.VehicleWheel;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = 0.36f;
            wc.suspensionDistance = 0.22f;
            return wc;
        }
    }
}
