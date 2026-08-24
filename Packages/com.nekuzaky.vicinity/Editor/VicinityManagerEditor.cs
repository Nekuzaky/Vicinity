using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    [CustomEditor(typeof(VicinityManager))]
    internal sealed class VicinityManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Drives every managed object in this scene. One is enough, and there is normally nothing to change here.",
                MessageType.None);

            DrawDefaultInspector();

            if (VicinitySceneSetup.FindTarget() == null)
            {
                EditorGUILayout.HelpBox("No viewpoint in this scene. Vicinity will fall back to the active camera.", MessageType.Warning);

                if (GUILayout.Button("Add a viewpoint"))
                {
                    VicinitySceneSetup.CreateTarget();
                }
            }

            if (GUILayout.Button("Open the Vicinity dashboard"))
            {
                VicinityDashboard.Open();
            }

            DrawLiveStatistics();
        }

        private void DrawLiveStatistics()
        {
            if (!Application.isPlaying || target is not VicinityManager manager)
            {
                return;
            }

            ResidencyStatistics statistics = manager.Statistics;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Right now", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Managed", statistics.Managed.ToString());
            EditorGUILayout.LabelField("Loaded", statistics.Resident.ToString());
            EditorGUILayout.LabelField("Loading", statistics.Loading.ToString());
            EditorGUILayout.LabelField("Waiting", statistics.Queued.ToString());
            EditorGUILayout.LabelField("Memory", VicinityEditorStyles.DescribeBytes(statistics.ResidentMemoryBytes));

            Repaint();
        }
    }
}
