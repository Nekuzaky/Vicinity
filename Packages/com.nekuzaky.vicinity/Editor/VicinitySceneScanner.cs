using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Nekuzaky.Vicinity.Editor
{
    internal sealed class ScanCandidate
    {
        internal GameObject Target;
        internal long EstimatedBytes;
        internal bool AlreadyManaged;
        internal bool WasEditedByHand;
        internal GameObject PrefabSource;
        internal bool Selected;

        internal string DisplayName => Target != null ? Target.name : "<removed>";
    }

    internal static class VicinitySceneScanner
    {
        #region Main Methods

        internal static List<ScanCandidate> Scan()
        {
            List<ScanCandidate> candidates = new List<ScanCandidate>();
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            HashSet<GameObject> seen = new HashSet<GameObject>();

            foreach (Renderer renderer in renderers)
            {
                GameObject host = FindManagedRoot(renderer);
                if (host == null || !seen.Add(host))
                {
                    continue;
                }

                VicinityObject existing = host.GetComponent<VicinityObject>();

                candidates.Add(new ScanCandidate
                {
                    Target = host,
                    EstimatedBytes = EstimateBytes(host),
                    AlreadyManaged = existing != null,
                    WasEditedByHand = existing != null && (existing.OverridesDistances || !existing.HasMissingModel),
                    PrefabSource = PrefabUtility.GetCorrespondingObjectFromSource(host),
                    Selected = existing == null
                });
            }

            candidates.Sort(static (left, right) => right.EstimatedBytes.CompareTo(left.EstimatedBytes));
            return candidates;
        }

        internal static int Apply(IReadOnlyList<ScanCandidate> candidates, bool overwriteHandEditedObjects)
        {
            int changed = 0;

            foreach (ScanCandidate candidate in candidates)
            {
                if (!candidate.Selected || candidate.Target == null)
                {
                    continue;
                }

                if (candidate.WasEditedByHand && !overwriteHandEditedObjects)
                {
                    continue;
                }

                if (Equip(candidate))
                {
                    changed++;
                }
            }

            return changed;
        }

        internal static int CountHandEdited(IReadOnlyList<ScanCandidate> candidates)
        {
            int count = 0;

            foreach (ScanCandidate candidate in candidates)
            {
                if (candidate.Selected && candidate.WasEditedByHand)
                {
                    count++;
                }
            }

            return count;
        }

        internal static long EstimateBytes(GameObject host)
        {
            if (host == null)
            {
                return 0L;
            }

            long total = 0L;
            HashSet<int> counted = new HashSet<int>();

            foreach (MeshFilter filter in host.GetComponentsInChildren<MeshFilter>(true))
            {
                total += MeasureOnce(filter.sharedMesh, counted);
            }

            foreach (Renderer renderer in host.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    total += MeasureMaterialTextures(material, counted);
                }
            }

            return total;
        }

        #endregion

        #region Privates

        private static GameObject FindManagedRoot(Renderer renderer)
        {
            if (renderer == null || renderer.gameObject == null)
            {
                return null;
            }

            GameObject host = renderer.gameObject;

            if (host.GetComponentInParent<VicinityManager>() != null)
            {
                return null;
            }

            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(host);
            return prefabRoot != null ? prefabRoot : host;
        }

        private static bool Equip(ScanCandidate candidate)
        {
            VicinityObject managed = candidate.Target.GetComponent<VicinityObject>();

            if (managed == null)
            {
                managed = Undo.AddComponent<VicinityObject>(candidate.Target);
            }
            else
            {
                Undo.RecordObject(managed, "Configure Vicinity Object");
            }

            if (managed.HasMissingModel && candidate.PrefabSource != null)
            {
                managed.SetDetailedModel(AssetKey.FromDirectReference(candidate.PrefabSource));
            }

            managed.SetEstimatedMemoryBytes(candidate.EstimatedBytes);
            EditorUtility.SetDirty(managed);
            return true;
        }

        private static long MeasureOnce(Object asset, HashSet<int> counted)
        {
            if (asset == null || !counted.Add(asset.GetInstanceID()))
            {
                return 0L;
            }

            return Profiler.GetRuntimeMemorySizeLong(asset);
        }

        private static long MeasureMaterialTextures(Material material, HashSet<int> counted)
        {
            if (material == null || material.shader == null)
            {
                return 0L;
            }

            long total = 0L;
            int propertyCount = material.shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                if (material.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    continue;
                }

                int nameId = material.shader.GetPropertyNameId(i);
                if (!material.HasProperty(nameId))
                {
                    continue;
                }

                total += MeasureOnce(material.GetTexture(nameId), counted);
            }

            return total;
        }

        #endregion
    }
}
