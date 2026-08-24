using System;
using System.Collections.Generic;
using NUnit.Framework;
using Nekuzaky.Vicinity.Graph;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class GraphFoundationTests
    {
        [TearDown]
        public void TearDown()
        {
            if (_graph != null)
            {
                UnityEngine.Object.DestroyImmediate(_graph);
                _graph = null;
            }
        }

        [Test]
        public void SocketsAreDiscoveredFromAttributedFields()
        {
            NodePortLayout layout = NodePortLayout.For(typeof(AddNode));

            Assert.AreEqual(2, layout.Inputs.Count);
            Assert.AreEqual(1, layout.Outputs.Count);
            Assert.IsTrue(layout.TryGetInput("m_left", out NodePort left));
            Assert.AreEqual("Left", left.Label, "a field named m_left must present itself as 'Left'");
            Assert.AreEqual(typeof(float), left.ValueType);
        }

        [Test]
        public void ANodeWithoutSocketsIsAllowed()
        {
            NodePortLayout layout = NodePortLayout.For(typeof(SideEffectNode));

            Assert.AreEqual(0, layout.Inputs.Count);
            Assert.AreEqual(0, layout.Outputs.Count);
        }

        [Test]
        public void ValuesTravelAlongTheWires()
        {
            TestGraph graph = CreateGraph();

            ConstantNode five = Add(graph, new ConstantNode { Value = 5f });
            ConstantNode seven = Add(graph, new ConstantNode { Value = 7f });
            AddNode sum = Add(graph, new AddNode());

            Assert.IsTrue(graph.Connect(five.Id, "m_result", sum.Id, "m_left"));
            Assert.IsTrue(graph.Connect(seven.Id, "m_result", sum.Id, "m_right"));

            GraphExecutor executor = new GraphExecutor(graph);

            Assert.AreEqual(GraphExecutionResult.Completed, executor.Execute());
            Assert.AreEqual(12f, sum.Sum, 0.0001f);
        }

        [Test]
        public void ANodeRunsOnlyAfterEverythingWiredIntoIt()
        {
            TestGraph graph = CreateGraph();

            ConstantNode source = Add(graph, new ConstantNode { Value = 3f });
            AddNode first = Add(graph, new AddNode());
            AddNode second = Add(graph, new AddNode());

            graph.Connect(source.Id, "m_result", first.Id, "m_left");
            graph.Connect(first.Id, "m_sum", second.Id, "m_left");

            GraphExecutor executor = new GraphExecutor(graph);

            List<string> order = new List<string>();
            foreach (VicinityNode node in executor.Order)
            {
                order.Add(node.Id);
            }

            Assert.Less(order.IndexOf(source.Id), order.IndexOf(first.Id));
            Assert.Less(order.IndexOf(first.Id), order.IndexOf(second.Id));
        }

        [Test]
        public void AnInputTakesASingleWire()
        {
            TestGraph graph = CreateGraph();

            ConstantNode first = Add(graph, new ConstantNode { Value = 1f });
            ConstantNode second = Add(graph, new ConstantNode { Value = 2f });
            AddNode sum = Add(graph, new AddNode());

            graph.Connect(first.Id, "m_result", sum.Id, "m_left");
            graph.Connect(second.Id, "m_result", sum.Id, "m_left");

            Assert.AreEqual(1, graph.Edges.Count, "wiring a second source into one input must replace the first");
            Assert.AreEqual(second.Id, graph.Edges[0].FromNodeId);
        }

        [Test]
        public void SocketsOfIncompatibleTypesRefuseToConnect()
        {
            TestGraph graph = CreateGraph();

            ConstantNode number = Add(graph, new ConstantNode { Value = 1f });
            TextNode text = Add(graph, new TextNode());

            Assert.IsFalse(graph.CanConnect(number.Id, "m_result", text.Id, "m_label"));
            Assert.IsFalse(graph.Connect(number.Id, "m_result", text.Id, "m_label"));
            Assert.AreEqual(0, graph.Edges.Count);
        }

        [Test]
        public void ANodeCannotWireIntoItself()
        {
            TestGraph graph = CreateGraph();
            AddNode sum = Add(graph, new AddNode());

            Assert.IsFalse(graph.Connect(sum.Id, "m_sum", sum.Id, "m_left"));
        }

        [Test]
        public void ALoopIsDetectedAndNothingRuns()
        {
            TestGraph graph = CreateGraph();

            AddNode first = Add(graph, new AddNode());
            AddNode second = Add(graph, new AddNode());

            graph.Connect(first.Id, "m_sum", second.Id, "m_left");
            graph.Connect(second.Id, "m_sum", first.Id, "m_left");

            GraphExecutor executor = new GraphExecutor(graph);

            Assert.IsTrue(executor.HasCircularDependency);
            Assert.AreEqual(GraphExecutionResult.CircularDependency, executor.Execute());
            Assert.AreEqual(0, executor.Order.Count, "a looping graph must run nothing, not half of itself");
        }

        [Test]
        public void ANodeThatThrowsStopsTheRunWithoutEscaping()
        {
            TestGraph graph = CreateGraph();

            ConstantNode source = Add(graph, new ConstantNode { Value = 1f });
            FailingNode failing = Add(graph, new FailingNode());
            graph.Connect(source.Id, "m_result", failing.Id, "m_input");

            GraphExecutor executor = new GraphExecutor(graph);

            Assert.AreEqual(GraphExecutionResult.NodeFailed, executor.Execute());
            Assert.AreSame(failing, executor.FailedNode);
            Assert.IsNotNull(executor.Failure);
        }

        [Test]
        public void AnEmptyGraphReportsItselfAsEmpty()
        {
            TestGraph graph = CreateGraph();

            Assert.AreEqual(GraphExecutionResult.Empty, new GraphExecutor(graph).Execute());
        }

        [Test]
        public void RemovingANodeRemovesItsWires()
        {
            TestGraph graph = CreateGraph();

            ConstantNode source = Add(graph, new ConstantNode { Value = 1f });
            AddNode sum = Add(graph, new AddNode());
            graph.Connect(source.Id, "m_result", sum.Id, "m_left");

            graph.Remove(source);

            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.AreEqual(0, graph.Edges.Count, "a wire must never survive the node it was attached to");
        }

        [Test]
        public void EveryNodeGetsItsOwnIdentity()
        {
            TestGraph graph = CreateGraph();

            ConstantNode first = Add(graph, new ConstantNode());
            ConstantNode second = Add(graph, new ConstantNode());

            Assert.IsNotEmpty(first.Id);
            Assert.AreNotEqual(first.Id, second.Id);
        }

        private TestGraph _graph;

        private TestGraph CreateGraph()
        {
            _graph = ScriptableObject.CreateInstance<TestGraph>();
            return _graph;
        }

        private static TNode Add<TNode>(TestGraph graph, TNode node) where TNode : VicinityNode
        {
            graph.Add(node);
            return node;
        }

        internal sealed class TestGraph : VicinityGraphAsset
        {
        }

        [Serializable]
        internal sealed class ConstantNode : VicinityNode
        {
            [GraphOutput("Result")] private float m_result;

            public override string Title => "Constant";

            public float Value { get; set; }

            public override void Process()
            {
                m_result = Value;
            }
        }

        [Serializable]
        internal sealed class AddNode : VicinityNode
        {
            [GraphInput] private float m_left;
            [GraphInput] private float m_right;
            [GraphOutput] private float m_sum;

            public override string Title => "Add";

            public float Sum => m_sum;

            public override void Process()
            {
                m_sum = m_left + m_right;
            }
        }

        [Serializable]
        internal sealed class TextNode : VicinityNode
        {
            [GraphInput] private string m_label;

            public override string Title => "Text";

            public string Label => m_label;

            public override void Process()
            {
            }
        }

        [Serializable]
        internal sealed class SideEffectNode : VicinityNode
        {
            public override string Title => "Side Effect";

            public override void Process()
            {
            }
        }

        [Serializable]
        internal sealed class FailingNode : VicinityNode
        {
            [GraphInput] private float m_input;

            public override string Title => "Failing";

            public override void Process()
            {
                throw new InvalidOperationException($"this node always fails, input was {m_input}");
            }
        }
    }
}
