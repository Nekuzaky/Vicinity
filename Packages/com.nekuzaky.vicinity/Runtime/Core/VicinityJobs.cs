using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Nekuzaky.Vicinity
{
    internal struct EvaluationOutput
    {
        public NativeList<PendingLoad>.ParallelWriter LoadCandidates;
        public NativeList<int>.ParallelWriter UnloadCandidates;
    }

    internal static class EntryDecision
    {
        internal static void Evaluate(
            int entryIndex,
            in VicinityEntryData entry,
            byte state,
            float3 evaluationPosition,
            in VicinityViewState view,
            float hiddenPriorityScale,
            ref EvaluationOutput output)
        {
            if (entry.IsActive == 0)
            {
                return;
            }

            float distanceSquared = math.distancesq(evaluationPosition, entry.Position);

            bool wantsMemory = state == (byte)ResidencyState.Unloaded || state == (byte)ResidencyState.Queued;
            bool insideBand = distanceSquared <= entry.LoadDistanceSquared
                && distanceSquared >= entry.InnerLoadDistanceSquared;

            if (wantsMemory && insideBand)
            {
                output.LoadCandidates.AddNoResize(new PendingLoad
                {
                    EntryIndex = entryIndex,
                    Priority = ComputePriority(entry, distanceSquared, view, hiddenPriorityScale)
                });

                return;
            }

            bool holdsMemory = state == (byte)ResidencyState.Failed
                || state == (byte)ResidencyState.Queued
                || state == (byte)ResidencyState.Loading
                || state == (byte)ResidencyState.Resident;

            bool outsideBand = distanceSquared >= entry.UnloadDistanceSquared
                || distanceSquared < entry.InnerUnloadDistanceSquared;

            if (holdsMemory && outsideBand)
            {
                output.UnloadCandidates.AddNoResize(entryIndex);
            }
        }

        private static float ComputePriority(in VicinityEntryData entry, float distanceSquared, in VicinityViewState view, float hiddenPriorityScale)
        {
            float priority = math.sqrt(distanceSquared);
            float sizePenalty = 1f + entry.RelativeCost;

            return IsInsideFrustum(entry, view) ? priority * sizePenalty : priority * sizePenalty * hiddenPriorityScale;
        }

        private static bool IsInsideFrustum(in VicinityEntryData entry, in VicinityViewState view)
        {
            if (!view.HasFrustum)
            {
                return true;
            }

            float radius = entry.BoundsRadius;
            return IsInFront(view.PlaneLeft, entry.Position, radius)
                && IsInFront(view.PlaneRight, entry.Position, radius)
                && IsInFront(view.PlaneDown, entry.Position, radius)
                && IsInFront(view.PlaneUp, entry.Position, radius)
                && IsInFront(view.PlaneNear, entry.Position, radius)
                && IsInFront(view.PlaneFar, entry.Position, radius);
        }

        private static bool IsInFront(float4 plane, float3 position, float radius)
        {
            return math.dot(plane.xyz, position) + plane.w >= -radius;
        }
    }

    [BurstCompile]
    internal struct CellRelevanceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VicinityCell> Cells;
        [ReadOnly] public NativeArray<int> CellActiveCount;

        public float3 EvaluationPosition;
        public float MaxUnloadDistance;

        [WriteOnly] public NativeArray<byte> Relevance;

        public void Execute(int index)
        {
            VicinityCell cell = Cells[index];
            float3 outside = math.max(math.abs(EvaluationPosition - cell.Center) - cell.Extents, float3.zero);
            bool withinReach = math.lengthsq(outside) <= MaxUnloadDistance * MaxUnloadDistance;
            bool holdsMemory = CellActiveCount[index] > 0;

            Relevance[index] = (byte)(withinReach || holdsMemory ? 1 : 0);
        }
    }

    [BurstCompile]
    internal struct EntryEvaluationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VicinityCell> Cells;
        [ReadOnly] public NativeArray<byte> Relevance;
        [ReadOnly] public NativeArray<int> EntryOrder;
        [ReadOnly] public NativeArray<VicinityEntryData> Entries;
        [ReadOnly] public NativeArray<byte> States;

        public VicinityViewState View;
        public float3 EvaluationPosition;
        public float HiddenPriorityScale;

        public NativeList<PendingLoad>.ParallelWriter LoadCandidates;
        public NativeList<int>.ParallelWriter UnloadCandidates;

        public void Execute(int cellIndex)
        {
            if (Relevance[cellIndex] == 0)
            {
                return;
            }

            EvaluationOutput output = new EvaluationOutput
            {
                LoadCandidates = LoadCandidates,
                UnloadCandidates = UnloadCandidates
            };

            VicinityCell cell = Cells[cellIndex];

            for (int slot = 0; slot < cell.EntryCount; slot++)
            {
                int entryIndex = EntryOrder[cell.EntryStart + slot];

                EntryDecision.Evaluate(
                    entryIndex,
                    Entries[entryIndex],
                    States[entryIndex],
                    EvaluationPosition,
                    View,
                    HiddenPriorityScale,
                    ref output);
            }
        }
    }

    [BurstCompile]
    internal struct MobileEvaluationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> MobileEntries;
        [ReadOnly] public NativeArray<VicinityEntryData> Entries;
        [ReadOnly] public NativeArray<byte> States;

        public VicinityViewState View;
        public float3 EvaluationPosition;
        public float HiddenPriorityScale;

        public NativeList<PendingLoad>.ParallelWriter LoadCandidates;
        public NativeList<int>.ParallelWriter UnloadCandidates;

        public void Execute(int index)
        {
            EvaluationOutput output = new EvaluationOutput
            {
                LoadCandidates = LoadCandidates,
                UnloadCandidates = UnloadCandidates
            };

            int entryIndex = MobileEntries[index];

            EntryDecision.Evaluate(
                entryIndex,
                Entries[entryIndex],
                States[entryIndex],
                EvaluationPosition,
                View,
                HiddenPriorityScale,
                ref output);
        }
    }

    internal struct PendingLoadComparer : System.Collections.Generic.IComparer<PendingLoad>
    {
        public int Compare(PendingLoad left, PendingLoad right)
        {
            int byPriority = left.Priority.CompareTo(right.Priority);
            return byPriority != 0 ? byPriority : left.EntryIndex.CompareTo(right.EntryIndex);
        }
    }

    internal struct FurthestFirstComparer : System.Collections.Generic.IComparer<PendingLoad>
    {
        public int Compare(PendingLoad left, PendingLoad right)
        {
            int byPriority = right.Priority.CompareTo(left.Priority);
            return byPriority != 0 ? byPriority : left.EntryIndex.CompareTo(right.EntryIndex);
        }
    }
}
