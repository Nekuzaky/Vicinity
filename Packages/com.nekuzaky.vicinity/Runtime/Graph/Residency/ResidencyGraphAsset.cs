using System.Collections.Generic;
using Nekuzaky.Vicinity.GraphProcessor;
using Unity.Collections;
using UnityEngine;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>
    /// Turns a graph into the flat program the Burst job runs. Nothing here happens at runtime: a graph is
    /// compiled once, and what the job sees afterwards is instructions and constants, never nodes.
    /// </summary>
    internal sealed class RuleProgramBuilder
    {
        #region Main Methods

        internal RuleProgramBuilder(BaseGraph graph)
        {
            foreach (SerializableEdge edge in graph.edges)
            {
                if (edge?.inputNode == null || edge.outputNode == null)
                {
                    continue;
                }

                _incoming[EdgeKey(edge.inputNode.GUID, edge.inputFieldName)] = edge;
            }
        }

        internal int Emit(RuleOp op, int a = 0, int b = 0, int c = 0)
        {
            _instructions.Add(new RuleInstruction { Op = op, A = a, B = b, C = c });
            return _instructions.Count - 1;
        }

        internal int EmitConstant(float value)
        {
            _constants.Add(value);
            return Emit(RuleOp.Constant, _constants.Count - 1);
        }

        internal void Bind(BaseNode node, int register)
        {
            _registers[node.GUID] = register;
        }

        /// <summary>
        /// The register feeding one input of <paramref name="node"/>. An input nothing is wired into keeps
        /// the value typed on the node, which is what makes a half-built graph still compile.
        /// </summary>
        internal int InputRegister(BaseNode node, string fieldName, float fallback)
        {
            if (!_incoming.TryGetValue(EdgeKey(node.GUID, fieldName), out SerializableEdge edge))
            {
                return EmitConstant(fallback);
            }

            return _registers.TryGetValue(edge.outputNode.GUID, out int register)
                ? register
                : EmitConstant(fallback);
        }

        internal CompiledResidencyRules Finish(int loadRegister, int releaseRegister, int priorityRegister, string tag)
        {
            NativeArray<RuleInstruction> instructions =
                new NativeArray<RuleInstruction>(_instructions.Count, Allocator.Persistent);

            for (int i = 0; i < _instructions.Count; i++)
            {
                instructions[i] = _instructions[i];
            }

            NativeArray<float> constants = new NativeArray<float>(
                Mathf.Max(_constants.Count, 1), Allocator.Persistent);

            for (int i = 0; i < _constants.Count; i++)
            {
                constants[i] = _constants[i];
            }

            return CompiledResidencyRules.Accepted(
                instructions, constants, loadRegister, releaseRegister, priorityRegister, tag);
        }

        #endregion

        #region Privates

        private readonly List<RuleInstruction> _instructions = new List<RuleInstruction>();
        private readonly List<float> _constants = new List<float>();
        private readonly Dictionary<string, int> _registers = new Dictionary<string, int>();
        private readonly Dictionary<string, SerializableEdge> _incoming = new Dictionary<string, SerializableEdge>();

        private static string EdgeKey(string nodeId, string port) => $"{nodeId}|{port}";

        #endregion
    }

    /// <summary>
    /// A graph that decides, per object, how close the player must be for it to load. Compiled once into a
    /// flat program, then evaluated for every managed object without reflection.
    /// </summary>
    public sealed class ResidencyGraphAsset : BaseGraph
    {
        #region Main Methods

        /// <summary>Turns this graph into a program. Never throws; check the result before using it.</summary>
        public CompiledResidencyRules Compile()
        {
            if (!TryFindOutput(out ResidencyOutputNode output, out string outputProblem))
            {
                return CompiledResidencyRules.Rejected(outputProblem);
            }

            GraphExecutor executor = new GraphExecutor(this);

            if (executor.HasCircularDependency)
            {
                return CompiledResidencyRules.Rejected(
                    "The nodes form a loop, so there is no order in which they could run.");
            }

            if (!TryFindTag(out string tag, out string tagProblem))
            {
                return CompiledResidencyRules.Rejected(tagProblem);
            }

            RuleProgramBuilder builder = new RuleProgramBuilder(this);

            foreach (BaseNode node in executor.Order)
            {
                if (node == output)
                {
                    continue;
                }

                if (node is not ResidencyRuleNode rule)
                {
                    return CompiledResidencyRules.Rejected(
                        $"'{node.name}' does not belong in a residency graph.");
                }

                builder.Bind(rule, rule.Emit(builder));
            }

            int load = builder.InputRegister(output, ResidencyOutputNode.LoadField, DefaultLoadDistance);
            int release = builder.InputRegister(output, ResidencyOutputNode.ReleaseField, DefaultReleaseDistance);
            int priority = builder.InputRegister(output, ResidencyOutputNode.PriorityField, DefaultPriorityScale);

            return builder.Finish(load, release, priority, tag);
        }

        /// <summary>Creates a graph that reproduces Vicinity's built-in behaviour, ready to be edited.</summary>
        public static ResidencyGraphAsset CreateStartingPoint()
        {
            ResidencyGraphAsset graph = CreateInstance<ResidencyGraphAsset>();
            graph.Seed();

            return graph;
        }

        /// <summary>
        /// Fills an empty graph with the nodes that reproduce Vicinity's built-in behaviour. Does nothing to a
        /// graph that already holds something, so it is safe to call on one the user has built.
        /// </summary>
        public bool Seed()
        {
            if (nodes.Count > 0)
            {
                return false;
            }

            NumberNode loadDistance = BaseNode.CreateFromType<NumberNode>(new Vector2(-320f, -80f));
            loadDistance.Value = DefaultLoadDistance;

            NumberNode releaseDistance = BaseNode.CreateFromType<NumberNode>(new Vector2(-320f, 60f));
            releaseDistance.Value = DefaultReleaseDistance;

            ResidencyOutputNode output = BaseNode.CreateFromType<ResidencyOutputNode>(new Vector2(60f, -20f));

            AddNode(loadDistance);
            AddNode(releaseDistance);
            AddNode(output);

            Wire(loadDistance, ResultField, output, ResidencyOutputNode.LoadField);
            Wire(releaseDistance, ResultField, output, ResidencyOutputNode.ReleaseField);

            return true;
        }

        #endregion

        #region Privates

        private const float DefaultLoadDistance = ResidencySettings.DefaultLoadDistance;
        private const float DefaultReleaseDistance = ResidencySettings.DefaultUnloadDistance;
        private const float DefaultPriorityScale = 1f;
        private const string ResultField = "m_result";

        private void Wire(BaseNode from, string fromField, BaseNode to, string toField)
        {
            NodePort source = from.GetPort(fromField, null);
            NodePort destination = to.GetPort(toField, null);

            if (source != null && destination != null)
            {
                Connect(destination, source);
            }
        }

        private bool TryFindTag(out string tag, out string problem)
        {
            tag = string.Empty;
            problem = string.Empty;

            foreach (BaseNode node in nodes)
            {
                if (node is not ObjectTagNode tagNode || string.IsNullOrEmpty(tagNode.Tag))
                {
                    continue;
                }

                if (tag.Length == 0)
                {
                    tag = tagNode.Tag;
                    continue;
                }

                if (tag != tagNode.Tag)
                {
                    problem = $"This graph asks about two different tags, '{tag}' and '{tagNode.Tag}'. One graph can ask about a single tag.";
                    return false;
                }
            }

            return true;
        }

        private bool TryFindOutput(out ResidencyOutputNode output, out string problem)
        {
            output = null;
            int found = 0;

            foreach (BaseNode node in nodes)
            {
                if (node is ResidencyOutputNode candidate)
                {
                    output = candidate;
                    found++;
                }
            }

            if (found == 0)
            {
                problem = "This graph has no Residency Output node, so nothing tells Vicinity what to do.";
                return false;
            }

            if (found > 1)
            {
                problem = $"This graph has {found} Residency Output nodes. Keep exactly one.";
                return false;
            }

            problem = string.Empty;
            return true;
        }

        #endregion
    }
}
