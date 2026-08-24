using Nekuzaky.Vicinity.Editor.Graph;
using Nekuzaky.Vicinity.Graph;
using Nekuzaky.Vicinity.GraphProcessor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    /// <summary>
    /// Checks that the graph window assembles itself the way the base window expects. The base class looks
    /// for the canvas among the root's children and only complains in the console when it is missing, so
    /// nothing but a test like this catches a window that opens empty.
    /// </summary>
    internal sealed class GraphWindowTests
    {
        #region Main Methods

        [Test]
        public void OpeningAGraphPutsTheCanvasInTheWindow()
        {
            OpenWindow();

            Assert.IsNotNull(_window.rootVisualElement.Q<BaseGraphView>(),
                "the base window finds the canvas among the root's children; without it the window comes up empty");
        }

        [Test]
        public void OpeningAGraphPutsTheStatusLineUnderTheCanvas()
        {
            OpenWindow();

            VisualElement root = _window.rootVisualElement;
            VisualElement canvas = root.Q<BaseGraphView>();
            VisualElement status = root.Q<Label>(className: "vicinity-graph__status");

            Assert.IsNotNull(status, "the line that says what the graph would do must be there");
            Assert.Greater(status.parent.IndexOf(status), status.parent.IndexOf(canvas),
                "the status line reads as a footer, so it must come after the canvas");
        }

        [Test]
        public void OpeningTheSameGraphTwiceDoesNotStackCanvases()
        {
            OpenWindow();
            VicinityGraphWindow.Open(_graph);

            Assert.AreEqual(1, CountOf<BaseGraphView>(_window.rootVisualElement),
                "a second open must reuse the canvas rather than leave two behind");
        }

        #endregion

        #region Privates

        private ResidencyGraphAsset _graph;
        private VicinityGraphWindow _window;

        [TearDown]
        public void CloseWindow()
        {
            if (_window != null)
            {
                _window.Close();
                Object.DestroyImmediate(_window);
                _window = null;
            }

            if (_graph != null)
            {
                Object.DestroyImmediate(_graph);
                _graph = null;
            }
        }

        private void OpenWindow()
        {
            _graph = ResidencyGraphAsset.CreateStartingPoint();

            VicinityGraphWindow.Open(_graph);
            _window = EditorWindow.GetWindow<VicinityGraphWindow>();

            Assert.IsNotNull(_window, "the window did not open at all");
        }

        private static int CountOf<TElement>(VisualElement root) where TElement : VisualElement
        {
            int found = 0;

            foreach (VisualElement child in root.Children())
            {
                if (child is TElement)
                {
                    found++;
                }
            }

            return found;
        }

        #endregion
    }
}
