#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOSettingsScriptImportTests
    {
        [TestCase("AsteroidResourceCatalog.cs", typeof(AsteroidResourceCatalog))]
        [TestCase("AsteroidGeneratorSettings.cs", typeof(AsteroidGeneratorSettings))]
        [TestCase("AsteroidFieldSettings.cs", typeof(AsteroidFieldSettings))]
        public void SerializedSettingsTypesHaveMatchingMonoScripts(string scriptFile, Type expected)
        {
            string basename = Path.GetFileNameWithoutExtension(scriptFile);
            foreach (string guid in AssetDatabase.FindAssets(basename))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) != scriptFile) continue;
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Assert.NotNull(script, scriptFile + " did not import as a MonoScript");
                Assert.AreEqual(expected, script.GetClass(), scriptFile + " imported the wrong type");
                Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)), "Missing script GUID");
                return;
            }
            Assert.Fail("Required ScriptableObject source file not found: " + scriptFile);
        }
    }
}
#endif
