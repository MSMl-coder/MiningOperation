using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MONaturalBeltV264Tests
    {
        private AsteroidResourceCatalog catalog;
        private AsteroidGeneratorSettings generator;
        private AsteroidFieldSettings field;

        [SetUp]
        public void Setup()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.asteroidSettings = generator;
            field.asteroidCount = 180;
            field.formation = AsteroidFormationType.CrescentBelt;
            field.radiusMeters = 8000f;
            field.thicknessMeters = 1800f;
            field.minimumSurfaceGapMeters = 24f;
            field.placementAttempts = 110;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(field);
            UnityEngine.Object.DestroyImmediate(generator);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        private AsteroidFieldLayoutOptions Options(AsteroidFormationType formation, bool composed)
        {
            var options = AsteroidFieldLayoutOptions.FromSettings(field);
            options.formation = formation;
            options.useComposedLayout = composed;
            options.asteroidCount = 180;
            options.minimumDiameter = 12f;
            options.maximumDiameter = 420f;
            options.depthMode = MOFieldDepthMode.PlanarSlab;
            options.depthCenterY = 80f;
            options.depthHalfRange = 140f;
            options.naturalBeltIrregularity = .85f;
            options.naturalDensityContrast = .8f;
            options.naturalSatelliteChance = .1f;
            options.useNaturalBeltDistribution = true;
            return options;
        }

        [TestCase(AsteroidFormationType.CrescentBelt, false)]
        [TestCase(AsteroidFormationType.CrescentBelt, true)]
        [TestCase(AsteroidFormationType.BrokenBelt, false)]
        [TestCase(AsteroidFormationType.BrokenBelt, true)]
        public void RepeatedSeedProducesSamePositionsAndNoNominalOverlap(
            AsteroidFormationType formation, bool composed)
        {
            var options = Options(formation, composed);
            var a = AsteroidFieldLayout.Plan(field, 725173L, "NATURAL-BELT", options);
            var b = AsteroidFieldLayout.Plan(field, 725173L, "NATURAL-BELT", options);
            Assert.Greater(a.Count, 40);
            Assert.AreEqual(a.Count, b.Count);
            var ids = new HashSet<string>();
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].asteroidId, b[i].asteroidId);
                Assert.AreEqual(a[i].asteroidSeed, b[i].asteroidSeed);
                Assert.AreEqual(a[i].localPosition, b[i].localPosition);
                Assert.AreEqual(a[i].nominalDiameter, b[i].nominalDiameter);
                Assert.IsTrue(ids.Add(a[i].asteroidId));
                Assert.That(a[i].localPosition.y, Is.InRange(-60.001f, 220.001f));
                for (int j = 0; j < i; j++)
                {
                    float gap = .95f * (a[i].nominalDiameter + a[j].nominalDiameter) +
                        options.minimumSurfaceGapMeters;
                    Assert.GreaterOrEqual(Vector3.Distance(a[i].localPosition, a[j].localPosition) + .03f, gap);
                }
            }
        }

        [TestCase(AsteroidFormationType.CrescentBelt)]
        [TestCase(AsteroidFormationType.BrokenBelt)]
        public void OldBeltAlgorithmRemainsSelectable(AsteroidFormationType formation)
        {
            var options = Options(formation, true);
            var natural = AsteroidFieldLayout.Plan(field, 831L, "COMPARE", options);
            options.useNaturalBeltDistribution = false;
            var legacy = AsteroidFieldLayout.Plan(field, 831L, "COMPARE", options);
            Assert.IsNotEmpty(natural);
            Assert.IsNotEmpty(legacy);
            Assert.AreNotEqual(natural[0].localPosition, legacy[0].localPosition);
        }

        [Test]
        public void UnrelatedStreamIgnoresNaturalBeltParameters()
        {
            var options = Options(AsteroidFormationType.Stream, true);
            var a = AsteroidFieldLayout.Plan(field, 2147L, "STREAM-STAYS-UNCHANGED", options);
            options.useNaturalBeltDistribution = false;
            options.naturalSatelliteChance = .35f;
            options.naturalDensityContrast = 0f;
            var b = AsteroidFieldLayout.Plan(field, 2147L, "STREAM-STAYS-UNCHANGED", options);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i].localPosition, b[i].localPosition);
        }

        [Test]
        public void OldAndCurrentSaveLayoutVersionsRemainValid()
        {
            foreach (int v in new[] { 1, 5, 6, 7 })
            {
                var snapshot = new AsteroidFieldSnapshot
                {
                    layoutVersion = v, fieldId = "LEGACY-LAYOUT", contentType = MiningFieldContent.Rock
                };
                Assert.DoesNotThrow(() => AsteroidFieldSaveUtility.Validate(snapshot));
            }
            Assert.Throws<InvalidOperationException>(() => AsteroidFieldSaveUtility.Validate(
                new AsteroidFieldSnapshot { layoutVersion = 8, fieldId = "FUTURE" }));
        }
    }
}
