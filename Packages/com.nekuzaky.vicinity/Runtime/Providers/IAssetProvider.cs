using System.Threading;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// Turns an <see cref="AssetKey"/> into a live instance. Vicinity ships one implementation per
    /// asset source and picks the right one automatically, so a project never selects a provider by hand.
    /// </summary>
    public interface IAssetProvider
    {
        /// <summary>The asset source this provider knows how to load.</summary>
        AssetSourceKind SourceKind { get; }

        /// <summary>
        /// Loads and instantiates the asset. Throws when the asset cannot be produced; Vicinity catches
        /// the failure, marks the entry as failed and logs it once.
        /// </summary>
        Awaitable<GameObject> LoadAsync(AssetKey key, CancellationToken cancellationToken);

        /// <summary>Releases an instance previously returned by <see cref="LoadAsync"/>.</summary>
        void Release(GameObject instance);
    }
}
