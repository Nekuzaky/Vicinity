using System;

namespace Nekuzaky.Vicinity.Editor
{
    /// <summary>
    /// Lets Vicinity hand an asset over to Addressables without depending on the Addressables package.
    /// A companion assembly fills this in when the package is installed; when it is absent nothing
    /// registers, <see cref="IsAvailable"/> stays false, and callers fall back to a weaker reference.
    /// </summary>
    internal static class VicinityAddressableBridge
    {
        #region Main Methods

        /// <summary>True when the Addressables package is installed and able to take assets.</summary>
        internal static bool IsAvailable => _authoring != null;

        /// <summary>Called once by the companion assembly. Later calls replace the earlier one.</summary>
        internal static void Register(Func<string, string, string> authoring)
        {
            _authoring = authoring;
        }

        /// <summary>
        /// Marks the asset at <paramref name="assetPath"/> addressable under <paramref name="wantedAddress"/>
        /// and returns the address it ended up with. Returns null when Addressables is not installed, or
        /// when it refused the asset.
        /// </summary>
        internal static string MakeAddressable(string assetPath, string wantedAddress)
        {
            if (_authoring == null || string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            return _authoring(assetPath, wantedAddress);
        }

        #endregion

        #region Privates

        private static Func<string, string, string> _authoring;

        #endregion
    }
}
