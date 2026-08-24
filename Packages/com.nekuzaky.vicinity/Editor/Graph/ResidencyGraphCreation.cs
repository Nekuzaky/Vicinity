using Nekuzaky.Vicinity.Graph;
using UnityEditor;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    /// <summary>
    /// Makes residency graphs. Every graph starts already wired to an output, because a graph without one
    /// tells Vicinity nothing and would greet the user with an error on a blank canvas.
    /// </summary>
    internal static class ResidencyGraphCreation
    {
        #region Main Methods

        /// <summary>The name given to a graph the user has not named yet.</summary>
        internal const string DefaultName = "Residency Graph.asset";

        [MenuItem("Assets/Create/Vicinity/Residency Graph", false, 201)]
        internal static void CreateFromProjectWindow()
        {
            ProjectWindowUtil.CreateAsset(ResidencyGraphAsset.CreateStartingPoint(), DefaultName);
        }

        /// <summary>Writes a graph at <paramref name="path"/> and returns it, or null if Unity refused.</summary>
        internal static ResidencyGraphAsset CreateAt(string path)
        {
            ResidencyGraphAsset graph = ResidencyGraphAsset.CreateStartingPoint();

            AssetDatabase.CreateAsset(graph, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();

            return graph;
        }

        /// <summary>
        /// Fills in a graph that has no nodes at all, so an empty asset opens as something the user can read
        /// and edit rather than as an error. Returns true when it changed something.
        /// </summary>
        internal static bool SeedIfEmpty(ResidencyGraphAsset graph)
        {
            if (graph == null || !graph.Seed())
            {
                return false;
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(graph);

            return true;
        }

        #endregion
    }
}
