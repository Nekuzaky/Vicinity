using System.Collections.Generic;
using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// Covers an area of the level with its own distances. Objects inside it use this volume's profile
    /// instead of the one on the manager, which is how a cramped interior and an open field can coexist.
    /// </summary>
    [AddComponentMenu("Vicinity/Vicinity Volume")]
    [DisallowMultipleComponent]
    public sealed class VicinityVolume : MonoBehaviour
    {
        #region Exposed

        [SerializeField]
        [Tooltip("Where the box sits, relative to this object.")]
        private Vector3 m_center = Vector3.zero;

        [SerializeField]
        [Tooltip("How large the box is, in meters.")]
        private Vector3 m_size = new Vector3(50f, 20f, 50f);

        [SerializeField]
        [Tooltip("Distances and budgets used by every managed object inside this box. Leave empty to use the manager's settings.")]
        private VicinityProfile m_profile;

        [SerializeField]
        [Tooltip("When two volumes overlap, the one with the highest number wins for the objects in the overlap.")]
        private int m_priority;

        #endregion

        #region Unity API

        private void OnEnable()
        {
            if (!_volumes.Contains(this))
            {
                _volumes.Add(this);
            }
        }

        private void OnDisable()
        {
            _volumes.Remove(this);
        }

        #endregion

        #region Main Methods

        /// <summary>Distances and budgets applied to managed objects inside this volume.</summary>
        public VicinityProfile Profile => m_profile;

        /// <summary>When two volumes overlap, the highest number wins.</summary>
        public int Priority => m_priority;

        /// <summary>Where the box sits, relative to this object.</summary>
        public Vector3 Center => m_center;

        /// <summary>How large the box is, in meters.</summary>
        public Vector3 Size => m_size;

        /// <summary>The box in world space, taking this object's transform into account.</summary>
        public Bounds WorldBounds
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                Vector3 worldSize = new Vector3(
                    Mathf.Abs(m_size.x * scale.x),
                    Mathf.Abs(m_size.y * scale.y),
                    Mathf.Abs(m_size.z * scale.z));

                return new Bounds(transform.TransformPoint(m_center), worldSize);
            }
        }

        /// <summary>True when a world position falls inside this volume.</summary>
        public bool Contains(Vector3 worldPosition) => WorldBounds.Contains(worldPosition);

        /// <summary>Resizes the box. Meant for the scene view handles and for the dashboard.</summary>
        public void SetBox(Vector3 center, Vector3 size)
        {
            m_center = center;
            m_size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        /// <summary>Every volume currently enabled in the loaded scenes.</summary>
        public static IReadOnlyList<VicinityVolume> Active => _volumes;

        /// <summary>Returns the highest priority volume covering a position, or null when none does.</summary>
        public static VicinityVolume FindCovering(Vector3 worldPosition)
        {
            VicinityVolume best = null;

            for (int i = 0; i < _volumes.Count; i++)
            {
                VicinityVolume candidate = _volumes[i];
                if (candidate == null || !candidate.Contains(worldPosition))
                {
                    continue;
                }

                if (best == null || candidate.Priority > best.Priority)
                {
                    best = candidate;
                }
            }

            return best;
        }

        #endregion

        #region Privates

        private static readonly List<VicinityVolume> _volumes = new List<VicinityVolume>();

        internal static void ClearRegistry()
        {
            _volumes.Clear();
        }

        #endregion
    }
}
