#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOBakedMaterialSyncTests
    {
        private GameObject fieldObject;
        private AsteroidFieldSpawnPoint spawn;
        private AsteroidFieldController controller;
        private MOBakedAsteroid asteroid;
        private MeshRenderer primary;
        private MeshRenderer lod1;
        private Material oldMaterial;
        private Material newMaterial;
        private Material customMaterial;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(shader);
            oldMaterial = new Material(shader) { name = "Old material" };
            newMaterial = new Material(shader) { name = "New material" };
            customMaterial = new Material(shader) { name = "Manual override" };

            fieldObject = new GameObject("MOBakedMaterialTest");
            spawn = fieldObject.AddComponent<AsteroidFieldSpawnPoint>();
            controller = spawn.Controller;
            var rootObject = new GameObject("__MO_BakedField");
            rootObject.transform.SetParent(fieldObject.transform, false);
            var root = rootObject.AddComponent<MOBakedFieldRoot>();
            root.Configure(controller, "Assets/_MiningOperation/BakedFields/Test");
            var asteroidObject = new GameObject("AST-test");
            asteroidObject.transform.SetParent(rootObject.transform, false);
            asteroid = asteroidObject.AddComponent<MOBakedAsteroid>();
            asteroid.Configure(controller, new AsteroidFieldRecord
            {
                index = 1,
                asteroid = new AsteroidSaveRecord { asteroidId = "AST-test" }
            });
            var visual = new GameObject("__MO_BakedVisuals");
            visual.transform.SetParent(asteroidObject.transform, false);
            primary = visual.AddComponent<MeshRenderer>();
            primary.sharedMaterial = oldMaterial;
            var lod = new GameObject("__LOD1");
            lod.transform.SetParent(visual.transform, false);
            lod1 = lod.AddComponent<MeshRenderer>();
            lod1.sharedMaterial = oldMaterial;
            asteroid.CaptureBakeBaseline();
            controller.SetBakedScene(root);
            root.Reindex();
            spawn.visuals = new MOSpawnVisuals();
            spawn.visuals.unifiedAsteroidMaterialOverride = newMaterial;
        }

        [TearDown]
        public void TearDown()
        {
            if (fieldObject != null) Object.DestroyImmediate(fieldObject);
            Object.DestroyImmediate(oldMaterial);
            Object.DestroyImmediate(newMaterial);
            Object.DestroyImmediate(customMaterial);
        }

        [Test]
        public void ChangingSpawnMaterialSynchronizesAllManagedLodsWithoutRebake()
        {
            var result = MOBakedMaterialSync.Apply(spawn, false, false);
            Assert.AreSame(newMaterial, primary.sharedMaterial);
            Assert.AreSame(newMaterial, lod1.sharedMaterial);
            Assert.AreEqual(2, result.changedRenderers);
            Assert.AreSame(newMaterial, asteroid.BakedMaterialBaseline);
            Assert.IsFalse(asteroid.HasManualEdits);
        }

        [Test]
        public void ManualLODOverridePreservesWholeAsteroidUnlessForced()
        {
            lod1.sharedMaterial = customMaterial;
            var result = MOBakedMaterialSync.Apply(spawn, false, false);
            Assert.AreEqual(0, result.changedRenderers);
            Assert.AreSame(oldMaterial, primary.sharedMaterial);
            Assert.AreSame(customMaterial, lod1.sharedMaterial);
            Assert.Greater(result.customRenderers, 0);
            result = MOBakedMaterialSync.Apply(spawn, true, false);
            Assert.AreEqual(2, result.changedRenderers);
            Assert.AreSame(newMaterial, primary.sharedMaterial);
            Assert.AreSame(newMaterial, lod1.sharedMaterial);
        }

        [Test]
        public void ProtectedBodiesAreLeftUntouchedUnlessForced()
        {
            asteroid.ProtectFromRebake = true;
            var result = MOBakedMaterialSync.Apply(spawn, false, false);
            Assert.AreEqual(1, result.protectedBodies);
            Assert.AreSame(oldMaterial, primary.sharedMaterial);
            MOBakedMaterialSync.Apply(spawn, true, false);
            Assert.AreSame(newMaterial, primary.sharedMaterial);
        }

        [Test]
        public void MaterialSyncDoesNotResetManualTransformBaseline()
        {
            asteroid.transform.localPosition = new Vector3(12f, 3f, 0f);
            Assert.IsTrue(asteroid.HasManualEdits);
            MOBakedMaterialSync.Apply(spawn, false, false);
            Assert.AreSame(newMaterial, primary.sharedMaterial);
            Assert.IsTrue(asteroid.HasManualEdits, "Material sync must not clear manual positional edits.");
        }
    }
}
#endif
