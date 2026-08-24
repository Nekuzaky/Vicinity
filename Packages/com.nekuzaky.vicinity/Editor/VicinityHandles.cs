using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    internal static class VicinityHandles
    {
        #region Main Methods

        internal static void DrawRings(Vector3 center, float loadDistance, float unloadDistance, Color stateColor)
        {
            if (!IsWorthDrawing(center, unloadDistance))
            {
                return;
            }

            Handles.color = VicinityEditorStyles.LoadRingColor;
            Handles.DrawWireDisc(center, Vector3.up, loadDistance);

            Handles.color = VicinityEditorStyles.UnloadRingColor;
            Handles.DrawWireDisc(center, Vector3.up, unloadDistance);

            Handles.color = stateColor;
            Handles.DrawWireDisc(center, Vector3.up, MarkerRadius);
        }

        internal static void DrawDistanceHandles(
            Vector3 center,
            SerializedProperty loadDistance,
            SerializedProperty unloadDistance,
            SerializedObject serializedObject)
        {
            if (!IsWorthDrawing(center, unloadDistance.floatValue))
            {
                return;
            }

            float newLoad = DrawRadiusHandle(center, loadDistance.floatValue, Vector3.right, VicinityEditorStyles.LoadRingColor, "Loads at");
            float newUnload = DrawRadiusHandle(center, unloadDistance.floatValue, Vector3.forward, VicinityEditorStyles.UnloadRingColor, "Releases at");

            if (Mathf.Approximately(newLoad, loadDistance.floatValue) && Mathf.Approximately(newUnload, unloadDistance.floatValue))
            {
                return;
            }

            loadDistance.floatValue = Mathf.Max(0f, newLoad);
            unloadDistance.floatValue = Mathf.Max(loadDistance.floatValue + MinimumMargin, newUnload);
            serializedObject.ApplyModifiedProperties();
        }

        internal static void DrawLevelRings(VicinityObject managed, Color stateColor)
        {
            Vector3 center = managed.transform.position;
            int levelCount = managed.LevelCount;
            float outermost = levelCount > 0 ? managed.GetLevel(levelCount - 1).Range : 0f;

            if (!IsWorthDrawing(center, outermost))
            {
                return;
            }

            for (int level = 0; level < levelCount; level++)
            {
                float range = managed.GetLevel(level).Range;
                Handles.color = level == 0 ? VicinityEditorStyles.LoadRingColor : VicinityEditorStyles.UnloadRingColor;
                Handles.DrawWireDisc(center, Vector3.up, range);
                Handles.Label(center + Vector3.right * range, $"Step {level + 1} to {range:0.#} m");
            }

            Handles.color = stateColor;
            Handles.DrawWireDisc(center, Vector3.up, MarkerRadius);
        }

        internal static void DrawVolumeBox(VicinityVolume volume)
        {
            Bounds bounds = volume.WorldBounds;

            if (!IsWorthDrawing(bounds.center, bounds.extents.magnitude))
            {
                return;
            }

            Handles.color = VicinityEditorStyles.VolumeColor;
            Handles.DrawWireCube(bounds.center, bounds.size);
        }

        #endregion

        #region Privates

        private const float MarkerRadius = 0.35f;
        private const float MinimumMargin = 1f;
        private const float HandleScreenScale = 0.06f;
        private const float MaximumDrawDistance = 4000f;

        private static bool IsWorthDrawing(Vector3 center, float radius)
        {
            Camera sceneCamera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            if (sceneCamera == null)
            {
                return true;
            }

            float distance = Vector3.Distance(sceneCamera.transform.position, center);
            return distance - radius < MaximumDrawDistance;
        }

        private static float DrawRadiusHandle(Vector3 center, float radius, Vector3 direction, Color color, string label)
        {
            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.up, radius);

            Vector3 handlePosition = center + direction * radius;
            float handleSize = HandleUtility.GetHandleSize(handlePosition) * HandleScreenScale;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(handlePosition, direction, handleSize, Handles.DotHandleCap, 0f);

            if (EditorGUI.EndChangeCheck())
            {
                radius = Vector3.Dot(moved - center, direction);
            }

            Handles.Label(handlePosition, $"{label} {Mathf.Max(0f, radius):0.#} m");
            return radius;
        }

        #endregion
    }
}
