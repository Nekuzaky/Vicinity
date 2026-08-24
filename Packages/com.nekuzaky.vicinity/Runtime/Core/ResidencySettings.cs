using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>Tuning shared by every object a manager drives.</summary>
    [System.Serializable]
    public struct ResidencySettings
    {
        /// <summary>Default distance at which an object starts loading, in meters.</summary>
        public const float DefaultLoadDistance = 60f;

        /// <summary>Default distance at which an object is released, in meters. Always above the load distance.</summary>
        public const float DefaultUnloadDistance = 85f;

        /// <summary>Default width of one grid cell, in meters.</summary>
        public const float DefaultCellSize = 32f;

        /// <summary>Default delay between two evaluations, in seconds.</summary>
        public const float DefaultEvaluationInterval = 0.1f;

        /// <summary>Default number of objects allowed to load at the same time.</summary>
        public const int DefaultMaxConcurrentLoads = 6;

        /// <summary>Default look-ahead applied to the target's movement, in seconds.</summary>
        public const float DefaultPredictionHorizon = 1f;

        /// <summary>Default penalty applied to objects outside the view, which load last.</summary>
        public const float DefaultHiddenPriorityScale = 2.5f;

        /// <summary>Default number of load attempts before an object is abandoned.</summary>
        public const int DefaultMaxLoadAttempts = 3;

        /// <summary>Default milliseconds Unity may spend per frame finishing instantiations.</summary>
        public const float DefaultIntegrationTimeMs = 2f;

        /// <summary>Default distance the viewpoint must travel before Vicinity looks again, in meters.</summary>
        public const float DefaultMovementDeadZone = 1f;

        /// <summary>Default number of released instances kept aside for reuse.</summary>
        public const int DefaultPoolCapacity = 16;

        [SerializeField] private float m_cellSize;
        [SerializeField] private float m_evaluationInterval;
        [SerializeField] private int m_maxConcurrentLoads;
        [SerializeField] private float m_predictionHorizon;
        [SerializeField] private float m_hiddenPriorityScale;
        [SerializeField] private int m_maxLoadAttempts;
        [SerializeField] private float m_integrationTimeMs;
        [SerializeField] private float m_movementDeadZone;
        [SerializeField] private int m_poolCapacity;
        [SerializeField] private long m_memoryBudgetBytes;

        /// <summary>Width of one grid cell, in meters.</summary>
        public float CellSize
        {
            readonly get => m_cellSize;
            set => m_cellSize = Mathf.Max(1f, value);
        }

        /// <summary>Delay between two evaluations, in seconds.</summary>
        public float EvaluationInterval
        {
            readonly get => m_evaluationInterval;
            set => m_evaluationInterval = Mathf.Max(0f, value);
        }

        /// <summary>How many objects may load at the same time.</summary>
        public int MaxConcurrentLoads
        {
            readonly get => m_maxConcurrentLoads;
            set => m_maxConcurrentLoads = Mathf.Max(1, value);
        }

        /// <summary>How far ahead the target's movement is projected, in seconds.</summary>
        public float PredictionHorizon
        {
            readonly get => m_predictionHorizon;
            set => m_predictionHorizon = Mathf.Max(0f, value);
        }

        /// <summary>Priority penalty applied to objects the camera cannot see.</summary>
        public float HiddenPriorityScale
        {
            readonly get => m_hiddenPriorityScale;
            set => m_hiddenPriorityScale = Mathf.Max(1f, value);
        }

        /// <summary>How many times an object is retried before Vicinity gives up on it.</summary>
        public int MaxLoadAttempts
        {
            readonly get => m_maxLoadAttempts;
            set => m_maxLoadAttempts = Mathf.Max(1, value);
        }

        /// <summary>Milliseconds Unity may spend per frame finishing instantiations.</summary>
        public float IntegrationTimeMs
        {
            readonly get => m_integrationTimeMs;
            set => m_integrationTimeMs = Mathf.Max(0.1f, value);
        }

        /// <summary>How far the viewpoint must travel before Vicinity evaluates again, in meters.</summary>
        public float MovementDeadZone
        {
            readonly get => m_movementDeadZone;
            set => m_movementDeadZone = Mathf.Max(0f, value);
        }

        /// <summary>How many released instances are kept aside for reuse instead of being destroyed.</summary>
        public int PoolCapacity
        {
            readonly get => m_poolCapacity;
            set => m_poolCapacity = Mathf.Max(0, value);
        }

        /// <summary>Memory ceiling for loaded objects, in bytes. 0 lets memory grow freely.</summary>
        public long MemoryBudgetBytes
        {
            readonly get => m_memoryBudgetBytes;
            set => m_memoryBudgetBytes = value < 0L ? 0L : value;
        }

        /// <summary>Settings that behave correctly without any tuning.</summary>
        public static ResidencySettings Default => new ResidencySettings
        {
            m_cellSize = DefaultCellSize,
            m_evaluationInterval = DefaultEvaluationInterval,
            m_maxConcurrentLoads = DefaultMaxConcurrentLoads,
            m_predictionHorizon = DefaultPredictionHorizon,
            m_hiddenPriorityScale = DefaultHiddenPriorityScale,
            m_maxLoadAttempts = DefaultMaxLoadAttempts,
            m_integrationTimeMs = DefaultIntegrationTimeMs,
            m_movementDeadZone = DefaultMovementDeadZone,
            m_poolCapacity = DefaultPoolCapacity,
            m_memoryBudgetBytes = 0L
        };
    }
}
