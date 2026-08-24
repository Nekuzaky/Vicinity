using System;
using System.Collections.Generic;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>Why a graph refused to run.</summary>
    public enum GraphExecutionResult
    {
        /// <summary>Every node ran, in dependency order.</summary>
        Completed = 0,

        /// <summary>Nothing to run.</summary>
        Empty = 1,

        /// <summary>Wires form a loop, so no order exists. Nothing ran.</summary>
        CircularDependency = 2,

        /// <summary>A node threw. Everything before it ran; nothing after it did.</summary>
        NodeFailed = 3
    }

    /// <summary>
    /// Runs a graph once, in dependency order: a node runs only after everything wired into it has run.
    /// Loops are detected before anything runs rather than half way through.
    /// </summary>
    public sealed class GraphExecutor
    {
        #region Main Methods

        /// <summary>Prepares to run a graph. The order is computed once and reused.</summary>
        public GraphExecutor(VicinityGraphAsset graph)
        {
            _graph = graph != null ? graph : throw new ArgumentNullException(nameof(graph));
            BuildOrder();
        }

        /// <summary>Nodes in the order they will run. Empty when the graph loops.</summary>
        public IReadOnlyList<VicinityNode> Order => _order;

        /// <summary>True when the wires form a loop, which makes an order impossible.</summary>
        public bool HasCircularDependency { get; private set; }

        /// <summary>The node that threw during the last run, or null.</summary>
        public VicinityNode FailedNode { get; private set; }

        /// <summary>The exception thrown during the last run, or null.</summary>
        public Exception Failure { get; private set; }

        /// <summary>Runs every node once. Never throws; read the result to know what happened.</summary>
        public GraphExecutionResult Execute()
        {
            FailedNode = null;
            Failure = null;

            if (HasCircularDependency)
            {
                return GraphExecutionResult.CircularDependency;
            }

            if (_order.Count == 0)
            {
                return GraphExecutionResult.Empty;
            }

            for (int i = 0; i < _order.Count; i++)
            {
                VicinityNode node = _order[i];
                FeedInputs(node);

                try
                {
                    node.Process();
                }
                catch (Exception exception)
                {
                    FailedNode = node;
                    Failure = exception;
                    return GraphExecutionResult.NodeFailed;
                }
            }

            return GraphExecutionResult.Completed;
        }

        #endregion

        #region Privates

        private readonly VicinityGraphAsset _graph;
        private readonly List<VicinityNode> _order = new List<VicinityNode>();
        private readonly Dictionary<string, List<NodeEdge>> _incoming = new Dictionary<string, List<NodeEdge>>();

        private void BuildOrder()
        {
            _order.Clear();
            _incoming.Clear();

            IReadOnlyList<VicinityNode> nodes = _graph.Nodes;
            Dictionary<string, int> remaining = new Dictionary<string, int>(nodes.Count);
            Dictionary<string, List<string>> dependents = new Dictionary<string, List<string>>(nodes.Count);

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null)
                {
                    continue;
                }

                remaining[nodes[i].Id] = 0;
                dependents[nodes[i].Id] = new List<string>();
            }

            foreach (NodeEdge edge in _graph.Edges)
            {
                if (!remaining.ContainsKey(edge.FromNodeId) || !remaining.ContainsKey(edge.ToNodeId))
                {
                    continue;
                }

                remaining[edge.ToNodeId]++;
                dependents[edge.FromNodeId].Add(edge.ToNodeId);

                if (!_incoming.TryGetValue(edge.ToNodeId, out List<NodeEdge> wires))
                {
                    wires = new List<NodeEdge>();
                    _incoming[edge.ToNodeId] = wires;
                }

                wires.Add(edge);
            }

            Queue<string> ready = new Queue<string>();

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && remaining[nodes[i].Id] == 0)
                {
                    ready.Enqueue(nodes[i].Id);
                }
            }

            while (ready.Count > 0)
            {
                string nodeId = ready.Dequeue();
                VicinityNode node = _graph.Find(nodeId);

                if (node != null)
                {
                    _order.Add(node);
                }

                foreach (string dependent in dependents[nodeId])
                {
                    if (--remaining[dependent] == 0)
                    {
                        ready.Enqueue(dependent);
                    }
                }
            }

            HasCircularDependency = _order.Count != remaining.Count;

            if (HasCircularDependency)
            {
                _order.Clear();
            }
        }

        private void FeedInputs(VicinityNode node)
        {
            if (!_incoming.TryGetValue(node.Id, out List<NodeEdge> wires))
            {
                return;
            }

            NodePortLayout targetLayout = NodePortLayout.For(node.GetType());

            for (int i = 0; i < wires.Count; i++)
            {
                NodeEdge edge = wires[i];
                VicinityNode source = _graph.Find(edge.FromNodeId);

                if (source == null)
                {
                    continue;
                }

                if (!NodePortLayout.For(source.GetType()).TryGetOutput(edge.FromPort, out NodePort output))
                {
                    continue;
                }

                if (!targetLayout.TryGetInput(edge.ToPort, out NodePort input))
                {
                    continue;
                }

                input.Field.SetValue(node, output.Field.GetValue(source));
            }
        }

        #endregion
    }
}
