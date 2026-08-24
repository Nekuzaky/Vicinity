using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    [CustomEditor(typeof(VicinityVolume))]
    [CanEditMultipleObjects]
    internal sealed class VicinityVolumeEditor : UnityEditor.Editor
    {
        #region Unity API

        private void OnEnable()
        {
            _boxHandle = new BoxBoundsHandle { handleColor = VicinityEditorStyles.VolumeColor, wireframeColor = VicinityEditorStyles.VolumeColor };
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Objects inside this box use the profile below instead of the manager's. Use it when one area of the level needs different distances, for example a cramped interior inside an open landscape.",
                MessageType.None);

            DrawDefaultInspector();

            if (target is VicinityVolume volume && volume.Profile == null)
            {
                EditorGUILayout.HelpBox("No profile assigned, so this volume changes nothing. Objects inside it fall back to the manager's settings.", MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            if (!VicinityEditorStyles.GizmosVisible || target is not VicinityVolume volume)
            {
                return;
            }

            Transform owner = volume.transform;

            using (new Handles.DrawingScope(Matrix4x4.TRS(owner.position, owner.rotation, Vector3.one)))
            {
                _boxHandle.center = volume.Center;
                _boxHandle.size = volume.Size;

                EditorGUI.BeginChangeCheck();
                _boxHandle.DrawHandle();

                if (!EditorGUI.EndChangeCheck())
                {
                    return;
                }

                Undo.RecordObject(volume, "Resize Vicinity Volume");
                volume.SetBox(_boxHandle.center, _boxHandle.size);
                EditorUtility.SetDirty(volume);
            }
        }

        #endregion

        #region Privates

        private BoxBoundsHandle _boxHandle;

        #endregion
    }
}
