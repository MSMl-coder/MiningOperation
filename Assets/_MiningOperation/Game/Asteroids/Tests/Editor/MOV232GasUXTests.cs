using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOV232GasUXTests
    {
        private GameObject host;
        private AsteroidFieldSpawnPoint point;

        [SetUp]
        public void Setup()
        {
            host = new GameObject("MO visual gas tests");
            point = host.AddComponent<AsteroidFieldSpawnPoint>();
            point.fieldId = "GAS-VISUAL-TEST";
            point.fieldSeed = 882211L;
            point.contentType = MiningFieldContent.Gas;
            point.radiusMeters = 2300f;
            point.thicknessMeters = 350f;
            point.gasCloudCount = 9;
            point.gasPuffsPerCloud = 48;
            point.gasCloudRadiusMeters = 520f;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void IdenticalInputsProduceIdenticalVisualRecordsAndSignature()
        {
            var a = MOGasFieldLayout.Plan(point);
            var b = MOGasFieldLayout.Plan(point);
            Assert.AreEqual(a.authoringSignature, b.authoringSignature);
            for (int i = 0; i < a.clouds.Count; i++)
            {
                Assert.AreEqual(a.clouds[i].localCenter, b.clouds[i].localCenter);
                Assert.AreEqual(a.clouds[i].visualDensity01, b.clouds[i].visualDensity01);
            }
        }

        [Test]
        public void VisualOnlySettingsDoNotInvalidateLayout()
        {
            string signature = MOGasFieldLayout.BuildSignature(point);
            point.gasPuffsPerCloud = 160;
            point.gasOpacity = 0.25f;
            point.gasColorA = Color.red;
            point.gasColorB = Color.yellow;
            point.gasDriftStrength = 0.9f;
            point.gasLayerContrast = 0.6f;
            Assert.AreEqual(signature, MOGasFieldLayout.BuildSignature(point));
            point.gasCloudRadiusMeters += 100f;
            Assert.AreNotEqual(signature, MOGasFieldLayout.BuildSignature(point));
        }

        [Test]
        public void LegacyGasMiningJsonLoadsButDoesNotCarryResourcesForward()
        {
            var map = MOGasFieldLayout.Plan(point);
            string json = JsonUtility.ToJson(map);
            json = json.Replace("\"schemaVersion\":2", "\"schemaVersion\":1");
            json = json.Replace("\"clouds\":", "\"gasResourceId\":\"volatile_gases\",\"clouds\":");
            json = json.Replace("\"radiusMeters\":", "\"remainingMassKg\":5000,\"initialMassKg\":10000,\"radiusMeters\":");
            var converted = MOGasFieldLayout.FromJson(json);
            Assert.AreEqual(2, converted.schemaVersion);
            Assert.AreEqual(string.Empty, converted.authoringSignature);
            string exported = JsonUtility.ToJson(converted);
            Assert.IsFalse(exported.Contains("MassKg"));
            Assert.IsFalse(exported.Contains("gasResourceId"));
            MOGasFieldLayout.Validate(converted);
        }

        [Test]
        public void GasLayoutHasNoExtractionMethod()
        {
            Assert.IsNull(typeof(MOGasFieldRenderer).GetMethod("ExtractGas"));
        }

        [Test]
        public void SwitchingToRockHidesGasVisualsWithoutDeletingThem()
        {
            var gasRenderer = host.AddComponent<MOGasFieldRenderer>();
            var visuals = new GameObject("MO Gas Visuals");
            visuals.transform.SetParent(host.transform, false);
            gasRenderer.SyncVisualVisibility();
            Assert.IsTrue(visuals.activeSelf);
            point.contentType = MiningFieldContent.Rock;
            gasRenderer.SyncVisualVisibility();
            Assert.IsFalse(visuals.activeSelf);
            point.contentType = MiningFieldContent.Gas;
            gasRenderer.SyncVisualVisibility();
            Assert.IsTrue(visuals.activeSelf);
        }
    }
}
