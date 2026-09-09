using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV2FightModelTests
    {
        [Test]
        public void Initialization_UsesConfiguredSafeDefaults()
        {
            FishingV2FightTuning tuning = new FishingV2FightTuning { InitialDistanceMeters = 13f };
            FishingV2FightModel model = new FishingV2FightModel(tuning);

            Assert.That(model.FishDistanceMeters, Is.EqualTo(13f));
            Assert.That(model.FishStaminaNormalized, Is.EqualTo(1f));
            Assert.That(model.BreakStressNormalized, Is.Zero);
            Assert.That(model.HookLooseRiskNormalized, Is.Zero);
            Assert.That(model.TensionZone, Is.EqualTo(FishingV2TensionZone.Good));
        }

        [Test]
        public void TensionZone_UsesExplicitInclusiveBoundaries()
        {
            FishingV2FightModel model = new FishingV2FightModel();

            Assert.That(model.ClassifyTension(0f), Is.EqualTo(FishingV2TensionZone.Slack));
            Assert.That(model.ClassifyTension(0.1499f), Is.EqualTo(FishingV2TensionZone.Slack));
            Assert.That(model.ClassifyTension(0.15f), Is.EqualTo(FishingV2TensionZone.Low));
            Assert.That(model.ClassifyTension(0.30f), Is.EqualTo(FishingV2TensionZone.Good));
            Assert.That(model.ClassifyTension(0.75f), Is.EqualTo(FishingV2TensionZone.High));
            Assert.That(model.ClassifyTension(0.90f), Is.EqualTo(FishingV2TensionZone.Danger));
            Assert.That(model.ClassifyTension(1f), Is.EqualTo(FishingV2TensionZone.Danger));
        }

        [Test]
        public void ReelStress_IsHigherDuringRunThanRest()
        {
            FishingV2FightModel run = new FishingV2FightModel();
            FishingV2FightModel rest = new FishingV2FightModel();

            run.Tick(0.1f, 1f, 0f, 0f, Sample(FishingV2BehaviorState.Run));
            rest.Tick(0.1f, 1f, 0f, 0f, Sample(FishingV2BehaviorState.Rest));

            Assert.That(run.TargetVirtualLineTensionNormalized,
                Is.GreaterThan(rest.TargetVirtualLineTensionNormalized));
        }

        [Test]
        public void RunRodResponse_IsBetterWhenFollowingFishDirection()
        {
            FishingV2FightModel following = new FishingV2FightModel();
            FishingV2FightModel opposing = new FishingV2FightModel();

            following.Tick(0.1f, 0f, 0f, 1f, Sample(FishingV2BehaviorState.Run, 0.8f, 1f));
            opposing.Tick(0.1f, 0f, 0f, -1f, Sample(FishingV2BehaviorState.Run, 0.8f, 1f));

            Assert.That(following.RodResponseQualityNormalized,
                Is.GreaterThan(opposing.RodResponseQualityNormalized));
            Assert.That(following.TargetVirtualLineTensionNormalized,
                Is.LessThan(opposing.TargetVirtualLineTensionNormalized));
        }

        [Test]
        public void TensionSmoothing_RisesAndFallsWithoutJumpingOrLeavingRange()
        {
            FishingV2FightModel model = new FishingV2FightModel();
            float initial = model.VirtualLineTensionNormalized;

            model.Tick(0.1f, 1f, 0f, -1f, Sample(FishingV2BehaviorState.Run, 1f, 1f));
            float raised = model.VirtualLineTensionNormalized;
            Assert.That(raised, Is.GreaterThan(initial));
            Assert.That(raised, Is.LessThan(model.TargetVirtualLineTensionNormalized));

            model.Tick(0.1f, 0f, 0f, 0f, Sample(FishingV2BehaviorState.Rest, 0.2f));
            Assert.That(model.VirtualLineTensionNormalized, Is.LessThan(raised));
            Assert.That(model.VirtualLineTensionNormalized, Is.InRange(0f, 1f));
        }

        [Test]
        public void BreakStress_AccumulatesInDangerRecoversWhenSafeAndIsNotInstant()
        {
            FishingV2FightTuning tuning = new FishingV2FightTuning
            {
                InitialVirtualTensionNormalized = 1f
            };
            FishingV2FightModel model = new FishingV2FightModel(tuning);

            model.Tick(0.1f, 1f, 0f, -1f, Sample(FishingV2BehaviorState.Run, 1f, 1f));
            float oneFrameRisk = model.BreakStressNormalized;
            Assert.That(oneFrameRisk, Is.GreaterThan(0f));
            Assert.That(oneFrameRisk, Is.LessThan(1f));
            Assert.That(model.FailureCondition, Is.EqualTo(FishingV2FailureCondition.None));

            Step(model, FishingV2BehaviorState.Rest, 0f, 0f, 0f, 0.6f, 0.2f);
            Assert.That(model.BreakStressNormalized, Is.LessThan(oneFrameRisk));
        }

        [Test]
        public void HookLooseRisk_AccumulatesInSlackAndRecoversAboveLowPressure()
        {
            FishingV2FightModel model = new FishingV2FightModel();
            Step(model, FishingV2BehaviorState.Rest, 0f, -1f, 0f, 0.8f, 0.2f);
            float slackRisk = model.HookLooseRiskNormalized;
            Assert.That(slackRisk, Is.GreaterThan(0f));

            Step(model, FishingV2BehaviorState.Fight, 1f, 0.35f, 0f, 0.8f, 0.7f);
            Assert.That(model.HookLooseRiskNormalized, Is.LessThan(slackRisk));
        }

        [Test]
        public void RestNeutralWithoutReel_DoesNotCreateSlackRisk()
        {
            FishingV2FightModel model = new FishingV2FightModel();

            Step(model, FishingV2BehaviorState.Rest, 0f, 0f, 0f, 5f, 0.2f);

            Assert.That(model.HookLooseRiskNormalized, Is.Zero.Within(0.0001f));
            Assert.That(model.VirtualLineTensionNormalized, Is.GreaterThanOrEqualTo(0.15f));
        }

        [Test]
        public void Distance_UsesBehaviorSpecificReelEfficiencyAndRunGain()
        {
            FishingV2FightModel rest = new FishingV2FightModel();
            FishingV2FightModel fight = new FishingV2FightModel();
            FishingV2FightModel run = new FishingV2FightModel();

            rest.Tick(0.25f, 1f, 0f, 0f, Sample(FishingV2BehaviorState.Rest));
            fight.Tick(0.25f, 1f, 0.35f, 0f, Sample(FishingV2BehaviorState.Fight));
            run.Tick(0.25f, 1f, 0f, 1f, Sample(FishingV2BehaviorState.Run, 1f, 1f));

            Assert.That(rest.FishDistanceMeters, Is.LessThan(fight.FishDistanceMeters));
            Assert.That(fight.FishDistanceMeters, Is.LessThan(12f));
            Assert.That(run.FishDistanceMeters, Is.GreaterThan(12f));
        }

        [Test]
        public void Stamina_RunDrainsMostFightPressureDrainsAndRestDrainsLeast()
        {
            FishingV2FightModel run = new FishingV2FightModel();
            FishingV2FightModel fight = new FishingV2FightModel();
            FishingV2FightModel rest = new FishingV2FightModel();

            Step(run, FishingV2BehaviorState.Run, 0f, 0f, 1f, 1f, 1f);
            Step(fight, FishingV2BehaviorState.Fight, 1f, 0.35f, 0f, 1f, 0.7f);
            Step(rest, FishingV2BehaviorState.Rest, 0f, 0f, 0f, 1f, 0.2f);

            Assert.That(run.FishStaminaNormalized, Is.LessThan(rest.FishStaminaNormalized));
            Assert.That(fight.FishStaminaNormalized, Is.LessThan(rest.FishStaminaNormalized));
        }

        [Test]
        public void Catch_RequiresLowStaminaNearDistanceSafeTensionAndNotRun()
        {
            FishingV2FightTuning tuning = new FishingV2FightTuning
            {
                InitialDistanceMeters = 1f,
                CatchDistanceMeters = 1f,
                CatchStaminaNormalized = 1f
            };
            FishingV2FightModel model = new FishingV2FightModel(tuning);

            model.Tick(0.1f, 0f, 0f, 0f, Sample(FishingV2BehaviorState.Rest, 0.2f));

            Assert.That(model.CanCatch, Is.True);
        }

        [Test]
        public void Catch_IsBlockedDuringRunEvenWhenOtherConditionsMatch()
        {
            FishingV2FightTuning tuning = new FishingV2FightTuning
            {
                InitialDistanceMeters = 1f,
                CatchDistanceMeters = 2f,
                CatchStaminaNormalized = 1f
            };
            FishingV2FightModel model = new FishingV2FightModel(tuning);

            model.Tick(0.1f, 0f, 0f, 1f, Sample(FishingV2BehaviorState.Run, 0.5f, 1f));

            Assert.That(model.CanCatch, Is.False);
        }

        [Test]
        public void BreakStressThreshold_ProducesLineBrokenCondition()
        {
            FishingV2FightTuning tuning = new FishingV2FightTuning
            {
                InitialVirtualTensionNormalized = 1f,
                BreakStressPerSecond = 4f,
                SevereBreakStressBonusPerSecond = 4f
            };
            FishingV2FightModel model = new FishingV2FightModel(tuning);

            model.Tick(0.25f, 1f, 0f, -1f, Sample(FishingV2BehaviorState.Run, 1f, 1f));

            Assert.That(model.FailureCondition, Is.EqualTo(FishingV2FailureCondition.LineBroken));
        }

        [Test]
        public void HookLooseThreshold_ProducesSlackLineCondition()
        {
            FishingV2FightTuning tuning = new FishingV2FightTuning
            {
                InitialVirtualTensionNormalized = 0f,
                TensionRisePerSecond = 0.01f,
                LooseRiskPerSecond = 4f
            };
            FishingV2FightModel model = new FishingV2FightModel(tuning);

            model.Tick(0.25f, 0f, -1f, 0f, Sample(FishingV2BehaviorState.Rest, 0.2f));

            Assert.That(model.FailureCondition, Is.EqualTo(FishingV2FailureCondition.SlackLine));
        }

        [Test]
        public void HeadShakeModifier_RaisesTargetTensionWithoutInstantBreak()
        {
            FishingV2FightTuning tuning = new FishingV2FightTuning
            {
                HeadShakeMaxTensionModifier = 0.12f
            };
            FishingV2FightModel baseline = new FishingV2FightModel(tuning);
            FishingV2FightModel shaken = new FishingV2FightModel(tuning);
            FishingV2BehaviorSample behavior = Sample(FishingV2BehaviorState.Fight, 0.6f);

            baseline.Tick(0.05f, 0.5f, 0.35f, 0f, behavior, 0f);
            shaken.Tick(0.05f, 0.5f, 0.35f, 0f, behavior,
                tuning.HeadShakeMaxTensionModifier);

            Assert.That(shaken.TargetVirtualLineTensionNormalized,
                Is.GreaterThan(baseline.TargetVirtualLineTensionNormalized));
            Assert.That(shaken.FailureCondition, Is.EqualTo(FishingV2FailureCondition.None));
            Assert.That(shaken.BreakStressNormalized, Is.LessThan(1f));
        }

        private static FishingV2BehaviorSample Sample(
            FishingV2BehaviorState state,
            float force = 0.5f,
            float direction = 1f)
        {
            return new FishingV2BehaviorSample(state, force, direction);
        }

        private static void Step(
            FishingV2FightModel model,
            FishingV2BehaviorState state,
            float reel,
            float pitch,
            float yaw,
            float seconds,
            float force)
        {
            int steps = (int)(seconds / 0.05f);
            for (int i = 0; i < steps; i++)
            {
                model.Tick(0.05f, reel, pitch, yaw, Sample(state, force, 1f));
            }
        }
    }
}
