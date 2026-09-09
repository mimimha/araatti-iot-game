using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV2StateMachineTests
    {
        [Test]
        public void HookedToFighting_InitializesMetricsWithoutPreFightRisk()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialDistanceMeters = 12f,
                InitialVirtualTensionNormalized = 1f,
                BreakStressPerSecond = 10f
            });

            DriveToHooked(machine, 0f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Hooked));
            Assert.That(machine.Current.BreakStressNormalized, Is.Zero);
            Assert.That(machine.Current.HookLooseRiskNormalized, Is.Zero);

            machine.Tick(Frame(), 0.01f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));
            Assert.That(machine.Current.FishDistanceMeters, Is.EqualTo(12f));
            Assert.That(machine.Current.FishStaminaNormalized, Is.EqualTo(1f));
            Assert.That(machine.Current.BreakStressNormalized, Is.Zero);
        }

        [Test]
        public void CanCatch_TransitionsStateMachineToCaught()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialDistanceMeters = 1f,
                CatchDistanceMeters = 1f,
                CatchStaminaNormalized = 1f
            });
            DriveToFighting(machine, 0.5f);

            machine.Tick(Frame(reel: 0f, pitch: 0f), 0.1f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Caught));
            Assert.That(machine.LastResult.WasCaught, Is.True);
        }

        [Test]
        public void LineBrokenCondition_TransitionsStateMachineToMatchingEscape()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialVirtualTensionNormalized = 1f,
                TensionFallPerSecond = 0.01f,
                BreakStressPerSecond = 4f,
                SevereBreakStressBonusPerSecond = 4f
            });
            DriveToFighting(machine, 0f);

            machine.Tick(Frame(reel: 1f, yaw: -1f), 0.25f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(machine.Current.EscapeReason, Is.EqualTo(FishingEscapeReason.LineBroken));
        }

        [Test]
        public void SlackLineCondition_TransitionsStateMachineToMatchingEscape()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialVirtualTensionNormalized = 0f,
                TensionRisePerSecond = 0.01f,
                LooseRiskPerSecond = 4f
            });
            DriveToFighting(machine, 1f);

            machine.Tick(Frame(pitch: -1f), 0.25f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(machine.Current.EscapeReason, Is.EqualTo(FishingEscapeReason.SlackLine));
        }

        [Test]
        public void InputTension_IsNotCopiedIntoSingleFishVirtualTension()
        {
            FishingStateMachine lowInput = CreateMachine(new FishingV2FightTuning());
            FishingStateMachine highInput = CreateMachine(new FishingV2FightTuning());
            DriveToFighting(lowInput, 0f);
            DriveToFighting(highInput, 1f);

            lowInput.Tick(Frame(tension: 0f), 0.1f);
            highInput.Tick(Frame(tension: 1f), 0.1f);

            Assert.That(lowInput.Current.RawTensionNormalized, Is.EqualTo(0f));
            Assert.That(highInput.Current.RawTensionNormalized, Is.EqualTo(1f));
            Assert.That(lowInput.Current.VirtualLineTensionNormalized,
                Is.EqualTo(highInput.Current.VirtualLineTensionNormalized).Within(0.0001f));
            Assert.That(lowInput.Current.TensionNormalized,
                Is.EqualTo(lowInput.Current.VirtualLineTensionNormalized).Within(0.0001f));
        }

        [Test]
        public void HookedToFighting_StartsFormalAIInFight()
        {
            FishingStateMachine machine = CreateMachine(
                new FishingV2FightTuning(), new FishingV2FishAITuning());

            DriveToFighting(machine, 0.5f);

            Assert.That(machine.Current.V2BehaviorState, Is.EqualTo(FishingV2BehaviorState.Fight));
            Assert.That(machine.Current.Feedback.State, Is.EqualTo(FishingFeedbackState.Fight));
            Assert.That(machine.Current.AIStaminaBand, Is.EqualTo(FishingV2StaminaBand.High));
            Assert.That(machine.Current.AIPhaseRemainingSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void InjectedAITuning_DrivesTelegraphedRunIndependentlyOfLegacyProfile()
        {
            FishingV2FishAITuning aiTuning = new FishingV2FishAITuning
            {
                FightMinDuration = 0.05f,
                FightMaxDuration = 0.05f,
                HighFightWeight = 0f,
                HighRunWeight = 1f,
                HighRestWeight = 0f,
                RunTelegraphMinDuration = 0.2f,
                RunTelegraphMaxDuration = 0.2f,
                HeadShakeChance = 0f,
                FinalRunStaminaThreshold = 0f
            };
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning(), aiTuning);
            DriveToFighting(machine, 0.5f);

            machine.Tick(Frame(), 0.05f);

            Assert.That(machine.Current.IsRunTelegraphing, Is.True);
            Assert.That(machine.Current.V2BehaviorState, Is.Not.EqualTo(FishingV2BehaviorState.Run));
            Assert.That(machine.Current.RunTelegraphDirectionNormalized, Is.EqualTo(-1f).Or.EqualTo(1f));
        }

        [Test]
        public void FinalRunPending_SuppressesCatchUntilTelegraphedRunStarts()
        {
            FishingV2FightTuning fightTuning = new FishingV2FightTuning
            {
                InitialDistanceMeters = 1f,
                CatchDistanceMeters = 2f,
                CatchStaminaNormalized = 1f
            };
            FishingV2FishAITuning aiTuning = new FishingV2FishAITuning
            {
                FightMinDuration = 0.05f,
                FightMaxDuration = 0.05f,
                HeadShakeChance = 0f,
                FinalRunChance = 0.99f,
                FinalRunStaminaThreshold = 1f,
                FinalRunDistanceThreshold = 2f,
                RunTelegraphMinDuration = 0.2f,
                RunTelegraphMaxDuration = 0.2f
            };
            FishingStateMachine machine = CreateMachine(fightTuning, aiTuning, 1);
            DriveToFighting(machine, 0.5f);

            machine.Tick(Frame(), 0.05f);

            Assert.That(machine.Current.FinalRunPending, Is.True);
            Assert.That(machine.Current.IsRunTelegraphing, Is.True);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));
        }

        [Test]
        public void LegacyBehaviorKnobs_DoNotChangeV2AISequence()
        {
            FishingV2FishAITuning tuning = new FishingV2FishAITuning { HeadShakeChance = 0f };
            FishingStateMachine runLegacyKnobs = CreateMachine(
                new FishingV2FightTuning(), tuning, 73, 1f, 0f, 0.35f);
            FishingStateMachine restLegacyKnobs = CreateMachine(
                new FishingV2FightTuning(), tuning, 73, 0f, 1f, 30f);
            DriveToFighting(runLegacyKnobs, 0.5f);
            DriveToFighting(restLegacyKnobs, 0.5f);

            for (int i = 0; i < 80; i++)
            {
                FishingInputFrame input = Frame(pitch: 0.35f);
                runLegacyKnobs.Tick(input, 0.05f);
                restLegacyKnobs.Tick(input, 0.05f);
                Assert.That(runLegacyKnobs.Current.V2BehaviorState,
                    Is.EqualTo(restLegacyKnobs.Current.V2BehaviorState));
                Assert.That(runLegacyKnobs.Current.V2FishDirectionNormalized,
                    Is.EqualTo(restLegacyKnobs.Current.V2FishDirectionNormalized));
                Assert.That(runLegacyKnobs.Current.V2FishForceNormalized,
                    Is.EqualTo(restLegacyKnobs.Current.V2FishForceNormalized));
            }
        }

        private static FishingStateMachine CreateMachine(FishingV2FightTuning tuning)
        {
            return CreateMachine(tuning, new FishingV2FishAITuning());
        }

        private static FishingStateMachine CreateMachine(
            FishingV2FightTuning tuning,
            FishingV2FishAITuning aiTuning,
            int seed = 73,
            float legacyRunChance = 0f,
            float legacyRestChance = 1f,
            float legacyPhaseSeconds = 10f)
        {
            FishProfile fish = new FishProfile
            {
                UseSpeciesRuleOverrides = true,
                MinBiteDelaySeconds = 0.05f,
                MaxBiteDelaySeconds = 0.05f,
                HookWindowSeconds = 1f,
                RunChance = legacyRunChance,
                RestChance = legacyRestChance,
                MinBehaviorPhaseSeconds = legacyPhaseSeconds,
                MaxBehaviorPhaseSeconds = legacyPhaseSeconds
            };
            FishingRules rules = new FishingRules
            {
                MinBiteDelaySeconds = 0.05f,
                MaxBiteDelaySeconds = 0.05f,
                HookWindowSeconds = 1f,
                HookSettleSeconds = 0f
            };
            FishingStateMachine machine = new FishingStateMachine();
            machine.Initialize(new FishingRoundContext
            {
                Seed = seed,
                Rules = rules,
                Fish = fish,
                UseV2FightModel = true,
                V2FightTuning = tuning,
                V2FishAITuning = aiTuning
            });
            return machine;
        }

        private static void DriveToHooked(FishingStateMachine machine, float tension)
        {
            machine.Tick(Frame(castPressed: true, tension: tension), 0.1f);
            machine.Tick(Frame(castReleased: true, tension: tension), 0.1f);
            machine.Tick(Frame(tension: tension), 0.1f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.BiteWindow));
            machine.Tick(Frame(hookPressed: true, tension: tension), 0.01f);
        }

        private static void DriveToFighting(FishingStateMachine machine, float tension)
        {
            DriveToHooked(machine, tension);
            machine.Tick(Frame(tension: tension), 0.01f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float reel = 0f,
            float tension = 0.5f,
            float pitch = 0f,
            float yaw = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                ReelDelta = reel,
                TensionNormalized = tension,
                RodPitch = pitch,
                RodYaw = yaw,
                IsDeviceConnected = true
            };
        }
    }
}
