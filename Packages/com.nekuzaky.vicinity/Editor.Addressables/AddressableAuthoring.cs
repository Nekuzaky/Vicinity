using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Nekuzaky.Vicinity.Editor
{
    /// <summary>
    /// Hands assets over to Addressables on Vicinity's behalf. This whole assembly is skipped when the
    /// Addressables package is not installed, which is what keeps Vicinity usable without it.
    /// </summary>
    internal static class AddressableAuthoring
    {
        #region Unity API

        [InitializeOnLoadMethod]
        private static void Register()
        {
            VicinityAddressableBridge.Register(MakeAddressable);
        }

        #endregion

        #region Privates

        private static string MakeAddressable(string assetPath, string wantedAddress)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            // Creates the settings asset on first use, so the user never has to open the
            // Addressables window before Vicinity can stream anything.
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            if (settings == null || settings.DefaultGroup == null)
            {
                return null;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);

            if (entry == null)
            {
                return null;
            }

            entry.address = wantedAddress;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);

            return entry.address;
        }

        #endregion
    }
}
