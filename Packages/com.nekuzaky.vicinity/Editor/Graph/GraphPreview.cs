using System.Collections.Generic;
using System.Reflection;
using Nekuzaky.Vicinity.Graph;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    internal static class GraphPreview
    {
        #region Main Methods

        internal static Dictionary<string, float> Evaluate(VicinityGraphAsset graph, ObjectFacts facts)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();

            if (graph == null)
            {
                return values;
            }

            InjectFacts(graph, facts);

            GraphExecutor executor = new GraphExecutor(graph);

            if (executor.Execute() != GraphExecutionResult.Completed)
            {
                return values;
            }

            foreach (VicinityNode node in graph.Nodes)
            {
                if (TryReadFirstOutput(node, out float value))
                {
                    values[node.Id] = value;
                }
            }

            return values;
        }

        internal static bool TryReadInput(VicinityNode node, string fieldName, out float value)
        {
            value = 0f;
            FieldInfo field = FindField(node.GetType(), fieldName);

            if (field == null || field.FieldType != typeof(float))
            {
                return false;
            }

            value = (float)field.GetValue(node);
            return true;
        }

        #endregion

        #region Privates

        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static void InjectFacts(VicinityGraphAsset graph, ObjectFacts facts)
        {
            foreach (VicinityNode node in graph.Nodes)
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

        private static void Assign(VicinityNode node, string fieldName, float value)
        {
            FieldInfo field = FindField(node.GetType(), fieldName);
            field?.SetValue(node, value);
        }

        private static bool TryReadFirstOutput(VicinityNode node, out float value)
        {
            value = 0f;

            if (node is ResidencyOutputNode output)
            {
                value = output.LoadDistance;
                return true;
            }

            NodePortLayout layout = NodePortLayout.For(node.GetType());

            if (layout.Outputs.Count == 0)
            {
                return false;
            }

            NodePort port = layout.Outputs[0];

            if (port.ValueType != typeof(float))
            {
                return false;
            }

            value = (float)port.Field.GetValue(node);
            return true;
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            for (System.Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, FieldFlags | BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        #endregion
    }
}
