using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class ResidencyControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            _controller = null;

            _provider?.DestroySpawnedInstances();
            _provider = null;
        }

        [Test]
        public void AnObjectBeyondTheLoadDistanceStaysUnloaded()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            int entry = controller.Register(EntryAt(new float3(200f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(entry));
            Assert.AreEqual(0, _provider.LoadCallCount);
        }

        [Test]
        public void AnObjectWithinTheLoadDistanceBecomesResident()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            int entry = controller.Register(EntryAt(new float3(10f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));
            Assert.AreEqual(1, _provider.LoadCallCount);
        }

        [Test]
        public void AnObjectBetweenTheTwoDistancesStaysResident()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            int entry = controller.Register(EntryAt(new float3(10f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));

            Advance(controller, ViewAt(new float3(-55f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));
            Assert.AreEqual(0, _provider.ReleaseCallCount);
        }

        [Test]
        public void CrossingTheBoundaryBackAndForthNeverReloads()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));

            Advance(controller, ViewAt(new float3(40f, 0f, 0f)));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));

            for (int i = 0; i < 20; i++)
            {
                float distance = i % 2 == 0 ? 49f : 51f;
                Advance(controller, ViewAt(new float3(distance, 0f, 0f)));
            }

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));
            Assert.AreEqual(1, _provider.LoadCallCount);
            Assert.AreEqual(0, _provider.ReleaseCallCount);
        }

        [Test]
        public void AnObjectBeyondTheUnloadDistanceIsReleased()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));

            Advance(controller, ViewAt(new float3(500f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(entry));
            Assert.AreEqual(1, _provider.ReleaseCallCount);
        }

        [Test]
        public void SimultaneousLoadsNeverExceedTheBudget()
        {
            ResidencyController controller = CreateController(completeImmediately: false, budget: 3);

            for (int i = 0; i < 20; i++)
            {
                controller.Register(EntryAt(new float3(i * 0.5f, 0f, 0f)));
            }

            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(3, controller.Statistics.Loading);
            Assert.AreEqual(17, controller.Statistics.Queued);
            Assert.AreEqual(3, _provider.LoadCallCount);
        }

        [Test]
        public void QueuedObjectsStartLoadingAsSlotsFreeUp()
        {
            ResidencyController controller = CreateController(completeImmediately: false, budget: 2);

            for (int i = 0; i < 6; i++)
            {
                controller.Register(EntryAt(new float3(i * 0.5f, 0f, 0f)));
            }

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(2, controller.Statistics.Loading);

            _provider.CompleteAllPending();
            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(2, controller.Statistics.Resident);
            Assert.AreEqual(2, controller.Statistics.Loading);
        }

        [Test]
        public void TheClosestObjectIsLoadedFirst()
        {
            ResidencyController controller = CreateController(completeImmediately: false, budget: 1);

            int far = controller.Register(EntryAt(new float3(40f, 0f, 0f)));
            int near = controller.Register(EntryAt(new float3(5f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Loading, controller.GetState(near));
            Assert.AreEqual(ResidencyState.Queued, controller.GetState(far));
        }

        [Test]
        public void MovementPredictionLoadsWhatIsAhead()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            controller.Settings = WithPredictionHorizon(controller.Settings, 2f);

            int entry = controller.Register(EntryAt(new float3(90f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));

            Advance(controller, ViewAt(float3.zero, new float3(25f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));
        }

        [Test]
        public void TeleportingTheTargetReleasesEverything()
        {
            ResidencyController controller = CreateController(completeImmediately: true);

            for (int i = 0; i < 8; i++)
            {
                controller.Register(EntryAt(new float3(i * 2f, 0f, 0f)));
            }

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(8, controller.Statistics.Resident);

            Advance(controller, ViewAt(new float3(100000f, 0f, 100000f)));

            Assert.AreEqual(0, controller.Statistics.Resident);
            Assert.AreEqual(8, _provider.ReleaseCallCount);
        }

        [Test]
        public void AnAssetIsNeverReleasedBeforeItsInstantiationCompletes()
        {
            ResidencyController controller = CreateController(completeImmediately: false);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(ResidencyState.Loading, controller.GetState(entry));

            Advance(controller, ViewAt(new float3(500f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Loading, controller.GetState(entry), "the entry must stay in Loading until instantiation finishes");
            Assert.AreEqual(0, _provider.ReleaseCallCount, "nothing may be released while an instantiation is still running");

            _provider.CompleteAllPendingIgnoringCancellation();

            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(entry));
            Assert.AreEqual(1, _provider.ReleaseCallCount, "the produced instance must be released as soon as it exists");
        }

        [Test]
        public void ACanceledLoadLeavesTheEntryUnloaded()
        {
            ResidencyController controller = CreateController(completeImmediately: false);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));

            Advance(controller, ViewAt(float3.zero));
            Advance(controller, ViewAt(new float3(500f, 0f, 0f)));

            _provider.CompleteAllPending();

            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(entry));
        }

        [Test]
        public void AFailingProviderMarksTheEntryFailedAndReportsOnce()
        {
            LogAssert.ignoreFailingMessages = true;

            ResidencyController controller = CreateController(completeImmediately: true);
            _provider.FailEveryLoad = true;

            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f)));
            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Failed, controller.GetState(entry));

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void AnEntryIsAbandonedAfterTooManyFailures()
        {
            LogAssert.ignoreFailingMessages = true;

            ResidencyController controller = CreateController(completeImmediately: true);
            controller.Settings = WithMaxAttempts(controller.Settings, 2);
            _provider.FailEveryLoad = true;

            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Advance(controller, ViewAt(float3.zero));
                Advance(controller, ViewAt(new float3(500f, 0f, 0f)));
            }

            Assert.AreEqual(ResidencyState.Failed, controller.GetState(entry));
            Assert.AreEqual(2, _provider.LoadCallCount, "Vicinity must stop retrying once the attempt budget is spent");
            Assert.AreEqual(0, controller.Statistics.Managed);

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void UnregisteringAResidentObjectReleasesIt()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));

            controller.Unregister(entry);

            Assert.AreEqual(1, _provider.ReleaseCallCount);
            Assert.AreEqual(0, controller.Statistics.Managed);
        }

        [Test]
        public void TwoObjectsInTheSameCellAreBothEvaluated()
        {
            ResidencyController controller = CreateController(completeImmediately: true);

            int first = controller.Register(EntryAt(new float3(1f, 0f, 1f)));
            int second = controller.Register(EntryAt(new float3(2f, 0f, 2f)));

            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(first));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(second));
        }

        [Test]
        public void TheSameCameraPathProducesTheSameDecisions()
        {
            float3[] path =
            {
                new float3(0f, 0f, 0f),
                new float3(30f, 0f, 0f),
                new float3(70f, 0f, 0f),
                new float3(120f, 0f, 0f),
                new float3(40f, 0f, 0f)
            };

            string firstRun = RunPathAndDescribeStates(path);
            string secondRun = RunPathAndDescribeStates(path);

            Assert.AreEqual(firstRun, secondRun);
        }

        [Test]
        public void HysteresisIsForcedEvenWhenTheMarginIsWrong()
        {
            ResidencyController controller = CreateController(completeImmediately: true);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f), loadDistance: 60f, unloadDistance: 10f));

            Advance(controller, ViewAt(new float3(30f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry),
                "an unload distance below the load distance must be corrected, not obeyed");
        }

        private static void Advance(ResidencyController controller, VicinityViewState view)
        {
            controller.Tick(1f, view);
            controller.Tick(0f, view);
        }

        private ResidencyController _controller;
        private FakeAssetProvider _provider;

        private ResidencyController CreateController(bool completeImmediately, int budget = 8)
        {
            _provider = new FakeAssetProvider(completeImmediately);

            AssetProviderRegistry registry = new AssetProviderRegistry();
            registry.Register(_provider);

            ResidencySettings settings = ResidencySettings.Default;
            settings.EvaluationInterval = 0f;
            settings.PredictionHorizon = 0f;
            settings.MaxConcurrentLoads = budget;
            settings.PoolCapacity = 0;

            _controller = new ResidencyController(registry, null, settings);
            return _controller;
        }

        private string RunPathAndDescribeStates(float3[] path)
        {
            _controller?.Dispose();
            _provider?.DestroySpawnedInstances();

            ResidencyController controller = CreateController(completeImmediately: true, budget: 2);
            int[] entries = new int[6];

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = controller.Register(EntryAt(new float3(i * 25f, 0f, 0f), loadDistance: 50f, unloadDistance: 80f));
            }

            System.Text.StringBuilder trace = new System.Text.StringBuilder();

            for (int step = 0; step < path.Length; step++)
            {
                Advance(controller, ViewAt(path[step]));

                for (int i = 0; i < entries.Length; i++)
                {
                    trace.Append((int)controller.GetState(entries[i]));
                    trace.Append(',');
                }

                trace.Append(';');
            }

            return trace.ToString();
        }

        private static ResidencySettings WithPredictionHorizon(ResidencySettings settings, float horizon)
        {
            settings.PredictionHorizon = horizon;
            return settings;
        }

        private static ResidencySettings WithMaxAttempts(ResidencySettings settings, int attempts)
        {
            settings.MaxLoadAttempts = attempts;
            return settings;
        }

        private static VicinityViewState ViewAt(float3 position) => ViewAt(position, float3.zero);

        private static VicinityViewState ViewAt(float3 position, float3 velocity)
        {
            return new VicinityViewState
            {
                Position = position,
                Velocity = velocity,
                HasFrustum = false
            };
        }

        private static EntryRegistration EntryAt(float3 position, float loadDistance = 50f, float unloadDistance = 80f)
        {
            return new EntryRegistration
            {
                Key = AssetKey.FromDirectReference(null),
                Position = position,
                BoundsRadius = 1f,
                LoadDistance = loadDistance,
                UnloadDistance = unloadDistance,
                EstimatedBytes = 1024L
            };
        }
    }
}
