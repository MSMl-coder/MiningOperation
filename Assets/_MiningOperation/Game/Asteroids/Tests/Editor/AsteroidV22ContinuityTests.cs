using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidV22ContinuityTests
    {
        private AsteroidGeneratorSettings generator;
        private AsteroidResourceCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            generator.volumeSubdivisions = 2;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(generator);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void NewGeometryVersionFiveIsDeterministicAcrossLods()
        {
            AsteroidData asteroid = AsteroidGeneration.Create(420420, generator);
            Assert.AreEqual(AsteroidGeneration.GeneratorVersion, asteroid.generatorVersion);
            Mesh low = AsteroidGeometryGenerator.Generate(asteroid, generator, 2);
            Mesh high = AsteroidGeometryGenerator.Generate(asteroid, generator, 3);
            try
            {
                Vector3[] a = low.vertices, b = high.vertices;
                Assert.Greater(b.Length, a.Length);
                for (int i = 0; i < 162; ++i)
                    Assert.Less((a[i] - b[i]).sqrMagnitude, 0.0000001f,
                        "Shared icosphere vertices must be identical between LODs");
                Assert.AreEqual(a.Length, low.uv.Length);
                Assert.Greater(AsteroidGeneration.CalculateVolume(high), 0d);
            }
            finally { Object.DestroyImmediate(low); Object.DestroyImmediate(high); }
        }

        [Test]
        public void SeededRotationIsFrameRateIndependent()
        {
            Quaternion start = Quaternion.Euler(17f, 32f, 8f);
            Quaternion a = AsteroidSpin.Evaluate(start, 77, 130f, 720.0);
            Quaternion b = AsteroidSpin.Evaluate(start, 77, 130f, 720.0);
            Quaternion c = AsteroidSpin.Evaluate(start, 77, 130f, 0.0);
            Assert.Less(Quaternion.Angle(a, b), 0.0001f);
            Assert.Greater(Quaternion.Angle(a, c), 0.001f);
        }

        [Test]
        public void SharedFarMeshIsUsedWithoutRegenerationOnNearSpawn()
        {
            AsteroidData asteroid = AsteroidGeneration.Create(8080, generator);
            var record = AsteroidFieldSaveUtility.CopyAsteroid(asteroid);
            Mesh borrowed = AsteroidGeometryGenerator.Generate(asteroid, generator, 2);
            borrowed.name = "MO_FarExact_TEST";
            borrowed.hideFlags = HideFlags.DontSave;
            var go = new GameObject("shared-mesh-near-test");
            try
            {
                var instance = go.AddComponent<AsteroidInstance>();
                instance.Settings = generator;
                instance.AddMeshCollider = false;
                instance.RestoreStreamedShared(record, 2, borrowed);
                Assert.AreSame(borrowed, go.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(2, instance.StreamedSubdivisionLevel);
                Assert.IsTrue(instance.UpgradeStreamedGeometry(3));
                Assert.AreEqual(3, instance.StreamedSubdivisionLevel);
                Assert.AreNotSame(borrowed, go.GetComponent<MeshFilter>().sharedMesh);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(borrowed);
            }
        }

        [Test]
        public void RotationClockSurvivesFieldJsonRoundTrip()
        {
            AsteroidData asteroid = AsteroidGeneration.Create(1221, generator);
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "v22-field", fieldSeed = 11, simulationSeconds = 1470.25d,
                asteroids = new System.Collections.Generic.List<AsteroidFieldRecord>
                {
                    new AsteroidFieldRecord
                    {
                        index = 0, asteroid = AsteroidFieldSaveUtility.CopyAsteroid(asteroid),
                        localRotation = Quaternion.identity
                    }
                }
            };
            var loaded = AsteroidFieldSaveUtility.FromJson(AsteroidFieldSaveUtility.ToJson(snapshot));
            Assert.AreEqual(snapshot.simulationSeconds, loaded.simulationSeconds, 0.00001d);
            Assert.AreEqual(asteroid.asteroidId, loaded.asteroids[0].asteroid.asteroidId);
        }
    }
}
