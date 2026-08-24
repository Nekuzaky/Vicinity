using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Nekuzaky.Vicinity.Graph
{
    internal static class RuleProgramEvaluation
    {
        internal const float MinimumReleaseRatio = 1.05f;
        internal const float MaximumDistance = 100000f;
        internal const float MinimumPriorityScale = 0.01f;
        internal const float MaximumPriorityScale = 100f;

        internal static void Run(
            in NativeArray<RuleInstruction> instructions,
            in NativeArray<float> constants,
            in ObjectFacts facts,
            NativeArray<float> registers)
        {
            for (int i = 0; i < instructions.Length; i++)
            {
                RuleInstruction instruction = instructions[i];

                switch (instruction.Op)
                {
                    case RuleOp.Constant:
                        registers[i] = constants[instruction.A];
                        break;

                    case RuleOp.Size:
                        registers[i] = facts.SizeMeters;
                        break;

                    case RuleOp.Memory:
                        registers[i] = facts.MemoryMegabytes;
                        break;

                    case RuleOp.TagMatch:
                        registers[i] = facts.TagMatch;
                        break;

                    case RuleOp.Add:
                        registers[i] = registers[instruction.A] + registers[instruction.B];
                        break;

                    case RuleOp.Subtract:
                        registers[i] = registers[instruction.A] - registers[instruction.B];
                        break;

                    case RuleOp.Multiply:
                        registers[i] = registers[instruction.A] * registers[instruction.B];
                        break;

                    case RuleOp.Divide:
                        registers[i] = SafeDivide(registers[instruction.A], registers[instruction.B]);
                        break;

                    case RuleOp.Minimum:
                        registers[i] = math.min(registers[instruction.A], registers[instruction.B]);
                        break;

                    case RuleOp.Maximum:
                        registers[i] = math.max(registers[instruction.A], registers[instruction.B]);
                        break;

                    case RuleOp.Clamp:
                        registers[i] = math.clamp(registers[instruction.A], registers[instruction.B], registers[instruction.C]);
                        break;

                    case RuleOp.Greater:
                        registers[i] = registers[instruction.A] > registers[instruction.B] ? 1f : 0f;
                        break;

                    case RuleOp.Less:
                        registers[i] = registers[instruction.A] < registers[instruction.B] ? 1f : 0f;
                        break;

                    case RuleOp.And:
                        registers[i] = registers[instruction.A] > 0.5f && registers[instruction.B] > 0.5f ? 1f : 0f;
                        break;

                    case RuleOp.Or:
                        registers[i] = registers[instruction.A] > 0.5f || registers[instruction.B] > 0.5f ? 1f : 0f;
                        break;

                    case RuleOp.Not:
                        registers[i] = registers[instruction.A] > 0.5f ? 0f : 1f;
                        break;

                    case RuleOp.Select:
                        registers[i] = registers[instruction.A] > 0.5f ? registers[instruction.B] : registers[instruction.C];
                        break;

                    default:
                        registers[i] = 0f;
                        break;
                }
            }
        }

        internal static ResolvedRule MakeSafe(in ResolvedRule resolved, in ResolvedRule fallback)
        {
            float load = Sanitise(resolved.LoadDistance, fallback.LoadDistance);
            load = math.clamp(load, 0f, MaximumDistance);

            float release = Sanitise(resolved.ReleaseDistance, fallback.ReleaseDistance);
            release = math.clamp(release, load * MinimumReleaseRatio, MaximumDistance);

            float priority = Sanitise(resolved.PriorityScale, fallback.PriorityScale);
            priority = math.clamp(priority, MinimumPriorityScale, MaximumPriorityScale);

            return new ResolvedRule
            {
                LoadDistance = load,
                ReleaseDistance = release,
                PriorityScale = priority
            };
        }

        private static float Sanitise(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return math.abs(denominator) < math.EPSILON ? 0f : numerator / denominator;
        }
    }

    [BurstCompile]
    internal struct ResidencyRuleJob : IJob
    {
        [ReadOnly] public NativeArray<RuleInstruction> Instructions;
        [ReadOnly] public NativeArray<float> Constants;
        [ReadOnly] public NativeArray<ObjectFacts> Facts;

        public NativeArray<float> Registers;

        public ResolvedRule Fallback;
        public int LoadRegister;
        public int ReleaseRegister;
        public int PriorityRegister;

        [WriteOnly] public NativeArray<ResolvedRule> Results;

        public void Execute()
        {
            for (int i = 0; i < Facts.Length; i++)
            {
                RuleProgramEvaluation.Run(Instructions, Constants, Facts[i], Registers);

                ResolvedRule raw = new ResolvedRule
                {
                    LoadDistance = Registers[LoadRegister],
                    ReleaseDistance = Registers[ReleaseRegister],
                    PriorityScale = Registers[PriorityRegister]
                };

                Results[i] = RuleProgramEvaluation.MakeSafe(raw, Fallback);
            }
        }
    }
}
