using System;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>Where a managed object gets its heavy asset from.</summary>
    public enum AssetSourceKind
    {
        /// <summary>The prefab is referenced directly by the scene and always ships with it.</summary>
        DirectReference = 0,

        /// <summary>The prefab lives in a Resources folder and is loaded by path.</summary>
        Resources = 1,

        /// <summary>The prefab is an addressable asset, loaded by address.</summary>
        Addressables = 2
    }

    /// <summary>Identifies the heavy asset a managed object loads when the player comes close.</summary>
    [Serializable]
    public struct AssetKey : IEquatable<AssetKey>
    {
        [SerializeField] private AssetSourceKind m_sourceKind;
        [SerializeField] private GameObject m_directReference;
        [SerializeField] private string m_address;

        /// <summary>Which loading strategy this key requires.</summary>
        public readonly AssetSourceKind SourceKind => m_sourceKind;

        /// <summary>The prefab, when this key points at a direct reference.</summary>
        public readonly GameObject DirectReference => m_directReference;

        /// <summary>The Resources path or addressable address, depending on the source kind.</summary>
        public readonly string Address => m_address;

        /// <summary>True when this key carries enough information to be loaded.</summary>
        public readonly bool IsValid => m_sourceKind == AssetSourceKind.DirectReference
            ? m_directReference != null
            : !string.IsNullOrWhiteSpace(m_address);

        /// <summary>Builds a key that loads a prefab referenced directly by the scene.</summary>
        public static AssetKey FromDirectReference(GameObject prefab)
        {
            return new AssetKey
            {
                m_sourceKind = AssetSourceKind.DirectReference,
                m_directReference = prefab,
                m_address = null
            };
        }

        /// <summary>Builds a key that loads a prefab from a Resources folder.</summary>
        public static AssetKey FromResourcesPath(string resourcesPath)
        {
            return new AssetKey
            {
                m_sourceKind = AssetSourceKind.Resources,
                m_directReference = null,
                m_address = resourcesPath
            };
        }

        /// <summary>Builds a key that loads an addressable asset by address.</summary>
        public static AssetKey FromAddress(string address)
        {
            return new AssetKey
            {
                m_sourceKind = AssetSourceKind.Addressables,
                m_directReference = null,
                m_address = address
            };
        }

        /// <summary>Compares two keys by source kind and payload.</summary>
        public readonly bool Equals(AssetKey other)
        {
            return m_sourceKind == other.m_sourceKind
                && m_directReference == other.m_directReference
                && string.Equals(m_address, other.m_address, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public readonly override bool Equals(object obj) => obj is AssetKey other && Equals(other);

        /// <inheritdoc />
        public readonly override int GetHashCode()
        {
            int referenceHash = m_directReference == null ? 0 : m_directReference.GetInstanceID();
            int addressHash = m_address == null ? 0 : m_address.GetHashCode();
            return ((int)m_sourceKind * 397 ^ referenceHash) * 397 ^ addressHash;
        }

        /// <summary>A short human readable form, used by the dashboard and by error messages.</summary>
        public readonly override string ToString()
        {
            return m_sourceKind switch
            {
                AssetSourceKind.DirectReference => m_directReference == null ? "<missing prefab>" : m_directReference.name,
                _ => string.IsNullOrEmpty(m_address) ? "<no address>" : m_address
            };
        }
    }
}
