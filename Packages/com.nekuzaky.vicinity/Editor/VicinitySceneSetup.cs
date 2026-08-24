using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    internal struct SetupResult
    {
        internal int Equipped;
        internal int Candidates;
        internal int SkippedHandEdited;
        internal long TotalBytes;
    }

    internal static class VicinitySceneSetup
    {
        internal const string ManagerObjectName = "Vicinity Manager";
        internal const string TargetObjectName = "Vicinity Target";

        internal static VicinityManager FindManager()
        {
            return Object.FindFirstObjectByType<VicinityManager>(FindObjectsInactive.Include);
        }

        internal static VicinityTarget FindTarget()
        {
            return Object.FindFirstObjectByType<VicinityTarget>(FindObjectsInactive.Include);
        }

        [MenuItem("Tools/Vicinity/Set Up This Scene", false, 1)]
        internal static void SetUpSceneFromMenu()
        {
            SetupResult result = SetUpScene(false);

            string message = result.Equipped == 0
                ? "Vicinity found nothing new to manage in this scene."
                : $"Vicinity now manages {result.Equipped} objects, {VicinityEditorStyles.DescribeBytes(result.TotalBytes)} of models.";

            if (result.SkippedHandEdited > 0)
            {
                message += $" {result.SkippedHandEdited} objects you configured by hand were left untouched.";
            }

            EditorUtility.DisplayDialog("Vicinity", message, "Done");
        }

        internal static VicinityManager EnsureManager()
        {
            VicinityManager existing = FindManager();
            if (existing != null)
            {
                return existing;
            }

            GameObject host = new GameObject(ManagerObjectName);
            Undo.RegisterCreatedObjectUndo(host, "Create Vicinity Manager");

            VicinityManager manager = Undo.AddComponent<VicinityManager>(host);
            Selection.activeGameObject = host;
            return manager;
        }

        internal static VicinityTarget CreateTarget()
        {
            VicinityTarget existing = FindTarget();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return existing;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                VicinityTarget onCamera = Undo.AddComponent<VicinityTarget>(camera.gameObject);
                Selection.activeGameObject = camera.gameObject;
                return onCamera;
            }

            GameObject host = new GameObject(TargetObjectName);
            Undo.RegisterCreatedObjectUndo(host, "Create Vicinity Target");

            VicinityTarget target = Undo.AddComponent<VicinityTarget>(host);
            Selection.activeGameObject = host;
            return target;
        }

        internal static SetupResult SetUpScene(bool overwriteHandEdited)
        {
            Undo.SetCurrentGroupName("Set up Vicinity in this scene");
            int group = Undo.GetCurrentGroup();

            VicinityManager manager = EnsureManager();

            if (FindTarget() == null)
            {
                CreateTarget();
            }

            List<ScanCandidate> candidates = VicinitySceneScanner.Scan();
            SetupResult result = new SetupResult
            {
                SkippedHandEdited = overwriteHandEdited ? 0 : VicinitySceneScanner.CountHandEdited(candidates)
            };

            foreach (ScanCandidate candidate in candidates)
            {
                if (candidate.Selected)
                {
                    result.TotalBytes += candidate.EstimatedBytes;
                }
            }

            result.Equipped = VicinitySceneScanner.Apply(candidates, overwriteHandEdited);
            result.Candidates = candidates.Count;

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = manager.gameObject;

            return result;
        }

        internal static VicinityVolume CreateVolume()
        {
            GameObject host = new GameObject("Vicinity Volume");
            Undo.RegisterCreatedObjectUndo(host, "Create Vicinity Volume");

            VicinityVolume volume = Undo.AddComponent<VicinityVolume>(host);
            Selection.activeGameObject = host;
            return volume;
        }
    }
}
