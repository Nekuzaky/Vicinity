using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nekuzaky.Vicinity.Graph
{
    /// <summary>A wire between one node's output socket and another node's input socket.</summary>
    [Serializable]
    public struct NodeEdge : IEquatable<NodeEdge>
    {
        [SerializeField] private string m_fromNodeId;
        [SerializeField] private string m_fromPort;
        [SerializeField] private string m_toNodeId;
        [SerializeField] private string m_toPort;

        /// <summary>Creates a wire between two sockets.</summary>
        public NodeEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            m_fromNodeId = fromNodeId;
            m_fromPort = fromPort;
            m_toNodeId = toNodeId;
            m_toPort = toPort;
        }

        /// <summary>Identity of the node the value comes from.</summary>
        public readonly string FromNodeId => m_fromNodeId;

        /// <summary>Output socket the value comes from.</summary>
        public readonly string FromPort => m_fromPort;

        /// <summary>Identity of the node the value goes to.</summary>
        public readonly string ToNodeId => m_toNodeId;

        /// <summary>Input socket the value goes to.</summary>
        public readonly string ToPort => m_toPort;

        /// <inheritdoc />
        public readonly bool Equals(NodeEdge other)
        {
            return m_fromNodeId == other.m_fromNodeId
                && m_fromPort == other.m_fromPort
                && m_toNodeId == other.m_toNodeId
                && m_toPort == other.m_toPort;
        }

        /// <inheritdoc />
        public readonly override bool Equals(object obj) => obj is NodeEdge other && Equals(other);

        /// <inheritdoc />
        public readonly override int GetHashCode()
        {
            return HashCode.Combine(m_fromNodeId, m_fromPort, m_toNodeId, m_toPort);
        }
    }

    /// <summary>
    /// A graph saved as an asset. Vicinity ships two kinds: one that decides what stays in memory,
    /// and one that bakes the model used at a distance.
    /// </summary>
    public abstract class VicinityGraphAsset : ScriptableObject
    {
        #region Exposed

        [SerializeReference]
        [HideInInspector]
        private List<VicinityNode> m_nodes = new List<VicinityNode>();

        [SerializeField]
        [HideInInspector]
        private List<NodeEdge> m_edges = new List<NodeEdge>();

        #endregion

        #region Main Methods

        /// <summary>Every node in this graph.</summary>
        public IReadOnlyList<VicinityNode> Nodes => m_nodes;

        /// <summary>Every wire in this graph.</summary>
        public IReadOnlyList<NodeEdge> Edges => m_edges;

        /// <summary>Adds a node and gives it an identity.</summary>
        public void Add(VicinityNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            node.EnsureIdentity();
            m_nodes.Add(node);
        }

        /// <summary>Removes a node together with every wire that touched it.</summary>
        public void Remove(VicinityNode node)
        {
            if (node == null || !m_nodes.Remove(node))
            {
                return;
            }

            for (int i = m_edges.Count - 1; i >= 0; i--)
            {
                if (m_edges[i].FromNodeId == node.Id || m_edges[i].ToNodeId == node.Id)
                {
                    m_edges.RemoveAt(i);
                }
            }
        }

        /// <summary>Returns the node with this identity, or null.</summary>
        public VicinityNode Find(string nodeId)
        {
            for (int i = 0; i < m_nodes.Count; i++)
            {
                if (m_nodes[i] != null && m_nodes[i].Id == nodeId)
                {
                    return m_nodes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Wires an output socket to an input socket. An input takes a single wire, so connecting a
        /// second one replaces the first. Returns false when the sockets do not exist or do not match.
        /// </summary>
        public bool Connect(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            if (!CanConnect(fromNodeId, fromPort, toNodeId, toPort))
            {
                return false;
            }

            DisconnectInput(toNodeId, toPort);
            m_edges.Add(new NodeEdge(fromNodeId, fromPort, toNodeId, toPort));
            return true;
        }

        /// <summary>True when these two sockets exist and carry compatible values.</summary>
        public bool CanConnect(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            if (fromNodeId == toNodeId)
            {
                return false;
            }

            VicinityNode source = Find(fromNodeId);
            VicinityNode target = Find(toNodeId);

            if (source == null || target == null)
            {
                return false;
            }

            if (!NodePortLayout.For(source.GetType()).TryGetOutput(fromPort, out NodePort output))
            {
                return false;
            }

            if (!NodePortLayout.For(target.GetType()).TryGetInput(toPort, out NodePort input))
            {
                return false;
            }

            return input.ValueType.IsAssignableFrom(output.ValueType);
        }

        /// <summary>Removes a specific wire.</summary>
        public void Disconnect(NodeEdge edge)
        {
            m_edges.Remove(edge);
        }

        /// <summary>Removes whatever is wired into an input socket.</summary>
        public void DisconnectInput(string toNodeId, string toPort)
        {
            for (int i = m_edges.Count - 1; i >= 0; i--)
            {
                if (m_edges[i].ToNodeId == toNodeId && m_edges[i].ToPort == toPort)
                {
                    m_edges.RemoveAt(i);
                }
            }
        }

        /// <summary>Drops nodes that failed to deserialize, and wires that lead nowhere.</summary>
        public void RemoveBrokenParts()
        {
            for (int i = m_nodes.Count - 1; i >= 0; i--)
            {
                if (m_nodes[i] == null)
                {
                    m_nodes.RemoveAt(i);
                }
            }

            for (int i = m_edges.Count - 1; i >= 0; i--)
            {
                if (Find(m_edges[i].FromNodeId) == null || Find(m_edges[i].ToNodeId) == null)
                {
                    m_edges.RemoveAt(i);
                }
            }
        }

        #endregion
    }
}
