using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingStateMachineTests
    {
        [Test]
        public void FullCycle_CanCatchFishAndReturnToIdle()
        {
            FishingStateMachine machine = CreateMachine();

            Step(machine, 0.01f, castPressed: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Casting));

            Step(machine, 0.25f, castReleased: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));

            AdvanceUntil(machine, FishingPlayerState.BiteWindow, 20, 0.05f);
            Step(machine, 0.01f, hookPressed: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Hooked));

            Step(machine, 0.01f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));

            for (int i = 0; i < 30 && machine.Current.State == FishingPlayerState.Fighting; i++)
            {
                Step(machine, 0.1f, tension: 0.5f, reel: 1f);
            }

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Caught));
            Assert.That(machine.LastResult, Is.Not.Null);
            Assert.That(machine.LastResult.WasCaught, Is.True);
            Assert.That(machine.Current.TotalScore, Is.GreaterThan(0));

            AdvanceUntil(machine, FishingPlayerState.Cooldown, 20, 0.05f);
            AdvanceUntil(machine, FishingPlayerState.Idle, 20, 0.05f);
            Assert.That(machine.Current.CompletedCycles, Is.EqualTo(1));
        }

        [Test]
        public void BiteWindow_ExpiresIntoEscaped()
        {
            FishingStateMachine machine = CreateMachine();
            Step(machine, 0.01f, castPressed: true);
            Step(machine, 0.2f, castReleased: true);
            AdvanceUntil(machine, FishingPlayerState.BiteWindow, 20, 0.05f);

            AdvanceUntil(machine, FishingPlayerState.Escaped, 20, 0.05f);

            Assert.That(machine.LastResult, Is.Not.Null);
            Assert.That(machine.LastResult.WasCaught, Is.False);
            Assert.That(machine.LastResult.EscapeReason, Is.EqualTo(FishingEscapeReason.MissedBite));
        }

        [Test]
        public void Waiting_TwoFalseStrikesScareFishAway()
        {
            FishingStateMachine machine = CreateMachine();
            Step(machine, 0.01f, castPressed: true);
            Step(machine, 0.2f, castReleased: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));

            Step(machine, 0.01f, hookPressed: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(machine.Current.FalseStrikeCount, Is.EqualTo(1));

            Step(machine, 0.01f, hookPressed: true);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(machine.LastResult.EscapeReason, Is.EqualTo(FishingEscapeReason.MissedBite));
        }

        [Test]
        public void Fighting_RunCreatesSeededTensionShiftAndDirection()
        {
            FishingStateMachine machine = CreateMachine();
            EnterFighting(machine);
            Step(machine, 0.2f, tension: 0.5f, rodYaw: 0f);

            Assert.That(machine.Current.Feedback.State, Is.EqualTo(FishingFeedbackState.Run));
            Assert.That(machine.Current.FightDirection, Is.EqualTo(-1f).Or.EqualTo(1f));
            Assert.That(machine.Current.FishTensionShift, Is.GreaterThan(0f));
            Assert.That(machine.Current.TensionNormalized, Is.GreaterThan(machine.Current.RawTensionNormalized));
        }

        [Test]
        public void Fighting_HighTensionBreaksLine()
        {
            FishingStateMachine machine = CreateMachine();
            EnterFighting(machine);

            for (int i = 0; i < 10 && machine.Current.State == FishingPlayerState.Fighting; i++)
            {
                Step(machine, 0.1f, tension: 1f);
            }

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(machine.LastResult.EscapeReason, Is.EqualTo(FishingEscapeReason.LineBroken));
        }

        [Test]
        public void PrepareCycle_ChangesFishWithoutLosingRoundTotals()
        {
            FishingStateMachine machine = CreateMachine();
            EnterFighting(machine);
            for (int i = 0; i < 30 && machine.Current.State == FishingPlayerState.Fighting; i++)
            {
                Step(machine, 0.1f, tension: 0.5f, reel: 1f);
            }
            AdvanceUntil(machine, FishingPlayerState.Cooldown, 20, 0.05f);
            AdvanceUntil(machine, FishingPlayerState.Idle, 20, 0.05f);
            int previousScore = machine.Current.TotalScore;

            machine.PrepareCycle(new FishProfile
            {
                FishId = "hard-catfish",
                DisplayName = "Hard Catfish",
                DifficultyLabel = "Hard",
                BaseScore = 300,
                MaxStamina = 145f,
                PullStrength = 0.85f,
                UseSpeciesRuleOverrides = true,
                MinBiteDelaySeconds = 0.2f,
                MaxBiteDelaySeconds = 0.2f,
                HookWindowSeconds = 0.1f,
                FightTimeoutSeconds = 2f,
                ReelEfficiencyMultiplier = 0.8f,
                DifficultyScoreMultiplier = 1.6f
            });

            Assert.That(machine.Current.FishId, Is.EqualTo("hard-catfish"));
            Assert.That(machine.Current.DifficultyLabel, Is.EqualTo("Hard"));
            Assert.That(machine.Current.FishMaxHealth, Is.EqualTo(145f));
            Assert.That(machine.Current.TotalScore, Is.EqualTo(previousScore));
            Assert.That(machine.Current.CompletedCycles, Is.EqualTo(1));
        }

        [Test]
        public void RoundTracker_TimeExpiryCompletesExactlyOnce()
        {
            FishingRoundTracker tracker = new FishingRoundTracker();
            int completionCount = 0;
            tracker.Completed += _ => completionCount++;
            tracker.Initialize(new FishingLaunchContext
            {
                RoundId = "round-test",
                RoundDurationSeconds = 0.2f,
                CountdownSeconds = 0.1f
            });

            tracker.Begin();
            tracker.Tick(0.1f);
            Assert.That(tracker.Current.State, Is.EqualTo(FishingRoundState.Playing));
            tracker.Tick(0.2f);
            tracker.Tick(0.2f);
            tracker.Stop();

            Assert.That(tracker.Current.State, Is.EqualTo(FishingRoundState.Completed));
            Assert.That(tracker.Result.EndReason, Is.EqualTo(FishingRoundEndReason.TimeExpired));
            Assert.That(completionCount, Is.EqualTo(1));
        }

        private static FishingStateMachine CreateMachine()
        {
            FishingStateMachine machine = new FishingStateMachine();
            machine.Initialize(new FishingRoundContext
            {
                Seed = 7,
                Rules = new FishingRules
                {
                    MaxCastChargeSeconds = 0.2f,
                    MinBiteDelaySeconds = 0.1f,
                    MaxBiteDelaySeconds = 0.1f,
                    HookWindowSeconds = 0.2f,
                    HookSettleSeconds = 0f,
                    FightTimeoutSeconds = 5f,
                    SafeTensionMin = 0.3f,
                    SafeTensionMax = 0.75f,
                    SlackEscapeSeconds = 0.5f,
                    HighTensionGraceSeconds = 0.1f,
                    HighTensionDamagePerSecond = 1000f,
                    FishDamagePerSecond = 100f,
                    OutcomeDisplaySeconds = 0.1f,
                    CooldownSeconds = 0.1f
                },
                Fish = new FishProfile
                {
                    FishId = "test-fish",
                    DisplayName = "Test Fish",
                    BaseScore = 10,
                    MaxStamina = 50f,
                    PullStrength = 0.5f,
                    MinBehaviorPhaseSeconds = 1f,
                    MaxBehaviorPhaseSeconds = 1f,
                    RunChance = 1f,
                    RestChance = 0f,
                    MaxTensionShift = 0.12f,
                    DirectionChangeChance = 1f
                }
            });
            return machine;
        }

        private static void EnterFighting(FishingStateMachine machine)
        {
            Step(machine, 0.01f, castPressed: true);
            Step(machine, 0.2f, castReleased: true);
            AdvanceUntil(machine, FishingPlayerState.BiteWindow, 20, 0.05f);
            Step(machine, 0.01f, hookPressed: true);
            Step(machine, 0.01f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));
        }

        private static void AdvanceUntil(FishingStateMachine machine, FishingPlayerState state, int maxSteps, float deltaTime)
        {
            for (int i = 0; i < maxSteps && machine.Current.State != state; i++)
            {
                Step(machine, deltaTime);
            }
            Assert.That(machine.Current.State, Is.EqualTo(state));
        }

        private static void Step(
            FishingStateMachine machine,
            float deltaTime,
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float tension = 0.5f,
            float reel = 0f,
            float rodYaw = 0f)
        {
            machine.Tick(new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                ReelDelta = reel,
                TensionNormalized = tension,
                RodYaw = rodYaw,
                IsDeviceConnected = true
            }, deltaTime);
        }
    }
}
