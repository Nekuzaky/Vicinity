using UnityEngine;

namespace Nekuzaky.Vicinity
{
    internal static class AddressablesProviderBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            VicinityProviders.RegisterFactory(AssetSourceKind.Addressables, static () => new AddressablesProvider());
        }
    }
}
