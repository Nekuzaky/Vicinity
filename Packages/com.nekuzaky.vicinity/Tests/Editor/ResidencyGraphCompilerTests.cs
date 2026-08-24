using NUnit.Framework;
using Nekuzaky.Vicinity.Graph;
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
            _graph.Add(new NumberNode { Value = 10f });

            _program = _graph.Compile();

            Assert.IsFalse(_program.IsValid);
            StringAssert.Contains("Residency Output", _program.Problem);
        }

        [Test]
        public void AGraphWithTwoOutputsIsRejected()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();
            _graph.Add(new ResidencyOutputNode());
            _graph.Add(new ResidencyOutputNode());

            _program = _graph.Compile();

            Assert.IsFalse(_program.IsValid);
            StringAssert.Contains("Keep exactly one", _program.Problem);
        }

        [Test]
        public void ALoopIsRejectedRatherThanCompiledHalfWay()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            MathsNode first = new MathsNode { Operation = RuleMathOperation.Add };
            MathsNode second = new MathsNode { Operation = RuleMathOperation.Add };
            ResidencyOutputNode output = new ResidencyOutputNode();

            _graph.Add(first);
            _graph.Add(second);
            _graph.Add(output);

            _graph.Connect(first.Id, "m_result", second.Id, "m_left");
            _graph.Connect(second.Id, "m_result", first.Id, "m_left");

            _program = _graph.Compile();

            Assert.IsFalse(_program.IsValid);
            StringAssert.Contains("loop", _program.Problem);
        }

        [Test]
        public void BiggerObjectsCanBeMadeToLoadFromFurtherAway()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            ObjectSizeNode size = new ObjectSizeNode();
            NumberNode factor = new NumberNode { Value = 8f };
            MathsNode multiply = new MathsNode { Operation = RuleMathOperation.Multiply };
            ClampNode clamp = new ClampNode();
            ResidencyOutputNode output = new ResidencyOutputNode();

            _graph.Add(size);
            _graph.Add(factor);
            _graph.Add(multiply);
            _graph.Add(clamp);
            _graph.Add(output);

            _graph.Connect(size.Id, "m_meters", multiply.Id, "m_left");
            _graph.Connect(factor.Id, "m_result", multiply.Id, "m_right");
            _graph.Connect(multiply.Id, "m_result", clamp.Id, "m_value");
            _graph.Connect(clamp.Id, "m_result", output.Id, "m_loadDistance");

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

            ObjectTagNode tag = new ObjectTagNode { Tag = "Hero" };
            NumberNode near = new NumberNode { Value = 40f };
            NumberNode far = new NumberNode { Value = 300f };
            ChooseNode choose = new ChooseNode();
            ResidencyOutputNode output = new ResidencyOutputNode();

            _graph.Add(tag);
            _graph.Add(near);
            _graph.Add(far);
            _graph.Add(choose);
            _graph.Add(output);

            _graph.Connect(tag.Id, "m_matches", choose.Id, "m_condition");
            _graph.Connect(far.Id, "m_result", choose.Id, "m_then");
            _graph.Connect(near.Id, "m_result", choose.Id, "m_otherwise");
            _graph.Connect(choose.Id, "m_result", output.Id, "m_loadDistance");

            _program = _graph.Compile();
            Assert.IsTrue(_program.IsValid, _program.Problem);

            Assert.AreEqual(300f, _program.Evaluate(FactsFor(tagMatch: 1f), Fallback).LoadDistance, 0.001f);
            Assert.AreEqual(40f, _program.Evaluate(FactsFor(tagMatch: 0f), Fallback).LoadDistance, 0.001f);
        }

        [Test]
        public void AReleaseDistanceBelowTheLoadDistanceIsCorrected()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            NumberNode load = new NumberNode { Value = 100f };
            NumberNode release = new NumberNode { Value = 20f };
            ResidencyOutputNode output = new ResidencyOutputNode();

            _graph.Add(load);
            _graph.Add(release);
            _graph.Add(output);

            _graph.Connect(load.Id, "m_result", output.Id, "m_loadDistance");
            _graph.Connect(release.Id, "m_result", output.Id, "m_releaseDistance");

            _program = _graph.Compile();
            ResolvedRule rule = _program.Evaluate(FactsFor(), Fallback);

            Assert.Greater(rule.ReleaseDistance, rule.LoadDistance,
                "a graph must never be able to produce a releasing distance below the loading distance");
        }

        [Test]
        public void DividingByZeroGivesZeroRatherThanSomethingUnusable()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            NumberNode numerator = new NumberNode { Value = 100f };
            NumberNode zero = new NumberNode { Value = 0f };
            MathsNode divide = new MathsNode { Operation = RuleMathOperation.Divide };
            ResidencyOutputNode output = new ResidencyOutputNode();

            _graph.Add(numerator);
            _graph.Add(zero);
            _graph.Add(divide);
            _graph.Add(output);

            _graph.Connect(numerator.Id, "m_result", divide.Id, "m_left");
            _graph.Connect(zero.Id, "m_result", divide.Id, "m_right");
            _graph.Connect(divide.Id, "m_result", output.Id, "m_loadDistance");

            _program = _graph.Compile();
            ResolvedRule rule = _program.Evaluate(FactsFor(), Fallback);

            Assert.AreEqual(0f, rule.LoadDistance, 0.001f);
            Assert.IsFalse(float.IsNaN(rule.ReleaseDistance));
        }

        [Test]
        public void AnUnconnectedInputFallsBackToItsTypedValue()
        {
            _graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            ResidencyOutputNode output = new ResidencyOutputNode();
            _graph.Add(output);

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

            ObjectSizeNode size = new ObjectSizeNode();
            NumberNode multiplier = new NumberNode { Value = factor };
            MathsNode multiply = new MathsNode { Operation = RuleMathOperation.Multiply };
            ClampNode clamp = new ClampNode();
            ResidencyOutputNode output = new ResidencyOutputNode();

            graph.Add(size);
            graph.Add(multiplier);
            graph.Add(multiply);
            graph.Add(clamp);
            graph.Add(output);

            graph.Connect(size.Id, "m_meters", multiply.Id, "m_left");
            graph.Connect(multiplier.Id, "m_result", multiply.Id, "m_right");
            graph.Connect(multiply.Id, "m_result", clamp.Id, "m_value");
            graph.Connect(clamp.Id, "m_result", output.Id, "m_loadDistance");

            return graph;
        }
    }
}
