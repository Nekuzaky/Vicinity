using System;
using Unity.Profiling;
using Unity.Profiling.Editor;

namespace Nekuzaky.Vicinity.Editor
{
    [Serializable]
    [ProfilerModuleMetadata("Vicinity")]
    internal sealed class VicinityProfilerModule : ProfilerModule
    {
        internal VicinityProfilerModule()
            : base(Counters, ProfilerModuleChartType.Line, AutoEnabledCategories)
        {
        }

        private static readonly string[] AutoEnabledCategories = { VicinityProfiling.CategoryName };

        private static readonly ProfilerCounterDescriptor[] Counters =
        {
            new ProfilerCounterDescriptor(VicinityProfiling.ResidentCounterName, VicinityProfiling.Category),
            new ProfilerCounterDescriptor(VicinityProfiling.LoadingCounterName, VicinityProfiling.Category),
            new ProfilerCounterDescriptor(VicinityProfiling.QueuedCounterName, VicinityProfiling.Category),
            new ProfilerCounterDescriptor(VicinityProfiling.FailedCounterName, VicinityProfiling.Category),
            new ProfilerCounterDescriptor(VicinityProfiling.ManagedCounterName, VicinityProfiling.Category),
            new ProfilerCounterDescriptor(VicinityProfiling.MemoryCounterName, VicinityProfiling.Category)
        };
    }
}
