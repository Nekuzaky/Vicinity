using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nekuzaky.Vicinity.Tests
{
    internal sealed class LoadedModelIntegrationTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.Destroy(spawned);
                }
            }

            _spawned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheLoadedModelMatchesTheScaleOfTheStandIn()
        {
            yield return BuildScene(standInScale: 3f, prefabKeepsCollider: true);

            yield return WaitForLoadedModel();

            Transform loaded = _managed.transform.GetChild(0);

            Assert.AreEqual(3f, loaded.lossyScale.x, 0.001f,
                "a stand-in scaled in the scene must produce a detailed model at the same world size");
            Assert.AreEqual(3f, loaded.lossyScale.y, 0.001f);
            Assert.AreEqual(3f, loaded.lossyScale.z, 0.001f);
        }

        [UnityTest]
        public IEnumerator TheLoadedModelSitsExactlyWhereTheStandInSat()
        {
            yield return BuildScene(standInScale: 1f, prefabKeepsCollider: true);

            yield return WaitForLoadedModel();

            Transform loaded = _managed.transform.GetChild(0);

            Assert.AreEqual(0f, Vector3.Distance(loaded.position, _managed.transform.position), 0.001f);
            Assert.AreEqual(0f, Quaternion.Angle(loaded.rotation, _managed.transform.rotation), 0.01f);
        }

        [UnityTest]
        public IEnumerator TheLoadedModelInheritsTheBakedLightingOfTheStandIn()
        {
            yield return BuildScene(standInScale: 1f, prefabKeepsCollider: true);

            Renderer standIn = _managedObject.GetComponent<Renderer>();
            standIn.lightmapIndex = 3;
            standIn.lightmapScaleOffset = new Vector4(0.5f, 0.5f, 0.25f, 0.125f);

            yield return WaitForLoadedModel();

            Renderer loaded = _managed.transform.GetChild(0).GetComponent<Renderer>();

            Assert.AreEqual(3, loaded.lightmapIndex,
                "a model loaded at runtime keeps no lightmap of its own; it must take the one baked for the stand-in");
            Assert.AreEqual(0.5f, loaded.lightmapScaleOffset.x, 0.0001f);
            Assert.AreEqual(0.125f, loaded.lightmapScaleOffset.w, 0.0001f);
        }

        [UnityTest]
        public IEnumerator TheStandInColliderStepsAsideWhenTheModelBringsItsOwn()
        {
            yield return BuildScene(standInScale: 1f, prefabKeepsCollider: true);

            Collider standInCollider = _managedObject.GetComponent<Collider>();
            Assert.IsTrue(standInCollider.enabled);

            yield return WaitForLoadedModel();

            Assert.IsFalse(standInCollider.enabled,
                "two overlapping colliders would double every contact");
        }

        [UnityTest]
        public IEnumerator TheStandInColliderStaysWhenTheModelHasNone()
        {
            yield return BuildScene(standInScale: 1f, prefabKeepsCollider: false);

            yield return WaitForLoadedModel();

            Collider standInCollider = _managedObject.GetComponent<Collider>();

            Assert.IsTrue(standInCollider.enabled,
                "removing the only collision in the scene would drop the player through the floor");
        }

        [UnityTest]
        public IEnumerator ReleasingTheModelGivesTheStandInItsColliderBack()
        {
            yield return BuildScene(standInScale: 1f, prefabKeepsCollider: true);
            yield return WaitForLoadedModel();

            Collider standInCollider = _managedObject.GetComponent<Collider>();
            Assert.IsFalse(standInCollider.enabled);

            _targetObject.transform.position = new Vector3(0f, 0f, 5000f);
            yield return WaitForState(ResidencyState.Unloaded);

            Assert.IsTrue(standInCollider.enabled);
        }

        private const float TimeoutSeconds = 15f;

        private readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();

        private GameObject _managedObject;
        private GameObject _targetObject;
        private VicinityObject _managed;

        private IEnumerator BuildScene(float standInScale, bool prefabKeepsCollider)
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prefab.name = "Detailed Model";
            prefab.SetActive(false);
            _spawned.Add(prefab);

            if (!prefabKeepsCollider)
            {
                Object.DestroyImmediate(prefab.GetComponent<Collider>());
            }

            _targetObject = new GameObject("Target");
            _targetObject.transform.position = Vector3.zero;
            _targetObject.AddComponent<VicinityTarget>();
            _spawned.Add(_targetObject);

            _managedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _managedObject.name = "Managed";
            _managedObject.transform.position = new Vector3(5f, 0f, 0f);
            _managedObject.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            _managedObject.transform.localScale = Vector3.one * standInScale;
            _spawned.Add(_managedObject);

            _managed = _managedObject.AddComponent<VicinityObject>();
            _managed.SetDetailedModel(AssetKey.FromDirectReference(prefab));
            _managed.SetEstimatedMemoryBytes(2048L);

            GameObject managerObject = new GameObject("Manager");
            managerObject.AddComponent<VicinityManager>();
            _spawned.Add(managerObject);

            yield return null;
        }

        private IEnumerator WaitForLoadedModel()
        {
            yield return WaitForState(ResidencyState.Resident);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_managed.transform.childCount == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.Greater(_managed.transform.childCount, 0, "the loaded model was never parented");
        }

        private IEnumerator WaitForState(ResidencyState expected)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (_managed != null && _managed.State == expected)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"never reached {expected} within {TimeoutSeconds} seconds; it is {_managed?.State.ToString() ?? "gone"}");
        }
    }
}
