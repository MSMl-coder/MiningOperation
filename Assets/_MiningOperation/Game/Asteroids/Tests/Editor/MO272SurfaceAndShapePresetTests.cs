using NUnit.Framework;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MO272SurfaceAndShapePresetTests
    {
        [Test]
        public void Library_ContainsCategoriesAndIndependentStoneAndIceValues()
        {
            Assert.GreaterOrEqual(MOAsteroidMaterialPresets.All.Length, 8);
            var stone = MOAsteroidMaterialPresets.Find("rock.basalt");
            var ice = MOAsteroidMaterialPresets.Find("ice.dirty");
            Assert.IsNotNull(stone);
            Assert.IsNotNull(ice);
            Assert.AreNotEqual(stone.category, ice.category);
            Assert.AreEqual(0f, stone.floats["_IceBlend"]);
            Assert.Greater(ice.floats["_IceBlend"], 0.5f);
            Assert.Greater(stone.floats["_MacroFrequency"], 0f);
            Assert.GreaterOrEqual(ice.floats["_CraterRimHeight"], 0f);
        }

        [Test]
        public void ApplyingPreset_ChangesMaterialOnlyAndWritesPersistentTag()
        {
            Shader shader = Shader.Find(MOAsteroidMaterialPresets.RequiredShader);
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            try
            {
                var basalt = MOAsteroidMaterialPresets.Find("rock.basalt");
                Assert.IsTrue(MOAsteroidMaterialPresets.Apply(material, basalt));
                Assert.AreEqual(basalt.id, material.GetTag(MOAsteroidMaterialPresets.MaterialTag, false, ""));
                Assert.AreEqual(basalt.floats["_CraterFrequency"], material.GetFloat("_CraterFrequency"), 1e-4f);
                var ice = MOAsteroidMaterialPresets.Find("ice.clean");
                Assert.IsTrue(MOAsteroidMaterialPresets.Apply(material, ice));
                Assert.AreEqual(1f, material.GetFloat("_IceBlend"), 1e-4f);
                Assert.AreEqual(ice.id, material.GetTag(MOAsteroidMaterialPresets.MaterialTag, false, ""));
                material.SetFloat("_CraterDensity", .9f); // manual override remains possible
                Assert.AreEqual(.9f, material.GetFloat("_CraterDensity"), 1e-4f);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void ShapePreset_IsCopiedIntoSavedGeometryRecipe()
        {
            var settings = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            try
            {
                settings.shapePreset = MOAsteroidShapePreset.Fractured;
                var profile = AsteroidGeometryProfile.FromSettings(settings);
                Assert.AreEqual(MOAsteroidShapePreset.Fractured, profile.shapePreset);
                Assert.AreEqual(MOAsteroidShapePreset.Fractured, profile.Copy().shapePreset);
                // Backward-compatible JSON records with no new field read as Mixed=0.
                var legacy = JsonUtility.FromJson<AsteroidGeometryProfile>("{\"shapeIrregularity\":0.5}");
                Assert.AreEqual(MOAsteroidShapePreset.Mixed, legacy.shapePreset);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void ForcedGeometryFamiliesProduceDifferentMeshesForTheSameSeed()
        {
            var rounded = new AsteroidGeometryProfile
            {
                shapePreset = MOAsteroidShapePreset.Rounded,
                shapeIrregularity = .5f, craterIntensity = .4f, surfaceRoughness = .2f,
                minCraters = 2, maxCraters = 4, minimumAxisScale = .55f
            };
            var fractured = rounded.Copy();
            fractured.shapePreset = MOAsteroidShapePreset.Fractured;
            Mesh a = null, b = null;
            try
            {
                a = AsteroidGeometryGenerator.Generate(new AsteroidData
                {
                    seed = 12345, diameterMeters = 45f, generatorVersion = 8,
                    geometryProfile = rounded
                }, null, 2);
                b = AsteroidGeometryGenerator.Generate(new AsteroidData
                {
                    seed = 12345, diameterMeters = 45f, generatorVersion = 8,
                    geometryProfile = fractured
                }, null, 2);
                Assert.AreEqual(a.vertexCount, b.vertexCount);
                Vector3[] va = a.vertices, vb = b.vertices;
                float difference = 0f;
                for (int i = 0; i < va.Length; i++) difference += (va[i] - vb[i]).magnitude;
                Assert.Greater(difference, 1f);
            }
            finally
            {
                if (a != null) Object.DestroyImmediate(a);
                if (b != null) Object.DestroyImmediate(b);
            }
        }
    }
}
