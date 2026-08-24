using System.Text;
using Nekuzaky.Vicinity.Editor.Graph;
using Nekuzaky.Vicinity.Graph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    /// <summary>Temporary diagnostic. Prints what the graph window actually builds.</summary>
    internal sealed class GraphWindowTreeDump
    {
        [Test]
        public void Dump()
        {
            ResidencyGraphAsset graph = ResidencyGraphAsset.CreateStartingPoint();
            VicinityGraphWindow window = ScriptableObject.CreateInstance<VicinityGraphWindow>();

            try
            {
                window.InitializeGraph(graph);

                StringBuilder builder = new StringBuilder("VICTREE\n");
                Describe(window.rootVisualElement, 0, builder);

                Debug.Log(builder.ToString());
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(graph);
            }
        }

        private static void Describe(VisualElement element, int depth, StringBuilder builder)
        {
            builder.Append(' ', depth * 2)
                .Append(element.GetType().Name)
                .Append(" name=").Append(string.IsNullOrEmpty(element.name) ? "-" : element.name)
                .Append(" pos=").Append(element.resolvedStyle.position)
                .Append(" picking=").Append(element.pickingMode)
                .Append(" children=").Append(element.childCount)
                .Append('\n');

            if (depth >= 2)
            {
                return;
            }

            foreach (VisualElement child in element.Children())
            {
                Describe(child, depth + 1, builder);
            }
        }
    }
}
