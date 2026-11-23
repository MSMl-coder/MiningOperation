using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Read-only diagnosis of why field-generated asteroids appear less lit
    /// than separately placed custom meshes. Does not touch materials or saves.
    /// </summary>
    public static class MOAsteroidLightingAudit
    {
        private const string Command = "Tools/Mining Operation/Diagnostics/Asteroid Lighting Report";

        [MenuItem(Command)]
        public static void Run()
        {
            GameObject selected = Selection.activeGameObject;
            AsteroidFieldSpawnPoint spawn = selected != null
                ? selected.GetComponentInParent<AsteroidFieldSpawnPoint>() : null;
            if (spawn == null)
            {
                EditorUtility.DisplayDialog("Asteroid lighting report",
                    "Select Rock Spawn Point (or its generated asteroid child) in the Hierarchy, " +
                    "then open " + Command + ". This tool does not modify your scene.", "OK");
                return;
            }

            var report = new StringBuilder(4096);
            report.AppendLine("[MO Lighting Audit] " + spawn.name);
            report.AppendLine("Pipeline: " + (GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name : "Built-in / unassigned"));
            report.AppendLine("Content: " + spawn.contentType);
            report.AppendLine("Ambient mode: " + RenderSettings.ambientMode +
                "; ambient color: " + RenderSettings.ambientLight);
            report.AppendLine("RenderSettings.sun: " +
                (RenderSettings.sun != null ? RenderSettings.sun.name : "<not explicitly assigned>"));

            Material selectedSurface = MOBakedMaterialSync.ResolveSelectedMaterial(spawn);
            report.AppendLine("Selected shared/override asteroid material: " + Describe(selectedSurface));
            if (spawn.visuals != null)
                report.AppendLine("Local surface material override: " +
                    Describe(spawn.visuals.unifiedAsteroidMaterialOverride));
            if (spawn.settings != null)
            {
                report.AppendLine("Shared Unified: " + Describe(spawn.settings.unifiedAsteroidMaterial));
                report.AppendLine("Shared Near: " + Describe(spawn.settings.asteroidMaterial));
                report.AppendLine("Shared Far: " + Describe(spawn.settings.distantMaterial));
                report.AppendLine("Experimental material toggle: " +
                    spawn.settings.allowExperimentalRockShaders);
            }

            if (selectedSurface != null && selectedSurface.HasProperty("_AmbientFill"))
            {
                report.AppendLine("Custom-shader ambient fill: " +
                    selectedSurface.GetFloat("_AmbientFill").ToString("F3"));
                if (selectedSurface.HasProperty("_DarkSideFill"))
                    report.AppendLine("Dark-side fill: " +
                        selectedSurface.GetFloat("_DarkSideFill").ToString("F3"));
                if (selectedSurface.HasProperty("_LightWrap"))
                    report.AppendLine("Light wrap: " +
                        selectedSurface.GetFloat("_LightWrap").ToString("F3"));
                report.AppendLine("Large ambient/dark-side fill can flatten the terminator, " +
                    "even when a valid directional light exists.");
            }

            var centers = new List<Vector3>(3);
            var controller = spawn.Controller;
            if (controller != null && controller.Snapshot != null &&
                controller.Snapshot.asteroids != null)
            {
                int count = controller.Snapshot.asteroids.Count;
                report.AppendLine("Runtime field records: " + count);
                for (int i = 0; i < count && centers.Count < 3; ++i)
                {
                    var rec = controller.Snapshot.asteroids[i];
                    if (rec != null)
                        centers.Add(spawn.transform.TransformPoint(rec.localPosition));
                }
            }
            if (centers.Count == 0) centers.Add(spawn.transform.position);

            var lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int directionalCount = 0, enabledDirectionalCount = 0, pointCount = 0;
            foreach (var light in lights)
            {
                if (light == null) continue;
                bool active = light.enabled && light.gameObject.activeInHierarchy;
                if (light.type == LightType.Directional)
                {
                    directionalCount++;
                    if (active) enabledDirectionalCount++;
                    report.AppendLine("Directional: " + light.name + " active=" + active +
                        " intensity=" + light.intensity.ToString("F2") +
                        " direction=" + light.transform.forward);
                }
                else if (light.type == LightType.Point || light.type == LightType.Spot)
                {
                    pointCount++;
                    if (!active) continue;
                    float closest = float.PositiveInfinity;
                    foreach (var c in centers) closest = Mathf.Min(
                        closest, Vector3.Distance(light.transform.position, c));
                    if (closest <= light.range * 1.5f)
                        report.AppendLine(light.type + " light " + light.name +
                            " range=" + light.range.ToString("F0") +
                            " distance from sampled field rock=" + closest.ToString("F0") +
                            " (Spot lights additionally require checking cone direction).");
                }
            }
            report.AppendLine("Directional active: " + enabledDirectionalCount +
                "/" + directionalCount + "; point/spot total: " + pointCount);
            if (enabledDirectionalCount == 0)
                report.AppendLine("WARNING: no active directional light found. " +
                    "Far asteroids cannot be illuminated by a nearby limited-range Point Light.");

            if (controller != null && controller.BakedSceneRoot != null)
            {
                var baked = controller.BakedSceneRoot.GetComponentsInChildren<MeshRenderer>(true);
                report.AppendLine("Baked renderers: " + baked.Length);
                int samples = Mathf.Min(3, baked.Length);
                for (int i = 0; i < samples; ++i)
                {
                    var renderer = baked[i];
                    report.AppendLine("Baked renderer [" + i + "] " + renderer.name +
                        " enabled=" + renderer.enabled +
                        " material=" + Describe(renderer.sharedMaterial));
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                        report.AppendLine("Mesh vertices=" + filter.sharedMesh.vertexCount +
                            " normals=" + filter.sharedMesh.normals.Length);
                }
            }
            else
            {
                report.AppendLine("Unbaked/streamed field: far asteroids are drawn via " +
                    "Graphics.RenderMesh rather than individual Hierarchy MeshRenderers. " +
                    "The selected field surface material is the relevant starting point.");
            }

            report.AppendLine("DIAGNOSTIC TEST: place one generated asteroid next to the " +
                "bright custom asteroid under the same light. If it remains dull, " +
                "compare the materials and normals. If it becomes bright, check Point/Spot range.");
            Debug.Log(report.ToString(), spawn);
            EditorUtility.DisplayDialog("Asteroid lighting report",
                "The read-only report was written to Unity Console. " +
                "Copy the full '[MO Lighting Audit]' message to review the exact cause.", "OK");
        }

        private static string Describe(Material material)
        {
            if (material == null) return "<None>";
            string path = AssetDatabase.GetAssetPath(material);
            string shader = material.shader != null ? material.shader.name : "<missing shader>";
            return material.name + " | Shader=" + shader +
                " | Supported=" + (material.shader != null && material.shader.isSupported) +
                " | Asset=" + (string.IsNullOrEmpty(path) ? "<runtime/generated>" : path);
        }
    }
}
