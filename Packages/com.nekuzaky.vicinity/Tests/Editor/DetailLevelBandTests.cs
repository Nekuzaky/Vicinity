using NUnit.Framework;
using Unity.Mathematics;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class DetailLevelBandTests
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
        public void OnlyTheStepCoveringTheDistanceIsLoaded()
        {
            ResidencyController controller = CreateController();

            int close = controller.Register(Band(inner: 0f, outer: 60f));
            int far = controller.Register(Band(inner: 60f, outer: 200f));

            Advance(controller, ViewAt(new float3(20f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(close));
            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(far));
        }

        [Test]
        public void TheDistantStepTakesOverBeyondTheCloseOne()
        {
            ResidencyController controller = CreateController();

            int close = controller.Register(Band(inner: 0f, outer: 60f));
            int far = controller.Register(Band(inner: 60f, outer: 200f));

            Advance(controller, ViewAt(new float3(150f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(close));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(far));
        }

        [Test]
        public void BothStepsStayLoadedInsideTheOverlapSoNoHoleAppears()
        {
            ResidencyController controller = CreateController();

            int close = controller.Register(Band(inner: 0f, outer: 60f));
            int far = controller.Register(Band(inner: 60f, outer: 200f));

            Advance(controller, ViewAt(new float3(150f, 0f, 0f)));
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(far));

            Advance(controller, ViewAt(new float3(55f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(close), "the close step must be loaded");
            Assert.AreEqual(ResidencyState.Resident, controller.GetState(far),
                "the distant step must stay until the close one is ready, otherwise a hole appears");
        }

        [Test]
        public void WalkingAllTheWayInReleasesTheDistantStep()
        {
            ResidencyController controller = CreateController();

            int close = controller.Register(Band(inner: 0f, outer: 60f));
            int far = controller.Register(Band(inner: 60f, outer: 200f));

            Advance(controller, ViewAt(new float3(150f, 0f, 0f)));
            Advance(controller, ViewAt(new float3(55f, 0f, 0f)));
            Advance(controller, ViewAt(new float3(10f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Resident, controller.GetState(close));
            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(far));
        }

        [Test]
        public void WalkingAllTheWayOutReleasesEveryStep()
        {
            ResidencyController controller = CreateController();

            int close = controller.Register(Band(inner: 0f, outer: 60f));
            int far = controller.Register(Band(inner: 60f, outer: 200f));

            Advance(controller, ViewAt(new float3(20f, 0f, 0f)));
            Advance(controller, ViewAt(new float3(1000f, 0f, 0f)));

            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(close));
            Assert.AreEqual(ResidencyState.Unloaded, controller.GetState(far));
        }

        [Test]
        public void CrossingTheStepBoundaryBackAndForthNeverReloads()
        {
            ResidencyController controller = CreateController();

            controller.Register(Band(inner: 0f, outer: 60f));
            controller.Register(Band(inner: 60f, outer: 200f));

            Advance(controller, ViewAt(new float3(58f, 0f, 0f)));
            Advance(controller, ViewAt(new float3(61f, 0f, 0f)));
            int loadsAfterFirstCrossing = _provider.LoadCallCount;

            for (int i = 0; i < 20; i++)
            {
                float distance = i % 2 == 0 ? 59f : 61f;
                Advance(controller, ViewAt(new float3(distance, 0f, 0f)));
            }

            Assert.AreEqual(loadsAfterFirstCrossing, _provider.LoadCallCount,
                "once both steps are settled, pacing across the boundary must not trigger a single extra load");
            Assert.AreEqual(0, _provider.ReleaseCallCount);
        }

        [Test]
        public void TheViewpointStandingStillSkipsEvaluation()
        {
            ResidencyController controller = CreateController();
            ResidencySettings settings = controller.Settings;
            settings.MovementDeadZone = 5f;
            controller.Settings = settings;

            controller.Register(Band(inner: 0f, outer: 60f));

            Advance(controller, ViewAt(new float3(1000f, 0f, 0f)));
            Advance(controller, ViewAt(new float3(1001f, 0f, 0f)));
            Advance(controller, ViewAt(new float3(1002f, 0f, 0f)));

            Assert.AreEqual(0, _provider.LoadCallCount);

            Advance(controller, ViewAt(new float3(10f, 0f, 0f)));

            Assert.AreEqual(1, _provider.LoadCallCount, "a real move must still be evaluated");
        }

        private static void Advance(ResidencyController controller, VicinityViewState view)
        {
            controller.Tick(1f, view);
            controller.Tick(0f, view);
        }

        private ResidencyController _controller;
        private FakeAssetProvider _provider;

        private ResidencyController CreateController()
        {
            _provider = new FakeAssetProvider(completeImmediately: true);

            AssetProviderRegistry registry = new AssetProviderRegistry();
            registry.Register(_provider);

            ResidencySettings settings = ResidencySettings.Default;
            settings.EvaluationInterval = 0f;
            settings.PredictionHorizon = 0f;
            settings.MovementDeadZone = 0f;
            settings.MaxConcurrentLoads = 8;
            settings.PoolCapacity = 0;

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

        private static EntryRegistration Band(float inner, float outer)
        {
            const float marginRatio = 1.4f;

            return new EntryRegistration
            {
                Key = AssetKey.FromDirectReference(null),
                Position = float3.zero,
                BoundsRadius = 1f,
                LoadDistance = outer,
                UnloadDistance = outer * marginRatio,
                InnerLoadDistance = inner,
                InnerUnloadDistance = inner / marginRatio,
                EstimatedBytes = 1024L
            };
        }
    }
}
