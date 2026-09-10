using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV3ModelTests
    {
        [Test]
        public void Capture_ReelDeltaZero_DoesNotIncreaseProgress()
        {
            FishingV3Model model = CreateZoneModel(0.5f);

            model.Tick(FishingV3FishState.Fight, 0f, 0.1f);

            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void Capture_SafePositiveReelDelta_IncreasesProgress()
        {
            FishingV3Model model = CreateZoneModel(0.5f, 0.25f);

            model.Tick(FishingV3FishState.Fight, 0.4f, 0.1f);

            Assert.That(model.CaptureProgressNormalized, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void Capture_SlackPositiveReelDelta_DoesNotIncreaseProgress()
        {
            FishingV3Model model = CreateZoneModel(0.1f);

            model.Tick(FishingV3FishState.Fight, 0.4f, 0.1f);

            Assert.That(model.TensionZone, Is.EqualTo(FishingV3TensionZone.Slack));
            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void Capture_LowPositiveReelDelta_DoesNotIncreaseProgress()
        {
            FishingV3Model model = CreateZoneModel(0.2f);

            model.Tick(FishingV3FishState.Fight, 0.4f, 0.1f);

            Assert.That(model.TensionZone, Is.EqualTo(FishingV3TensionZone.Low));
            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void Capture_HighPositiveReelDelta_DoesNotIncreaseProgress()
        {
            FishingV3Model model = CreateZoneModel(0.8f);

            model.Tick(FishingV3FishState.Fight, 0.4f, 0.1f);

            Assert.That(model.TensionZone, Is.EqualTo(FishingV3TensionZone.High));
            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void Capture_DangerPositiveReelDelta_DoesNotIncreaseProgress()
        {
            FishingV3Model model = CreateZoneModel(0.95f);

            model.Tick(FishingV3FishState.Fight, 0.4f, 0.1f);

            Assert.That(model.TensionZone, Is.EqualTo(FishingV3TensionZone.Danger));
            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void Capture_SameTotalReelDelta_IsIndependentOfFrameSlicingWhenSafe()
        {
            FishingV3Model whole = CreateZoneModel(0.5f, 0.4f);
            FishingV3Model sliced = CreateZoneModel(0.5f, 0.4f);

            whole.Tick(FishingV3FishState.Fight, 1f, 1f);
            for (int i = 0; i < 10; i++)
            {
                sliced.Tick(FishingV3FishState.Fight, 0.1f, 0.1f);
            }

            Assert.That(sliced.CaptureProgressNormalized,
                Is.EqualTo(whole.CaptureProgressNormalized).Within(0.0001f));
        }

        [Test]
        public void Capture_ProgressNeverDecreases()
        {
            FishingV3Model model = CreateZoneModel(0.5f, 0.5f);
            model.Tick(FishingV3FishState.Fight, 0.5f, 0.1f);
            float captured = model.CaptureProgressNormalized;

            model.Tick(FishingV3FishState.Fight, -2f, 1f);
            model.Tick(FishingV3FishState.Fight, 0f, 1f);

            Assert.That(model.CaptureProgressNormalized, Is.EqualTo(captured));
        }

        [Test]
        public void Capture_ProgressAtOne_ProducesCaughtResult()
        {
            FishingV3Model model = CreateZoneModel(0.5f, 1f);

            model.Tick(FishingV3FishState.Fight, 1f, 0.1f);

            Assert.That(model.CaptureProgressNormalized, Is.EqualTo(1f));
            Assert.That(model.Result, Is.EqualTo(FishingV3Result.Caught));
        }

        [Test]
        public void Capture_ProgressStaysNormalized()
        {
            FishingV3Model model = CreateZoneModel(0.5f, 10f);

            model.Tick(FishingV3FishState.Fight, 10f, 0.1f);

            Assert.That(model.CaptureProgressNormalized, Is.InRange(0f, 1f));
        }

        [Test]
        public void Reel_SamePositiveRateAcrossDeltaTime_ProducesSameTargetContribution()
        {
            FishingV3Tuning tuning = new FishingV3Tuning
            {
                ReferenceReelRate = 2f,
                ReelTensionGain = 0.3f
            };
            FishingV3Model shortFrame = new FishingV3Model(tuning);
            FishingV3Model longFrame = new FishingV3Model(tuning);

            shortFrame.Tick(FishingV3FishState.Fight, 0.1f, 0.1f);
            longFrame.Tick(FishingV3FishState.Fight, 0.2f, 0.2f);

            Assert.That(shortFrame.ReelRateNormalized,
                Is.EqualTo(longFrame.ReelRateNormalized).Within(0.0001f));
            Assert.That(shortFrame.TargetTensionNormalized,
                Is.EqualTo(longFrame.TargetTensionNormalized).Within(0.0001f));
        }

        [Test]
        public void Reel_HigherRate_DoesNotLowerTargetTension()
        {
            FishingV3Model lowRate = new FishingV3Model();
            FishingV3Model highRate = new FishingV3Model();

            lowRate.Tick(FishingV3FishState.Fight, 0.02f, 0.1f);
            highRate.Tick(FishingV3FishState.Fight, 0.08f, 0.1f);

            Assert.That(highRate.TargetTensionNormalized,
                Is.GreaterThanOrEqualTo(lowRate.TargetTensionNormalized));
        }

        [Test]
        public void Tension_AlwaysStaysNormalized()
        {
            FishingV3Model model = new FishingV3Model(new FishingV3Tuning
            {
                RunBaseTension = 1f,
                ReelTensionGain = float.MaxValue,
                TensionRisePerSecond = float.MaxValue
            });

            model.Tick(FishingV3FishState.Run, float.MaxValue, float.Epsilon);

            AssertFiniteNormalized(model.TargetTensionNormalized);
            AssertFiniteNormalized(model.TensionNormalized);
            AssertFiniteNormalized(model.ReelRateNormalized);
        }

        [Test]
        public void Break_ShortDangerExposure_DoesNotBreakLineImmediately()
        {
            FishingV3Model model = CreateZoneModel(0.95f);

            model.Tick(FishingV3FishState.Run, 0f, 0.1f);

            Assert.That(model.BreakStressNormalized, Is.GreaterThan(0f));
            Assert.That(model.BreakStressNormalized, Is.LessThan(1f));
            Assert.That(model.Result, Is.EqualTo(FishingV3Result.Active));
        }

        [Test]
        public void Break_SustainedDanger_ProducesLineBroken()
        {
            FishingV3Model model = CreateZoneModel(0.95f);

            Step(model, FishingV3FishState.Run, 0f, 2.1f, 0.1f);

            Assert.That(model.BreakStressNormalized, Is.EqualTo(1f));
            Assert.That(model.Result, Is.EqualTo(FishingV3Result.LineBroken));
        }

        [Test]
        public void Break_LeavingDanger_RecoversStress()
        {
            FishingV3Model model = new FishingV3Model(CreateTransitionTuning());
            model.Tick(FishingV3FishState.Run, 0f, 0.4f);
            float dangerStress = model.BreakStressNormalized;

            model.Tick(FishingV3FishState.Fight, 0f, 0.2f);

            Assert.That(model.TensionZone, Is.EqualTo(FishingV3TensionZone.Safe));
            Assert.That(model.BreakStressNormalized, Is.LessThan(dangerStress));
        }

        [Test]
        public void Escape_ShortSlackExposure_DoesNotEscapeImmediately()
        {
            FishingV3Model model = CreateZoneModel(0.1f);

            model.Tick(FishingV3FishState.Calm, 0f, 0.1f);

            Assert.That(model.EscapeRiskNormalized, Is.GreaterThan(0f));
            Assert.That(model.EscapeRiskNormalized, Is.LessThan(1f));
            Assert.That(model.Result, Is.EqualTo(FishingV3Result.Active));
        }

        [Test]
        public void Escape_SustainedSlack_ProducesFishEscaped()
        {
            FishingV3Model model = CreateZoneModel(0.1f);

            Step(model, FishingV3FishState.Calm, 0f, 2.1f, 0.1f);

            Assert.That(model.EscapeRiskNormalized, Is.EqualTo(1f));
            Assert.That(model.Result, Is.EqualTo(FishingV3Result.FishEscaped));
        }

        [Test]
        public void Escape_LeavingSlack_RecoversRisk()
        {
            FishingV3Tuning tuning = CreateTransitionTuning();
            tuning.InitialTensionNormalized = 0.1f;
            tuning.CalmBaseTension = 0.1f;
            FishingV3Model model = new FishingV3Model(tuning);
            model.Tick(FishingV3FishState.Calm, 0f, 0.4f);
            float slackRisk = model.EscapeRiskNormalized;

            model.Tick(FishingV3FishState.Fight, 0f, 0.2f);

            Assert.That(model.TensionZone, Is.EqualTo(FishingV3TensionZone.Safe));
            Assert.That(model.EscapeRiskNormalized, Is.LessThan(slackRisk));
        }

        [Test]
        public void Terminal_CaughtResultCannotChange()
        {
            FishingV3Model model = CreateCaughtModel();

            model.Tick(FishingV3FishState.Run, float.MaxValue, 10f);

            Assert.That(model.Result, Is.EqualTo(FishingV3Result.Caught));
        }

        [Test]
        public void Terminal_LineBrokenResultCannotChange()
        {
            FishingV3Model model = CreateZoneModel(0.95f);
            Step(model, FishingV3FishState.Run, 0f, 2.1f, 0.1f);

            model.Tick(FishingV3FishState.Fight, 100f, 1f);

            Assert.That(model.Result, Is.EqualTo(FishingV3Result.LineBroken));
            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void Terminal_FishEscapedResultCannotChange()
        {
            FishingV3Model model = CreateZoneModel(0.1f);
            Step(model, FishingV3FishState.Calm, 0f, 2.1f, 0.1f);

            model.Tick(FishingV3FishState.Fight, 100f, 1f);

            Assert.That(model.Result, Is.EqualTo(FishingV3Result.FishEscaped));
            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void InvalidDeltaTime_RemainsFiniteAndDoesNotCapture()
        {
            float[] invalidDeltaTimes = { 0f, -0.1f, float.NaN, float.PositiveInfinity };
            FishingV3Model model = CreateZoneModel(0.5f);

            foreach (float deltaTime in invalidDeltaTimes)
            {
                model.Tick(FishingV3FishState.Fight, 1f, deltaTime);
                AssertFiniteNormalized(model.TargetTensionNormalized);
                AssertFiniteNormalized(model.TensionNormalized);
                AssertFiniteNormalized(model.CaptureProgressNormalized);
                AssertFiniteNormalized(model.BreakStressNormalized);
                AssertFiniteNormalized(model.EscapeRiskNormalized);
            }

            Assert.That(model.CaptureProgressNormalized, Is.Zero);
            Assert.That(model.Result, Is.EqualTo(FishingV3Result.Active));
        }

        [Test]
        public void NegativeReelDelta_DoesNotIncreaseCapture()
        {
            FishingV3Model model = CreateZoneModel(0.5f);

            model.Tick(FishingV3FishState.Fight, -1f, 0.1f);

            Assert.That(model.CaptureProgressNormalized, Is.Zero);
        }

        [Test]
        public void NegativeReelDelta_DoesNotCreatePositiveTensionContribution()
        {
            FishingV3Model zero = new FishingV3Model();
            FishingV3Model negative = new FishingV3Model();

            zero.Tick(FishingV3FishState.Fight, 0f, 0.1f);
            negative.Tick(FishingV3FishState.Fight, -1f, 0.1f);

            Assert.That(negative.ReelRateNormalized, Is.Zero);
            Assert.That(negative.TargetTensionNormalized,
                Is.EqualTo(zero.TargetTensionNormalized));
        }

        [Test]
        public void FishStates_UseOrderedBaseTensions()
        {
            FishingV3Model calm = new FishingV3Model();
            FishingV3Model fight = new FishingV3Model();
            FishingV3Model run = new FishingV3Model();

            calm.Tick(FishingV3FishState.Calm, 0f, 0.1f);
            fight.Tick(FishingV3FishState.Fight, 0f, 0.1f);
            run.Tick(FishingV3FishState.Run, 0f, 0.1f);

            Assert.That(calm.TargetTensionNormalized,
                Is.LessThan(fight.TargetTensionNormalized));
            Assert.That(fight.TargetTensionNormalized,
                Is.LessThan(run.TargetTensionNormalized));
        }

        [Test]
        public void TensionZone_UsesContiguousInclusiveBoundaries()
        {
            FishingV3Tuning tuning = new FishingV3Tuning();
            FishingV3Model model = new FishingV3Model(tuning);

            Assert.That(model.ClassifyTension(0f), Is.EqualTo(FishingV3TensionZone.Slack));
            Assert.That(model.ClassifyTension(tuning.SlackUpperThreshold),
                Is.EqualTo(FishingV3TensionZone.Low));
            Assert.That(model.ClassifyTension(tuning.LowUpperThreshold),
                Is.EqualTo(FishingV3TensionZone.Safe));
            Assert.That(model.ClassifyTension(tuning.SafeUpperThreshold),
                Is.EqualTo(FishingV3TensionZone.High));
            Assert.That(model.ClassifyTension(tuning.HighUpperThreshold),
                Is.EqualTo(FishingV3TensionZone.Danger));
            Assert.That(model.ClassifyTension(1f), Is.EqualTo(FishingV3TensionZone.Danger));
        }

        [Test]
        public void Smoothing_UsesSeparateRiseAndFallRates()
        {
            FishingV3Tuning tuning = new FishingV3Tuning
            {
                InitialTensionNormalized = 0.4f,
                CalmBaseTension = 0.2f,
                RunBaseTension = 0.8f,
                ReelTensionGain = 0f,
                TensionRisePerSecond = 0.2f,
                TensionFallPerSecond = 0.1f
            };
            FishingV3Model model = new FishingV3Model(tuning);

            model.Tick(FishingV3FishState.Run, 0f, 0.5f);
            Assert.That(model.TensionNormalized, Is.EqualTo(0.5f).Within(0.0001f));

            model.Tick(FishingV3FishState.Calm, 0f, 0.5f);
            Assert.That(model.TensionNormalized, Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void Smoothing_SameElapsedTime_IsIndependentOfFrameSlicing()
        {
            FishingV3Tuning tuning = new FishingV3Tuning
            {
                InitialTensionNormalized = 0.35f,
                RunBaseTension = 0.9f,
                ReelTensionGain = 0f,
                TensionRisePerSecond = 0.2f
            };
            FishingV3Model whole = new FishingV3Model(tuning);
            FishingV3Model sliced = new FishingV3Model(tuning);

            whole.Tick(FishingV3FishState.Run, 0f, 1f);
            for (int i = 0; i < 10; i++)
            {
                sliced.Tick(FishingV3FishState.Run, 0f, 0.1f);
            }

            Assert.That(sliced.TensionNormalized,
                Is.EqualTo(whole.TensionNormalized).Within(0.0001f));
        }

        private static FishingV3Model CreateZoneModel(float tension, float captureScale = 0.25f)
        {
            return new FishingV3Model(new FishingV3Tuning
            {
                InitialTensionNormalized = tension,
                CalmBaseTension = tension,
                FightBaseTension = tension,
                RunBaseTension = tension,
                ReelTensionGain = 0f,
                TensionRisePerSecond = 100f,
                TensionFallPerSecond = 100f,
                CaptureScale = captureScale
            });
        }

        private static FishingV3Tuning CreateTransitionTuning()
        {
            return new FishingV3Tuning
            {
                InitialTensionNormalized = 0.95f,
                CalmBaseTension = 0.1f,
                FightBaseTension = 0.5f,
                RunBaseTension = 0.95f,
                ReelTensionGain = 0f,
                TensionRisePerSecond = 100f,
                TensionFallPerSecond = 100f
            };
        }

        private static FishingV3Model CreateCaughtModel()
        {
            FishingV3Model model = CreateZoneModel(0.5f, 1f);
            model.Tick(FishingV3FishState.Fight, 1f, 0.1f);
            Assert.That(model.Result, Is.EqualTo(FishingV3Result.Caught));
            return model;
        }

        private static void Step(
            FishingV3Model model,
            FishingV3FishState state,
            float reelDelta,
            float seconds,
            float stepSeconds)
        {
            int steps = (int)(seconds / stepSeconds);
            for (int i = 0; i < steps && model.Result == FishingV3Result.Active; i++)
            {
                model.Tick(state, reelDelta, stepSeconds);
            }
        }

        private static void AssertFiniteNormalized(float value)
        {
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
            Assert.That(value, Is.InRange(0f, 1f));
        }
    }
}
