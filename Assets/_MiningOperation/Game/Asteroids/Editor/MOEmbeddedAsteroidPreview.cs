using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Small, reusable 3D sample inside Field Studio. Not another editor window.</summary>
    internal sealed class MOEmbeddedAsteroidPreview
    {
        private PreviewRenderUtility utility;
        private Mesh mesh;
        private Material material;
        private int lastSeed = int.MinValue;
        private bool lastIce;
        private bool lastShelf;
        private float lastShelfThickness;
        private float lastShelfFractures;
        private float lastShelfCollision;
        private float lastShelfRift;
        private float yaw = 25f;
        private float pitch = -18f;

        public void Initialize()
        {
            if (utility != null) return;
            utility = new PreviewRenderUtility();
            utility.camera.fieldOfView = 36f;
            utility.camera.nearClipPlane = .1f;
            utility.camera.farClipPlane = 1000f;
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = new Color(.08f, .14f, .2f);
            utility.lights[0].intensity = 1.45f;
            utility.lights[0].transform.rotation = Quaternion.Euler(35f, -45f, 0f);
            utility.lights[1].intensity = .5f;
        }

        public void Invalidate() { lastSeed = int.MinValue; }

        public void Cleanup()
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
            if (material != null) Object.DestroyImmediate(material);
            mesh = null;
            material = null;
            utility?.Cleanup();
            utility = null;
        }

        public void Draw(Rect rect, AsteroidFieldSettings settings, int seed, bool ice,
            bool shelf = false, float shelfThickness = .085f, float shelfFractures = .38f,
            float shelfCollision = .58f, float shelfRift = .48f,
            MOSpawnGeology geology = null, Material materialOverride = null)
        {
            if (utility == null) Initialize();
            if (settings == null || settings.asteroidSettings == null)
            {
                EditorGUI.DrawRect(rect, new Color(.08f, .14f, .2f));
                return;
            }
            if (mesh == null || material == null || lastSeed != seed || lastIce != ice || lastShelf != shelf ||
                !Mathf.Approximately(lastShelfThickness, shelfThickness) ||
                !Mathf.Approximately(lastShelfFractures, shelfFractures) ||
                !Mathf.Approximately(lastShelfCollision, shelfCollision) ||
                !Mathf.Approximately(lastShelfRift, shelfRift))
                Regenerate(settings, seed, ice, shelf, shelfThickness, shelfFractures, shelfCollision, shelfRift,
                    geology, materialOverride);
            if (Event.current.type == EventType.MouseDrag && rect.Contains(Event.current.mousePosition))
            {
                yaw += Event.current.delta.x * .6f;
                pitch = Mathf.Clamp(pitch + Event.current.delta.y * .6f, -80f, 80f);
                Event.current.Use();
            }
            if (Event.current.type != EventType.Repaint || mesh == null || material == null) return;
            utility.BeginPreview(rect, GUIStyle.none);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            utility.camera.transform.position = rotation * new Vector3(0f, 0f, -3.9f);
            utility.camera.transform.LookAt(Vector3.zero);
            utility.DrawMesh(mesh, Matrix4x4.identity, material, 0);
            utility.camera.Render();
            GUI.DrawTexture(rect, utility.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private void Regenerate(AsteroidFieldSettings settings, int seed, bool ice,
            bool shelf, float shelfThickness, float shelfFractures, float shelfCollision, float shelfRift,
            MOSpawnGeology geology, Material materialOverride)
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
            if (material != null) Object.DestroyImmediate(material);
            mesh = null;
            material = null;
            lastSeed = seed;
            lastIce = ice;
            lastShelf = shelf;
            lastShelfThickness = shelfThickness;
            lastShelfFractures = shelfFractures;
            lastShelfCollision = shelfCollision;
            lastShelfRift = shelfRift;
            if (shelf) pitch = -47f;
            try
            {
                var generator = ice && settings.iceGeneratorSettings != null
                    ? settings.iceGeneratorSettings : settings.asteroidSettings;
                if (shelf)
                {
                    AsteroidGeometryProfile shape = AsteroidGeometryProfile.FromSettings(generator);
                    if (geology != null) geology.ApplyTo(shape);
                    shape.iceShelf = true;
                    shape.continentalShelf = true;
                    shape.shelfThicknessRatio = Mathf.Clamp(shelfThickness, .025f, .28f);
                    shape.shelfFractureStrength = Mathf.Clamp01(shelfFractures);
                    shape.shelfCollisionStrength = Mathf.Clamp01(shelfCollision);
                    shape.shelfRiftDepth = Mathf.Clamp01(shelfRift);
                    mesh = AsteroidGeometryGenerator.Generate(new AsteroidData
                    {
                        seed = seed,
                        diameterMeters = 1.8f,
                        generatorVersion = AsteroidGeneration.IceShelfContinentalGeneratorVersion,
                        geometryProfile = shape
                    }, generator, 3);
                }
                else
                {
                    AsteroidGeometryProfile shape = AsteroidGeometryProfile.FromSettings(generator);
                    if (geology != null) geology.ApplyTo(shape);
                    mesh = AsteroidGeometryGenerator.Generate(new AsteroidData
                    {
                        seed = seed,
                        diameterMeters = 1.8f,
                        generatorVersion = AsteroidGeneration.GeneratorVersion,
                        geometryProfile = shape
                    }, generator, 3);
                }
                Material source = materialOverride != null ? materialOverride :
                    settings.unifiedAsteroidMaterial != null ? settings.unifiedAsteroidMaterial :
                    ice ? settings.iceMaterial : settings.asteroidMaterial;
                Shader shader = source != null && source.shader != null && source.shader.isSupported ? source.shader :
                    Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) return;
                material = source != null && source.shader == shader ? new Material(source) : new Material(shader);
                if (AsteroidStableMaterials.IsUnified(material))
                {
                    if (string.IsNullOrEmpty(material.GetTag("MO_SurfacePreset", false, "")))
                        material.SetFloat("_IceBlend", ice ? 1f : 0f);
                    material.SetFloat("_MO_ObjectRadius", .9f); // preview geometry diameter = 1.8m
                    material.SetFloat("_MO_SurfaceSeed", (unchecked((uint)seed) & 65535u) / 65535f);
                    material.SetFloat("_MO_LODVisibility", 1f);
                }
                else
                {
                    material.SetColor("_BaseColor", ice ? new Color(.78f,.91f,1f) : new Color(.76f,.73f,.68f));
                    if (!ice && settings.rockAlbedoTexture != null)
                        material.SetTexture("_BaseMap", settings.rockAlbedoTexture);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[MO] Preview generation: " + e.Message); }
        }
    }
}
