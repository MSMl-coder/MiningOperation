#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOBakedFieldTests
    {
        [Test]
        public void BakingModeDisablesRuntimeGenerationAndStreaming()
        {
            var fieldObject = new GameObject("BakedFieldTest");
            var child = new GameObject("__MO_BakedField");
            try
            {
                var controller = fieldObject.AddComponent<AsteroidFieldController>();
                child.transform.SetParent(fieldObject.transform);
                var root = child.AddComponent<MOBakedFieldRoot>();
                root.Configure(controller, "Assets/_MiningOperation/BakedFields/Test");
                controller.GenerateOnStart = true;
                controller.RuntimeStreaming = true;
                controller.SetBakedScene(root);
                Assert.IsTrue(controller.BakedSceneMode);
                Assert.IsFalse(controller.GenerateOnStart);
                Assert.IsFalse(controller.RuntimeStreaming);
                Assert.AreSame(root, controller.BakedSceneRoot);
            }
            finally { Object.DestroyImmediate(fieldObject); }
        }

        [Test]
        public void ManualRootTransformAndComponentsTriggerProtection()
        {
            var go = new GameObject("AST-test");
            try
            {
                var data = go.AddComponent<MOBakedAsteroid>();
                var visual = new GameObject("__MO_BakedVisuals");
                visual.transform.SetParent(go.transform, false);
                visual.AddComponent<MeshRenderer>();
                data.CaptureBakeBaseline();
                Assert.IsFalse(data.HasManualEdits);
                go.transform.localPosition = new Vector3(13f, 0f, 22f);
                Assert.IsTrue(data.HasManualEdits);
                go.transform.localPosition = Vector3.zero;
                Assert.IsFalse(data.HasManualEdits);
                go.AddComponent<BoxCollider>();
                Assert.IsTrue(data.HasManualEdits);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EditedSceneTransformSynchronizesWithSnapshotByPersistentId()
        {
            var fieldObject = new GameObject("BakedFieldTest");
            var rootObject = new GameObject("__MO_BakedField");
            var asteroidObject = new GameObject("AST-test");
            try
            {
                var controller = fieldObject.AddComponent<AsteroidFieldController>();
                rootObject.transform.SetParent(fieldObject.transform, false);
                var root = rootObject.AddComponent<MOBakedFieldRoot>();
                root.Configure(controller, "Assets/_MiningOperation/BakedFields/Test");
                asteroidObject.transform.SetParent(rootObject.transform, false);
                var asteroid = asteroidObject.AddComponent<MOBakedAsteroid>();
                var record = new AsteroidFieldRecord
                {
                    index = 7,
                    localPosition = new Vector3(1f, 0f, 2f),
                    localRotation = Quaternion.identity,
                    asteroid = new AsteroidSaveRecord { asteroidId = "AST-test" }
                };
                asteroid.Configure(controller, record);
                var snapshot = new AsteroidFieldSnapshot
                {
                    fieldId = "test-field",
                    asteroids = new System.Collections.Generic.List<AsteroidFieldRecord> { record }
                };
                typeof(AsteroidFieldController).GetField("snapshot",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(controller, snapshot);
                controller.SetBakedScene(root);
                root.Reindex();
                asteroidObject.transform.localPosition = new Vector3(37f, 6f, -2f);
                controller.SyncBakedSceneToSnapshot();
                Assert.AreEqual(new Vector3(37f, 6f, -2f), record.localPosition);
                asteroidObject.transform.localPosition = Vector3.zero;
                controller.SyncSnapshotToBakedScene();
                Assert.AreEqual(new Vector3(37f, 6f, -2f), asteroidObject.transform.position);
                Assert.AreSame(asteroid, root.FindAsteroid("AST-test"));
            }
            finally { Object.DestroyImmediate(fieldObject); }
        }

        [Test]
        public void BakedRuntimeDebrisRespectsSettingsAndSavedField()
        {
            var config = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            try
            {
                var method = typeof(AsteroidFieldController).GetMethod(
                    "CanDrawDecorativeDebris", System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);
                Assert.IsNotNull(method, "Baked and streamed fields must share debris validation.");
                var snapshot = new AsteroidFieldSnapshot
                {
                    asteroids = new System.Collections.Generic.List<AsteroidFieldRecord>
                    {
                        new AsteroidFieldRecord { asteroid = new AsteroidSaveRecord { asteroidId = "AST-1" } }
                    }
                };
                config.enableDecorativeDebris = true;
                config.decorativeRockCount = 120;
                config.microFragmentCount = 0;
                Assert.IsTrue((bool)method.Invoke(null, new object[] { config, snapshot }));
                config.enableDecorativeDebris = false;
                Assert.IsFalse((bool)method.Invoke(null, new object[] { config, snapshot }));
                config.enableDecorativeDebris = true;
                config.decorativeRockCount = 0;
                Assert.IsFalse((bool)method.Invoke(null, new object[] { config, snapshot }));
                config.microFragmentCount = 30;
                Assert.IsTrue((bool)method.Invoke(null, new object[] { config, snapshot }));
                snapshot.asteroids.Clear();
                Assert.IsFalse((bool)method.Invoke(null, new object[] { config, snapshot }));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ExplicitLockPreventsRebake()
        {
            var go = new GameObject("AST-protected");
            try
            {
                var data = go.AddComponent<MOBakedAsteroid>();
                data.ProtectFromRebake = true;
                Assert.IsTrue(data.HasManualEdits);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
