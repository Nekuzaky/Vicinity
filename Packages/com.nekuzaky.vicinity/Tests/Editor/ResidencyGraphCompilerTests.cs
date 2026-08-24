using NUnit.Framework;
using Nekuzaky.Vicinity.Graph;
using Nekuzaky.Vicinity.GraphProcessor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class ResidencyGraphCompilerTests
    {
        [TearDown]
        public void TearDown()
        {
            _program?.Dispose();
            _program = null;

            if (_graph != null)
            {
                Object.DestroyImmediate(_graph);
                _graph = null;
            }
        }

        [Test]
        public void TheStartingGraphReproducesTheBuiltInDistances()
        {
            _graph = ResidencyGraphAsset.CreateStartingPoint();
            _program = _graph.Compile();

            Assert.IsTrue(_program.IsValid, _program.Problem);

            ResolvedRule rule = _program.Evaluate(FactsFor(sizeMeters: 2f), Fallback);

            Assert.AreEqual(ResidencySettings.DefaultLoadDistance, rule.LoadDistance, 0.001f);
            Assert.AreEqual(ResidencySettings.DefaultUnloadDistance, rule.ReleaseDistance, 0.001f);
        }

        [Test]
        public void AGraphWithoutAnOutputIsRejectedWithAnExplanation()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();
            _graph.AddNode(Number(10f));

            _program = _graph.Compile();

            Assert.IsFalse(_program.IsValid);
            StringAssert.Contains("Residency Output", _program.Problem);
        }

        [Test]
        public void AGraphWithTwoOutputsIsRejected()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();
            _graph.AddNode(Make<ResidencyOutputNode>());
            _graph.AddNode(Make<ResidencyOutputNode>());

            _program = _graph.Compile();

            Assert.IsFalse(_program.IsValid);
            StringAssert.Contains("Keep exactly one", _program.Problem);
        }

        [Test]
        public void ALoopIsRejectedRatherThanCompiledHalfWay()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            MathsNode first = Maths(RuleMathOperation.Add);
            MathsNode second = Maths(RuleMathOperation.Add);
            ResidencyOutputNode output = Make<ResidencyOutputNode>();

            _graph.AddNode(first);
            _graph.AddNode(second);
            _graph.AddNode(output);

            Wire(_graph, first, "m_result", second, "m_left");
            Wire(_graph, second, "m_result", first, "m_left");

            _program = _graph.Compile();

            Assert.IsFalse(_program.IsValid);
            StringAssert.Contains("loop", _program.Problem);
        }

        [Test]
        public void BiggerObjectsCanBeMadeToLoadFromFurtherAway()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            ObjectSizeNode size = Make<ObjectSizeNode>();
            NumberNode factor = Number(8f);
            MathsNode multiply = Maths(RuleMathOperation.Multiply);
            ClampNode clamp = Make<ClampNode>();
            ResidencyOutputNode output = Make<ResidencyOutputNode>();

            _graph.AddNode(size);
            _graph.AddNode(factor);
            _graph.AddNode(multiply);
            _graph.AddNode(clamp);
            _graph.AddNode(output);

            Wire(_graph, size, "m_meters", multiply, "m_left");
            Wire(_graph, factor, "m_result", multiply, "m_right");
            Wire(_graph, multiply, "m_result", clamp, "m_value");
            Wire(_graph, clamp, "m_result", output, "m_loadDistance");

            _program = _graph.Compile();
            Assert.IsTrue(_program.IsValid, _program.Problem);

            Assert.AreEqual(80f, _program.Evaluate(FactsFor(sizeMeters: 10f), Fallback).LoadDistance, 0.001f);
            Assert.AreEqual(240f, _program.Evaluate(FactsFor(sizeMeters: 30f), Fallback).LoadDistance, 0.001f);
        }

        [Test]
        public void TheClampBoundsAreRespectedAtBothEnds()
        {
            _graph = BuildSizeTimesFactorGraph(factor: 8f);
            _program = _graph.Compile();

            Assert.AreEqual(10f, _program.Evaluate(FactsFor(sizeMeters: 0.1f), Fallback).LoadDistance, 0.001f,
                "a tiny object must not fall below the lowest bound");
            Assert.AreEqual(500f, _program.Evaluate(FactsFor(sizeMeters: 5000f), Fallback).LoadDistance, 0.001f,
                "a huge object must not exceed the highest bound");
        }

        [Test]
        public void ATagCanSwitchBetweenTwoDistances()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            ObjectTagNode tag = Tagged("Hero");
            NumberNode near = Number(40f);
            NumberNode far = Number(300f);
            ChooseNode choose = Make<ChooseNode>();
            ResidencyOutputNode output = Make<ResidencyOutputNode>();

            _graph.AddNode(tag);
            _graph.AddNode(near);
            _graph.AddNode(far);
            _graph.AddNode(choose);
            _graph.AddNode(output);

            Wire(_graph, tag, "m_matches", choose, "m_condition");
            Wire(_graph, far, "m_result", choose, "m_then");
            Wire(_graph, near, "m_result", choose, "m_otherwise");
            Wire(_graph, choose, "m_result", output, "m_loadDistance");

            _program = _graph.Compile();
            Assert.IsTrue(_program.IsValid, _program.Problem);

            Assert.AreEqual(300f, _program.Evaluate(FactsFor(tagMatch: 1f), Fallback).LoadDistance, 0.001f);
            Assert.AreEqual(40f, _program.Evaluate(FactsFor(tagMatch: 0f), Fallback).LoadDistance, 0.001f);
        }

        [Test]
        public void AReleaseDistanceBelowTheLoadDistanceIsCorrected()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            NumberNode load = Number(100f);
            NumberNode release = Number(20f);
            ResidencyOutputNode output = Make<ResidencyOutputNode>();

            _graph.AddNode(load);
            _graph.AddNode(release);
            _graph.AddNode(output);

            Wire(_graph, load, "m_result", output, "m_loadDistance");
            Wire(_graph, release, "m_result", output, "m_releaseDistance");

            _program = _graph.Compile();
            ResolvedRule rule = _program.Evaluate(FactsFor(), Fallback);

            Assert.Greater(rule.ReleaseDistance, rule.LoadDistance,
                "a graph must never be able to produce a releasing distance below the loading distance");
        }

        [Test]
        public void DividingByZeroGivesZeroRatherThanSomethingUnusable()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            NumberNode numerator = Number(100f);
            NumberNode zero = Number(0f);
            MathsNode divide = Maths(RuleMathOperation.Divide);
            ResidencyOutputNode output = Make<ResidencyOutputNode>();

            _graph.AddNode(numerator);
            _graph.AddNode(zero);
            _graph.AddNode(divide);
            _graph.AddNode(output);

            Wire(_graph, numerator, "m_result", divide, "m_left");
            Wire(_graph, zero, "m_result", divide, "m_right");
            Wire(_graph, divide, "m_result", output, "m_loadDistance");

            _program = _graph.Compile();
            ResolvedRule rule = _program.Evaluate(FactsFor(), Fallback);

            Assert.AreEqual(0f, rule.LoadDistance, 0.001f);
            Assert.IsFalse(float.IsNaN(rule.ReleaseDistance));
        }

        [Test]
        public void AnUnconnectedInputFallsBackToItsTypedValue()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            ResidencyOutputNode output = Make<ResidencyOutputNode>();
            _graph.AddNode(output);

            _program = _graph.Compile();

            Assert.IsTrue(_program.IsValid, _program.Problem);
            Assert.AreEqual(ResidencySettings.DefaultLoadDistance,
                _program.Evaluate(FactsFor(), Fallback).LoadDistance, 0.001f);
        }

        [Test]
        public void EveryObjectGetsItsOwnAnswerFromTheSameProgram()
        {
            _graph = BuildSizeTimesFactorGraph(factor: 5f);
            _program = _graph.Compile();

            float small = _program.Evaluate(FactsFor(sizeMeters: 4f), Fallback).LoadDistance;
            float large = _program.Evaluate(FactsFor(sizeMeters: 40f), Fallback).LoadDistance;

            Assert.AreEqual(20f, small, 0.001f);
            Assert.AreEqual(200f, large, 0.001f);
        }

        private ResidencyGraphAsset _graph;
        private CompiledResidencyRules _program;

        private static readonly ResolvedRule Fallback = new ResolvedRule
        {
            LoadDistance = ResidencySettings.DefaultLoadDistance,
            ReleaseDistance = ResidencySettings.DefaultUnloadDistance,
            PriorityScale = 1f
        };

        private static ObjectFacts FactsFor(float sizeMeters = 1f, float memoryMegabytes = 1f, float tagMatch = 0f)
        {
            return new ObjectFacts
            {
                SizeMeters = sizeMeters,
                MemoryMegabytes = memoryMegabytes,
                TagMatch = tagMatch
            };
        }

        private static ResidencyGraphAsset BuildSizeTimesFactorGraph(float factor)
        {
            ResidencyGraphAsset graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            ObjectSizeNode size = Make<ObjectSizeNode>();
            NumberNode multiplier = Number(factor);
            MathsNode multiply = Maths(RuleMathOperation.Multiply);
            ClampNode clamp = Make<ClampNode>();
            ResidencyOutputNode output = Make<ResidencyOutputNode>();

            graph.AddNode(size);
            graph.AddNode(multiplier);
            graph.AddNode(multiply);
            graph.AddNode(clamp);
            graph.AddNode(output);

            Wire(graph, size, "m_meters", multiply, "m_left");
            Wire(graph, multiplier, "m_result", multiply, "m_right");
            Wire(graph, multiply, "m_result", clamp, "m_value");
            Wire(graph, clamp, "m_result", output, "m_loadDistance");

            return graph;
        }

        private static TNode Make<TNode>() where TNode : BaseNode
        {
            return BaseNode.CreateFromType<TNode>(Vector2.zero);
        }

        private static NumberNode Number(float value)
        {
            NumberNode node = Make<NumberNode>();
            node.Value = value;

            return node;
        }

        private static MathsNode Maths(RuleMathOperation operation)
        {
            MathsNode node = Make<MathsNode>();
            node.Operation = operation;

            return node;
        }

        private static ObjectTagNode Tagged(string tag)
        {
            ObjectTagNode node = Make<ObjectTagNode>();
            node.Tag = tag;

            return node;
        }

        private static void Wire(BaseGraph graph, BaseNode from, string fromField, BaseNode to, string toField)
        {
            graph.Connect(to.GetPort(toField, null), from.GetPort(fromField, null));
        }
    }
}
