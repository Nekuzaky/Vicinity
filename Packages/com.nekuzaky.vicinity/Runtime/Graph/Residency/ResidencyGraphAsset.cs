using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Nekuzaky.Vicinity.Graph
{
    internal sealed class RuleProgramBuilder
    {
        #region Main Methods

        internal RuleProgramBuilder(VicinityGraphAsset graph)
        {
            _graph = graph;

            foreach (NodeEdge edge in graph.Edges)
            {
                _incoming[EdgeKey(edge.ToNodeId, edge.ToPort)] = edge;
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

        internal void Bind(VicinityNode node, int register)
        {
            _registers[node.Id] = register;
        }

        internal int InputRegister(VicinityNode node, string fieldName, float fallback)
        {
            if (!_incoming.TryGetValue(EdgeKey(node.Id, fieldName), out NodeEdge edge))
            {
                return EmitConstant(fallback);
            }

            return _registers.TryGetValue(edge.FromNodeId, out int register)
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

        private readonly VicinityGraphAsset _graph;
        private readonly List<RuleInstruction> _instructions = new List<RuleInstruction>();
        private readonly List<float> _constants = new List<float>();
        private readonly Dictionary<string, int> _registers = new Dictionary<string, int>();
        private readonly Dictionary<string, NodeEdge> _incoming = new Dictionary<string, NodeEdge>();

        private static string EdgeKey(string nodeId, string port) => $"{nodeId}|{port}";

        #endregion
    }

    /// <summary>
    /// A graph that decides, per object, how close the player must be for it to load. Compiled once
    /// into a flat program, then evaluated for every managed object without reflection.
    /// </summary>
    public sealed class ResidencyGraphAsset : VicinityGraphAsset
    {
        #region Main Methods

        /// <summary>Turns this graph into a program. Never throws; check the result before using it.</summary>
        public CompiledResidencyRules Compile()
        {
            RemoveBrokenParts();

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

            foreach (VicinityNode node in executor.Order)
            {
                if (node == output)
                {
                    continue;
                }

                if (node is not ResidencyRuleNode rule)
                {
                    return CompiledResidencyRules.Rejected(
                        $"'{node.Title}' does not belong in a residency graph.");
                }

                builder.Bind(rule, rule.Emit(builder));
            }

            int load = builder.InputRegister(output, "m_loadDistance", DefaultLoadDistance);
            int release = builder.InputRegister(output, "m_releaseDistance", DefaultReleaseDistance);
            int priority = builder.InputRegister(output, "m_priorityScale", DefaultPriorityScale);

            return builder.Finish(load, release, priority, tag);
        }

        /// <summary>Creates a graph that reproduces Vicinity's built-in behaviour, ready to be edited.</summary>
        public static ResidencyGraphAsset CreateStartingPoint()
        {
            ResidencyGraphAsset graph = CreateInstance<ResidencyGraphAsset>();

            NumberNode loadDistance = new NumberNode { Value = DefaultLoadDistance, Position = new Vector2(-260f, -60f) };
            NumberNode releaseDistance = new NumberNode { Value = DefaultReleaseDistance, Position = new Vector2(-260f, 40f) };
            ResidencyOutputNode output = new ResidencyOutputNode { Position = new Vector2(60f, -10f) };

            graph.Add(loadDistance);
            graph.Add(releaseDistance);
            graph.Add(output);

            graph.Connect(loadDistance.Id, "m_result", output.Id, "m_loadDistance");
            graph.Connect(releaseDistance.Id, "m_result", output.Id, "m_releaseDistance");

            return graph;
        }

        #endregion

        #region Privates

        private const float DefaultLoadDistance = ResidencySettings.DefaultLoadDistance;
        private const float DefaultReleaseDistance = ResidencySettings.DefaultUnloadDistance;
        private const float DefaultPriorityScale = 1f;

        private bool TryFindTag(out string tag, out string problem)
        {
            tag = string.Empty;
            problem = string.Empty;

            foreach (VicinityNode node in Nodes)
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

            foreach (VicinityNode node in Nodes)
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
