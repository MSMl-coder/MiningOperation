using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Test selection UI, self-contained (OnGUI works with both Input System and legacy Input Manager).
    /// Picks against persistent field records, including instanced rocks with no collider/GameObject.
    /// Uses a separate preview camera + RenderTexture, not a clone of the asteroid in the world.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class AsteroidSelectionController : MonoBehaviour
    {
        [SerializeField] private bool selectionEnabled = true;
        [SerializeField] private float maximumPickDistance = 40000f;
        [SerializeField] private int previewResolution = 256;
        private Camera mainCamera;
        private AsteroidFieldController selectedField;
        private AsteroidFieldRecord selectedRecord;
        private Camera previewCamera;
        private GameObject previewModel;
        private Mesh previewMesh;
        private RenderTexture previewTexture;
        private Texture2D ring;
        private float rotation;
        private GUIStyle heading, info, sub;
        private int previousCameraCullingMask;
        private bool changedCullingMask;
        private const int PreviewLayer = 31;

        public AsteroidFieldRecord SelectedRecord => selectedRecord;
        public void ClearSelection()
        {
            selectedField = null;
            selectedRecord = null;
            if (previewMesh != null)
            {
                if (previewModel != null)
                {
                    var filter = previewModel.GetComponent<MeshFilter>();
                    if (filter != null) filter.sharedMesh = null;
                }
                AsteroidGeneration.DestroyTemporary(previewMesh);
                previewMesh = null;
            }
        }

        private void Awake()
        {
            mainCamera = GetComponent<Camera>();
            // Keep isolated preview geometry out of the gameplay camera.
            previousCameraCullingMask = mainCamera.cullingMask;
            mainCamera.cullingMask &= ~(1 << PreviewLayer);
            changedCullingMask = true;
        }

        private void LateUpdate()
        {
            if (previewCamera == null || selectedRecord == null || previewModel == null) return;
            rotation += Time.unscaledDeltaTime * 11f;
            previewModel.transform.rotation = Quaternion.Euler(13f, rotation, -9f);
            previewCamera.Render();
        }

        private void OnGUI()
        {
            if (!selectionEnabled || mainCamera == null) return;
            Event ev = Event.current;
            Rect window = GetPanelRect();
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                ClearSelection();
                ev.Use();
            }
            if (ev.type == EventType.MouseDown && ev.button == 0 && !(selectedRecord != null && window.Contains(ev.mousePosition)))
            {
                Ray ray = mainCamera.ScreenPointToRay(new Vector3(ev.mousePosition.x,
                    Screen.height - ev.mousePosition.y, 0f));
                if (TryPick(ray, out var field, out var record))
                {
                    Select(field, record);
                    ev.Use();
                }
                else ClearSelection();
            }
            if (selectedRecord == null || selectedRecord.asteroid == null) return;
            EnsureStyles();
            GUI.Box(window, GUIContent.none);
            float offsetX = window.x + 12f;
            GUI.Label(new Rect(offsetX, window.y + 8f, window.width - 65f, 23f),
                "ASTEROID / FOCUS", heading);
            if (GUI.Button(new Rect(window.xMax - 31f, window.y + 6f, 24f, 23f), "×")) ClearSelection();
            if (selectedRecord == null) return;
            if (previewTexture != null)
            {
                Rect imageRect = new Rect(offsetX + 6f, window.y + 33f, 116f, 116f);
                GUI.DrawTexture(imageRect, previewTexture, ScaleMode.ScaleToFit, true);
                if (ring == null) ring = MakeRing();
                if (ring != null) GUI.DrawTexture(imageRect, ring, ScaleMode.StretchToFill, true);
            }
            var rock = selectedRecord.asteroid;
            float x = offsetX + 142f;
            float w = window.width - 160f;
            GUI.Label(new Rect(x, window.y + 34f, w, 24f), rock.asteroidId, heading);
            GUI.Label(new Rect(x, window.y + 61f, w, 21f),
                "CLASS " + (rock.asteroidClass == 3 ? "III" : rock.asteroidClass == 2 ? "II" : "I") + "   ·   Ø " + rock.diameterMeters.ToString("F1") + " m", info);
            GUI.Label(new Rect(x, window.y + 84f, w, 21f),
                "VOLUME  " + rock.volumeM3.ToString("N0") + " m³", sub);
            GUI.Label(new Rect(x, window.y + 106f, w, 21f),
                "MASS  " + (rock.remainingMassKg / 1000d).ToString("N1") + " / " +
                    (rock.initialMassKg / 1000d).ToString("N1") + " t", sub);
            string resources = "COMPOSITION  ";
            if (rock.composition != null)
            {
                var fractions = new List<MaterialFraction>(rock.composition);
                fractions.Sort((a,b) => b.fraction.CompareTo(a.fraction));
                for (int i = 0; i < Mathf.Min(3, fractions.Count); i++)
                {
                    if (i > 0) resources += "   ·   ";
                    resources += ResourceName(fractions[i].resourceId) + " " +
                        (fractions[i].fraction * 100d).ToString("F1") + "%";
                }
            }
            GUI.Label(new Rect(x, window.y + 129f, w, 36f), resources, sub);
        }

        private Rect GetPanelRect()
        {
            float w = Mathf.Min(665f, Screen.width - 30f);
            return new Rect((Screen.width - w) * 0.5f, 15f, w, 176f);
        }

        private void EnsureStyles()
        {
            if (heading == null) heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.95f, 0.91f) }
            };
            if (info == null) info = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, normal = { textColor = new Color(0.86f, 0.78f, 0.49f) }
            };
            if (sub == null) sub = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, wordWrap = true,
                normal = { textColor = new Color(0.72f, 0.83f, 0.85f) }
            };
        }

        public bool TryPick(Ray ray, out AsteroidFieldController bestField, out AsteroidFieldRecord bestRecord)
        {
            bestField = null;
            bestRecord = null;
            float nearest = Mathf.Max(1f, maximumPickDistance);
            var fields = FindObjectsByType<AsteroidFieldController>(FindObjectsSortMode.None);
            foreach (var field in fields)
            {
                var snapshot = field != null ? field.Snapshot : null;
                if (snapshot == null || snapshot.asteroids == null) continue;
                foreach (var record in snapshot.asteroids)
                {
                    if (record?.asteroid == null || record.asteroid.remainingMassKg <= 0d) continue;
                    Vector3 center = field.transform.TransformPoint(record.localPosition);
                    float radius = Mathf.Max(1f, record.asteroid.diameterMeters * 0.57f);
                    if (!RaySphere(ray, center, radius, out float t) || t > nearest) continue;
                    nearest = t;
                    bestField = field;
                    bestRecord = record;
                }
            }
            return bestRecord != null;
        }

        public static bool RaySphere(Ray ray, Vector3 center, float radius, out float distance)
        {
            Vector3 delta = ray.origin - center;
            float b = Vector3.Dot(delta, ray.direction);
            float c = Vector3.Dot(delta, delta) - radius * radius;
            float det = b * b - c;
            if (det < 0f) { distance = 0f; return false; }
            float root = Mathf.Sqrt(det);
            float near = -b - root;
            float far = -b + root;
            distance = near >= 0f ? near : far;
            return distance >= 0f;
        }

        private void Select(AsteroidFieldController field, AsteroidFieldRecord record)
        {
            if (selectedField == field && selectedRecord == record) return;
            ClearSelection();
            selectedField = field;
            selectedRecord = record;
            try
            {
                EnsurePreview();
                var rock = record.asteroid;
                var geometry = new AsteroidData
                {
                    seed = rock.seed, generatorVersion = rock.generatorVersion,
                    diameterMeters = rock.diameterMeters, geometryProfile = rock.geometryProfile
                };
                previewMesh = AsteroidGeometryGenerator.Generate(geometry,
                    field.Settings.asteroidSettings, 3);
                previewMesh.hideFlags = HideFlags.DontSave;
                previewModel.GetComponent<MeshFilter>().sharedMesh = previewMesh;
                previewModel.GetComponent<MeshRenderer>().sharedMaterial = field.Settings.asteroidMaterial;
                previewModel.transform.localScale = Vector3.one / Mathf.Max(1f, rock.diameterMeters);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                ClearSelection();
            }
        }

        private string ResourceName(string id)
        {
            var catalog = selectedField != null && selectedField.Settings != null &&
                          selectedField.Settings.asteroidSettings != null ?
                          selectedField.Settings.asteroidSettings.catalog : null;
            if (catalog?.resources != null)
                foreach (var item in catalog.resources)
                    if (item != null && item.id == id && !string.IsNullOrEmpty(item.displayName))
                        return item.displayName;
            return id ?? "Unknown";
        }

        private void EnsurePreview()
        {
            if (previewCamera != null) return;
            previewResolution = Mathf.Clamp(previewResolution, 128, 512);
            previewTexture = new RenderTexture(previewResolution, previewResolution, 16, RenderTextureFormat.ARGB32)
            { name = "MO Asteroid Selection Preview", hideFlags = HideFlags.DontSave };
            previewTexture.Create();
            var cameraGo = new GameObject("__MO Selection Preview Camera");
            cameraGo.hideFlags = HideFlags.HideAndDontSave;
            previewCamera = cameraGo.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.02f, 0.035f, 0.05f, 0f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.targetTexture = previewTexture;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 8f;
            previewCamera.fieldOfView = 32f;
            cameraGo.transform.position = new Vector3(0f, 0f, -2f);
            cameraGo.transform.LookAt(Vector3.zero);
            previewModel = new GameObject("__MO Selection Model");
            previewModel.hideFlags = HideFlags.HideAndDontSave;
            previewModel.layer = PreviewLayer;
            previewModel.AddComponent<MeshFilter>();
            previewModel.AddComponent<MeshRenderer>();
        }

        private static Texture2D MakeRing()
        {
            const int side = 128;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.DontSave, name = "MO Selection Ring" };
            var px = new Color32[side * side];
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float dx = (x - 63.5f) / 63.5f;
                float dy = (y - 63.5f) / 63.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float opacity = Mathf.Clamp01((r - 0.87f) * 65f) *
                                Mathf.Clamp01((0.94f - r) * 65f);
                px[y * side + x] = new Color(0.54f, 0.86f, 0.9f, opacity * 0.8f);
            }
            texture.SetPixels32(px);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            ClearSelection();
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (previewCamera != null) AsteroidGeneration.DestroyTemporary(previewCamera.gameObject);
            if (previewModel != null) AsteroidGeneration.DestroyTemporary(previewModel);
            if (previewTexture != null) { previewTexture.Release(); AsteroidGeneration.DestroyTemporary(previewTexture); }
            if (ring != null) AsteroidGeneration.DestroyTemporary(ring);
            if (mainCamera != null && changedCullingMask) mainCamera.cullingMask = previousCameraCullingMask;
        }
    }
}
