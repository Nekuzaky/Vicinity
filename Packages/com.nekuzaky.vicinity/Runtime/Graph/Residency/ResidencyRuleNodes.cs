using System;
using UnityEngine;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>Base for every node that can live in a residency graph.</summary>
    [Serializable]
    public abstract class ResidencyRuleNode : VicinityNode
    {
        internal abstract int Emit(RuleProgramBuilder builder);
    }

    /// <summary>A fixed number you type in.</summary>
    [Serializable]
    [GraphNodeMenu("Value/Number")]
    public sealed class NumberNode : ResidencyRuleNode
    {
        [SerializeField] private float m_value;
        [SerializeField] [GraphOutput("Result")] private float m_result;

        /// <inheritdoc />
        public override string Title => "Number";

        /// <inheritdoc />
        public override string Summary => "A fixed number, in whatever unit the socket it feeds expects.";

        /// <summary>The number this node outputs.</summary>
        public float Value
        {
            get => m_value;
            set => m_value = value;
        }

        /// <inheritdoc />
        public override void Process() => m_result = m_value;

        internal override int Emit(RuleProgramBuilder builder) => builder.EmitConstant(m_value);
    }

    /// <summary>How large the object is, measured from what it draws.</summary>
    [Serializable]
    [GraphNodeMenu("Object/Size")]
    public sealed class ObjectSizeNode : ResidencyRuleNode
    {
        [SerializeField] [GraphOutput("Meters")] private float m_meters;

        /// <inheritdoc />
        public override string Title => "Object Size";

        /// <inheritdoc />
        public override string Summary => "How large this object is, in meters. Bigger things are usually worth loading from further away.";

        /// <inheritdoc />
        public override void Process()
        {
        }

        internal override int Emit(RuleProgramBuilder builder) => builder.Emit(RuleOp.Size);
    }

    /// <summary>Roughly how much memory the object's models take.</summary>
    [Serializable]
    [GraphNodeMenu("Object/Memory")]
    public sealed class ObjectMemoryNode : ResidencyRuleNode
    {
        [SerializeField] [GraphOutput("Megabytes")] private float m_megabytes;

        /// <inheritdoc />
        public override string Title => "Object Memory";

        /// <inheritdoc />
        public override string Summary => "Roughly how much memory this object's models take, in megabytes.";

        /// <inheritdoc />
        public override void Process()
        {
        }

        internal override int Emit(RuleProgramBuilder builder) => builder.Emit(RuleOp.Memory);
    }

    /// <summary>Whether the object carries a given tag.</summary>
    [Serializable]
    [GraphNodeMenu("Object/Has Tag")]
    public sealed class ObjectTagNode : ResidencyRuleNode
    {
        [SerializeField] private string m_tag = "Untagged";
        [SerializeField] [GraphOutput("Matches")] private float m_matches;

        /// <inheritdoc />
        public override string Title => "Has Tag";

        /// <inheritdoc />
        public override string Summary => "1 when the object carries this tag, 0 otherwise. Feed it into a Choose node.";

        /// <summary>The tag this node looks for.</summary>
        public string Tag
        {
            get => m_tag;
            set => m_tag = value;
        }

        /// <inheritdoc />
        public override void Process()
        {
        }

        internal override int Emit(RuleProgramBuilder builder) => builder.Emit(RuleOp.TagMatch);
    }

    /// <summary>What a Maths node does with its two inputs.</summary>
    public enum RuleMathOperation
    {
        /// <summary>Adds the two inputs.</summary>
        Add = 0,

        /// <summary>Subtracts the second input from the first.</summary>
        Subtract = 1,

        /// <summary>Multiplies the two inputs.</summary>
        Multiply = 2,

        /// <summary>Divides the first input by the second. Dividing by zero gives zero.</summary>
        Divide = 3,

        /// <summary>Keeps whichever input is smaller.</summary>
        Minimum = 4,

        /// <summary>Keeps whichever input is larger.</summary>
        Maximum = 5
    }

    /// <summary>Combines two numbers.</summary>
    [Serializable]
    [GraphNodeMenu("Maths/Maths")]
    public sealed class MathsNode : ResidencyRuleNode
    {
        [SerializeField] private RuleMathOperation m_operation = RuleMathOperation.Multiply;
        [SerializeField] [GraphInput] private float m_left = 1f;
        [SerializeField] [GraphInput] private float m_right = 1f;
        [SerializeField] [GraphOutput("Result")] private float m_result;

        /// <inheritdoc />
        public override string Title => m_operation.ToString();

        /// <inheritdoc />
        public override string Summary => "Combines two numbers.";

        /// <summary>What this node does with its inputs.</summary>
        public RuleMathOperation Operation
        {
            get => m_operation;
            set => m_operation = value;
        }

        /// <inheritdoc />
        public override void Process()
        {
            m_result = m_operation switch
            {
                RuleMathOperation.Add => m_left + m_right,
                RuleMathOperation.Subtract => m_left - m_right,
                RuleMathOperation.Divide => Mathf.Approximately(m_right, 0f) ? 0f : m_left / m_right,
                RuleMathOperation.Minimum => Mathf.Min(m_left, m_right),
                RuleMathOperation.Maximum => Mathf.Max(m_left, m_right),
                _ => m_left * m_right
            };
        }

        internal override int Emit(RuleProgramBuilder builder)
        {
            int left = builder.InputRegister(this, "m_left", m_left);
            int right = builder.InputRegister(this, "m_right", m_right);

            RuleOp op = m_operation switch
            {
                RuleMathOperation.Add => RuleOp.Add,
                RuleMathOperation.Subtract => RuleOp.Subtract,
                RuleMathOperation.Divide => RuleOp.Divide,
                RuleMathOperation.Minimum => RuleOp.Minimum,
                RuleMathOperation.Maximum => RuleOp.Maximum,
                _ => RuleOp.Multiply
            };

            return builder.Emit(op, left, right);
        }
    }

    /// <summary>Keeps a number inside a range.</summary>
    [Serializable]
    [GraphNodeMenu("Maths/Keep Between")]
    public sealed class ClampNode : ResidencyRuleNode
    {
        [SerializeField] [GraphInput] private float m_value;
        [SerializeField] [GraphInput("Lowest")] private float m_lowest = 10f;
        [SerializeField] [GraphInput("Highest")] private float m_highest = 500f;
        [SerializeField] [GraphOutput("Result")] private float m_result;

        /// <inheritdoc />
        public override string Title => "Keep Between";

        /// <inheritdoc />
        public override string Summary => "Stops a number from going below or above the bounds you set.";

        /// <inheritdoc />
        public override void Process() => m_result = Mathf.Clamp(m_value, m_lowest, m_highest);

        internal override int Emit(RuleProgramBuilder builder)
        {
            int value = builder.InputRegister(this, "m_value", m_value);
            int lowest = builder.InputRegister(this, "m_lowest", m_lowest);
            int highest = builder.InputRegister(this, "m_highest", m_highest);

            return builder.Emit(RuleOp.Clamp, value, lowest, highest);
        }
    }

    /// <summary>How a Compare node compares its inputs.</summary>
    public enum RuleComparison
    {
        /// <summary>True when the first input is larger.</summary>
        GreaterThan = 0,

        /// <summary>True when the first input is smaller.</summary>
        LessThan = 1
    }

    /// <summary>Answers a yes or no question about two numbers.</summary>
    [Serializable]
    [GraphNodeMenu("Logic/Compare")]
    public sealed class CompareNode : ResidencyRuleNode
    {
        [SerializeField] private RuleComparison m_comparison = RuleComparison.GreaterThan;
        [SerializeField] [GraphInput] private float m_left;
        [SerializeField] [GraphInput] private float m_right;
        [SerializeField] [GraphOutput("Yes")] private float m_yes;

        /// <inheritdoc />
        public override string Title => m_comparison == RuleComparison.GreaterThan ? "Is Greater" : "Is Less";

        /// <inheritdoc />
        public override string Summary => "Outputs 1 when the comparison holds, 0 when it does not.";

        /// <summary>Which way the comparison runs.</summary>
        public RuleComparison Comparison
        {
            get => m_comparison;
            set => m_comparison = value;
        }

        /// <inheritdoc />
        public override void Process()
        {
            bool holds = m_comparison == RuleComparison.GreaterThan ? m_left > m_right : m_left < m_right;
            m_yes = holds ? 1f : 0f;
        }

        internal override int Emit(RuleProgramBuilder builder)
        {
            int left = builder.InputRegister(this, "m_left", m_left);
            int right = builder.InputRegister(this, "m_right", m_right);
            RuleOp op = m_comparison == RuleComparison.GreaterThan ? RuleOp.Greater : RuleOp.Less;

            return builder.Emit(op, left, right);
        }
    }

    /// <summary>Picks one of two numbers depending on a yes or no input.</summary>
    [Serializable]
    [GraphNodeMenu("Logic/Choose")]
    public sealed class ChooseNode : ResidencyRuleNode
    {
        [SerializeField] [GraphInput("When yes")] private float m_condition;
        [SerializeField] [GraphInput("Then")] private float m_then = 1f;
        [SerializeField] [GraphInput("Otherwise")] private float m_otherwise;
        [SerializeField] [GraphOutput("Result")] private float m_result;

        /// <inheritdoc />
        public override string Title => "Choose";

        /// <inheritdoc />
        public override string Summary => "Takes the first number when the condition holds, the second when it does not.";

        /// <inheritdoc />
        public override void Process() => m_result = m_condition > 0.5f ? m_then : m_otherwise;

        internal override int Emit(RuleProgramBuilder builder)
        {
            int condition = builder.InputRegister(this, "m_condition", m_condition);
            int then = builder.InputRegister(this, "m_then", m_then);
            int otherwise = builder.InputRegister(this, "m_otherwise", m_otherwise);

            return builder.Emit(RuleOp.Select, condition, then, otherwise);
        }
    }

    /// <summary>Where a residency graph ends. Exactly one per graph.</summary>
    [Serializable]
    [GraphNodeMenu("Output/Residency Output")]
    public sealed class ResidencyOutputNode : VicinityNode
    {
        [SerializeField] [GraphInput("Loads at")] private float m_loadDistance = ResidencySettings.DefaultLoadDistance;
        [SerializeField] [GraphInput("Releases at")] private float m_releaseDistance = ResidencySettings.DefaultUnloadDistance;
        [SerializeField] [GraphInput("Priority scale")] private float m_priorityScale = 1f;

        /// <inheritdoc />
        public override string Title => "Residency Output";

        /// <inheritdoc />
        public override string Summary => "The distances Vicinity will use for this object. Every graph needs exactly one.";

        /// <summary>The loading distance this node resolved to, in meters.</summary>
        public float LoadDistance => m_loadDistance;

        /// <summary>The releasing distance this node resolved to, in meters.</summary>
        public float ReleaseDistance => m_releaseDistance;

        /// <summary>The priority multiplier this node resolved to.</summary>
        public float PriorityScale => m_priorityScale;

        /// <inheritdoc />
        public override void Process()
        {
        }
    }
}
