using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public class MOV23FieldTests
    {
        private GameObject go;
        private AsteroidFieldSpawnPoint point;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("Gas Visual Test");
            point = go.AddComponent<AsteroidFieldSpawnPoint>();
            point.fieldId = "TEST-GAS";
            point.fieldSeed = 12345;
            point.contentType = MiningFieldContent.Gas;
            point.gasCloudCount = 6;
            point.gasPuffsPerCloud = 48;
            point.gasCloudRadiusMeters = 350;
            point.radiusMeters = 2400;
            point.SyncToController();
        }

        [TearDown]
        public void Teardown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void GasPlanIsStableForSameSeed()
        {
            var a = MOGasFieldLayout.Plan(point);
            var b = MOGasFieldLayout.Plan(point);
            Assert.AreEqual(6, a.clouds.Count);
            Assert.AreEqual(2, a.schemaVersion);
            for (int i = 0; i < a.clouds.Count; i++)
            {
                Assert.AreEqual(a.clouds[i].seed, b.clouds[i].seed);
                Assert.AreEqual(a.clouds[i].id, b.clouds[i].id);
                Assert.AreEqual(a.clouds[i].localCenter, b.clouds[i].localCenter);
                Assert.AreEqual(a.clouds[i].radiusMeters, b.clouds[i].radiusMeters);
            }
            MOGasFieldLayout.Validate(a);
        }

        [Test]
        public void DifferentSeedChangesCloudCenters()
        {
            var before = MOGasFieldLayout.Plan(point);
            point.fieldSeed = 808080;
            var after = MOGasFieldLayout.Plan(point);
            Assert.AreNotEqual(before.clouds[0].localCenter, after.clouds[0].localCenter);
        }

        [Test]
        public void GasVisualJsonHasNoMiningInventory()
        {
            var gas = go.AddComponent<MOGasFieldRenderer>();
            gas.BuildRecords();
            string json = gas.CaptureJson();
            Assert.IsFalse(json.Contains("initialMassKg"));
            Assert.IsFalse(json.Contains("remainingMassKg"));
            Assert.IsFalse(json.Contains("gasResourceId"));
            gas.LoadJson(json);
            Assert.AreEqual(6, gas.CloudCount);
        }

        [Test]
        public void IceSnapshotSerializesWithoutChangingLegacySchema()
        {
            var s = new AsteroidFieldSnapshot
            {
                schemaVersion = AsteroidFieldSaveUtility.SchemaVersion,
                layoutVersion = AsteroidFieldLayout.LayoutVersion,
                fieldId = "ICE-TEST",
                fieldSeed = 7L,
                contentType = MiningFieldContent.Ice,
                formation = AsteroidFormationType.ClusterGroups
            };
            var json = AsteroidFieldSaveUtility.ToJson(s);
            Assert.AreEqual(MiningFieldContent.Ice, AsteroidFieldSaveUtility.FromJson(json).contentType);
            Assert.AreEqual(AsteroidFieldSaveUtility.SchemaVersion, s.schemaVersion);
        }

        [Test]
        public void GasNeverStartsRockStreaming()
        {
            point.SyncToController();
            Assert.IsFalse(point.Controller.GenerateOnStart);
            Assert.IsFalse(point.Controller.RuntimeStreaming);
            Assert.IsFalse(point.Controller.enabled);
        }
    }
}
