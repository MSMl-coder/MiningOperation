using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOGroundFogTests
    {
        private GameObject host;
        private AsteroidFieldSpawnPoint point;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Ground fog test");
            point = host.AddComponent<AsteroidFieldSpawnPoint>();
            point.fieldId = "GROUND-TEST";
            point.fieldSeed = 45117;
            point.contentType = MiningFieldContent.Gas;
            point.gasCloudCount = 3;
            point.gasCloudRadiusMeters = 500;
            host.transform.position = new Vector3(123, -3371, 345);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void GroundFogModePreservesExistingGasLayoutSignature()
        {
            string before = MOGasFieldLayout.BuildSignature(point);
            point.gasPresentation = MOGasPresentation.GroundFog;
            point.groundFogFloorY = 0;
            point.groundFogHeightMeters = 120;
            point.groundFogRadiusMultiplier = 2;
            Assert.AreEqual(before, MOGasFieldLayout.BuildSignature(point));
            Assert.AreEqual(2, (int)MOGasPresentation.GroundFog);
        }

        [Test]
        public void FlattenedMeshStaysAtWorldHeightWhenSpawnIsFarBelowZero()
        {
            var snapshot = MOGasFieldLayout.Plan(point);
            Mesh mesh = MOGroundFogMeshBuilder.Build(snapshot, host.transform, 0, 120, 2);
            try
            {
                var verts = mesh.vertices;
                float minY = float.PositiveInfinity;
                float maxY = float.NegativeInfinity;
                foreach (var v in verts)
                {
                    float worldY = host.transform.TransformPoint(v).y;
                    minY = Mathf.Min(minY, worldY);
                    maxY = Mathf.Max(maxY, worldY);
                }
                Assert.That(minY, Is.EqualTo(0).Within(.01f));
                Assert.That(maxY, Is.EqualTo(120).Within(.01f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void GroundFogDoesNotModifyGasSnapshotRecords()
        {
            var data = MOGasFieldLayout.Plan(point);
            string before = JsonUtility.ToJson(data);
            Mesh mesh = MOGroundFogMeshBuilder.Build(data, host.transform, 0, 90, 1.2f);
            Object.DestroyImmediate(mesh);
            Assert.AreEqual(before, JsonUtility.ToJson(data));
        }
        [Test]
        public void OneSavedClusterCanGenerateManyDifferentCloudlets()
        {
            var data = MOGasFieldLayout.Plan(point);
            var cloud = data.clouds[0];
            data.clouds.Clear();
            data.clouds.Add(cloud);
            string original = JsonUtility.ToJson(data);
            const int cloudlets = 12;
            Mesh mesh = MOGroundFogMeshBuilder.Build(data, host.transform, 0, 120, 1.5f,
                cloudletsPerCluster: cloudlets, heightVariation: .8f, cloudletSpread: .85f);
            try
            {
                Assert.AreEqual(cloudlets * 21 * 13, mesh.vertexCount);
                var attributes = new System.Collections.Generic.List<Vector4>();
                mesh.GetUVs(1, attributes);
                Assert.AreEqual(mesh.vertexCount, attributes.Count);
                var heights = new System.Collections.Generic.HashSet<int>();
                // One attribute block per subcloud; each cloudlet has different half-height.
                for (int i = 0; i < cloudlets; i++)
                    heights.Add(Mathf.RoundToInt(attributes[i * 21 * 13].w * 100f));
                Assert.Greater(heights.Count, 4, "Cloudlet tops should not form a uniform flat ceiling.");
                Assert.AreEqual(original, JsonUtility.ToJson(data),
                    "Procedural visual subdivisions must not mutate saved gas records.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CloudletMeshIsDeterministicForTheSameSavedGasRecords()
        {
            var data = MOGasFieldLayout.Plan(point);
            Mesh a = MOGroundFogMeshBuilder.Build(data, host.transform, 0, 90, 1f,
                cloudletsPerCluster: 9, heightVariation: .75f);
            Mesh b = MOGroundFogMeshBuilder.Build(data, host.transform, 0, 90, 1f,
                cloudletsPerCluster: 9, heightVariation: .75f);
            try
            {
                var av = a.vertices;
                var bv = b.vertices;
                Assert.AreEqual(av.Length, bv.Length);
                for (int i = 0; i < av.Length; i++)
                    Assert.AreEqual(av[i], bv[i]);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

    }
}
