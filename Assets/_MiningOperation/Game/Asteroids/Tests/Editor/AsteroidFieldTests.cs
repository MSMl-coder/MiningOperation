using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public class AsteroidFieldTests
    {
        private AsteroidGeneratorSettings asteroidSettings;
        private AsteroidResourceCatalog catalog;
        private AsteroidFieldSettings fieldSettings;

        [SetUp]
        public void Setup()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            asteroidSettings = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            asteroidSettings.catalog = catalog;
            fieldSettings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            fieldSettings.asteroidSettings = asteroidSettings;
            fieldSettings.asteroidCount = 55;
            fieldSettings.radiusMeters = 12000f;
            fieldSettings.thicknessMeters = 800f;
            fieldSettings.minimumSurfaceGapMeters = 80f;
        }

        [TearDown]
        public void Teardown()
        {
            UnityEngine.Object.DestroyImmediate(fieldSettings);
            UnityEngine.Object.DestroyImmediate(asteroidSettings);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [TestCase(AsteroidFormationType.CrescentBelt)]
        [TestCase(AsteroidFormationType.SphericalCloud)]
        [TestCase(AsteroidFormationType.ClusterGroups)]
        [TestCase(AsteroidFormationType.BrokenBelt)]
        [TestCase(AsteroidFormationType.Stream)]
        [TestCase(AsteroidFormationType.BrokenRing)]
        public void PlanningIsReproducible(AsteroidFormationType type)
        {
            fieldSettings.formation = type;
            var a = AsteroidFieldLayout.Plan(fieldSettings, 7192L, "sector-alpha");
            var b = AsteroidFieldLayout.Plan(fieldSettings, 7192L, "sector-alpha");
            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(a.Count, 0);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].asteroidId, b[i].asteroidId);
                Assert.AreEqual(a[i].asteroidSeed, b[i].asteroidSeed);
                Assert.AreEqual(a[i].localPosition, b[i].localPosition);
                Assert.AreEqual(a[i].localRotation, b[i].localRotation);
                Assert.AreEqual(a[i].nominalDiameter, b[i].nominalDiameter);
            }
        }

        [Test]
        public void SameSeedDifferentFieldIdCannotShareAsteroidIds()
        {
            var a = AsteroidFieldLayout.Plan(fieldSettings, 7L, "alpha");
            var b = AsteroidFieldLayout.Plan(fieldSettings, 7L, "beta");
            Assert.AreNotEqual(a[0].asteroidId, b[0].asteroidId);
        }

        [Test]
        public void NoNominalOverlapWithConfiguredGap()
        {
            foreach (AsteroidFormationType type in Enum.GetValues(typeof(AsteroidFormationType)))
            {
                fieldSettings.formation = type;
                var plan = AsteroidFieldLayout.Plan(fieldSettings, 99L, "test-field");
                for (int i = 0; i < plan.Count; i++)
                for (int j = i + 1; j < plan.Count; j++)
                {
                    float safe = 0.95f * (plan[i].nominalDiameter + plan[j].nominalDiameter) + fieldSettings.minimumSurfaceGapMeters;
                    Assert.GreaterOrEqual(Vector3.Distance(plan[i].localPosition, plan[j].localPosition) + 0.02f, safe);
                }
            }
        }

        [Test]
        public void FieldSaveRoundTripPreservesSpatialAndResourceState()
        {
            var layout = AsteroidFieldLayout.Plan(fieldSettings, 1337L, "persistence-field");
            AsteroidFieldPlacement p = layout[0];
            var rock = AsteroidGeneration.Create(p.asteroidSeed, asteroidSettings, null, p.asteroidId);
            var save = AsteroidFieldSaveUtility.CopyAsteroid(rock);
            save.remainingMassKg *= 0.25;
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "persistence-field", fieldSeed = 1337L,
                formation = fieldSettings.formation,
                asteroids = new List<AsteroidFieldRecord>
                {
                    new AsteroidFieldRecord {index = p.index, localPosition = p.localPosition,
                        localRotation = p.localRotation, asteroid = save}
                }
            };
            string json = AsteroidFieldSaveUtility.ToJson(snapshot);
            var restored = AsteroidFieldSaveUtility.FromJson(json);
            Assert.AreEqual(snapshot.fieldId, restored.fieldId);
            Assert.AreEqual(p.asteroidId, restored.asteroids[0].asteroid.asteroidId);
            Assert.AreEqual(p.localPosition, restored.asteroids[0].localPosition);
            Assert.AreEqual(save.remainingMassKg, restored.asteroids[0].asteroid.remainingMassKg, 0.1);
            Assert.AreEqual(save.composition.Count, restored.asteroids[0].asteroid.composition.Count);
        }

        [Test]
        public void BackgroundExtractionDoesNotRequireVisibleMesh()
        {
            var placement = AsteroidFieldLayout.Plan(fieldSettings, 88L, "remote-field")[0];
            var rock = AsteroidGeneration.Create(placement.asteroidSeed, asteroidSettings, null, placement.asteroidId);
            var record = AsteroidFieldSaveUtility.CopyAsteroid(rock);
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "remote-field", fieldSeed = 88L,
                asteroids = new List<AsteroidFieldRecord>
                {
                    new AsteroidFieldRecord { index = placement.index,
                        localPosition = placement.localPosition,
                        localRotation = placement.localRotation, asteroid = record }
                }
            };
            var go = new GameObject("Test Field");
            try
            {
                var controller = go.AddComponent<AsteroidFieldController>();
                controller.LoadJson(AsteroidFieldSaveUtility.ToJson(snapshot));
                Assert.AreEqual(0, controller.ActiveCount);
                var extracted = controller.ExtractResourcesById(placement.asteroidId, 1000.0);
                double total = 0;
                foreach (var value in extracted.Values) total += value;
                Assert.AreEqual(1000.0, total, 1e-5);
                Assert.AreEqual(record.remainingMassKg - 1000.0,
                    controller.FindRecordById(placement.asteroidId).asteroid.remainingMassKg, 1e-4);
                var roundTrip = AsteroidFieldSaveUtility.FromJson(controller.CaptureJson());
                Assert.AreEqual(record.remainingMassKg - 1000.0,
                    roundTrip.asteroids[0].asteroid.remainingMassKg, 1e-4);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TamperedDuplicateIdIsRejected()
        {
            var p = AsteroidFieldLayout.Plan(fieldSettings, 11L, "tamper-field")[0];
            var rock = AsteroidGeneration.Create(p.asteroidSeed, asteroidSettings, null, p.asteroidId);
            var sample = AsteroidFieldSaveUtility.CopyAsteroid(rock);
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "tamper-field", asteroids = new List<AsteroidFieldRecord>
                {
                    new AsteroidFieldRecord {index=0, asteroid=sample, localRotation=Quaternion.identity},
                    new AsteroidFieldRecord {index=1, asteroid=sample, localRotation=Quaternion.identity}
                }
            };
            Assert.Throws<InvalidOperationException>(() => AsteroidFieldSaveUtility.ToJson(snapshot));
        }
    }
}
