using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOIndependentFieldSettingsTests
    {
        private const string Root = "Assets/_MiningOperation/AsteroidAuthoring";
        private const string Temp = Root + "/__UniqueFieldTests";
        private AsteroidResourceCatalog catalog;
        private AsteroidGeneratorSettings generator;
        private AsteroidFieldSettings template;
        private AsteroidFieldSpawnPoint first;
        private AsteroidFieldSpawnPoint second;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Temp))
                AssetDatabase.CreateFolder(Root, "__UniqueFieldTests");

            catalog = ScriptableObject.CreateInstance<AsteroidResourceCatalog>();
            AssetDatabase.CreateAsset(catalog, Temp + "/Catalog.asset");
            generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            AssetDatabase.CreateAsset(generator, Temp + "/Generator.asset");
            template = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            template.asteroidSettings = generator;
            template.asteroidCount = 87;
            template.formation = AsteroidFormationType.BrokenBelt;
            AssetDatabase.CreateAsset(template, Temp + "/Template.asset");
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (first != null) UnityEngine.Object.DestroyImmediate(first.gameObject);
            if (second != null) UnityEngine.Object.DestroyImmediate(second.gameObject);
            AssetDatabase.DeleteAsset(Temp + "/Catalog.asset");
            AssetDatabase.DeleteAsset(Temp + "/Generator.asset");
            AssetDatabase.DeleteAsset(Temp + "/Template.asset");
            if (AssetDatabase.IsValidFolder(Temp)) AssetDatabase.DeleteAsset(Temp);
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void EachSpawnCreatesSeparatePersistentFieldSettings_WithoutChangingTemplate()
        {
            first = AsteroidAuthoringWorkflow.CreateSpawnPoint(template, MiningFieldContent.Rock);
            second = AsteroidAuthoringWorkflow.CreateSpawnPoint(template, MiningFieldContent.Rock);
            string pathA = AssetDatabase.GetAssetPath(first.settings);
            string pathB = AssetDatabase.GetAssetPath(second.settings);
            try
            {
                Assert.IsFalse(string.IsNullOrEmpty(pathA));
                Assert.IsFalse(string.IsNullOrEmpty(pathB));
                Assert.AreNotEqual(pathA, pathB);
                Assert.AreNotSame(first.settings, second.settings);
                Assert.AreNotSame(first.settings, template);
                Assert.AreNotSame(second.settings, template);
                StringAssert.Contains("Formations/", pathA);
                StringAssert.Contains("Formations/", pathB);
                Assert.AreSame(generator, first.settings.asteroidSettings,
                    "Generator templates must stay shared; only the Field Settings asset is cloned.");
                Assert.AreSame(catalog, first.settings.asteroidSettings.catalog);
                Assert.AreEqual(87, first.settings.asteroidCount);
                Assert.AreEqual(AsteroidFormationType.BrokenBelt, first.settings.formation);
                Assert.AreEqual(AsteroidFormationType.BrokenBelt, template.formation);
                Assert.AreSame(template, AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>(
                    Temp + "/Template.asset"));

                first.settings.asteroidCount = 333;
                Assert.AreEqual(87, second.settings.asteroidCount);
                Assert.AreEqual(87, template.asteroidCount);
            }
            finally
            {
                if (!string.IsNullOrEmpty(pathA)) AssetDatabase.DeleteAsset(pathA);
                if (!string.IsNullOrEmpty(pathB)) AssetDatabase.DeleteAsset(pathB);
            }
        }

        [Test]
        public void IceShelfReceivesItsOwnFormationAndDoesNotChangeRockTemplate()
        {
            first = AsteroidAuthoringWorkflow.CreateSpawnPoint(template, MiningFieldContent.IceShelf);
            string path = AssetDatabase.GetAssetPath(first.settings);
            try
            {
                Assert.AreEqual(MiningFieldContent.IceShelf, first.contentType);
                Assert.AreEqual(AsteroidFormationType.IceShelfContinents, first.formation);
                Assert.AreEqual(AsteroidFormationType.IceShelfContinents, first.settings.formation);
                Assert.AreEqual(AsteroidFormationType.BrokenBelt, template.formation);
            }
            finally
            {
                if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void FirstTimeSetupCanReuseItsAlreadyUniqueFieldSettings()
        {
            first = AsteroidAuthoringWorkflow.CreateSpawnPoint(template,
                MiningFieldContent.Rock, reuseFirstSetupAsset: true);
            Assert.AreSame(template, first.settings);
        }
    }
}
