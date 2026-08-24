using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    internal static class VicinityEditorStyles
    {
        internal const string GizmoVisibilityKey = "Nekuzaky.Vicinity.ShowGizmos";

        internal static readonly Color LoadRingColor = new Color(0.35f, 0.78f, 1f, 0.9f);
        internal static readonly Color UnloadRingColor = new Color(1f, 0.62f, 0.24f, 0.9f);
        internal static readonly Color VolumeColor = new Color(0.45f, 0.9f, 0.6f, 0.9f);

        internal static readonly Color UnloadedColor = new Color(0.45f, 0.45f, 0.48f, 1f);
        internal static readonly Color QueuedColor = new Color(0.95f, 0.83f, 0.3f, 1f);
        internal static readonly Color LoadingColor = new Color(1f, 0.55f, 0.15f, 1f);
        internal static readonly Color ResidentColor = new Color(0.35f, 0.85f, 0.45f, 1f);
        internal static readonly Color FailedColor = new Color(0.95f, 0.32f, 0.32f, 1f);

        internal static bool GizmosVisible
        {
            get => EditorPrefs.GetBool(GizmoVisibilityKey, true);
            set => EditorPrefs.SetBool(GizmoVisibilityKey, value);
        }

        internal static Color ColorForState(ResidencyState state)
        {
            return state switch
            {
                ResidencyState.Queued => QueuedColor,
                ResidencyState.Loading => LoadingColor,
                ResidencyState.Resident => ResidentColor,
                ResidencyState.Unloading => LoadingColor,
                ResidencyState.Failed => FailedColor,
                _ => UnloadedColor
            };
        }

        internal static Texture ErrorIcon => LoadIcon("console.erroricon");

        internal static Texture WarningIcon => LoadIcon("console.warnicon");

        internal static Texture InfoIcon => LoadIcon("console.infoicon");

        internal static Texture SupportIcon => LoadIcon("Favorite");

        internal static string DescribeBytes(long bytes)
        {
            if (bytes <= 0L)
            {
                return "unknown";
            }

            if (bytes < 1024L * 1024L)
            {
                return $"{bytes / 1024f:0.#} KB";
            }

            return $"{bytes / (1024f * 1024f):0.##} MB";
        }

        private static Texture LoadIcon(string iconName)
        {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            return content == null ? null : content.image;
        }
    }
}
