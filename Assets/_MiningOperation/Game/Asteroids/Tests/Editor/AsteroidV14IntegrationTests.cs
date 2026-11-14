using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidV14IntegrationTests
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
            generator.volumeSubdivisions = 2;
            generator.lod0Subdivisions = 3;
            generator.lod1Subdivisions = 2;
            generator.lod2Subdivisions = 1;
            generator.lod3Subdivisions = 1;
            generator.lod4Subdivisions = 0;
            field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.asteroidSettings = generator;
            field.asteroidCount = 12;
            field.radiusMeters = 12000f;
            field.thicknessMeters = 800f;
            field.minimumSurfaceGapMeters = 25f;
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(field);
            UnityEngine.Object.DestroyImmediate(generator);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test]
        public void EveryPlacedAsteroidHasIndependentSeedAndDiameter()
        {
            var layout = AsteroidFieldLayout.Plan(field, 1441L, "field-a");
            Assert.Greater(layout.Count, 5);
            var seeds = new HashSet<int>();
            foreach (var placement in layout)
            {
                Assert.IsTrue(seeds.Add(placement.asteroidSeed));
                var data = AsteroidGeneration.Create(placement.asteroidSeed, generator,
                    null, placement.asteroidId, placement.nominalDiameter);
                Assert.AreEqual(placement.nominalDiameter, data.diameterMeters);
            }
        }

        [Test]
        public void DifferentFormationOverridesChangePositionsAndNotSettingsAsset()
        {
            var options = AsteroidFieldLayoutOptions.FromSettings(field);
            options.asteroidCount = 12;
            options.formation = AsteroidFormationType.BrokenRing;
            var ring = AsteroidFieldLayout.Plan(field, 13L, "same-field", options);
            options.formation = AsteroidFormationType.Stream;
            var stream = AsteroidFieldLayout.Plan(field, 13L, "same-field", options);
            Assert.Greater(ring.Count, 0);
            Assert.Greater(stream.Count, 0);
            Assert.AreNotEqual(ring[0].localPosition, stream[0].localPosition);
            Assert.AreEqual(AsteroidFormationType.CrescentBelt, field.formation);
            Assert.AreEqual(ring[0].asteroidId, stream[0].asteroidId);
        }

        [Test]
        public void SceneSpawnPointCanRegenerateItsOwnCountAndFormation()
        {
            var go = new GameObject("Spawn Point Test");
            try
            {
                var anchor = go.AddComponent<AsteroidFieldSpawnPoint>();
                anchor.settings = field;
                anchor.fieldId = "test-anchor-a";
                anchor.fieldSeed = 4121L;
                anchor.radiusMeters = 12000f;
                anchor.thicknessMeters = 1500f;
                anchor.minimumDiameterMeters = 8f;
                anchor.maximumDiameterMeters = 140f;
                anchor.fixedCount = 6;
                anchor.formation = AsteroidFormationType.ClusterGroups;
                anchor.SyncToController();
                var controller = anchor.Controller;
                controller.ForceRebuildImmediately();
                Assert.AreEqual(AsteroidFormationType.ClusterGroups, controller.Snapshot.formation);
                Assert.AreEqual(6, controller.Snapshot.asteroids.Count);
                string firstId = controller.Snapshot.asteroids[0].asteroid.asteroidId;
                anchor.fixedCount = 9;
                anchor.formation = AsteroidFormationType.Stream;
                controller.ForceRebuildImmediately();
                Assert.AreEqual(9, controller.Snapshot.asteroids.Count);
                Assert.AreEqual(AsteroidFormationType.Stream, controller.Snapshot.formation);
                Assert.AreEqual(firstId, controller.Snapshot.asteroids[0].asteroid.asteroidId);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void PerAnchorCountsAreDeterministicAndDensityBased()
        {
            var go = new GameObject("Counts");
            try
            {
                var anchor = go.AddComponent<AsteroidFieldSpawnPoint>();
                anchor.countMode = AsteroidCountMode.SeededRange;
                anchor.randomCountMin = 40;
                anchor.randomCountMax = 70;
                anchor.fieldSeed = 7777L;
                int a = anchor.ResolveCount();
                Assert.AreEqual(a, anchor.ResolveCount());
                Assert.IsTrue(a >= 40 && a <= 70);
                anchor.countMode = AsteroidCountMode.DensityPerSquareKm;
                anchor.radiusMeters = 1000f;
                anchor.asteroidsPerSquareKm = 10f;
                Assert.AreEqual(31, anchor.ResolveCount());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FiveLodsUseCrossFade()
        {
            var root = new GameObject("LOD test");
            try
            {
                var instance = root.AddComponent<AsteroidInstance>();
                instance.Settings = generator;
                instance.AddMeshCollider = false;
                instance.Apply(AsteroidGeneration.Create(444, generator));
                var group = root.GetComponent<LODGroup>();
                Assert.IsNotNull(group);
                Assert.AreEqual(5, group.GetLODs().Length);
                Assert.AreEqual(LODFadeMode.CrossFade, group.fadeMode);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SelectionRaySphereFindsFirstPositiveIntersection()
        {
            var ray = new Ray(Vector3.zero, Vector3.forward);
            Assert.IsTrue(AsteroidSelectionController.RaySphere(ray,
                new Vector3(0f, 0f, 10f), 1f, out float hit));
            Assert.AreEqual(9f, hit, 0.0001f);
            Assert.IsFalse(AsteroidSelectionController.RaySphere(ray,
                new Vector3(4f, 0f, 10f), 1f, out _));
        }
    }
}
