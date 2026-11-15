using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidV25IceShelfTests
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
            field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.asteroidSettings = generator;
            field.iceGeneratorSettings = generator;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(field);
            UnityEngine.Object.DestroyImmediate(generator);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ShelfIsFlatAndDeterministicAcrossRebuild()
        {
            AsteroidData shelf = AsteroidGeneration.Create(551, generator, null,
                "AST-SHELF-551", 1200f, true, .085f, .38f);
            Assert.AreEqual(6, shelf.generatorVersion);
            Assert.IsTrue(shelf.geometryProfile.iceShelf);
            Mesh a = AsteroidGeometryGenerator.Generate(shelf, generator, 2);
            Mesh b = AsteroidGeometryGenerator.Generate(shelf, generator, 2);
            try
            {
                Assert.Greater(a.vertexCount, 200);
                Assert.AreEqual(a.vertexCount, b.vertexCount);
                Assert.AreEqual(a.triangles.Length, b.triangles.Length);
                var bounds = a.bounds.size;
                Assert.Greater(bounds.x, bounds.y * 3f);
                Assert.Greater(bounds.z, bounds.y * 3f);
                for (int i = 0; i < a.vertexCount; ++i)
                    Assert.AreEqual(a.vertices[i], b.vertices[i]);
                Assert.Greater(AsteroidGeneration.CalculateVolume(a), 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ShelfSaveRoundTripKeepsItsOwnShapeAndResources()
        {
            AsteroidData data = AsteroidGeneration.Create(912, generator,
                AsteroidClass.II, "AST-SHELF-PERSIST", 950f, true, .11f, .55f);
            AsteroidSaveRecord record = AsteroidFieldSaveUtility.CopyAsteroid(data);
            string json = JsonUtility.ToJson(record);
            var copy = JsonUtility.FromJson<AsteroidSaveRecord>(json);
            AsteroidSaveUtility.ValidateRecord(copy);
            Assert.AreEqual(6, copy.generatorVersion);
            Assert.IsTrue(copy.geometryProfile.iceShelf);
            Assert.AreEqual(data.geometryProfile.shelfThicknessRatio,
                copy.geometryProfile.shelfThicknessRatio, 1e-6f);
            Assert.AreEqual(data.initialMassKg, copy.initialMassKg, 1e-6);
            Assert.AreEqual(data.composition.Count, copy.composition.Count);
        }

        [Test]
        public void LegacyRockGenerationRemainsVersionFive()
        {
            var rock = AsteroidGeneration.Create(454, generator);
            Assert.AreEqual(5, rock.generatorVersion);
            Assert.IsFalse(rock.geometryProfile.iceShelf);
        }

        [Test]
        public void ShelfPreviewWorksAtMultipleGeometryResolutions()
        {
            var data = AsteroidGeneration.Create(779, generator, null,
                "AST-SHELF-LOD", 700f, true, .085f, .44f);
            Mesh near = AsteroidGeometryGenerator.Generate(data, generator, 3);
            Mesh far = AsteroidGeometryGenerator.Generate(data, generator, 1);
            try
            {
                Assert.Greater(near.vertexCount, far.vertexCount);
                Assert.Greater(AsteroidGeneration.CalculateVolume(near), 0d);
                Assert.Greater(AsteroidGeneration.CalculateVolume(far), 0d);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(near);
                UnityEngine.Object.DestroyImmediate(far);
            }
        }

        [Test]
        public void DedicatedFormationCanPlanAndUsesStableIds()
        {
            field.useComposedLayout = true;
            var options = AsteroidFieldLayoutOptions.FromSettings(field);
            options.formation = AsteroidFormationType.IceShelfFragments;
            options.asteroidCount = 8;
            options.radiusMeters = 8000f;
            options.thicknessMeters = 700f;
            options.minimumSurfaceGapMeters = 150f;
            options.minimumDiameter = 350f;
            options.maximumDiameter = 1600f;
            List<AsteroidFieldPlacement> a = AsteroidFieldLayout.Plan(field, 8971L, "SHELF-TEST", options);
            List<AsteroidFieldPlacement> b = AsteroidFieldLayout.Plan(field, 8971L, "SHELF-TEST", options);
            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(a.Count, 0);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].asteroidId, b[i].asteroidId);
                Assert.AreEqual(a[i].localPosition, b[i].localPosition);
            }
        }
    }
}
