using System;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOV262HeightConstraintTests
    {
        private AsteroidResourceCatalog catalog;
        private AsteroidGeneratorSettings generator;
        private AsteroidFieldSettings field;

        [SetUp]
        public void SetUp()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.asteroidSettings = generator;
            field.asteroidCount = 64;
            field.formation = AsteroidFormationType.SphericalCloud;
            field.radiusMeters = 1800f;
            field.thicknessMeters = 400f;
            field.clusterCount = 3;
            field.placementAttempts = 80;
            field.minimumSurfaceGapMeters = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(field);
            UnityEngine.Object.DestroyImmediate(generator);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlanarSlabConstrainsHeightYButDoesNotFlattenDepthZ(bool composed)
        {
            var options = AsteroidFieldLayoutOptions.FromSettings(field);
            options.useComposedLayout = composed;
            options.asteroidCount = 64;
            options.minimumDiameter = 8f;
            options.maximumDiameter = 24f;
            options.depthMode = MOFieldDepthMode.PlanarSlab;
            options.depthCenterY = 350f;
            options.depthHalfRange = 25f;
            var result = AsteroidFieldLayout.Plan(field, 5001, "Y-PLANE", options);
            Assert.Greater(result.Count, 20);
            bool zVaries = false;
            for (int i = 0; i < result.Count; i++)
            {
                Assert.That(result[i].localPosition.y, Is.InRange(325f - .001f, 375f + .001f));
                if (Mathf.Abs(result[i].localPosition.z) > 100f) zVaries = true;
            }
            Assert.IsTrue(zVaries, "The XZ gameplay plane must keep its Z distribution.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Strict2DPlaneHonorsYJitterAndStableSeed(bool composed)
        {
            var options = AsteroidFieldLayoutOptions.FromSettings(field);
            options.useComposedLayout = composed;
            options.minimumDiameter = 8f;
            options.maximumDiameter = 24f;
            options.depthMode = MOFieldDepthMode.Strict2DPlane;
            options.depthCenterY = -40f;
            options.depthJitter = 3f;
            var a = AsteroidFieldLayout.Plan(field, 3007, "Y-STRICT", options);
            var b = AsteroidFieldLayout.Plan(field, 3007, "Y-STRICT", options);
            Assert.Greater(a.Count, 20);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].localPosition.y, Is.InRange(-43.001f, -36.999f));
                Assert.AreEqual(a[i].localPosition, b[i].localPosition);
                Assert.AreEqual(a[i].asteroidId, b[i].asteroidId);
            }
        }

        [Test]
        public void VersionFiveAndOldFourRemainValidSnapshots()
        {
            Assert.GreaterOrEqual(AsteroidFieldLayout.LayoutVersion, 7);
            foreach (int version in new[] { 4, 5, 6, AsteroidFieldLayout.LayoutVersion })
            {
                var snap = new AsteroidFieldSnapshot
                {
                    fieldId = "Y-HEIGHT-SNAPSHOT", layoutVersion = version,
                    contentType = MiningFieldContent.Rock
                };
                Assert.DoesNotThrow(() => AsteroidFieldSaveUtility.Validate(snap));
            }
        }
    }
}
