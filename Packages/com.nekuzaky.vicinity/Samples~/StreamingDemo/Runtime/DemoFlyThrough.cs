using UnityEngine;

namespace Nekuzaky.Vicinity.Demo
{
    /// <summary>Walks the viewpoint back and forth across the demo field so objects load and release on their own.</summary>
    [AddComponentMenu("Vicinity/Demo/Demo Fly Through")]
    public sealed class DemoFlyThrough : MonoBehaviour
    {
        #region Exposed

        [SerializeField]
        [Tooltip("How fast the viewpoint travels, in meters per second.")]
        [Min(0f)]
        private float m_speed = 40f;

        [SerializeField]
        [Tooltip("How far it travels before turning around, in meters.")]
        [Min(1f)]
        private float m_range = 400f;

        [SerializeField]
        [Tooltip("Jump straight to the far end instead of walking there. Use it to check that a teleport releases everything at once.")]
        private KeyCode m_teleportKey = KeyCode.T;

        #endregion

        #region Unity API

        private void Start()
        {
            _origin = transform.position;
        }

        private void Update()
        {
            if (Input.GetKeyDown(m_teleportKey))
            {
                _travelled = _travelled > m_range * 0.5f ? 0f : m_range;
                Apply();
                return;
            }

            _travelled += m_speed * Time.deltaTime * _direction;

            if (_travelled > m_range)
            {
                _travelled = m_range;
                _direction = -1f;
            }
            else if (_travelled < 0f)
            {
                _travelled = 0f;
                _direction = 1f;
            }

            Apply();
        }

        #endregion

        #region Privates

        private Vector3 _origin;
        private float _travelled;
        private float _direction = 1f;

        private void Apply()
        {
            transform.position = _origin + Vector3.forward * _travelled;
        }

        #endregion
    }
}
