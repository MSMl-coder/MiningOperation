using NUnit.Framework;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MO273FracturedRockTests
    {
        [Test]
        public void CinematicRockPresetExposesFracturesWithoutIceOrGlowingRims()
        {
            var preset = MOAsteroidMaterialPresets.Find("rock.cinematic_fractured");
            Assert.IsNotNull(preset);
            Assert.Greater(preset.floats["_RockStructureStrength"], .8f);
            Assert.AreEqual(0f, preset.floats["_IceBlend"]);
            Assert.Less(preset.floats["_CraterRimHeight"], .05f);
            Assert.AreEqual(0f, preset.floats["_RimStrength"]);

            Shader shader = Shader.Find(MOAsteroidMaterialPresets.RequiredShader);
            Assert.IsNotNull(shader, "Unified rock shader is missing or failed to import");
            var mat = new Material(shader);
            try
            {
                Assert.IsTrue(mat.HasProperty("_RockStructureStrength"));
                Assert.IsTrue(mat.HasProperty("_RockFractureFrequency"));
                Assert.IsTrue(mat.HasProperty("_RockCrackDarkness"));
                Assert.IsTrue(mat.HasProperty("_RockHeightStrength"));
                Assert.IsTrue(MOAsteroidMaterialPresets.Apply(mat, preset));
                Assert.Greater(mat.GetFloat("_RockStructureStrength"), .8f);
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void GeometryVersionNineIsDeterministicAndKeepsPositivePhysicalVolume()
        {
            var profile = new AsteroidGeometryProfile
            {
                shapePreset = MOAsteroidShapePreset.Fractured,
                shapeIrregularity = .68f,
                craterIntensity = .38f,
                surfaceRoughness = .22f,
                minCraters = 2,
                maxCraters = 5,
                minimumAxisScale = .55f
            };
            Mesh a = null, b = null, legacy = null;
            try
            {
                var v9 = new AsteroidData
                {
                    seed = 60241, diameterMeters = 160f,
                    generatorVersion = 9, geometryProfile = profile
                };
                var v8 = new AsteroidData
                {
                    seed = 60241, diameterMeters = 160f,
                    generatorVersion = 8, geometryProfile = profile
                };
                a = AsteroidGeometryGenerator.Generate(v9, null, 2);
                b = AsteroidGeometryGenerator.Generate(v9, null, 2);
                legacy = AsteroidGeometryGenerator.Generate(v8, null, 2);
                Assert.AreEqual(a.vertexCount, b.vertexCount);
                Assert.Greater(AsteroidGeneration.CalculateVolume(a), 1f);
                float changeFromV8 = 0f;
                Vector3[] va = a.vertices, vb = b.vertices, old = legacy.vertices;
                for (int i = 0; i < va.Length; i++)
                {
                    Assert.Less((va[i] - vb[i]).sqrMagnitude, 1e-8f, "Non-deterministic vertex at " + i);
                    changeFromV8 += (va[i] - old[i]).sqrMagnitude;
                }
                Assert.Greater(changeFromV8, 1f, "V9 fractured geometry produced no visible change.");
            }
            finally
            {
                if (a != null) Object.DestroyImmediate(a);
                if (b != null) Object.DestroyImmediate(b);
                if (legacy != null) Object.DestroyImmediate(legacy);
            }
        }

        [Test]
        public void IceAndExistingV8StayOnTheirHistoricalGeometryVersions()
        {
            Assert.AreEqual(9, AsteroidGeneration.GeneratorVersion);
            Assert.AreEqual(6, AsteroidGeneration.IceShelfGeneratorVersion);
            Assert.AreEqual(7, AsteroidGeneration.IceShelfContinentalGeneratorVersion);
        }
    }
}
