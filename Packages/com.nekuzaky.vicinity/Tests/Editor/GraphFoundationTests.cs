using Nekuzaky.Vicinity.Graph;
using Nekuzaky.Vicinity.GraphProcessor;
using NUnit.Framework;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    /// <summary>
    /// Covers the ordering Vicinity does on top of NodeGraphProcessor. The canvas, the ports and the wiring
    /// rules belong to that library and carry their own tests; what is tested here is the order compilation
    /// needs, and the refusal to compile a graph that loops.
    /// </summary>
    internal sealed class GraphFoundationTests
    {
        #region Main Methods

        [Test]
        public void ANodeComesAfterEverythingWiredIntoIt()
        {
            ResidencyGraphAsset graph = CreateGraph();

            NumberNode source = AddNode<NumberNode>(graph);
            MathsNode middle = AddNode<MathsNode>(graph);
            MathsNode last = AddNode<MathsNode>(graph);

            Wire(graph, source, "m_result", middle, "m_left");
            Wire(graph, middle, "m_result", last, "m_left");

            GraphExecutor executor = new GraphExecutor(graph);

            Assert.IsFalse(executor.HasCircularDependency);
            Assert.Less(IndexOf(executor, source), IndexOf(executor, middle));
            Assert.Less(IndexOf(executor, middle), IndexOf(executor, last));
        }

        [Test]
        public void EveryNodeIsOrderedExactlyOnce()
        {
            ResidencyGraphAsset graph = CreateGraph();

            AddNode<NumberNode>(graph);
            AddNode<NumberNode>(graph);
            AddNode<MathsNode>(graph);

            GraphExecutor executor = new GraphExecutor(graph);

            Assert.AreEqual(graph.nodes.Count, executor.Order.Count,
                "a node left out of the order would never be compiled");
        }

        [Test]
        public void ALoopIsDetectedAndNothingIsOrdered()
        {
            ResidencyGraphAsset graph = CreateGraph();

            MathsNode first = AddNode<MathsNode>(graph);
            MathsNode second = AddNode<MathsNode>(graph);

            Wire(graph, first, "m_result", second, "m_left");
            Wire(graph, second, "m_result", first, "m_left");

            GraphExecutor executor = new GraphExecutor(graph);

            Assert.IsTrue(executor.HasCircularDependency,
                "a loop has no valid order, and must be reported rather than hung on");

            Assert.IsEmpty(executor.Order);
        }

        [Test]
        public void AGraphThatLoopsRefusesToCompileWithAnExplanation()
        {
            ResidencyGraphAsset graph = CreateGraph();

            ResidencyOutputNode output = AddNode<ResidencyOutputNode>(graph);
            MathsNode first = AddNode<MathsNode>(graph);
            MathsNode second = AddNode<MathsNode>(graph);

            Wire(graph, first, "m_result", second, "m_left");
            Wire(graph, second, "m_result", first, "m_left");
            Wire(graph, second, "m_result", output, "m_loadDistance");

            CompiledResidencyRules compiled = graph.Compile();

            Assert.IsFalse(compiled.IsValid);
            Assert.IsNotEmpty(compiled.Problem, "a refusal without a reason leaves the user stuck");

            compiled.Dispose();
        }

        [Test]
        public void AnEmptyGraphOrdersNothingAndDoesNotClaimALoop()
        {
            GraphExecutor executor = new GraphExecutor(CreateGraph());

            Assert.IsEmpty(executor.Order);
            Assert.IsFalse(executor.HasCircularDependency);
        }

        [Test]
        public void ANullGraphIsSurvived()
        {
            GraphExecutor executor = new GraphExecutor(null);

            Assert.IsEmpty(executor.Order);
            Assert.IsFalse(executor.HasCircularDependency);
        }

        #endregion

        #region Privates

        private ResidencyGraphAsset _graph;

        [TearDown]
        public void RemoveGraph()
        {
            if (_graph != null)
            {
                Object.DestroyImmediate(_graph);
                _graph = null;
            }
        }

        private ResidencyGraphAsset CreateGraph()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();
            return _graph;
        }

        private static TNode AddNode<TNode>(ResidencyGraphAsset graph) where TNode : BaseNode
        {
            TNode node = BaseNode.CreateFromType<TNode>(Vector2.zero);
            graph.AddNode(node);

            return node;
        }

        private static void Wire(ResidencyGraphAsset graph, BaseNode from, string fromField, BaseNode to, string toField)
        {
            NodePort source = from.GetPort(fromField, null);
            NodePort destination = to.GetPort(toField, null);

            Assert.IsNotNull(source, $"'{fromField}' is not an output port of {from.name}");
            Assert.IsNotNull(destination, $"'{toField}' is not an input port of {to.name}");

            graph.Connect(destination, source);
        }

        private static int IndexOf(GraphExecutor executor, BaseNode node)
        {
            for (int i = 0; i < executor.Order.Count; i++)
            {
                if (executor.Order[i] == node)
                {
                    return i;
                }
            }

            Assert.Fail($"{node.name} never appears in the order");
            return -1;
        }

        #endregion
    }
}
