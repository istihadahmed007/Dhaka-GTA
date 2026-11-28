using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Reusable runtime & Editor factory assembling complete multi-part Dhaka land vehicles (2/3/4/6 wheels)
    /// and marine watercraft (Speedboat/Launch) directly from VehicleSpecSO definitions.
    /// Avoids duplicating vehicle hierarchy assembly across Editor Prefabs, Scene Spawners, and PlayMode tests.
    /// </summary>
    public static class DhakaVehicleFactory
    {
        public sealed class VehicleVisualMaterials
        {
            public Material PaintBodyMat;
            public Material CabinGlassMat;
            public Material TrimDarkMat;
            public Material TireRubberMat;
            public Material AlloyRimMat;
            public Material BrakeLightMat;
            public Material ReverseLightMat;
        }

        public static GameObject CreateVehicleFromSpec(
            VehicleSpecSO spec,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            VehicleVisualMaterials mats = null,
            GameObject optionalImportedModel = null)
        {
            if (spec == null) return null;
            mats ??= CreateFallbackMaterials();

            var root = new GameObject($"Vehicle_{spec.vehicleId}");
            root.layer = PhysicsLayers.Vehicle;
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(90f, spec.massKg);
            rb.centerOfMass = new Vector3(0f, spec.centerOfMassYOffset, 0.08f);

            var chassisCol = root.AddComponent<BoxCollider>();
            Vector3 sz = spec.chassisSizeMeters;
            chassisCol.size = new Vector3(sz.x, sz.y * 0.72f, sz.z);
            chassisCol.center = new Vector3(0f, spec.wheelRadiusMeters + sz.y * 0.42f, 0f);

            var bodyRenderers = new List<Renderer>();
            Transform steeringWheelPivot = null;

            if (optionalImportedModel != null)
            {
                GameObject modelInst = Object.Instantiate(optionalImportedModel, root.transform, false);
                modelInst.name = "BlenderModel";
                bodyRenderers.AddRange(modelInst.GetComponentsInChildren<Renderer>(true));
            }
            else
            {
                BuildProceduralBody(root.transform, spec, mats, bodyRenderers, out steeringWheelPivot);
            }

            // Seats & Door Interact
            Transform seatsRoot = new GameObject("Seats").transform;
            seatsRoot.SetParent(root.transform, false);

            var driverSeat = new GameObject("DriverSeat_RHD").transform;
            driverSeat.SetParent(seatsRoot, false);
            driverSeat.localPosition = new Vector3(spec.isRightHandDrive ? sz.x * 0.22f : 0f, spec.wheelRadiusMeters + 0.45f, 0.15f);

            var cockpitEye = new GameObject("FirstPersonCockpitEye").transform;
            cockpitEye.SetParent(seatsRoot, false);
            cockpitEye.localPosition = spec.cockpitEyeLocalOffset;

            var doorAnchor = new GameObject("EnterExitDoorAnchor").transform;
            doorAnchor.SetParent(seatsRoot, false);
            doorAnchor.localPosition = new Vector3(spec.isRightHandDrive ? sz.x * 0.65f + 0.85f : -(sz.x * 0.65f + 0.85f), 0.15f, 0.2f);

            int pCount = Mathf.Max(0, spec.seatCount - 1);
            var passengers = new Transform[pCount];
            for (int i = 0; i < pCount; i++)
            {
                var p = new GameObject($"PassengerSeat_{i + 1}").transform;
                p.SetParent(seatsRoot, false);
                float px = (i % 2 == 0 ? -1f : 1f) * sz.x * 0.2f;
                float pz = i == 0 ? 0.15f : -0.65f - (i / 2) * 0.85f;
                p.localPosition = new Vector3(px, spec.wheelRadiusMeters + 0.45f, pz);
                passengers[i] = p;
            }

            var seatsComp = root.AddComponent<DhakaVehicleSeatsAndInteract>();
            seatsComp.ConfigureSeats(driverSeat, passengers, cockpitEye, doorAnchor);

            // Lights & Horn
            AttachLightsAndBeacons(root.transform, spec, mats, out var headlights, out var brakeRenderers, out var reverseRenderers, out var polRed, out var polBlue);
            var lightsComp = root.AddComponent<DhakaVehicleLightsAndHorn>();
            lightsComp.Configure(headlights, brakeRenderers, reverseRenderers, polRed, polBlue);

            // Tuning & Damage
            var damageTuning = root.AddComponent<DhakaVehicleTuningAndDamage>();
            Color initColor = (spec.typicalLiveries != null && spec.typicalLiveries.Length > 0) ? spec.typicalLiveries[0] : Color.white;
            damageTuning.Initialize(spec.maxHealth, bodyRenderers.ToArray(), initColor);

            // Audio
            var audioComp = root.AddComponent<DhakaVehicleAudioController>();
            audioComp.Initialize(spec);

            if (spec.isWatercraft)
            {
                AttachWatercraftRig(root, spec);
            }
            else
            {
                AttachLandWheelRig(root, spec, steeringWheelPivot, damageTuning, lightsComp, audioComp, mats);
            }

            return root;
        }

        private static void AttachWatercraftRig(GameObject root, VehicleSpecSO spec)
        {
            Vector3 sz = spec.chassisSizeMeters;
            float hx = sz.x * 0.42f;
            float hz = sz.z * 0.40f;
            Vector3[] offsets =
            {
                new Vector3(-hx, 0f, hz),
                new Vector3(hx, 0f, hz),
                new Vector3(-hx, 0f, -hz),
                new Vector3(hx, 0f, -hz),
            };
            var pontoons = new Transform[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var pt = new GameObject($"BuoyancyPontoon_{i}").transform;
                pt.SetParent(root.transform, false);
                pt.localPosition = offsets[i];
                pontoons[i] = pt;
            }

            var waterCtrl = root.AddComponent<DhakaWatercraftController>();
            waterCtrl.ConfigureWatercraft(spec, pontoons, -0.35f);
        }

        private static void AttachLandWheelRig(
            GameObject root,
            VehicleSpecSO spec,
            Transform steeringWheelPivot,
            DhakaVehicleTuningAndDamage dmg,
            DhakaVehicleLightsAndHorn lights,
            DhakaVehicleAudioController audio,
            VehicleVisualMaterials mats)
        {
            float halfW = spec.trackWidthMeters * 0.5f;
            float halfB = spec.wheelbaseMeters * 0.5f;
            float mountY = spec.wheelRadiusMeters + spec.suspensionTravelMeters * 0.85f;

            var wheelSpecs = new List<(string name, Vector3 pos, bool steer, bool drive, bool rear)>();
            if (spec.wheelCount == 2)
            {
                // Motorcycle
                wheelSpecs.Add(("Wheel_F", new Vector3(0f, mountY, halfB), true, false, false));
                wheelSpecs.Add(("Wheel_R", new Vector3(0f, mountY, -halfB), false, true, true));
            }
            else if (spec.wheelCount == 3)
            {
                // CNG Auto / Cycle Rickshaw
                wheelSpecs.Add(("Wheel_F", new Vector3(0f, mountY, halfB), true, false, false));
                wheelSpecs.Add(("Wheel_RL", new Vector3(-halfW, mountY, -halfB), false, true, true));
                wheelSpecs.Add(("Wheel_RR", new Vector3(halfW, mountY, -halfB), false, true, true));
            }
            else
            {
                bool fwd = spec.drivetrain == DrivetrainMode.FrontWheelDrive || spec.drivetrain == DrivetrainMode.AllWheelDrive;
                bool rwd = spec.drivetrain == DrivetrainMode.RearWheelDrive || spec.drivetrain == DrivetrainMode.AllWheelDrive;
                wheelSpecs.Add(("Wheel_FL", new Vector3(-halfW, mountY, halfB), true, fwd, false));
                wheelSpecs.Add(("Wheel_FR", new Vector3(halfW, mountY, halfB), true, fwd, false));
                wheelSpecs.Add(("Wheel_RL", new Vector3(-halfW, mountY, -halfB), false, rwd, true));
                wheelSpecs.Add(("Wheel_RR", new Vector3(halfW, mountY, -halfB), false, rwd, true));
            }

            var physxWheels = new DhakaPhysxWheel[wheelSpecs.Count];
            float tireWidth = spec.wheelCount <= 3 ? 0.14f : 0.25f;

            for (int i = 0; i < wheelSpecs.Count; i++)
            {
                var ws = wheelSpecs[i];
                var mountGo = new GameObject(ws.name);
                mountGo.layer = PhysicsLayers.VehicleWheel;
                mountGo.transform.SetParent(root.transform, false);
                mountGo.transform.localPosition = ws.pos;

                var vis = new GameObject("Visual").transform;
                vis.SetParent(mountGo.transform, false);

                var tireCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCollider(tireCyl);
                tireCyl.layer = PhysicsLayers.VehicleWheel;
                tireCyl.transform.SetParent(vis, false);
                tireCyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                tireCyl.transform.localScale = new Vector3(spec.wheelRadiusMeters * 2f, tireWidth * 0.5f, spec.wheelRadiusMeters * 2f);
                tireCyl.GetComponent<MeshRenderer>().sharedMaterial = mats.TireRubberMat;

                var pw = mountGo.AddComponent<DhakaPhysxWheel>();
                pw.Configure(ws.steer, ws.drive, ws.rear, spec.wheelRadiusMeters, spec.suspensionTravelMeters, vis);
                physxWheels[i] = pw;
            }

            var vehicleCtrl = root.AddComponent<DhakaVehicleController>();
            vehicleCtrl.Configure(spec, physxWheels, steeringWheelPivot, dmg, lights, audio);
        }

        private static void BuildProceduralBody(
            Transform root,
            VehicleSpecSO spec,
            VehicleVisualMaterials mats,
            List<Renderer> bodyRenderers,
            out Transform steeringWheelPivot)
        {
            steeringWheelPivot = null;
            Vector3 sz = spec.chassisSizeMeters;
            float baseH = spec.isWatercraft ? 0.35f : spec.wheelRadiusMeters + 0.15f;

            // Main sculpted lower tub
            GameObject tub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tub.name = "BodyTub";
            DestroyCollider(tub);
            tub.transform.SetParent(root, false);
            tub.transform.localPosition = new Vector3(0f, baseH + sz.y * 0.25f, 0f);
            tub.transform.localScale = new Vector3(sz.x, sz.y * 0.48f, sz.z);
            var tubRend = tub.GetComponent<MeshRenderer>();
            tubRend.sharedMaterial = mats.PaintBodyMat;
            bodyRenderers.Add(tubRend);

            // Cabin / Roof / Canopy distinct per Dhaka vehicle class
            if (spec.vehicleClass == DhakaVehicleClass.Motorcycle)
            {
                GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tank.name = "FuelTank";
                DestroyCollider(tank);
                tank.transform.SetParent(root, false);
                tank.transform.localPosition = new Vector3(0f, baseH + 0.38f, 0.25f);
                tank.transform.localScale = new Vector3(0.32f, 0.22f, 0.56f);
                tank.GetComponent<MeshRenderer>().sharedMaterial = mats.PaintBodyMat;
                bodyRenderers.Add(tank.GetComponent<MeshRenderer>());
                return;
            }

            float cabinLen = spec.vehicleClass == DhakaVehicleClass.PickupTruck ? sz.z * 0.36f :
                             (spec.vehicleClass == DhakaVehicleClass.CityBus || spec.vehicleClass == DhakaVehicleClass.IntercityBus || spec.vehicleClass == DhakaVehicleClass.BurigangaLaunch ? sz.z * 0.88f : sz.z * 0.54f);
            float cabinZ = spec.vehicleClass == DhakaVehicleClass.PickupTruck ? sz.z * 0.15f : -0.10f;
            float cabinH = sz.y * 0.44f;

            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "CabinGlasshouse";
            DestroyCollider(cabin);
            cabin.transform.SetParent(root, false);
            cabin.transform.localPosition = new Vector3(0f, baseH + sz.y * 0.48f + cabinH * 0.5f, cabinZ);
            cabin.transform.localScale = new Vector3(sz.x * 0.86f, cabinH, cabinLen);
            cabin.GetComponent<MeshRenderer>().sharedMaterial = mats.CabinGlassMat;

            // Steering wheel in RHD cabin
            var steerRoot = new GameObject("SteeringWheelPivot").transform;
            steerRoot.SetParent(root, false);
            steerRoot.localPosition = new Vector3(spec.isRightHandDrive ? sz.x * 0.22f : 0f, baseH + sz.y * 0.45f, 0.52f);
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(rim);
            rim.transform.SetParent(steerRoot, false);
            rim.transform.localScale = new Vector3(0.32f, 0.025f, 0.32f);
            rim.GetComponent<MeshRenderer>().sharedMaterial = mats.TrimDarkMat;
            steeringWheelPivot = steerRoot;
        }

        private static void AttachLightsAndBeacons(
            Transform root,
            VehicleSpecSO spec,
            VehicleVisualMaterials mats,
            out Light[] headlights,
            out Renderer[] brakeRenderers,
            out Renderer[] reverseRenderers,
            out Light polRed,
            out Light polBlue)
        {
            Vector3 sz = spec.chassisSizeMeters;
            float lightY = spec.wheelRadiusMeters + 0.42f;
            float frontZ = sz.z * 0.5f + 0.03f;
            float rearZ = -sz.z * 0.5f - 0.03f;
            float xOff = Mathf.Max(0.15f, sz.x * 0.34f);

            int hlCount = spec.vehicleClass == DhakaVehicleClass.Motorcycle || spec.vehicleClass == DhakaVehicleClass.CngAutoRickshaw || spec.vehicleClass == DhakaVehicleClass.CycleRickshaw ? 1 : 2;
            headlights = new Light[hlCount];
            for (int i = 0; i < hlCount; i++)
            {
                float lx = hlCount == 1 ? 0f : (i == 0 ? -xOff : xOff);
                var go = new GameObject($"Headlight_{i}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(lx, lightY, frontZ);
                go.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
                var spot = go.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.range = 60f;
                spot.spotAngle = 52f;
                spot.intensity = 1200f;
                spot.useColorTemperature = true;
                spot.colorTemperature = 5600f;
                spot.enabled = WorldSurfaceState.IsNightOrLowVisibility;
                headlights[i] = spot;
            }

            brakeRenderers = new Renderer[2];
            reverseRenderers = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float lx = i == 0 ? -xOff : xOff;
                var br = GameObject.CreatePrimitive(PrimitiveType.Cube);
                br.name = $"BrakeLight_{i}";
                DestroyCollider(br);
                br.transform.SetParent(root, false);
                br.transform.localPosition = new Vector3(lx, lightY + 0.06f, rearZ);
                br.transform.localScale = new Vector3(0.28f, 0.12f, 0.06f);
                br.GetComponent<MeshRenderer>().sharedMaterial = mats.BrakeLightMat;
                brakeRenderers[i] = br.GetComponent<MeshRenderer>();

                var rv = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rv.name = $"ReverseLight_{i}";
                DestroyCollider(rv);
                rv.transform.SetParent(root, false);
                rv.transform.localPosition = new Vector3(lx * 0.7f, lightY - 0.06f, rearZ);
                rv.transform.localScale = new Vector3(0.16f, 0.09f, 0.06f);
                rv.GetComponent<MeshRenderer>().sharedMaterial = mats.ReverseLightMat;
                reverseRenderers[i] = rv.GetComponent<MeshRenderer>();
            }

            polRed = null;
            polBlue = null;
            if (spec.vehicleClass == DhakaVehicleClass.DmpPoliceCruiser)
            {
                polRed = CreateBeacon(root, "PoliceBeacon_Red", new Vector3(-0.35f, spec.wheelRadiusMeters + sz.y + 0.18f, 0f), new Color(1f, 0.12f, 0.12f));
                polBlue = CreateBeacon(root, "PoliceBeacon_Blue", new Vector3(0.35f, spec.wheelRadiusMeters + sz.y + 0.18f, 0f), new Color(0.15f, 0.45f, 1f));
            }
        }

        private static Light CreateBeacon(Transform parent, string name, Vector3 localPos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 18f;
            l.intensity = 650f;
            l.color = color;
            l.enabled = false;
            return l;
        }

        private static void DestroyCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Object.DestroyImmediate(col); return; }
#endif
            Object.Destroy(col);
        }

        private static VehicleVisualMaterials CreateFallbackMaterials()
        {
            Shader s = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            Material Make(Color col)
            {
                var m = new Material(s);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                else m.color = col;
                return m;
            }
            return new VehicleVisualMaterials
            {
                PaintBodyMat = Make(new Color(0.75f, 0.14f, 0.14f)),
                CabinGlassMat = Make(new Color(0.12f, 0.24f, 0.30f)),
                TrimDarkMat = Make(new Color(0.12f, 0.12f, 0.14f)),
                TireRubberMat = Make(new Color(0.06f, 0.06f, 0.06f)),
                AlloyRimMat = Make(new Color(0.72f, 0.74f, 0.76f)),
                BrakeLightMat = Make(new Color(0.9f, 0.1f, 0.1f)),
                ReverseLightMat = Make(new Color(0.95f, 0.95f, 0.95f)),
            };
        }
    }
}
