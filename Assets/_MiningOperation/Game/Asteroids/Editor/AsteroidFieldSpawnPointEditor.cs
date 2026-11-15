using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Compact Inspector entry point: all actual authoring stays in Field Studio.</summary>
    [CustomEditor(typeof(AsteroidFieldSpawnPoint))]
    public sealed class AsteroidFieldSpawnPointEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var anchor = (AsteroidFieldSpawnPoint)target;
            EditorGUILayout.LabelField("MINING OPERATION / LOCATION", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Type", anchor.contentType.ToString());
            EditorGUILayout.LabelField("Seed", anchor.fieldSeed.ToString());
            EditorGUILayout.LabelField("Settings", anchor.settings != null ? anchor.settings.name : "Missing");
            if (anchor.contentType == MiningFieldContent.Gas)
            {
                var gas = anchor.GetComponent<MOGasFieldRenderer>();
                EditorGUILayout.LabelField("Clouds", gas != null ? gas.CloudCount.ToString() : "Not generated");
            }
            else
            {
                var controller = anchor.Controller;
                EditorGUILayout.LabelField("Asteroids", controller != null && controller.Snapshot != null ?
                    controller.Snapshot.asteroids.Count.ToString() : "Not built");
            }
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Open Environment Generator", GUILayout.Height(32f)))
                AsteroidFieldStudio.OpenFor(anchor);
            EditorGUILayout.HelpBox("Move or rotate this empty object in Scene View. All other settings and build actions are in the unified generator window.", MessageType.None);
        }
    }
}
