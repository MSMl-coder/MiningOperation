using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Actual rendered scene preview, not a 2D scatter plot. No persistent GameObjects
    /// or asset changes. Lighting/sky come from the scene if installed via V2 setup.
    /// Main entities are preview meshes; smaller visual debris uses shared meshes.
    /// </summary>
    public sealed class AsteroidField3DPreview : EditorWindow
    {
        private AsteroidFieldSpawnPoint anchor;
        private AsteroidFieldSettings settings;
        private PreviewRenderUtility preview;
        private readonly List<Mesh> variants = new List<Mesh>();
        private List<AsteroidFieldPlacement> points;
        private Material material;
        private float yaw = 22f, pitch = 20f, zoom = 1.3f;
        private int previewDebris = 280;
        private string error;
        private Vector3 center;
        private float extent = 5000f;

        public static void Open()
        {
            var w = GetWindow<AsteroidField3DPreview>("Formation 3D");
            w.minSize = new Vector2(420f, 400f);
            w.Show();
        }
        public static void OpenFor(AsteroidFieldSpawnPoint point)
        {
            var w = GetWindow<AsteroidField3DPreview>("Formation 3D");
            w.anchor = point;
            w.settings = point != null ? point.settings : null;
            w.minSize = new Vector2(420f, 400f);
            w.BuildPreview();
            w.Show();
        }

        private void OnEnable()
        {
            preview = new PreviewRenderUtility();
            preview.camera.fieldOfView = 55f;
            preview.camera.farClipPlane = 200000f;
            preview.camera.nearClipPlane = 1f;
            preview.camera.clearFlags = CameraClearFlags.Skybox;
            if (preview.lights != null && preview.lights.Length >= 2)
            {
                preview.lights[0].intensity = 1.45f;
                preview.lights[0].transform.rotation = Quaternion.Euler(34f, 35f, 0f);
                preview.lights[1].intensity = 0.4f;
            }
        }
        private void OnDisable()
        {
            FreeMeshes();
            preview?.Cleanup();
            preview = null;
        }
        private void FreeMeshes()
        {
            foreach (var m in variants) if (m != null) DestroyImmediate(m);
            variants.Clear();
            if (material != null) DestroyImmediate(material);
            material = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("MINING OPERATION  /  3D FORMATION PREVIEW", EditorStyles.boldLabel);
            anchor = (AsteroidFieldSpawnPoint)EditorGUILayout.ObjectField("Spawn Point", anchor,
                typeof(AsteroidFieldSpawnPoint), true);
            settings = anchor != null ? anchor.settings : (AsteroidFieldSettings)EditorGUILayout.ObjectField(
                "Field Settings", settings, typeof(AsteroidFieldSettings), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate 3D Preview", GUILayout.Height(25))) BuildPreview();
                if (GUILayout.Button("Frame All", GUILayout.Height(25))) { yaw = 22f; pitch = 20f; zoom = 1.3f; }
            }
            previewDebris = EditorGUILayout.IntSlider("Preview fragments", previewDebris, 0, 600);
            EditorGUILayout.HelpBox("Mouse drag: orbit  |  Wheel: zoom  |  Preview never changes saved asteroid data. " +
                "The background and lights match the current scene when the V2 environment is installed.", MessageType.None);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (points != null)
                EditorGUILayout.LabelField("Mineable placements: " + points.Count +
                    "  |  Visual fragment preview: " + previewDebris, EditorStyles.miniLabel);
            Rect rect = GUILayoutUtility.GetRect(300f, 600f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (rect.width < 1f || rect.height < 1f) return;
            HandleOrbit(rect);
            if (Event.current.type == EventType.Repaint) DrawPreview(rect);
        }

        private void HandleOrbit(Rect r)
        {
            Event e = Event.current;
            if (!r.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDrag && (e.button == 0 || e.button == 1))
            {
                yaw += e.delta.x * 0.35f;
                pitch = Mathf.Clamp(pitch + e.delta.y * 0.35f, -85f, 85f);
                e.Use(); Repaint();
            }
            if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * Mathf.Exp(e.delta.y * 0.07f), 0.24f, 5.5f);
                e.Use(); Repaint();
            }
        }

        private void BuildPreview()
        {
            try
            {
                if (settings == null || settings.asteroidSettings == null)
                    throw new InvalidOperationException("Select a Spawn Point with Field Settings and Generator Settings.");
                var options = anchor != null ? anchor.BuildOptions(settings) : AsteroidFieldLayoutOptions.FromSettings(settings);
                points = AsteroidFieldLayout.Plan(settings, anchor != null ? anchor.fieldSeed : 80521L,
                    anchor != null ? anchor.fieldId : "MO-V2-PREVIEW", options);
                FreeMeshes();
                var shape = AsteroidGeometryProfile.FromSettings(settings.asteroidSettings);
                for (int i = 0; i < 12; ++i)
                {
                    Mesh mesh = AsteroidGeometryGenerator.Generate(new AsteroidData
                    {
                        generatorVersion = 3, seed = AsteroidIdUtility.DeriveSeed(31875, i),
                        diameterMeters = 1f, geometryProfile = shape
                    }, settings.asteroidSettings, i < 4 ? 2 : 1);
                    variants.Add(mesh);
                }
                Shader shader = Shader.Find("Mining Operation/Asteroid Far URP");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                material = settings.distantMaterial != null ? new Material(settings.distantMaterial) :
                    shader != null ? new Material(shader) : null;
                if (material == null) throw new InvalidOperationException("No URP material available.");
                material.enableInstancing = true;
                center = Vector3.zero;
                foreach (var p in points) center += p.localPosition;
                if (points.Count > 0) center /= points.Count;
                extent = 200f;
                foreach (var p in points) extent = Mathf.Max(extent, (p.localPosition - center).magnitude + p.nominalDiameter);
                error = null;
                Repaint();
            }
            catch (Exception ex)
            {
                FreeMeshes();
                points = null;
                error = ex.Message;
                Repaint();
            }
        }

        private void DrawPreview(Rect rect)
        {
            if (preview == null || points == null || material == null || variants.Count == 0)
            {
                EditorGUI.DrawRect(rect, new Color(0.055f, 0.11f, 0.17f));
                GUI.Label(new Rect(rect.x+20f,rect.y+20f,rect.width-40f,60f),
                    "Generate Preview to render a 3D belt", EditorStyles.whiteLabel);
                return;
            }
            preview.BeginPreview(rect, GUIStyle.none);
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            float distance = Mathf.Max(350f, extent * 1.35f * zoom);
            preview.camera.transform.position = center + orbit * new Vector3(0f, 0f, -distance);
            preview.camera.transform.LookAt(center);
            preview.camera.farClipPlane = Mathf.Max(30000f, extent * 8f);
            preview.camera.backgroundColor = new Color(0.055f, 0.11f, 0.17f);
            int limit = Mathf.Min(points.Count, 550);
            for (int j = 0; j < limit; ++j)
            {
                var p = points[j];
                int variant = (int)(unchecked((uint)p.asteroidSeed) % (uint)variants.Count);
                preview.DrawMesh(variants[variant], Matrix4x4.TRS(p.localPosition, p.localRotation,
                    Vector3.one * p.nominalDiameter), material, 0);
            }
            if (previewDebris > 0 && points.Count > 0)
            {
                int seed = anchor != null ? AsteroidIdUtility.DeriveSeed(anchor.fieldSeed, 1543) : 1543;
                var rng = new AsteroidRandom(unchecked((uint)seed));
                for (int i = 0; i < previewDebris; ++i)
                {
                    var host = points[rng.Range(0, points.Count)];
                    Vector3 dir = rng.UnitVector();
                    float radius = Mathf.Max(host.nominalDiameter * 0.75f,
                        rng.Range(25f, settings.debrisSpreadMeters * 2f));
                    Vector3 where = host.localPosition + Vector3.Scale(dir,
                        new Vector3(radius, radius * .5f, radius));
                    float diameter = Mathf.Exp(rng.Range(Mathf.Log(0.5f), Mathf.Log(12f)));
                    int variant = rng.Range(0, variants.Count);
                    preview.DrawMesh(variants[variant], Matrix4x4.TRS(where,
                        Quaternion.Euler(rng.Range(0f,360f),rng.Range(0f,360f),rng.Range(0f,360f)),
                        Vector3.one*diameter), material, 0);
                }
            }
            preview.camera.Render();
            Texture texture = preview.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }
    }
}
