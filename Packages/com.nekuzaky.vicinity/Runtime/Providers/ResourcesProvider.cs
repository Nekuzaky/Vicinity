using System.Threading;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>Loads a prefab from a Resources folder, then instantiates it. Needs no extra package.</summary>
    public sealed class ResourcesProvider : IAssetProvider
    {
        #region Main Methods

        /// <inheritdoc />
        public AssetSourceKind SourceKind => AssetSourceKind.Resources;

        /// <inheritdoc />
        public async Awaitable<GameObject> LoadAsync(AssetKey key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key.Address))
            {
                throw new AssetLoadException(key, "the Resources path is empty");
            }

            ResourceRequest request = Resources.LoadAsync<GameObject>(key.Address);
            await Awaitable.FromAsyncOperation(request, cancellationToken);

            if (request.asset is not GameObject prefab)
            {
                throw new AssetLoadException(key, "no prefab was found at that Resources path");
            }

            return await AsyncInstantiation.InstantiateOneAsync(key, prefab, cancellationToken);
        }

        /// <inheritdoc />
        public void Release(GameObject instance)
        {
            if (instance != null)
            {
                VicinityLifetime.Destroy(instance);
            }
        }

        #endregion
    }
}
