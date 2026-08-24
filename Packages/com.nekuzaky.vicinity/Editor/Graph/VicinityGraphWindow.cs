using Nekuzaky.Vicinity.Graph;
using Nekuzaky.Vicinity.GraphProcessor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    /// <summary>
    /// The residency graph editor. The canvas, the node creation menu and the selection handling come from
    /// NodeGraphProcessor; what Vicinity adds is the sample object in the toolbar and the status line that
    /// says, in words, what the graph would do to it.
    /// </summary>
    internal sealed class VicinityGraphWindow : BaseGraphWindow
    {
        #region Main Methods

        [MenuItem("Tools/Vicinity/Residency Graph", false, 1)]
        internal static void OpenLastGraph()
        {
            string[] found = AssetDatabase.FindAssets("t:ResidencyGraphAsset");

            if (found.Length == 0)
            {
                bool create = EditorUtility.DisplayDialog(
                    "Vicinity",
                    "This project has no residency graph yet. A graph decides, per object, how close the player must be before it loads.",
                    "Make me one",
                    "Not now");

                if (create)
                {
                    Open(ResidencyGraphCreation.CreateAt("Assets/Residency Graph.asset"));
                }

                return;
            }

            Open(AssetDatabase.LoadAssetAtPath<ResidencyGraphAsset>(AssetDatabase.GUIDToAssetPath(found[0])));
        }

        [OnOpenAsset]
        internal static bool OpenFromProject(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not ResidencyGraphAsset asset)
            {
                return false;
            }

            Open(asset);
            return true;
        }

        internal static void Open(ResidencyGraphAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            // An asset saved with no nodes would open on a blank canvas under a red error. Fill it in first.
            ResidencyGraphCreation.SeedIfEmpty(asset);

            VicinityGraphWindow window = GetWindow<VicinityGraphWindow>();
            window.titleContent = new GUIContent(asset.name);
            window.minSize = new Vector2(720f, 480f);
            window.InitializeGraph(asset);
            window.Show();
        }

        #endregion

        #region Unity API

        /// <inheritdoc />
        protected override void InitializeWindow(BaseGraph graph)
        {
            titleContent = new GUIContent(graph != null ? graph.name : "Residency Graph");

            if (graphView == null)
            {
                graphView = new BaseGraphView(this);
                graphView.Add(BuildToolbar());

                // Groups and sticky notes are in the canvas' own context menu; the minimap is not, and
                // has to be asked for.
                graphView.Add(new MiniMapView(graphView));
            }

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);

            if (style != null && !rootView.styleSheets.Contains(style))
            {
                rootView.styleSheets.Add(style);
            }

            // The base window looks for the graph view among the root's children, so adding it is not
            // optional: without this the window comes up empty with an error in the console.
            graphView.style.flexGrow = 1f;
            rootView.Add(graphView);

            _status ??= BuildStatusLine();

            // Adding an element that already has a parent moves it, which keeps the status line last
            // however many times this window is rebuilt.
            rootView.Add(_status);
        }

        /// <inheritdoc />
        protected override void InitializeGraphView(BaseGraphView view)
        {
            view.initialized += RefreshPreview;

            if (!_refreshScheduled)
            {
                _refreshScheduled = true;

                // Adding or removing a node raises an event, but typing a number into one does not. A graph
                // this small recompiles for nothing, so a steady refresh beats chasing every edit.
                rootView.schedule.Execute(RefreshPreview).Every(RefreshMilliseconds);
            }

            RefreshPreview();
        }

        #endregion

        #region Privates

        private const string StylePath = "Packages/com.nekuzaky.vicinity/Editor/UI/VicinityGraph.uss";
        private const float DefaultSampleSize = 4f;
        private const float DefaultSampleMemory = 8f;
        private const long RefreshMilliseconds = 250L;

        [SerializeField] private float m_sampleSize = DefaultSampleSize;
        [SerializeField] private float m_sampleMemory = DefaultSampleMemory;
        [SerializeField] private bool m_sampleTagMatch;

        private Label _status;
        private bool _refreshScheduled;

        private VisualElement BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("vicinity-graph__toolbar");

            Label sample = new Label("Preview an object of");
            sample.AddToClassList("vicinity-graph__hint");
            toolbar.Add(sample);

            FloatField size = new FloatField("m") { value = m_sampleSize };
            size.AddToClassList("vicinity-graph__sample");
            size.RegisterValueChangedCallback(evt =>
            {
                m_sampleSize = evt.newValue;
                RefreshPreview();
            });

            toolbar.Add(size);

            FloatField memory = new FloatField("MB") { value = m_sampleMemory };
            memory.AddToClassList("vicinity-graph__sample");
            memory.RegisterValueChangedCallback(evt =>
            {
                m_sampleMemory = evt.newValue;
                RefreshPreview();
            });

            toolbar.Add(memory);

            ToolbarToggle tagged = new ToolbarToggle { text = "Tagged", value = m_sampleTagMatch };
            tagged.RegisterValueChangedCallback(evt =>
            {
                m_sampleTagMatch = evt.newValue;
                RefreshPreview();
            });

            toolbar.Add(tagged);
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(VicinityDocs.Link("Manual", DocPage.ResidencyGraph));

            return toolbar;
        }

        private Label BuildStatusLine()
        {
            _status = new Label(string.Empty);
            _status.AddToClassList("vicinity-graph__status");

            return _status;
        }

        private ObjectFacts SampleFacts()
        {
            return new ObjectFacts
            {
                SizeMeters = m_sampleSize,
                MemoryMegabytes = m_sampleMemory,
                TagMatch = m_sampleTagMatch ? 1f : 0f
            };
        }

        private void RefreshPreview()
        {
            if (graph is not ResidencyGraphAsset residency || _status == null)
            {
                return;
            }

            // Running the preview leaves each node holding the value it would produce, which is what the
            // canvas draws. The dictionary it returns is not needed here.
            GraphPreview.Evaluate(residency, SampleFacts());

            RefreshStatus(residency);
        }

        private void RefreshStatus(ResidencyGraphAsset residency)
        {
            CompiledResidencyRules compiled = residency.Compile();

            if (compiled.IsValid)
            {
                ResolvedRule fallback = new ResolvedRule
                {
                    LoadDistance = ResidencySettings.DefaultLoadDistance,
                    ReleaseDistance = ResidencySettings.DefaultUnloadDistance,
                    PriorityScale = 1f
                };

                ResolvedRule rule = compiled.Evaluate(SampleFacts(), fallback);

                _status.RemoveFromClassList("vicinity-graph__status--broken");
                _status.text =
                    $"This object would load at {rule.LoadDistance:0.#} m, be released at {rule.ReleaseDistance:0.#} m, " +
                    $"priority x{rule.PriorityScale:0.##}   ·   {compiled.InstructionCount} instructions";
            }
            else
            {
                _status.AddToClassList("vicinity-graph__status--broken");
                _status.text = compiled.Problem;
            }

            compiled.Dispose();
        }

        #endregion
    }
}
