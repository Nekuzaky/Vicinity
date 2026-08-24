using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// Loads addressable assets. This class only exists when the Addressables package is installed;
    /// Vicinity registers it automatically and nothing needs to be configured.
    /// </summary>
    public sealed class AddressablesProvider : IAssetProvider
    {
        #region Main Methods

        /// <inheritdoc />
        public AssetSourceKind SourceKind => AssetSourceKind.Addressables;

        /// <inheritdoc />
        public async Awaitable<GameObject> LoadAsync(AssetKey key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key.Address))
            {
                throw new AssetLoadException(key, "the address is empty");
            }

            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key.Address);
            await handle.Task;
            await Awaitable.MainThreadAsync();

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                string reason = handle.OperationException == null
                    ? "the address could not be resolved"
                    : handle.OperationException.Message;
                throw new AssetLoadException(key, reason);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Addressables.ReleaseInstance(handle.Result);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return handle.Result;
        }

        /// <inheritdoc />
        public void Release(GameObject instance)
        {
            if (instance != null && !Addressables.ReleaseInstance(instance))
            {
                Object.Destroy(instance);
            }
        }

        #endregion
    }
}
