using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// Drives every managed object in the scene. One is enough, and the dashboard creates it for you.
    /// There is normally nothing to configure here beyond picking a profile.
    /// </summary>
    [AddComponentMenu("Vicinity/Vicinity Manager")]
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class VicinityManager : MonoBehaviour, IResidencyHost
    {
        #region Exposed

        [SerializeField]
        [HideInInspector]
        private int m_serializedVersion = CurrentSerializedVersion;

        [SerializeField]
        [Tooltip("Distances and budgets used by objects that are not covered by a volume. Leave empty to use sensible defaults.")]
        private VicinityProfile m_profile;

        #endregion

        #region Unity API

        private void Awake()
        {
            if (_activeManager != null && _activeManager != this)
            {
                Debug.LogWarning($"Vicinity found a second manager on '{name}'. Only '{_activeManager.name}' stays active.", this);
                enabled = false;
                return;
            }

            _activeManager = this;
            _settings = m_profile != null ? m_profile.ToSettings() : ResidencySettings.Default;
            _providers = AssetProviderRegistry.CreateDefault();
            _controller = new ResidencyController(_providers, this, _settings);

            AsyncInstantiateOperation.SetIntegrationTimeMS(_settings.IntegrationTimeMs);
        }

        private void OnEnable()
        {
            AttachObjectsAlreadyInScene();
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            VicinityTarget target = VicinityTargetRegistry.Best();

            if (target != null)
            {
                target.SampleMovement(deltaTime);
                _settings.PredictionHorizon = target.LookAheadSeconds;
                _controller.Settings = _settings;
            }

            RefreshMobilePositions();
            _controller.Tick(deltaTime, BuildViewState(target));
        }

        private void OnDestroy()
        {
            _controller?.Dispose();
            _controller = null;

            if (_activeManager == this)
            {
                _activeManager = null;
            }
        }

        #endregion

        #region Main Methods

        /// <summary>The manager currently driving the scene, or null when there is none.</summary>
        public static VicinityManager ActiveManager => _activeManager;

        /// <summary>Distances and budgets used by objects that no volume covers.</summary>
        public VicinityProfile Profile => m_profile;

        /// <summary>What Vicinity is currently holding in memory.</summary>
        public ResidencyStatistics Statistics => _controller?.Statistics ?? default;

        /// <summary>Assigns the profile used by objects that no volume covers.</summary>
        public void SetProfile(VicinityProfile profile)
        {
            m_profile = profile;
        }

        /// <summary>Where one quality step stands between "not in memory" and "fully loaded".</summary>
        public ResidencyState GetState(int entryIndex)
        {
            return _controller?.GetState(entryIndex) ?? ResidencyState.Unloaded;
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

        internal void Attach(VicinityObject managed)
        {
            if (_controller == null || managed == null || managed.EntryCount > 0)
            {
                return;
            }

            managed.MigrateIfNeeded();

            int levelCount = managed.LevelCount;
            if (levelCount == 0)
            {
                return;
            }

            int firstEntryIndex = -1;

            for (int level = 0; level < levelCount; level++)
            {
                int entryIndex = _controller.Register(BuildRegistration(managed, level));

                if (level == 0)
                {
                    firstEntryIndex = entryIndex;
                }

                while (_managedObjects.Count <= entryIndex)
                {
                    _managedObjects.Add(null);
                    _entryLevels.Add(0);
                }

                _managedObjects[entryIndex] = managed;
                _entryLevels[entryIndex] = level;
            }

            managed.BindOwner(this);
            managed.FirstEntryIndex = firstEntryIndex;
            managed.EntryCount = levelCount;

            if (managed.MovesAtRuntime)
            {
                _mobileObjects.Add(managed);
            }
        }

        internal void Detach(VicinityObject managed)
        {
            if (_controller == null || managed == null || managed.EntryCount <= 0)
            {
                return;
            }

            int first = managed.FirstEntryIndex;

            for (int level = 0; level < managed.EntryCount; level++)
            {
                int entryIndex = first + level;
                _controller.Unregister(entryIndex);

                if (entryIndex < _managedObjects.Count)
                {
                    _managedObjects[entryIndex] = null;
                }
            }

            managed.FirstEntryIndex = -1;
            managed.EntryCount = 0;
            _mobileObjects.Remove(managed);
        }

        void IResidencyHost.OnResident(int entryIndex, GameObject instance)
        {
            VicinityObject managed = ResolveObject(entryIndex);

            if (managed == null)
            {
                VicinityLifetime.Destroy(instance);
                return;
            }

            managed.ShowLoadedModel(ResolveLevel(entryIndex), instance);
        }

        void IResidencyHost.OnUnloaded(int entryIndex)
        {
            ResolveObject(entryIndex)?.HideLoadedModel(ResolveLevel(entryIndex));
        }

        void IResidencyHost.OnFailed(int entryIndex, string reason)
        {
            ResolveObject(entryIndex)?.HideLoadedModel(ResolveLevel(entryIndex));
        }

        #endregion

        #region Privates

        private const int ExecutionOrder = -100;
        private const int CurrentSerializedVersion = 1;
        private const int FrustumPlaneCount = 6;
        private const float FallbackMarginRatio = 1.4f;

        private static VicinityManager _activeManager;

        private readonly List<VicinityObject> _managedObjects = new List<VicinityObject>();
        private readonly List<int> _entryLevels = new List<int>();
        private readonly List<VicinityObject> _mobileObjects = new List<VicinityObject>();
        private readonly Plane[] _frustumPlanes = new Plane[FrustumPlaneCount];

        private ResidencyController _controller;
        private AssetProviderRegistry _providers;
        private ResidencySettings _settings;
        private Camera[] _cameraBuffer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeManager = null;
            VicinityTargetRegistry.Clear();
            VicinityVolume.ClearRegistry();
        }

        private void RefreshMobilePositions()
        {
            for (int i = _mobileObjects.Count - 1; i >= 0; i--)
            {
                VicinityObject managed = _mobileObjects[i];

                if (managed == null || managed.EntryCount <= 0)
                {
                    _mobileObjects.RemoveAt(i);
                    continue;
                }

                float3 position = managed.transform.position;

                for (int level = 0; level < managed.EntryCount; level++)
                {
                    _controller.UpdatePosition(managed.FirstEntryIndex + level, position);
                }
            }
        }

        private VicinityObject ResolveObject(int entryIndex)
        {
            return entryIndex >= 0 && entryIndex < _managedObjects.Count ? _managedObjects[entryIndex] : null;
        }

        private int ResolveLevel(int entryIndex)
        {
            return entryIndex >= 0 && entryIndex < _entryLevels.Count ? _entryLevels[entryIndex] : 0;
        }

        private void AttachObjectsAlreadyInScene()
        {
            VicinityObject[] existing = FindObjectsByType<VicinityObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

            for (int i = 0; i < existing.Length; i++)
            {
                Attach(existing[i]);
            }
        }

        private EntryRegistration BuildRegistration(VicinityObject managed, int level)
        {
            ResolveDistances(managed, out float loadDistance, out float unloadDistance);

            float marginRatio = loadDistance > 0f ? unloadDistance / loadDistance : FallbackMarginRatio;
            ResolveBand(managed, level, loadDistance, out float innerRange, out float outerRange);

            return new EntryRegistration
            {
                Key = managed.GetLevel(level).Model,
                Position = managed.transform.position,
                BoundsRadius = managed.BoundsRadius,
                LoadDistance = outerRange,
                UnloadDistance = outerRange * marginRatio,
                InnerLoadDistance = innerRange,
                InnerUnloadDistance = innerRange / marginRatio,
                EstimatedBytes = managed.EstimatedMemoryBytes,
                IsMobile = managed.MovesAtRuntime
            };
        }

        private static void ResolveBand(VicinityObject managed, int level, float loadDistance, out float innerRange, out float outerRange)
        {
            if (!managed.HasSeveralLevels)
            {
                innerRange = 0f;
                outerRange = loadDistance;
                return;
            }

            outerRange = managed.GetLevel(level).Range;
            innerRange = level == 0 ? 0f : managed.GetLevel(level - 1).Range;
        }

        private void ResolveDistances(VicinityObject managed, out float loadDistance, out float unloadDistance)
        {
            if (managed.OverridesDistances)
            {
                loadDistance = managed.LoadDistance;
                unloadDistance = managed.UnloadDistance;
                return;
            }

            VicinityVolume covering = VicinityVolume.FindCovering(managed.transform.position);
            VicinityProfile profile = covering != null && covering.Profile != null ? covering.Profile : m_profile;

            loadDistance = profile != null ? profile.LoadDistance : ResidencySettings.DefaultLoadDistance;
            unloadDistance = profile != null ? profile.UnloadDistance : ResidencySettings.DefaultUnloadDistance;
        }

        private VicinityViewState BuildViewState(VicinityTarget target)
        {
            Camera camera = target != null ? target.ViewCamera : null;
            if (camera == null)
            {
                camera = FindFallbackCamera();
            }

            Vector3 position = target != null
                ? target.Position
                : camera != null ? camera.transform.position : transform.position;

            Vector3 velocity = target != null ? target.Velocity : Vector3.zero;
            bool wantsFrustum = camera != null && (target == null || target.PrioritisesWhatTheCameraSees);

            VicinityViewState view = new VicinityViewState
            {
                Position = position,
                Velocity = velocity,
                HasFrustum = wantsFrustum
            };

            if (!wantsFrustum)
            {
                return view;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            view.PlaneLeft = ToVector(_frustumPlanes[0]);
            view.PlaneRight = ToVector(_frustumPlanes[1]);
            view.PlaneDown = ToVector(_frustumPlanes[2]);
            view.PlaneUp = ToVector(_frustumPlanes[3]);
            view.PlaneNear = ToVector(_frustumPlanes[4]);
            view.PlaneFar = ToVector(_frustumPlanes[5]);

            return view;
        }

        private static float4 ToVector(Plane plane)
        {
            return new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
        }

        private Camera FindFallbackCamera()
        {
            int count = Camera.allCamerasCount;
            if (count == 0)
            {
                return null;
            }

            if (_cameraBuffer == null || _cameraBuffer.Length < count)
            {
                _cameraBuffer = new Camera[count];
            }

            Camera.GetAllCameras(_cameraBuffer);

            for (int i = 0; i < count; i++)
            {
                Camera candidate = _cameraBuffer[i];
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    return candidate;
                }
            }

            return null;
        }

        #endregion
    }
}
