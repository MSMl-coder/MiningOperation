using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidGeneratorTests
    {
        private AsteroidGeneratorSettings settings;
        private AsteroidResourceCatalog catalog;

        [SetUp]
        public void Setup()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            settings = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            settings.catalog = catalog;
            settings.lod0Subdivisions = 2; // fast visual LOD tests; physics resolution stays fixed.
            settings.lod1Subdivisions = 1;
            settings.lod2Subdivisions = 0;
            settings.volumeSubdivisions = 3;
        }
        [TearDown]
        public void Teardown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test]
        public void SeedDeterminismIncludesShapeAndComposition()
        {
            AsteroidData a = AsteroidGeneration.Create(12345, settings);
            AsteroidData b = AsteroidGeneration.Create(12345, settings);
            Assert.AreEqual(a.asteroidId, b.asteroidId);
            Assert.AreEqual(a.asteroidClass, b.asteroidClass);
            Assert.AreEqual(a.volumeM3, b.volumeM3, 1e-7);
            Assert.AreEqual(a.initialMassKg, b.initialMassKg, 1e-6);
            Assert.AreEqual(a.porosity01, b.porosity01, 1e-12);
            Assert.AreEqual(a.composition.Count, b.composition.Count);
            for (int i = 0; i < a.composition.Count; i++)
            {
                Assert.AreEqual(a.composition[i].resourceId, b.composition[i].resourceId);
                Assert.AreEqual(a.composition[i].fraction, b.composition[i].fraction, 1e-12);
            }
        }

        [Test]
        public void VolumeMassAndFractionsValidAcrossManySeeds()
        {
            for (int i = 0; i < 30; i++)
            {
                AsteroidData data = AsteroidGeneration.Create(i, settings);
                Assert.Greater(data.volumeM3, 0);
                Assert.Greater(data.densityKgM3, 0);
                Assert.AreEqual(data.volumeM3 * data.densityKgM3, data.initialMassKg, 0.00001);
                double sum = 0;
                foreach (var part in data.composition)
                {
                    Assert.GreaterOrEqual(part.fraction, 0);
                    sum += part.fraction;
                }
                Assert.AreEqual(1, sum, 1e-10);
            }
        }

        [Test]
        public void ClassGatesAndSpecialFractionCapsWork()
        {
            settings.rareChanceClassII = 1f;
            settings.rareChanceClassIII = 1f;
            settings.exoticChanceClassIII = 1f;
            var rarity = new Dictionary<string, ResourceRarity>();
            foreach (var r in catalog.resources) rarity.Add(r.id, r.rarity);
            for (int i = 0; i < 30; i++)
            {
                var one = AsteroidGeneration.Create(i, settings, AsteroidClass.I);
                var two = AsteroidGeneration.Create(i, settings, AsteroidClass.II);
                var three = AsteroidGeneration.Create(i, settings, AsteroidClass.III);
                bool classTwoHasRare = false, classThreeHasRare = false, classThreeHasExotic = false;
                foreach (var p in one.composition) Assert.LessOrEqual((int)rarity[p.resourceId], 1);
                foreach (var p in two.composition)
                {
                    Assert.AreNotEqual(ResourceRarity.Exotic, rarity[p.resourceId]);
                    if (rarity[p.resourceId] == ResourceRarity.Rare)
                    {
                        classTwoHasRare = true;
                        Assert.LessOrEqual(p.fraction, settings.maximumRareFractionClassII + 1e-9);
                    }
                }
                foreach (var p in three.composition)
                {
                    if (rarity[p.resourceId] == ResourceRarity.Rare)
                    {
                        classThreeHasRare = true;
                        Assert.LessOrEqual(p.fraction, settings.maximumRareFractionClassIII + 1e-9);
                    }
                    if (rarity[p.resourceId] == ResourceRarity.Exotic)
                    {
                        classThreeHasExotic = true;
                        Assert.LessOrEqual(p.fraction, settings.maximumExoticFractionClassIII + 1e-9);
                    }
                }
                Assert.IsTrue(classTwoHasRare);
                Assert.IsTrue(classThreeHasRare);
                Assert.IsTrue(classThreeHasExotic);
            }
            settings.rareChanceClassII = 0;
            settings.rareChanceClassIII = 0;
            settings.exoticChanceClassIII = 0;
            for (int i = 0; i < 20; i++)
                foreach (var p in AsteroidGeneration.Create(i + 44, settings, AsteroidClass.III).composition)
                    Assert.LessOrEqual((int)rarity[p.resourceId], 1);
        }

        [Test]
        public void ClassDoesNotChangeGeometry()
        {
            AsteroidData a = AsteroidGeneration.Create(99, settings, AsteroidClass.I);
            AsteroidData b = AsteroidGeneration.Create(99, settings, AsteroidClass.III);
            Assert.AreEqual(a.diameterMeters, b.diameterMeters, 0);
            Assert.AreEqual(a.volumeM3, b.volumeM3, 0);
            Assert.AreEqual(a.geometryProfile.minimumAxisScale, b.geometryProfile.minimumAxisScale);
        }

        [Test]
        public void RenderLODChangeDoesNotChangeVolumeOrMass()
        {
            AsteroidData a = AsteroidGeneration.Create(145, settings);
            settings.lod0Subdivisions = 4;
            settings.lod1Subdivisions = 3;
            settings.lod2Subdivisions = 2;
            AsteroidData b = AsteroidGeneration.Create(145, settings);
            Assert.AreEqual(a.volumeM3, b.volumeM3, 0);
            Assert.AreEqual(a.initialMassKg, b.initialMassKg, 0);
        }

        [Test]
        public void V2ProfileSnapshotProtectsGeometryFromSettingsChanges()
        {
            AsteroidData a = AsteroidGeneration.Create(400, settings);
            Mesh before = AsteroidGeometryGenerator.Generate(a, settings, 2);
            settings.shapeIrregularity = 0;
            settings.craterIntensity = 0;
            settings.minimumAxisScale = 0.1f;
            Mesh after = AsteroidGeometryGenerator.Generate(a, settings, 2);
            try
            {
                Assert.AreEqual(before.vertexCount, after.vertexCount);
                for (int i = 0; i < before.vertexCount; i++)
                    Assert.AreEqual(before.vertices[i], after.vertices[i]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(before);
                UnityEngine.Object.DestroyImmediate(after);
            }
        }

        [Test]
        public void MeshIsClosedAndNonDegenerate()
        {
            Mesh mesh = AsteroidGeometryGenerator.Generate(7, 50, settings, 2);
            try
            {
                Assert.Greater(AsteroidGeneration.CalculateVolume(mesh), 0);
                // Rendering UV seams duplicate *indices*, not positions. Weld exact
                // coincident positions before checking geometric watertightness.
                var canonical = new Dictionary<Vector3, int>();
                Vector3[] positions = mesh.vertices;
                int[] welded = new int[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    if (!canonical.TryGetValue(positions[i], out int representative))
                    {
                        representative = canonical.Count;
                        canonical.Add(positions[i], representative);
                    }
                    welded[i] = representative;
                }
                var edges = new Dictionary<ulong, int>();
                int[] tri = mesh.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    CountEdge(welded[tri[i]], welded[tri[i + 1]], edges);
                    CountEdge(welded[tri[i + 1]], welded[tri[i + 2]], edges);
                    CountEdge(welded[tri[i + 2]], welded[tri[i]], edges);
                }
                foreach (var pair in edges) Assert.AreEqual(2, pair.Value);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private static void CountEdge(int a, int b, Dictionary<ulong, int> edges)
        {
            uint lo = (uint)Math.Min(a,b), hi = (uint)Math.Max(a,b);
            ulong key = ((ulong)lo << 32) | hi;
            edges.TryGetValue(key, out int n);
            edges[key] = n + 1;
        }

        [Test]
        public void BulkDensityUsesMassFractionHarmonicMeanAndPorosity()
        {
            var parts = new List<MaterialFraction>
            {
                new MaterialFraction("iron", 0.5), new MaterialFraction("silicates", 0.5)
            };
            double expected = 0.8 / (0.5 / 7870.0 + 0.5 / 2900.0);
            Assert.AreEqual(expected, AsteroidCompositionGenerator.CalculateBulkDensity(parts, catalog, 0.2), 1e-7);
        }

        [Test]
        public void InvalidCatalogRejected()
        {
            catalog.resources.Add(new ResourceDefinition { id = "iron" });
            Assert.Throws<InvalidOperationException>(() => AsteroidGeneration.Create(1, settings));
        }

        [Test]
        public void SaveRoundTripAndLegacySchemaSupported()
        {
            var go = new GameObject("TestAsteroid");
            try
            {
                var instance = go.AddComponent<AsteroidInstance>();
                instance.Settings = settings;
                instance.AddMeshCollider = false;
                instance.Apply(AsteroidGeneration.Create(100, settings));
                double mined = instance.ExtractMass(10000);
                string json = AsteroidSaveUtility.CaptureJson(new[] { instance });
                var parsed = AsteroidSaveUtility.ParseJson(json);
                Assert.AreEqual(2, parsed.schemaVersion);
                Assert.AreEqual(instance.Data.remainingMassKg, parsed.asteroids[0].remainingMassKg, 1e-5);
                instance.ExtractMass(3000);
                instance.Restore(parsed.asteroids[0]);
                Assert.AreEqual(parsed.asteroids[0].remainingMassKg, instance.Data.remainingMassKg, 1e-5);
                Assert.Greater(mined, 0);

                var legacy = instance.Capture();
                legacy.generatorVersion = 1;
                legacy.geometryProfile = null;
                var legacyCollection = new AsteroidSaveCollection { schemaVersion = 1 };
                legacyCollection.asteroids.Add(legacy);
                var legacyParsed = AsteroidSaveUtility.ParseJson(JsonUtility.ToJson(legacyCollection));
                Assert.AreEqual(1, legacyParsed.schemaVersion);
                instance.Restore(legacyParsed.asteroids[0]);
                Assert.AreEqual(1, instance.Data.generatorVersion);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ExtractionConservesMassAndRejectsInvalidInput()
        {
            var go = new GameObject("ExtractionTest");
            try
            {
                var asteroid = go.AddComponent<AsteroidInstance>();
                asteroid.Settings = settings;
                asteroid.AddMeshCollider = false;
                asteroid.Apply(AsteroidGeneration.Create(2, settings));
                double before = asteroid.Data.remainingMassKg;
                Assert.AreEqual(0, asteroid.ExtractMass(-1));
                Assert.AreEqual(0, asteroid.ExtractMass(double.NaN));
                Assert.AreEqual(0, asteroid.ExtractMass(double.PositiveInfinity));
                var output = asteroid.ExtractResources(Math.Min(2000, before));
                double total = 0;
                foreach (var pair in output) total += pair.Value;
                Assert.AreEqual(before - asteroid.Data.remainingMassKg, total, 1e-7);
                Assert.AreEqual(before, asteroid.Data.initialMassKg, 1e-7);
                Assert.Greater(asteroid.ExtractMass(double.MaxValue), 0d);
                Assert.AreEqual(0d, asteroid.Data.remainingMassKg, 1e-9);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TwoInstancesDoNotShareMutableMiningState()
        {
            var aObject = new GameObject("A");
            var bObject = new GameObject("B");
            try
            {
                AsteroidData shared = AsteroidGeneration.Create(84, settings);
                var a = aObject.AddComponent<AsteroidInstance>();
                var b = bObject.AddComponent<AsteroidInstance>();
                a.Settings = settings;
                b.Settings = settings;
                a.AddMeshCollider = false;
                b.AddMeshCollider = false;
                a.Apply(shared);
                b.Apply(shared);
                double original = shared.remainingMassKg;
                Assert.Greater(a.ExtractMass(1000), 0);
                Assert.AreEqual(original, b.Data.remainingMassKg, 1e-7);
                Assert.AreEqual(original, shared.remainingMassKg, 1e-7);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aObject);
                UnityEngine.Object.DestroyImmediate(bObject);
            }
        }

        [Test]
        public void FieldIdsStableAndDistinct()
        {
            Assert.AreNotEqual(AsteroidIdUtility.CreateFieldId(123, 1), AsteroidIdUtility.CreateFieldId(123, 2));
            Assert.AreEqual(AsteroidIdUtility.DeriveSeed(123, 1), AsteroidIdUtility.DeriveSeed(123, 1));
            Assert.AreEqual(AsteroidIdUtility.CreateFromField(123, 1, settings).asteroidId,
                AsteroidIdUtility.CreateFromField(123, 1, settings).asteroidId);
        }

        [Test]
        public void CorruptSaveRejectedAndDuplicateIdsDetected()
        {
            var data = AsteroidGeneration.Create(19, settings);
            var go = new GameObject("SaveTest");
            try
            {
                var instance = go.AddComponent<AsteroidInstance>();
                instance.Settings = settings;
                instance.AddMeshCollider = false;
                instance.Apply(data);
                AsteroidSaveRecord record = instance.Capture();
                record.remainingMassKg = -1;
                Assert.Throws<InvalidOperationException>(() => AsteroidSaveUtility.ValidateRecord(record));
                record.remainingMassKg = 0;
                var collection = new AsteroidSaveCollection();
                collection.asteroids.Add(record);
                collection.asteroids.Add(record);
                Assert.Throws<InvalidOperationException>(() => AsteroidSaveUtility.ParseJson(JsonUtility.ToJson(collection)));
                Assert.Throws<InvalidOperationException>(() => AsteroidSaveUtility.CaptureJson(new[] { instance, instance }));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
