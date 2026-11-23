using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>A purely visual cloud. No inventory, extraction or gas deposits.</summary>
    [Serializable]
    public sealed class MOGasCloudRecord
    {
        public string id;
        public int seed;
        public Vector3 localCenter;
        public float radiusMeters;
        [Range(0f, 1f)] public float visualDensity01 = 0.75f;
    }

    [Serializable]
    public sealed class MOGasFieldSnapshot
    {
        // Legacy v2.3.x schema 1 included mass data. It is accepted on import, but not saved.
        public int schemaVersion = 2;
        public string fieldId;
        public long fieldSeed;
        public string authoringSignature;
        public List<MOGasCloudRecord> clouds = new List<MOGasCloudRecord>();
    }

    /// <summary>
    /// Procedural visual environment only. Gas usable by gameplay is manufactured elsewhere.
    /// The field is deterministic from identity, seed and layout parameters.
    /// </summary>
    public static class MOGasFieldLayout
    {
        public static string BuildSignature(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null) return string.Empty;
            string data = string.Format(CultureInfo.InvariantCulture,
                "gas-visual-v233|{0}|{1}|{2:R}|{3:R}|{4}|{5:R}",
                spawn.fieldId, spawn.fieldSeed, spawn.radiusMeters, spawn.thicknessMeters,
                Mathf.Clamp(spawn.gasCloudCount, 1, 20), spawn.gasCloudRadiusMeters);
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < data.Length; i++)
                {
                    hash ^= data[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("X16", CultureInfo.InvariantCulture);
            }
        }

        public static MOGasFieldSnapshot Plan(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));
            if (string.IsNullOrWhiteSpace(spawn.fieldId))
                throw new InvalidOperationException("Gas visual location needs a Field ID.");
            int count = Mathf.Clamp(spawn.gasCloudCount, 1, 20);
            float fieldRadius = Mathf.Max(100f, spawn.radiusMeters);
            float cloudRadius = Mathf.Max(50f, spawn.gasCloudRadiusMeters);
            var map = new MOGasFieldSnapshot
            {
                fieldId = spawn.fieldId,
                fieldSeed = spawn.fieldSeed,
                authoringSignature = BuildSignature(spawn)
            };
            for (int i = 0; i < count; i++)
            {
                int seed = AsteroidIdUtility.DeriveSeed(spawn.fieldSeed, i + 7937);
                var rng = new AsteroidRandom(unchecked((uint)seed));
                float r = fieldRadius * Mathf.Sqrt(rng.Value()) * 0.85f;
                float angle = rng.Range(0f, Mathf.PI * 2f);
                float height = rng.Range(-0.5f, 0.5f) * Mathf.Max(50f, spawn.thicknessMeters);
                map.clouds.Add(new MOGasCloudRecord
                {
                    id = spawn.fieldId + "-G" + i.ToString("D3", CultureInfo.InvariantCulture),
                    seed = seed,
                    localCenter = new Vector3(Mathf.Cos(angle) * r, height, Mathf.Sin(angle) * r),
                    radiusMeters = cloudRadius * rng.Range(0.65f, 1.35f),
                    visualDensity01 = rng.Range(0.58f, 1f)
                });
            }
            return map;
        }

        public static void Validate(MOGasFieldSnapshot data)
        {
            if (data == null || data.schemaVersion != 2 ||
                string.IsNullOrWhiteSpace(data.fieldId) || data.fieldId.Length > 100 ||
                data.clouds == null || data.clouds.Count < 1 || data.clouds.Count > 20)
                throw new InvalidOperationException("Invalid gas visual layout.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cloud in data.clouds)
            {
                if (cloud == null || string.IsNullOrWhiteSpace(cloud.id) || !ids.Add(cloud.id) ||
                    !Finite(cloud.radiusMeters) || cloud.radiusMeters < 1f ||
                    !Finite(cloud.localCenter.x) || !Finite(cloud.localCenter.y) ||
                    !Finite(cloud.localCenter.z) || !Finite(cloud.visualDensity01) ||
                    cloud.visualDensity01 < 0f || cloud.visualDensity01 > 1f)
                    throw new InvalidOperationException("Invalid gas cloud visual record.");
            }
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        /// <summary>
        /// Reads v2.3.x gas JSON and ignores its obsolete resource/mass fields.
        /// No production inventory is ever imported from the old gas snapshot.
        /// </summary>
        public static MOGasFieldSnapshot FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Empty gas JSON.", nameof(json));
            MOGasFieldSnapshot data;
            try { data = JsonUtility.FromJson<MOGasFieldSnapshot>(json); }
            catch (Exception ex) { throw new InvalidOperationException("Invalid gas JSON.", ex); }
            if (data == null) throw new InvalidOperationException("Missing gas JSON data.");
            if (data.schemaVersion == 1)
            {
                data.schemaVersion = 2;
                // Old signature included gas mass. Never compare that with the new visual signature.
                data.authoringSignature = string.Empty;
                if (data.clouds != null)
                    foreach (var cloud in data.clouds)
                        if (cloud != null && cloud.visualDensity01 <= 0f) cloud.visualDensity01 = 0.75f;
            }
            Validate(data);
            return data;
        }
    }

    /// <summary>
    /// One reusable particle renderer per gas spawn point. No colliders, gas mining,
    /// physics bodies or per-cloud GameObjects. A soft-sprite pseudo-volume, not raymarching.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AsteroidFieldSpawnPoint))]
    public sealed class MOGasFieldRenderer : MonoBehaviour
    {
        [SerializeField] private MOGasFieldSnapshot snapshot;
        private ParticleSystem system;
        private Mesh generatedVolumeMesh;
        private const string ChildName = "MO Gas Visuals";
        private const string VolumeChildName = "MO Gas Volume";
        private float nextVisibilityCheck;
        private bool isVolumeActive;

        public bool IsVolumeActive => isVolumeActive;

        public MOGasFieldSnapshot Snapshot => snapshot;
        public int CloudCount => snapshot != null && snapshot.clouds != null ? snapshot.clouds.Count : 0;

        private void Start()
        {
            if (!Application.isPlaying) return;
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            if (anchor == null || anchor.contentType != MiningFieldContent.Gas)
            {
                SyncVisualVisibility();
                return;
            }
            if (snapshot == null || snapshot.clouds == null || snapshot.clouds.Count == 0)
                BuildRecords();
            else RebuildVisuals();
        }

        public void SyncVisualVisibility()
        {
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            bool show = anchor != null && anchor.contentType == MiningFieldContent.Gas;
            SetChildVisible(ChildName, show && !isVolumeActive);
            SetChildVisible(VolumeChildName, show && isVolumeActive);
        }

        private void SetChildVisible(string name, bool visible)
        {
            Transform child = transform.Find(name);
            if (child != null && child.gameObject.activeSelf != visible)
                child.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || Time.unscaledTime < nextVisibilityCheck) return;
            nextVisibilityCheck = Time.unscaledTime + 0.4f;
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            if (anchor == null || anchor.contentType != MiningFieldContent.Gas)
            {
                SetChildVisible(ChildName, false);
                SetChildVisible(VolumeChildName, false);
                return;
            }
            // Large clouds may touch the view even when their center is far away.
            Camera camera = Camera.main;
            if (camera == null) return;
            float drawDistance = Mathf.Max(100f, anchor.gasVisibilityDistanceMeters);
            bool visible = false;
            if (snapshot != null && snapshot.clouds != null)
            {
                foreach (var cloud in snapshot.clouds)
                {
                    bool ground = anchor.gasPresentation == MOGasPresentation.GroundFog;
                    float worldRadius = ground
                        ? cloud.radiusMeters * anchor.groundFogRadiusMultiplier
                        : cloud.radiusMeters * MaxScale(transform.lossyScale);
                    Vector3 worldCenter = transform.TransformPoint(cloud.localCenter);
                    if (ground) worldCenter.y = anchor.groundFogFloorY + anchor.groundFogHeightMeters * 0.5f;
                    if (Vector3.Distance(camera.transform.position, worldCenter) <= drawDistance + worldRadius)
                    {
                        visible = true;
                        break;
                    }
                }
            }
            SetChildVisible(ChildName, visible && !isVolumeActive);
            SetChildVisible(VolumeChildName, visible && isVolumeActive);
        }

        private static float MaxScale(Vector3 scale) =>
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        public void BuildRecords()
        {
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            if (anchor == null) throw new InvalidOperationException("Gas visuals require a Spawn Point.");
            snapshot = MOGasFieldLayout.Plan(anchor);
            RebuildVisuals();
        }

        private void UpgradeLegacySnapshotIfNeeded()
        {
            if (snapshot != null && snapshot.schemaVersion == 1)
                snapshot = MOGasFieldLayout.FromJson(JsonUtility.ToJson(snapshot));
        }

        // Optional export for authoring. No gas depletion data is stored or required.
        public string CaptureJson()
        {
            UpgradeLegacySnapshotIfNeeded();
            MOGasFieldLayout.Validate(snapshot);
            return JsonUtility.ToJson(snapshot);
        }

        public void LoadJson(string json)
        {
            snapshot = MOGasFieldLayout.FromJson(json);
            RebuildVisuals();
        }

        public void RebuildVisuals()
        {
            UpgradeLegacySnapshotIfNeeded();
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            if (anchor == null || anchor.contentType != MiningFieldContent.Gas || snapshot == null)
            {
                SyncVisualVisibility();
                return;
            }
            MOGasFieldLayout.Validate(snapshot);
            Material volume = anchor.visuals != null && anchor.visuals.gasVolumeMaterialOverride != null
                ? anchor.visuals.gasVolumeMaterialOverride
                : anchor.settings != null ? anchor.settings.gasVolumeMaterial : null;
            bool groundMode = anchor.gasPresentation == MOGasPresentation.GroundFog;
            if (groundMode)
            {
                volume = anchor.visuals != null && anchor.visuals.groundFogMaterialOverride != null
                    ? anchor.visuals.groundFogMaterialOverride
                    : anchor.settings != null ? anchor.settings.groundFogMaterial : null;
            }
            bool requestedVolume = anchor.gasPresentation != MOGasPresentation.SoftParticles;
            bool volumeValid = volume != null && volume.shader != null && volume.shader.isSupported &&
                volume.shader.name == (groundMode ? "MiningOperation/GroundFogURP" : "MiningOperation/GasVolumeURP");
            if (requestedVolume && volumeValid)
            {
                BuildVolumeVisuals(anchor, volume, groundMode);
                RemoveGeneratedChild(ChildName);
                isVolumeActive = true;
                SyncVisualVisibility();
                return;
            }
            if (requestedVolume)
                Debug.LogWarning("[MO] Volumetric gas shader/material unavailable. Falling back to stable URP particles. " +
                    "Install / Repair Ice & Gas Assets in Field Studio.", this);
            Material material = anchor.visuals != null && anchor.visuals.gasParticleMaterialOverride != null
                ? anchor.visuals.gasParticleMaterialOverride
                : anchor.settings != null ? anchor.settings.gasParticleMaterial : null;
            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                isVolumeActive = false;
                SetChildVisible(ChildName, false);
                SetChildVisible(VolumeChildName, false);
                Debug.LogWarning("[MO] Gas material missing/unsupported. Use Field Studio > VISUALS > Install Ice & Gas Assets.", this);
                return;
            }
            isVolumeActive = false;
            RemoveGeneratedChild(VolumeChildName);
            Transform child = transform.Find(ChildName);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(ChildName);
                go.transform.SetParent(transform, false);
            }
            else go = child.gameObject;
            if (!go.activeSelf) go.SetActive(true);
            system = go.GetComponent<ParticleSystem>();
            if (system == null) system = go.AddComponent<ParticleSystem>();

            int puffs = Mathf.Clamp(anchor.gasPuffsPerCloud, 24, 200);
            int maxCount = Mathf.Clamp(snapshot.clouds.Count * puffs, 24, 4000);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.maxParticles = maxCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var noise = system.noise;
            noise.enabled = anchor.gasDriftStrength > 0.001f;
            if (noise.enabled)
            {
                // Slow, subtle cloud motion with bounded displacement. No per-frame mesh generation.
                noise.strength = new ParticleSystem.MinMaxCurve(Mathf.Min(30f,
                    anchor.gasCloudRadiusMeters * 0.035f * anchor.gasDriftStrength));
                noise.frequency = 0.06f;
                noise.scrollSpeed = 0.04f;
                noise.damping = true;
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) throw new InvalidOperationException("ParticleSystemRenderer is missing.");
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var particles = new List<ParticleSystem.Particle>(maxCount);
            foreach (var cloud in snapshot.clouds)
            {
                var rng = new AsteroidRandom(unchecked((uint)cloud.seed));
                for (int i = 0; i < puffs; i++)
                {
                    // Outer veil, denser core and intermediate wisps overlap in 3D.
                    int layer = i % 3;
                    float shell = layer == 0 ? 0.42f : layer == 1 ? 0.75f : 1.0f;
                    Vector3 dir = rng.UnitVector();
                    float r = Mathf.Pow(Mathf.Max(0.0001f, rng.Value()), layer == 0 ? 0.8f : 0.45f) * shell;
                    Vector3 offset = new Vector3(dir.x, dir.y * 0.70f, dir.z) * (r * cloud.radiusMeters);
                    float colorMix = Mathf.Clamp01(rng.Value() * 0.85f + 0.08f);
                    Color color = Color.Lerp(anchor.gasColorA, anchor.gasColorB, colorMix);
                    float alphaScale = layer == 0 ? 0.62f : layer == 1 ? 0.48f : 0.27f;
                    float irregular = Mathf.Lerp(1f, rng.Range(0.48f, 1f), anchor.gasLayerContrast);
                    color.a = Mathf.Clamp01(anchor.gasOpacity * alphaScale * cloud.visualDensity01 * irregular);
                    float size = cloud.radiusMeters * (layer == 0 ?
                        rng.Range(0.42f, 0.76f) : rng.Range(0.55f, 1.15f));
                    particles.Add(new ParticleSystem.Particle
                    {
                        position = cloud.localCenter + offset,
                        startColor = color,
                        startSize = size,
                        startLifetime = 100000f,
                        remainingLifetime = 100000f,
                        rotation = rng.Range(0f, 360f),
                        randomSeed = rng.NextUInt() | 1u
                    });
                }
            }
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (Application.isPlaying) system.Play(true);
            system.SetParticles(particles.ToArray(), particles.Count);
            // The system has no emission: all gas billboards come from SetParticles.
        }

        private void BuildVolumeVisuals(AsteroidFieldSpawnPoint anchor, Material volumeMaterial, bool groundMode)
        {
            Transform existing = transform.Find(VolumeChildName);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(VolumeChildName);
                go.transform.SetParent(transform, false);
            }
            else go = existing.gameObject;
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null) filter = go.AddComponent<MeshFilter>();
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = go.AddComponent<MeshRenderer>();

            var oldMesh = filter.sharedMesh;
            var newMesh = groundMode
                ? MOGroundFogMeshBuilder.Build(snapshot, transform, anchor.groundFogFloorY,
                    anchor.groundFogHeightMeters, anchor.groundFogRadiusMultiplier)
                : MOGasVolumeMeshBuilder.Build(snapshot);
            newMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            filter.sharedMesh = newMesh;
            generatedVolumeMesh = newMesh;
            if (oldMesh != null && oldMesh != newMesh &&
                (oldMesh.name == MOGasVolumeMeshBuilder.GeneratedMeshName ||
                 oldMesh.name == MOGroundFogMeshBuilder.GeneratedMeshName) &&
                (oldMesh.hideFlags & HideFlags.DontSaveInEditor) != 0)
                DestroyGeneratedMesh(oldMesh);

            renderer.sharedMaterial = volumeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_GasColorA", anchor.gasColorA);
            properties.SetColor("_GasColorB", anchor.gasColorB);
            properties.SetFloat("_GasOpacity", anchor.gasOpacity);
            properties.SetFloat("_GasContrast", anchor.gasLayerContrast);
            properties.SetFloat("_GasNoiseScale", anchor.gasVolumeNoiseScale);
            properties.SetFloat("_GasBrightness", anchor.gasVolumeBrightness);
            properties.SetFloat("_GasDrift", anchor.gasDriftStrength);
            properties.SetFloat("_GasSteps", anchor.gasVolumeSteps);
            if (groundMode)
            {
                properties.SetFloat("_FogTopSoftness", anchor.groundFogTopSoftness);
                var lightPositions = new Vector4[4];
                var lightColors = new Vector4[4];
                int count = 0;
                if (anchor.groundFogLights != null)
                    foreach (var light in anchor.groundFogLights)
                    {
                        if (count >= 4) break;
                        if (light == null || !light.enabled || !light.gameObject.activeInHierarchy) continue;
                        if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                        var p = light.transform.position;
                        lightPositions[count] = new Vector4(p.x, p.y, p.z, Mathf.Max(1f, light.range));
                        Color c = light.color * Mathf.Min(12f, Mathf.Max(0f, light.intensity) * 0.15f);
                        lightColors[count] = new Vector4(c.r, c.g, c.b, 1f);
                        count++;
                    }
                properties.SetVectorArray("_FogLightPosRange", lightPositions);
                properties.SetVectorArray("_FogLightColors", lightColors);
                properties.SetInt("_FogLightCount", count);
            }
            Light sun = RenderSettings.sun;
            Vector3 towardLight = sun != null ? -sun.transform.forward :
                new Vector3(-0.42f, 0.71f, -0.56f).normalized;
            Color lightColor = sun != null ? sun.color * Mathf.Clamp(sun.intensity, 0.5f, 1.6f) : Color.white;
            properties.SetVector("_GasLightDirection", new Vector4(towardLight.x, towardLight.y, towardLight.z, 0f));
            properties.SetColor("_GasLightColor", lightColor);
            renderer.SetPropertyBlock(properties);
            go.layer = gameObject.layer;
            SetChildVisible(ChildName, false);
        }

        // Keep only one visual renderer per gas location, including after switching modes.
        private void RemoveGeneratedChild(string name)
        {
            Transform item = transform.Find(name);
            if (item == null) return;
            item.gameObject.SetActive(false);
            if (name == VolumeChildName)
            {
                var filter = item.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null &&
                    (filter.sharedMesh.name == MOGasVolumeMeshBuilder.GeneratedMeshName ||
                     filter.sharedMesh.name == MOGroundFogMeshBuilder.GeneratedMeshName) &&
                    (filter.sharedMesh.hideFlags & HideFlags.DontSaveInEditor) != 0)
                {
                    Mesh existing = filter.sharedMesh;
                    filter.sharedMesh = null;
                    if (generatedVolumeMesh == existing) generatedVolumeMesh = null;
                    DestroyGeneratedMesh(existing);
                }
            }
            if (Application.isPlaying) UnityEngine.Object.Destroy(item.gameObject);
            else UnityEngine.Object.DestroyImmediate(item.gameObject);
        }

        private static void DestroyGeneratedMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
            else UnityEngine.Object.DestroyImmediate(mesh);
        }

        private void OnDestroy()
        {
            if (generatedVolumeMesh != null)
            {
                DestroyGeneratedMesh(generatedVolumeMesh);
                generatedVolumeMesh = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            UpgradeLegacySnapshotIfNeeded();
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            if (anchor == null || anchor.contentType != MiningFieldContent.Gas) return;
            MOGasFieldSnapshot map = snapshot;
            if (map == null)
            {
                if (string.IsNullOrWhiteSpace(anchor.fieldId)) return;
                map = MOGasFieldLayout.Plan(anchor);
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.35f, 0.8f, 0.98f, 0.25f);
            if (map.clouds != null)
                foreach (var cloud in map.clouds)
                    Gizmos.DrawWireSphere(cloud.localCenter, cloud.radiusMeters);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
