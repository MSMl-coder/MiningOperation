using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MO271LayeredMaterialTests
    {
        [Test]
        public void LayeredShader_ExposesIndependentMasksAndNoiseSelectors()
        {
            Shader shader = Shader.Find("Mining Operation/Asteroid Unified Surface");
            Assert.IsNotNull(shader, "Unified asteroid shader not imported.");
            var material = new Material(shader);
            try
            {
                string[] controls =
                {
                    "_NoiseMode", "_MacroNormalStrength", "_MacroFrequency", "_MacroWarp",
                    "_CraterMode", "_CraterFrequency", "_CraterDensity", "_CraterRadius",
                    "_CraterDepth", "_CraterRimHeight", "_CraterRimSharpness", "_CraterSecondary",
                    "_CraterColorStrength", "_CraterNormalStrength", "_CraterRoughnessStrength",
                    "_MicroNoiseType", "_DetailFrequency", "_DetailStrength",
                    "_DetailNormalStrength", "_MicroRoughnessStrength",
                    "_CavityColor", "_CavityStrength", "_CavityContrast", "_CavityRoughnessStrength",
                    "_MO_ObjectRadius", "_MO_SurfaceSeed", "_MO_LODVisibility", "_IceBlend"
                };
                foreach (string control in controls)
                    Assert.IsTrue(material.HasProperty(control), "Missing shader control: " + control);
                Assert.GreaterOrEqual(material.FindPass("DepthOnly"), 0);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void RockAndIce_UseSameShaderWithIndependentCraterAndMicroControls()
        {
            Shader shader = Shader.Find("Mining Operation/Asteroid Unified Surface");
            Assert.IsNotNull(shader);
            var rock = new Material(shader);
            var ice = new Material(shader);
            try
            {
                rock.SetFloat("_IceBlend", 0f);
                rock.SetFloat("_CraterMode", 2f);
                rock.SetFloat("_CraterFrequency", 1.1f);
                ice.SetFloat("_IceBlend", 1f);
                ice.SetFloat("_CraterMode", 4f);
                ice.SetFloat("_CraterFrequency", 2.6f);
                Assert.AreSame(rock.shader, ice.shader);
                Assert.AreEqual(0f, rock.GetFloat("_IceBlend"));
                Assert.AreEqual(1f, ice.GetFloat("_IceBlend"));
                Assert.AreEqual(1.1f, rock.GetFloat("_CraterFrequency"), .0001f);
                Assert.AreEqual(2.6f, ice.GetFloat("_CraterFrequency"), .0001f);
            }
            finally
            {
                Object.DestroyImmediate(rock);
                Object.DestroyImmediate(ice);
            }
        }
    }
}
