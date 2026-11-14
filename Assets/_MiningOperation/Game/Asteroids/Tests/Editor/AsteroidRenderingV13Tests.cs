using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidRenderingV13Tests
    {
        private AsteroidResourceCatalog catalog;
        private AsteroidGeneratorSettings generator;
        private AsteroidFieldSettings settings;

        [SetUp]
        public void Init()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            generator.volumeSubdivisions = 2;
            generator.lod0Subdivisions = 2;
            generator.lod1Subdivisions = 1;
            generator.lod2Subdivisions = 0;
            settings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            settings.asteroidSettings = generator;
            settings.asteroidCount = 8;
            settings.radiusMeters = 2800f;
            settings.thicknessMeters = 320f;
        }
        [TearDown]
        public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(generator);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
        [Test]
        public void NewMeshesHaveDeterministicGeologicalVertexColors()
        {
            AsteroidData data = AsteroidGeneration.Create(789, generator, AsteroidClass.III);
            Mesh a = null, b = null;
            try
            {
                a = AsteroidGeometryGenerator.Generate(data, generator, 2);
                b = AsteroidGeometryGenerator.Generate(data, generator, 2);
                Assert.AreEqual(a.vertexCount, a.colors32.Length);
                Assert.AreEqual(b.vertexCount, b.colors32.Length);
                for (int i = 0; i < a.vertexCount; i++)
                    Assert.AreEqual(a.colors32[i], b.colors32[i]);
            }
            finally
            {
                if (a != null) UnityEngine.Object.DestroyImmediate(a);
                if (b != null) UnityEngine.Object.DestroyImmediate(b);
            }
        }
        [Test]
        public void OptimizedModeKeepsAllRecordsWithoutSpawningDistantObjects()
        {
            var go = new GameObject("Test far-only field");
            try
            {
                var controller = go.AddComponent<AsteroidFieldController>();
                controller.Settings = settings;
                controller.FieldId = "far-only-test";
                controller.BeginBuild();
                while (!controller.BuildStep(2)) { }
                int expected = controller.Snapshot.asteroids.Count;
                Assert.Greater(expected, 0);
                controller.StreamAround(new Vector3(999999f, 999999f, 999999f), 100);
                Assert.AreEqual(0, controller.ActiveCount);
                Assert.AreEqual(0, go.transform.childCount);
                Assert.AreEqual(expected, controller.Snapshot.asteroids.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void OptimizedModeDoesNotModifyPersistentAsteroidValues()
        {
            settings.useGpuFarField = true;
            var plan = AsteroidFieldLayout.Plan(settings, 2L, "stable-field");
            var place = plan[0];
            var rock = AsteroidGeneration.Create(place.asteroidSeed, generator, null, place.asteroidId);
            var record = AsteroidFieldSaveUtility.CopyAsteroid(rock);
            record.remainingMassKg *= 0.37;
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "stable-field", fieldSeed = 2L,
                asteroids = new List<AsteroidFieldRecord> {
                    new AsteroidFieldRecord { index = place.index, localPosition = place.localPosition,
                        localRotation = place.localRotation, asteroid = record }
                }
            };
            string oldJson = AsteroidFieldSaveUtility.ToJson(snapshot);
            var restored = AsteroidFieldSaveUtility.FromJson(oldJson);
            Assert.AreEqual(record.asteroidId, restored.asteroids[0].asteroid.asteroidId);
            Assert.AreEqual(record.remainingMassKg, restored.asteroids[0].asteroid.remainingMassKg, 1e-4);
            Assert.AreEqual(record.geometryProfile.shapeIrregularity,
                restored.asteroids[0].asteroid.geometryProfile.shapeIrregularity);
        }
    }
}
