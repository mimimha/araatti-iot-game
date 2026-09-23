using System.Collections.Generic;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3FishBehaviorTests
    {
        [Test]
        public void Reset_StartsInValidCalmPhaseWithinConfiguredDuration()
        {
            FishingV3FishBehavior behavior = new FishingV3FishBehavior();

            Assert.That(behavior.State, Is.EqualTo(FishingV3FishState.Calm));
            Assert.That(behavior.PhaseRemainingSeconds, Is.InRange(2f, 4f));
            Assert.That(behavior.HeadShakeEventSequence, Is.Zero);
        }

        [Test]
        public void FixedDurations_TransitionThroughAllThreeStatesWithoutRepeatingForever()
        {
            FishingV3FishBehavior behavior = new FishingV3FishBehavior(FixedTuning());
            HashSet<FishingV3FishState> reached = new HashSet<FishingV3FishState>
            {
                behavior.State
            };

            behavior.Tick(2f);
            reached.Add(behavior.State);
            Assert.That(behavior.State, Is.EqualTo(FishingV3FishState.Fight));

            behavior.Tick(2f);
            reached.Add(behavior.State);
            Assert.That(behavior.State, Is.EqualTo(FishingV3FishState.Run));

            behavior.Tick(1f);
            reached.Add(behavior.State);
            Assert.That(behavior.State, Is.EqualTo(FishingV3FishState.Fight));

            behavior.Tick(2f);
            reached.Add(behavior.State);
            Assert.That(behavior.State, Is.EqualTo(FishingV3FishState.Calm));
            Assert.That(reached, Is.EquivalentTo(new[]
            {
                FishingV3FishState.Calm,
                FishingV3FishState.Fight,
                FishingV3FishState.Run
            }));
        }

        [Test]
        public void RunHeadShake_IsOneShotOverlayEventAndDoesNotChangeMainState()
        {
            FishingV3FishBehaviorTuning tuning = FixedTuning();
            tuning.RunHeadShakeDelayMinSeconds = 0.5f;
            tuning.RunHeadShakeDelayMaxSeconds = 0.5f;
            tuning.HeadShakeIntensityMinNormalized = 0.8f;
            tuning.HeadShakeIntensityMaxNormalized = 0.8f;
            FishingV3FishBehavior behavior = new FishingV3FishBehavior(tuning);
            behavior.SetState(FishingV3FishState.Run);

            behavior.Tick(0.49f);
            Assert.That(behavior.HeadShakeEventSequence, Is.Zero);

            behavior.Tick(0.01f);

            Assert.That(behavior.State, Is.EqualTo(FishingV3FishState.Run));
            Assert.That(behavior.HeadShakeEventSequence, Is.EqualTo(1));
            Assert.That(behavior.LastHeadShakeIntensityNormalized,
                Is.EqualTo(0.8f).Within(0.000001f));
        }

        [Test]
        public void AutomaticState_UsesExistingModelTargetTensionWithoutHeadShakeModification()
        {
            FishingV3Tuning modelTuning = new FishingV3Tuning
            {
                InitialTensionNormalized = 0.5f,
                CalmBaseTension = 0.2f,
                FightBaseTension = 0.5f,
                RunBaseTension = 0.8f,
                ReelTensionGain = 0f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
            FishingV3FishBehaviorTuning behaviorTuning = FixedTuning();
            behaviorTuning.CalmDurationMinSeconds = 0.1f;
            behaviorTuning.CalmDurationMaxSeconds = 0.1f;
            behaviorTuning.FightDurationMinSeconds = 0.1f;
            behaviorTuning.FightDurationMaxSeconds = 0.1f;
            behaviorTuning.RunDurationMinSeconds = 1f;
            behaviorTuning.RunDurationMaxSeconds = 1f;
            behaviorTuning.RunHeadShakeDelayMinSeconds = 0.1f;
            behaviorTuning.RunHeadShakeDelayMaxSeconds = 0.1f;
            FishingV3Runtime runtime = CreateRuntime(modelTuning, behaviorTuning);
            runtime.Begin();

            float calmTarget = runtime.Current.TargetTensionNormalized;
            runtime.Tick(Frame(0f), 0.1f);
            float fightTarget = runtime.Current.TargetTensionNormalized;
            runtime.Tick(Frame(0f), 0.2f);
            float runTargetAtHeadShake = runtime.Current.TargetTensionNormalized;

            Assert.That(runtime.Current.FishState, Is.EqualTo(FishingV3FishState.Run));
            Assert.That(runtime.Current.HeadShakeEventSequence, Is.EqualTo(1));
            Assert.That(calmTarget, Is.LessThan(fightTarget));
            Assert.That(fightTarget, Is.LessThan(runTargetAtHeadShake));
            Assert.That(runTargetAtHeadShake, Is.EqualTo(0.8f).Within(0.000001f));
        }

        [Test]
        public void TerminalResult_StopsBehaviorSchedulerAndRemainsSticky()
        {
            FishingV3Tuning tuning = new FishingV3Tuning
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
            FishingV3Runtime runtime = CreateRuntime(tuning, FixedTuning());
            runtime.Begin();
            runtime.Tick(Frame(1f), 1f);
            FishingV3FishState terminalState = runtime.Current.FishState;
            int terminalSequence = runtime.Current.HeadShakeEventSequence;

            runtime.Tick(Frame(0f), 30f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(runtime.State, Is.EqualTo(FishingV3RuntimeState.Completed));
            Assert.That(runtime.Current.FishState, Is.EqualTo(terminalState));
            Assert.That(runtime.Current.HeadShakeEventSequence, Is.EqualTo(terminalSequence));
        }

        [TestCase(FishingV3FishState.Calm)]
        [TestCase(FishingV3FishState.Fight)]
        [TestCase(FishingV3FishState.Run)]
        public void StatePattern_ChangesTargetTensionOverTime(
            FishingV3FishState state)
        {
            FishingV3Runtime runtime = CreateRuntime(
                VariableModelTuning(),
                VariableBehaviorTuning());
            runtime.Begin();
            runtime.SetFishState(state);
            float minimum = 1f;
            float maximum = 0f;

            for (int i = 0; i < 12; i++)
            {
                runtime.Tick(Frame(0f), 0.1f);
                minimum = System.Math.Min(
                    minimum,
                    runtime.Current.TargetTensionNormalized);
                maximum = System.Math.Max(
                    maximum,
                    runtime.Current.TargetTensionNormalized);
            }

            Assert.That(maximum - minimum, Is.GreaterThan(0.005f));
        }

        [TestCase(FishingV3FishState.Calm, 0.045f)]
        [TestCase(FishingV3FishState.Fight, 0.14f)]
        [TestCase(FishingV3FishState.Run, 0.18f)]
        public void StatePattern_RemainsWithinConfiguredOffsetBounds(
            FishingV3FishState state,
            float maximumOffset)
        {
            FishingV3FishBehavior behavior =
                new FishingV3FishBehavior(VariableBehaviorTuning());
            behavior.SetState(state);

            for (int i = 0; i < 20; i++)
            {
                behavior.Tick(0.1f);
                Assert.That(System.Math.Abs(behavior.TensionOffsetNormalized),
                    Is.LessThanOrEqualTo(maximumOffset + 0.000001f));
            }
        }

        [Test]
        public void RepresentativeMeanTargetTension_PreservesStateOrdering()
        {
            float calm = AverageTarget(FishingV3FishState.Calm);
            float fight = AverageTarget(FishingV3FishState.Fight);
            float run = AverageTarget(FishingV3FishState.Run);

            Assert.That(calm, Is.LessThan(fight));
            Assert.That(fight, Is.LessThan(run));
        }

        [Test]
        public void FightAndRunPullBursts_AreSmoothPositiveAndRunIsStronger()
        {
            FishingV3FishBehaviorTuning tuning = VariableBehaviorTuning();
            tuning.CalmOscillationAmplitudeNormalized = 0f;
            tuning.FightOscillationAmplitudeNormalized = 0f;
            tuning.RunOscillationAmplitudeNormalized = 0f;
            tuning.FightPullBurstAmplitudeNormalized = 0.07f;
            tuning.RunPullBurstAmplitudeNormalized = 0.11f;
            tuning.FightPullBurstDurationSeconds = 0.4f;
            tuning.RunPullBurstDurationSeconds = 0.4f;
            tuning.FightPullBurstDelayMinSeconds = 0.2f;
            tuning.FightPullBurstDelayMaxSeconds = 0.2f;
            tuning.RunPullBurstDelayMinSeconds = 0.2f;
            tuning.RunPullBurstDelayMaxSeconds = 0.2f;
            FishingV3FishBehavior fight = new FishingV3FishBehavior(tuning);
            FishingV3FishBehavior run = new FishingV3FishBehavior(tuning);
            fight.SetState(FishingV3FishState.Fight);
            run.SetState(FishingV3FishState.Run);

            fight.Tick(0.4f);
            run.Tick(0.4f);

            Assert.That(fight.PullBurstOffsetNormalized, Is.GreaterThan(0f));
            Assert.That(run.PullBurstOffsetNormalized,
                Is.GreaterThan(fight.PullBurstOffsetNormalized));

            fight.Tick(0.4f);
            run.Tick(0.4f);
            Assert.That(fight.PullBurstOffsetNormalized, Is.Zero.Within(0.000001f));
            Assert.That(run.PullBurstOffsetNormalized, Is.Zero.Within(0.000001f));
        }

        [Test]
        public void SameSeedAndElapsedTime_ProduceIdenticalBehavior()
        {
            FishingV3FishBehavior whole =
                new FishingV3FishBehavior(VariableBehaviorTuning());
            FishingV3FishBehavior sliced =
                new FishingV3FishBehavior(VariableBehaviorTuning());
            whole.SetState(FishingV3FishState.Fight);
            sliced.SetState(FishingV3FishState.Fight);

            whole.Tick(0.8f);
            for (int i = 0; i < 8; i++) sliced.Tick(0.1f);

            Assert.That(sliced.State, Is.EqualTo(whole.State));
            Assert.That(sliced.TensionOffsetNormalized,
                Is.EqualTo(whole.TensionOffsetNormalized).Within(0.000001f));
            Assert.That(sliced.PullBurstOffsetNormalized,
                Is.EqualTo(whole.PullBurstOffsetNormalized).Within(0.000001f));
            Assert.That(sliced.HeadShakeEventSequence,
                Is.EqualTo(whole.HeadShakeEventSequence));
        }

        [Test]
        public void ReelContribution_RemainsAdditiveToIdenticalBehaviorPattern()
        {
            FishingV3Runtime idleReel = CreateRuntime(
                VariableModelTuning(),
                VariableBehaviorTuning());
            FishingV3Runtime activeReel = CreateRuntime(
                VariableModelTuning(),
                VariableBehaviorTuning());
            idleReel.Begin();
            activeReel.Begin();
            idleReel.SetFishState(FishingV3FishState.Fight);
            activeReel.SetFishState(FishingV3FishState.Fight);

            idleReel.Tick(Frame(0f), 0.2f);
            activeReel.Tick(Frame(0.5f), 0.2f);

            Assert.That(activeReel.Current.BehaviorTensionOffsetNormalized,
                Is.EqualTo(idleReel.Current.BehaviorTensionOffsetNormalized)
                    .Within(0.000001f));
            Assert.That(activeReel.Current.TargetTensionNormalized,
                Is.GreaterThan(idleReel.Current.TargetTensionNormalized));
        }

        [Test]
        public void DefaultRun_ContinuousVirtualReel_ReachesLineBrokenAfterSustainedDanger()
        {
            FishingV3Runtime runtime = CreateFailureRuntime();
            runtime.Begin();
            runtime.SetFishState(FishingV3FishState.Run);
            float runDuration = runtime.Current.FishStateRemainingSeconds;

            float elapsed = TickUntilTerminal(runtime, Frame(1f), 3f, 0.01f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.LineBroken));
            Assert.That(elapsed, Is.GreaterThan(1f));
            Assert.That(elapsed, Is.LessThan(runDuration));
            Assert.That(runtime.Current.BreakStressNormalized, Is.EqualTo(1f));
        }

        [Test]
        public void DefaultRun_ReelReleaseRecoversBreakStressAndAvoidsLineBroken()
        {
            FishingV3Runtime runtime = CreateFailureRuntime(LongPhaseTuning());
            runtime.Begin();
            runtime.SetFishState(FishingV3FishState.Run);

            TickFor(runtime, Frame(1f), 0.8f, 0.02f);
            float stressBeforeRelease = runtime.Current.BreakStressNormalized;
            TickFor(runtime, Frame(0f), 4f, 0.02f);

            Assert.That(stressBeforeRelease, Is.GreaterThan(0f));
            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Active));
            Assert.That(runtime.Current.BreakStressNormalized,
                Is.LessThan(stressBeforeRelease));
            Assert.That(runtime.Current.BreakStressNormalized,
                Is.Zero.Within(0.000001f));
        }

        [TestCase(FishingV3FishState.Calm)]
        [TestCase(FishingV3FishState.Fight)]
        public void CalmAndFight_ContinuousVirtualReel_DoNotBecomeLineBreakTraps(
            FishingV3FishState state)
        {
            FishingV3Runtime runtime = CreateFailureRuntime(LongPhaseTuning());
            runtime.Begin();
            runtime.SetFishState(state);

            TickFor(runtime, Frame(1f), 5f, 0.02f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Active));
            Assert.That(runtime.Current.BreakStressNormalized,
                Is.Zero.Within(0.000001f));
            Assert.That(runtime.Current.TensionZone,
                Is.Not.EqualTo(FishingV3TensionZone.Danger));
        }

        [Test]
        public void DefaultCalm_WithoutReel_ReachesFishEscapedAfterSustainedSlack()
        {
            FishingV3Runtime runtime = CreateFailureRuntime();
            runtime.Begin();
            float calmDuration = runtime.Current.FishStateRemainingSeconds;

            float elapsed = TickUntilTerminal(runtime, Frame(0f), 3f, 0.01f);

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.FishEscaped));
            Assert.That(elapsed, Is.GreaterThan(1f));
            Assert.That(elapsed, Is.LessThan(calmDuration));
            Assert.That(runtime.Current.EscapeRiskNormalized, Is.EqualTo(1f));
        }

        private static FishingV3Runtime CreateRuntime(
            FishingV3Tuning modelTuning,
            FishingV3FishBehaviorTuning behaviorTuning)
        {
            return new FishingV3Runtime(
                modelTuning,
                new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                },
                behaviorTuning);
        }

        private static FishingV3Runtime CreateFailureRuntime(
            FishingV3FishBehaviorTuning behaviorTuning = null)
        {
            FishingV3Tuning tuning = new FishingV3Tuning
            {
                CaptureScale = 0f
            };
            return new FishingV3Runtime(
                tuning,
                new FishingV3ReelInputTuning(),
                behaviorTuning ?? new FishingV3FishBehaviorTuning());
        }

        private static FishingV3FishBehaviorTuning LongPhaseTuning()
        {
            return new FishingV3FishBehaviorTuning
            {
                CalmDurationMinSeconds = 10f,
                CalmDurationMaxSeconds = 10f,
                FightDurationMinSeconds = 10f,
                FightDurationMaxSeconds = 10f,
                RunDurationMinSeconds = 10f,
                RunDurationMaxSeconds = 10f
            };
        }

        private static float TickUntilTerminal(
            FishingV3Runtime runtime,
            FishingInputFrame frame,
            float maximumSeconds,
            float stepSeconds)
        {
            float elapsed = 0f;
            while (runtime.Result == FishingV3Result.Active &&
                   elapsed < maximumSeconds)
            {
                runtime.Tick(frame, stepSeconds);
                elapsed += stepSeconds;
            }
            return elapsed;
        }

        private static void TickFor(
            FishingV3Runtime runtime,
            FishingInputFrame frame,
            float seconds,
            float stepSeconds)
        {
            int steps = (int)(seconds / stepSeconds);
            for (int i = 0;
                 i < steps && runtime.Result == FishingV3Result.Active;
                 i++)
            {
                runtime.Tick(frame, stepSeconds);
            }
        }

        private static FishingV3FishBehaviorTuning FixedTuning()
        {
            return new FishingV3FishBehaviorTuning
            {
                CalmDurationMinSeconds = 2f,
                CalmDurationMaxSeconds = 2f,
                FightDurationMinSeconds = 2f,
                FightDurationMaxSeconds = 2f,
                RunDurationMinSeconds = 1f,
                RunDurationMaxSeconds = 1f,
                RunHeadShakeDelayMinSeconds = 0.25f,
                RunHeadShakeDelayMaxSeconds = 0.25f,
                HeadShakeIntensityMinNormalized = 1f,
                HeadShakeIntensityMaxNormalized = 1f,
                CalmOscillationAmplitudeNormalized = 0f,
                FightOscillationAmplitudeNormalized = 0f,
                RunOscillationAmplitudeNormalized = 0f,
                FightPullBurstAmplitudeNormalized = 0f,
                RunPullBurstAmplitudeNormalized = 0f,
                RandomSeed = 11
            };
        }

        private static FishingV3FishBehaviorTuning VariableBehaviorTuning()
        {
            return new FishingV3FishBehaviorTuning
            {
                CalmDurationMinSeconds = 10f,
                CalmDurationMaxSeconds = 10f,
                FightDurationMinSeconds = 10f,
                FightDurationMaxSeconds = 10f,
                RunDurationMinSeconds = 10f,
                RunDurationMaxSeconds = 10f,
                CalmOscillationAmplitudeNormalized = 0.045f,
                CalmOscillationFrequencyHz = 0.22f,
                FightOscillationAmplitudeNormalized = 0.065f,
                FightOscillationFrequencyHz = 0.65f,
                RunOscillationAmplitudeNormalized = 0.08f,
                RunOscillationFrequencyHz = 1.05f,
                FightPullBurstAmplitudeNormalized = 0.075f,
                FightPullBurstDurationSeconds = 0.65f,
                FightPullBurstDelayMinSeconds = 0.6f,
                FightPullBurstDelayMaxSeconds = 0.6f,
                RunPullBurstAmplitudeNormalized = 0.1f,
                RunPullBurstDurationSeconds = 0.75f,
                RunPullBurstDelayMinSeconds = 0.25f,
                RunPullBurstDelayMaxSeconds = 0.25f,
                RunHeadShakeDelayMinSeconds = 0.5f,
                RunHeadShakeDelayMaxSeconds = 0.5f,
                RandomSeed = 29
            };
        }

        private static FishingV3Tuning VariableModelTuning()
        {
            return new FishingV3Tuning
            {
                InitialTensionNormalized = 0.5f,
                CalmBaseTension = 0.2f,
                FightBaseTension = 0.45f,
                RunBaseTension = 0.75f,
                ReelTensionGain = 0.25f,
                TensionRisePerSecond = 10f,
                TensionFallPerSecond = 10f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f,
                CaptureScale = 0f
            };
        }

        private static float AverageTarget(FishingV3FishState state)
        {
            FishingV3Runtime runtime = CreateRuntime(
                VariableModelTuning(),
                VariableBehaviorTuning());
            runtime.Begin();
            runtime.SetFishState(state);
            float sum = 0f;
            const int sampleCount = 20;
            for (int i = 0; i < sampleCount; i++)
            {
                runtime.Tick(Frame(0f), 0.1f);
                sum += runtime.Current.TargetTensionNormalized;
            }
            return sum / sampleCount;
        }

        private static FishingInputFrame Frame(float normalizedReelInput)
        {
            return new FishingInputFrame
            {
                ReelDelta = normalizedReelInput,
                IsDeviceConnected = true
            };
        }
    }
}
