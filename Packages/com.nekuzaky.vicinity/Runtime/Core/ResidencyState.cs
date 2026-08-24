namespace Nekuzaky.Vicinity
{
    /// <summary>Where a managed object currently stands between "not in memory" and "fully loaded".</summary>
    public enum ResidencyState : byte
    {
        /// <summary>Not in memory. Only the lightweight proxy is present in the scene.</summary>
        Unloaded = 0,

        /// <summary>Close enough to be loaded, waiting for a free loading slot.</summary>
        Queued = 1,

        /// <summary>Being loaded right now.</summary>
        Loading = 2,

        /// <summary>Fully loaded and visible.</summary>
        Resident = 3,

        /// <summary>Being released.</summary>
        Unloading = 4,

        /// <summary>Loading failed. Vicinity retries a bounded number of times, then gives up quietly.</summary>
        Failed = 5
    }
}
