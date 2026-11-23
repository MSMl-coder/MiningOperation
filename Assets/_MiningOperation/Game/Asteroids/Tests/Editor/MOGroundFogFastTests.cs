using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOGroundFogFastTests
    {
        private GameObject owner;
        private AsteroidFieldSpawnPoint spawn;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Fast fog test");
            spawn = owner.AddComponent<AsteroidFieldSpawnPoint>();
            spawn.fieldId = "FAST-FOG-TEST";
            spawn.fieldSeed = 77013;
            spawn.contentType = MiningFieldContent.Gas;
            spawn.gasCloudCount = 2;
            spawn.gasCloudRadiusMeters = 650f;
            owner.transform.position = new Vector3(112f, -3371f, 550f);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) Object.DestroyImmediate(owner);
        }

        [Test]
        public void GroundFogQualityDefaultsToLowCostSurface()
        {
            Assert.AreEqual(MOGroundFogQuality.FastSurface, spawn.groundFogQuality);
        }

        [Test]
        public void FastSurfaceIsOneMeshAtAbsoluteWorldHeightWithoutChangingSnapshot()
        {
            var snapshot = MOGasFieldLayout.Plan(spawn);
            string original = JsonUtility.ToJson(snapshot);
            Mesh mesh = MOGroundFogSurfaceMeshBuilder.Build(snapshot, owner.transform,
                0f, 120f, 1.5f, resolution: 32);
            try
            {
                Assert.AreEqual(33 * 33, mesh.vertexCount);
                Assert.AreEqual(32 * 32 * 6, mesh.triangles.Length);
                Assert.AreEqual(1, mesh.subMeshCount);
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;
                bool visible = false;
                foreach (Vector3 v in mesh.vertices)
                {
                    float y = owner.transform.TransformPoint(v).y;
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
                foreach (Color c in mesh.colors)
                    visible |= c.a > .005f;
                Assert.That(min, Is.GreaterThanOrEqualTo(-.005f));
                Assert.That(max, Is.LessThanOrEqualTo(120f * 1.2f + .005f));
                Assert.IsTrue(visible);
                Assert.AreEqual(original, JsonUtility.ToJson(snapshot));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void FastSurfaceGeometryIsDeterministic()
        {
            var snapshot = MOGasFieldLayout.Plan(spawn);
            Mesh a = MOGroundFogSurfaceMeshBuilder.Build(snapshot, owner.transform, 0, 100, 1.5f, 24);
            Mesh b = MOGroundFogSurfaceMeshBuilder.Build(snapshot, owner.transform, 0, 100, 1.5f, 24);
            try
            {
                Assert.AreEqual(a.vertexCount, b.vertexCount);
                var va = a.vertices;
                var vb = b.vertices;
                for (int i = 0; i < va.Length; i++) Assert.AreEqual(va[i], vb[i]);
                int[] triangles = a.triangles;
                Vector3 normal = Vector3.Cross(va[triangles[1]] - va[triangles[0]],
                    va[triangles[2]] - va[triangles[0]]);
                Assert.Greater(normal.y, 0, "Top-facing winding should survive URP backface culling.");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
