using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3TimingReelTests
    {
        [Test]
        public void Pointer_PingPongIsDeltaTimeBasedAndFrameSliceIndependent()
        {
            FishingV3TimingReelTuning tuning = UniformTuning();
            tuning.CalmPointerSpeedNormalizedPerSecond = 0.8f;
            FishingV3TimingReel whole = new FishingV3TimingReel(tuning);
            FishingV3TimingReel sliced = new FishingV3TimingReel(tuning);

            whole.Tick(FishingV3FishState.Calm, 2f);
            for (int index = 0; index < 20; index++)
            {
                sliced.Tick(FishingV3FishState.Calm, 0.1f);
            }

            Assert.That(whole.PointerNormalized, Is.EqualTo(0.4f).Within(0.000001f));
            Assert.That(sliced.PointerNormalized,
                Is.EqualTo(whole.PointerNormalized).Within(0.000001f));
        }

        [Test]
        public void FishState_DrivesIncreasingSpeedAndNarrowingWindows()
        {
            FishingV3TimingReel meter = new FishingV3TimingReel();

            Assert.That(meter.GetPointerSpeed(FishingV3FishState.Calm),
                Is.LessThan(meter.GetPointerSpeed(FishingV3FishState.Fight)));
            Assert.That(meter.GetPointerSpeed(FishingV3FishState.Fight),
                Is.LessThan(meter.GetPointerSpeed(FishingV3FishState.Run)));
            Assert.That(meter.GetGoodHalfWidth(FishingV3FishState.Calm),
                Is.GreaterThan(meter.GetGoodHalfWidth(FishingV3FishState.Fight)));
            Assert.That(meter.GetGoodHalfWidth(FishingV3FishState.Fight),
                Is.GreaterThan(meter.GetGoodHalfWidth(FishingV3FishState.Run)));
            Assert.That(meter.GetPerfectHalfWidth(FishingV3FishState.Calm),
                Is.GreaterThan(meter.GetPerfectHalfWidth(FishingV3FishState.Fight)));
            Assert.That(meter.GetPerfectHalfWidth(FishingV3FishState.Fight),
                Is.GreaterThan(meter.GetPerfectHalfWidth(FishingV3FishState.Run)));
        }

        [Test]
        public void Perfect_ProducesConfiguredLargeCanonicalReelDelta()
        {
            FishingV3TimingReel meter = MeterAt(0.5f);

            FishingV3TimingJudgement result = meter.Judge(FishingV3FishState.Calm);

            Assert.That(result.Grade, Is.EqualTo(FishingV3TimingGrade.Perfect));
            Assert.That(result.ReelInput.ReelDeltaRevolutions,
                Is.EqualTo(0.16f).Within(0.000001f));
        }

        [Test]
        public void Good_ProducesConfiguredSmallCanonicalReelDelta()
        {
            FishingV3TimingReel meter = MeterAt(0.65f);

            FishingV3TimingJudgement result = meter.Judge(FishingV3FishState.Calm);

            Assert.That(result.Grade, Is.EqualTo(FishingV3TimingGrade.Good));
            Assert.That(result.ReelInput.ReelDeltaRevolutions,
                Is.EqualTo(0.08f).Within(0.000001f));
        }

        [Test]
        public void PerfectAndGood_AddOrderedSuccessfulReelSupport()
        {
            FishingV3TimingReel perfect = MeterAt(0.5f);
            FishingV3TimingReel good = MeterAt(0.65f);

            perfect.Judge(FishingV3FishState.Calm);
            good.Judge(FishingV3FishState.Calm);

            Assert.That(perfect.SuccessfulReelSupportNormalized,
                Is.EqualTo(0.10f).Within(0.000001f));
            Assert.That(good.SuccessfulReelSupportNormalized,
                Is.EqualTo(0.05f).Within(0.000001f));
            Assert.That(perfect.SuccessfulReelSupportNormalized,
                Is.GreaterThan(good.SuccessfulReelSupportNormalized));
        }

        [Test]
        public void Miss_DoesNotAddSuccessfulReelSupport()
        {
            FishingV3TimingReel meter = MeterAt(0.05f);

            FishingV3TimingJudgement result = meter.Judge(FishingV3FishState.Calm);

            Assert.That(result.Grade, Is.EqualTo(FishingV3TimingGrade.Miss));
            Assert.That(meter.SuccessfulReelSupportNormalized, Is.Zero);
        }

        [Test]
        public void SuccessfulReelSupport_DecayIsDeltaTimeBasedAndFrameSliceIndependent()
        {
            FishingV3TimingReel whole = MeterAt(0.5f);
            FishingV3TimingReel sliced = MeterAt(0.5f);
            whole.Judge(FishingV3FishState.Calm);
            sliced.Judge(FishingV3FishState.Calm);

            whole.Tick(FishingV3FishState.Calm, 1f);
            for (int index = 0; index < 10; index++)
            {
                sliced.Tick(FishingV3FishState.Calm, 0.1f);
            }

            Assert.That(whole.SuccessfulReelSupportNormalized,
                Is.EqualTo(0.075f).Within(0.000001f));
            Assert.That(sliced.SuccessfulReelSupportNormalized,
                Is.EqualTo(whole.SuccessfulReelSupportNormalized).Within(0.000001f));
        }

        [Test]
        public void SuccessfulReelSupport_IsCappedAndSameOpportunitySpamAddsNone()
        {
            FishingV3TimingReelTuning tuning = UniformTuning();
            tuning.SuccessfulReelSupportDecayPerSecond = 0f;
            FishingV3TimingReel meter = new FishingV3TimingReel(tuning);
            meter.Tick(FishingV3FishState.Calm, 0.5f);

            meter.Judge(FishingV3FishState.Calm);
            float afterFirst = meter.SuccessfulReelSupportNormalized;
            FishingV3TimingJudgement spam = meter.Judge(FishingV3FishState.Calm);

            Assert.That(spam.WasAccepted, Is.False);
            Assert.That(meter.SuccessfulReelSupportNormalized,
                Is.EqualTo(afterFirst).Within(0.000001f));

            for (int index = 0; index < 4; index++)
            {
                meter.Tick(FishingV3FishState.Calm, 1f);
                meter.Judge(FishingV3FishState.Calm);
            }

            Assert.That(meter.SuccessfulReelSupportNormalized,
                Is.EqualTo(0.12f).Within(0.000001f));
        }

        [Test]
        public void SameOpportunity_OnlyFirstInputCanProduceReelDelta()
        {
            FishingV3TimingReel meter = MeterAt(0.5f);

            FishingV3TimingJudgement first = meter.Judge(FishingV3FishState.Calm);
            FishingV3TimingJudgement spam = meter.Judge(FishingV3FishState.Calm);

            Assert.That(first.WasAccepted, Is.True);
            Assert.That(first.Grade, Is.EqualTo(FishingV3TimingGrade.Perfect));
            Assert.That(first.ReelInput.ReelDeltaRevolutions,
                Is.EqualTo(0.16f).Within(0.000001f));
            Assert.That(spam.WasAccepted, Is.False);
            Assert.That(spam.Grade, Is.EqualTo(FishingV3TimingGrade.Miss));
            Assert.That(spam.ReelInput.ReelDeltaRevolutions, Is.Zero);
            Assert.That(meter.MissPenaltyNormalized,
                Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(meter.SuccessfulReelSupportNormalized,
                Is.EqualTo(0.10f).Within(0.000001f));
        }

        [Test]
        public void EndpointReflection_OpensNextTimingOpportunity()
        {
            FishingV3TimingReel meter = MeterAt(0.5f);
            meter.Judge(FishingV3FishState.Calm);
            int before = meter.OpportunitySequence;

            meter.Tick(FishingV3FishState.Calm, 0.85f);
            FishingV3TimingJudgement next = meter.Judge(FishingV3FishState.Calm);

            Assert.That(meter.OpportunitySequence, Is.EqualTo(before + 1));
            Assert.That(next.WasAccepted, Is.True);
            Assert.That(next.Grade, Is.EqualTo(FishingV3TimingGrade.Good));
            Assert.That(next.ReelInput.ReelDeltaRevolutions,
                Is.EqualTo(0.08f).Within(0.000001f));
        }

        [Test]
        public void RunState_StillAllowsAccurateTimingInput()
        {
            FishingV3TimingReel meter = MeterAt(0.5f);

            FishingV3TimingJudgement result = meter.Judge(FishingV3FishState.Run);

            Assert.That(result.WasAccepted, Is.True);
            Assert.That(result.Grade, Is.EqualTo(FishingV3TimingGrade.Perfect));
            Assert.That(result.ReelInput.ReelDeltaRevolutions,
                Is.EqualTo(0.16f).Within(0.000001f));
        }

        [Test]
        public void Miss_ProducesZeroReelDeltaAndBoundedRecoveringPenalty()
        {
            FishingV3TimingReel meter = MeterAt(0.05f);

            FishingV3TimingJudgement result = meter.Judge(FishingV3FishState.Calm);
            for (int index = 0; index < 10; index++)
            {
                meter.Judge(FishingV3FishState.Calm);
            }

            Assert.That(result.Grade, Is.EqualTo(FishingV3TimingGrade.Miss));
            Assert.That(result.ReelInput.ReelDeltaRevolutions, Is.Zero);
            Assert.That(meter.MissPenaltyNormalized,
                Is.EqualTo(0.22f).Within(0.000001f));

            meter.Tick(FishingV3FishState.Calm, 1f);

            Assert.That(meter.MissPenaltyNormalized, Is.Zero);
        }

        [Test]
        public void TimingMode_PerfectAndGoodAdvanceExistingCapturePath()
        {
            FishingV3TimingReelTuning timing = UniformTuning();
            timing.PerfectReelDeltaRevolutions = 0.2f;
            timing.GoodReelDeltaRevolutions = 0.1f;
            FishingV3Runtime runtime = RunningTimingRuntime(timing);

            runtime.Tick(Frame(), 0.5f);
            runtime.Tick(Frame(timingPressed: true), 0.001f);
            float afterPerfect = runtime.Current.CaptureProgressNormalized;
            runtime.Tick(Frame(), 0.849f);
            runtime.Tick(Frame(timingPressed: true), 0.001f);

            Assert.That(afterPerfect, Is.EqualTo(0.2f).Within(0.000001f));
            Assert.That(runtime.Current.LastTimingGrade,
                Is.EqualTo(FishingV3TimingGrade.Good));
            Assert.That(runtime.Current.CaptureProgressNormalized,
                Is.EqualTo(0.3f).Within(0.000001f));
        }

        [Test]
        public void AccuratePattern_OutperformsMashUnderSameSeedAndDuration()
        {
            PatternOutcome accurate = SimulateSafePattern(mash: false, durationSeconds: 8f);
            PatternOutcome mash = SimulateSafePattern(mash: true, durationSeconds: 8f);

            Assert.That(accurate.Result, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(accurate.Capture, Is.GreaterThan(mash.Capture));
            Assert.That(accurate.Misses, Is.LessThan(mash.Misses));
            Assert.That(accurate.FinalMissPenalty, Is.LessThan(mash.FinalMissPenalty));
            Assert.That(accurate.MaximumTension, Is.LessThan(mash.MaximumTension));
            Assert.That(mash.Capture, Is.Zero);
        }

        [Test]
        public void SustainedMashInRun_UsesExistingDangerStressToBreakLine()
        {
            FishingV3Tuning model = StableTuning();
            model.InitialTensionNormalized = 0.75f;
            model.CalmBaseTension = 0.75f;
            model.FightBaseTension = 0.75f;
            model.RunBaseTension = 0.75f;
            model.BreakStressPerSecond = 0.7f;
            model.BreakStressRecoveryPerSecond = 0.25f;
            FishingV3Runtime runtime = RunningTimingRuntime(
                UniformTuning(),
                model,
                FishingV3FishState.Run);

            for (int index = 0;
                 index < 400 && runtime.Result == FishingV3Result.Active;
                 index++)
            {
                runtime.Tick(Frame(timingPressed: true), 0.01f);
            }

            Assert.That(runtime.Current.TimingMissPenaltyNormalized,
                Is.GreaterThan(0f));
            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.LineBroken));
        }

        [Test]
        public void PassivePattern_UsesExistingSlackRiskToEscape()
        {
            FishingV3Tuning model = StableTuning();
            model.InitialTensionNormalized = 0.05f;
            model.CalmBaseTension = 0.05f;
            model.FightBaseTension = 0.05f;
            model.RunBaseTension = 0.05f;
            model.EscapeRiskPerSecond = 0.5f;
            model.EscapeRiskRecoveryPerSecond = 0.4f;
            FishingV3Runtime runtime = RunningTimingRuntime(
                UniformTuning(),
                model,
                FishingV3FishState.Calm);

            for (int index = 0;
                 index < 300 && runtime.Result == FishingV3Result.Active;
                 index++)
            {
                runtime.Tick(Frame(), 0.01f);
            }

            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.FishEscaped));
        }

        [Test]
        public void Reset_ClearsConsumedOpportunityState()
        {
            FishingV3TimingReel meter = MeterAt(0.5f);
            meter.Judge(FishingV3FishState.Calm);
            Assert.That(meter.HasJudgedCurrentOpportunity, Is.True);

            meter.Reset();

            Assert.That(meter.HasJudgedCurrentOpportunity, Is.False);
            Assert.That(meter.OpportunitySequence, Is.Zero);
            Assert.That(meter.JudgementSequence, Is.Zero);
        }

        [Test]
        public void TimingMode_IgnoresLegacyHoldWithoutTimingPress()
        {
            FishingV3Runtime runtime = RunningTimingRuntime(UniformTuning());

            runtime.Tick(Frame(reel: 1f), 0.5f);

            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
            Assert.That(runtime.Current.LastTimingGrade,
                Is.EqualTo(FishingV3TimingGrade.None));
        }

        [Test]
        public void MissPenalty_EntersExistingTargetTensionContributionOnly()
        {
            FishingV3TimingReelTuning timing = UniformTuning();
            timing.MissPenaltyPerInputNormalized = 0.1f;
            timing.MaximumMissPenaltyNormalized = 0.2f;
            FishingV3Runtime runtime = RunningTimingRuntime(timing);

            runtime.Tick(Frame(timingPressed: true), 0.1f);

            Assert.That(runtime.Current.LastTimingGrade,
                Is.EqualTo(FishingV3TimingGrade.Miss));
            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
            Assert.That(runtime.Current.TimingMissPenaltyNormalized,
                Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(runtime.Current.TargetTensionNormalized,
                Is.EqualTo(0.6f).Within(0.000001f));
        }

        [Test]
        public void SlackPerfect_RaisesTargetTensionTowardSafeWithoutChangingThresholds()
        {
            FishingV3Tuning model = StableTuning();
            model.InitialTensionNormalized = 0.2f;
            model.CalmBaseTension = 0.2f;
            model.FightBaseTension = 0.2f;
            model.RunBaseTension = 0.2f;
            FishingV3Runtime runtime = RunningTimingRuntime(UniformTuning(), model);

            runtime.Tick(Frame(), 0.5f);
            runtime.Tick(Frame(timingPressed: true), 0.001f);

            Assert.That(runtime.Current.LastTimingGrade,
                Is.EqualTo(FishingV3TimingGrade.Perfect));
            Assert.That(runtime.Current.SuccessfulReelSupportNormalized,
                Is.EqualTo(0.10f).Within(0.0001f));
            Assert.That(runtime.Current.TargetTensionNormalized,
                Is.EqualTo(0.30f).Within(0.0001f));
            Assert.That(runtime.Current.TensionNormalized, Is.GreaterThan(0.2f));
            Assert.That(runtime.Current.TensionZone,
                Is.EqualTo(FishingV3TensionZone.Slack));
        }

        [Test]
        public void SlackGood_RaisesTargetLessThanPerfect()
        {
            FishingV3Tuning model = StableTuning();
            model.InitialTensionNormalized = 0.2f;
            model.CalmBaseTension = 0.2f;
            model.FightBaseTension = 0.2f;
            model.RunBaseTension = 0.2f;
            FishingV3Runtime runtime = RunningTimingRuntime(UniformTuning(), model);

            runtime.Tick(Frame(), 0.65f);
            runtime.Tick(Frame(timingPressed: true), 0.001f);

            Assert.That(runtime.Current.LastTimingGrade,
                Is.EqualTo(FishingV3TimingGrade.Good));
            Assert.That(runtime.Current.SuccessfulReelSupportNormalized,
                Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(runtime.Current.TargetTensionNormalized,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(runtime.Current.TargetTensionNormalized, Is.LessThan(0.30f));
        }

        [Test]
        public void HighPressure_SuccessfulSupportCannotSustainDangerAfterReelingStops()
        {
            FishingV3Tuning model = StableTuning();
            model.InitialTensionNormalized = 0.8f;
            model.CalmBaseTension = 0.8f;
            model.FightBaseTension = 0.8f;
            model.RunBaseTension = 0.8f;
            model.ReelTensionGain = 0.25f;
            FishingV3Runtime runtime = RunningTimingRuntime(UniformTuning(), model);

            runtime.Tick(Frame(), 0.5f);
            runtime.Tick(Frame(timingPressed: true), 0.001f);
            float reelingTarget = runtime.Current.TargetTensionNormalized;
            runtime.Tick(Frame(), 0.1f);

            Assert.That(reelingTarget, Is.GreaterThanOrEqualTo(0.85f));
            Assert.That(runtime.Current.SuccessfulReelSupportNormalized,
                Is.GreaterThan(0f));
            Assert.That(runtime.Current.TargetTensionNormalized, Is.LessThan(0.85f));
        }

        [Test]
        public void TerminalResult_StopsTimingAndNewSessionResetsIt()
        {
            FishingV3TimingReelTuning timing = UniformTuning();
            FishingV3Tuning model = StableTuning();
            model.CaptureScale = 10f;
            FishingV3Runtime runtime = new FishingV3Runtime(
                model,
                null,
                StableBehavior(),
                timing,
                FishingV3ReelControlMode.Timing);
            runtime.Begin();
            runtime.SetFishState(FishingV3FishState.Calm);
            runtime.Tick(Frame(), 0.5f);
            runtime.Tick(Frame(timingPressed: true), 0.001f);
            float terminalPointer = runtime.Current.TimingPointerNormalized;
            int terminalJudgementSequence = runtime.Current.TimingJudgementSequence;

            runtime.Tick(Frame(timingPressed: true), 1f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Completed));
            Assert.That(runtime.Current.SuccessfulReelSupportNormalized, Is.Zero);
            Assert.That(runtime.Current.TimingPointerNormalized,
                Is.EqualTo(terminalPointer));
            Assert.That(runtime.Current.TimingJudgementSequence,
                Is.EqualTo(terminalJudgementSequence));

            runtime.Begin();

            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Running));
            Assert.That(runtime.Current.TimingPointerNormalized, Is.Zero);
            Assert.That(runtime.Current.LastTimingGrade,
                Is.EqualTo(FishingV3TimingGrade.None));
            Assert.That(runtime.Current.TimingMissPenaltyNormalized, Is.Zero);
            Assert.That(runtime.Current.SuccessfulReelSupportNormalized, Is.Zero);
        }

        [Test]
        public void Abort_ClearsSuccessfulReelSupportImmediately()
        {
            FishingV3Runtime runtime = RunningTimingRuntime(UniformTuning());
            runtime.Tick(Frame(), 0.5f);
            runtime.Tick(Frame(timingPressed: true), 0.001f);
            Assert.That(runtime.Current.SuccessfulReelSupportNormalized,
                Is.GreaterThan(0f));

            runtime.Abort();

            Assert.That(runtime.Current.SuccessfulReelSupportNormalized, Is.Zero);
        }

        [TestCase(FishingV3Result.LineBroken, 0.95f)]
        [TestCase(FishingV3Result.FishEscaped, 0.05f)]
        public void TimingMode_PreservesExistingFailureResults(
            FishingV3Result expected,
            float tension)
        {
            FishingV3Tuning model = StableTuning();
            model.InitialTensionNormalized = tension;
            model.CalmBaseTension = tension;
            model.FightBaseTension = tension;
            model.RunBaseTension = tension;
            model.BreakStressPerSecond = expected == FishingV3Result.LineBroken ? 2f : 0f;
            model.EscapeRiskPerSecond = expected == FishingV3Result.FishEscaped ? 2f : 0f;
            FishingV3Runtime runtime = new FishingV3Runtime(
                model,
                null,
                StableBehavior(),
                UniformTuning(),
                FishingV3ReelControlMode.Timing);
            runtime.Begin();

            runtime.Tick(Frame(), 0.5f);

            Assert.That(runtime.Result, Is.EqualTo(expected));
            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Completed));
        }

        private static FishingV3TimingReel MeterAt(float pointer)
        {
            FishingV3TimingReel meter = new FishingV3TimingReel(UniformTuning());
            meter.Tick(FishingV3FishState.Calm, pointer);
            return meter;
        }

        private static FishingV3Runtime RunningTimingRuntime(
            FishingV3TimingReelTuning timing,
            FishingV3Tuning model = null,
            FishingV3FishState fishState = FishingV3FishState.Calm)
        {
            FishingV3Runtime runtime = new FishingV3Runtime(
                model ?? StableTuning(),
                null,
                StableBehavior(),
                timing,
                FishingV3ReelControlMode.Timing);
            runtime.Begin();
            runtime.SetFishState(fishState);
            return runtime;
        }

        private static PatternOutcome SimulateSafePattern(bool mash, float durationSeconds)
        {
            const float deltaTime = 0.01f;
            FishingV3Runtime runtime = RunningTimingRuntime(UniformTuning());
            bool accurateInputArmed = true;
            int lastJudgementSequence = 0;
            int misses = 0;
            float maximumTension = runtime.Current.TensionNormalized;
            int tickCount = (int)(durationSeconds / deltaTime);

            for (int index = 0;
                 index < tickCount && runtime.Result == FishingV3Result.Active;
                 index++)
            {
                float pointer = runtime.Current.TimingPointerNormalized;
                if (!accurateInputArmed && (pointer <= 0.02f || pointer >= 0.98f))
                {
                    accurateInputArmed = true;
                }

                bool accuratePress = accurateInputArmed &&
                    pointer >= 0.49f && pointer <= 0.51f;
                bool pressed = mash || accuratePress;
                if (accuratePress) accurateInputArmed = false;

                runtime.Tick(Frame(timingPressed: pressed), deltaTime);
                maximumTension = System.Math.Max(
                    maximumTension,
                    runtime.Current.TensionNormalized);
                if (runtime.Current.TimingJudgementSequence > lastJudgementSequence)
                {
                    if (runtime.Current.LastTimingGrade == FishingV3TimingGrade.Miss)
                    {
                        misses++;
                    }
                    lastJudgementSequence = runtime.Current.TimingJudgementSequence;
                }
            }

            return new PatternOutcome(
                runtime.Current.CaptureProgressNormalized,
                runtime.Current.TimingMissPenaltyNormalized,
                maximumTension,
                misses,
                runtime.Result);
        }

        private static FishingV3TimingReelTuning UniformTuning()
        {
            return new FishingV3TimingReelTuning
            {
                CalmPointerSpeedNormalizedPerSecond = 1f,
                FightPointerSpeedNormalizedPerSecond = 1f,
                RunPointerSpeedNormalizedPerSecond = 1f,
                CalmPerfectHalfWidthNormalized = 0.05f,
                FightPerfectHalfWidthNormalized = 0.05f,
                RunPerfectHalfWidthNormalized = 0.05f,
                CalmGoodHalfWidthNormalized = 0.2f,
                FightGoodHalfWidthNormalized = 0.2f,
                RunGoodHalfWidthNormalized = 0.2f,
                PerfectReelDeltaRevolutions = 0.16f,
                GoodReelDeltaRevolutions = 0.08f,
                PerfectSuccessfulReelSupportNormalized = 0.10f,
                GoodSuccessfulReelSupportNormalized = 0.05f,
                MaximumSuccessfulReelSupportNormalized = 0.12f,
                SuccessfulReelSupportDecayPerSecond = 0.025f,
                MissPenaltyPerInputNormalized = 0.1f,
                MaximumMissPenaltyNormalized = 0.22f,
                MissPenaltyRecoveryPerSecond = 0.28f
            };
        }

        private static FishingV3Tuning StableTuning()
        {
            return new FishingV3Tuning
            {
                InitialTensionNormalized = 0.5f,
                CalmBaseTension = 0.5f,
                FightBaseTension = 0.5f,
                RunBaseTension = 0.5f,
                ReelTensionGain = 0f,
                TensionRisePerSecond = 10f,
                TensionFallPerSecond = 10f,
                CaptureScale = 1f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
        }

        private static FishingV3FishBehaviorTuning StableBehavior()
        {
            return new FishingV3FishBehaviorTuning
            {
                CalmDurationMinSeconds = 100f,
                CalmDurationMaxSeconds = 100f,
                FightDurationMinSeconds = 100f,
                FightDurationMaxSeconds = 100f,
                RunDurationMinSeconds = 100f,
                RunDurationMaxSeconds = 100f,
                CalmOscillationAmplitudeNormalized = 0f,
                FightOscillationAmplitudeNormalized = 0f,
                RunOscillationAmplitudeNormalized = 0f,
                FightPullBurstAmplitudeNormalized = 0f,
                RunPullBurstAmplitudeNormalized = 0f
            };
        }

        private static FishingInputFrame Frame(
            float reel = 0f,
            bool timingPressed = false)
        {
            return new FishingInputFrame
            {
                ReelDelta = reel,
                TimingPressed = timingPressed,
                IsDeviceConnected = true
            };
        }

        private readonly struct PatternOutcome
        {
            public float Capture { get; }
            public float FinalMissPenalty { get; }
            public float MaximumTension { get; }
            public int Misses { get; }
            public FishingV3Result Result { get; }

            public PatternOutcome(
                float capture,
                float finalMissPenalty,
                float maximumTension,
                int misses,
                FishingV3Result result)
            {
                Capture = capture;
                FinalMissPenalty = finalMissPenalty;
                MaximumTension = maximumTension;
                Misses = misses;
                Result = result;
            }
        }
    }
}
