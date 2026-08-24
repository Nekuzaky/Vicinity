using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    internal static class VicinityGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(VicinityObject))]
        private static void DrawManagedObject(VicinityObject managed, GizmoType gizmoType)
        {
            if (!VicinityEditorStyles.GizmosVisible)
            {
                return;
            }

            Color stateColor = VicinityEditorStyles.ColorForState(managed.State);

            if (managed.HasSeveralLevels)
            {
                VicinityHandles.DrawLevelRings(managed, stateColor);
                return;
            }

            DistancePreview preview = VicinityDistancePreview.Resolve(managed);
            VicinityHandles.DrawRings(managed.transform.position, preview.LoadDistance, preview.ReleaseDistance, stateColor);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(VicinityVolume))]
        private static void DrawVolume(VicinityVolume volume, GizmoType gizmoType)
        {
            if (!VicinityEditorStyles.GizmosVisible)
            {
                return;
            }

            VicinityHandles.DrawVolumeBox(volume);
        }

    }
}
