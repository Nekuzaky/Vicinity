using Unity.Profiling;

namespace Nekuzaky.Vicinity
{
    internal static class VicinityProfiling
    {
        internal const string CategoryName = "Vicinity";
        internal const string ResidentCounterName = "Vicinity Resident Objects";
        internal const string QueuedCounterName = "Vicinity Queued Objects";
        internal const string LoadingCounterName = "Vicinity Loading Objects";
        internal const string FailedCounterName = "Vicinity Failed Objects";
        internal const string ManagedCounterName = "Vicinity Managed Objects";
        internal const string MemoryCounterName = "Vicinity Resident Memory";

        internal static readonly ProfilerCategory Category = new ProfilerCategory(CategoryName);

        internal static readonly ProfilerMarker EvaluateMarker = new ProfilerMarker(Category, "Vicinity.Evaluate");
        internal static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker(Category, "Vicinity.Schedule");
        internal static readonly ProfilerMarker LoadMarker = new ProfilerMarker(Category, "Vicinity.Load");
        internal static readonly ProfilerMarker IntegrateMarker = new ProfilerMarker(Category, "Vicinity.Integrate");

        internal static ProfilerCounterValue<int> ResidentCount =
            new ProfilerCounterValue<int>(Category, ResidentCounterName, ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        internal static ProfilerCounterValue<int> QueuedCount =
            new ProfilerCounterValue<int>(Category, QueuedCounterName, ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        internal static ProfilerCounterValue<int> LoadingCount =
            new ProfilerCounterValue<int>(Category, LoadingCounterName, ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        internal static ProfilerCounterValue<int> FailedCount =
            new ProfilerCounterValue<int>(Category, FailedCounterName, ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        internal static ProfilerCounterValue<int> ManagedCount =
            new ProfilerCounterValue<int>(Category, ManagedCounterName, ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        internal static ProfilerCounterValue<long> ResidentMemory =
            new ProfilerCounterValue<long>(Category, MemoryCounterName, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);
    }
}
