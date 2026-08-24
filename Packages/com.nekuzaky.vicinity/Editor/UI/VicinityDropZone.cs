using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor
{
    /// <summary>
    /// A place to drop prefabs and imported 3D models. Each one comes back as a prefab Vicinity manages,
    /// measured and given distances that suit its size, without the user setting anything.
    /// </summary>
    internal sealed class VicinityDropZone : VisualElement
    {
        #region Main Methods

        internal VicinityDropZone()
        {
            AddToClassList("vicinity-drop");
            focusable = true;

            _headline = new Label("Drop prefabs or 3D models here");
            _headline.AddToClassList("vicinity-drop__headline");
            Add(_headline);

            _detail = new Label(DefaultDetail);
            _detail.AddToClassList("vicinity-drop__detail");
            Add(_detail);

            RegisterCallback<DragEnterEvent>(OnDragEnter);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);
            RegisterCallback<DragExitedEvent>(OnDragLeave);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        /// <summary>Raised once per drop, with one result per prefab that was dropped.</summary>
        internal event Action<List<PrefabConversion>> Converted;

        /// <summary>Converts whatever is selected in the Project window, for people who would rather not drag.</summary>
        internal void ConvertSelection()
        {
            List<GameObject> prefabs = new List<GameObject>();

            foreach (UnityEngine.Object selected in Selection.objects)
            {
                Collect(AssetDatabase.GetAssetPath(selected), prefabs);
            }

            Run(prefabs);
        }

        /// <summary>How many prefabs the Project selection would convert. Drives the button next to the zone.</summary>
        internal static int CountInSelection()
        {
            List<GameObject> prefabs = new List<GameObject>();

            foreach (UnityEngine.Object selected in Selection.objects)
            {
                Collect(AssetDatabase.GetAssetPath(selected), prefabs);
            }

            return prefabs.Count;
        }

        #endregion

        #region Privates

        private const string HoverClass = "vicinity-drop--hover";
        private const string RefusedClass = "vicinity-drop--refused";
        private const string DefaultDetail = "Prefabs or 3D models — one, a pile of them, or a whole folder. You get a copy that streams itself.";

        private static readonly string[] FolderFilters = { "t:Prefab", "t:Model" };

        private readonly Label _headline;
        private readonly Label _detail;

        private void OnDragEnter(DragEnterEvent evt)
        {
            List<GameObject> prefabs = Gather();

            if (prefabs.Count > 0)
            {
                AddToClassList(HoverClass);
                _headline.text = prefabs.Count == 1
                    ? $"Take over {prefabs[0].name}"
                    : $"Take over {prefabs.Count} of them";

                return;
            }

            AddToClassList(RefusedClass);
            _headline.text = "Nothing here Vicinity can take";
            _detail.text = "Prefabs and 3D models only, and not ones Vicinity already manages.";
        }

        private void OnDragLeave(EventBase evt)
        {
            RemoveFromClassList(HoverClass);
            RemoveFromClassList(RefusedClass);
            _headline.text = "Drop prefabs or 3D models here";
            _detail.text = DefaultDetail;
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = Gather().Count > 0
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;

            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            DragAndDrop.AcceptDrag();
            OnDragLeave(evt);
            Run(Gather());
            evt.StopPropagation();
        }

        private static List<GameObject> Gather()
        {
            List<GameObject> prefabs = new List<GameObject>();

            foreach (string path in DragAndDrop.paths)
            {
                Collect(path, prefabs);
            }

            return prefabs;
        }

        private static void Collect(string path, List<GameObject> prefabs)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                string[] here = { path };

                // Prefabs and imported models are separate search filters, and both belong here.
                foreach (string filter in FolderFilters)
                {
                    foreach (string guid in AssetDatabase.FindAssets(filter, here))
                    {
                        Collect(AssetDatabase.GUIDToAssetPath(guid), prefabs);
                    }
                }

                return;
            }

            // Anything whose main asset is a GameObject: .prefab, .fbx, .obj, .blend, and the rest.
            if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(GameObject))
            {
                return;
            }

            // A folder sweep would otherwise pick up the prefabs Vicinity itself produced.
            if (Path.GetFileNameWithoutExtension(path).EndsWith(VicinityPrefabFactory.Suffix, StringComparison.Ordinal))
            {
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && VicinityPrefabFactory.CanConvert(prefab, out _) && !prefabs.Contains(prefab))
            {
                prefabs.Add(prefab);
            }
        }

        private void Run(List<GameObject> prefabs)
        {
            if (prefabs.Count == 0)
            {
                return;
            }

            List<PrefabConversion> results = new List<PrefabConversion>(prefabs.Count);

            try
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    if (prefabs.Count > 1 && EditorUtility.DisplayCancelableProgressBar(
                            "Vicinity",
                            $"Taking over {prefabs[i].name}",
                            (float)i / prefabs.Count))
                    {
                        break;
                    }

                    results.Add(VicinityPrefabFactory.Convert(prefabs[i]));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            Converted?.Invoke(results);
        }

        #endregion
    }
}
