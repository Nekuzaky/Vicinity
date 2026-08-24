using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// Marks this object as managed by Vicinity. What sits in the scene stays as the lightweight stand-in;
    /// the models listed below are loaded as the player comes closer, and released when they leave.
    /// </summary>
    [AddComponentMenu("Vicinity/Vicinity Object")]
    [DisallowMultipleComponent]
    public sealed class VicinityObject : MonoBehaviour
    {
        #region Exposed

        [SerializeField]
        [HideInInspector]
        private int m_serializedVersion = CurrentSerializedVersion;

        [SerializeField]
        [HideInInspector]
        private AssetKey m_detailedModel;

        [SerializeField]
        [Tooltip("Quality steps, closest first. One step is the usual case. Add more to load a lighter model at a distance and a heavier one up close.")]
        private DetailLevel[] m_detailLevels = new DetailLevel[0];

        [SerializeField]
        [Tooltip("Use distances of your own instead of the ones from the manager or the volume covering this object. Ignored once you add a second quality step, because the steps then carry the distances.")]
        private bool m_overrideDistances;

        [SerializeField]
        [Tooltip("How close the player must be, in meters, before this object starts loading.")]
        [Min(0f)]
        private float m_loadDistance = ResidencySettings.DefaultLoadDistance;

        [SerializeField]
        [Tooltip("How far the player must walk away, in meters, before this object is released.")]
        [Min(0f)]
        private float m_unloadDistance = ResidencySettings.DefaultUnloadDistance;

        [SerializeField]
        [Tooltip("Tick this if the object moves during play, for example on a platform or a vehicle. Moving objects are re-checked every time, which costs more, so leave it off for scenery that never moves.")]
        private bool m_movesAtRuntime;

        [SerializeField]
        [Tooltip("Roughly how much memory the models take. Measured by the dashboard when you scan the scene, and only used for reporting.")]
        [Min(0L)]
        private long m_estimatedMemoryBytes;

        [SerializeField]
        [HideInInspector]
        [Min(0f)]
        private float m_authoredRadius;

        #endregion

        #region Unity API

        private void OnEnable()
        {
            MigrateIfNeeded();
            CaptureStandInRenderers();
            EnsureInstanceSlots();
            VicinityManager.ActiveManager?.Attach(this);
        }

        private void OnDisable()
        {
            _owner?.Detach(this);
            RestoreStandIn();
        }

        #endregion

        #region Main Methods

        /// <summary>How many quality steps this object has. Always at least one once configured.</summary>
        public int LevelCount => m_detailLevels == null ? 0 : m_detailLevels.Length;

        /// <summary>True when this object loads a lighter model at a distance and a heavier one up close.</summary>
        public bool HasSeveralLevels => LevelCount > 1;

        /// <summary>The most detailed model this object loads.</summary>
        public AssetKey DetailedModel => LevelCount > 0 ? m_detailLevels[0].Model : default;

        /// <summary>Returns one quality step, counted from the most detailed.</summary>
        public DetailLevel GetLevel(int level)
        {
            return level >= 0 && level < LevelCount ? m_detailLevels[level] : default;
        }

        /// <summary>True when this object ignores the distances of its manager and volume.</summary>
        public bool OverridesDistances => m_overrideDistances && !HasSeveralLevels;

        /// <summary>The distance at which this object starts loading, in meters.</summary>
        public float LoadDistance => m_loadDistance;

        /// <summary>The distance at which this object is released, in meters.</summary>
        public float UnloadDistance => Mathf.Max(m_unloadDistance, m_loadDistance + MinimumMargin);

        /// <summary>True when this object moves during play and must be re-checked at its new position.</summary>
        public bool MovesAtRuntime => m_movesAtRuntime;

        /// <summary>Roughly how much memory the models take, in bytes.</summary>
        public long EstimatedMemoryBytes => m_estimatedMemoryBytes;

        /// <summary>
        /// Where this object stands between "not in memory" and "fully loaded". With several steps it
        /// reports the furthest along any step has reached.
        /// </summary>
        public ResidencyState State
        {
            get
            {
                if (_owner == null || EntryCount <= 0)
                {
                    return ResidencyState.Unloaded;
                }

                ResidencyState best = ResidencyState.Unloaded;

                for (int level = 0; level < EntryCount; level++)
                {
                    ResidencyState candidate = _owner.GetState(FirstEntryIndex + level);
                    if (Rank(candidate) > Rank(best))
                    {
                        best = candidate;
                    }
                }

                return best;
            }
        }

        /// <summary>True when a releasing distance leaves no margin above its loading distance.</summary>
        public bool HasInvalidMargin => OverridesDistances && m_unloadDistance <= m_loadDistance;

        /// <summary>True when a quality step names no model.</summary>
        public bool HasMissingModel
        {
            get
            {
                if (LevelCount == 0)
                {
                    return true;
                }

                for (int level = 0; level < m_detailLevels.Length; level++)
                {
                    if (!m_detailLevels[level].Model.IsValid)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>True when the quality steps are not ordered from closest to furthest.</summary>
        public bool HasUnorderedLevels
        {
            get
            {
                for (int level = 1; level < LevelCount; level++)
                {
                    if (m_detailLevels[level].Range <= m_detailLevels[level - 1].Range)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Distance from this object to the furthest corner of what it draws, in meters.</summary>
        public float BoundsRadius
        {
            get
            {
                CaptureStandInRenderers();
                return Mathf.Max(_boundsRadius, m_authoredRadius);
            }
        }

        /// <summary>Replaces every quality step with a single model. Meant for the dashboard.</summary>
        public void SetDetailedModel(AssetKey model)
        {
            m_detailedModel = model;
            m_detailLevels = new[] { DetailLevel.Create(model, m_loadDistance) };
            EnsureInstanceSlots();
        }

        /// <summary>Replaces the quality steps, closest first. Meant for the dashboard and for editor tooling.</summary>
        public void SetDetailLevels(DetailLevel[] levels)
        {
            m_detailLevels = levels ?? new DetailLevel[0];
            m_detailedModel = LevelCount > 0 ? m_detailLevels[0].Model : default;
            EnsureInstanceSlots();
        }

        /// <summary>Records the measured size of the models. Meant for the dashboard.</summary>
        public void SetEstimatedMemoryBytes(long bytes)
        {
            m_estimatedMemoryBytes = bytes < 0L ? 0L : bytes;
        }

        /// <summary>
        /// Records how big the models are, in meters from here to their furthest corner. An object that keeps
        /// no stand-in draws nothing of its own, so without this it would measure as a point and any rule that
        /// asks about size would misjudge it.
        /// </summary>
        public void SetAuthoredRadius(float radius)
        {
            m_authoredRadius = radius < 0f ? 0f : radius;
        }

        /// <summary>
        /// Gives this object distances of its own instead of the ones from the manager or a volume. The
        /// releasing distance is pushed out if needed, so an object set up this way can never flicker.
        /// Meant for tooling.
        /// </summary>
        public void SetOwnDistances(float loadDistance, float releaseDistance)
        {
            m_overrideDistances = true;
            m_loadDistance = Mathf.Max(loadDistance, 0f);
            m_unloadDistance = Mathf.Max(releaseDistance, m_loadDistance + MinimumMargin);
        }

        /// <summary>Rewrites values saved by an older version of Vicinity. Safe to call repeatedly.</summary>
        public void MigrateIfNeeded()
        {
            if (m_serializedVersion >= CurrentSerializedVersion)
            {
                return;
            }

            if (LevelCount == 0 && m_detailedModel.IsValid)
            {
                m_detailLevels = new[] { DetailLevel.Create(m_detailedModel, m_loadDistance) };
            }

            m_serializedVersion = CurrentSerializedVersion;
        }

        internal int FirstEntryIndex { get; set; } = -1;

        internal int EntryCount { get; set; }

        internal void BindOwner(VicinityManager owner)
        {
            _owner = owner;
        }

        internal void ShowLoadedModel(int level, GameObject instance)
        {
            EnsureInstanceSlots();

            if (instance == null || level < 0 || level >= _instances.Length)
            {
                return;
            }

            AlignToStandIn(instance);
            ApplyBakedLighting(instance);

            _instances[level] = instance;
            _instanceProvidesCollision[level] = instance.GetComponentInChildren<Collider>(true) != null;
            RefreshVisibleLevel();
        }

        internal void HideLoadedModel(int level)
        {
            if (_instances == null || level < 0 || level >= _instances.Length)
            {
                return;
            }

            _instances[level] = null;
            RefreshVisibleLevel();
        }

        #endregion

        #region Privates

        private const int CurrentSerializedVersion = 2;
        private const float MinimumMargin = 1f;
        private const int UnlitLightmapIndex = 65534;

        private Renderer[] _standInRenderers;
        private Collider[] _standInColliders;
        private bool[] _instanceProvidesCollision = new bool[0];
        private GameObject[] _instances = new GameObject[0];
        private VicinityManager _owner;
        private float _boundsRadius;
        private bool _standInCaptured;

        private static int Rank(ResidencyState state)
        {
            return state switch
            {
                ResidencyState.Resident => 5,
                ResidencyState.Loading => 4,
                ResidencyState.Queued => 3,
                ResidencyState.Unloading => 2,
                ResidencyState.Failed => 1,
                _ => 0
            };
        }

        private void EnsureInstanceSlots()
        {
            int required = Mathf.Max(LevelCount, 1);

            if (_instances != null && _instances.Length == required)
            {
                return;
            }

            _instances = new GameObject[required];
            _instanceProvidesCollision = new bool[required];
        }

        private void AlignToStandIn(GameObject instance)
        {
            Transform instanceTransform = instance.transform;
            Vector3 authoredScale = instanceTransform.localScale;

            instanceTransform.SetParent(transform, false);
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = authoredScale;
        }

        private void ApplyBakedLighting(GameObject instance)
        {
            if (_standInRenderers == null || _standInRenderers.Length == 0)
            {
                return;
            }

            Renderer source = _standInRenderers[0];
            if (source == null || source.lightmapIndex < 0 || source.lightmapIndex >= UnlitLightmapIndex)
            {
                return;
            }

            Renderer[] loaded = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] == null)
                {
                    continue;
                }

                loaded[i].lightmapIndex = source.lightmapIndex;
                loaded[i].lightmapScaleOffset = source.lightmapScaleOffset;
            }
        }

        private void SetStandInCollidersEnabled(bool value)
        {
            if (_standInColliders == null)
            {
                return;
            }

            for (int i = 0; i < _standInColliders.Length; i++)
            {
                if (_standInColliders[i] != null)
                {
                    _standInColliders[i].enabled = value;
                }
            }
        }

        private void RefreshVisibleLevel()
        {
            int finestLoaded = -1;

            for (int level = 0; level < _instances.Length; level++)
            {
                if (_instances[level] != null)
                {
                    finestLoaded = level;
                    break;
                }
            }

            for (int level = 0; level < _instances.Length; level++)
            {
                if (_instances[level] != null)
                {
                    _instances[level].SetActive(level == finestLoaded);
                }
            }

            SetStandInVisible(finestLoaded < 0);

            bool loadedProvidesCollision = finestLoaded >= 0 && _instanceProvidesCollision[finestLoaded];
            SetStandInCollidersEnabled(!loadedProvidesCollision);
        }

        private void CaptureStandInRenderers()
        {
            if (_standInCaptured)
            {
                return;
            }

            _standInCaptured = true;
            _standInRenderers = GetComponentsInChildren<Renderer>(true);
            _standInColliders = GetComponentsInChildren<Collider>(true);
            _boundsRadius = ComputeBoundsRadius();
        }

        private float ComputeBoundsRadius()
        {
            if (_standInRenderers == null || _standInRenderers.Length == 0)
            {
                return 0f;
            }

            Vector3 origin = transform.position;
            float radius = 0f;

            for (int i = 0; i < _standInRenderers.Length; i++)
            {
                Renderer renderer = _standInRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                float reach = Vector3.Distance(origin, bounds.center) + bounds.extents.magnitude;
                radius = Mathf.Max(radius, reach);
            }

            return radius;
        }

        private void SetStandInVisible(bool visible)
        {
            if (_standInRenderers == null)
            {
                return;
            }

            for (int i = 0; i < _standInRenderers.Length; i++)
            {
                if (_standInRenderers[i] != null)
                {
                    _standInRenderers[i].enabled = visible;
                }
            }
        }

        private void RestoreStandIn()
        {
            if (_instances != null)
            {
                for (int level = 0; level < _instances.Length; level++)
                {
                    _instances[level] = null;
                }
            }

            SetStandInVisible(true);
            SetStandInCollidersEnabled(true);
            _owner = null;
            FirstEntryIndex = -1;
            EntryCount = 0;
        }

        #endregion
    }
}
