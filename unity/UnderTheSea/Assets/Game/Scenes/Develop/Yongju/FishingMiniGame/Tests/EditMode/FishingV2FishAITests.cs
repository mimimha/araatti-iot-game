using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV2FishAITests
    {
        [Test]
        public void Reset_StartsFightAndClearsSignals()
        {
            FishingV2FishAI ai = new FishingV2FishAI(NoEventsTuning(), 11);
            FishingV2FishAIOutput output = ai.Current;

            Assert.That(output.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Fight));
            Assert.That(output.IsRunTelegraphing, Is.False);
            Assert.That(output.HeadShakeActive, Is.False);
            Assert.That(output.HeadShakeEventSequence, Is.Zero);
            Assert.That(output.FinalRunDecisionMade, Is.False);
            Assert.That(output.FinalRunPending, Is.False);
            Assert.That(output.FinalRunUsed, Is.False);
        }

        [Test]
        public void SameSeedAndInputs_ProduceIdenticalSequence()
        {
            FishingV2FishAI left = new FishingV2FishAI(new FishingV2FishAITuning(), 41, 0.7f);
            FishingV2FishAI right = new FishingV2FishAI(new FishingV2FishAITuning(), 41, 0.7f);

            for (int i = 0; i < 240; i++)
            {
                float stamina = 1f - i / 300f;
                float distance = 12f - i / 30f;
                FishingV2FishAIOutput a = left.Tick(0.05f, stamina, distance);
                FishingV2FishAIOutput b = right.Tick(0.05f, stamina, distance);
                AssertOutputsEqual(a, b);
            }
        }

        [Test]
        public void Fight_DoesNotChangeBeforeMinimumDuration()
        {
            FishingV2FishAITuning tuning = NoEventsTuning();
            tuning.FightMinDuration = tuning.FightMaxDuration = 1f;
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 3);

            ai.Tick(0.25f, 1f, 10f);
            ai.Tick(0.25f, 1f, 10f);
            ai.Tick(0.25f, 1f, 10f);

            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Fight));
            Assert.That(ai.Current.IsRunTelegraphing, Is.False);
        }

        [Test]
        public void TransitionConstraints_BlockRunToRunAndRestToRest()
        {
            FishingV2FishAI ai = new FishingV2FishAI(NoEventsTuning(), 5);
            FishingV2FishAIWeights afterRun = ai.GetTransitionWeights(
                FishingV2StaminaBand.High, FishingV2BehaviorState.Run,
                FishingV2BehaviorState.Fight, false);
            FishingV2FishAIWeights afterRest = ai.GetTransitionWeights(
                FishingV2StaminaBand.Low, FishingV2BehaviorState.Rest,
                FishingV2BehaviorState.Fight, false);

            Assert.That(afterRun.Run, Is.Zero);
            Assert.That(afterRest.Rest, Is.Zero);
        }

        [Test]
        public void RunAndRest_DoNotImmediatelyRepeatInActualTransitions()
        {
            FishingV2FishAITuning runTuning = ForcedRunTuning();
            FishingV2FishAI runAI = new FishingV2FishAI(runTuning, 5);
            EnterRun(runAI, 1f);
            for (int i = 0; i < 4 && runAI.Current.Behavior.State == FishingV2BehaviorState.Run; i++)
                runAI.Tick(0.25f, 1f, 10f);
            Assert.That(runAI.Current.Behavior.State, Is.Not.EqualTo(FishingV2BehaviorState.Run));
            Assert.That(runAI.Current.IsRunTelegraphing, Is.False);

            FishingV2FishAITuning restTuning = NoEventsTuning();
            restTuning.HighFightWeight = 0f;
            restTuning.HighRunWeight = 0f;
            restTuning.HighRestWeight = 1f;
            FishingV2FishAI restAI = new FishingV2FishAI(restTuning, 5);
            restAI.Tick(0.1f, 1f, 10f);
            Assert.That(restAI.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Rest));
            restAI.Tick(0.25f, 1f, 10f);
            restAI.Tick(0.25f, 1f, 10f);
            Assert.That(restAI.Current.Behavior.State, Is.Not.EqualTo(FishingV2BehaviorState.Rest));
        }

        [Test]
        public void StaminaWeights_FavorHighRunsAndLowRests()
        {
            FishingV2FishAI ai = new FishingV2FishAI(NoEventsTuning(), 7);
            FishingV2FishAIWeights high = ai.GetTransitionWeights(
                FishingV2StaminaBand.High, FishingV2BehaviorState.Fight,
                FishingV2BehaviorState.None, false);
            FishingV2FishAIWeights mid = ai.GetTransitionWeights(
                FishingV2StaminaBand.Mid, FishingV2BehaviorState.Fight,
                FishingV2BehaviorState.None, false);
            FishingV2FishAIWeights low = ai.GetTransitionWeights(
                FishingV2StaminaBand.Low, FishingV2BehaviorState.Fight,
                FishingV2BehaviorState.None, false);

            Assert.That(high.Run, Is.GreaterThan(low.Run));
            Assert.That(low.Rest, Is.GreaterThan(high.Rest));
            Assert.That(mid.Fight + mid.Run + mid.Rest, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void RunForce_IsStrongerAtHighStaminaThanLowStamina()
        {
            FishingV2FishAITuning tuning = ForcedRunTuning();
            FishingV2FishAI high = new FishingV2FishAI(tuning, 13);
            FishingV2FishAI low = new FishingV2FishAI(tuning, 13);

            EnterRun(high, 1f);
            EnterRun(low, 0.2f);

            Assert.That(high.Current.Behavior.ForceNormalized,
                Is.GreaterThan(low.Current.Behavior.ForceNormalized));
        }

        [Test]
        public void RunTelegraph_PrecedesRunAndPreservesDirection()
        {
            FishingV2FishAI ai = new FishingV2FishAI(ForcedRunTuning(), 17);

            ai.Tick(0.1f, 1f, 10f);
            FishingV2FishAIOutput telegraph = ai.Current;
            Assert.That(telegraph.IsRunTelegraphing, Is.True);
            Assert.That(telegraph.Behavior.State, Is.Not.EqualTo(FishingV2BehaviorState.Run));
            Assert.That(telegraph.RunTelegraphDirectionNormalized, Is.EqualTo(-1f).Or.EqualTo(1f));
            float direction = telegraph.RunTelegraphDirectionNormalized;

            ai.Tick(0.1f, 1f, 10f);
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Run));
            Assert.That(ai.Current.Behavior.DirectionNormalized, Is.EqualTo(direction));
            ai.Tick(0.04f, 1f, 10f);
            Assert.That(ai.Current.Behavior.DirectionNormalized, Is.EqualTo(direction));
        }

        [Test]
        public void HeadShake_IsTimedFightEventWithSingleSequenceIncrement()
        {
            FishingV2FishAITuning tuning = NoEventsTuning();
            tuning.FightMinDuration = tuning.FightMaxDuration = 2f;
            tuning.HeadShakeChance = 1f;
            tuning.HeadShakeDuration = 0.3f;
            tuning.HeadShakeCooldown = 0.5f;
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 19);

            AdvanceUntil(ai, output => output.HeadShakeActive, 60, 0.05f, 1f, 10f);
            int sequence = ai.Current.HeadShakeEventSequence;
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Fight));
            Assert.That(sequence, Is.EqualTo(1));

            ai.Tick(0.05f, 1f, 10f);
            Assert.That(ai.Current.HeadShakeActive, Is.True);
            Assert.That(ai.Current.HeadShakeEventSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void TelegraphAndHeadShake_NeverOverlap()
        {
            FishingV2FishAITuning tuning = ForcedRunTuning();
            tuning.FightMinDuration = tuning.FightMaxDuration = 0.8f;
            tuning.HeadShakeChance = 1f;
            tuning.HeadShakeDuration = 0.25f;
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 23);

            for (int i = 0; i < 100; i++)
            {
                FishingV2FishAIOutput output = ai.Tick(0.05f, 1f, 10f);
                Assert.That(output.HeadShakeActive && output.IsRunTelegraphing, Is.False);
            }
        }

        [Test]
        public void HeadShakeCooldown_PreventsImmediateSecondEvent()
        {
            FishingV2FishAITuning tuning = NoEventsTuning();
            tuning.FightMinDuration = tuning.FightMaxDuration = 0.4f;
            tuning.HighFightWeight = 1f;
            tuning.HighRunWeight = 0f;
            tuning.HighRestWeight = 0f;
            tuning.HeadShakeChance = 1f;
            tuning.HeadShakeDuration = 0.1f;
            tuning.HeadShakeCooldown = 1f;
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 29);

            AdvanceUntil(ai, output => output.HeadShakeEventSequence == 1,
                20, 0.05f, 1f, 10f);
            for (int i = 0; i < 10; i++) ai.Tick(0.05f, 1f, 10f);

            Assert.That(ai.Current.HeadShakeEventSequence, Is.EqualTo(1));
        }

        [Test]
        public void FinalRun_SuccessIsPendingDuringTelegraphThenUsedOnce()
        {
            FishingV2FishAITuning tuning = FinalRunTuning(0.99f);
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 1);

            for (int i = 0; i < 8; i++) ai.Tick(0.25f, 0.1f, 2f);
            Assert.That(ai.Current.FinalRunDecisionMade, Is.True);
            Assert.That(ai.Current.FinalRunPending, Is.True);
            Assert.That(ai.Current.IsRunTelegraphing, Is.True);
            Assert.That(ai.Current.Behavior.State, Is.Not.EqualTo(FishingV2BehaviorState.Run));
            float direction = ai.Current.RunTelegraphDirectionNormalized;

            ai.Tick(0.1f, 0.1f, 2f);
            Assert.That(ai.Current.FinalRunPending, Is.False);
            Assert.That(ai.Current.FinalRunUsed, Is.True);
            Assert.That(ai.Current.IsFinalRun, Is.True);
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Run));
            Assert.That(ai.Current.Behavior.DirectionNormalized, Is.EqualTo(direction));

            for (int i = 0; i < 30; i++) ai.Tick(0.1f, 0.1f, 2f);
            Assert.That(ai.Current.FinalRunDecisionMade, Is.True);
            Assert.That(ai.Current.FinalRunUsed, Is.True);
            Assert.That(ai.Current.FinalRunPending, Is.False);
        }

        [Test]
        public void FinalRun_FailedRollNeverRerolls()
        {
            FishingV2FishAI ai = new FishingV2FishAI(FinalRunTuning(0.01f), 1);

            for (int i = 0; i < 8; i++) ai.Tick(0.25f, 0.1f, 2f);
            Assert.That(ai.Current.FinalRunDecisionMade, Is.True);
            Assert.That(ai.Current.FinalRunPending, Is.False);
            Assert.That(ai.Current.FinalRunUsed, Is.False);

            for (int i = 0; i < 50; i++) ai.Tick(0.1f, 0.1f, 2f);
            Assert.That(ai.Current.FinalRunDecisionMade, Is.True);
            Assert.That(ai.Current.FinalRunPending, Is.False);
            Assert.That(ai.Current.FinalRunUsed, Is.False);
        }

        [Test]
        public void FinalRun_IsNotConsideredOutsideLowAndNearEligibility()
        {
            FishingV2FishAI high = new FishingV2FishAI(FinalRunTuning(0.99f), 1);
            FishingV2FishAI far = new FishingV2FishAI(FinalRunTuning(0.99f), 1);

            for (int i = 0; i < 8; i++)
            {
                high.Tick(0.25f, 0.8f, 2f);
                far.Tick(0.25f, 0.1f, 8f);
            }

            Assert.That(high.Current.FinalRunDecisionMade, Is.False);
            Assert.That(far.Current.FinalRunDecisionMade, Is.False);
        }

        [Test]
        public void FinalRun_DoesNotInterruptCurrentFightPhase()
        {
            FishingV2FishAITuning tuning = FinalRunTuning(0.99f);
            tuning.FightMinDuration = tuning.FightMaxDuration = 1f;
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 1);

            ai.Tick(0.25f, 1f, 10f);
            ai.Tick(0.25f, 0.1f, 2f);
            ai.Tick(0.25f, 0.1f, 2f);

            Assert.That(ai.Current.FinalRunDecisionMade, Is.False);
            Assert.That(ai.Current.FinalRunPending, Is.False);
            Assert.That(ai.Current.IsRunTelegraphing, Is.False);
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Fight));

            ai.Tick(0.25f, 0.1f, 2f);

            Assert.That(ai.Current.FinalRunDecisionMade, Is.True);
            Assert.That(ai.Current.FinalRunPending, Is.True);
            Assert.That(ai.Current.IsRunTelegraphing, Is.True);
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Fight));
        }

        [Test]
        public void NormalRun_DoesNotTransitionDirectlyIntoFinalRun()
        {
            FishingV2FishAITuning tuning = FinalRunTuning(0.99f);
            tuning.FightMinDuration = tuning.FightMaxDuration = 0.1f;
            tuning.RunMinDuration = tuning.RunMaxDuration = 0.2f;
            tuning.RestMinDuration = tuning.RestMaxDuration = 0.1f;
            tuning.HighFightWeight = 0f;
            tuning.HighRunWeight = 1f;
            tuning.HighRestWeight = 0f;
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 1);

            ai.Tick(0.1f, 1f, 10f);
            Assert.That(ai.Current.IsRunTelegraphing, Is.True);
            ai.Tick(0.1f, 1f, 10f);
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Run));

            ai.Tick(0.22f, 0.1f, 2f);

            Assert.That(ai.Current.FinalRunDecisionMade, Is.False);
            Assert.That(ai.Current.FinalRunPending, Is.False);
            Assert.That(ai.Current.IsRunTelegraphing, Is.False);
            Assert.That(ai.Current.Behavior.State,
                Is.EqualTo(FishingV2BehaviorState.Fight).Or.EqualTo(FishingV2BehaviorState.Rest));

            ai.Tick(0.25f, 0.1f, 2f);

            Assert.That(ai.Current.FinalRunDecisionMade, Is.True);
            Assert.That(ai.Current.FinalRunPending, Is.True);
            Assert.That(ai.Current.IsRunTelegraphing, Is.True);
        }

        [Test]
        public void PostFinalRun_ExcludesAnotherRunAndBiasesRest()
        {
            FishingV2FishAI ai = new FishingV2FishAI(FinalRunTuning(0.99f), 1);
            FishingV2FishAIWeights weights = ai.GetTransitionWeights(
                FishingV2StaminaBand.Low, FishingV2BehaviorState.Run,
                FishingV2BehaviorState.Rest, true);

            Assert.That(weights.Run, Is.Zero);
            Assert.That(weights.Rest, Is.GreaterThan(weights.Fight));
        }

        [Test]
        public void ResetAfterEvents_ClearsHeadShakeSequenceAndFinalRunFlags()
        {
            FishingV2FishAITuning tuning = FinalRunTuning(0.99f);
            FishingV2FishAI ai = new FishingV2FishAI(tuning, 1);
            for (int i = 0; i < 8; i++) ai.Tick(0.25f, 0.1f, 2f);

            ai.Reset(tuning, 1);

            Assert.That(ai.Current.HeadShakeEventSequence, Is.Zero);
            Assert.That(ai.Current.FinalRunDecisionMade, Is.False);
            Assert.That(ai.Current.FinalRunPending, Is.False);
            Assert.That(ai.Current.FinalRunUsed, Is.False);
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Fight));
        }

        private static FishingV2FishAITuning NoEventsTuning()
        {
            return new FishingV2FishAITuning
            {
                HeadShakeChance = 0f,
                FinalRunStaminaThreshold = 0f,
                FightMinDuration = 0.1f,
                FightMaxDuration = 0.1f,
                RunMinDuration = 0.5f,
                RunMaxDuration = 0.5f,
                RestMinDuration = 0.5f,
                RestMaxDuration = 0.5f,
                RunTelegraphMinDuration = 0.1f,
                RunTelegraphMaxDuration = 0.1f
            };
        }

        private static FishingV2FishAITuning ForcedRunTuning()
        {
            FishingV2FishAITuning tuning = NoEventsTuning();
            tuning.HighFightWeight = 0f;
            tuning.HighRunWeight = 1f;
            tuning.HighRestWeight = 0f;
            tuning.MidFightWeight = 0f;
            tuning.MidRunWeight = 1f;
            tuning.MidRestWeight = 0f;
            tuning.LowFightWeight = 0f;
            tuning.LowRunWeight = 1f;
            tuning.LowRestWeight = 0f;
            return tuning;
        }

        private static FishingV2FishAITuning FinalRunTuning(float chance)
        {
            FishingV2FishAITuning tuning = NoEventsTuning();
            tuning.FightMinDuration = tuning.FightMaxDuration = 2f;
            tuning.RunMinDuration = tuning.RunMaxDuration = 0.1f;
            tuning.RunTelegraphMinDuration = tuning.RunTelegraphMaxDuration = 0.1f;
            tuning.FinalRunChance = chance;
            tuning.FinalRunStaminaThreshold = 0.2f;
            tuning.FinalRunDistanceThreshold = 3f;
            return tuning;
        }

        private static void EnterRun(FishingV2FishAI ai, float stamina)
        {
            ai.Tick(0.1f, stamina, 10f);
            Assert.That(ai.Current.IsRunTelegraphing, Is.True);
            ai.Tick(0.1f, stamina, 10f);
            Assert.That(ai.Current.Behavior.State, Is.EqualTo(FishingV2BehaviorState.Run));
        }

        private static void AdvanceUntil(
            FishingV2FishAI ai,
            System.Func<FishingV2FishAIOutput, bool> predicate,
            int maxSteps,
            float dt,
            float stamina,
            float distance)
        {
            for (int i = 0; i < maxSteps && !predicate(ai.Current); i++)
                ai.Tick(dt, stamina, distance);
            Assert.That(predicate(ai.Current), Is.True);
        }

        private static void AssertOutputsEqual(FishingV2FishAIOutput left, FishingV2FishAIOutput right)
        {
            Assert.That(left.Behavior.State, Is.EqualTo(right.Behavior.State));
            Assert.That(left.Behavior.ForceNormalized, Is.EqualTo(right.Behavior.ForceNormalized));
            Assert.That(left.Behavior.DirectionNormalized, Is.EqualTo(right.Behavior.DirectionNormalized));
            Assert.That(left.StaminaBand, Is.EqualTo(right.StaminaBand));
            Assert.That(left.PhaseRemainingSeconds, Is.EqualTo(right.PhaseRemainingSeconds));
            Assert.That(left.IsRunTelegraphing, Is.EqualTo(right.IsRunTelegraphing));
            Assert.That(left.RunTelegraphDirectionNormalized, Is.EqualTo(right.RunTelegraphDirectionNormalized));
            Assert.That(left.HeadShakeActive, Is.EqualTo(right.HeadShakeActive));
            Assert.That(left.HeadShakeEventSequence, Is.EqualTo(right.HeadShakeEventSequence));
            Assert.That(left.FinalRunDecisionMade, Is.EqualTo(right.FinalRunDecisionMade));
            Assert.That(left.FinalRunPending, Is.EqualTo(right.FinalRunPending));
            Assert.That(left.IsFinalRun, Is.EqualTo(right.IsFinalRun));
            Assert.That(left.FinalRunUsed, Is.EqualTo(right.FinalRunUsed));
        }
    }
}
