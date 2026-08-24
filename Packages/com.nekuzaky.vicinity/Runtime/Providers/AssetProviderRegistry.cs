using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// Maps every asset source to the provider able to load it. Vicinity fills this automatically from
    /// the packages installed in the project, so an artist never chooses a provider.
    /// </summary>
    public sealed class AssetProviderRegistry
    {
        #region Main Methods

        /// <summary>Registers a provider, replacing any previous provider for the same source.</summary>
        public void Register(IAssetProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _providers[provider.SourceKind] = provider;
        }

        /// <summary>True when an asset coming from this source can be loaded in the current project.</summary>
        public bool Supports(AssetSourceKind sourceKind) => _providers.ContainsKey(sourceKind);

        /// <summary>Returns the provider for a key, or null when that source is not available.</summary>
        public IAssetProvider Resolve(AssetKey key)
        {
            return _providers.TryGetValue(key.SourceKind, out IAssetProvider provider) ? provider : null;
        }

        /// <summary>Loads a key through the provider that owns its source.</summary>
        public Awaitable<GameObject> LoadAsync(AssetKey key, CancellationToken cancellationToken)
        {
            IAssetProvider provider = Resolve(key);
            if (provider == null)
            {
                throw new AssetLoadException(key, $"no provider is installed for {key.SourceKind}");
            }

            return provider.LoadAsync(key, cancellationToken);
        }

        /// <summary>Releases an instance through the provider that owns its source.</summary>
        public void Release(AssetKey key, GameObject instance)
        {
            Resolve(key)?.Release(instance);
        }

        /// <summary>Builds the registry with every provider available in this project.</summary>
        public static AssetProviderRegistry CreateDefault()
        {
            AssetProviderRegistry registry = new AssetProviderRegistry();
            registry.Register(new DirectReferenceProvider());
            registry.Register(new ResourcesProvider());
            VicinityProviders.PopulateOptional(registry);
            return registry;
        }

        #endregion

        #region Privates

        private readonly Dictionary<AssetSourceKind, IAssetProvider> _providers =
            new Dictionary<AssetSourceKind, IAssetProvider>();

        #endregion
    }
}
