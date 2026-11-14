using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidV2CompositionTests
    {
        private AsteroidGeneratorSettings generator;
        private AsteroidResourceCatalog catalog;
        private AsteroidFieldSettings field;

        [SetUp]
        public void Setup()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            generator.volumeSubdivisions = 2;
            field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.asteroidSettings = generator;
            field.useComposedLayout = true;
            field.asteroidCount = 250;
            field.radiusMeters = 3400f;
            field.thicknessMeters = 950f;
            field.minimumSurfaceGapMeters = 12f;
            field.clusterStrength = .85f;
            field.densityPockets = 9;
            field.pocketWidthFraction = .055f;
        }
        [TearDown]
        public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(field);
            UnityEngine.Object.DestroyImmediate(generator);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test]
        public void V2CompositionIsSeedDeterministicAndHasUniqueIds()
        {
            var a = AsteroidFieldLayout.Plan(field, 523213L, "MO-V2-TEST");
            var b = AsteroidFieldLayout.Plan(field, 523213L, "MO-V2-TEST");
            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(a.Count, 180);
            var ids = new HashSet<string>();
            for (int i = 0; i < a.Count; ++i)
            {
                Assert.IsTrue(ids.Add(a[i].asteroidId));
                Assert.AreEqual(a[i].asteroidId, b[i].asteroidId);
                Assert.AreEqual(a[i].asteroidSeed, b[i].asteroidSeed);
                Assert.AreEqual(a[i].localPosition, b[i].localPosition);
                Assert.AreEqual(a[i].nominalDiameter, b[i].nominalDiameter);
            }
        }

        [Test]
        public void DifferentFormationsHaveDifferentSpatialLayoutsButStableIdentities()
        {
            var opts = AsteroidFieldLayoutOptions.FromSettings(field);
            opts.formation = AsteroidFormationType.CrescentBelt;
            var a = AsteroidFieldLayout.Plan(field, 12L, "STABLE", opts);
            opts.formation = AsteroidFormationType.Stream;
            var b = AsteroidFieldLayout.Plan(field, 12L, "STABLE", opts);
            Assert.Greater(a.Count, 10);
            Assert.Greater(b.Count, 10);
            Assert.AreEqual(a[0].asteroidId, b[0].asteroidId);
            Assert.AreNotEqual(a[0].localPosition, b[0].localPosition);
        }

        [Test]
        public void PreviousLayoutVersionTwoStillParsesAndSerializes()
        {
            var p = AsteroidFieldLayout.Plan(field, 713L, "LEGACY-V2")[0];
            var body = AsteroidGeneration.Create(p.asteroidSeed, generator, null, p.asteroidId, p.nominalDiameter);
            var scene = new AsteroidFieldSnapshot
            {
                layoutVersion = 2, fieldId = "LEGACY-V2", fieldSeed = 713L,
                asteroids = new List<AsteroidFieldRecord>
                {
                    new AsteroidFieldRecord
                    {
                        index = p.index, localPosition = p.localPosition,
                        localRotation = p.localRotation, asteroid = AsteroidFieldSaveUtility.CopyAsteroid(body)
                    }
                }
            };
            string serialized = AsteroidFieldSaveUtility.ToJson(scene);
            var loaded = AsteroidFieldSaveUtility.FromJson(serialized);
            Assert.AreEqual(2, loaded.layoutVersion);
            Assert.AreEqual(scene.asteroids[0].asteroid.remainingMassKg,
                loaded.asteroids[0].asteroid.remainingMassKg);
        }

        [Test]
        public void LegacyGeometryVersionTwoRemainsSupported()
        {
            var body = AsteroidGeneration.Create(931, generator);
            body.generatorVersion = 2;
            Mesh mesh = AsteroidGeometryGenerator.Generate(body, generator, 2);
            try { Assert.Greater(AsteroidGeneration.CalculateVolume(mesh), 0d); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            var save = AsteroidFieldSaveUtility.CopyAsteroid(body);
            AsteroidSaveUtility.ValidateRecord(save);
        }

        [Test]
        public void VisualDebrisSettingsDoNotAffectPersistentLayoutOrResources()
        {
            var a = AsteroidFieldLayout.Plan(field, 42L, "DEBRIS-TEST");
            field.decorativeRockCount = 13000;
            field.microFragmentCount = 18000;
            field.debrisSpreadMeters = 900;
            var b = AsteroidFieldLayout.Plan(field, 42L, "DEBRIS-TEST");
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; ++i)
            {
                Assert.AreEqual(a[i].asteroidId, b[i].asteroidId);
                Assert.AreEqual(a[i].localPosition, b[i].localPosition);
            }
        }

        [Test]
        public void V3RockShapesAreIndependentOfClass()
        {
            var a = AsteroidGeneration.Create(7117, generator, AsteroidClass.I);
            var b = AsteroidGeneration.Create(7117, generator, AsteroidClass.III);
            Assert.AreEqual(3, a.generatorVersion);
            Assert.AreEqual(a.volumeM3, b.volumeM3, 0d);
            Mesh mesh = AsteroidGeometryGenerator.Generate(a, generator, 2);
            try { Assert.Greater(AsteroidGeneration.CalculateVolume(mesh), 0d); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
