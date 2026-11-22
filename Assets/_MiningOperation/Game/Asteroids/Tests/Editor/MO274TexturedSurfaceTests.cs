using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MO274TexturedSurfaceTests
    {
        [Test]
        public void MaterialCatalog_AllPresetsHaveThreeMapsAndUniqueIds()
        {
            Assert.AreEqual(11, MOTexturedMaterialLibrary.Recipes.Length);
            Assert.AreEqual(11, MOTexturedMaterialLibrary.Recipes.Select(p => p.id).Distinct().Count());
            foreach (var p in MOTexturedMaterialLibrary.Recipes)
            {
                foreach (var kind in new[] { "Albedo", "Normal", "Mask" })
                {
                    string path = MOTexturedMaterialLibrary.TextureRoot + "/" + kind +
                        "/MO_" + p.fileStem + "_" + kind + ".png";
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path), path);
                }
            }
        }

        [Test]
        public void TexturedSurface_IsAcceptedByNearFarRuntime()
        {
            Shader s = Shader.Find(MOTexturedMaterialLibrary.ShaderName);
            Assert.IsNotNull(s, "Reimport the new textured shader and check Console errors.");
            using (var holder = new TemporaryMaterial(s))
                Assert.IsTrue(AsteroidStableMaterials.IsUnified(holder.Value));
        }

        [Test]
        public void ApplyingMaterialDoesNotMutateGeneratorOrFieldAssets()
        {
            Shader s = Shader.Find(MOTexturedMaterialLibrary.ShaderName);
            Assert.IsNotNull(s);
            using (var holder = new TemporaryMaterial(s))
            {
                MOTexturedMaterialLibrary.Apply(holder.Value, MOTexturedMaterialLibrary.Recipes[0]);
                Assert.IsNotNull(holder.Value.GetTexture("_BaseMap"));
                Assert.IsNotNull(holder.Value.GetTexture("_NormalMap"));
                Assert.IsNotNull(holder.Value.GetTexture("_MaskMap"));
                Assert.IsTrue(holder.Value.enableInstancing);
            }
        }

        private sealed class TemporaryMaterial : System.IDisposable
        {
            public readonly Material Value;
            public TemporaryMaterial(Shader s) { Value = new Material(s); }
            public void Dispose() { Object.DestroyImmediate(Value); }
        }
    }
}
