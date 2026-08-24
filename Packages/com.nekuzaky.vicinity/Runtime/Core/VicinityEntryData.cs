using Unity.Mathematics;

namespace Nekuzaky.Vicinity
{
    internal struct VicinityEntryData
    {
        public float3 Position;
        public float BoundsRadius;
        public float LoadDistanceSquared;
        public float UnloadDistanceSquared;
        public float InnerLoadDistanceSquared;
        public float InnerUnloadDistanceSquared;
        public int CellIndex;
        public float RelativeCost;
        public byte IsActive;
        public byte IsMobile;
    }

    internal enum EntryVerdict : byte
    {
        Hold = 0,
        ShouldLoad = 1,
        ShouldUnload = 2
    }

    internal struct EntryEvaluation
    {
        public float Priority;
        public EntryVerdict Verdict;
    }

    internal struct PositionUpdate
    {
        public int EntryIndex;
        public float3 Position;
    }

    internal struct PendingLoad
    {
        public float Priority;
        public int EntryIndex;
    }

    internal struct VicinityViewState
    {
        public float3 Position;
        public float3 Velocity;
        public float4 PlaneLeft;
        public float4 PlaneRight;
        public float4 PlaneDown;
        public float4 PlaneUp;
        public float4 PlaneNear;
        public float4 PlaneFar;
        public bool HasFrustum;
    }
}
