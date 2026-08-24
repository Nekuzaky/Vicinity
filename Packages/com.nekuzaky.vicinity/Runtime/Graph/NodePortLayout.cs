using System;
using System.Collections.Generic;
using System.Reflection;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>One socket on a node, discovered from an attributed field.</summary>
    public readonly struct NodePort
    {
        internal NodePort(FieldInfo field, string label)
        {
            Field = field;
            Label = label;
        }

        /// <summary>The field this socket reads from or writes to.</summary>
        public string FieldName => Field.Name;

        /// <summary>The name shown next to the socket.</summary>
        public string Label { get; }

        /// <summary>What kind of value travels through this socket.</summary>
        public Type ValueType => Field.FieldType;

        internal FieldInfo Field { get; }
    }

    /// <summary>The sockets of one node type. Discovered once per type and reused.</summary>
    public sealed class NodePortLayout
    {
        #region Main Methods

        /// <summary>Sockets the node reads from.</summary>
        public IReadOnlyList<NodePort> Inputs => _inputs;

        /// <summary>Sockets the node writes to.</summary>
        public IReadOnlyList<NodePort> Outputs => _outputs;

        /// <summary>Returns the sockets of a node type, computing them the first time only.</summary>
        public static NodePortLayout For(Type nodeType)
        {
            if (nodeType == null)
            {
                throw new ArgumentNullException(nameof(nodeType));
            }

            if (_cache.TryGetValue(nodeType, out NodePortLayout cached))
            {
                return cached;
            }

            NodePortLayout layout = Build(nodeType);
            _cache[nodeType] = layout;
            return layout;
        }

        /// <summary>Finds an input socket by field name, or returns false when there is none.</summary>
        public bool TryGetInput(string fieldName, out NodePort port) => TryFind(_inputs, fieldName, out port);

        /// <summary>Finds an output socket by field name, or returns false when there is none.</summary>
        public bool TryGetOutput(string fieldName, out NodePort port) => TryFind(_outputs, fieldName, out port);

        #endregion

        #region Privates

        private const BindingFlags PortFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<Type, NodePortLayout> _cache = new Dictionary<Type, NodePortLayout>();

        private readonly List<NodePort> _inputs = new List<NodePort>();
        private readonly List<NodePort> _outputs = new List<NodePort>();

        private static NodePortLayout Build(Type nodeType)
        {
            NodePortLayout layout = new NodePortLayout();

            for (Type current = nodeType; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(PortFieldFlags | BindingFlags.DeclaredOnly))
                {
                    GraphInputAttribute input = field.GetCustomAttribute<GraphInputAttribute>();
                    if (input != null)
                    {
                        layout._inputs.Add(new NodePort(field, input.Label ?? Prettify(field.Name)));
                        continue;
                    }

                    GraphOutputAttribute output = field.GetCustomAttribute<GraphOutputAttribute>();
                    if (output != null)
                    {
                        layout._outputs.Add(new NodePort(field, output.Label ?? Prettify(field.Name)));
                    }
                }
            }

            layout._inputs.Reverse();
            layout._outputs.Reverse();

            return layout;
        }

        private static bool TryFind(List<NodePort> ports, string fieldName, out NodePort port)
        {
            for (int i = 0; i < ports.Count; i++)
            {
                if (string.Equals(ports[i].FieldName, fieldName, StringComparison.Ordinal))
                {
                    port = ports[i];
                    return true;
                }
            }

            port = default;
            return false;
        }

        private static string Prettify(string fieldName)
        {
            string trimmed = fieldName;

            if (trimmed.StartsWith("m_", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(2);
            }
            else if (trimmed.StartsWith("_", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(1);
            }

            if (trimmed.Length == 0)
            {
                return fieldName;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(char.ToUpperInvariant(trimmed[0]));

            for (int i = 1; i < trimmed.Length; i++)
            {
                if (char.IsUpper(trimmed[i]) && !char.IsUpper(trimmed[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(trimmed[i]);
            }

            return builder.ToString();
        }

        #endregion
    }
}
