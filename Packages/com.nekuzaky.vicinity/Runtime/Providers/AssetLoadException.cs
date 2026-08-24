using System;

namespace Nekuzaky.Vicinity
{
    /// <summary>Raised by a provider when an asset cannot be loaded. Never reaches user code.</summary>
    public sealed class AssetLoadException : Exception
    {
        /// <summary>Creates an exception describing why a key failed to load.</summary>
        public AssetLoadException(AssetKey key, string reason)
            : base($"Vicinity could not load '{key}': {reason}")
        {
            Key = key;
        }

        /// <summary>The key that failed.</summary>
        public AssetKey Key { get; }
    }
}
