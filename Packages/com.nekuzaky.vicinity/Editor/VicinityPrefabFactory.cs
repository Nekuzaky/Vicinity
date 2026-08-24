using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    /// <summary>How firmly the produced prefab holds on to the model it stands for.</summary>
    internal enum ReferenceStrength
    {
        /// <summary>Named through Addressables. The only kind that actually keeps the model out of memory.</summary>
        Addressable,

        /// <summary>Named through a Resources folder. Also keeps it out of memory, but ships it whole.</summary>
        Resources,

        /// <summary>Pointed at directly. Convenient, but the scene pulls the model into memory anyway.</summary>
        Direct
    }

    /// <summary>What came out of one conversion.</summary>
    internal sealed class PrefabConversion
    {
        internal GameObject Source;
        internal GameObject Result;
        internal string ResultPath;
        internal long EstimatedBytes;
        internal float Radius;
        internal float LoadDistance;
        internal float ReleaseDistance;
        internal ReferenceStrength Strength;
        internal bool ReplacedExisting;
        internal string Problem = string.Empty;

        internal bool Succeeded => Problem.Length == 0 && Result != null;

        internal string SourceName => Source != null ? Source.name : "<removed>";
    }

    /// <summary>
    /// Turns an ordinary prefab into one Vicinity manages: the same object in the scene, but its model is
    /// named rather than held, so it only reaches memory once the player is close enough to see it.
    /// </summary>
    internal static class VicinityPrefabFactory
    {
        #region Main Methods

        /// <summary>The suffix given to produced prefabs, so they read clearly in the Project window.</summary>
        internal const string Suffix = " (Vicinity)";

        /// <summary>Whether this object can be converted, and if not, what to tell the user.</summary>
        internal static bool CanConvert(UnityEngine.Object candidate, out string reason)
        {
            reason = string.Empty;

            if (candidate is not GameObject prefab)
            {
                reason = candidate == null
                    ? "That is not a prefab."
                    : $"'{candidate.name}' is not a prefab.";

                return false;
            }

            // Persistence rather than prefab-ness, so an imported model works as well as a prefab.
            if (!EditorUtility.IsPersistent(prefab))
            {
                reason = $"'{prefab.name}' lives in a scene. Drop a prefab or a model from the Project window instead.";
                return false;
            }

            if (prefab.GetComponent<VicinityObject>() != null)
            {
                reason = $"'{prefab.name}' is already managed by Vicinity.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                reason = $"'{prefab.name}' draws nothing, so there is no memory to save on it.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Produces, next to <paramref name="sourcePrefab"/>, a prefab that stands in for it and loads it on
        /// approach. Running it again on the same prefab redoes the measurements and leaves any distance the
        /// user set by hand alone.
        /// </summary>
        internal static PrefabConversion Convert(GameObject sourcePrefab)
        {
            PrefabConversion conversion = new PrefabConversion { Source = sourcePrefab };

            if (!CanConvert(sourcePrefab, out string reason))
            {
                conversion.Problem = reason;
                return conversion;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);

            if (string.IsNullOrEmpty(sourcePath))
            {
                conversion.Problem = $"'{sourcePrefab.name}' has no place on disk.";
                return conversion;
            }

            conversion.EstimatedBytes = VicinitySceneScanner.EstimateBytes(sourcePrefab);
            conversion.Radius = MeasureRadius(sourcePrefab);
            conversion.LoadDistance = LoadDistanceForRadius(conversion.Radius);
            conversion.ReleaseDistance = ReleaseDistanceFor(conversion.LoadDistance);

            AssetKey key = BuildKey(sourcePath, sourcePrefab, out ReferenceStrength strength);
            conversion.Strength = strength;

            string resultPath = ResultPathFor(sourcePath);
            conversion.ResultPath = resultPath;
            conversion.ReplacedExisting = AssetDatabase.LoadAssetAtPath<GameObject>(resultPath) != null;

            conversion.Result = conversion.ReplacedExisting
                ? Refresh(resultPath, key, conversion)
                : Create(resultPath, sourcePrefab, key, conversion);

            if (conversion.Result == null && conversion.Problem.Length == 0)
            {
                conversion.Problem = $"Unity refused to write '{resultPath}'.";
            }

            return conversion;
        }

        /// <summary>
        /// The managed prefab standing in for <paramref name="sourcePrefab"/>, converting it only if one does
        /// not exist yet. Meant for paths that run on every drop, where re-measuring an asset that was already
        /// taken over would be wasted work and would touch the user's prefab for nothing.
        /// </summary>
        internal static PrefabConversion Ensure(GameObject sourcePrefab)
        {
            if (!CanConvert(sourcePrefab, out string reason))
            {
                return new PrefabConversion { Source = sourcePrefab, Problem = reason };
            }

            string resultPath = ResultPathFor(AssetDatabase.GetAssetPath(sourcePrefab));
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(resultPath);

            if (existing == null || existing.GetComponent<VicinityObject>() == null)
            {
                return Convert(sourcePrefab);
            }

            return new PrefabConversion
            {
                Source = sourcePrefab,
                Result = existing,
                ResultPath = resultPath,
                ReplacedExisting = true
            };
        }

        /// <summary>
        /// How far away an object of this size should start loading. Bigger things are noticed from further
        /// away, so they are given more room; the result is rounded so it reads as an authored number rather
        /// than a computed one.
        /// </summary>
        internal static float LoadDistanceForRadius(float radius)
        {
            float wanted = Mathf.Clamp(radius * SizeToDistance, MinimumLoadDistance, MaximumLoadDistance);
            return Mathf.Round(wanted / RoundingStep) * RoundingStep;
        }

        /// <summary>How far the player must walk away before the object is let go.</summary>
        internal static float ReleaseDistanceFor(float loadDistance)
        {
            return Mathf.Round(loadDistance * ReleaseMargin / RoundingStep) * RoundingStep;
        }

        /// <summary>Where the produced prefab is written, so callers can look before acting.</summary>
        internal static string ResultPathFor(string sourcePath)
        {
            string directory = Path.GetDirectoryName(sourcePath);
            string folder = string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(sourcePath);

            return $"{folder}/{name}{Suffix}.prefab";
        }

        #endregion

        #region Privates

        private const float SizeToDistance = 15f;
        private const float MinimumLoadDistance = 25f;
        private const float MaximumLoadDistance = 400f;
        private const float ReleaseMargin = 1.35f;
        private const float RoundingStep = 5f;
        private const string ResourcesFolder = "/Resources/";

        private static GameObject Create(string resultPath, GameObject source, AssetKey key, PrefabConversion conversion)
        {
            GameObject root = new GameObject(source.name + Suffix);

            try
            {
                // Some imported models carry an axis conversion on their own root. Wearing it here means the
                // loaded model stands the way its author left it, instead of tipping over. Scale is left
                // alone: the loaded instance keeps its own, so copying it here would apply it twice.
                root.transform.localRotation = source.transform.localRotation;

                Configure(root.AddComponent<VicinityObject>(), key, conversion, true);
                return PrefabUtility.SaveAsPrefabAsset(root, resultPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject Refresh(string resultPath, AssetKey key, PrefabConversion conversion)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(resultPath);

            try
            {
                VicinityObject managed = contents.GetComponent<VicinityObject>();

                if (managed == null)
                {
                    conversion.Problem =
                        $"'{resultPath}' already exists and is not a Vicinity prefab. Rename or delete it first.";

                    return null;
                }

                // Distances set by hand outlive a refresh; only the measurements are redone.
                bool keepDistances = managed.OverridesDistances;
                Configure(managed, key, conversion, !keepDistances);

                if (keepDistances)
                {
                    conversion.LoadDistance = managed.LoadDistance;
                    conversion.ReleaseDistance = managed.UnloadDistance;
                }

                return PrefabUtility.SaveAsPrefabAsset(contents, resultPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void Configure(VicinityObject managed, AssetKey key, PrefabConversion conversion, bool setDistances)
        {
            if (setDistances)
            {
                managed.SetOwnDistances(conversion.LoadDistance, conversion.ReleaseDistance);
            }

            managed.SetDetailedModel(key);
            managed.SetEstimatedMemoryBytes(conversion.EstimatedBytes);
            managed.SetAuthoredRadius(conversion.Radius);
        }

        private static AssetKey BuildKey(string sourcePath, GameObject sourcePrefab, out ReferenceStrength strength)
        {
            string address = VicinityAddressableBridge.MakeAddressable(sourcePath, sourcePath);

            if (!string.IsNullOrEmpty(address))
            {
                strength = ReferenceStrength.Addressable;
                return AssetKey.FromAddress(address);
            }

            int marker = sourcePath.LastIndexOf(ResourcesFolder, StringComparison.OrdinalIgnoreCase);

            if (marker >= 0)
            {
                string relative = sourcePath.Substring(marker + ResourcesFolder.Length);
                strength = ReferenceStrength.Resources;

                return AssetKey.FromResourcesPath(Path.ChangeExtension(relative, null).Replace('\\', '/'));
            }

            strength = ReferenceStrength.Direct;
            return AssetKey.FromDirectReference(sourcePrefab);
        }

        private static float MeasureRadius(GameObject prefab)
        {
            // Measured as the root's parent sees it. Going only as far as the root's own local space would
            // cancel the scale on the root itself, and a model scaled up there would measure as if it were
            // still its authored size.
            Matrix4x4 toRoot = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix;
            float radius = 0f;

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    radius = Mathf.Max(radius, ReachOf(filter.sharedMesh.bounds, toRoot * filter.transform.localToWorldMatrix));
                }
            }

            foreach (SkinnedMeshRenderer skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh != null)
                {
                    radius = Mathf.Max(radius, ReachOf(skinned.sharedMesh.bounds, toRoot * skinned.transform.localToWorldMatrix));
                }
            }

            return radius;
        }

        private static float ReachOf(Bounds local, Matrix4x4 matrix)
        {
            Vector3 center = local.center;
            Vector3 extents = local.extents;
            float reach = 0f;

            // Every corner, because a rotated child can reach further than its own extents suggest.
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);

                reach = Mathf.Max(reach, matrix.MultiplyPoint3x4(center + offset).magnitude);
            }

            return reach;
        }

        #endregion
    }
}
