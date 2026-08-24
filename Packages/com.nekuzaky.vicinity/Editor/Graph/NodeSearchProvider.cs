using System;
using System.Collections.Generic;
using System.Reflection;
using Nekuzaky.Vicinity.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    internal sealed class NodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        #region Main Methods

        internal void Prepare(Type nodeBaseType, Action<Type, Vector2> onNodeChosen)
        {
            _nodeBaseType = nodeBaseType;
            _onNodeChosen = onNodeChosen;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Add a node"), 0)
            };

            List<NodeEntry> entries = CollectNodes();
            entries.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));

            string currentGroup = string.Empty;

            foreach (NodeEntry entry in entries)
            {
                string group = entry.Group;

                if (group.Length > 0 && group != currentGroup)
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(group), 1));
                    currentGroup = group;
                }

                tree.Add(new SearchTreeEntry(new GUIContent(entry.Name))
                {
                    level = group.Length > 0 ? 2 : 1,
                    userData = entry.NodeType
                });
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is not Type nodeType)
            {
                return false;
            }

            _onNodeChosen?.Invoke(nodeType, context.screenMousePosition);
            return true;
        }

        #endregion

        #region Privates

        private Type _nodeBaseType;
        private Action<Type, Vector2> _onNodeChosen;

        private readonly struct NodeEntry
        {
            internal NodeEntry(Type nodeType, string path)
            {
                NodeType = nodeType;
                Path = path;
            }

            internal Type NodeType { get; }

            internal string Path { get; }

            internal string Group
            {
                get
                {
                    int separator = Path.LastIndexOf('/');
                    return separator < 0 ? string.Empty : Path.Substring(0, separator);
                }
            }

            internal string Name
            {
                get
                {
                    int separator = Path.LastIndexOf('/');
                    return separator < 0 ? Path : Path.Substring(separator + 1);
                }
            }
        }

        private List<NodeEntry> CollectNodes()
        {
            List<NodeEntry> entries = new List<NodeEntry>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type.IsAbstract || !_nodeBaseType.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    GraphNodeMenuAttribute menu = type.GetCustomAttribute<GraphNodeMenuAttribute>();
                    entries.Add(new NodeEntry(type, menu != null ? menu.Path : type.Name));
                }
            }

            return entries;
        }

        #endregion
    }
}
