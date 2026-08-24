using System.Collections.Generic;
using Nekuzaky.Vicinity.GraphProcessor;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>
    /// Puts a graph's nodes in an order where every node comes after the ones feeding it, and says so plainly
    /// when no such order exists. Compilation needs this; a loop must be caught before anything runs rather
    /// than discovered as a hang.
    /// </summary>
    public sealed class GraphExecutor
    {
        #region Main Methods

        public GraphExecutor(BaseGraph graph)
        {
            _order = new List<BaseNode>();

            if (graph == null)
            {
                return;
            }

            Sort(graph);
        }

        /// <summary>The nodes, each one after everything it depends on. Empty when the graph has a loop.</summary>
        public IReadOnlyList<BaseNode> Order => _order;

        /// <summary>True when the nodes form a loop, so no valid order exists.</summary>
        public bool HasCircularDependency { get; private set; }

        #endregion

        #region Privates

        private readonly List<BaseNode> _order;

        private void Sort(BaseGraph graph)
        {
            Dictionary<string, int> remaining = new Dictionary<string, int>(graph.nodes.Count);
            Dictionary<string, List<BaseNode>> dependents = new Dictionary<string, List<BaseNode>>(graph.nodes.Count);

            foreach (BaseNode node in graph.nodes)
            {
                if (node != null)
                {
                    remaining[node.GUID] = 0;
                }
            }

            foreach (SerializableEdge edge in graph.edges)
            {
                if (edge?.inputNode == null || edge.outputNode == null)
                {
                    continue;
                }

                if (!remaining.ContainsKey(edge.inputNode.GUID) || !remaining.ContainsKey(edge.outputNode.GUID))
                {
                    continue;
                }

                remaining[edge.inputNode.GUID]++;

                if (!dependents.TryGetValue(edge.outputNode.GUID, out List<BaseNode> list))
                {
                    list = new List<BaseNode>();
                    dependents[edge.outputNode.GUID] = list;
                }

                list.Add(edge.inputNode);
            }

            Queue<BaseNode> ready = new Queue<BaseNode>();

            foreach (BaseNode node in graph.nodes)
            {
                if (node != null && remaining[node.GUID] == 0)
                {
                    ready.Enqueue(node);
                }
            }

            while (ready.Count > 0)
            {
                BaseNode node = ready.Dequeue();
                _order.Add(node);

                if (!dependents.TryGetValue(node.GUID, out List<BaseNode> list))
                {
                    continue;
                }

                foreach (BaseNode dependent in list)
                {
                    if (--remaining[dependent.GUID] == 0)
                    {
                        ready.Enqueue(dependent);
                    }
                }
            }

            if (_order.Count == remaining.Count)
            {
                return;
            }

            // Some node was never reached, which can only happen when it sits on a cycle.
            HasCircularDependency = true;
            _order.Clear();
        }

        #endregion
    }
}
