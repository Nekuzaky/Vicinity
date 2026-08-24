using System;
using System.Threading;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    internal static class AsyncInstantiation
    {
        internal static async Awaitable<GameObject> InstantiateOneAsync(AssetKey key, GameObject prefab, CancellationToken cancellationToken)
        {
            InstantiateParameters parameters = new InstantiateParameters { worldSpace = true };
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(prefab, 1, parameters, cancellationToken);

            await Awaitable.FromAsyncOperation(operation, CancellationToken.None);

            GameObject[] produced = operation.Result;
            if (cancellationToken.IsCancellationRequested)
            {
                DestroyProduced(produced);
                throw new OperationCanceledException(cancellationToken);
            }

            if (produced == null || produced.Length == 0 || produced[0] == null)
            {
                throw new AssetLoadException(key, "instantiation produced no object");
            }

            return produced[0];
        }

        private static void DestroyProduced(GameObject[] produced)
        {
            if (produced == null)
            {
                return;
            }

            for (int i = 0; i < produced.Length; i++)
            {
                if (produced[i] != null)
                {
                    VicinityLifetime.Destroy(produced[i]);
                }
            }
        }
    }
}
