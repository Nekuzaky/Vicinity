using System.Threading;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>Instantiates a prefab that the scene already references. Needs no extra package.</summary>
    public sealed class DirectReferenceProvider : IAssetProvider
    {
        #region Main Methods

        /// <inheritdoc />
        public AssetSourceKind SourceKind => AssetSourceKind.DirectReference;

        /// <inheritdoc />
        public async Awaitable<GameObject> LoadAsync(AssetKey key, CancellationToken cancellationToken)
        {
            GameObject prefab = key.DirectReference;
            if (prefab == null)
            {
                throw new AssetLoadException(key, "the prefab reference is empty");
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
