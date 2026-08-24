using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class FakeAssetProvider : IAssetProvider
    {
        internal FakeAssetProvider(bool completeImmediately)
        {
            _completeImmediately = completeImmediately;
        }

        public AssetSourceKind SourceKind => AssetSourceKind.DirectReference;

        internal int LoadCallCount { get; private set; }

        internal int ReleaseCallCount { get; private set; }

        internal bool FailEveryLoad { get; set; }

        internal int PendingCount => _pending.Count;

        public Awaitable<GameObject> LoadAsync(AssetKey key, CancellationToken cancellationToken)
        {
            LoadCallCount++;
            AwaitableCompletionSource<GameObject> source = new AwaitableCompletionSource<GameObject>();

            if (FailEveryLoad)
            {
                source.SetException(new AssetLoadException(key, "the fake provider was told to fail"));
                return source.Awaitable;
            }

            if (_completeImmediately)
            {
                source.SetResult(CreateInstance());
                return source.Awaitable;
            }

            _pending.Add(new PendingRequest { Source = source, Cancellation = cancellationToken });
            return source.Awaitable;
        }

        public void Release(GameObject instance)
        {
            ReleaseCallCount++;

            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        internal void CompleteAllPending()
        {
            List<PendingRequest> snapshot = new List<PendingRequest>(_pending);
            _pending.Clear();

            foreach (PendingRequest request in snapshot)
            {
                if (request.Cancellation.IsCancellationRequested)
                {
                    request.Source.SetCanceled();
                    continue;
                }

                request.Source.SetResult(CreateInstance());
            }
        }

        internal void CompleteAllPendingIgnoringCancellation()
        {
            List<PendingRequest> snapshot = new List<PendingRequest>(_pending);
            _pending.Clear();

            foreach (PendingRequest request in snapshot)
            {
                request.Source.SetResult(CreateInstance());
            }
        }

        internal void DestroySpawnedInstances()
        {
            foreach (GameObject instance in _spawned)
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            _spawned.Clear();
        }

        private readonly List<PendingRequest> _pending = new List<PendingRequest>();
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly bool _completeImmediately;

        private GameObject CreateInstance()
        {
            GameObject instance = new GameObject("Vicinity Test Instance")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _spawned.Add(instance);
            return instance;
        }

        private struct PendingRequest
        {
            public AwaitableCompletionSource<GameObject> Source;
            public CancellationToken Cancellation;
        }
    }
}
