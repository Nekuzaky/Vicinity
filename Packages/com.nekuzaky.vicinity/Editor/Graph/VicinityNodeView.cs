using System;
using System.Collections.Generic;
using System.Reflection;
using Nekuzaky.Vicinity.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    internal sealed class VicinityNodeView : Node
    {
        #region Main Methods

        internal VicinityNodeView(VicinityNode node, Action onEdited)
        {
            Node = node;
            _onEdited = onEdited;

            title = node.Title;
            tooltip = node.Summary;
            viewDataKey = node.Id;

            AddToClassList("vicinity-node");
            AddToClassList(CategoryClass(node));

            BuildPorts();
            BuildInlineFields();
            BuildPreview();

            SetPosition(new Rect(node.Position, Vector2.zero));
            RefreshExpandedState();
            RefreshPorts();
        }

        internal VicinityNode Node { get; }

        internal IReadOnlyDictionary<string, Port> InputPorts => _inputPorts;

        internal IReadOnlyDictionary<string, Port> OutputPorts => _outputPorts;

        internal void ShowPreview(string text)
        {
            if (_preview == null)
            {
                return;
            }

            _preview.text = text;
            _preview.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public override void SetPosition(Rect newPosition)
        {
            base.SetPosition(newPosition);
            Node.Position = newPosition.position;
        }

        #endregion

        #region Privates

        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly Dictionary<string, Port> _inputPorts = new Dictionary<string, Port>();
        private readonly Dictionary<string, Port> _outputPorts = new Dictionary<string, Port>();
        private readonly Action _onEdited;

        private Label _preview;

        private static string CategoryClass(VicinityNode node)
        {
            return node switch
            {
                ResidencyOutputNode => "vicinity-node--output",
                ObjectSizeNode or ObjectMemoryNode or ObjectTagNode => "vicinity-node--fact",
                CompareNode or ChooseNode => "vicinity-node--logic",
                _ => "vicinity-node--maths"
            };
        }

        private void BuildPorts()
        {
            NodePortLayout layout = NodePortLayout.For(Node.GetType());

            foreach (NodePort port in layout.Inputs)
            {
                Port view = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, port.ValueType);
                view.portName = port.Label;
                view.userData = port.FieldName;

                _inputPorts[port.FieldName] = view;
                inputContainer.Add(view);
            }

            foreach (NodePort port in layout.Outputs)
            {
                Port view = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, port.ValueType);
                view.portName = port.Label;
                view.userData = port.FieldName;

                _outputPorts[port.FieldName] = view;
                outputContainer.Add(view);
            }
        }

        private void BuildInlineFields()
        {
            NodePortLayout layout = NodePortLayout.For(Node.GetType());

            foreach (FieldInfo field in CollectEditableFields(layout))
            {
                VisualElement control = BuildControl(field);

                if (control != null)
                {
                    control.AddToClassList("vicinity-node__field");
                    extensionContainer.Add(control);
                }
            }
        }

        private List<FieldInfo> CollectEditableFields(NodePortLayout layout)
        {
            List<FieldInfo> editable = new List<FieldInfo>();

            for (Type current = Node.GetType(); current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(FieldFlags | BindingFlags.DeclaredOnly))
                {
                    if (field.GetCustomAttribute<GraphInputAttribute>() != null)
                    {
                        continue;
                    }

                    if (field.GetCustomAttribute<GraphOutputAttribute>() != null)
                    {
                        continue;
                    }

                    if (field.GetCustomAttribute<SerializeField>() == null)
                    {
                        continue;
                    }

                    if (field.GetCustomAttribute<HideInInspector>() != null)
                    {
                        continue;
                    }

                    editable.Add(field);
                }
            }

            editable.Reverse();
            return editable;
        }

        private VisualElement BuildControl(FieldInfo field)
        {
            string label = Prettify(field.Name);

            if (field.FieldType == typeof(float))
            {
                FloatField control = new FloatField(label) { value = (float)field.GetValue(Node) };
                control.RegisterValueChangedCallback(evt => Assign(field, evt.newValue));
                return control;
            }

            if (field.FieldType == typeof(int))
            {
                IntegerField control = new IntegerField(label) { value = (int)field.GetValue(Node) };
                control.RegisterValueChangedCallback(evt => Assign(field, evt.newValue));
                return control;
            }

            if (field.FieldType == typeof(string))
            {
                TextField control = new TextField(label) { value = (string)field.GetValue(Node) ?? string.Empty };
                control.RegisterValueChangedCallback(evt => Assign(field, evt.newValue));
                return control;
            }

            if (field.FieldType.IsEnum)
            {
                EnumField control = new EnumField(label, (Enum)field.GetValue(Node));
                control.RegisterValueChangedCallback(evt =>
                {
                    Assign(field, evt.newValue);
                    title = Node.Title;
                });

                return control;
            }

            return null;
        }

        private void Assign(FieldInfo field, object value)
        {
            field.SetValue(Node, value);
            _onEdited?.Invoke();
        }

        private void BuildPreview()
        {
            _preview = new Label(string.Empty);
            _preview.AddToClassList("vicinity-node__preview");
            _preview.style.display = DisplayStyle.None;

            extensionContainer.Add(_preview);
        }

        private static string Prettify(string fieldName)
        {
            string trimmed = fieldName.StartsWith("m_", StringComparison.Ordinal) ? fieldName.Substring(2) : fieldName;

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
