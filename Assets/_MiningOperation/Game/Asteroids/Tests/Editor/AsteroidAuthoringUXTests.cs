using NUnit.Framework;
using UnityEngine;
using MiningOperation.Asteroids.Editor;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidAuthoringUXTests
    {
        private GameObject go;
        private AsteroidFieldSpawnPoint anchor;
        private AsteroidResourceCatalog catalog;
        private AsteroidGeneratorSettings generator;
        private AsteroidFieldSettings field;

        [SetUp]
        public void SetUp()
        {
            catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            generator.volumeSubdivisions = 2;
            field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.asteroidSettings = generator;
            go = new GameObject("UX test spawn");
            anchor = go.AddComponent<AsteroidFieldSpawnPoint>();
            anchor.settings = field;
            anchor.fieldId = "UX-TEST-0A233";
            anchor.fieldSeed = 10293L;
            anchor.fixedCount = 4;
            anchor.radiusMeters = 10000f;
            anchor.thicknessMeters = 2000f;
            anchor.SyncToController();
        }

        [TearDown]
        public void TearDown()
        {
            AsteroidAuthoringWorkflow.CancelBuild();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(field);
            Object.DestroyImmediate(generator);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void SignatureTracksLayoutAndResourceInputsButNotRenderingSettings()
        {
            string initial = AsteroidAuthoringWorkflow.Signature(anchor);
            anchor.formation = AsteroidFormationType.Stream;
            Assert.AreNotEqual(initial, AsteroidAuthoringWorkflow.Signature(anchor));
            anchor.formation = AsteroidFormationType.CrescentBelt;
            Assert.AreEqual(initial, AsteroidAuthoringWorkflow.Signature(anchor));
            generator.rareChanceClassII = 0.75f;
            Assert.AreNotEqual(initial, AsteroidAuthoringWorkflow.Signature(anchor));
            generator.rareChanceClassII = 0.25f;
            Assert.AreEqual(initial, AsteroidAuthoringWorkflow.Signature(anchor));
            field.detailedDistanceMeters += 500f;
            Assert.AreEqual(initial, AsteroidAuthoringWorkflow.Signature(anchor));
        }

        [Test]
        public void BuildMetadataDetectsChangedCountAndKeepsSavedFieldUntilExplicitRebuild()
        {
            anchor.Controller.ForceRebuildImmediately();
            string signature = AsteroidAuthoringWorkflow.Signature(anchor);
            anchor.LastBuildSignature = signature;
            Assert.IsFalse(AsteroidAuthoringWorkflow.IsOutdated(anchor));
            int oldCount = anchor.Controller.Snapshot.asteroids.Count;
            anchor.fixedCount = 9;
            Assert.IsTrue(AsteroidAuthoringWorkflow.IsOutdated(anchor));
            Assert.AreEqual(oldCount, anchor.Controller.Snapshot.asteroids.Count);
        }

        [Test]
        public void PreviewIsNonDestructive()
        {
            anchor.Controller.ForceRebuildImmediately();
            var snapshot = anchor.Controller.Snapshot;
            var originalMass = snapshot.asteroids[0].asteroid.remainingMassKg;
            var placements = AsteroidFieldLayout.Plan(field, anchor.fieldSeed, anchor.fieldId,
                anchor.BuildOptions(field));
            Assert.Greater(placements.Count, 0);
            Assert.AreSame(snapshot, anchor.Controller.Snapshot);
            Assert.AreEqual(originalMass, snapshot.asteroids[0].asteroid.remainingMassKg);
        }
    }
}
