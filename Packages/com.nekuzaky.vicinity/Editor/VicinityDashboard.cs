using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor
{
    internal sealed class VicinityDashboard : EditorWindow
    {
        #region Unity API

        private void CreateGUI()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);

            if (layout == null)
            {
                rootVisualElement.Add(new Label("Vicinity could not load its own interface files. Reimport the package."));
                return;
            }

            layout.CloneTree(rootVisualElement);

            if (style != null)
            {
                rootVisualElement.styleSheets.Add(style);
            }

            _content = rootVisualElement.Q<ScrollView>("content");
            _tabs = new[]
            {
                rootVisualElement.Q<Button>("tab-setup"),
                rootVisualElement.Q<Button>("tab-validation"),
                rootVisualElement.Q<Button>("tab-live")
            };

            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabs[i].clicked += () => SelectTab(index);
            }

            BuildDocsLink();

            Toggle gizmoToggle = rootVisualElement.Q<Toggle>("gizmo-toggle");
            gizmoToggle.SetValueWithoutNotify(VicinityEditorStyles.GizmosVisible);
            gizmoToggle.RegisterValueChangedCallback(OnGizmoToggleChanged);

            Toggle sceneDropToggle = rootVisualElement.Q<Toggle>("scene-drop-toggle");
            sceneDropToggle.tooltip =
                "Drag a model from the Project window into a scene that has a Vicinity Manager, and it is placed as a managed object. Without this you place the model as it is.";

            sceneDropToggle.SetValueWithoutNotify(VicinitySceneDropHandler.Enabled);
            sceneDropToggle.RegisterValueChangedCallback(static evt => VicinitySceneDropHandler.Enabled = evt.newValue);

            BuildFooter();
            SelectTab(_activeTab);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void Update()
        {
            if (_activeTab != LiveTabIndex || !EditorApplication.isPlaying)
            {
                return;
            }

            SampleLiveStatistics();
            Repaint();
            RefreshLiveValues();
        }

        #endregion

        #region Main Methods

        [MenuItem("Tools/Vicinity/Dashboard", false, 0)]
        internal static void Open()
        {
            VicinityDashboard window = GetWindow<VicinityDashboard>();
            window.titleContent = new GUIContent("Vicinity");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        [MenuItem("GameObject/Vicinity/Volume", false, 10)]
        private static void CreateVolumeFromMenu()
        {
            VicinitySceneSetup.CreateVolume();
        }

        #endregion

        #region Privates

        private const string PackageRoot = "Packages/com.nekuzaky.vicinity/Editor/UI/";
        private const string LayoutPath = PackageRoot + "VicinityDashboard.uxml";
        private const string StylePath = PackageRoot + "VicinityDashboard.uss";
        private const int LiveTabIndex = 2;
        private const int MemorySampleCount = 300;
        private const string ActiveTabClass = "vicinity-tab--active";
        private const string PatreonUrl = "https://www.patreon.com/Nekuzaky";
        private const string CoffeeUrl = "https://www.buymeacoffee.com/nekuzaky";

        private ScrollView _content;
        private Button[] _tabs;
        private int _activeTab;

        private List<ScanCandidate> _candidates;
        private List<PrefabConversion> _conversions;
        private MemoryGraph _memoryGraph;
        private Label _liveSummary;
        private Label _liveExclusions;
        private readonly Dictionary<string, Label> _liveValues = new Dictionary<string, Label>();
        private readonly List<ResidencySample> _samples = new List<ResidencySample>();

        private void BuildDocsLink()
        {
            VisualElement tabBar = rootVisualElement.Q<VisualElement>("tab-bar");

            if (tabBar == null)
            {
                return;
            }

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;

            tabBar.Add(spacer);
            tabBar.Add(VicinityDocs.Link("Documentation", DocPage.Home));
        }

        private void BuildFooter()
        {
            VisualElement footer = rootVisualElement.Q<VisualElement>("footer");

            if (footer == null)
            {
                return;
            }

            Image icon = new Image { image = VicinityEditorStyles.SupportIcon };
            icon.AddToClassList("vicinity-footer__icon");
            footer.Add(icon);

            Label text = new Label("Free for noncommercial use. If Vicinity saved you time:");
            text.AddToClassList("vicinity-footer__text");
            footer.Add(text);

            footer.Add(BuildSupportLink("Patreon", PatreonUrl));
            footer.Add(BuildSupportLink("Buy me a coffee", CoffeeUrl));
        }

        private static Button BuildSupportLink(string label, string url)
        {
            Button link = new Button(() => Application.OpenURL(url)) { text = label, tooltip = url };
            link.AddToClassList("vicinity-footer__link");
            return link;
        }

        private void OnGizmoToggleChanged(ChangeEvent<bool> evt)
        {
            VicinityEditorStyles.GizmosVisible = evt.newValue;
            SceneView.RepaintAll();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _samples.Clear();
            }

            SelectTab(_activeTab);
        }

        private void SelectTab(int index)
        {
            _activeTab = index;

            if (_tabs == null || _content == null)
            {
                return;
            }

            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].EnableInClassList(ActiveTabClass, i == index);
            }

            _content.Clear();
            _liveValues.Clear();

            switch (index)
            {
                case 0:
                    BuildSetupTab();
                    break;

                case 1:
                    BuildValidationTab();
                    break;

                default:
                    BuildLiveTab();
                    break;
            }
        }

        private void BuildSetupTab()
        {
            _content.Add(Section("Take prefabs over"));

            VicinityDropZone drop = new VicinityDropZone();
            drop.Converted += OnPrefabsConverted;
            _content.Add(drop);

            BuildSelectionShortcut(drop);
            BuildConversionResults();

            _content.Add(Section("Or set up this whole scene"));

            Button oneClick = new Button(SetUpSceneFromDashboard) { text = "Set up this scene" };
            oneClick.AddToClassList("vicinity-hero-button");
            _content.Add(oneClick);

            _content.Add(Hint("Adds a manager and a viewpoint if this scene has none, then hands every object that draws something over to Vicinity. One undo takes it all back."));

            BuildProjectChecks();
            BuildManualSelection();
        }

        private void BuildProjectChecks()
        {
            List<ProjectCheck> checks = VicinityProjectChecks.Collect();
            int satisfied = 0;

            foreach (ProjectCheck check in checks)
            {
                if (check.IsSatisfied)
                {
                    satisfied++;
                }
            }

            _content.Add(Section("Project configuration"));

            if (satisfied == checks.Count)
            {
                _content.Add(Hint("Nothing to fix in this project."));
            }

            foreach (ProjectCheck check in checks)
            {
                if (!check.IsSatisfied)
                {
                    _content.Add(BuildCheckRow(check));
                }
            }

            if (satisfied == 0)
            {
                return;
            }

            Foldout settled = new Foldout
            {
                text = $"{satisfied} already fine",
                value = false
            };

            settled.AddToClassList("vicinity-foldout");

            foreach (ProjectCheck check in checks)
            {
                if (check.IsSatisfied)
                {
                    settled.Add(BuildCheckRow(check));
                }
            }

            _content.Add(settled);
        }

        private void BuildManualSelection()
        {
            _content.Add(Section("Or choose the objects yourself"));

            VisualElement actions = new VisualElement();
            actions.AddToClassList("vicinity-primary-actions");

            Button scan = new Button(RunScan) { text = "Scan Scene" };
            Button apply = new Button(ApplyScan) { text = "Apply to selected" };
            apply.SetEnabled(_candidates != null && _candidates.Count > 0);

            actions.Add(scan);
            actions.Add(apply);
            _content.Add(actions);

            if (_candidates == null)
            {
                _content.Add(Hint("Scan to list what Vicinity could take over, heaviest first."));
                return;
            }

            if (_candidates.Count == 0)
            {
                _content.Add(Hint("Nothing to manage in this scene. Vicinity looks for objects that draw something."));
                return;
            }

            _content.Add(BuildCandidateList());
        }

        private void SetUpSceneFromDashboard()
        {
            SetupResult result = VicinitySceneSetup.SetUpScene(false);

            _candidates = VicinitySceneScanner.Scan();
            SelectTab(0);

            string message = result.Equipped == 0
                ? "Nothing new to manage."
                : $"{result.Equipped} objects are now managed, {VicinityEditorStyles.DescribeBytes(result.TotalBytes)} of models.";

            ShowNotification(new GUIContent(message));
        }

        private VisualElement BuildCandidateList()
        {
            VisualElement container = new VisualElement();
            long total = 0L;
            int selected = 0;

            foreach (ScanCandidate candidate in _candidates)
            {
                container.Add(BuildCandidateRow(candidate));

                if (candidate.Selected)
                {
                    total += candidate.EstimatedBytes;
                    selected++;
                }
            }

            Label summary = new Label($"{selected} of {_candidates.Count} selected, {VicinityEditorStyles.DescribeBytes(total)} of models.");
            summary.AddToClassList("vicinity-empty");
            container.Insert(0, summary);

            return container;
        }

        private VisualElement BuildCandidateRow(ScanCandidate candidate)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("vicinity-candidate-row");

            Toggle toggle = new Toggle { value = candidate.Selected };
            toggle.RegisterValueChangedCallback(evt => candidate.Selected = evt.newValue);
            row.Add(toggle);

            VisualElement text = new VisualElement();
            text.AddToClassList("vicinity-row__text");

            Label title = new Label(candidate.DisplayName);
            title.AddToClassList("vicinity-row__title");
            text.Add(title);

            string status = candidate.WasEditedByHand
                ? "already set up by hand, left untouched unless you allow overwriting"
                : candidate.AlreadyManaged
                    ? "already managed, will be refreshed"
                    : "not managed yet";

            Label detail = new Label($"{VicinityEditorStyles.DescribeBytes(candidate.EstimatedBytes)} — {status}");
            detail.AddToClassList("vicinity-row__detail");
            text.Add(detail);

            row.Add(text);

            Button select = new Button(() => Selection.activeGameObject = candidate.Target) { text = "Select" };
            select.AddToClassList("vicinity-row__action");
            row.Add(select);

            return row;
        }

        private void RunScan()
        {
            _candidates = VicinitySceneScanner.Scan();
            SelectTab(0);
        }

        private void ApplyScan()
        {
            if (_candidates == null)
            {
                return;
            }

            int handEdited = VicinitySceneScanner.CountHandEdited(_candidates);
            bool overwrite = false;

            if (handEdited > 0)
            {
                overwrite = EditorUtility.DisplayDialog(
                    "Objects configured by hand",
                    $"{handEdited} of the selected objects were set up by hand. Overwrite their settings, or leave them exactly as they are?",
                    "Overwrite them",
                    "Leave them alone");
            }

            Undo.SetCurrentGroupName("Apply Vicinity to scene");
            int group = Undo.GetCurrentGroup();

            VicinityManager manager = VicinitySceneSetup.EnsureManager();
            int changed = VicinitySceneScanner.Apply(_candidates, overwrite);

            if (VicinitySceneSetup.FindTarget() == null)
            {
                VicinitySceneSetup.CreateTarget();
            }

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = manager.gameObject;

            _candidates = VicinitySceneScanner.Scan();
            SelectTab(0);

            ShowNotification(new GUIContent($"{changed} objects are now managed by Vicinity."));
        }

        private void BuildValidationTab()
        {
            List<ValidationIssue> issues = VicinityValidation.Collect();

            _content.Add(new Button(() => SelectTab(1)) { text = "Check again" });

            if (issues.Count == 0)
            {
                _content.Add(Hint("Nothing wrong in this scene."));
                return;
            }

            _content.Add(Section($"{issues.Count} thing{(issues.Count == 1 ? "" : "s")} to look at"));

            foreach (ValidationIssue issue in issues)
            {
                _content.Add(BuildIssueRow(issue));
            }
        }

        private VisualElement BuildIssueRow(ValidationIssue issue)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("vicinity-row");

            row.AddToClassList(issue.Severity switch
            {
                IssueSeverity.Error => "vicinity-row--error",
                IssueSeverity.Warning => "vicinity-row--warn",
                _ => "vicinity-row--ok"
            });

            Image badge = new Image
            {
                image = issue.Severity switch
                {
                    IssueSeverity.Error => VicinityEditorStyles.ErrorIcon,
                    IssueSeverity.Warning => VicinityEditorStyles.WarningIcon,
                    _ => VicinityEditorStyles.InfoIcon
                }
            };

            badge.AddToClassList("vicinity-row__icon");
            row.Add(badge);

            VisualElement text = new VisualElement();
            text.AddToClassList("vicinity-row__text");

            Label title = new Label(issue.Title);
            title.AddToClassList("vicinity-row__title");
            text.Add(title);

            Label detail = new Label(issue.Explanation);
            detail.AddToClassList("vicinity-row__detail");
            text.Add(detail);

            row.Add(text);

            if (issue.Context != null)
            {
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.clickCount >= 2)
                    {
                        Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                });
            }

            if (issue.Fix != null)
            {
                Button fix = new Button(() =>
                {
                    issue.Fix.Invoke();
                    SelectTab(1);
                })
                {
                    text = issue.FixLabel ?? "Fix"
                };

                fix.AddToClassList("vicinity-row__action");
                row.Add(fix);
            }

            return row;
        }

        private VisualElement BuildCheckRow(ProjectCheck check)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("vicinity-row");

            row.AddToClassList(check.IsSatisfied ? "vicinity-row--ok" : check.IsAdvisory ? "vicinity-row--warn" : "vicinity-row--error");

            Image badge = new Image
            {
                image = check.IsSatisfied
                    ? null
                    : check.IsAdvisory ? VicinityEditorStyles.WarningIcon : VicinityEditorStyles.ErrorIcon
            };

            badge.AddToClassList("vicinity-row__icon");
            row.Add(badge);

            VisualElement text = new VisualElement();
            text.AddToClassList("vicinity-row__text");

            Label title = new Label(check.Title);
            title.AddToClassList("vicinity-row__title");
            text.Add(title);

            Label detail = new Label(check.Explanation);
            detail.AddToClassList("vicinity-row__detail");
            text.Add(detail);

            row.Add(text);

            if (check.Fix != null)
            {
                Button fix = new Button(() =>
                {
                    check.Fix.Invoke();
                    SelectTab(0);
                })
                {
                    text = check.FixLabel ?? "Fix"
                };

                fix.AddToClassList("vicinity-row__action");
                row.Add(fix);
            }

            return row;
        }

        private void BuildLiveTab()
        {
            if (!EditorApplication.isPlaying)
            {
                _content.Add(Hint("Enter Play Mode to watch what Vicinity is holding in memory."));
                return;
            }

            if (VicinityManager.ActiveManager == null)
            {
                _content.Add(Hint("No Vicinity manager is running in this scene."));
                return;
            }

            _content.Add(Section("Objects"));

            VisualElement grid = new VisualElement();
            grid.AddToClassList("vicinity-stat-grid");

            grid.Add(BuildStat("Managed", "managed"));
            grid.Add(BuildStat("Loaded", "resident"));
            grid.Add(BuildStat("Loading", "loading"));
            grid.Add(BuildStat("Waiting", "queued"));
            grid.Add(BuildStat("Given up", "failed"));

            _content.Add(grid);

            _liveExclusions = new Label(string.Empty);
            _liveExclusions.AddToClassList("vicinity-row__detail");
            _content.Add(_liveExclusions);

            _content.Add(Section("Memory held by loaded objects"));

            _liveSummary = new Label(string.Empty);
            _liveSummary.AddToClassList("vicinity-memory-line");
            _content.Add(_liveSummary);

            _memoryGraph = new MemoryGraph(_samples);
            _memoryGraph.AddToClassList("vicinity-graph");
            _content.Add(_memoryGraph);

            _content.Add(new Button(ExportCsv) { text = "Export these numbers as CSV" });

            RefreshLiveValues();
        }

        private VisualElement BuildStat(string label, string key)
        {
            VisualElement stat = new VisualElement();
            stat.AddToClassList("vicinity-stat");

            Label value = new Label("0");
            value.AddToClassList("vicinity-stat__value");
            stat.Add(value);

            Label caption = new Label(label);
            caption.AddToClassList("vicinity-stat__label");
            stat.Add(caption);

            _liveValues[key] = value;
            return stat;
        }

        private void SampleLiveStatistics()
        {
            VicinityManager manager = VicinityManager.ActiveManager;
            if (manager == null)
            {
                return;
            }

            ResidencyStatistics statistics = manager.Statistics;

            _samples.Add(new ResidencySample
            {
                Frame = Time.frameCount,
                Managed = statistics.Managed,
                Resident = statistics.Resident,
                Loading = statistics.Loading,
                Queued = statistics.Queued,
                Failed = statistics.Failed,
                MemoryBytes = statistics.ResidentMemoryBytes
            });

            while (_samples.Count > MemorySampleCount)
            {
                _samples.RemoveAt(0);
            }
        }

        private void RefreshLiveValues()
        {
            VicinityManager manager = VicinityManager.ActiveManager;
            if (manager == null || _liveValues.Count == 0)
            {
                return;
            }

            ResidencyStatistics statistics = manager.Statistics;

            SetLiveValue("managed", statistics.Managed);
            SetLiveValue("resident", statistics.Resident);
            SetLiveValue("loading", statistics.Loading);
            SetLiveValue("queued", statistics.Queued);
            SetLiveValue("failed", statistics.Failed);

            if (_liveSummary != null)
            {
                float budget = manager.Profile != null ? manager.Profile.MemoryBudgetMegabytes : 0f;
                string budgetText = budget > 0f ? $" of a {budget:0} MB budget" : string.Empty;
                _liveSummary.text = $"{VicinityEditorStyles.DescribeBytes(statistics.ResidentMemoryBytes)}{budgetText}";
            }

            if (_liveExclusions != null)
            {
                int excluded = CountObjectsExcludedFromGpuInstancing();
                _liveExclusions.text = excluded == 0
                    ? $"{statistics.Managed} managed objects, none excluded from the GPU Resident Drawer."
                    : $"{statistics.Managed} managed objects, {excluded} excluded from the GPU Resident Drawer. The Validation tab names each one and why.";
            }

            _memoryGraph?.MarkDirtyRepaint();
        }

        private void SetLiveValue(string key, int value)
        {
            if (_liveValues.TryGetValue(key, out Label label))
            {
                label.text = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static int CountObjectsExcludedFromGpuInstancing()
        {
            int count = 0;

            foreach (ValidationIssue issue in VicinityValidation.Collect())
            {
                if (issue.Title.Contains("excluded from the GPU Resident Drawer", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export Vicinity statistics", string.Empty, "vicinity-session.csv", "csv");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("frame,managed,resident,loading,queued,failed,memory_bytes");

            foreach (ResidencySample sample in _samples)
            {
                builder.Append(sample.Frame).Append(',')
                    .Append(sample.Managed).Append(',')
                    .Append(sample.Resident).Append(',')
                    .Append(sample.Loading).Append(',')
                    .Append(sample.Queued).Append(',')
                    .Append(sample.Failed).Append(',')
                    .Append(sample.MemoryBytes)
                    .AppendLine();
            }

            File.WriteAllText(path, builder.ToString());
            ShowNotification(new GUIContent($"Saved {_samples.Count} frames."));
        }

        private void BuildSelectionShortcut(VicinityDropZone drop)
        {
            int count = VicinityDropZone.CountInSelection();

            if (count == 0)
            {
                return;
            }

            VisualElement actions = new VisualElement();
            actions.AddToClassList("vicinity-primary-actions");

            actions.Add(new Button(drop.ConvertSelection)
            {
                text = count == 1
                    ? "Take over the selected prefab"
                    : $"Take over the {count} selected prefabs"
            });

            _content.Add(actions);
        }

        private void OnPrefabsConverted(List<PrefabConversion> conversions)
        {
            _conversions = conversions;

            List<UnityEngine.Object> produced = new List<UnityEngine.Object>();

            foreach (PrefabConversion conversion in conversions)
            {
                if (conversion.Succeeded)
                {
                    produced.Add(conversion.Result);
                }
            }

            if (produced.Count > 0)
            {
                Selection.objects = produced.ToArray();
                EditorGUIUtility.PingObject(produced[0]);
            }

            ShowNotification(new GUIContent(produced.Count == 1
                ? $"{produced[0].name} is ready to place."
                : $"{produced.Count} prefabs are ready to place."));

            // The drop zone is still handling its own event, so the rebuild that removes it waits a frame.
            rootVisualElement.schedule.Execute(() => SelectTab(0));
        }

        private void BuildConversionResults()
        {
            if (_conversions == null || _conversions.Count == 0)
            {
                _content.Add(Hint("What comes out lands beside the original, named the same with \"(Vicinity)\" after it. Place that one in your scene instead of the original."));
                return;
            }

            int done = 0;
            long total = 0L;
            bool anyDirect = false;

            foreach (PrefabConversion conversion in _conversions)
            {
                if (!conversion.Succeeded)
                {
                    continue;
                }

                done++;
                total += conversion.EstimatedBytes;
                anyDirect |= conversion.Strength == ReferenceStrength.Direct;
            }

            _content.Add(Hint($"{done} of {_conversions.Count} taken over, {VicinityEditorStyles.DescribeBytes(total)} of models. Place the new prefabs instead of the originals."));

            if (anyDirect)
            {
                _content.Add(BuildDirectReferenceWarning());
            }

            foreach (PrefabConversion conversion in _conversions)
            {
                _content.Add(BuildConversionRow(conversion));
            }
        }

        private VisualElement BuildDirectReferenceWarning()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("vicinity-row");
            row.AddToClassList("vicinity-row--warn");

            VisualElement text = new VisualElement();
            text.AddToClassList("vicinity-row__text");

            Label title = new Label("These point straight at their models, so memory will not drop");
            title.AddToClassList("vicinity-row__title");
            text.Add(title);

            Label detail = new Label(
                "Vicinity still shows and hides them, but a scene that names a model directly makes Unity load it anyway. Install Addressables and drop the prefabs again to get the saving.");

            detail.AddToClassList("vicinity-row__detail");
            text.Add(detail);

            row.Add(text);

            // The page this opens is the one that explains the whole trade-off, so it belongs
            // exactly here, next to the warning that raises the question.
            row.Add(VicinityDocs.Link("Why?", DocPage.AssetSources));

            Button install = new Button(static () => UnityEditor.PackageManager.UI.Window.Open("com.unity.addressables"))
            {
                text = "Install Addressables"
            };

            install.AddToClassList("vicinity-row__action");
            row.Add(install);

            return row;
        }

        private VisualElement BuildConversionRow(PrefabConversion conversion)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("vicinity-row");
            row.AddToClassList(conversion.Succeeded ? "vicinity-row--ok" : "vicinity-row--error");

            VisualElement text = new VisualElement();
            text.AddToClassList("vicinity-row__text");

            Label title = new Label(conversion.Succeeded
                ? Path.GetFileNameWithoutExtension(conversion.ResultPath)
                : conversion.SourceName);

            title.AddToClassList("vicinity-row__title");
            text.Add(title);

            Label detail = new Label(conversion.Succeeded ? Describe(conversion) : conversion.Problem);
            detail.AddToClassList("vicinity-row__detail");
            text.Add(detail);

            row.Add(text);

            if (!conversion.Succeeded)
            {
                return row;
            }

            Button reveal = new Button(() =>
            {
                Selection.activeObject = conversion.Result;
                EditorGUIUtility.PingObject(conversion.Result);
            })
            {
                text = "Show in Project"
            };

            reveal.AddToClassList("vicinity-row__action");
            row.Add(reveal);

            return row;
        }

        private static string Describe(PrefabConversion conversion)
        {
            string how = conversion.Strength switch
            {
                ReferenceStrength.Addressable => "loaded through Addressables",
                ReferenceStrength.Resources => "loaded from Resources",
                _ => "pointed at directly"
            };

            string again = conversion.ReplacedExisting ? ", refreshed" : string.Empty;

            return $"{VicinityEditorStyles.DescribeBytes(conversion.EstimatedBytes)}, {conversion.Radius * 2f:0.#} m across — " +
                $"loads at {conversion.LoadDistance:0.#} m, released at {conversion.ReleaseDistance:0.#} m, {how}{again}.";
        }

        private static Label Section(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("vicinity-section-title");
            return label;
        }

        private static Label Hint(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("vicinity-empty");
            return label;
        }

        private struct ResidencySample
        {
            public int Frame;
            public int Managed;
            public int Resident;
            public int Loading;
            public int Queued;
            public int Failed;
            public long MemoryBytes;
        }

        private sealed class MemoryGraph : VisualElement
        {
            internal MemoryGraph(List<ResidencySample> samples)
            {
                _samples = samples;
                generateVisualContent += OnGenerateVisualContent;
            }

            private readonly List<ResidencySample> _samples;

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                if (_samples.Count < 2)
                {
                    return;
                }

                Rect area = contentRect;
                if (area.width <= 1f || area.height <= 1f)
                {
                    return;
                }

                long peak = 1L;
                foreach (ResidencySample sample in _samples)
                {
                    peak = Math.Max(peak, sample.MemoryBytes);
                }

                Painter2D painter = context.painter2D;
                painter.strokeColor = VicinityEditorStyles.ResidentColor;
                painter.lineWidth = 2f;
                painter.BeginPath();

                for (int i = 0; i < _samples.Count; i++)
                {
                    float x = area.width * i / (_samples.Count - 1);
                    float y = area.height * (1f - (float)_samples[i].MemoryBytes / peak);

                    if (i == 0)
                    {
                        painter.MoveTo(new Vector2(x, y));
                    }
                    else
                    {
                        painter.LineTo(new Vector2(x, y));
                    }
                }

                painter.Stroke();
            }
        }

        #endregion
    }
}
