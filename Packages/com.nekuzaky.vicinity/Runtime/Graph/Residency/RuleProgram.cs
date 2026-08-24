using System;
using Unity.Collections;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>What Vicinity knows about one object before deciding how to treat it.</summary>
    public struct ObjectFacts
    {
        /// <summary>How large the object is, in meters, measured from what it draws.</summary>
        public float SizeMeters;

        /// <summary>Roughly how much memory its models take, in megabytes.</summary>
        public float MemoryMegabytes;

        /// <summary>1 when the object carries the tag the graph asks about, 0 otherwise.</summary>
        public float TagMatch;
    }

    /// <summary>The distances and priority a graph decided for one object.</summary>
    public struct ResolvedRule
    {
        /// <summary>How close the player must be before the object loads, in meters.</summary>
        public float LoadDistance;

        /// <summary>How far the player must be before the object is released, in meters.</summary>
        public float ReleaseDistance;

        /// <summary>Multiplier applied to this object's loading priority. Above 1 means later.</summary>
        public float PriorityScale;
    }

    internal enum RuleOp : byte
    {
        Constant = 0,
        Size = 1,
        Memory = 2,
        TagMatch = 3,
        Add = 4,
        Subtract = 5,
        Multiply = 6,
        Divide = 7,
        Minimum = 8,
        Maximum = 9,
        Clamp = 10,
        Greater = 11,
        Less = 12,
        And = 13,
        Or = 14,
        Not = 15,
        Select = 16
    }

    internal struct RuleInstruction
    {
        public RuleOp Op;
        public int A;
        public int B;
        public int C;
    }

    /// <summary>
    /// A residency graph turned into a flat list of instructions. Evaluating it costs no reflection
    /// and no allocation, which is what makes it affordable on tens of thousands of objects.
    /// </summary>
    public sealed class CompiledResidencyRules : IDisposable
    {
        #region Main Methods

        /// <summary>True when the graph compiled into something that can be evaluated.</summary>
        public bool IsValid { get; private set; }

        /// <summary>Why the graph did not compile, or an empty string.</summary>
        public string Problem { get; private set; } = string.Empty;

        /// <summary>How many instructions the program holds.</summary>
        public int InstructionCount => _instructions.IsCreated ? _instructions.Length : 0;

        /// <summary>Builds an unusable program that explains what went wrong.</summary>
        public static CompiledResidencyRules Rejected(string problem)
        {
            return new CompiledResidencyRules
            {
                IsValid = false,
                Problem = problem
            };
        }

        /// <summary>Evaluates the program for one object. Returns the fallback when invalid.</summary>
        public ResolvedRule Evaluate(in ObjectFacts facts, in ResolvedRule fallback)
        {
            if (!IsValid)
            {
                return fallback;
            }

            NativeArray<float> registers = new NativeArray<float>(_instructions.Length, Allocator.Temp);
            RuleProgramEvaluation.Run(_instructions, _constants, facts, registers);

            ResolvedRule resolved = new ResolvedRule
            {
                LoadDistance = registers[_loadRegister],
                ReleaseDistance = registers[_releaseRegister],
                PriorityScale = registers[_priorityRegister]
            };

            registers.Dispose();
            return RuleProgramEvaluation.MakeSafe(resolved, fallback);
        }

        /// <summary>Releases the native memory holding the program.</summary>
        public void Dispose()
        {
            if (_instructions.IsCreated)
            {
                _instructions.Dispose();
            }

            if (_constants.IsCreated)
            {
                _constants.Dispose();
            }

            IsValid = false;
        }

        #endregion

        #region Privates

        private NativeArray<RuleInstruction> _instructions;
        private NativeArray<float> _constants;
        private int _loadRegister;
        private int _releaseRegister;
        private int _priorityRegister;

        internal static CompiledResidencyRules Accepted(
            NativeArray<RuleInstruction> instructions,
            NativeArray<float> constants,
            int loadRegister,
            int releaseRegister,
            int priorityRegister)
        {
            return new CompiledResidencyRules
            {
                IsValid = true,
                _instructions = instructions,
                _constants = constants,
                _loadRegister = loadRegister,
                _releaseRegister = releaseRegister,
                _priorityRegister = priorityRegister
            };
        }

        internal NativeArray<RuleInstruction> Instructions => _instructions;

        internal NativeArray<float> Constants => _constants;

        internal int LoadRegister => _loadRegister;

        internal int ReleaseRegister => _releaseRegister;

        internal int PriorityRegister => _priorityRegister;

        #endregion
    }
}
