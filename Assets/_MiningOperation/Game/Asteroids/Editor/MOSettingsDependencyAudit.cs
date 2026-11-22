#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Read-only dependency inspection: no asset creation, no relinking by name,
    /// no changes to GUIDs, resource catalogs or saved field snapshots.
    /// </summary>
    internal static class MOSettingsDependencyAudit
    {
        [MenuItem("Tools/Mining Operation/Audit Field Settings Dependencies", priority = 212)]
        public static void Audit()
        {
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Mining Operation", "Wait for Unity to finish compiling, then run the audit again.", "OK");
                return;
            }

            int problems = 0;
            int inspected = 0;
            Object firstBroken = null;
            var summary = new StringBuilder();
            var seen = new HashSet<AsteroidFieldSettings>();

            string[] guids = AssetDatabase.FindAssets("t:AsteroidFieldSettings");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var field = AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>(path);
                if (field == null) continue; // Broken m_Script links are handled by the separate script repair tool.
                if (!seen.Add(field)) continue;
                inspected++;
                CheckField(field, path, ref problems, ref firstBroken, summary);
            }

            var spawnPoints = Object.FindObjectsByType<AsteroidFieldSpawnPoint>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var spawn in spawnPoints)
            {
                if (spawn == null) continue;
                var field = spawn.settings;
                if (field == null)
                {
                    Report("Spawn Point '" + spawn.name + "' has no Field Settings assigned.",
                        spawn, ref problems, ref firstBroken, summary);
                }
                else if (seen.Add(field))
                {
                    inspected++;
                    CheckField(field, "Spawn Point '" + spawn.name + "'", ref problems, ref firstBroken, summary);
                }
            }

            if (firstBroken != null)
            {
                Selection.activeObject = firstBroken;
                EditorGUIUtility.PingObject(firstBroken);
            }
            if (problems == 0)
            {
                Debug.Log("[MO Dependency Audit] Inspected " + inspected +
                    " Field Settings. No missing mandatory generator/catalog links found.");
                EditorUtility.DisplayDialog("Mining Operation: dependencies", "No missing mandatory generator/catalog links were found in " + inspected + " Field Settings assets. If runtime still fails, check the first Console error and verify your active Spawn Point.", "OK");
                return;
            }
            Debug.LogError("[MO Dependency Audit] Found " + problems + " missing mandatory references:\n" + summary);
            EditorUtility.DisplayDialog("Mining Operation: missing references",
                "Found " + problems + " missing mandatory references in " + inspected +
                " Field Settings assets.\n\nThe first affected asset was selected in Project/Hierarchy. " +
                "Read the Console audit for exact fields.\n\n" +
                "Restore links to your EXISTING Generator Settings and Resource Catalog assets. " +
                "This audit deliberately does not create assets or change the saved asteroid state.", "OK");
        }

        private static void CheckField(AsteroidFieldSettings field, string label,
            ref int problems, ref Object firstBroken, StringBuilder summary)
        {
            if (field.asteroidSettings == null)
            {
                Report(label + ": Field Settings '" + field.name + "' is missing its mandatory Asteroid Settings (rock generator) reference.",
                    field, ref problems, ref firstBroken, summary);
                return;
            }
            if (field.asteroidSettings.catalog == null)
            {
                Report(label + ": Generator Settings '" + field.asteroidSettings.name +
                    "' is missing its mandatory Resource Catalog reference.",
                    field.asteroidSettings, ref problems, ref firstBroken, summary);
            }
            // The ice generator is optional for rock-only fields; an absent ice generator
            // is not reported as a mandatory failure, and existing assets are left alone.
            if (field.iceGeneratorSettings != null && field.iceGeneratorSettings.catalog == null)
            {
                Report(label + ": Ice Generator Settings '" + field.iceGeneratorSettings.name +
                    "' has no Resource Catalog. Fix before rebuilding an ice field.",
                    field.iceGeneratorSettings, ref problems, ref firstBroken, summary);
            }
        }

        private static void Report(string message, Object context,
            ref int problems, ref Object firstBroken, StringBuilder summary)
        {
            problems++;
            if (firstBroken == null) firstBroken = context;
            summary.AppendLine("• " + message);
        }
    }
}
#endif
