using System.Collections.Generic;
using DhakaGta.Data;
using DhakaGta.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class VehicleSystemEditTests
    {
        [Test]
        public void AllElevenVehicleClasses_FactoryAssemblesLandAndWatercraftCleanly()
        {
            var classes = new[]
            {
                (DhakaVehicleClass.CngAutoRickshaw, "cng_auto", 3, 3, false),
                (DhakaVehicleClass.CycleRickshaw, "cycle_rickshaw", 3, 3, false),
                (DhakaVehicleClass.Motorcycle, "motorcycle", 2, 2, false),
                (DhakaVehicleClass.CityBus, "city_bus", 8, 4, false),
                (DhakaVehicleClass.IntercityBus, "intercity_bus", 8, 4, false),
                (DhakaVehicleClass.CompactSedan, "sedan", 4, 4, false),
                (DhakaVehicleClass.Suv, "suv", 4, 4, false),
                (DhakaVehicleClass.DmpPoliceCruiser, "police_cruiser", 4, 4, false),
                (DhakaVehicleClass.PickupTruck, "pickup", 4, 4, false),
                (DhakaVehicleClass.Speedboat, "speedboat", 4, 0, true),
                (DhakaVehicleClass.BurigangaLaunch, "buriganga_launch", 8, 0, true),
            };

            var root = new GameObject("EditTestVehiclesRoot");
            try
            {
                foreach (var (cls, id, seats, wheels, isWater) in classes)
                {
                    var spec = ScriptableObject.CreateInstance<VehicleSpecSO>();
                    spec.vehicleId = id;
                    spec.vehicleClass = cls;
                    spec.seatCount = seats;
                    spec.wheelCount = wheels;
                    spec.isWatercraft = isWater;
                    spec.maxHealth = 1000f;
                    spec.maxLeanAngleDeg = cls == DhakaVehicleClass.Motorcycle ? 26f : 0f;

                    GameObject instance = DhakaVehicleFactory.CreateVehicleFromSpec(spec, Vector3.zero, Quaternion.identity, root.transform);
                    Assert.NotNull(instance, $"Failed creating {id}");
                    Assert.NotNull(instance.GetComponent<IVehicleDrivable>());

                    var seatComp = instance.GetComponent<DhakaVehicleSeatsAndInteract>();
                    Assert.NotNull(seatComp);
                    Assert.AreEqual(seats, seatComp.SeatCount);

                    if (isWater)
                    {
                        Assert.NotNull(instance.GetComponent<DhakaWatercraftController>());
                    }
                    else
                    {
                        Assert.NotNull(instance.GetComponent<DhakaVehicleController>());
                        Assert.AreEqual(wheels, instance.GetComponentsInChildren<DhakaPhysxWheel>(true).Length);
                    }
                    Object.DestroyImmediate(spec);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VehicleTuningDamageAndGarage_SaveLoadAndUpgradesWork()
        {
            var host = new GameObject("TestGarageHost");
            var carObj = new GameObject("TestTuneCar");
            var spec = ScriptableObject.CreateInstance<VehicleSpecSO>();
            spec.vehicleId = "sedan";
            spec.maxHealth = 1000f;

            try
            {
                var dmgTuning = carObj.AddComponent<DhakaVehicleTuningAndDamage>();
                dmgTuning.Initialize(1000f, null, Color.red);
                dmgTuning.ApplyDamage(750f);
                Assert.IsTrue(dmgTuning.IsSmoking);
                Assert.IsFalse(dmgTuning.IsWrecked);
                dmgTuning.ApplyDamage(300f);
                Assert.IsTrue(dmgTuning.IsWrecked);
                dmgTuning.RepairFull();
                Assert.AreEqual(1000f, dmgTuning.CurrentHealth, 0.01f);

                dmgTuning.ApplyTuningUpgrade(3, TireCompoundType.MonsoonRainTread, 1.15f);
                Assert.Greater(dmgTuning.Tuning.TorqueMultiplier, 1.4f);
                Assert.Greater(dmgTuning.Tuning.WetGripBonus, 1.3f);

                var garage = host.AddComponent<DhakaGarageManager>();
                garage.ConfigureCatalog(new[] { spec }, host.transform);
                OwnedVehicleRecord rec = garage.GrantVehicleToGarage("sedan", Color.blue);
                Assert.IsTrue(rec.licensePlate.StartsWith("DHAKA-METRO"));
                garage.TuneAndRepaintOwnedVehicle(0, Color.green, 3, TireCompoundType.MonsoonRainTread);

                string json = garage.SerializeToJson();
                var garage2 = host.AddComponent<DhakaGarageManager>();
                garage2.DeserializeFromJson(json);
                Assert.AreEqual(1, garage2.OwnedFleet.Count);
                Assert.AreEqual(3, garage2.OwnedFleet[0].engineStage);
                Assert.AreEqual(TireCompoundType.MonsoonRainTread, garage2.OwnedFleet[0].tireCompound);
            }
            finally
            {
                Object.DestroyImmediate(carObj);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(spec);
            }
        }
    }
}
