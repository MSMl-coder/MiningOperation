using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MO268CleanupTests
    {
        [Test]
        public void RetroPresetSettings_AreNotPartOfSpawnVisualConfiguration()
        {
            Assert.IsNull(typeof(MOSpawnVisuals).GetField("artDirection"));
            Assert.IsNull(typeof(MOSpawnVisuals).GetField("retroNoiseSuppression"));
            Assert.IsNull(typeof(MOSpawnVisuals).GetField("retroCraterReadability"));
            Assert.IsNull(typeof(MOSpawnVisuals).GetField("retroSilhouetteStrength"));
        }

        [Test]
        public void OldRetroSceneCommand_IsNotExposed()
        {
            Assert.IsNull(typeof(AsteroidV2SceneSetup).GetMethod("InstallRetroEnvironment",
                BindingFlags.Public | BindingFlags.Static));
        }

        [Test]
        public void TwoSpawnGeologyProfiles_RemainIndependentAndDoNotMutateSharedSettings()
        {
            var settings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            var generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            var aObject = new GameObject("A");
            var bObject = new GameObject("B");
            try
            {
                settings.asteroidSettings = generator;
                var a = aObject.AddComponent<AsteroidFieldSpawnPoint>();
                var b = bObject.AddComponent<AsteroidFieldSpawnPoint>();
                a.settings = settings;
                b.settings = settings;
                a.EnsureLocalSettings();
                b.EnsureLocalSettings();
                a.rockGeology.shapeIrregularity = 0.15f;
                b.rockGeology.shapeIrregularity = 0.85f;
                Assert.AreEqual(0.15f, a.ActiveGeology.shapeIrregularity, 0.0001f);
                Assert.AreEqual(0.85f, b.ActiveGeology.shapeIrregularity, 0.0001f);
                Assert.AreEqual(generator.shapeIrregularity, settings.asteroidSettings.shapeIrregularity);
            }
            finally
            {
                Object.DestroyImmediate(aObject);
                Object.DestroyImmediate(bObject);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(generator);
            }
        }
    }
}
