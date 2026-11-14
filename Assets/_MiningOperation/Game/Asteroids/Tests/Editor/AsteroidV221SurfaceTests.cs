using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidV221SurfaceTests
    {
        private AsteroidGeneratorSettings settings;
        private AsteroidResourceCatalog catalog;

        [SetUp]
        public void Setup()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            settings = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            settings.catalog = catalog;
            settings.volumeSubdivisions = 2;
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void VisualUVsAreSingleWrapWithShortTriangleSeams()
        {
            AsteroidData data = AsteroidGeneration.Create(202211, settings);
            Mesh mesh = AsteroidGeometryGenerator.Generate(data, settings, 3);
            try
            {
                Vector2[] uv = mesh.uv;
                Assert.AreEqual(mesh.vertexCount, uv.Length);
                foreach (Vector2 p in uv)
                {
                    Assert.That(p.x, Is.InRange(0f, 2f));
                    Assert.That(p.y, Is.InRange(0f, 1f));
                }
                int[] tri = mesh.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    float a = uv[tri[i]].x, b = uv[tri[i + 1]].x, c = uv[tri[i + 2]].x;
                    Assert.Less(Mathf.Max(a, Mathf.Max(b,c)) - Mathf.Min(a, Mathf.Min(b,c)), .6f,
                        "UV seam must not interpolate across the whole texture");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void OldVersionFourShapeIsIndependentOfNewVersionFive()
        {
            AsteroidData data = AsteroidGeneration.Create(80523, settings);
            Assert.AreEqual(5, data.generatorVersion);
            data.generatorVersion = 4;
            Mesh oldA = AsteroidGeometryGenerator.Generate(data, settings, 2);
            Mesh oldB = AsteroidGeometryGenerator.Generate(data, settings, 2);
            try
            {
                Assert.AreEqual(oldA.vertexCount, oldB.vertexCount);
                Assert.That(Vector3.Distance(oldA.vertices[0], oldB.vertices[0]), Is.LessThan(.00001f));
            }
            finally { Object.DestroyImmediate(oldA); Object.DestroyImmediate(oldB); }
        }

        [Test]
        public void RotationMultiplierChangesAngularSpeedButNotSeedDeterminism()
        {
            Quaternion start = Quaternion.identity;
            Quaternion a = AsteroidSpin.Evaluate(start, 10001, 70f, 8d, 2f);
            Quaternion b = AsteroidSpin.Evaluate(start, 10001, 70f, 8d, 2f);
            Quaternion slow = AsteroidSpin.Evaluate(start, 10001, 70f, 8d, 1f);
            Assert.Less(Quaternion.Angle(a, b), .00001f);
            Assert.Greater(Quaternion.Angle(start, a), Quaternion.Angle(start, slow));
        }
    }
}
