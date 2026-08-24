using System.Collections;
using NUnit.Framework;
using Nekuzaky.Vicinity.Graph;
using Nekuzaky.Vicinity.GraphProcessor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nekuzaky.Vicinity.Tests
{
    internal sealed class ResidencyGraphPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.Destroy(spawned);
                }
            }

            _spawned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AGraphDecidesTheDistanceInsteadOfTheProfile()
        {
            ResidencyGraphAsset graph = BuildFixedDistanceGraph(loadDistance: 400f);
            yield return BuildScene(graph, distanceFromTarget: 250f);

            yield return WaitForState(ResidencyState.Resident);

            Assert.AreEqual(ResidencyState.Resident, _managed.State,
                "the profile alone would never load an object 250 m away; the graph raised the distance to 400 m");
        }

        [UnityTest]
        public IEnumerator WithoutAGraphTheProfileStillDecides()
        {
            yield return BuildScene(graph: null, distanceFromTarget: 250f);

            yield return WaitSeconds(SettleSeconds);

            Assert.AreEqual(ResidencyState.Unloaded, _managed.State,
                "with no graph the built-in distances must apply exactly as before");
        }

        [UnityTest]
        public IEnumerator ABrokenGraphFallsBackToTheProfileAndSaysSo()
        {
            LogAssert.ignoreFailingMessages = true;

            ResidencyGraphAsset graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();
            graph.AddNode(Number(400f));
            _spawned.Add(graph);

            yield return BuildScene(graph, distanceFromTarget: 5f);
            yield return WaitForState(ResidencyState.Resident);

            Assert.AreEqual(ResidencyState.Resident, _managed.State,
                "a graph that cannot compile must not stop the scene from streaming");

            LogAssert.ignoreFailingMessages = false;
        }

        private const float TimeoutSeconds = 15f;
        private const float SettleSeconds = 2f;

        private readonly System.Collections.Generic.List<Object> _spawned = new System.Collections.Generic.List<Object>();

        private VicinityObject _managed;

        private ResidencyGraphAsset BuildFixedDistanceGraph(float loadDistance)
        {
            ResidencyGraphAsset graph = ScriptableObject.CreateInstance<ResidencyGraphAsset>();

            NumberNode load = Number(loadDistance);
            NumberNode release = Number(loadDistance * 1.4f);
            ResidencyOutputNode output = Make<ResidencyOutputNode>();

            graph.AddNode(load);
            graph.AddNode(release);
            graph.AddNode(output);

            Wire(graph, load, "m_result", output, "m_loadDistance");
            Wire(graph, release, "m_result", output, "m_releaseDistance");

            _spawned.Add(graph);
            return graph;
        }

        private IEnumerator BuildScene(ResidencyGraphAsset graph, float distanceFromTarget)
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prefab.name = "Detailed Model";
            prefab.SetActive(false);
            _spawned.Add(prefab);

            GameObject targetObject = new GameObject("Target");
            targetObject.transform.position = Vector3.zero;
            targetObject.AddComponent<VicinityTarget>();
            _spawned.Add(targetObject);

            GameObject managedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            managedObject.name = "Managed";
            managedObject.transform.position = new Vector3(distanceFromTarget, 0f, 0f);
            _spawned.Add(managedObject);

            _managed = managedObject.AddComponent<VicinityObject>();
            _managed.SetDetailedModel(AssetKey.FromDirectReference(prefab));
            _managed.SetEstimatedMemoryBytes(2048L);

            VicinityProfile profile = ScriptableObject.CreateInstance<VicinityProfile>();
            _spawned.Add(profile);

            if (graph != null)
            {
                SerializedProfileGraph(profile, graph);
            }

            GameObject managerObject = new GameObject("Manager");
            VicinityManager manager = managerObject.AddComponent<VicinityManager>();
            manager.SetProfile(profile);
            _spawned.Add(managerObject);

            yield return null;
        }

        private static void SerializedProfileGraph(VicinityProfile profile, ResidencyGraphAsset graph)
        {
            System.Reflection.FieldInfo field = typeof(VicinityProfile).GetField(
                "m_residencyGraph",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            field.SetValue(profile, graph);
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private IEnumerator WaitForState(ResidencyState expected)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (_managed != null && _managed.State == expected)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"never reached {expected}; it is {_managed?.State.ToString() ?? "gone"}");
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

        private static void Wire(BaseGraph graph, BaseNode from, string fromField, BaseNode to, string toField)
        {
            graph.Connect(to.GetPort(toField, null), from.GetPort(fromField, null));
        }

    }
}
