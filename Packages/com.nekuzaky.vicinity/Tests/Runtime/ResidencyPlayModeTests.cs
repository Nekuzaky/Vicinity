using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nekuzaky.Vicinity.Tests
{
    internal sealed class ResidencyPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_managerObject != null)
            {
                Object.Destroy(_managerObject);
            }

            if (_targetObject != null)
            {
                Object.Destroy(_targetObject);
            }

            if (_managedObject != null)
            {
                Object.Destroy(_managedObject);
            }

            if (_prefab != null)
            {
                Object.Destroy(_prefab);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AnObjectLoadsWhenTheTargetComesClose()
        {
            yield return BuildScene(distanceFromTarget: 5f);

            yield return WaitForState(ResidencyState.Resident);

            Assert.AreEqual(ResidencyState.Resident, _managed.State);
            Assert.AreEqual(1, _managed.transform.childCount, "the loaded model should be parented under the managed object");
        }

        [UnityTest]
        public IEnumerator TheStandInIsHiddenOnlyOnceTheModelIsLoaded()
        {
            yield return BuildScene(distanceFromTarget: 5f);

            Renderer standIn = _managedObject.GetComponent<Renderer>();
            Assert.IsTrue(standIn.enabled, "the stand-in must stay visible while the model is still loading");

            yield return WaitForState(ResidencyState.Resident);

            Assert.IsFalse(standIn.enabled);
        }

        [UnityTest]
        public IEnumerator AnObjectIsReleasedWhenTheTargetWalksAway()
        {
            yield return BuildScene(distanceFromTarget: 5f);
            yield return WaitForState(ResidencyState.Resident);

            _targetObject.transform.position = new Vector3(0f, 0f, 5000f);

            yield return WaitForState(ResidencyState.Unloaded);

            Assert.AreEqual(ResidencyState.Unloaded, _managed.State);
            Assert.AreEqual(0, _managed.transform.childCount);
            Assert.IsTrue(_managedObject.GetComponent<Renderer>().enabled);
        }

        [UnityTest]
        public IEnumerator ATeleportAwayAndBackLoadsAgain()
        {
            yield return BuildScene(distanceFromTarget: 5f);
            yield return WaitForState(ResidencyState.Resident);

            _targetObject.transform.position = new Vector3(0f, 0f, 5000f);
            yield return WaitForState(ResidencyState.Unloaded);

            _targetObject.transform.position = Vector3.zero;
            yield return WaitForState(ResidencyState.Resident);

            Assert.AreEqual(ResidencyState.Resident, _managed.State);
        }

        [UnityTest]
        public IEnumerator DestroyingTheManagedObjectDuringALoadDoesNotThrow()
        {
            yield return BuildScene(distanceFromTarget: 5f);

            Object.Destroy(_managedObject);
            _managedObject = null;
            _managed = null;

            yield return WaitSeconds(SettleSeconds);

            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator TheManagerReportsWhatItHoldsInMemory()
        {
            yield return BuildScene(distanceFromTarget: 5f);
            yield return WaitForState(ResidencyState.Resident);

            ResidencyStatistics statistics = _manager.Statistics;

            Assert.AreEqual(1, statistics.Managed);
            Assert.AreEqual(1, statistics.Resident);
            Assert.Greater(statistics.ResidentMemoryBytes, 0L);
        }

        private const float TimeoutSeconds = 15f;
        private const float SettleSeconds = 2f;

        private GameObject _managerObject;
        private GameObject _targetObject;
        private GameObject _managedObject;
        private GameObject _prefab;
        private VicinityManager _manager;
        private VicinityObject _managed;

        private IEnumerator BuildScene(float distanceFromTarget)
        {
            _prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _prefab.name = "Detailed Model";
            _prefab.SetActive(false);

            _targetObject = new GameObject("Target");
            _targetObject.transform.position = Vector3.zero;
            _targetObject.AddComponent<VicinityTarget>();

            _managedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _managedObject.name = "Managed";
            _managedObject.transform.position = new Vector3(distanceFromTarget, 0f, 0f);

            _managed = _managedObject.AddComponent<VicinityObject>();
            _managed.SetDetailedModel(AssetKey.FromDirectReference(_prefab));
            _managed.SetEstimatedMemoryBytes(2048L);

            _managerObject = new GameObject("Manager");
            _manager = _managerObject.AddComponent<VicinityManager>();

            yield return null;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
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

            ResidencyStatistics diagnostics = _manager != null ? _manager.Statistics : default;
            float distance = _managed != null && _targetObject != null
                ? Vector3.Distance(_managed.transform.position, _targetObject.transform.position)
                : -1f;

            Assert.Fail($"never reached {expected} within {TimeoutSeconds} seconds; " +
                $"state={_managed?.State.ToString() ?? "gone"} distance={distance:0.#} " +
                $"managed={diagnostics.Managed} resident={diagnostics.Resident} " +
                $"loading={diagnostics.Loading} queued={diagnostics.Queued}");
        }
    }
}
