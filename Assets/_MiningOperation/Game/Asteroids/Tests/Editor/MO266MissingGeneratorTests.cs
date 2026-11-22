using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Asteroids.Tests
{
    public sealed class MO266MissingGeneratorTests
    {
        [Test]
        public void SavedSnapshot_DebrisInit_WithoutGenerator_DoesNotThrowOrChangeRecords()
        {
            var go = new GameObject("Missing-Generator-Regression-Test");
            var field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.enableDecorativeDebris = true;
            field.asteroidSettings = null;
            field.iceGeneratorSettings = null;

            var snapshot = new AsteroidFieldSnapshot
            {
                fieldId = "recover-existing-belt",
                fieldSeed = 109L,
                contentType = MiningFieldContent.Rock,
                asteroids = new List<AsteroidFieldRecord> { new AsteroidFieldRecord { index = 7 } }
            };
            object renderer = null;
            try
            {
                var rendererType = typeof(AsteroidFieldController).Assembly.GetType(
                    "MiningOperation.Asteroids.AsteroidDebrisRenderer", throwOnError: true);
                renderer = Activator.CreateInstance(rendererType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new object[] { go.transform, field }, null);
                var initialize = rendererType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(initialize);
                Assert.DoesNotThrow(() => initialize.Invoke(renderer, new object[] { snapshot }));
                Assert.AreEqual(1, snapshot.asteroids.Count);
                Assert.AreEqual(7, snapshot.asteroids[0].index);
                Assert.AreEqual("recover-existing-belt", snapshot.fieldId);
            }
            finally
            {
                (renderer as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(field);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
