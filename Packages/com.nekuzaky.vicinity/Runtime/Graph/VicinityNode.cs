using System;
using UnityEngine;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>Marks a field as an input socket. The graph fills it from whatever is wired into it.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GraphInputAttribute : Attribute
    {
        /// <summary>Creates an input socket, optionally labelled differently from the field name.</summary>
        public GraphInputAttribute(string label = null)
        {
            Label = label;
        }

        /// <summary>The name shown on the socket, or null to use the field name.</summary>
        public string Label { get; }
    }

    /// <summary>Marks a field as an output socket. The graph reads it after the node has run.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GraphOutputAttribute : Attribute
    {
        /// <summary>Creates an output socket, optionally labelled differently from the field name.</summary>
        public GraphOutputAttribute(string label = null)
        {
            Label = label;
        }

        /// <summary>The name shown on the socket, or null to use the field name.</summary>
        public string Label { get; }
    }

    /// <summary>Places a node in the creation menu under the given path.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GraphNodeMenuAttribute : Attribute
    {
        /// <summary>Creates a menu entry, for example "Distance/Distance To Player".</summary>
        public GraphNodeMenuAttribute(string path)
        {
            Path = path;
        }

        /// <summary>Slash-separated path shown in the node creation window.</summary>
        public string Path { get; }
    }

    /// <summary>
    /// One box in a Vicinity graph. Derive from this, mark fields with
    /// <see cref="GraphInputAttribute"/> and <see cref="GraphOutputAttribute"/>, and implement
    /// <see cref="Process"/>. Everything else is handled for you.
    /// </summary>
    [Serializable]
    public abstract class VicinityNode
    {
        #region Exposed

        [SerializeField]
        [HideInInspector]
        private string m_id;

        [SerializeField]
        [HideInInspector]
        private Vector2 m_position;

        #endregion

        #region Main Methods

        /// <summary>Stable identity of this node inside its graph.</summary>
        public string Id => m_id;

        /// <summary>Where the node sits on the canvas.</summary>
        public Vector2 Position
        {
            get => m_position;
            set => m_position = value;
        }

        /// <summary>The name shown on the node's header.</summary>
        public abstract string Title { get; }

        /// <summary>One sentence explaining what this node does, shown as a tooltip.</summary>
        public virtual string Summary => string.Empty;

        /// <summary>Reads the inputs and writes the outputs. Called once per graph run.</summary>
        public abstract void Process();

        /// <summary>Gives this node an identity if it has none. Called when it enters a graph.</summary>
        public void EnsureIdentity()
        {
            if (string.IsNullOrEmpty(m_id))
            {
                m_id = Guid.NewGuid().ToString("N");
            }
        }

        #endregion
    }
}
