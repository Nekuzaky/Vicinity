using UnityEngine;

namespace Nekuzaky.Vicinity
{
    /// <summary>
    /// Marks the point of view Vicinity measures distances from, usually the player or the main camera.
    /// Put one in the scene; Vicinity falls back to the active camera if none is present.
    /// </summary>
    [AddComponentMenu("Vicinity/Vicinity Target")]
    [DisallowMultipleComponent]
    public sealed class VicinityTarget : MonoBehaviour
    {
        #region Exposed

        [SerializeField]
        [Tooltip("When several targets exist, the one with the highest number is used. Leave at 0 unless you are switching between viewpoints.")]
        private int m_priority;

        [SerializeField]
        [Tooltip("Look ahead of the target's movement so assets start loading before the player arrives. In seconds. 0 disables it.")]
        [Range(0f, 5f)]
        private float m_lookAheadSeconds = ResidencySettings.DefaultPredictionHorizon;

        [SerializeField]
        [Tooltip("Load what the player looks at first. Turn this off if the player can turn around instantly, for example in a top-down game.")]
        private bool m_prioritiseWhatTheCameraSees = true;

        #endregion

        #region Unity API

        private void OnEnable()
        {
            _previousPosition = transform.position;
            _velocity = Vector3.zero;
            _cameraSearched = false;
            VicinityTargetRegistry.Add(this);
        }

        private void OnDisable()
        {
            VicinityTargetRegistry.Remove(this);
        }

        #endregion

        #region Main Methods

        /// <summary>When several targets exist, the highest number wins.</summary>
        public int Priority => m_priority;

        /// <summary>How far ahead of the target's movement Vicinity looks, in seconds.</summary>
        public float LookAheadSeconds => m_lookAheadSeconds;

        /// <summary>True when objects in view should be loaded before objects behind the player.</summary>
        public bool PrioritisesWhatTheCameraSees => m_prioritiseWhatTheCameraSees;

        /// <summary>
        /// The camera used for the view test, when there is one on this object or below it. Looked up once
        /// per enable: a miss is remembered, because a search that fails is the expensive one and this is
        /// read every frame. A camera added later is picked up when the target is next enabled.
        /// </summary>
        public Camera ViewCamera
        {
            get
            {
                if (_cameraSearched)
                {
                    return _camera;
                }

                _cameraSearched = true;
                _camera = GetComponentInChildren<Camera>();

                return _camera;
            }
        }

        /// <summary>Current world position of the target.</summary>
        public Vector3 Position => transform.position;

        /// <summary>Speed and direction measured over the last frames, used to look ahead.</summary>
        public Vector3 Velocity => _velocity;

        internal void SampleMovement(float deltaTime)
        {
            Vector3 current = transform.position;

            if (deltaTime <= 0f)
            {
                _previousPosition = current;
                return;
            }

            Vector3 instantVelocity = (current - _previousPosition) / deltaTime;
            _previousPosition = current;

            if (instantVelocity.sqrMagnitude > TeleportSpeedSquared)
            {
                _velocity = Vector3.zero;
                return;
            }

            _velocity = Vector3.Lerp(_velocity, instantVelocity, VelocitySmoothing);
        }

        #endregion

        #region Privates

        private const float VelocitySmoothing = 0.25f;
        private const float TeleportSpeedSquared = 500f * 500f;

        private Vector3 _previousPosition;
        private Vector3 _velocity;
        private Camera _camera;
        private bool _cameraSearched;

        #endregion
    }
}
