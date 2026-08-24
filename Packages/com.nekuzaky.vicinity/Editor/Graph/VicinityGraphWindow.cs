using System.Collections.Generic;
using Nekuzaky.Vicinity.Graph;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    internal sealed class VicinityGraphWindow : EditorWindow
    {
        #region Unity API

        private void OnDisable()
        {
            _view = null;
        }

        #endregion

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
            if (EditorUtility.InstanceIDToObject(instanceId) is not VicinityGraphAsset asset)
            {
                return false;
            }

            Open(asset);
            return true;
        }

        internal static void Open(VicinityGraphAsset asset)
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
            window.Load(asset);
            window.Show();
        }

        #endregion

        #region Privates

        private const string StylePath = "Packages/com.nekuzaky.vicinity/Editor/UI/VicinityGraph.uss";
        private const float DefaultSampleSize = 4f;
        private const float DefaultSampleMemory = 8f;

        [SerializeField] private VicinityGraphAsset m_asset;
        [SerializeField] private float m_sampleSize = DefaultSampleSize;
        [SerializeField] private float m_sampleMemory = DefaultSampleMemory;
        [SerializeField] private bool m_sampleTagMatch;

        private VicinityGraphView _view;
        private Label _status;

        private void Load(VicinityGraphAsset asset)
        {
            m_asset = asset;
            Rebuild();
        }

        private void CreateGUI()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();

            if (m_asset == null)
            {
                rootVisualElement.Add(new Label("Open a Vicinity graph from the Project window."));
                return;
            }

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);

            if (style != null)
            {
                rootVisualElement.styleSheets.Add(style);
            }

            rootVisualElement.Add(BuildToolbar());

            _view = new VicinityGraphView(m_asset, typeof(ResidencyRuleNode));
            _view.style.flexGrow = 1f;
            _view.Changed += OnGraphChanged;

            rootVisualElement.Add(_view);

            _status = new Label(string.Empty);
            _status.AddToClassList("vicinity-graph__status");
            rootVisualElement.Add(_status);

            OnGraphChanged();
        }

        private VisualElement BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("vicinity-graph__toolbar");

            Label name = new Label(m_asset.name);
            name.AddToClassList("vicinity-graph__name");
            toolbar.Add(name);

            toolbar.Add(new ToolbarSpacer { flex = true });

            Label sample = new Label("Preview an object of");
            sample.AddToClassList("vicinity-graph__hint");
            toolbar.Add(sample);

            FloatField size = new FloatField("m") { value = m_sampleSize };
            size.AddToClassList("vicinity-graph__sample");
            size.RegisterValueChangedCallback(evt =>
            {
                m_sampleSize = evt.newValue;
                OnGraphChanged();
            });

            toolbar.Add(size);

            FloatField memory = new FloatField("MB") { value = m_sampleMemory };
            memory.AddToClassList("vicinity-graph__sample");
            memory.RegisterValueChangedCallback(evt =>
            {
                m_sampleMemory = evt.newValue;
                OnGraphChanged();
            });

            toolbar.Add(memory);

            ToolbarToggle tagged = new ToolbarToggle { text = "Tagged", value = m_sampleTagMatch };
            tagged.RegisterValueChangedCallback(evt =>
            {
                m_sampleTagMatch = evt.newValue;
                OnGraphChanged();
            });

            toolbar.Add(tagged);
            toolbar.Add(VicinityDocs.Link("Manual", DocPage.ResidencyGraph));

            return toolbar;
        }

        private void OnGraphChanged()
        {
            if (_view == null || m_asset == null)
            {
                return;
            }

            ObjectFacts facts = new ObjectFacts
            {
                SizeMeters = m_sampleSize,
                MemoryMegabytes = m_sampleMemory,
                TagMatch = m_sampleTagMatch ? 1f : 0f
            };

            Dictionary<string, float> values = GraphPreview.Evaluate(m_asset, facts);
            _view.RefreshPreviews(node => values.TryGetValue(node.Id, out float value) ? $"{value:0.##}" : string.Empty);

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (m_asset is not ResidencyGraphAsset residency)
            {
                _status.text = string.Empty;
                return;
            }

            CompiledResidencyRules compiled = residency.Compile();

            if (compiled.IsValid)
            {
                ResolvedRule fallback = new ResolvedRule
                {
                    LoadDistance = ResidencySettings.DefaultLoadDistance,
                    ReleaseDistance = ResidencySettings.DefaultUnloadDistance,
                    PriorityScale = 1f
                };

                ObjectFacts facts = new ObjectFacts
                {
                    SizeMeters = m_sampleSize,
                    MemoryMegabytes = m_sampleMemory,
                    TagMatch = m_sampleTagMatch ? 1f : 0f
                };

                ResolvedRule rule = compiled.Evaluate(facts, fallback);

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
