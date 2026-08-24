using System;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// One quality step of a managed object. The first level is the most detailed and is used when the
    /// player is closest; each following level covers a band further away.
    /// </summary>
    [Serializable]
    public struct DetailLevel
    {
        [SerializeField]
        [Tooltip("The model loaded for this step.")]
        private AssetKey m_model;

        [SerializeField]
        [Tooltip("How far from the player this step stops being used, in meters. The next step takes over beyond it.")]
        [Min(0f)]
        private float m_range;

        /// <summary>The model this step loads.</summary>
        public readonly AssetKey Model => m_model;

        /// <summary>How far from the player this step stops being used, in meters.</summary>
        public readonly float Range => m_range;

        /// <summary>True when this step names a model that can actually be loaded.</summary>
        public readonly bool IsValid => m_model.IsValid && m_range > 0f;

        /// <summary>Builds a step from a model and the distance it reaches.</summary>
        public static DetailLevel Create(AssetKey model, float range)
        {
            return new DetailLevel
            {
                m_model = model,
                m_range = Mathf.Max(0f, range)
            };
        }
    }
}
