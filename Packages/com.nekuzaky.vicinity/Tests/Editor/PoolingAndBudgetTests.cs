using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class PoolingAndBudgetTests
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
        public void AReleasedInstanceIsKeptAsideInsteadOfBeingDestroyed()
        {
            ResidencyController controller = CreateController(poolCapacity: 4);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));

            Advance(controller, ViewAt(new float3(500f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(entry));
            Assert.AreEqual(0, _provider.ReleaseCallCount, "the instance should have gone to the pool, not to the provider");
            Assert.AreEqual(1, controller.Statistics.Pooled);
        }

        [Test]
        public void WalkingBackReusesThePooledInstanceInsteadOfLoadingAgain()
        {
            ResidencyController controller = CreateController(poolCapacity: 4);
            int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));
            Advance(controller, ViewAt(new float3(500f, 0f, 0f)));
            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry));
            Assert.AreEqual(1, _provider.LoadCallCount, "the pooled instance should have been reused");
            Assert.AreEqual(0, controller.Statistics.Pooled);
        }

        [Test]
        public void AFullPoolDestroysTheOverflow()
        {
            ResidencyController controller = CreateController(poolCapacity: 1);

            controller.Register(EntryAt(new float3(0f, 0f, 0f)));
            controller.Register(EntryAt(new float3(1f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));
            Advance(controller, ViewAt(new float3(500f, 0f, 0f)));

            Assert.AreEqual(1, controller.Statistics.Pooled);
            Assert.AreEqual(1, _provider.ReleaseCallCount, "what does not fit in the pool must be destroyed");
        }

        [Test]
        public void ADisabledPoolDestroysEverything()
        {
            ResidencyController controller = CreateController(poolCapacity: 0);
            controller.Register(EntryAt(new float3(0f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));
            Advance(controller, ViewAt(new float3(500f, 0f, 0f)));

            Assert.AreEqual(0, controller.Statistics.Pooled);
            Assert.AreEqual(1, _provider.ReleaseCallCount);
        }

        [Test]
        public void AnUnregisteredSlotIsReusedByTheNextRegistration()
        {
            ResidencyController controller = CreateController(poolCapacity: 0);

            int first = controller.Register(EntryAt(new float3(0f, 0f, 0f)));
            controller.Unregister(first);

            int second = controller.Register(EntryAt(new float3(10f, 0f, 0f)));

            Assert.AreEqual(first, second, "slots must be recycled, otherwise the table grows forever");
            Assert.AreEqual(1, controller.EntryCount);
            Assert.AreEqual(1, controller.Statistics.Managed);
        }

        [Test]
        public void ChurningObjectsNeverGrowTheEntryTable()
        {
            ResidencyController controller = CreateController(poolCapacity: 0);

            for (int i = 0; i < 50; i++)
            {
                int entry = controller.Register(EntryAt(new float3(0f, 0f, 0f)));
                Advance(controller, ViewAt(float3.zero));
                controller.Unregister(entry);
            }

            Assert.AreEqual(1, controller.EntryCount);
            Assert.AreEqual(0, controller.Statistics.Managed);
        }

        [Test]
        public void AMovingObjectIsEvaluatedAtItsNewPosition()
        {
            ResidencyController controller = CreateController(poolCapacity: 0);

            EntryRegistration registration = EntryAt(new float3(500f, 0f, 0f));
            registration.IsMobile = true;
            int entry = controller.Register(registration);

            Advance(controller, ViewAt(float3.zero));
            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(entry));

            controller.UpdatePosition(entry, new float3(10f, 0f, 0f));
            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(entry),
                "an object that moved must be judged where it is now, not where it was registered");
        }

        [Test]
        public void ReachingTheMemoryCeilingReleasesTheFurthestObjects()
        {
            LogAssert.ignoreFailingMessages = true;

            ResidencyController controller = CreateController(poolCapacity: 0);
            ResidencySettings settings = controller.Settings;
            settings.MemoryBudgetBytes = 2500L;
            controller.Settings = settings;

            int near = controller.Register(EntryAt(new float3(1f, 0f, 0f)));
            int middle = controller.Register(EntryAt(new float3(10f, 0f, 0f)));
            int far = controller.Register(EntryAt(new float3(30f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(near));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(middle));
            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(far),
                "the object furthest from the player is the one that must go");
            Assert.LessOrEqual(controller.Statistics.ResidentMemoryBytes, 2500L);
            Assert.AreEqual(1, controller.Statistics.Evicted);

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void NoCeilingMeansNothingIsEvicted()
        {
            ResidencyController controller = CreateController(poolCapacity: 0);

            controller.Register(EntryAt(new float3(1f, 0f, 0f)));
            controller.Register(EntryAt(new float3(10f, 0f, 0f)));
            controller.Register(EntryAt(new float3(30f, 0f, 0f)));

            Advance(controller, ViewAt(float3.zero));

            Assert.AreEqual(3, controller.Statistics.Resident);
            Assert.AreEqual(0, controller.Statistics.Evicted);
        }

        [Test]
        public void AHeavyObjectLoadsAfterALightOneAtTheSameDistance()
        {
            ResidencyController controller = CreateController(poolCapacity: 0, budget: 1);

            EntryRegistration heavy = EntryAt(new float3(10f, 0f, 0f));
            heavy.EstimatedBytes = 32L * 1024L * 1024L;
            int heavyEntry = controller.Register(heavy);

            EntryRegistration light = EntryAt(new float3(10f, 0f, 1f));
            light.EstimatedBytes = 1024L;
            int lightEntry = controller.Register(light);

            controller.Tick(1f, ViewAt(float3.zero));
            controller.Tick(0f, ViewAt(float3.zero));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(lightEntry));
            Assert.AreEqual(ResidencyState.Queued, controller.GetState(heavyEntry),
                "at equal distance the cheap object should come first");
        }

        private static void Advance(ResidencyController controller, VicinityViewState view)
        {
            controller.Tick(1f, view);
            controller.Tick(0f, view);
        }

        private ResidencyController _controller;
        private FakeAssetProvider _provider;

        private ResidencyController CreateController(int poolCapacity, int budget = 8)
        {
            _provider = new FakeAssetProvider(completeImmediately: true);

            AssetProviderRegistry registry = new AssetProviderRegistry();
            registry.Register(_provider);

            ResidencySettings settings = ResidencySettings.Default;
            settings.EvaluationInterval = 0f;
            settings.PredictionHorizon = 0f;
            settings.MovementDeadZone = 0f;
            settings.MaxConcurrentLoads = budget;
            settings.PoolCapacity = poolCapacity;
            settings.MemoryBudgetBytes = 0L;

            _controller = new ResidencyController(registry, null, settings);
            return _controller;
        }

        private static VicinityViewState ViewAt(float3 position)
        {
            return new VicinityViewState
            {
                Position = position,
                Velocity = float3.zero,
                HasFrustum = false
            };
        }

        private static EntryRegistration EntryAt(float3 position)
        {
            return new EntryRegistration
            {
                Key = AssetKey.FromDirectReference(null),
                Position = position,
                BoundsRadius = 1f,
                LoadDistance = 50f,
                UnloadDistance = 80f,
                EstimatedBytes = 1024L
            };
        }
    }
}
