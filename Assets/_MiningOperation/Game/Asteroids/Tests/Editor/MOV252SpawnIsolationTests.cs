using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public class MOV252SpawnIsolationTests
    {
        private static void DestroySafe(Object o)
        {
            if (o != null) Object.DestroyImmediate(o);
        }

        [Test]
        public void TwoSpawns_SharedAssets_ButIndependentQualityAndGeology()
        {
            var shared = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            var rock = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            var ice = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            var ga = new GameObject("Rock_A");
            var gb = new GameObject("Rock_B");
            AsteroidFieldSettings a = null, b = null;
            AsteroidGeneratorSettings ra = null, rb = null, ia = null, ib = null;
            try
            {
                shared.asteroidSettings = rock;
                shared.iceGeneratorSettings = ice;
                shared.detailedDistanceMeters = 1800f;
                shared.decorativeRockCount = 1600;
                var spawnA = ga.AddComponent<AsteroidFieldSpawnPoint>();
                var spawnB = gb.AddComponent<AsteroidFieldSpawnPoint>();
                spawnA.settings = shared;
                spawnB.settings = shared;
                Assert.IsTrue(spawnA.EnsureLocalSettings());
                Assert.IsTrue(spawnB.EnsureLocalSettings());
                spawnA.quality.detailedDistanceMeters = 6500f;
                spawnA.visuals.decorativeRockCount = 37;
                spawnA.rockGeology.craterIntensity = .92f;
                spawnB.quality.detailedDistanceMeters = 2000f;
                spawnB.visuals.decorativeRockCount = 500;
                a = spawnA.CreateResolvedSettings(out ra, out ia);
                b = spawnB.CreateResolvedSettings(out rb, out ib);
                Assert.AreEqual(6500f, a.detailedDistanceMeters);
                Assert.AreEqual(2000f, b.detailedDistanceMeters);
                Assert.AreEqual(1800f, shared.detailedDistanceMeters);
                Assert.AreEqual(37, a.decorativeRockCount);
                Assert.AreEqual(500, b.decorativeRockCount);
                Assert.AreEqual(1600, shared.decorativeRockCount);
                Assert.AreEqual(.92f, a.asteroidSettings.craterIntensity, .00001f);
                Assert.AreEqual(rock.craterIntensity, b.asteroidSettings.craterIntensity, .00001f);
                Assert.AreSame(shared.asteroidSettings.catalog, a.asteroidSettings.catalog);
            }
            finally
            {
                DestroySafe(ga); DestroySafe(gb);
                DestroySafe(ra); DestroySafe(rb); DestroySafe(ia); DestroySafe(ib);
                DestroySafe(a); DestroySafe(b);
                DestroySafe(rock); DestroySafe(ice); DestroySafe(shared);
            }
        }

        [Test]
        public void LegacySpawn_CopiesCurrentSharedValuesExactlyOnce()
        {
            var shared = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            var rock = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            var go = new GameObject("legacy");
            try
            {
                shared.asteroidSettings = rock;
                shared.rotationSpeedMultiplier = 3.5f;
                shared.clusterStrength = .33f;
                var spawn = go.AddComponent<AsteroidFieldSpawnPoint>();
                spawn.settings = shared;
                Assert.IsTrue(spawn.EnsureLocalSettings());
                Assert.AreEqual(3.5f, spawn.visuals.rotationSpeedMultiplier);
                Assert.AreEqual(.33f, spawn.clusterStrength);
                shared.rotationSpeedMultiplier = .5f;
                Assert.IsFalse(spawn.EnsureLocalSettings());
                Assert.AreEqual(3.5f, spawn.visuals.rotationSpeedMultiplier);
            }
            finally
            {
                DestroySafe(go); DestroySafe(rock); DestroySafe(shared);
            }
        }
    }
}
