using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// A reusable set of distances and budgets. Assign one to a manager or to a volume instead of
    /// retyping numbers on every object.
    /// </summary>
    [CreateAssetMenu(fileName = "Vicinity Profile", menuName = "Vicinity/Profile", order = 200)]
    public sealed class VicinityProfile : ScriptableObject
    {
        #region Exposed

        [SerializeField]
        [HideInInspector]
        private int m_serializedVersion = CurrentSerializedVersion;

        [SerializeField]
        [Tooltip("Optional. A graph that works out each object's distances from its size, its memory or its tag. Leave empty to use the two distances below for everything.")]
        private Graph.ResidencyGraphAsset m_residencyGraph;

        [SerializeField]
        [Tooltip("How close the player must be, in meters, before an object starts loading.")]
        [Min(0f)]
        private float m_loadDistance = ResidencySettings.DefaultLoadDistance;

        [SerializeField]
        [Tooltip("How far the player must walk away, in meters, before an object is released. Must be larger than the loading distance, otherwise objects load and unload endlessly on the boundary.")]
        [Min(0f)]
        private float m_unloadDistance = ResidencySettings.DefaultUnloadDistance;

        [SerializeField]
        [Tooltip("How many objects may load at the same time. Higher loads faster but can stutter on slow storage.")]
        [Range(1, 32)]
        private int m_simultaneousLoads = ResidencySettings.DefaultMaxConcurrentLoads;

        [SerializeField]
        [Tooltip("Size of one grid square, in meters. Roughly the spacing between your objects. Larger is cheaper but less precise.")]
        [Min(1f)]
        private float m_gridSize = ResidencySettings.DefaultCellSize;

        [SerializeField]
        [Tooltip("Delay between two checks, in seconds. Lower reacts faster and costs more.")]
        [Range(0f, 1f)]
        private float m_checkInterval = ResidencySettings.DefaultEvaluationInterval;

        [SerializeField]
        [Tooltip("Start loading what the player is heading towards, this many seconds early. Hides loading time on slow storage.")]
        [Range(0f, 5f)]
        private float m_lookAheadSeconds = ResidencySettings.DefaultPredictionHorizon;

        [SerializeField]
        [Tooltip("How much later objects outside the view are loaded compared to objects in front of the player.")]
        [Range(1f, 10f)]
        private float m_offScreenDelayFactor = ResidencySettings.DefaultHiddenPriorityScale;

        [SerializeField]
        [Tooltip("How many times a broken object is retried before Vicinity gives up and stops reporting it.")]
        [Range(1, 10)]
        private int m_retriesBeforeGivingUp = ResidencySettings.DefaultMaxLoadAttempts;

        [SerializeField]
        [Tooltip("Milliseconds per frame Unity may spend finishing loaded objects. Raising this loads faster but costs frame time.")]
        [Range(0.1f, 16f)]
        private float m_frameBudgetMilliseconds = ResidencySettings.DefaultIntegrationTimeMs;

        [SerializeField]
        [Tooltip("How far the player must move before Vicinity looks again, in meters. Raising it saves CPU when the player stands still; 0 checks every interval.")]
        [Range(0f, 10f)]
        private float m_minimumMovement = ResidencySettings.DefaultMovementDeadZone;

        [SerializeField]
        [Tooltip("How many released models are kept aside for reuse instead of being destroyed. Reusing avoids reloading when the player walks back, at the cost of keeping them in memory. 0 disables it.")]
        [Range(0, 128)]
        private int m_reusePoolSize = ResidencySettings.DefaultPoolCapacity;

        [SerializeField]
        [Tooltip("Memory ceiling for loaded models, in megabytes. When reached, Vicinity releases the objects furthest from the player until it is back under. 0 lets memory grow freely.")]
        [Min(0f)]
        private float m_memoryBudgetMegabytes = DefaultMemoryBudgetMegabytes;

        #endregion

        #region Unity API

        private void OnValidate()
        {
            m_unloadDistance = Mathf.Max(m_unloadDistance, m_loadDistance + MinimumMargin);
        }

        #endregion

        #region Main Methods

        /// <summary>The graph that works out each object's distances, or null when there is none.</summary>
        public Graph.ResidencyGraphAsset ResidencyGraph => m_residencyGraph;

        /// <summary>How close the player must be before an object starts loading, in meters.</summary>
        public float LoadDistance => m_loadDistance;

        /// <summary>How far the player must walk away before an object is released, in meters.</summary>
        public float UnloadDistance => Mathf.Max(m_unloadDistance, m_loadDistance + MinimumMargin);

        /// <summary>Memory loaded objects are expected to stay under, in megabytes.</summary>
        public float MemoryBudgetMegabytes => m_memoryBudgetMegabytes;

        /// <summary>True when the unloading distance leaves no margin above the loading distance.</summary>
        public bool HasInvalidMargin => m_unloadDistance <= m_loadDistance;

        /// <summary>Turns this profile into the values the streaming engine consumes.</summary>
        public ResidencySettings ToSettings()
        {
            ResidencySettings settings = ResidencySettings.Default;
            settings.CellSize = m_gridSize;
            settings.EvaluationInterval = m_checkInterval;
            settings.MaxConcurrentLoads = m_simultaneousLoads;
            settings.PredictionHorizon = m_lookAheadSeconds;
            settings.HiddenPriorityScale = m_offScreenDelayFactor;
            settings.MaxLoadAttempts = m_retriesBeforeGivingUp;
            settings.IntegrationTimeMs = m_frameBudgetMilliseconds;
            settings.MovementDeadZone = m_minimumMovement;
            settings.PoolCapacity = m_reusePoolSize;
            settings.MemoryBudgetBytes = (long)(m_memoryBudgetMegabytes * 1024f * 1024f);
            return settings;
        }

        /// <summary>Rewrites values saved by an older version of Vicinity. Safe to call repeatedly.</summary>
        public void MigrateIfNeeded()
        {
            if (m_serializedVersion == CurrentSerializedVersion)
            {
                return;
            }

            m_serializedVersion = CurrentSerializedVersion;
        }

        #endregion

        #region Privates

        private const int CurrentSerializedVersion = 1;
        private const float MinimumMargin = 1f;
        private const float DefaultMemoryBudgetMegabytes = 512f;

        #endregion
    }
}
