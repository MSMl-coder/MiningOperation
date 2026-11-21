#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Diagnoses/restores missing MonoScript PPtr references in known Mining Operation
    /// ScriptableObject YAML assets. Handles null {fileID: 0} as well as normal GUID refs.
    /// Does not guess when the type, script or reference cannot be identified.
    /// </summary>
    internal static class MOSettingsScriptRepair
    {
        private enum Kind { Unknown, Catalog, Generator, Field }

        private struct ScriptTarget
        {
            public Kind kind;
            public Type type;
            public string fileName;
            public string guid;
            public long fileId;
        }

        private struct RepairCandidate
        {
            public string path;
            public Kind kind;
            public string original;
            public string modified;
            public string oldReference;
            public string newReference;
        }

        // The script value can be {fileID: 0}, {fileID: 11500000, guid: ..., type: 3},
        // or another one-line YAML map. Do not require a GUID in order to recognize it.
        private static readonly Regex ScriptLine = new Regex(
            @"(?m)^(?<prefix>[ \t]*m_Script[ \t]*:[ \t]*)(?<value>\{[^\r\n}]*\})[ \t]*\r?$",
            RegexOptions.Compiled);
        private static readonly Regex GuidInRef = new Regex(@"\bguid\s*:\s*(?<value>[0-9a-fA-F]{32})\b", RegexOptions.Compiled);
        private static readonly Regex FileIdInRef = new Regex(@"\bfileID\s*:\s*(?<value>-?\d+)\b", RegexOptions.Compiled);
        private static readonly Regex AnyScriptLine = new Regex(@"(?m)^[ \t]*m_Script\s*:.*$", RegexOptions.Compiled);
        private static readonly Regex ClassIdentifier = new Regex(@"(?m)^[ \t]*m_EditorClassIdentifier\s*:\s*(?<value>[^\r\n]*)", RegexOptions.Compiled);

        [MenuItem("Tools/Mining Operation/Check Settings Asset Scripts", priority = 210)]
        public static void Check()
        {
            if (!Collect(out var candidates, out var messages, out int classified, out int unresolved)) return;
            foreach (string message in messages) Debug.LogWarning("[MO Asset Check] " + message);
            foreach (var candidate in candidates)
                Debug.LogWarning($"[MO Asset Check] Broken: {candidate.path}: {candidate.oldReference} -> {candidate.newReference}");
            string summary = $"Recognized: {classified}\nRepair candidates: {candidates.Count}\nUnresolved: {unresolved}";
            if (unresolved > 0)
                summary += "\n\nSome assets could not be inspected. Check the Console; no automatic fix will be attempted for those assets.";
            else if (candidates.Count == 0)
                summary += "\n\nNo incorrect m_Script references found among inspected assets. If the Inspector still reports Missing Script, check compilation, assembly definitions and m_EditorClassIdentifier.";
            else
                summary += "\n\nUse Tools > Mining Operation > Repair Missing Settings Scripts to back up and repair recognized broken references.";
            Debug.Log($"[MO Asset Check] Recognized {classified}; broken {candidates.Count}; unresolved {unresolved}.");
            EditorUtility.DisplayDialog("Mining Operation - Asset script audit", summary, "OK");
        }

        [MenuItem("Tools/Mining Operation/Repair Missing Settings Scripts", priority = 211)]
        public static void Repair()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Mining Operation", "Exit Play Mode and finish script compilation first.", "OK");
                return;
            }
            if (!Collect(out var candidates, out var messages, out int classified, out int unresolved)) return;
            foreach (string message in messages) Debug.LogWarning("[MO Asset Repair] " + message);
            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog("Mining Operation", unresolved > 0
                    ? $"No automatically repairable references. {unresolved} assets require manual inspection. See Console."
                    : "No incorrect m_Script references detected. Check compilation and class identifiers if an asset still reports Missing Script.", "OK");
                return;
            }
            var summary = new StringBuilder();
            foreach (var candidate in candidates)
                summary.AppendLine(candidate.path + " (" + candidate.kind + ")");
            if (unresolved > 0)
                summary.AppendLine($"WARNING: {unresolved} assets could not be inspected and will not be modified.");
            if (!EditorUtility.DisplayDialog("Repair Mining Operation settings scripts?",
                    $"Repair {candidates.Count} verified broken YAML m_Script references?\n\n" +
                    "A backup of each .asset and .asset.meta is created under Library/MiningOperationAssetBackups.\n" +
                    "Serialized gameplay fields are not changed.\n\n" + summary,
                    "Back up and repair", "Cancel")) return;

            string root = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Project root not found.");
            string backupRoot = Path.Combine(root, "Library", "MiningOperationAssetBackups",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
            int changed = 0;
            try
            {
                foreach (var candidate in candidates)
                {
                    string absolute = Path.Combine(root, candidate.path.Replace('/', Path.DirectorySeparatorChar));
                    string backup = Path.Combine(backupRoot, candidate.path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(absolute) || File.ReadAllText(absolute) != candidate.original)
                    {
                        Debug.LogWarning("[MO Asset Repair] File changed during scan; skipped: " + candidate.path);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Copy(absolute, backup, false);
                    if (File.Exists(absolute + ".meta")) File.Copy(absolute + ".meta", backup + ".meta", false);
                    File.WriteAllText(absolute, candidate.modified, new UTF8Encoding(false));
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            Debug.Log($"[MO Asset Repair] Repaired {changed}; unresolved {unresolved}. Backups: {backupRoot}");
            EditorUtility.DisplayDialog("Mining Operation", $"Repaired {changed} script references.\nUnresolved: {unresolved}.\nBackups: {backupRoot}\n\nInspect the assets, then save the project and restart Unity to confirm persistence.", "OK");
        }

        private static bool Collect(out List<RepairCandidate> candidates, out List<string> messages,
            out int recognized, out int unresolved)
        {
            candidates = new List<RepairCandidate>();
            messages = new List<string>();
            recognized = 0;
            unresolved = 0;
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Mining Operation", "Unity is compiling. Resolve any compiler errors first.", "OK");
                return false;
            }
            var targets = new[]
            {
                new ScriptTarget { kind = Kind.Catalog, type = typeof(AsteroidResourceCatalog), fileName = "AsteroidResourceCatalog.cs" },
                new ScriptTarget { kind = Kind.Generator, type = typeof(AsteroidGeneratorSettings), fileName = "AsteroidGeneratorSettings.cs" },
                new ScriptTarget { kind = Kind.Field, type = typeof(AsteroidFieldSettings), fileName = "AsteroidFieldSettings.cs" }
            };
            for (int i = 0; i < targets.Length; i++)
            {
                if (!FindTarget(targets[i].fileName, targets[i].type, out string guid, out long fileId, out string issue))
                {
                    EditorUtility.DisplayDialog("Mining Operation", issue + "\n\nDo not repair until the correct scripts compile.", "OK");
                    return false;
                }
                targets[i].guid = guid;
                targets[i].fileId = fileId;
            }
            string directory = Path.Combine(Application.dataPath, "_MiningOperation");
            if (!Directory.Exists(directory))
            {
                EditorUtility.DisplayDialog("Mining Operation", "Assets/_MiningOperation is missing.", "OK");
                return false;
            }
            foreach (string file in Directory.GetFiles(directory, "*.asset", SearchOption.AllDirectories))
            {
                string path = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
                string yaml;
                try { yaml = File.ReadAllText(file); }
                catch (Exception ex) { unresolved++; messages.Add(path + ": unreadable (" + ex.Message + ")"); continue; }
                if (!yaml.StartsWith("%YAML", StringComparison.Ordinal))
                {
                    // The inspector may show a valid object, but binary serialization is not safe to rewrite.
                    Kind guessed = Classify(null, path);
                    if (guessed != Kind.Unknown) { unresolved++; messages.Add(path + ": binary/non-YAML asset; not modified"); }
                    continue;
                }
                Kind kind = Classify(yaml, path);
                if (kind == Kind.Unknown) continue;
                recognized++;
                ScriptTarget target = default;
                foreach (var item in targets) if (item.kind == kind) { target = item; break; }
                if (target.type == null) { unresolved++; messages.Add(path + ": unknown target type"); continue; }
                Match script = ScriptLine.Match(yaml);
                Match identifier = ClassIdentifier.Match(yaml);
                string classIdentifier = identifier.Success ? identifier.Groups["value"].Value.Trim() : "(none)";
                if (!script.Success)
                {
                    Match fallback = AnyScriptLine.Match(yaml);
                    string reference = fallback.Success ? fallback.Value.Trim() : "m_Script not present";
                    unresolved++;
                    messages.Add(path + ": unrecognized script syntax [" + reference + "]; m_EditorClassIdentifier=" + classIdentifier);
                    continue;
                }
                string oldRef = script.Groups["value"].Value;
                Match oldGuidMatch = GuidInRef.Match(oldRef);
                Match oldFileMatch = FileIdInRef.Match(oldRef);
                if (!oldFileMatch.Success)
                {
                    unresolved++;
                    messages.Add(path + ": m_Script has no valid fileID: " + oldRef);
                    continue;
                }
                string oldGuid = oldGuidMatch.Success ? oldGuidMatch.Groups["value"].Value : string.Empty;
                bool sameGuid = string.Equals(oldGuid, target.guid, StringComparison.OrdinalIgnoreCase);
                bool sameFileId = long.TryParse(oldFileMatch.Groups["value"].Value, out long id) && id == target.fileId;
                UnityEngine.Object loaded = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (loaded != null && loaded.GetType() == target.type) continue;
                if (sameGuid && sameFileId)
                {
                    unresolved++;
                    messages.Add(path + ": m_Script already points to correct script " + oldRef +
                        ", but asset is not loaded as " + target.type.Name +
                        "; m_EditorClassIdentifier=" + classIdentifier + ". Check Console, script assembly and source .meta.");
                    continue;
                }
                string newRef = "{fileID: " + target.fileId + ", guid: " + target.guid + ", type: 3}";
                string modified = yaml.Substring(0, script.Groups["value"].Index) + newRef +
                    yaml.Substring(script.Groups["value"].Index + script.Groups["value"].Length);
                candidates.Add(new RepairCandidate { path = path, kind = kind,
                    original = yaml, modified = modified, oldReference = oldRef, newReference = newRef });
            }
            return true;
        }

        private static bool FindTarget(string fileName, Type expected, out string guid, out long fileId, out string issue)
        {
            guid = null; fileId = 0; issue = null;
            int matches = 0;
            foreach (string candidate in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName)))
            {
                string path = AssetDatabase.GUIDToAssetPath(candidate);
                if (!string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase)) continue;
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null || script.GetClass() != expected) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string candidateGuid, out long candidateFileId)) continue;
                guid = candidateGuid; fileId = candidateFileId; matches++;
            }
            if (matches == 1) return true;
            issue = matches == 0
                ? "No compiled MonoScript found for " + expected.FullName + " in " + fileName
                : "Multiple compiled MonoScripts found for " + expected.FullName + ". Resolve duplicates first.";
            return false;
        }

        private static Kind Classify(string yaml, string path)
        {
            // Structure wins over file name. A duplicate or renamed asset is fine.
            if (!string.IsNullOrEmpty(yaml))
            {
                if (yaml.Contains("  resources:") && !yaml.Contains("  asteroidSettings:")) return Kind.Catalog;
                if (yaml.Contains("  catalog:") && yaml.Contains("  classIIProbability:") &&
                    yaml.Contains("  maximumDiameterMeters:")) return Kind.Generator;
                if (yaml.Contains("  asteroidSettings:") && yaml.Contains("  useGpuFarField:")) return Kind.Field;
            }
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.IndexOf("ResourceCatalog", StringComparison.OrdinalIgnoreCase) >= 0) return Kind.Catalog;
            if (name.IndexOf("AsteroidGenerator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("IceGenerator", StringComparison.OrdinalIgnoreCase) >= 0) return Kind.Generator;
            if (name.IndexOf("AsteroidField", StringComparison.OrdinalIgnoreCase) >= 0) return Kind.Field;
            return Kind.Unknown;
        }
    }
}
#endif
