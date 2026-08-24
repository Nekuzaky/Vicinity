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

            ResolveDistances(managed, out float loadDistance, out float unloadDistance);
            VicinityHandles.DrawRings(managed.transform.position, loadDistance, unloadDistance, stateColor);
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

        internal static void ResolveDistances(VicinityObject managed, out float loadDistance, out float unloadDistance)
        {
            if (managed.OverridesDistances)
            {
                loadDistance = managed.LoadDistance;
                unloadDistance = managed.UnloadDistance;
                return;
            }

            VicinityVolume covering = VicinityVolume.FindCovering(managed.transform.position);
            VicinityProfile profile = covering != null && covering.Profile != null
                ? covering.Profile
                : VicinitySceneSetup.FindManager()?.Profile;

            loadDistance = profile != null ? profile.LoadDistance : ResidencySettings.DefaultLoadDistance;
            unloadDistance = profile != null ? profile.UnloadDistance : ResidencySettings.DefaultUnloadDistance;
        }
    }
}
