using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidV211StableRenderingTests
    {
        [Test]
        public void NewFieldsUseStableMaterialsAndSingleMeshStreamingByDefault()
        {
            var s = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            try
            {
                Assert.IsFalse(s.allowExperimentalRockShaders);
                Assert.IsTrue(s.fastStreamedAsteroids);
                Assert.That(s.streamedMeshSubdivisions, Is.InRange(0, 4));
                Assert.That(s.cachedStreamedAsteroids, Is.InRange(0, 32));
            }
            finally { Object.DestroyImmediate(s); }
        }

        [Test]
        public void StableRenderMaterialUsesUrpLitWhenInstalled()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null || !lit.isSupported) Assert.Ignore("Unity 6 URP Lit unavailable in this editor test context.");
            Material material = AsteroidStableMaterials.Create(null, false, Color.gray, "MO test");
            try
            {
                Assert.IsNotNull(material);
                Assert.AreEqual(lit, material.shader);
                Assert.IsTrue(material.enableInstancing);
            }
            finally { if (material != null) Object.DestroyImmediate(material); }
        }
    }
}
