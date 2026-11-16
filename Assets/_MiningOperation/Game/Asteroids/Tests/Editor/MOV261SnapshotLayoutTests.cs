using System;
using NUnit.Framework;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MOV261SnapshotLayoutTests
    {
        // Empty fields are valid: a seed can legitimately produce zero accepted
        // bodies when placement constraints are too tight.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void ExistingLayoutVersionsCanRoundTrip(int layoutVersion)
        {
            var snapshot = new AsteroidFieldSnapshot
            {
                schemaVersion = AsteroidFieldSaveUtility.SchemaVersion,
                layoutVersion = layoutVersion,
                fieldId = "LAYOUT-COMPATIBILITY-TEST",
                fieldSeed = 12345L,
                formation = AsteroidFormationType.ClusterGroups,
                contentType = MiningFieldContent.Rock
            };
            string json = AsteroidFieldSaveUtility.ToJson(snapshot);
            AsteroidFieldSnapshot loaded = AsteroidFieldSaveUtility.FromJson(json);
            Assert.AreEqual(layoutVersion, loaded.layoutVersion);
        }

        [Test]
        public void CurrentGeneratorAndValidatorAgreeOnVersion()
        {
            Assert.AreEqual(7, AsteroidFieldLayout.LayoutVersion,
                "The natural belt layout updates the snapshot layout contract.");
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "CURRENT-LAYOUT",
                layoutVersion = AsteroidFieldLayout.LayoutVersion,
                contentType = MiningFieldContent.IceShelf,
                formation = AsteroidFormationType.IceShelfContinents
            };
            Assert.DoesNotThrow(() => AsteroidFieldSaveUtility.Validate(snapshot));
        }

        [Test]
        public void UnknownFutureVersionIsRejectedWithSpecificMessage()
        {
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "UNSUPPORTED-LAYOUT",
                layoutVersion = AsteroidFieldLayout.LayoutVersion + 1
            };
            var exception = Assert.Throws<InvalidOperationException>(
                () => AsteroidFieldSaveUtility.Validate(snapshot));
            StringAssert.Contains("Unsupported field layout version", exception.Message);
        }

        [Test]
        public void GasIsStillRejectedAsAsteroidSnapshot()
        {
            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "GAS-NOT-ASTEROIDS",
                layoutVersion = AsteroidFieldLayout.LayoutVersion,
                contentType = MiningFieldContent.Gas
            };
            Assert.Throws<InvalidOperationException>(
                () => AsteroidFieldSaveUtility.Validate(snapshot));
        }
    }
}
