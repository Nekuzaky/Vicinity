using System;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    internal struct EntryRegistration
    {
        public AssetKey Key;
        public float3 Position;
        public float BoundsRadius;
        public float LoadDistance;
        public float UnloadDistance;
        public float InnerLoadDistance;
        public float InnerUnloadDistance;
        public long EstimatedBytes;
        public float PriorityScale;
        public bool IsMobile;
    }

    internal interface IResidencyHost
    {
        void OnResident(int entryIndex, GameObject instance);

        void OnUnloaded(int entryIndex);

        void OnFailed(int entryIndex, string reason);
    }

    internal sealed class ResidencyController : IDisposable
    {
        #region Main Methods

        internal ResidencyController(AssetProviderRegistry providers, IResidencyHost host, ResidencySettings settings)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _host = host;
            _settings = settings;
            _pool = new InstancePool(_providers, settings.PoolCapacity);

            _grid = new VicinityGrid();
            _entries = new NativeList<VicinityEntryData>(InitialCapacity, Allocator.Persistent);
            _states = new NativeList<byte>(InitialCapacity, Allocator.Persistent);
            _loadCandidates = new NativeList<PendingLoad>(InitialCapacity, Allocator.Persistent);
            _unloadCandidates = new NativeList<int>(InitialCapacity, Allocator.Persistent);
            _residentEntries = new NativeList<PendingLoad>(InitialCapacity, Allocator.Persistent);
            _mobileEntries = new NativeList<int>(InitialCapacity, Allocator.Persistent);
            _freeEntries = new NativeList<int>(InitialCapacity, Allocator.Persistent);
            _pendingPositions = new NativeList<PositionUpdate>(InitialCapacity, Allocator.Persistent);
            _relevance = new NativeArray<byte>(0, Allocator.Persistent);

            _keys = new AssetKey[InitialCapacity];
            _instances = new GameObject[InitialCapacity];
            _cancellations = new CancellationTokenSource[InitialCapacity];
            _releaseWhenLoaded = new bool[InitialCapacity];
            _recycleWhenLoaded = new bool[InitialCapacity];
            _attempts = new int[InitialCapacity];
            _failureLogged = new bool[InitialCapacity];
            _estimatedBytes = new long[InitialCapacity];
        }

        internal ResidencySettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                _pool.Capacity = value.PoolCapacity;
            }
        }

        internal int EntryCount => _entries.Length;

        internal ResidencyStatistics Statistics => new ResidencyStatistics
        {
            Managed = _activeCount,
            Unloaded = _stateCounts[(int)ResidencyState.Unloaded],
            Queued = _stateCounts[(int)ResidencyState.Queued],
            Loading = _stateCounts[(int)ResidencyState.Loading],
            Resident = _stateCounts[(int)ResidencyState.Resident],
            Failed = _stateCounts[(int)ResidencyState.Failed],
            ResidentMemoryBytes = _residentMemoryBytes,
            Pooled = _pool.ParkedCount,
            Evicted = _evictedCount
        };

        internal int Register(in EntryRegistration registration)
        {
            HarvestScheduledJobs();

            float loadDistance = math.max(registration.LoadDistance, 0f);
            float unloadDistance = math.max(registration.UnloadDistance, loadDistance + MinimumHysteresis);
            float innerLoadDistance = math.clamp(registration.InnerLoadDistance, 0f, loadDistance);
            float innerUnloadDistance = math.clamp(registration.InnerUnloadDistance, 0f, innerLoadDistance);

            VicinityEntryData data = new VicinityEntryData
            {
                Position = registration.Position,
                BoundsRadius = math.max(registration.BoundsRadius, 0f),
                LoadDistanceSquared = loadDistance * loadDistance,
                UnloadDistanceSquared = unloadDistance * unloadDistance,
                InnerLoadDistanceSquared = innerLoadDistance * innerLoadDistance,
                InnerUnloadDistanceSquared = innerUnloadDistance * innerUnloadDistance,
                CellIndex = -1,
                PriorityMultiplier = ComputePriorityMultiplier(registration.EstimatedBytes, registration.PriorityScale),
                IsActive = 1,
                IsMobile = registration.IsMobile ? (byte)1 : (byte)0
            };

            int entryIndex = TakeSlot(data);

            _keys[entryIndex] = registration.Key;
            _instances[entryIndex] = null;
            _cancellations[entryIndex] = null;
            _releaseWhenLoaded[entryIndex] = false;
            _recycleWhenLoaded[entryIndex] = false;
            _attempts[entryIndex] = 0;
            _failureLogged[entryIndex] = false;
            _estimatedBytes[entryIndex] = registration.EstimatedBytes;

            if (registration.IsMobile)
            {
                _mobileEntries.Add(entryIndex);
            }

            _maxUnloadDistance = math.max(_maxUnloadDistance, unloadDistance);
            _activeCount++;
            _gridDirty = true;

            return entryIndex;
        }

        internal void Unregister(int entryIndex)
        {
            if (!IsValidIndex(entryIndex) || _entries[entryIndex].IsActive == 0)
            {
                return;
            }

            HarvestScheduledJobs();

            ReleaseOrCancel(entryIndex);
            Deactivate(entryIndex);
            RemoveFromMobileList(entryIndex);

            if ((ResidencyState)_states[entryIndex] == ResidencyState.Loading)
            {
                _recycleWhenLoaded[entryIndex] = true;
                return;
            }

            RecycleSlot(entryIndex);
        }

        internal void UpdatePosition(int entryIndex, float3 position)
        {
            if (!IsValidIndex(entryIndex))
            {
                return;
            }

            _pendingPositions.Add(new PositionUpdate
            {
                EntryIndex = entryIndex,
                Position = position
            });
        }

        internal ResidencyState GetState(int entryIndex)
        {
            return IsValidIndex(entryIndex) ? (ResidencyState)_states[entryIndex] : ResidencyState.Unloaded;
        }

        internal void Tick(float deltaTime, in VicinityViewState view)
        {
            if (_disposed)
            {
                return;
            }

            HarvestScheduledJobs();
            ApplyPendingPositions();

            _timeSinceEvaluation += math.max(deltaTime, 0f);
            bool dueForEvaluation = _timeSinceEvaluation >= _settings.EvaluationInterval;
            if (!dueForEvaluation && !_gridDirty)
            {
                return;
            }

            _timeSinceEvaluation = 0f;

            float3 evaluationPosition = view.Position + view.Velocity * _settings.PredictionHorizon;
            if (!IsWorthEvaluating(evaluationPosition))
            {
                return;
            }

            _lastEvaluationPosition = evaluationPosition;
            _hasEvaluatedOnce = true;

            if (_gridDirty)
            {
                RebuildGrid();
            }

            if (_entries.Length > 0)
            {
                ScheduleEvaluation(view, evaluationPosition);
            }

            PublishCounters();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_jobsScheduled)
            {
                _evaluationHandle.Complete();
                _jobsScheduled = false;
            }

            _disposed = true;

            for (int i = 0; i < _entries.Length; i++)
            {
                _cancellations[i]?.Cancel();

                if (_instances[i] != null)
                {
                    _providers.Release(_keys[i], _instances[i]);
                    _instances[i] = null;
                }
            }

            _pool.Clear();
            _grid.Dispose();

            DisposeIfCreated();
        }

        #endregion

        #region Privates

        private const int InitialCapacity = 64;
        private const int RelevanceBatchSize = 32;
        private const int EvaluationBatchSize = 8;
        private const int MobileBatchSize = 16;
        private const float MinimumHysteresis = 0.5f;
        private const float CostReferenceBytes = 8f * 1024f * 1024f;
        private const float MaximumRelativeCost = 4f;

        private readonly AssetProviderRegistry _providers;
        private readonly IResidencyHost _host;
        private readonly VicinityGrid _grid;
        private readonly InstancePool _pool;
        private readonly int[] _stateCounts = new int[6];

        private ResidencySettings _settings;
        private NativeList<VicinityEntryData> _entries;
        private NativeList<byte> _states;
        private NativeList<PendingLoad> _loadCandidates;
        private NativeList<int> _unloadCandidates;
        private NativeList<PendingLoad> _residentEntries;
        private NativeList<int> _mobileEntries;
        private NativeList<int> _freeEntries;
        private NativeList<PositionUpdate> _pendingPositions;
        private NativeArray<byte> _relevance;
        private JobHandle _evaluationHandle;

        private AssetKey[] _keys;
        private GameObject[] _instances;
        private CancellationTokenSource[] _cancellations;
        private bool[] _releaseWhenLoaded;
        private bool[] _recycleWhenLoaded;
        private int[] _attempts;
        private bool[] _failureLogged;
        private long[] _estimatedBytes;

        private float _timeSinceEvaluation;
        private float _maxUnloadDistance;
        private float3 _lastEvaluationPosition;
        private long _residentMemoryBytes;
        private int _loadsInFlight;
        private int _activeCount;
        private int _evictedCount;
        private bool _gridDirty;
        private bool _disposed;
        private bool _hasEvaluatedOnce;
        private bool _jobsScheduled;
        private bool _budgetWarningLogged;

        private static float ComputePriorityMultiplier(long estimatedBytes, float priorityScale)
        {
            float cost = estimatedBytes <= 0L
                ? 0f
                : math.min(estimatedBytes / CostReferenceBytes, MaximumRelativeCost);

            float scale = priorityScale <= 0f ? 1f : priorityScale;
            return (1f + cost) * scale;
        }

        private bool IsValidIndex(int entryIndex) => entryIndex >= 0 && entryIndex < _entries.Length;

        private int TakeSlot(in VicinityEntryData data)
        {
            if (_freeEntries.Length > 0)
            {
                int reused = _freeEntries[_freeEntries.Length - 1];
                _freeEntries.RemoveAt(_freeEntries.Length - 1);

                _stateCounts[_states[reused]]--;
                _states[reused] = (byte)ResidencyState.Unloaded;
                _stateCounts[(int)ResidencyState.Unloaded]++;
                _entries[reused] = data;

                return reused;
            }

            int appended = _entries.Length;
            EnsureManagedCapacity(appended + 1);

            _entries.Add(data);
            _states.Add((byte)ResidencyState.Unloaded);
            _stateCounts[(int)ResidencyState.Unloaded]++;

            return appended;
        }

        private void RecycleSlot(int entryIndex)
        {
            _recycleWhenLoaded[entryIndex] = false;
            _keys[entryIndex] = default;
            _instances[entryIndex] = null;
            _cancellations[entryIndex] = null;
            _releaseWhenLoaded[entryIndex] = false;
            _attempts[entryIndex] = 0;
            _failureLogged[entryIndex] = false;
            _estimatedBytes[entryIndex] = 0L;

            _freeEntries.Add(entryIndex);
            _gridDirty = true;
        }

        private void RemoveFromMobileList(int entryIndex)
        {
            for (int i = 0; i < _mobileEntries.Length; i++)
            {
                if (_mobileEntries[i] == entryIndex)
                {
                    _mobileEntries.RemoveAtSwapBack(i);
                    return;
                }
            }
        }

        private void EnsureManagedCapacity(int required)
        {
            if (_keys.Length >= required)
            {
                return;
            }

            int capacity = math.max(required, _keys.Length * 2);
            Array.Resize(ref _keys, capacity);
            Array.Resize(ref _instances, capacity);
            Array.Resize(ref _cancellations, capacity);
            Array.Resize(ref _releaseWhenLoaded, capacity);
            Array.Resize(ref _recycleWhenLoaded, capacity);
            Array.Resize(ref _attempts, capacity);
            Array.Resize(ref _failureLogged, capacity);
            Array.Resize(ref _estimatedBytes, capacity);
        }

        private void RebuildGrid()
        {
            _gridDirty = false;
            _grid.Rebuild(_entries.AsArray(), _entries.Length, _settings.CellSize);

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].IsActive != 0 && HoldsMemorySlot((ResidencyState)_states[i]))
                {
                    _grid.AddActive(_entries[i].CellIndex, 1);
                }
            }

            if (_relevance.Length != _grid.CellCount)
            {
                if (_relevance.IsCreated)
                {
                    _relevance.Dispose();
                }

                _relevance = new NativeArray<byte>(_grid.CellCount, Allocator.Persistent);
            }
        }

        private void ScheduleEvaluation(in VicinityViewState view, float3 evaluationPosition)
        {
            VicinityProfiling.EvaluateMarker.Begin();

            _loadCandidates.Clear();
            _unloadCandidates.Clear();

            EnsureCandidateCapacity();

            CellRelevanceJob relevanceJob = new CellRelevanceJob
            {
                Cells = _grid.Cells,
                CellActiveCount = _grid.CellActiveCount,
                EvaluationPosition = evaluationPosition,
                MaxUnloadDistance = _maxUnloadDistance,
                Relevance = _relevance
            };

            EntryEvaluationJob evaluationJob = new EntryEvaluationJob
            {
                Cells = _grid.Cells,
                Relevance = _relevance,
                EntryOrder = _grid.EntryOrder,
                Entries = _entries.AsArray(),
                States = _states.AsArray(),
                View = view,
                EvaluationPosition = evaluationPosition,
                HiddenPriorityScale = _settings.HiddenPriorityScale,
                LoadCandidates = _loadCandidates.AsParallelWriter(),
                UnloadCandidates = _unloadCandidates.AsParallelWriter()
            };

            MobileEvaluationJob mobileJob = new MobileEvaluationJob
            {
                MobileEntries = _mobileEntries.AsArray(),
                Entries = _entries.AsArray(),
                States = _states.AsArray(),
                View = view,
                EvaluationPosition = evaluationPosition,
                HiddenPriorityScale = _settings.HiddenPriorityScale,
                LoadCandidates = _loadCandidates.AsParallelWriter(),
                UnloadCandidates = _unloadCandidates.AsParallelWriter()
            };

            JobHandle relevanceHandle = relevanceJob.Schedule(_grid.CellCount, RelevanceBatchSize);
            JobHandle gridHandle = evaluationJob.Schedule(_grid.CellCount, EvaluationBatchSize, relevanceHandle);
            _evaluationHandle = mobileJob.Schedule(_mobileEntries.Length, MobileBatchSize, gridHandle);
            _jobsScheduled = true;

            JobHandle.ScheduleBatchedJobs();

            VicinityProfiling.EvaluateMarker.End();
        }

        private void EnsureCandidateCapacity()
        {
            int required = _entries.Length;

            if (_loadCandidates.Capacity < required)
            {
                _loadCandidates.Capacity = required;
            }

            if (_unloadCandidates.Capacity < required)
            {
                _unloadCandidates.Capacity = required;
            }

        }

        private void EnsureJobsComplete()
        {
            if (_jobsScheduled)
            {
                _evaluationHandle.Complete();
            }
        }

        private void HarvestScheduledJobs()
        {
            if (!_jobsScheduled)
            {
                return;
            }

            _evaluationHandle.Complete();
            _jobsScheduled = false;

            _loadCandidates.AsArray().Sort(new PendingLoadComparer());
            _unloadCandidates.AsArray().Sort();

            ApplyUnloads();
            ApplyLoads();
            EnforceMemoryBudget();
            PublishCounters();
        }

        private void ApplyUnloads()
        {
            for (int i = 0; i < _unloadCandidates.Length; i++)
            {
                ReleaseOrCancel(_unloadCandidates[i]);
            }
        }

        private void ApplyLoads()
        {
            VicinityProfiling.ScheduleMarker.Begin();

            int freeSlots = _settings.MaxConcurrentLoads - _loadsInFlight;

            for (int i = 0; i < _loadCandidates.Length; i++)
            {
                int entryIndex = _loadCandidates[i].EntryIndex;

                if (_entries[entryIndex].IsActive == 0)
                {
                    continue;
                }

                ResidencyState state = (ResidencyState)_states[entryIndex];

                if (state == ResidencyState.Unloaded)
                {
                    SetState(entryIndex, ResidencyState.Queued);
                    state = ResidencyState.Queued;
                }

                if (freeSlots <= 0 || state != ResidencyState.Queued)
                {
                    continue;
                }

                BeginLoad(entryIndex);
                freeSlots--;
            }

            VicinityProfiling.ScheduleMarker.End();
        }

        private void EnforceMemoryBudget()
        {
            long budget = _settings.MemoryBudgetBytes;
            if (budget <= 0L || _residentMemoryBytes <= budget)
            {
                return;
            }

            CollectResidentsByDistance();
            _residentEntries.AsArray().Sort(new FurthestFirstComparer());

            for (int i = 0; i < _residentEntries.Length && _residentMemoryBytes > budget; i++)
            {
                int entryIndex = _residentEntries[i].EntryIndex;

                if ((ResidencyState)_states[entryIndex] != ResidencyState.Resident)
                {
                    continue;
                }

                ReleaseResident(entryIndex);
                _evictedCount++;
            }

            WarnAboutBudgetOnce();
        }

        private void ApplyPendingPositions()
        {
            for (int i = 0; i < _pendingPositions.Length; i++)
            {
                PositionUpdate update = _pendingPositions[i];
                VicinityEntryData data = _entries[update.EntryIndex];
                data.Position = update.Position;
                _entries[update.EntryIndex] = data;
            }

            _pendingPositions.Clear();
        }

        private void CollectResidentsByDistance()
        {
            _residentEntries.Clear();

            for (int i = 0; i < _entries.Length; i++)
            {
                if ((ResidencyState)_states[i] != ResidencyState.Resident)
                {
                    continue;
                }

                _residentEntries.Add(new PendingLoad
                {
                    EntryIndex = i,
                    Priority = math.distancesq(_lastEvaluationPosition, _entries[i].Position)
                });
            }
        }

        private void WarnAboutBudgetOnce()
        {
            if (_budgetWarningLogged)
            {
                return;
            }

            _budgetWarningLogged = true;
            Debug.LogWarning(
                "Vicinity reached its memory ceiling and released the objects furthest from the player to stay under it. " +
                "Raise the budget in the profile, or shorten the loading distances. This message is not repeated.");
        }

        private void ReleaseOrCancel(int entryIndex)
        {
            switch ((ResidencyState)_states[entryIndex])
            {
                case ResidencyState.Queued:
                    SetState(entryIndex, ResidencyState.Unloaded);
                    break;

                case ResidencyState.Loading:
                    _releaseWhenLoaded[entryIndex] = true;
                    _cancellations[entryIndex]?.Cancel();
                    break;

                case ResidencyState.Resident:
                    ReleaseResident(entryIndex);
                    break;

                case ResidencyState.Failed:
                    SetState(entryIndex, ResidencyState.Unloaded);
                    break;
            }
        }

        private void ReleaseResident(int entryIndex)
        {
            GameObject instance = _instances[entryIndex];
            _instances[entryIndex] = null;
            _residentMemoryBytes -= _estimatedBytes[entryIndex];

            SetState(entryIndex, ResidencyState.Unloaded);
            _host?.OnUnloaded(entryIndex);

            if (instance != null)
            {
                _pool.Release(_keys[entryIndex], instance);
            }
        }

        private void BeginLoad(int entryIndex)
        {
            SetState(entryIndex, ResidencyState.Loading);
            _loadsInFlight++;
            _releaseWhenLoaded[entryIndex] = false;

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _cancellations[entryIndex] = cancellation;

            _ = RunLoadAsync(entryIndex, _keys[entryIndex], cancellation);
        }

        private async Awaitable RunLoadAsync(int entryIndex, AssetKey key, CancellationTokenSource cancellation)
        {
            GameObject instance = null;
            string failure = null;
            bool canceled = false;

            try
            {
                instance = await _pool.AcquireAsync(key, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }

            CompleteLoad(entryIndex, key, instance, failure, canceled, cancellation);
        }

        private void CompleteLoad(int entryIndex, AssetKey key, GameObject instance, string failure, bool canceled, CancellationTokenSource cancellation)
        {
            EnsureJobsComplete();
            _loadsInFlight--;
            cancellation.Dispose();

            if (_disposed)
            {
                if (instance != null)
                {
                    _providers.Release(key, instance);
                }

                return;
            }

            _cancellations[entryIndex] = null;
            bool releaseRequested = _releaseWhenLoaded[entryIndex];
            _releaseWhenLoaded[entryIndex] = false;

            if (instance == null)
            {
                if (canceled || releaseRequested)
                {
                    SetState(entryIndex, ResidencyState.Unloaded);
                }
                else
                {
                    HandleFailure(entryIndex, key, failure);
                }

                FinishRecyclingIfRequested(entryIndex);
                return;
            }

            if (releaseRequested || _entries[entryIndex].IsActive == 0)
            {
                _pool.Release(key, instance);
                SetState(entryIndex, ResidencyState.Unloaded);
                FinishRecyclingIfRequested(entryIndex);
                return;
            }

            VicinityProfiling.IntegrateMarker.Begin();

            _instances[entryIndex] = instance;
            _residentMemoryBytes += _estimatedBytes[entryIndex];
            SetState(entryIndex, ResidencyState.Resident);
            _host?.OnResident(entryIndex, instance);

            VicinityProfiling.IntegrateMarker.End();
        }

        private void FinishRecyclingIfRequested(int entryIndex)
        {
            if (_recycleWhenLoaded[entryIndex])
            {
                RecycleSlot(entryIndex);
            }
        }

        private void HandleFailure(int entryIndex, AssetKey key, string reason)
        {
            _attempts[entryIndex]++;

            if (!_failureLogged[entryIndex])
            {
                _failureLogged[entryIndex] = true;
                Debug.LogError($"Vicinity could not load {key}. {reason} This object is skipped, and this message is not repeated.");
            }

            SetState(entryIndex, ResidencyState.Failed);
            _host?.OnFailed(entryIndex, reason);

            if (_attempts[entryIndex] >= _settings.MaxLoadAttempts)
            {
                Deactivate(entryIndex);
            }
        }

        private void Deactivate(int entryIndex)
        {
            VicinityEntryData data = _entries[entryIndex];
            if (data.IsActive == 0)
            {
                return;
            }

            bool wasHolding = HoldsMemorySlot((ResidencyState)_states[entryIndex]);

            data.IsActive = 0;
            _entries[entryIndex] = data;
            _activeCount--;

            if (wasHolding)
            {
                _grid.AddActive(data.CellIndex, -1);
            }
        }

        private void SetState(int entryIndex, ResidencyState next)
        {
            ResidencyState previous = (ResidencyState)_states[entryIndex];
            if (previous == next)
            {
                return;
            }

            _stateCounts[(int)previous]--;
            _stateCounts[(int)next]++;
            _states[entryIndex] = (byte)next;

            if (_entries[entryIndex].IsActive == 0)
            {
                return;
            }

            bool wasHolding = HoldsMemorySlot(previous);
            bool isHolding = HoldsMemorySlot(next);

            if (wasHolding != isHolding)
            {
                _grid.AddActive(_entries[entryIndex].CellIndex, isHolding ? 1 : -1);
            }
        }

        private static bool HoldsMemorySlot(ResidencyState state) => state != ResidencyState.Unloaded;

        private bool IsWorthEvaluating(float3 evaluationPosition)
        {
            if (_gridDirty || !_hasEvaluatedOnce || _mobileEntries.Length > 0)
            {
                return true;
            }

            float deadZone = _settings.MovementDeadZone;
            bool viewpointMoved = math.distancesq(evaluationPosition, _lastEvaluationPosition) >= deadZone * deadZone;

            return viewpointMoved || _stateCounts[(int)ResidencyState.Queued] > 0 || _loadsInFlight > 0;
        }

        private void PublishCounters()
        {
            VicinityProfiling.ManagedCount.Value = _activeCount;
            VicinityProfiling.ResidentCount.Value = _stateCounts[(int)ResidencyState.Resident];
            VicinityProfiling.QueuedCount.Value = _stateCounts[(int)ResidencyState.Queued];
            VicinityProfiling.LoadingCount.Value = _stateCounts[(int)ResidencyState.Loading];
            VicinityProfiling.FailedCount.Value = _stateCounts[(int)ResidencyState.Failed];
            VicinityProfiling.ResidentMemory.Value = _residentMemoryBytes;
        }

        private void DisposeIfCreated()
        {
            if (_entries.IsCreated)
            {
                _entries.Dispose();
            }

            if (_states.IsCreated)
            {
                _states.Dispose();
            }

            if (_loadCandidates.IsCreated)
            {
                _loadCandidates.Dispose();
            }

            if (_unloadCandidates.IsCreated)
            {
                _unloadCandidates.Dispose();
            }

            if (_residentEntries.IsCreated)
            {
                _residentEntries.Dispose();
            }

            if (_mobileEntries.IsCreated)
            {
                _mobileEntries.Dispose();
            }

            if (_freeEntries.IsCreated)
            {
                _freeEntries.Dispose();
            }

            if (_pendingPositions.IsCreated)
            {
                _pendingPositions.Dispose();
            }

            if (_relevance.IsCreated)
            {
                _relevance.Dispose();
            }
        }

        #endregion
    }
}
