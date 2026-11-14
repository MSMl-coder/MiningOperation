using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    /// <summary>
    /// Regression tests for streaming-generated asteroid objects.
    /// v1.2 used GetComponent<T>() ?? AddComponent<T>() for Unity components;
    /// the resulting missing MeshRenderer triggered MissingComponentException.
    /// </summary>
    public sealed class AsteroidComponentRegressionTests
    {
        private AsteroidResourceCatalog catalog;
        private AsteroidGeneratorSettings asteroidSettings;
        private AsteroidFieldSettings fieldSettings;

        [SetUp]
        public void SetUp()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            asteroidSettings = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            asteroidSettings.catalog = catalog;
            asteroidSettings.lod0Subdivisions = 1;
            asteroidSettings.lod1Subdivisions = 0;
            asteroidSettings.lod2Subdivisions = 0;
            asteroidSettings.volumeSubdivisions = 2;

            fieldSettings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            fieldSettings.asteroidSettings = asteroidSettings;
            fieldSettings.meshColliders = false;
            fieldSettings.maxActiveAsteroids = 4;
            fieldSettings.spawnDistanceMeters = 2000f;
            fieldSettings.despawnDistanceMeters = 3000f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fieldSettings);
            Object.DestroyImmediate(asteroidSettings);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void NewAsteroidHasAllRenderComponentsAndFiveLODs()
        {
            var root = new GameObject("MO Regression Asteroid");
            try
            {
                var instance = root.AddComponent<AsteroidInstance>();
                instance.Settings = asteroidSettings;
                instance.AddMeshCollider = false;
                instance.Apply(AsteroidGeneration.Create(113, asteroidSettings));

                var filter = root.GetComponent<MeshFilter>();
                var renderer = root.GetComponent<MeshRenderer>();
                var group = root.GetComponent<LODGroup>();
                Assert.IsNotNull(filter);
                Assert.IsNotNull(renderer);
                Assert.IsNotNull(group);
                Assert.IsNotNull(filter.sharedMesh);
                Assert.AreEqual(5, group.GetLODs().Length);
                Assert.AreEqual(4, root.GetComponentsInChildren<AsteroidGeneratedLODMarker>(true).Length);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RebuildingSameAsteroidReplacesMeshesWithoutDuplicatingLODChildren()
        {
            var root = new GameObject("MO Rebuild Regression");
            try
            {
                var instance = root.AddComponent<AsteroidInstance>();
                instance.Settings = asteroidSettings;
                instance.AddMeshCollider = false;
                instance.Apply(AsteroidGeneration.Create(114, asteroidSettings));
                Mesh oldMesh = root.GetComponent<MeshFilter>().sharedMesh;

                instance.RebuildGeometry();

                Assert.AreNotSame(oldMesh, root.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(4, root.GetComponentsInChildren<AsteroidGeneratedLODMarker>(true).Length);
                Assert.AreEqual(5, root.GetComponent<LODGroup>().GetLODs().Length);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void StreamAroundCanSpawnOnInitiallyEmptyGameObject()
        {
            var asteroid = AsteroidGeneration.Create(115, asteroidSettings, null, "AST-STREAM-REGRESSION");
            var record = new AsteroidFieldRecord
            {
                index = 0,
                localPosition = new Vector3(25f, 0f, -30f),
                localRotation = Quaternion.identity,
                asteroid = AsteroidFieldSaveUtility.CopyAsteroid(asteroid)
            };
            var snapshot = new AsteroidFieldSnapshot
            {
                schemaVersion = AsteroidFieldSaveUtility.SchemaVersion,
                layoutVersion = AsteroidFieldLayout.LayoutVersion,
                fieldId = "regression-field",
                fieldSeed = 1234,
                formation = AsteroidFormationType.CrescentBelt,
                asteroids = new List<AsteroidFieldRecord> { record }
            };

            var root = new GameObject("MO Regression Field");
            try
            {
                var controller = root.AddComponent<AsteroidFieldController>();
                controller.Settings = fieldSettings;
                controller.LoadJson(AsteroidFieldSaveUtility.ToJson(snapshot));
                controller.StreamAround(record.localPosition, 1);

                Assert.AreEqual(1, controller.ActiveCount);
                var spawned = controller.GetActive(0);
                Assert.IsNotNull(spawned);
                Assert.IsNotNull(spawned.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsNotNull(spawned.GetComponent<MeshRenderer>());
                Assert.IsNotNull(spawned.GetComponent<LODGroup>());
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
