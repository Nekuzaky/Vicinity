using System;
using System.Collections.Generic;
using Nekuzaky.Vicinity.Graph;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    internal sealed class VicinityGraphView : GraphView
    {
        #region Main Methods

        internal VicinityGraphView(VicinityGraphAsset asset, Type nodeBaseType)
        {
            _asset = asset;
            _nodeBaseType = nodeBaseType;

            AddToClassList("vicinity-graph");

            SetupZoom(0.25f, 2.5f);
            Insert(0, new GridBackground());

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());

            _searchProvider = ScriptableObject.CreateInstance<NodeSearchProvider>();
            _searchProvider.Prepare(_nodeBaseType, CreateNodeAt);

            nodeCreationRequest = OnNodeCreationRequested;
            graphViewChanged = OnGraphChanged;

            Populate();
        }

        internal event Action Changed;

        internal void Populate()
        {
            DeleteElements(graphElements);
            _views.Clear();

            _asset.RemoveBrokenParts();

            foreach (VicinityNode node in _asset.Nodes)
            {
                AddNodeView(node);
            }

            foreach (NodeEdge edge in _asset.Edges)
            {
                AddEdgeView(edge);
            }
        }

        internal void RefreshPreviews(Func<VicinityNode, string> previewFor)
        {
            foreach (KeyValuePair<string, VicinityNodeView> view in _views)
            {
                view.Value.ShowPreview(previewFor(view.Value.Node));
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatible = new List<Port>();

            foreach (Port candidate in this.Query<Port>().ToList())
            {
                if (candidate.direction == startPort.direction || candidate.node == startPort.node)
                {
                    continue;
                }

                if (!TryDescribe(startPort, candidate, out string fromNode, out string fromPort, out string toNode, out string toPort))
                {
                    continue;
                }

                if (_asset.CanConnect(fromNode, fromPort, toNode, toPort))
                {
                    compatible.Add(candidate);
                }
            }

            return compatible;
        }

        #endregion

        #region Privates

        private const float SearchWindowWidth = 320f;
        private const float SearchWindowHeight = 420f;

        private readonly VicinityGraphAsset _asset;
        private readonly Type _nodeBaseType;
        private readonly NodeSearchProvider _searchProvider;
        private readonly Dictionary<string, VicinityNodeView> _views = new Dictionary<string, VicinityNodeView>();

        private void OnNodeCreationRequested(NodeCreationContext context)
        {
            SearchWindow.Open(
                new SearchWindowContext(context.screenMousePosition, SearchWindowWidth, SearchWindowHeight),
                _searchProvider);
        }

        private void CreateNodeAt(Type nodeType, Vector2 screenPosition)
        {
            if (Activator.CreateInstance(nodeType) is not VicinityNode node)
            {
                return;
            }

            Vector2 windowPosition = screenPosition - (Vector2)EditorWindow.focusedWindow.position.position;
            node.Position = contentViewContainer.WorldToLocal(windowPosition);

            Undo.RecordObject(_asset, "Add Vicinity node");
            _asset.Add(node);

            AddNodeView(node);
            MarkChanged();
        }

        private void AddNodeView(VicinityNode node)
        {
            VicinityNodeView view = new VicinityNodeView(node, MarkChanged);
            _views[node.Id] = view;
            AddElement(view);
        }

        private void AddEdgeView(NodeEdge edge)
        {
            if (!_views.TryGetValue(edge.FromNodeId, out VicinityNodeView from))
            {
                return;
            }

            if (!_views.TryGetValue(edge.ToNodeId, out VicinityNodeView to))
            {
                return;
            }

            if (!from.OutputPorts.TryGetValue(edge.FromPort, out Port output))
            {
                return;
            }

            if (!to.InputPorts.TryGetValue(edge.ToPort, out Port input))
            {
                return;
            }

            Edge view = output.ConnectTo(input);
            AddElement(view);
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change)
        {
            bool touched = false;

            if (change.elementsToRemove != null)
            {
                Undo.RecordObject(_asset, "Remove from Vicinity graph");

                foreach (GraphElement element in change.elementsToRemove)
                {
                    touched |= RemoveElementFromAsset(element);
                }
            }

            if (change.edgesToCreate != null)
            {
                Undo.RecordObject(_asset, "Connect Vicinity nodes");

                foreach (Edge edge in change.edgesToCreate)
                {
                    touched |= ConnectFromView(edge);
                }
            }

            if (change.movedElements != null)
            {
                touched = true;
            }

            if (touched)
            {
                MarkChanged();
            }

            return change;
        }

        private bool RemoveElementFromAsset(GraphElement element)
        {
            if (element is VicinityNodeView nodeView)
            {
                _asset.Remove(nodeView.Node);
                _views.Remove(nodeView.Node.Id);
                return true;
            }

            if (element is not Edge edge)
            {
                return false;
            }

            if (!TryDescribe(edge.output, edge.input, out _, out _, out string toNode, out string toPort))
            {
                return false;
            }

            _asset.DisconnectInput(toNode, toPort);
            return true;
        }

        private bool ConnectFromView(Edge edge)
        {
            if (!TryDescribe(edge.output, edge.input, out string fromNode, out string fromPort, out string toNode, out string toPort))
            {
                return false;
            }

            return _asset.Connect(fromNode, fromPort, toNode, toPort);
        }

        private static bool TryDescribe(
            Port first,
            Port second,
            out string fromNode,
            out string fromPort,
            out string toNode,
            out string toPort)
        {
            fromNode = fromPort = toNode = toPort = null;

            Port output = first.direction == Direction.Output ? first : second;
            Port input = first.direction == Direction.Output ? second : first;

            if (output.node is not VicinityNodeView outputView || input.node is not VicinityNodeView inputView)
            {
                return false;
            }

            fromNode = outputView.Node.Id;
            fromPort = output.userData as string;
            toNode = inputView.Node.Id;
            toPort = input.userData as string;

            return fromPort != null && toPort != null;
        }

        private void MarkChanged()
        {
            EditorUtility.SetDirty(_asset);
            Changed?.Invoke();
        }

        #endregion
    }
}
