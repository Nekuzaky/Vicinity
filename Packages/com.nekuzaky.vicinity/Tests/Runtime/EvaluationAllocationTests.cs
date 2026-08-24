using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nekuzaky.Vicinity.Tests
{
    /// <summary>
    /// Guards the claim that the evaluation loop allocates nothing once running. A per-frame allocation
    /// is what turns a streaming system into a source of hitches, so it is worth a test rather than a
    /// promise in a readme.
    /// </summary>
    internal sealed class EvaluationAllocationTests
    {
        #region Main Methods

        [UnityTest]
        public IEnumerator TheEvaluationLoopAllocatesNothingPerFrame()
        {
            yield return BuildScene(ObjectCount);

            using ProfilerRecorder recorder =
                ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", Samples);

            if (!recorder.Valid)
            {
                // Better to say the measurement could not be taken than to pass without measuring.
                Assert.Ignore("This player does not expose the GC allocation counter.");
            }

            // The first frames register objects, build the grid and warm the jobs. None of that is the
            // steady state being measured.
            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            long worstFrame = 0L;
            long total = 0L;
            int counted = 0;

            for (int i = 0; i < MeasuredFrames; i++)
            {
                yield return null;

                long frame = recorder.LastValue;

                worstFrame = frame > worstFrame ? frame : worstFrame;
                total += frame;
                counted++;
            }

            Assert.Greater(counted, 0, "no frame was measured");

            long average = total / counted;

            // Measured at 450 bytes a frame, and the same 450 with five times the objects: that is the
            // test runner resuming its own coroutine, not Vicinity. Anything that scaled with the scene
            // would land well above this ceiling — even 3 bytes per object would cross it here.
            Assert.Less(worstFrame, BudgetBytesPerFrame,
                $"the worst frame allocated {worstFrame} bytes with {ObjectCount} objects managed " +
                $"(average {average}). Something in the evaluation loop is allocating.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject built in _built)
            {
                if (built != null)
                {
                    Object.Destroy(built);
                }
            }

            _built.Clear();

            yield return null;
        }

        #endregion

        #region Privates

        private const int ObjectCount = 200;
        private const int WarmUpFrames = 30;
        private const int MeasuredFrames = 120;
        private const int Samples = 1;
        private const long BudgetBytesPerFrame = 1024L;

        private readonly List<GameObject> _built = new List<GameObject>();

        private IEnumerator BuildScene(int objectCount)
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prefab.SetActive(false);
            _built.Add(prefab);

            GameObject target = new GameObject("Target");
            target.AddComponent<VicinityTarget>();
            _built.Add(target);

            AssetKey key = AssetKey.FromDirectReference(prefab);

            for (int i = 0; i < objectCount; i++)
            {
                GameObject host = GameObject.CreatePrimitive(PrimitiveType.Cube);
                host.name = "Managed";

                // Spread them well beyond the loading distance: this measures evaluation, not loading.
                host.transform.position = new Vector3(300f + i * 4f, 0f, 0f);

                VicinityObject managed = host.AddComponent<VicinityObject>();
                managed.SetDetailedModel(key);
                managed.SetEstimatedMemoryBytes(2048L);

                _built.Add(host);
            }

            GameObject manager = new GameObject("Manager");
            manager.AddComponent<VicinityManager>();
            _built.Add(manager);

            yield return null;
        }

        #endregion
    }
}
