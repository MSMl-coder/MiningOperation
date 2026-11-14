using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class AsteroidV21CleanupTests
    {
        [Test]
        public void NewEnvironmentDoesNotInstallPlanet()
        {
            var profile = ScriptableObject.CreateInstance<AsteroidEnvironmentProfile>();
            try { Assert.IsFalse(profile.showPlanet); }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void NewFieldSettingsDoNotCreateMicroFragmentsByDefault()
        {
            var settings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            try
            {
                Assert.AreEqual(0, settings.microFragmentCount);
                Assert.LessOrEqual(settings.decorativeRockCount, 2000);
                Assert.IsTrue(settings.useGpuFarField);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void GraphicsSettingsDoNotAffectAsteroidDataIdentifiers()
        {
            // Snapshot/ID is intentionally independent of visual draw counts.
            var record = new AsteroidFieldSnapshot
            {
                fieldId = "unchanged", fieldSeed = 1234,
                asteroids = new System.Collections.Generic.List<AsteroidFieldRecord>()
            };
            var settings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            try
            {
                settings.decorativeRockCount = 100;
                settings.microFragmentCount = 0;
                Assert.AreEqual("unchanged", record.fieldId);
                Assert.AreEqual(1234L, record.fieldSeed);
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
