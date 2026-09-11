using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3RuntimeTests
    {
        [Test]
        public void NewRuntime_StartsReadyWithResetModelState()
        {
            FishingV3Runtime runtime = CreateRuntime(StableSafeTuning());

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Ready));
            Assert.That(runtime.Current.FishState, Is.EqualTo(FishingV3FishState.Calm));
            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
            Assert.That(runtime.Current.BreakStressNormalized, Is.Zero);
            Assert.That(runtime.Current.EscapeRiskNormalized, Is.Zero);
            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Active));
        }

        [Test]
        public void Begin_EnablesRuntimeProgression()
        {
            FishingV3Runtime runtime = CreateRuntime(StableSafeTuning());

            runtime.Begin();
            runtime.SetFishState(FishingV3FishState.Fight);
            runtime.Tick(Frame(0.5f), 0.5f);

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Running));
            Assert.That(runtime.Current.CaptureProgressNormalized, Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void ZeroNormalizedInput_DoesNotIncreaseCapture()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();

            runtime.Tick(Frame(0f), 0.5f);

            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void PositiveNormalizedInput_ReachesModelThroughAdapter()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();

            runtime.Tick(Frame(0.5f), 0.5f);

            Assert.That(runtime.Current.CaptureProgressNormalized, Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void SameInputDuration_IsFrameSliceIndependentInStableSafeZone()
        {
            FishingV3Runtime whole = RunningSafeRuntime();
            FishingV3Runtime sliced = RunningSafeRuntime();

            whole.Tick(Frame(0.4f), 1f);
            for (int i = 0; i < 10; i++) sliced.Tick(Frame(0.4f), 0.1f);

            Assert.That(sliced.Current.CaptureProgressNormalized,
                Is.EqualTo(whole.Current.CaptureProgressNormalized).Within(0.000001f));
        }

        [TestCase(FishingV3FishState.Calm)]
        [TestCase(FishingV3FishState.Fight)]
        [TestCase(FishingV3FishState.Run)]
        public void FishState_CanBeSetExternally(FishingV3FishState state)
        {
            FishingV3Runtime runtime = RunningSafeRuntime();

            runtime.SetFishState(state);
            runtime.Tick(Frame(0f), 0.1f);

            Assert.That(runtime.Current.FishState, Is.EqualTo(state));
        }

        [Test]
        public void FishState_DoesNotTransitionAutomatically()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();
            runtime.SetFishState(FishingV3FishState.Run);

            for (int i = 0; i < 20; i++) runtime.Tick(Frame(0f), 0.1f);

            Assert.That(runtime.Current.FishState, Is.EqualTo(FishingV3FishState.Run));
        }

        [Test]
        public void SufficientSafeReeling_ProducesCaughtResult()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();

            runtime.Tick(Frame(1f), 1f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Completed));
        }

        [Test]
        public void SustainedDanger_ProducesLineBrokenResult()
        {
            FishingV3Tuning tuning = StableSafeTuning();
            tuning.InitialTensionNormalized = 0.95f;
            tuning.CalmBaseTension = 0.95f;
            tuning.FightBaseTension = 0.95f;
            tuning.RunBaseTension = 0.95f;
            tuning.BreakStressPerSecond = 2f;
            FishingV3Runtime runtime = CreateRuntime(tuning);
            runtime.Begin();

            runtime.Tick(Frame(0f), 0.5f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.LineBroken));
        }

        [Test]
        public void SustainedSlack_ProducesFishEscapedResult()
        {
            FishingV3Tuning tuning = StableSafeTuning();
            tuning.InitialTensionNormalized = 0.05f;
            tuning.CalmBaseTension = 0.05f;
            tuning.FightBaseTension = 0.05f;
            tuning.RunBaseTension = 0.05f;
            tuning.EscapeRiskPerSecond = 2f;
            FishingV3Runtime runtime = CreateRuntime(tuning);
            runtime.Begin();

            runtime.Tick(Frame(0f), 0.5f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.FishEscaped));
        }

        [Test]
        public void TerminalResult_IsStickyAcrossAdditionalTicks()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();
            runtime.Tick(Frame(1f), 1f);
            FishingV3Snapshot terminal = runtime.Current;

            runtime.Tick(Frame(0f), 10f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Caught));
            AssertSnapshotProgress(runtime.Current, terminal);
        }

        [Test]
        public void Pause_FreezesAllModelProgression()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();
            runtime.Tick(Frame(0.2f), 0.5f);
            runtime.SetPaused(true);
            FishingV3Snapshot paused = runtime.Current;

            runtime.Tick(Frame(1f), 2f);

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Paused));
            AssertSnapshotProgress(runtime.Current, paused);
        }

        [Test]
        public void Resume_ContinuesExistingSessionProgression()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();
            runtime.Tick(Frame(0.2f), 0.5f);
            runtime.SetPaused(true);
            float beforeResume = runtime.Current.CaptureProgressNormalized;

            runtime.SetPaused(false);
            runtime.Tick(Frame(0.2f), 0.5f);

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Running));
            Assert.That(runtime.Current.CaptureProgressNormalized, Is.GreaterThan(beforeResume));
        }

        [Test]
        public void Abort_PreventsFurtherProgression()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();
            runtime.Abort();
            FishingV3Snapshot aborted = runtime.Current;

            runtime.Tick(Frame(1f), 1f);

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Aborted));
            AssertSnapshotProgress(runtime.Current, aborted);
        }

        [Test]
        public void Shutdown_PreventsFurtherProgression()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();
            runtime.Shutdown();
            FishingV3Snapshot shutdown = runtime.Current;

            runtime.Begin();
            runtime.Tick(Frame(1f), 1f);

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Shutdown));
            AssertSnapshotProgress(runtime.Current, shutdown);
        }

        [Test]
        public void Reset_RestoresInitialSessionValues()
        {
            FishingV3Runtime runtime = RunningSafeRuntime();
            runtime.Tick(Frame(0.4f), 0.5f);

            runtime.Reset();

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Ready));
            Assert.That(runtime.Current.FishState, Is.EqualTo(FishingV3FishState.Calm));
            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
            Assert.That(runtime.Current.BreakStressNormalized, Is.Zero);
            Assert.That(runtime.Current.EscapeRiskNormalized, Is.Zero);
            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Active));
        }

        private static FishingV3Runtime RunningSafeRuntime()
        {
            FishingV3Runtime runtime = CreateRuntime(StableSafeTuning());
            runtime.Begin();
            runtime.SetFishState(FishingV3FishState.Fight);
            return runtime;
        }

        private static FishingV3Runtime CreateRuntime(FishingV3Tuning tuning)
        {
            return new FishingV3Runtime(tuning, new FishingV3ReelInputTuning
            {
                VirtualReelSpeedRevolutionsPerSecond = 1f
            });
        }

        private static FishingV3Tuning StableSafeTuning()
        {
            return new FishingV3Tuning
            {
                InitialTensionNormalized = 0.5f,
                CalmBaseTension = 0.5f,
                FightBaseTension = 0.5f,
                RunBaseTension = 0.5f,
                ReelTensionGain = 0f,
                CaptureScale = 1f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
        }

        private static FishingInputFrame Frame(float normalizedReelInput)
        {
            return new FishingInputFrame
            {
                ReelDelta = normalizedReelInput,
                IsDeviceConnected = true
            };
        }

        private static void AssertSnapshotProgress(
            FishingV3Snapshot actual,
            FishingV3Snapshot expected)
        {
            Assert.That(actual.FishState, Is.EqualTo(expected.FishState));
            Assert.That(actual.TargetTensionNormalized, Is.EqualTo(expected.TargetTensionNormalized));
            Assert.That(actual.TensionNormalized, Is.EqualTo(expected.TensionNormalized));
            Assert.That(actual.TensionZone, Is.EqualTo(expected.TensionZone));
            Assert.That(actual.CaptureProgressNormalized, Is.EqualTo(expected.CaptureProgressNormalized));
            Assert.That(actual.BreakStressNormalized, Is.EqualTo(expected.BreakStressNormalized));
            Assert.That(actual.EscapeRiskNormalized, Is.EqualTo(expected.EscapeRiskNormalized));
            Assert.That(actual.Result, Is.EqualTo(expected.Result));
        }
    }
}
