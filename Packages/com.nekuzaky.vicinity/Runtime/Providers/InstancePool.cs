using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    internal sealed class InstancePool
    {
        #region Main Methods

        internal InstancePool(AssetProviderRegistry providers, int capacity)
        {
            _providers = providers;
            _capacity = Mathf.Max(0, capacity);
        }

        internal int Capacity
        {
            get => _capacity;
            set
            {
                _capacity = Mathf.Max(0, value);
                TrimToCapacity();
            }
        }

        internal int ParkedCount => _parkedCount;

        internal Awaitable<GameObject> AcquireAsync(AssetKey key, CancellationToken cancellationToken)
        {
            if (TryUnpark(key, out GameObject reused))
            {
                AwaitableCompletionSource<GameObject> source = new AwaitableCompletionSource<GameObject>();
                source.SetResult(reused);
                return source.Awaitable;
            }

            return _providers.LoadAsync(key, cancellationToken);
        }

        internal void Release(AssetKey key, GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (_capacity <= 0 || _parkedCount >= _capacity)
            {
                _providers.Release(key, instance);
                return;
            }

            Park(key, instance);
        }

        internal void Clear()
        {
            foreach (KeyValuePair<AssetKey, Stack<GameObject>> parked in _parked)
            {
                while (parked.Value.Count > 0)
                {
                    _providers.Release(parked.Key, parked.Value.Pop());
                }
            }

            _parked.Clear();
            _parkedCount = 0;

            if (_root != null)
            {
                VicinityLifetime.Destroy(_root);
                _root = null;
            }
        }

        #endregion

        #region Privates

        private const string RootName = "Vicinity Pool";

        private readonly AssetProviderRegistry _providers;
        private readonly Dictionary<AssetKey, Stack<GameObject>> _parked = new Dictionary<AssetKey, Stack<GameObject>>();

        private GameObject _root;
        private int _capacity;
        private int _parkedCount;

        private bool TryUnpark(AssetKey key, out GameObject instance)
        {
            instance = null;

            if (!_parked.TryGetValue(key, out Stack<GameObject> stack))
            {
                return false;
            }

            while (stack.Count > 0)
            {
                GameObject candidate = stack.Pop();
                _parkedCount--;

                if (candidate != null)
                {
                    instance = candidate;
                    return true;
                }
            }

            return false;
        }

        private void Park(AssetKey key, GameObject instance)
        {
            EnsureRoot();

            instance.SetActive(false);
            instance.transform.SetParent(_root.transform, false);

            if (!_parked.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                _parked[key] = stack;
            }

            stack.Push(instance);
            _parkedCount++;
        }

        private void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject(RootName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _root.SetActive(false);
        }

        private void TrimToCapacity()
        {
            foreach (KeyValuePair<AssetKey, Stack<GameObject>> parked in _parked)
            {
                while (_parkedCount > _capacity && parked.Value.Count > 0)
                {
                    _providers.Release(parked.Key, parked.Value.Pop());
                    _parkedCount--;
                }
            }
        }

        #endregion
    }
}
