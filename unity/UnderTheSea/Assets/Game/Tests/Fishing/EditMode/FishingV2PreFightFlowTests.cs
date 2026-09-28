using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV2PreFightFlowTests
    {
        [Test]
        public void PreFightTuning_SanitizesDurationsRangesAndTimingInvariant()
        {
            FishingV2PreFightTuning tuning = new FishingV2PreFightTuning
            {
                NibbleLeadMinSeconds = -1f,
                NibbleLeadMaxSeconds = -2f,
                NibbleDurationSeconds = -1f,
                NibbleToBiteGapSeconds = -1f,
                NibbleIntensityNormalized = 2f,
                EarlyHookPenaltyMinSeconds = -1f,
                EarlyHookPenaltyMaxSeconds = -2f,
                MissedBiteRetryMinSeconds = -1f,
                MissedBiteRetryMaxSeconds = -2f
            };

            tuning.Sanitize();

            Assert.That(tuning.NibbleDurationSeconds, Is.GreaterThan(0f));
            Assert.That(tuning.NibbleToBiteGapSeconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(tuning.NibbleLeadMinSeconds,
                Is.GreaterThanOrEqualTo(tuning.NibbleDurationSeconds + tuning.NibbleToBiteGapSeconds));
            Assert.That(tuning.NibbleLeadMaxSeconds, Is.GreaterThanOrEqualTo(tuning.NibbleLeadMinSeconds));
            Assert.That(tuning.NibbleIntensityNormalized, Is.InRange(0f, 1f));
            Assert.That(tuning.EarlyHookPenaltyMaxSeconds,
                Is.GreaterThanOrEqualTo(tuning.EarlyHookPenaltyMinSeconds));
            Assert.That(tuning.MissedBiteRetryMinSeconds, Is.GreaterThan(0f));
            Assert.That(tuning.MissedBiteRetryMaxSeconds,
                Is.GreaterThanOrEqualTo(tuning.MissedBiteRetryMinSeconds));
        }

        [Test]
        public void V2Cast_StartsAndCommitsIntoWaiting()
        {
            FishingStateMachine machine = CreateV2Machine();

            Step(machine, 0.01f, castPressed: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Casting));

            Step(machine, 0.1f, castReleased: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.BiteDelayRemainingSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void EveryV2Bite_HasOneObservedNibbleWithActiveDurationAndGap()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);

            AdvanceUntil(machine, snapshot => snapshot.IsNibbling, 20, 0.05f);
            int sequence = machine.Current.NibbleEventSequence;
            Assert.That(sequence, Is.EqualTo(1));
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.NibbleRemainingSeconds, Is.GreaterThan(0f));
            Assert.That(machine.Current.NibbleIntensityNormalized, Is.EqualTo(0.25f).Within(0.001f));

            Step(machine, 0.05f);
            Assert.That(machine.Current.IsNibbling, Is.True);
            Assert.That(machine.Current.NibbleEventSequence, Is.EqualTo(sequence));
            Assert.That(machine.Current.BiteEventSequence, Is.Zero);

            AdvanceUntil(machine, snapshot => !snapshot.IsNibbling, 20, 0.05f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.BiteEventSequence, Is.Zero);

            Step(machine, 0.05f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.BiteEventSequence, Is.Zero);

            AdvanceUntilState(machine, FishingPlayerState.BiteWindow, 20, 0.05f);
            Assert.That(machine.Current.NibbleEventSequence, Is.EqualTo(1));
            Assert.That(machine.Current.BiteEventSequence, Is.EqualTo(1));
        }

        [Test]
        public void LargeDeltaTime_PreservesObservedNibbleDurationAndGap()
        {
            FishingV2PreFightTuning tuning = FixedTuning();
            tuning.NibbleLeadMinSeconds = tuning.NibbleLeadMaxSeconds = 0.13f;
            tuning.NibbleDurationSeconds = 0.08f;
            tuning.NibbleToBiteGapSeconds = 0.05f;
            FishingRules rules = FixedRules();
            rules.MinBiteDelaySeconds = rules.MaxBiteDelaySeconds = 0.15f;
            FishingStateMachine machine = CreateV2Machine(tuning, rules);
            Cast(machine);

            Step(machine, 0.25f);

            Assert.That(machine.Current.NibbleEventSequence, Is.EqualTo(1));
            Assert.That(machine.Current.BiteEventSequence, Is.Zero);
            Assert.That(machine.Current.IsNibbling, Is.True);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));

            Step(machine, 0.04f);
            Assert.That(machine.Current.IsNibbling, Is.True);
            Assert.That(machine.Current.NibbleRemainingSeconds, Is.GreaterThan(0f));

            Step(machine, 0.04f);
            Assert.That(machine.Current.IsNibbling, Is.False);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.BiteEventSequence, Is.Zero);

            Step(machine, 0.04f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.BiteEventSequence, Is.Zero);

            Step(machine, 0.02f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.BiteWindow));
            Assert.That(machine.Current.BiteEventSequence, Is.EqualTo(1));
        }

        [Test]
        public void EarlyHookBeforeNibble_StartsPenalizedAttemptWithoutEndingEncounter()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);
            string fishId = machine.Current.FishId;
            float originalRemaining = machine.Current.BiteDelayRemainingSeconds;

            Step(machine, 0.01f, hookPressed: true);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.EarlyHookCount, Is.EqualTo(1));
            Assert.That(machine.Current.FalseStrikeCount, Is.Zero);
            Assert.That(machine.Current.FishId, Is.EqualTo(fishId));
            Assert.That(machine.Current.BiteDelayRemainingSeconds, Is.GreaterThan(originalRemaining));
            Assert.That(machine.LastResult, Is.Null);
            Assert.That(machine.Current.CompletedCycles, Is.Zero);
        }

        [Test]
        public void EarlyHookDuringNibble_StartsFreshAttemptWithoutHookOrEscape()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);
            AdvanceUntil(machine, snapshot => snapshot.IsNibbling, 20, 0.05f);
            int nibbleSequence = machine.Current.NibbleEventSequence;

            Step(machine, 0.01f, hookPressed: true);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.IsNibbling, Is.False);
            Assert.That(machine.Current.EarlyHookCount, Is.EqualTo(1));
            Assert.That(machine.Current.NibbleEventSequence, Is.EqualTo(nibbleSequence));
            Assert.That(machine.LastResult, Is.Null);
        }

        [Test]
        public void RepeatedEarlyHooks_KeepSameFishAndNeverFinishCycle()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);
            string fishId = machine.Current.FishId;
            int finished = 0;
            machine.CycleFinished += _ => finished++;

            for (int i = 0; i < 3; i++) Step(machine, 0.01f, hookPressed: true);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.EarlyHookCount, Is.EqualTo(3));
            Assert.That(machine.Current.FishId, Is.EqualTo(fishId));
            Assert.That(machine.LastResult, Is.Null);
            Assert.That(finished, Is.Zero);
        }

        [Test]
        public void BiteTransition_PrioritizesBiteButRequiresNextTickHook()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);
            AdvanceUntil(machine, snapshot =>
                snapshot.NibbleEventSequence == 1 &&
                !snapshot.IsNibbling &&
                snapshot.BiteDelayRemainingSeconds <= 0.05f,
                100, 0.01f);
            float step = machine.Current.BiteDelayRemainingSeconds + 0.01f;

            Step(machine, step, hookPressed: true);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.BiteWindow));
            Assert.That(machine.Current.BiteEventSequence, Is.EqualTo(1));
            Assert.That(machine.Current.EarlyHookCount, Is.Zero);

            Step(machine, 0.01f, hookPressed: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Hooked));
        }

        [Test]
        public void MissedBite_QuickRetriesSameFishAndRetryAlsoHasNibble()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);
            AdvanceUntilState(machine, FishingPlayerState.BiteWindow, 100, 0.05f);
            string fishId = machine.Current.FishId;
            int finished = 0;
            machine.CycleFinished += _ => finished++;

            Step(machine, 0.21f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.MissedBiteRetryCount, Is.EqualTo(1));
            Assert.That(machine.Current.FishId, Is.EqualTo(fishId));
            Assert.That(machine.Current.BiteDelayRemainingSeconds, Is.LessThan(0.6f));
            Assert.That(machine.Current.TotalScore, Is.Zero);
            Assert.That(machine.LastResult, Is.Null);
            Assert.That(finished, Is.Zero);

            AdvanceUntil(machine, snapshot => snapshot.NibbleEventSequence == 2, 100, 0.05f);
            Assert.That(machine.Current.IsNibbling, Is.True);
            AdvanceUntilState(machine, FishingPlayerState.BiteWindow, 100, 0.05f);
            Assert.That(machine.Current.NibbleEventSequence, Is.EqualTo(2));
            Assert.That(machine.Current.BiteEventSequence, Is.EqualTo(2));
        }

        [Test]
        public void IdenticalSeedTuningAndInput_ProduceIdenticalRetrySequence()
        {
            FishingStateMachine left = CreateV2Machine();
            FishingStateMachine right = CreateV2Machine();
            StepBoth(left, right, 0.01f, castPressed: true);
            StepBoth(left, right, 0.1f, castReleased: true);

            for (int i = 0; i < 80; i++)
            {
                StepBoth(left, right, 0.03f, hookPressed: i == 2);
                Assert.That(left.Current.State, Is.EqualTo(right.Current.State));
                Assert.That(left.Current.BiteDelayRemainingSeconds,
                    Is.EqualTo(right.Current.BiteDelayRemainingSeconds).Within(0.0001f));
                Assert.That(left.Current.NibbleRemainingSeconds,
                    Is.EqualTo(right.Current.NibbleRemainingSeconds).Within(0.0001f));
                Assert.That(left.Current.NibbleEventSequence, Is.EqualTo(right.Current.NibbleEventSequence));
                Assert.That(left.Current.BiteEventSequence, Is.EqualTo(right.Current.BiteEventSequence));
                Assert.That(left.Current.EarlyHookCount, Is.EqualTo(right.Current.EarlyHookCount));
                Assert.That(left.Current.MissedBiteRetryCount, Is.EqualTo(right.Current.MissedBiteRetryCount));
            }
        }

        [Test]
        public void SuccessfulHook_HandsOffToExistingT4FightStartingInFight()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);
            AdvanceUntilState(machine, FishingPlayerState.BiteWindow, 100, 0.05f);

            Step(machine, 0.01f, hookPressed: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Hooked));
            Step(machine, 0.05f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));
            Assert.That(machine.Current.V2BehaviorState, Is.EqualTo(FishingV2BehaviorState.Fight));
            Assert.That(machine.Current.NibbleEventSequence, Is.EqualTo(1));
            Assert.That(machine.Current.BiteEventSequence, Is.EqualTo(1));
        }

        [Test]
        public void PreFightFlag_IsIndependentFromFightModelFlag()
        {
            FishingStateMachine v2PreFightWithLegacyFight = CreateMachine(true, false);
            FishingStateMachine legacyPreFightWithV2Fight = CreateMachine(false, true);
            Cast(v2PreFightWithLegacyFight);
            Cast(legacyPreFightWithV2Fight);

            Step(v2PreFightWithLegacyFight, 0.01f, hookPressed: true);
            Step(legacyPreFightWithV2Fight, 0.01f, hookPressed: true);

            Assert.That(v2PreFightWithLegacyFight.Current.EarlyHookCount, Is.EqualTo(1));
            Assert.That(v2PreFightWithLegacyFight.Current.FalseStrikeCount, Is.Zero);
            Assert.That(legacyPreFightWithV2Fight.Current.EarlyHookCount, Is.Zero);
            Assert.That(legacyPreFightWithV2Fight.Current.FalseStrikeCount, Is.EqualTo(1));
        }

        [Test]
        public void LegacyMissedBite_StillEscapesEvenWithV2FightModelEnabled()
        {
            FishingStateMachine machine = CreateMachine(false, true);
            Cast(machine);
            AdvanceUntilState(machine, FishingPlayerState.BiteWindow, 100, 0.05f);

            Step(machine, 0.21f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(machine.LastResult, Is.Not.Null);
            Assert.That(machine.LastResult.EscapeReason, Is.EqualTo(FishingEscapeReason.MissedBite));
            Assert.That(machine.Current.MissedBiteRetryCount, Is.Zero);
        }

        [Test]
        public void ResetCycle_ClearsV2EventSequencesAndRetryCounters()
        {
            FishingStateMachine machine = CreateV2Machine();
            Cast(machine);
            Step(machine, 0.01f, hookPressed: true);
            AdvanceUntil(machine, snapshot => snapshot.NibbleEventSequence > 0, 100, 0.05f);

            machine.ResetCycle();

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Idle));
            Assert.That(machine.Current.NibbleEventSequence, Is.Zero);
            Assert.That(machine.Current.BiteEventSequence, Is.Zero);
            Assert.That(machine.Current.EarlyHookCount, Is.Zero);
            Assert.That(machine.Current.MissedBiteRetryCount, Is.Zero);
        }

        private static FishingStateMachine CreateV2Machine(
            FishingV2PreFightTuning tuning = null,
            FishingRules rules = null)
        {
            return CreateMachine(true, true, tuning, rules);
        }

        private static FishingStateMachine CreateMachine(
            bool useV2PreFight,
            bool useV2Fight,
            FishingV2PreFightTuning tuning = null,
            FishingRules rules = null)
        {
            FishingStateMachine machine = new FishingStateMachine();
            machine.Initialize(new FishingRoundContext
            {
                Seed = 47,
                Rules = rules ?? FixedRules(),
                Fish = new FishProfile
                {
                    FishId = "v2-pre-fight-fish",
                    DisplayName = "V2 Pre-Fight Fish",
                    MaxStamina = 100f,
                    PullStrength = 0.5f
                },
                UseV2PreFightFlow = useV2PreFight,
                V2PreFightTuning = tuning ?? FixedTuning(),
                UseV2FightModel = useV2Fight,
                V2FightTuning = new FishingV2FightTuning(),
                V2FishAITuning = new FishingV2FishAITuning { HeadShakeChance = 0f }
            });
            return machine;
        }

        private static FishingRules FixedRules()
        {
            return new FishingRules
            {
                MaxCastChargeSeconds = 0.2f,
                MinBiteDelaySeconds = 0.6f,
                MaxBiteDelaySeconds = 0.6f,
                HookWindowSeconds = 0.2f,
                HookSettleSeconds = 0.05f
            };
        }

        private static FishingV2PreFightTuning FixedTuning()
        {
            return new FishingV2PreFightTuning
            {
                NibbleLeadMinSeconds = 0.3f,
                NibbleLeadMaxSeconds = 0.3f,
                NibbleDurationSeconds = 0.2f,
                NibbleToBiteGapSeconds = 0.1f,
                NibbleIntensityNormalized = 0.25f,
                EarlyHookPenaltyMinSeconds = 0.4f,
                EarlyHookPenaltyMaxSeconds = 0.4f,
                MissedBiteRetryMinSeconds = 0.4f,
                MissedBiteRetryMaxSeconds = 0.4f
            };
        }

        private static void Cast(FishingStateMachine machine)
        {
            Step(machine, 0.01f, castPressed: true);
            Step(machine, 0.1f, castReleased: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
        }

        private static void AdvanceUntil(
            FishingStateMachine machine,
            System.Func<FishingSnapshot, bool> predicate,
            int maxSteps,
            float dt)
        {
            for (int i = 0; i < maxSteps && !predicate(machine.Current); i++) Step(machine, dt);
            Assert.That(predicate(machine.Current), Is.True);
        }

        private static void AdvanceUntilState(
            FishingStateMachine machine,
            FishingPlayerState state,
            int maxSteps,
            float dt)
        {
            AdvanceUntil(machine, snapshot => snapshot.State == state, maxSteps, dt);
        }

        private static void StepBoth(
            FishingStateMachine left,
            FishingStateMachine right,
            float dt,
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false)
        {
            FishingInputFrame frame = Frame(castPressed, castReleased, hookPressed);
            left.Tick(frame, dt);
            right.Tick(frame, dt);
        }

        private static void Step(
            FishingStateMachine machine,
            float dt,
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false)
        {
            machine.Tick(Frame(castPressed, castReleased, hookPressed), dt);
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                TensionNormalized = 0.5f,
                IsDeviceConnected = true
            };
        }
    }
}
