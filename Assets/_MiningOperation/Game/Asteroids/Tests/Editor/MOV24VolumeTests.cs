using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOV24VolumeTests
    {
        private GameObject host;
        private AsteroidFieldSpawnPoint anchor;

        [SetUp]
        public void Setup()
        {
            host = new GameObject("MO volume tests");
            anchor = host.AddComponent<AsteroidFieldSpawnPoint>();
            anchor.contentType = MiningFieldContent.Gas;
            anchor.fieldId = "GAS-V24-TEST";
            anchor.fieldSeed = 123411L;
            anchor.gasCloudCount = 3;
            anchor.gasCloudRadiusMeters = 350;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void OldSceneDefaultsToSafeParticleMode()
        {
            Assert.AreEqual(MOGasPresentation.SoftParticles, anchor.gasPresentation);
        }

        [Test]
        public void CombinedVolumeHasNoPerCloudGameObjects()
        {
            var data = MOGasFieldLayout.Plan(anchor);
            var mesh = MOGasVolumeMeshBuilder.Build(data);
            try
            {
                Assert.AreEqual(3 * 13 * 21, mesh.vertexCount);
                Assert.AreEqual(3 * 12 * 20 * 6, mesh.triangles.Length);
                Assert.Greater(mesh.bounds.size.x, 0);
                Assert.Greater(mesh.bounds.size.y, 0);
                Assert.AreEqual(0, host.transform.childCount);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void EachVolumeEncodesItsOwnCenterAndRadius()
        {
            var data = MOGasFieldLayout.Plan(anchor);
            var mesh = MOGasVolumeMeshBuilder.Build(data);
            try
            {
                var channels = new System.Collections.Generic.List<Vector4>();
                mesh.GetUVs(1, channels);
                int perCloud = 13 * 21;
                Assert.AreEqual(mesh.vertexCount, channels.Count);
                for (int i = 0; i < data.clouds.Count; ++i)
                {
                    Vector4 stored = channels[i * perCloud];
                    var cloud = data.clouds[i];
                    Assert.That(stored.x, Is.EqualTo(cloud.localCenter.x).Within(.001f));
                    Assert.That(stored.y, Is.EqualTo(cloud.localCenter.y).Within(.001f));
                    Assert.That(stored.z, Is.EqualTo(cloud.localCenter.z).Within(.001f));
                    Assert.That(stored.w, Is.EqualTo(cloud.radiusMeters).Within(.001f));
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void VolumeGeometryRemainsDeterministic()
        {
            var data = MOGasFieldLayout.Plan(anchor);
            var first = MOGasVolumeMeshBuilder.Build(data);
            var second = MOGasVolumeMeshBuilder.Build(data);
            try
            {
                CollectionAssert.AreEqual(first.vertices, second.vertices);
                CollectionAssert.AreEqual(first.triangles, second.triangles);
                CollectionAssert.AreEqual(first.colors, second.colors);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void VisualChangesDoNotChangeSavedGasLayout()
        {
            var map = MOGasFieldLayout.Plan(anchor);
            string signature = map.authoringSignature;
            anchor.gasPresentation = MOGasPresentation.Volumetric;
            anchor.gasVolumeNoiseScale = 2.7f;
            anchor.gasVolumeSteps = 18;
            anchor.gasVolumeBrightness = 1.4f;
            anchor.gasOpacity = .32f;
            anchor.gasVisibilityDistanceMeters = 5000;
            Assert.AreEqual(signature, MOGasFieldLayout.BuildSignature(anchor));
            Assert.IsFalse(JsonUtility.ToJson(map).Contains("MassKg"));
        }
    }
}
