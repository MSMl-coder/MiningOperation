using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOTexturedForwardPlusTests
    {
        [Test]
        public void TexturedSurface_CompilesAndHasLightingDebugProperty()
        {
            var shader = Shader.Find("Mining Operation/Asteroid Textured Surface");
            Assert.IsNotNull(shader, "Textured asteroid shader is missing.");
            Assert.IsTrue(shader.isSupported, "Textured asteroid shader is unsupported on this graphics API.");
            var material = new Material(shader);
            try
            {
                Assert.IsTrue(material.HasProperty("_MO_DebugLighting"));
                Assert.AreEqual(0f, material.GetFloat("_MO_DebugLighting"), 0.0001f);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void TexturedSurface_HasForwardPlusVariantAndKeepsPbr()
        {
            var shader = Shader.Find("Mining Operation/Asteroid Textured Surface");
            Assert.IsNotNull(shader);
            var path = AssetDatabase.GetAssetPath(shader);
            Assert.IsTrue(File.Exists(path), "Shader source must be accessible to inspect variants.");
            string source = File.ReadAllText(path);
            StringAssert.Contains("#pragma multi_compile _ _FORWARD_PLUS", source);
            StringAssert.Contains("UniversalFragmentPBR(inputData, surfaceData)", source);
        }
    }
}
