using Nekuzaky.Vicinity.Editor.Graph;
using Nekuzaky.Vicinity.Graph;
using NUnit.Framework;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class GraphSeedingTests
    {
        #region Main Methods

        [Test]
        public void AnEmptyGraphIsFilledInSoItNeverOpensOnAnError()
        {
            ResidencyGraphAsset graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            try
            {
                Assert.IsTrue(ResidencyGraphCreation.SeedIfEmpty(graph), "an empty graph is exactly what needs seeding");

                CompiledResidencyRules compiled = graph.Compile();

                Assert.IsTrue(compiled.IsValid, compiled.Problem);
                compiled.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AGraphTheUserAlreadyBuiltIsLeftAlone()
        {
            ResidencyGraphAsset graph = ResidencyGraphAsset.CreateStartingPoint();

            try
            {
                int before = graph.Nodes.Count;

                Assert.IsFalse(ResidencyGraphCreation.SeedIfEmpty(graph),
                    "seeding a graph that already has nodes would duplicate its output");

                Assert.AreEqual(before, graph.Nodes.Count);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void TheStartingPointReproducesVicinitysOwnDistances()
        {
            ResidencyGraphAsset graph = ResidencyGraphAsset.CreateStartingPoint();

            try
            {
                CompiledResidencyRules compiled = graph.Compile();

                Assert.IsTrue(compiled.IsValid, compiled.Problem);

                ResolvedRule fallback = new ResolvedRule { LoadDistance = -1f, ReleaseDistance = -1f, PriorityScale = 1f };
                ResolvedRule rule = compiled.Evaluate(new ObjectFacts { SizeMeters = 4f, MemoryMegabytes = 8f }, fallback);

                Assert.AreEqual(ResidencySettings.DefaultLoadDistance, rule.LoadDistance, 0.01f);
                Assert.AreEqual(ResidencySettings.DefaultUnloadDistance, rule.ReleaseDistance, 0.01f);

                compiled.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        #endregion
    }
}
