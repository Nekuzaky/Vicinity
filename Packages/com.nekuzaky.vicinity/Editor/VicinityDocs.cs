using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor
{
    /// <summary>The pages of the online manual Vicinity links to from the editor.</summary>
    internal enum DocPage
    {
        /// <summary>The manual's front page.</summary>
        Home,

        /// <summary>First steps, for someone who has just installed the package.</summary>
        GettingStarted,

        /// <summary>The drop zone: what it measures and what it produces.</summary>
        PrefabsAndModels,

        /// <summary>The two distances, the margin between them, and quality steps.</summary>
        DistancesAndSteps,

        /// <summary>Why a direct reference frees no memory, and what to do about it.</summary>
        AssetSources,

        /// <summary>Per-object rules built as a node graph.</summary>
        ResidencyGraph
    }

    /// <summary>
    /// One place holding where the manual lives, so a moved page is corrected once rather than
    /// hunted down across the editor.
    /// </summary>
    internal static class VicinityDocs
    {
        #region Main Methods

        /// <summary>The address of one page of the manual.</summary>
        internal static string Url(DocPage page)
        {
            return page switch
            {
                DocPage.GettingStarted => Root + "/getting-started",
                DocPage.PrefabsAndModels => Root + "/prefabs-and-models",
                DocPage.DistancesAndSteps => Root + "/distances-and-steps",
                DocPage.AssetSources => Root + "/asset-sources",
                DocPage.ResidencyGraph => Root + "/residency-graph",
                _ => Root
            };
        }

        /// <summary>Opens one page of the manual in the reader's browser.</summary>
        internal static void Open(DocPage page)
        {
            Application.OpenURL(Url(page));
        }

        /// <summary>A link to one page, styled for the dashboard and the graph window.</summary>
        internal static Button Link(string label, DocPage page)
        {
            Button link = new Button(() => Open(page))
            {
                text = label,
                tooltip = Url(page)
            };

            link.AddToClassList("vicinity-doc-link");
            return link;
        }

        /// <summary>
        /// A link to one page, drawn in an inspector. Sits on its own line, aligned right, so it
        /// reads as a way out rather than as another setting.
        /// </summary>
        internal static void DrawInspectorLink(string label, DocPage page)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent(label, Url(page)), EditorStyles.linkLabel))
                {
                    Open(page);
                }
            }
        }

        #endregion

        #region Privates

        private const string Root = "https://nekuzaky.com/docs/vicinity";

        #endregion
    }
}
