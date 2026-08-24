using System;
using System.Collections.Generic;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// The point where optional providers announce themselves. The Addressables provider registers here
    /// when the Addressables package is installed, which is how Vicinity supports it without depending on it.
    /// </summary>
    public static class VicinityProviders
    {
        #region Main Methods

        /// <summary>
        /// Declares the provider to use for an asset source. Registering the same source twice replaces the
        /// previous factory, which keeps the call safe to repeat across domain reloads.
        /// </summary>
        public static void RegisterFactory(AssetSourceKind sourceKind, Func<IAssetProvider> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _factories[sourceKind] = factory;
        }

        /// <summary>True when a provider is available for this asset source in the current project.</summary>
        public static bool IsRegistered(AssetSourceKind sourceKind) => _factories.ContainsKey(sourceKind);

        internal static void PopulateOptional(AssetProviderRegistry registry)
        {
            foreach (KeyValuePair<AssetSourceKind, Func<IAssetProvider>> factory in _factories)
            {
                IAssetProvider provider = factory.Value.Invoke();
                if (provider != null)
                {
                    registry.Register(provider);
                }
            }
        }

        #endregion

        #region Privates

        private static readonly Dictionary<AssetSourceKind, Func<IAssetProvider>> _factories =
            new Dictionary<AssetSourceKind, Func<IAssetProvider>>();

        #endregion
    }
}
