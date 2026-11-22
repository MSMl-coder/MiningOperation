using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Non-destructive asset migration and naming. AssetDatabase rename/move preserves GUIDs.</summary>
    public static class MOUnifiedAsteroidAssetTools
    {
        private const string Visuals = "Assets/_MiningOperation/AsteroidAuthoring/Visuals";
        private const string SharedMaterialPath = Visuals + "/MO_AsteroidUnifiedSurface.mat";
        private const string ShaderName = "Mining Operation/Asteroid Unified Surface";

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_MiningOperation"))
                AssetDatabase.CreateFolder("Assets", "_MiningOperation");
            if (!AssetDatabase.IsValidFolder("Assets/_MiningOperation/AsteroidAuthoring"))
                AssetDatabase.CreateFolder("Assets/_MiningOperation", "AsteroidAuthoring");
            if (!AssetDatabase.IsValidFolder(Visuals))
                AssetDatabase.CreateFolder("Assets/_MiningOperation/AsteroidAuthoring", "Visuals");
        }

        public static Material EnsureShared(AsteroidFieldSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Unified asteroid shader is missing/unsupported. Ensure Shaders/MO_AsteroidUnifiedSurface.shader is imported and URP is active.");
            EnsureFolder();
            Material shared = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absoluteAssetPath = Path.Combine(projectRoot ?? string.Empty, SharedMaterialPath);
            if (shared == null && File.Exists(absoluteAssetPath))
                throw new InvalidOperationException("An existing unified Material cannot load. Nothing was overwritten: " + SharedMaterialPath);
            if (shared == null)
            {
                shared = new Material(shader) { name = "MO_AsteroidUnifiedSurface", enableInstancing = true };
                AssetDatabase.CreateAsset(shared, SharedMaterialPath);
            }
            // Never replace the shader or settings of an existing material silently.
            if (shared.shader != shader)
                Debug.LogWarning("[MO] Existing unified material uses a different shader. Inspect it in Unity before replacing anything: " + SharedMaterialPath);
            if (settings.unifiedAsteroidMaterial == null)
            {
                Undo.RecordObject(settings, "Assign unified asteroid surface");
                settings.unifiedAsteroidMaterial = shared;
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            return shared;
        }

        public static Material MakeLocal(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null || spawn.settings == null)
                throw new InvalidOperationException("Select a Spawn Point with valid Field Settings.");
            EnsureFolder();
            Material source = spawn.visuals?.unifiedAsteroidMaterialOverride;
            if (source == null) source = spawn.settings.unifiedAsteroidMaterial ?? EnsureShared(spawn.settings);
            var targetPath = AssetDatabase.GenerateUniqueAssetPath(Visuals + "/" + NameFor(spawn, "Surface") + ".mat");
            var copy = new Material(source) { name = Path.GetFileNameWithoutExtension(targetPath), enableInstancing = true };
            AssetDatabase.CreateAsset(copy, targetPath);
            Undo.RecordObject(spawn, "Assign local asteroid surface");
            spawn.visuals.unifiedAsteroidMaterialOverride = copy;
            EditorUtility.SetDirty(spawn);
            AssetDatabase.SaveAssets();
            return copy;
        }

        public static string NameFor(AsteroidFieldSpawnPoint spawn, string suffix)
        {
            if (spawn == null) return "MO_Unassigned_" + suffix;
            var formation = spawn.contentType == MiningFieldContent.Gas ? "Cloud" :
                spawn.useOverrides ? spawn.formation.ToString() :
                (spawn.settings != null ? spawn.settings.formation.ToString() : "Inherited");
            string name = "MO_" + spawn.contentType + "_" + formation + "_" + spawn.gameObject.name + "_" + suffix;
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid) name = name.Replace(c, '_');
            name = name.Replace(' ', '_');
            return name.Length > 90 ? name.Substring(0, 90) : name;
        }

        public static void RenameFieldSettings(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null || spawn.settings == null)
                throw new InvalidOperationException("No linked Field Settings on the selected Spawn Point.");
            var otherAnchors = UnityEngine.Object.FindObjectsByType<AsteroidFieldSpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int users = otherAnchors.Count(a => a != null && a.settings == spawn.settings);
            if (users > 1)
                throw new InvalidOperationException("This Field Settings asset is shared by " + users +
                    " Spawn Points. A unique per-spawn name would be misleading. Keep it shared or duplicate the Field Settings explicitly first.");
            string assetPath = AssetDatabase.GetAssetPath(spawn.settings);
            if (string.IsNullOrEmpty(assetPath))
                throw new InvalidOperationException("The Field Settings asset is not a persisted .asset file.");
            string newName = NameFor(spawn, "FieldSettings");
            if (Path.GetFileNameWithoutExtension(assetPath) == newName) return;
            string error = AssetDatabase.RenameAsset(assetPath, newName); // preserves GUID and references
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Mining Operation/Audit Legacy Asteroid Materials and Textures")]
        public static void AuditLegacyAssets()
        {
            string[] known = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/_MiningOperation/", StringComparison.Ordinal))
                .Where(p =>
                {
                    string n = Path.GetFileName(p);
                    bool isMaterial = p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) &&
                        (n.StartsWith("AsteroidRock", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("AsteroidFar", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("MO_Ice_URP", StringComparison.OrdinalIgnoreCase));
                    bool isTexture = p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                        (n.StartsWith("MO_RockAlbedo_", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("MO_RockNormal_", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("MO_IceSurface_", StringComparison.OrdinalIgnoreCase));
                    return isMaterial || isTexture;
                }).ToArray();
            var report = new List<string>();
            var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                (p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))).ToArray();
            foreach (string old in known)
            {
                if (AssetDatabase.LoadMainAssetAtPath(old) == null && !File.Exists(old)) continue;
                var usedBy = new List<string>();
                foreach (string other in all)
                {
                    if (other == old) continue;
                    if (Array.IndexOf(AssetDatabase.GetDependencies(other, true), old) >= 0)
                        usedBy.Add(other);
                }
                report.Add(old + " — " + (usedBy.Count == 0 ? "no direct asset dependencies found" :
                    "REFERENCED by " + usedBy.Count + " assets: " + string.Join(", ", usedBy.Take(6))));
            }
            string output = report.Count == 0 ? "No known legacy materials or textures remain." :
                string.Join("\n\n", report);
            Debug.Log("[MO Legacy Asset Audit] Read-only report (no files deleted):\n" + output);
            EditorUtility.DisplayDialog("Asteroid legacy asset audit",
                "No assets were deleted. Details are printed to Console. Verify no scene/prefab/settings references before manual cleanup.", "OK");
        }
    }
}
