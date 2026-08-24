namespace Nekuzaky.Vicinity
{
    /// <summary>A snapshot of what Vicinity is currently holding in memory.</summary>
    public struct ResidencyStatistics
    {
        /// <summary>How many objects Vicinity drives in this scene.</summary>
        public int Managed;

        /// <summary>Objects that are not in memory.</summary>
        public int Unloaded;

        /// <summary>Objects waiting for a free loading slot.</summary>
        public int Queued;

        /// <summary>Objects loading right now.</summary>
        public int Loading;

        /// <summary>Objects fully loaded and visible.</summary>
        public int Resident;

        /// <summary>Objects Vicinity gave up on after repeated failures.</summary>
        public int Failed;

        /// <summary>Estimated memory held by loaded objects, in bytes.</summary>
        public long ResidentMemoryBytes;

        /// <summary>Instances kept aside for reuse instead of being destroyed.</summary>
        public int Pooled;

        /// <summary>Objects released because the memory ceiling was reached.</summary>
        public int Evicted;
    }
}
