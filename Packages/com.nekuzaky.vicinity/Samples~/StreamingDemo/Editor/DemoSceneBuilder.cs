using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nekuzaky.Vicinity.Demo.Editor
{
    internal static class DemoSceneBuilder
    {
        #region Main Methods

        [MenuItem("Tools/Vicinity/Build the Streaming Demo Scene", false, 200)]
        internal static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build the streaming demo",
                    $"This creates a new scene with {ObjectCount} managed objects, plus a few assets under {DemoFolder}. Your current scene will be closed.",
                    "Build it",
                    "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolders();

            GameObject detailedPrefab = CreateDetailedPrefab();
            VicinityProfile profile = FindOrCreateProfile();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            BuildGround();
            BuildManagedField(detailedPrefab, profile);
            BuildViewpoint();
            BuildManager(profile);

            string scenePath = $"{DemoFolder}/{SceneName}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Vicinity built the streaming demo at {scenePath}. Press Play, then open Window > Vicinity > Dashboard and watch the Live tab.");
        }

        #endregion

        #region Privates

        private const string DemoFolder = "Assets/Vicinity Demo";
        private const string SceneName = "Streaming Demo";
        private const string DetailedPrefabName = "Detailed Rock";
        private const string ProfileName = "Demo Open World";
        private const int ObjectCount = 5000;
        private const int Columns = 100;
        private const float Spacing = 9f;
        private const float DetailedScale = 1.35f;

        private static long EstimateBytes(GameObject prefab)
        {
            long total = 0L;

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                total += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(filter.sharedMesh);
            }

            return total;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(DemoFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Vicinity Demo");
            }
        }

        private static GameObject CreateDetailedPrefab()
        {
            string path = $"{DemoFolder}/{DetailedPrefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
            {
                return existing;
            }

            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            source.name = DetailedPrefabName;
            source.transform.localScale = Vector3.one * DetailedScale;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);

            return prefab;
        }

        private static VicinityProfile FindOrCreateProfile()
        {
            string path = $"{DemoFolder}/{ProfileName}.asset";
            VicinityProfile existing = AssetDatabase.LoadAssetAtPath<VicinityProfile>(path);

            if (existing != null)
            {
                return existing;
            }

            VicinityProfile profile = ScriptableObject.CreateInstance<VicinityProfile>();
            profile.name = ProfileName;
            AssetDatabase.CreateAsset(profile, path);

            return profile;
        }

        private static void BuildGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";

            float span = Columns * Spacing;
            ground.transform.localScale = new Vector3(span * 0.1f, 1f, span * 0.1f);
            ground.transform.position = new Vector3(span * 0.5f, 0f, span * 0.5f);
        }

        private static void BuildManagedField(GameObject detailedPrefab, VicinityProfile profile)
        {
            GameObject root = new GameObject("Managed Objects");
            AssetKey key = AssetKey.FromDirectReference(detailedPrefab);
            long estimatedBytes = EstimateBytes(detailedPrefab);

            for (int i = 0; i < ObjectCount; i++)
            {
                if (i % 250 == 0)
                {
                    EditorUtility.DisplayProgressBar("Building the streaming demo", $"{i} of {ObjectCount} objects", (float)i / ObjectCount);
                }

                GameObject standIn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                standIn.name = $"Managed {i:0000}";
                standIn.transform.SetParent(root.transform, false);
                standIn.transform.position = new Vector3(i % Columns * Spacing, 0.5f, i / Columns * Spacing);
                standIn.transform.localScale = Vector3.one * 0.8f;

                VicinityObject managed = standIn.AddComponent<VicinityObject>();
                managed.SetDetailedModel(key);
                managed.SetEstimatedMemoryBytes(estimatedBytes);
            }

            EditorUtility.ClearProgressBar();

            if (profile != null)
            {
                GameObject volumeHost = new GameObject("Field Volume");
                float span = Columns * Spacing;
                volumeHost.transform.position = new Vector3(span * 0.5f, 0f, span * 0.5f);

                VicinityVolume volume = volumeHost.AddComponent<VicinityVolume>();
                volume.SetBox(Vector3.zero, new Vector3(span, 50f, span));

                SerializedObject serialized = new SerializedObject(volume);
                serialized.FindProperty("m_profile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildViewpoint()
        {
            Camera camera = Object.FindFirstObjectByType<Camera>();
            GameObject host = camera != null ? camera.gameObject : new GameObject("Viewpoint");

            host.transform.position = new Vector3(Columns * Spacing * 0.5f, 12f, -30f);
            host.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            if (host.GetComponent<VicinityTarget>() == null)
            {
                host.AddComponent<VicinityTarget>();
            }

            if (host.GetComponent<DemoFlyThrough>() == null)
            {
                host.AddComponent<DemoFlyThrough>();
            }
        }

        private static void BuildManager(VicinityProfile profile)
        {
            GameObject host = new GameObject("Vicinity Manager");
            VicinityManager manager = host.AddComponent<VicinityManager>();
            manager.SetProfile(profile);
        }

        #endregion
    }
}
