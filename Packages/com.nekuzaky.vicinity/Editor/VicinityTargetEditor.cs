using UnityEditor;

namespace Nekuzaky.Vicinity.Editor
{
    [CustomEditor(typeof(VicinityTarget))]
    [CanEditMultipleObjects]
    internal sealed class VicinityTargetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Vicinity measures every distance from this point. Put it on the player, or on the camera that follows them.",
                MessageType.None);

            DrawDefaultInspector();
        }
    }
}
