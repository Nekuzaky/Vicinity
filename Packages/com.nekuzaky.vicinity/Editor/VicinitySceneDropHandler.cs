using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nekuzaky.Vicinity.Editor
{
    /// <summary>
    /// Lets an artist drag a model straight from the Project window into the Scene view and get a managed
    /// object, instead of having to convert it first and remember to place the copy. Only steps in on a scene
    /// that already has a manager — that is what says this scene is meant to stream.
    /// </summary>
    [InitializeOnLoad]
    internal static class VicinitySceneDropHandler
    {
        #region Unity API

        static VicinitySceneDropHandler()
        {
            // The static constructor runs again on every domain reload, and Unity keeps handlers across one.
            DragAndDrop.RemoveDropHandlerV2(Handler);
            DragAndDrop.AddDropHandlerV2(Handler);
        }

        #endregion

        #region Main Methods

        /// <summary>Whether dropping an asset into the Scene view hands it to Vicinity. Per user, not per project.</summary>
        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        #endregion

        #region Privates

        private const string EnabledKey = "Nekuzaky.Vicinity.TakeOverSceneDrops";
        private static readonly DragAndDrop.SceneDropHandler Handler = OnSceneDrop;

        private static DragAndDropVisualMode OnSceneDrop(
            Object dropUpon,
            Vector3 worldPosition,
            Vector2 viewportPosition,
            Transform parentForDraggedObjects,
            bool perform)
        {
            if (!Enabled || VicinitySceneSetup.FindManager() == null)
            {
                return DragAndDropVisualMode.None;
            }

            List<GameObject> sources = Convertible();

            if (sources.Count == 0)
            {
                return DragAndDropVisualMode.None;
            }

            if (perform)
            {
                Place(sources, worldPosition, parentForDraggedObjects);
            }

            return DragAndDropVisualMode.Copy;
        }

        private static List<GameObject> Convertible()
        {
            List<GameObject> sources = new List<GameObject>();

            foreach (string path in DragAndDrop.paths)
            {
                if (string.IsNullOrEmpty(path) || AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(GameObject))
                {
                    continue;
                }

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                // Anything Vicinity refuses — a prefab it already manages, an object that draws nothing —
                // falls through to Unity's own handling rather than being blocked.
                if (asset != null && VicinityPrefabFactory.CanConvert(asset, out _))
                {
                    sources.Add(asset);
                }
            }

            return sources;
        }

        private static void Place(List<GameObject> sources, Vector3 worldPosition, Transform parent)
        {
            Undo.SetCurrentGroupName(sources.Count == 1 ? "Place Vicinity object" : "Place Vicinity objects");
            int group = Undo.GetCurrentGroup();

            List<Object> placed = new List<Object>(sources.Count);
            int converted = 0;

            foreach (GameObject source in sources)
            {
                PrefabConversion conversion = VicinityPrefabFactory.Ensure(source);

                if (!conversion.Succeeded)
                {
                    Debug.LogWarning($"Vicinity could not take over '{source.name}': {conversion.Problem}");
                    continue;
                }

                GameObject instance = Instantiate(conversion.Result, parent);

                if (instance == null)
                {
                    continue;
                }

                instance.transform.position = worldPosition;
                Undo.RegisterCreatedObjectUndo(instance, "Place Vicinity object");

                placed.Add(instance);

                if (!conversion.ReplacedExisting)
                {
                    converted++;
                }
            }

            Undo.CollapseUndoOperations(group);

            if (placed.Count == 0)
            {
                return;
            }

            Selection.objects = placed.ToArray();
            Announce(placed.Count, converted);
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            if (parent != null)
            {
                return PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            }

            Scene scene = SceneManager.GetActiveScene();
            return PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        }

        private static void Announce(int placed, int converted)
        {
            // A drop that quietly places something other than what was dragged would be a nasty surprise,
            // so say what happened. The notification fades on its own and blocks nothing.
            string what = placed == 1 ? "It streams now" : $"{placed} objects stream now";
            string how = converted > 0 ? ", and a Vicinity prefab was made for it" : string.Empty;

            SceneView.lastActiveSceneView?.ShowNotification(
                new GUIContent($"{what}{how}. Turn this off in the Vicinity dashboard."),
                1.6d);
        }

        #endregion
    }
}
