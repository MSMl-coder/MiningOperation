using NUnit.Framework;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MO269UnifiedSurfaceTests
    {
        [Test]
        public void UnifiedShader_HasDocumentedAuthoringControls()
        {
            Shader shader = Shader.Find("Mining Operation/Asteroid Unified Surface");
            Assert.IsNotNull(shader, "Unified shader must be in the Unity project.");
            var material = new Material(shader);
            try
            {
                Assert.IsTrue(material.HasProperty("_MacroFrequency"));
                Assert.IsTrue(material.HasProperty("_MacroStrength"));
                Assert.IsTrue(material.HasProperty("_MacroWarp"));
                Assert.IsTrue(material.HasProperty("_MacroOctaves"));
                Assert.IsTrue(material.HasProperty("_NoiseMode"));
                Assert.IsTrue(material.HasProperty("_StrataStrength"));
                Assert.IsTrue(material.HasProperty("_IceCrackStrength"));
                Assert.IsTrue(material.HasProperty("_DetailFrequency"));
                Assert.IsTrue(material.HasProperty("_DetailNormalStrength"));
                Assert.IsTrue(material.HasProperty("_IceBlend"));
                Assert.IsTrue(material.HasProperty("_MO_ObjectRadius"));
                Assert.IsTrue(material.HasProperty("_MO_LODVisibility"));
                Assert.IsTrue(material.HasProperty("_LightWrap"));
                Assert.IsTrue(material.HasProperty("_DarkSideFill"));
                Assert.GreaterOrEqual(material.FindPass("DepthOnly"), 0,
                    "Unified surface must clip the depth prepass with the same dither as its color pass.");
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void PerSpawnOverrides_AreIndependentWithoutAlteringSharedMaterial()
        {
            Shader shader = Shader.Find("Mining Operation/Asteroid Unified Surface");
            Assert.IsNotNull(shader);
            var shared = new Material(shader);
            var privateMat = new Material(shader);
            var field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            var rockGen = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            var aObject = new GameObject("Belt Alpha");
            var bObject = new GameObject("Ice Delta");
            try
            {
                field.asteroidSettings = rockGen;
                field.unifiedAsteroidMaterial = shared;
                var a = aObject.AddComponent<AsteroidFieldSpawnPoint>();
                var b = bObject.AddComponent<AsteroidFieldSpawnPoint>();
                a.settings = b.settings = field;
                a.EnsureLocalSettings();
                b.EnsureLocalSettings();
                a.visuals.unifiedAsteroidMaterialOverride = privateMat;
                Assert.AreSame(shared, field.unifiedAsteroidMaterial);
                Assert.IsNull(b.visuals.unifiedAsteroidMaterialOverride);
                Assert.AreSame(privateMat, a.visuals.unifiedAsteroidMaterialOverride);
            }
            finally
            {
                Object.DestroyImmediate(aObject);
                Object.DestroyImmediate(bObject);
                Object.DestroyImmediate(rockGen);
                Object.DestroyImmediate(field);
                Object.DestroyImmediate(shared);
                Object.DestroyImmediate(privateMat);
            }
        }

        [Test]
        public void NewAuthoringNames_IncludeContentAndFormation()
        {
            var go = new GameObject("Outer Belt");
            try
            {
                var spawn = go.AddComponent<AsteroidFieldSpawnPoint>();
                spawn.contentType = MiningFieldContent.Rock;
                spawn.formation = AsteroidFormationType.BrokenBelt;
                string name = MOUnifiedAsteroidAssetTools.NameFor(spawn, "FieldSettings");
                StringAssert.Contains("Rock", name);
                StringAssert.Contains("BrokenBelt", name);
                StringAssert.Contains("FieldSettings", name);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
