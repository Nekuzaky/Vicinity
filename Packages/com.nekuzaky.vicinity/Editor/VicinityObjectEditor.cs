using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    [CustomEditor(typeof(VicinityObject))]
    [CanEditMultipleObjects]
    internal sealed class VicinityObjectEditor : UnityEditor.Editor
    {
        #region Unity API

        private void OnEnable()
        {
            _detailLevels = serializedObject.FindProperty("m_detailLevels");
            _overrideDistances = serializedObject.FindProperty("m_overrideDistances");
            _loadDistance = serializedObject.FindProperty("m_loadDistance");
            _unloadDistance = serializedObject.FindProperty("m_unloadDistance");
            _estimatedMemoryBytes = serializedObject.FindProperty("m_estimatedMemoryBytes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "What sits here in the scene is the lightweight stand-in. The models below are loaded as the player comes closer, and released when they walk away.",
                MessageType.None);

            VicinityDocs.DrawInspectorLink("How distances work", DocPage.DistancesAndSteps);

            DrawQualitySteps();
            DrawMissingModelWarning();
            DrawOrderWarning();

            EditorGUILayout.Space();
            DrawDistances();

            DrawMarginWarning();
            DrawNoTargetWarning();
            DrawMemoryLine();
            DrawRuntimeState();

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            if (!VicinityEditorStyles.GizmosVisible || target is not VicinityObject managed)
            {
                return;
            }

            if (managed.HasSeveralLevels || !managed.OverridesDistances)
            {
                return;
            }

            VicinityHandles.DrawDistanceHandles(
                managed.transform.position,
                _loadDistance,
                _unloadDistance,
                serializedObject);
        }

        #endregion

        #region Privates

        private const float SafeMarginRatio = 1.4f;
        private const float ExtraStepRatio = 2.5f;

        private SerializedProperty _detailLevels;
        private SerializedProperty _overrideDistances;
        private SerializedProperty _loadDistance;
        private SerializedProperty _unloadDistance;
        private SerializedProperty _estimatedMemoryBytes;

        private void DrawQualitySteps()
        {
            bool single = _detailLevels.arraySize <= 1 && !_detailLevels.hasMultipleDifferentValues;

            if (single)
            {
                DrawSingleStep();
            }
            else
            {
                EditorGUILayout.PropertyField(_detailLevels, new GUIContent("Quality steps"), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(single ? "Add a quality step" : "Add another step"))
                {
                    AppendStep();
                }

                using (new EditorGUI.DisabledScope(_detailLevels.arraySize <= 1))
                {
                    if (GUILayout.Button("Remove the furthest step"))
                    {
                        _detailLevels.arraySize--;
                    }
                }
            }
        }

        private void DrawSingleStep()
        {
            if (_detailLevels.arraySize == 0)
            {
                _detailLevels.arraySize = 1;
            }

            SerializedProperty level = _detailLevels.GetArrayElementAtIndex(0);
            SerializedProperty model = level.FindPropertyRelative("m_model");

            EditorGUILayout.PropertyField(model, new GUIContent("Detailed model", "The model loaded when the player comes close."));
        }

        private void AppendStep()
        {
            int index = _detailLevels.arraySize;
            _detailLevels.arraySize = index + 1;

            SerializedProperty added = _detailLevels.GetArrayElementAtIndex(index);
            SerializedProperty range = added.FindPropertyRelative("m_range");

            float previous = index == 0
                ? _loadDistance.floatValue
                : _detailLevels.GetArrayElementAtIndex(index - 1).FindPropertyRelative("m_range").floatValue;

            if (index == 0)
            {
                range.floatValue = _loadDistance.floatValue;
                return;
            }

            if (index == 1 && previous <= 0f)
            {
                previous = _loadDistance.floatValue;
                _detailLevels.GetArrayElementAtIndex(0).FindPropertyRelative("m_range").floatValue = previous;
            }

            range.floatValue = Mathf.Max(previous * ExtraStepRatio, previous + 1f);
        }

        private void DrawDistances()
        {
            bool stepsCarryDistances = _detailLevels.arraySize > 1;

            if (stepsCarryDistances)
            {
                EditorGUILayout.HelpBox(
                    "With several quality steps, each step's own distance decides where it is used.",
                    MessageType.Info);

                return;
            }

            if (_overrideDistances.boolValue)
            {
                DrawOwnDistances();
                return;
            }

            DrawInheritedDistances();
        }

        private void DrawOwnDistances()
        {
            EditorGUILayout.PropertyField(_loadDistance, new GUIContent("Loads at"));
            EditorGUILayout.PropertyField(_unloadDistance, new GUIContent("Released at"));

            if (GUILayout.Button("Go back to the shared distances"))
            {
                _overrideDistances.boolValue = false;
            }
        }

        private void DrawInheritedDistances()
        {
            if (targets.Length != 1 || target is not VicinityObject managed)
            {
                EditorGUILayout.PropertyField(_overrideDistances);
                return;
            }

            DistancePreview preview = VicinityDistancePreview.Resolve(managed);

            EditorGUILayout.LabelField(
                "Distances",
                $"loads at {preview.LoadDistance:0.#} m, released at {preview.ReleaseDistance:0.#} m");

            EditorGUILayout.LabelField(" ", $"from {preview.Source}", EditorStyles.miniLabel);

            if (!GUILayout.Button("Set distances just for this object"))
            {
                return;
            }

            _overrideDistances.boolValue = true;
            _loadDistance.floatValue = preview.LoadDistance;
            _unloadDistance.floatValue = preview.ReleaseDistance;
        }

        private void DrawMissingModelWarning()
        {
            bool anyMissing = false;

            foreach (Object candidate in targets)
            {
                if (candidate is VicinityObject managed && managed.HasMissingModel)
                {
                    anyMissing = true;
                    break;
                }
            }

            if (!anyMissing)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "A quality step names no model, so this object will never load anything there. Pick a prefab above, or remove the component.",
                MessageType.Warning);

            if (GUILayout.Button("Remove Vicinity from these objects"))
            {
                RemoveComponentFromTargets();
            }
        }

        private void DrawOrderWarning()
        {
            bool anyUnordered = false;

            foreach (Object candidate in targets)
            {
                if (candidate is VicinityObject managed && managed.HasUnorderedLevels)
                {
                    anyUnordered = true;
                    break;
                }
            }

            if (!anyUnordered)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Quality steps must go from closest to furthest, each distance larger than the one before it. As set, a step would never be used.",
                MessageType.Error);

            if (GUILayout.Button("Space the steps out"))
            {
                SpaceStepsOut();
            }
        }

        private void SpaceStepsOut()
        {
            float previous = 0f;

            for (int i = 0; i < _detailLevels.arraySize; i++)
            {
                SerializedProperty range = _detailLevels.GetArrayElementAtIndex(i).FindPropertyRelative("m_range");
                float wanted = i == 0 ? Mathf.Max(range.floatValue, _loadDistance.floatValue) : range.floatValue;

                if (wanted <= previous)
                {
                    wanted = Mathf.Max(previous * ExtraStepRatio, previous + 1f);
                }

                range.floatValue = wanted;
                previous = wanted;
            }
        }

        private void DrawMarginWarning()
        {
            if (_detailLevels.arraySize > 1 || !_overrideDistances.boolValue || _unloadDistance.floatValue > _loadDistance.floatValue)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "The releasing distance must be larger than the loading distance. As set, this object would load and unload on every step the player takes near the boundary.",
                MessageType.Error);

            if (GUILayout.Button("Set a safe releasing distance"))
            {
                _unloadDistance.floatValue = _loadDistance.floatValue * SafeMarginRatio;
            }
        }

        private void DrawNoTargetWarning()
        {
            if (VicinityTargetRegistry.Targets.Count > 0 || Object.FindFirstObjectByType<VicinityTarget>() != null)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "No viewpoint exists in this scene, so Vicinity has nothing to measure distances from. It will fall back to the active camera.",
                MessageType.Info);

            if (GUILayout.Button("Add a viewpoint to the scene"))
            {
                VicinitySceneSetup.CreateTarget();
            }
        }

        private void DrawMemoryLine()
        {
            long bytes = _estimatedMemoryBytes.longValue;

            EditorGUILayout.LabelField(
                "Model size",
                bytes > 0L ? VicinityEditorStyles.DescribeBytes(bytes) : "not measured yet");
        }

        private void DrawRuntimeState()
        {
            if (!Application.isPlaying || targets.Length != 1 || target is not VicinityObject managed)
            {
                return;
            }

            EditorGUILayout.LabelField("Currently", managed.State.ToString());
            Repaint();
        }

        private void RemoveComponentFromTargets()
        {
            foreach (Object candidate in targets)
            {
                if (candidate is VicinityObject managed)
                {
                    Undo.DestroyObjectImmediate(managed);
                }
            }
        }

        #endregion
    }
}
