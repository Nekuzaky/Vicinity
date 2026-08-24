using System.Collections.Generic;
using System.Reflection;
using Nekuzaky.Vicinity.Graph;
using Nekuzaky.Vicinity.GraphProcessor;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    /// <summary>
    /// Runs a residency graph on a made-up object so the editor can show, on each node, the value it would
    /// produce. This is the graph's own execution, not the compiled program the game runs — the two agree
    /// because they read the same fields, and the status line under the canvas reports the compiled answer.
    /// </summary>
    internal static class GraphPreview
    {
        #region Main Methods

        /// <summary>The value each node produces for an object with these facts, keyed by node GUID.</summary>
        internal static Dictionary<string, float> Evaluate(BaseGraph graph, ObjectFacts facts)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();

            if (graph == null)
            {
                return values;
            }

            InjectFacts(graph, facts);

            new ProcessGraphProcessor(graph).Run();

            foreach (BaseNode node in graph.nodes)
            {
                if (node != null && TryReadFirstOutput(node, out float value))
                {
                    values[node.GUID] = value;
                }
            }

            return values;
        }

        /// <summary>Reads one named field off a node, for the parts of the editor that need a single value.</summary>
        internal static bool TryReadInput(BaseNode node, string fieldName, out float value)
        {
            value = 0f;

            FieldInfo field = node?.GetType().GetField(fieldName, Flags);

            if (field == null || field.FieldType != typeof(float))
            {
                return false;
            }

            value = (float)field.GetValue(node);
            return true;
        }

        #endregion

        #region Privates

        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static void InjectFacts(BaseGraph graph, ObjectFacts facts)
        {
            foreach (BaseNode node in graph.nodes)
            {
                switch (node)
                {
                    case ObjectSizeNode:
                        Assign(node, "m_meters", facts.SizeMeters);
                        break;

                    case ObjectMemoryNode:
                        Assign(node, "m_megabytes", facts.MemoryMegabytes);
                        break;

                    case ObjectTagNode:
                        Assign(node, "m_matches", facts.TagMatch);
                        break;
                }
            }
        }

        private static void Assign(BaseNode node, string fieldName, float value)
        {
            node.GetType().GetField(fieldName, Flags)?.SetValue(node, value);
        }

        private static bool TryReadFirstOutput(BaseNode node, out float value)
        {
            value = 0f;

            foreach (NodePort port in node.outputPorts)
            {
                if (port?.fieldInfo != null && port.fieldInfo.FieldType == typeof(float))
                {
                    value = (float)port.fieldInfo.GetValue(node);
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
