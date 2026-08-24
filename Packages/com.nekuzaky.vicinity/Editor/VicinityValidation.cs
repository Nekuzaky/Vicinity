using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    internal enum IssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class ValidationIssue
    {
        internal IssueSeverity Severity;
        internal string Title;
        internal string Explanation;
        internal UnityEngine.Object Context;
        internal string FixLabel;
        internal Action Fix;
    }

    internal static class VicinityValidation
    {
        #region Main Methods

        internal static List<ValidationIssue> Collect()
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            VicinityObject[] managedObjects = UnityEngine.Object.FindObjectsByType<VicinityObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            CheckManagerPresence(issues, managedObjects);
            CheckTargetPresence(issues, managedObjects);
            CheckManagedObjects(issues, managedObjects);
            CheckRealtimeGlobalIllumination(issues, managedObjects);
            CheckProfiles(issues);
            CheckOverlappingVolumes(issues);

            issues.Sort(static (left, right) => right.Severity.CompareTo(left.Severity));
            return issues;
        }

        #endregion

        #region Privates

        private static void CheckManagerPresence(List<ValidationIssue> issues, VicinityObject[] managedObjects)
        {
            if (managedObjects.Length == 0 || VicinitySceneSetup.FindManager() != null)
            {
                return;
            }

            issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Error,
                Title = "This scene has managed objects but no manager",
                Explanation = "Nothing will ever load. A manager drives every managed object in the scene, and one is enough.",
                FixLabel = "Add a manager",
                Fix = static () => VicinitySceneSetup.EnsureManager()
            });
        }

        private static void CheckTargetPresence(List<ValidationIssue> issues, VicinityObject[] managedObjects)
        {
            if (managedObjects.Length == 0 || VicinitySceneSetup.FindTarget() != null)
            {
                return;
            }

            issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Warning,
                Title = "No viewpoint in this scene",
                Explanation = "Vicinity measures distances from a viewpoint. Without one it falls back to whichever camera happens to be active, which is fragile in a project with several cameras.",
                FixLabel = "Add a viewpoint",
                Fix = static () => VicinitySceneSetup.CreateTarget()
            });
        }

        private static void CheckManagedObjects(List<ValidationIssue> issues, VicinityObject[] managedObjects)
        {
            foreach (VicinityObject managed in managedObjects)
            {
                if (managed == null)
                {
                    continue;
                }

                CheckMissingModel(issues, managed);
                CheckMargin(issues, managed);
                CheckStandInEqualsDetailedModel(issues, managed);
                CheckLevelOrder(issues, managed);
                CheckGpuResidentDrawerEligibility(issues, managed);
            }
        }

        private static void CheckMissingModel(List<ValidationIssue> issues, VicinityObject managed)
        {
            if (!managed.HasMissingModel)
            {
                return;
            }

            issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Error,
                Title = $"'{managed.name}' has no detailed model",
                Explanation = "This object is marked as managed but points at nothing, so it will never load anything and only costs evaluation time.",
                Context = managed,
                FixLabel = "Remove Vicinity from it",
                Fix = () => Undo.DestroyObjectImmediate(managed)
            });
        }

        private static void CheckMargin(List<ValidationIssue> issues, VicinityObject managed)
        {
            if (!managed.HasInvalidMargin)
            {
                return;
            }

            issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Error,
                Title = $"'{managed.name}' releases before it loads",
                Explanation = "The releasing distance is not larger than the loading distance. A player walking back and forth across that boundary would load and release this object continuously.",
                Context = managed,
                FixLabel = "Set a safe releasing distance",
                Fix = () =>
                {
                    Undo.RecordObject(managed, "Fix Vicinity distances");
                    SerializedObject serialized = new SerializedObject(managed);
                    SerializedProperty unload = serialized.FindProperty("m_unloadDistance");
                    unload.floatValue = managed.LoadDistance * SafeMarginRatio;
                    serialized.ApplyModifiedProperties();
                }
            });
        }

        private static void CheckStandInEqualsDetailedModel(List<ValidationIssue> issues, VicinityObject managed)
        {
            if (managed.HasMissingModel || managed.DetailedModel.SourceKind != AssetSourceKind.DirectReference)
            {
                return;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(managed.gameObject);
            if (source == null || source != managed.DetailedModel.DirectReference)
            {
                return;
            }

            issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Info,
                Title = $"'{managed.name}' loads a copy of what is already there",
                Explanation = "Its detailed model is the very prefab it was made from, so loading gains nothing. Point it at a higher detail version, and leave a lighter mesh in the scene as the stand-in.",
                Context = managed
            });
        }

        private static void CheckLevelOrder(List<ValidationIssue> issues, VicinityObject managed)
        {
            if (!managed.HasUnorderedLevels)
            {
                return;
            }

            issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Error,
                Title = $"'{managed.name}' has quality steps in the wrong order",
                Explanation = "Steps must go from closest to furthest, each distance larger than the one before it. As set, at least one step covers no distance at all and will never be used.",
                Context = managed
            });
        }

        private static void CheckGpuResidentDrawerEligibility(List<ValidationIssue> issues, VicinityObject managed)
        {
            List<GpuInstancingExclusion> exclusions = new List<GpuInstancingExclusion>();
            GpuInstancingEligibility.Collect(managed.gameObject, exclusions);

            foreach (GpuInstancingExclusion exclusion in exclusions)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Title = $"'{managed.name}' is excluded from the GPU Resident Drawer",
                    Explanation = $"Unity drops it from GPU instancing because {exclusion.Reason}. It will cost a draw call of its own. Vicinity itself never uses property blocks or per-instance callbacks, for exactly this reason.",
                    Context = exclusion.Context != null ? exclusion.Context : managed
                });
            }
        }

        private static void CheckRealtimeGlobalIllumination(List<ValidationIssue> issues, VicinityObject[] managedObjects)
        {
            if (managedObjects.Length == 0 || !GpuInstancingEligibility.SceneUsesRealtimeGlobalIllumination())
            {
                return;
            }

            issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Warning,
                Title = "This scene uses realtime global illumination",
                Explanation = "The GPU Resident Drawer only supports baked lighting, so every object Vicinity loads here falls back to a draw call of its own.",
                FixLabel = "Open Lighting settings",
                Fix = static () => EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting")
            });
        }

        private static void CheckProfiles(List<ValidationIssue> issues)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:VicinityProfile"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                VicinityProfile profile = AssetDatabase.LoadAssetAtPath<VicinityProfile>(path);

                if (profile == null || !profile.HasInvalidMargin)
                {
                    continue;
                }

                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Title = $"Profile '{profile.name}' releases before it loads",
                    Explanation = "Its releasing distance is not larger than its loading distance. Every object using this profile would load and release continuously on the boundary.",
                    Context = profile,
                    FixLabel = "Set a safe releasing distance",
                    Fix = () =>
                    {
                        Undo.RecordObject(profile, "Fix Vicinity profile");
                        SerializedObject serialized = new SerializedObject(profile);
                        serialized.FindProperty("m_unloadDistance").floatValue = profile.LoadDistance * SafeMarginRatio;
                        serialized.ApplyModifiedProperties();
                    }
                });
            }
        }

        private static void CheckOverlappingVolumes(List<ValidationIssue> issues)
        {
            VicinityVolume[] volumes = UnityEngine.Object.FindObjectsByType<VicinityVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < volumes.Length; i++)
            {
                for (int j = i + 1; j < volumes.Length; j++)
                {
                    if (!Overlaps(volumes[i], volumes[j]))
                    {
                        continue;
                    }

                    if (volumes[i].Profile == volumes[j].Profile || volumes[i].Priority != volumes[j].Priority)
                    {
                        continue;
                    }

                    VicinityVolume second = volumes[j];

                    issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Title = $"'{volumes[i].name}' and '{second.name}' overlap with different settings",
                        Explanation = "Both cover the same space with different profiles and the same priority, so which one wins for the objects in between is arbitrary. Give one of them a higher priority.",
                        Context = volumes[i],
                        FixLabel = "Give the second volume priority",
                        Fix = () =>
                        {
                            Undo.RecordObject(second, "Set Vicinity volume priority");
                            SerializedObject serialized = new SerializedObject(second);
                            serialized.FindProperty("m_priority").intValue += 1;
                            serialized.ApplyModifiedProperties();
                        }
                    });
                }
            }
        }

        private static bool Overlaps(VicinityVolume left, VicinityVolume right)
        {
            return left != null && right != null && left.WorldBounds.Intersects(right.WorldBounds);
        }

        private const float SafeMarginRatio = 1.4f;

        #endregion
    }
}
