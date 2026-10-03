using System;
using System.Reflection;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV3FishProfileTests
    {
        [TestCase(FishingV3FishProfileId.Small, "Small Fish", "small")]
        [TestCase(FishingV3FishProfileId.Normal, "Normal Fish", "normal")]
        [TestCase(FishingV3FishProfileId.Strong, "Strong Fish", "strong")]
        public void DefaultProfiles_AreAvailableAndIdentifiable(
            FishingV3FishProfileId id,
            string displayName,
            string visualId)
        {
            FishingV3FishProfile profile = FishingV3FishProfiles.Create(id);

            Assert.That(profile.Id, Is.EqualTo(id));
            Assert.That(profile.DisplayName, Is.EqualTo(displayName));
            Assert.That(profile.VisualId, Is.EqualTo(visualId));
        }

        [Test]
        public void SeededSelection_IsDeterministic()
        {
            FishingV3FishProfileSelector first = new FishingV3FishProfileSelector(
                FishingV3FishProfileSelectionMode.SeededRandom,
                731);
            FishingV3FishProfileSelector second = new FishingV3FishProfileSelector(
                FishingV3FishProfileSelectionMode.SeededRandom,
                731);

            for (int i = 0; i < 12; i++)
            {
                Assert.That(first.SelectNext().Id, Is.EqualTo(second.SelectNext().Id));
            }
        }

        [Test]
        public void DefaultCompatibilitySeed_StartsWithNormalProfile()
        {
            FishingV3FishProfileSelector selector = new FishingV3FishProfileSelector(
                FishingV3FishProfileSelectionMode.SeededRandom,
                731);

            Assert.That(selector.SelectNext().Id, Is.EqualTo(FishingV3FishProfileId.Normal));
        }

        [TestCase(FishingV3FishProfileSelectionMode.ForceSmall, FishingV3FishProfileId.Small)]
        [TestCase(FishingV3FishProfileSelectionMode.ForceNormal, FishingV3FishProfileId.Normal)]
        [TestCase(FishingV3FishProfileSelectionMode.ForceStrong, FishingV3FishProfileId.Strong)]
        public void ForcedSelection_AlwaysReturnsRequestedProfile(
            FishingV3FishProfileSelectionMode mode,
            FishingV3FishProfileId expected)
        {
            FishingV3FishProfileSelector selector =
                new FishingV3FishProfileSelector(mode, 123);

            for (int i = 0; i < 6; i++)
            {
                Assert.That(selector.SelectNext().Id, Is.EqualTo(expected));
            }
        }

        [Test]
        public void NormalProfile_PreservesExistingTuningsExactly()
        {
            FishingV3Tuning model = new FishingV3Tuning();
            FishingV3FishBehaviorTuning behavior = new FishingV3FishBehaviorTuning();
            FishingV3TimingReelTuning timing = new FishingV3TimingReelTuning();

            FishingV3ProfileTuningSet applied = FishingV3FishProfileTuning.Apply(
                FishingV3FishProfiles.Create(FishingV3FishProfileId.Normal),
                model,
                behavior,
                timing);

            AssertPublicFieldsEqual(model, applied.Model);
            AssertPublicFieldsEqual(behavior, applied.Behavior);
            AssertPublicFieldsEqual(timing, applied.Timing);
        }

        [Test]
        public void DifficultyProfiles_ScaleTimingWindowsAndSpeedInExpectedOrder()
        {
            FishingV3ProfileTuningSet small = Apply(FishingV3FishProfileId.Small);
            FishingV3ProfileTuningSet normal = Apply(FishingV3FishProfileId.Normal);
            FishingV3ProfileTuningSet strong = Apply(FishingV3FishProfileId.Strong);

            Assert.That(small.Timing.FightPointerSpeedNormalizedPerSecond,
                Is.LessThan(normal.Timing.FightPointerSpeedNormalizedPerSecond));
            Assert.That(strong.Timing.FightPointerSpeedNormalizedPerSecond,
                Is.GreaterThan(normal.Timing.FightPointerSpeedNormalizedPerSecond));
            Assert.That(small.Timing.FightPerfectHalfWidthNormalized,
                Is.GreaterThan(normal.Timing.FightPerfectHalfWidthNormalized));
            Assert.That(strong.Timing.FightPerfectHalfWidthNormalized,
                Is.LessThan(normal.Timing.FightPerfectHalfWidthNormalized));
            Assert.That(small.Timing.FightGoodHalfWidthNormalized,
                Is.GreaterThan(normal.Timing.FightGoodHalfWidthNormalized));
            Assert.That(strong.Timing.FightGoodHalfWidthNormalized,
                Is.LessThan(normal.Timing.FightGoodHalfWidthNormalized));
            Assert.That(small.Timing.PerfectSuccessfulReelSupportNormalized,
                Is.GreaterThan(normal.Timing.PerfectSuccessfulReelSupportNormalized));
            Assert.That(strong.Timing.PerfectSuccessfulReelSupportNormalized,
                Is.EqualTo(normal.Timing.PerfectSuccessfulReelSupportNormalized));
            Assert.That(small.Timing.SuccessfulReelSupportDecayPerSecond,
                Is.LessThan(normal.Timing.SuccessfulReelSupportDecayPerSecond));
            Assert.That(strong.Timing.SuccessfulReelSupportDecayPerSecond,
                Is.EqualTo(normal.Timing.SuccessfulReelSupportDecayPerSecond));
        }

        [Test]
        public void ProfileBehavior_ProducesOrderedAverageTensionAndRunShare()
        {
            BehaviorMetrics small = SimulateBehavior(FishingV3FishProfileId.Small);
            BehaviorMetrics normal = SimulateBehavior(FishingV3FishProfileId.Normal);
            BehaviorMetrics strong = SimulateBehavior(FishingV3FishProfileId.Strong);

            Assert.That(small.AverageTension, Is.LessThan(normal.AverageTension));
            Assert.That(strong.AverageTension, Is.GreaterThan(normal.AverageTension));
            Assert.That(small.RunShare, Is.LessThan(normal.RunShare));
            Assert.That(strong.RunShare, Is.GreaterThan(normal.RunShare));
        }

        [Test]
        public void ProfileCaptureScale_ProducesOrderedProgressFromSameInput()
        {
            float small = CaptureFromSingleSafeInput(FishingV3FishProfileId.Small);
            float normal = CaptureFromSingleSafeInput(FishingV3FishProfileId.Normal);
            float strong = CaptureFromSingleSafeInput(FishingV3FishProfileId.Strong);

            Assert.That(small, Is.GreaterThan(normal));
            Assert.That(strong, Is.LessThan(normal));
        }

        [TestCase(FishingV3FishProfileId.Small)]
        [TestCase(FishingV3FishProfileId.Normal)]
        [TestCase(FishingV3FishProfileId.Strong)]
        public void EveryProfile_AccurateTimingCanStillCatchFish(
            FishingV3FishProfileId id)
        {
            AccuratePlayMetrics metrics =
                SimulateAccurateFight(id);

            Assert.That(metrics.Result, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(metrics.SuccessfulInputs, Is.GreaterThan(0));
            Assert.That(metrics.ElapsedSeconds, Is.LessThan(30f));
        }

        [Test]
        public void MashInput_IsMoreDangerousForStrongProfile()
        {
            float small = SimulateMashLineBreakTime(FishingV3FishProfileId.Small);
            float normal = SimulateMashLineBreakTime(FishingV3FishProfileId.Normal);
            float strong = SimulateMashLineBreakTime(FishingV3FishProfileId.Strong);

            Assert.That(strong, Is.LessThan(normal));
            Assert.That(normal, Is.LessThan(small));
        }

        [Test]
        public void Profiles_PreserveSharedFailureRules()
        {
            FishingV3ProfileTuningSet small = Apply(FishingV3FishProfileId.Small);
            FishingV3ProfileTuningSet normal = Apply(FishingV3FishProfileId.Normal);
            FishingV3ProfileTuningSet strong = Apply(FishingV3FishProfileId.Strong);

            Assert.That(small.Model.BreakStressPerSecond,
                Is.EqualTo(normal.Model.BreakStressPerSecond));
            Assert.That(strong.Model.BreakStressPerSecond,
                Is.EqualTo(normal.Model.BreakStressPerSecond));
            Assert.That(small.Model.EscapeRiskPerSecond,
                Is.EqualTo(normal.Model.EscapeRiskPerSecond));
            Assert.That(strong.Model.EscapeRiskPerSecond,
                Is.EqualTo(normal.Model.EscapeRiskPerSecond));
        }

        [Test]
        public void SmallProfile_StillUsesSharedLineBreakAndEscapeResults()
        {
            FishingV3ProfileTuningSet tuning = Apply(FishingV3FishProfileId.Small);
            FishingV3Model breakModel = new FishingV3Model(tuning.Model);
            for (int i = 0; i < 40 && breakModel.Result == FishingV3Result.Active; i++)
            {
                breakModel.Tick(FishingV3FishState.Run, 0f, 0.1f, 1f);
            }

            FishingV3Model escapeModel = new FishingV3Model(tuning.Model);
            for (int i = 0; i < 40 && escapeModel.Result == FishingV3Result.Active; i++)
            {
                escapeModel.Tick(FishingV3FishState.Calm, 0f, 0.1f, -1f);
            }

            Assert.That(breakModel.Result, Is.EqualTo(FishingV3Result.LineBroken));
            Assert.That(escapeModel.Result, Is.EqualTo(FishingV3Result.FishEscaped));
        }

        [TestCase(FishingV3FishProfileId.Small)]
        [TestCase(FishingV3FishProfileId.Normal)]
        [TestCase(FishingV3FishProfileId.Strong)]
        public void RuntimeSnapshot_ExposesSelectedProfileAndResetsLifecycle(
            FishingV3FishProfileId id)
        {
            FishingV3Tuning tuning = new FishingV3Tuning
            {
                FightBaseTension = 0.45f,
                ReelTensionGain = 0f,
                CaptureScale = 100f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
            FishingV3FishProfile profile = FishingV3FishProfiles.Create(id);
            FishingV3Runtime runtime = new FishingV3Runtime(
                modelTuning: tuning,
                reelInputTuning: new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                },
                fishProfile: profile);

            Assert.That(runtime.Current.FishProfileId, Is.EqualTo(id));
            Assert.That(runtime.Current.FishVisualId, Is.EqualTo(profile.VisualId));
            runtime.Begin();
            runtime.SetFishState(FishingV3FishState.Fight);
            runtime.Tick(new FishingInputFrame
            {
                ReelDelta = 1f,
                IsDeviceConnected = true
            }, 1f);
            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Caught));

            runtime.Begin();

            Assert.That(runtime.Result, Is.EqualTo(FishingV3Result.Active));
            Assert.That(runtime.Current.FishProfileId, Is.EqualTo(id));
            Assert.That(runtime.Current.CaptureProgressNormalized, Is.Zero);
        }

        private static FishingV3ProfileTuningSet Apply(FishingV3FishProfileId id)
        {
            return FishingV3FishProfileTuning.Apply(FishingV3FishProfiles.Create(id));
        }

        private static float CaptureFromSingleSafeInput(FishingV3FishProfileId id)
        {
            FishingV3ProfileTuningSet tuning = Apply(id);
            tuning.Model.BreakStressPerSecond = 0f;
            tuning.Model.EscapeRiskPerSecond = 0f;
            FishingV3Model model = new FishingV3Model(tuning.Model);
            model.Tick(FishingV3FishState.Fight, 0.1f, 0.1f);
            return model.CaptureProgressNormalized;
        }

        private static BehaviorMetrics SimulateBehavior(FishingV3FishProfileId id)
        {
            FishingV3ProfileTuningSet tuning = Apply(id);
            tuning.Model.BreakStressPerSecond = 0f;
            tuning.Model.EscapeRiskPerSecond = 0f;
            tuning.Behavior.RandomSeed = 823;
            FishingV3FishBehavior behavior = new FishingV3FishBehavior(tuning.Behavior);
            FishingV3Model model = new FishingV3Model(tuning.Model);
            behavior.SetState(FishingV3FishState.Fight);

            const float deltaTime = 0.02f;
            const int samples = 3000;
            double tensionSum = 0d;
            int runSamples = 0;
            for (int i = 0; i < samples; i++)
            {
                behavior.Tick(deltaTime);
                model.Tick(
                    behavior.State,
                    0f,
                    deltaTime,
                    behavior.TensionOffsetNormalized);
                tensionSum += model.TensionNormalized;
                if (behavior.State == FishingV3FishState.Run) runSamples++;
            }

            return new BehaviorMetrics(
                (float)(tensionSum / samples),
                (float)runSamples / samples);
        }

        private static AccuratePlayMetrics SimulateAccurateFight(
            FishingV3FishProfileId id)
        {
            FishingV3ProfileTuningSet tuning = Apply(id);
            FishingV3TimingReel timing = new FishingV3TimingReel(tuning.Timing);
            FishingV3Model model = new FishingV3Model(tuning.Model);
            float speed = tuning.Timing.GetPointerSpeed(FishingV3FishState.Fight);
            float elapsed = 0f;
            int successfulInputs = 0;

            float firstCenter = 0.5f / speed;
            timing.Tick(FishingV3FishState.Fight, firstCenter);
            model.Tick(FishingV3FishState.Fight, 0f, firstCenter);
            elapsed += firstCenter;

            while (model.Result == FishingV3Result.Active && elapsed < 30f)
            {
                FishingV3TimingJudgement judgement =
                    timing.Judge(FishingV3FishState.Fight);
                Assert.That(judgement.Grade, Is.EqualTo(FishingV3TimingGrade.Perfect));
                model.Tick(
                    FishingV3FishState.Fight,
                    judgement.ReelInput.ReelDeltaRevolutions,
                    0.01f,
                    timing.SuccessfulReelSupportNormalized);
                elapsed += 0.01f;
                successfulInputs++;
                if (model.Result != FishingV3Result.Active) break;

                float nextCenter = 1f / speed;
                timing.Tick(FishingV3FishState.Fight, nextCenter);
                model.Tick(
                    FishingV3FishState.Fight,
                    0f,
                    nextCenter,
                    timing.SuccessfulReelSupportNormalized);
                elapsed += nextCenter;
            }

            return new AccuratePlayMetrics(model.Result, elapsed, successfulInputs);
        }

        private static float SimulateMashLineBreakTime(FishingV3FishProfileId id)
        {
            FishingV3ProfileTuningSet tuning = Apply(id);
            tuning.Model.EscapeRiskPerSecond = 0f;
            FishingV3TimingReel timing = new FishingV3TimingReel(tuning.Timing);
            FishingV3Model model = new FishingV3Model(tuning.Model);

            const float deltaTime = 0.01f;
            for (int i = 1; i <= 600; i++)
            {
                timing.Tick(FishingV3FishState.Run, deltaTime);
                FishingV3TimingJudgement judgement =
                    timing.Judge(FishingV3FishState.Run);
                model.Tick(
                    FishingV3FishState.Run,
                    judgement.ReelInput.ReelDeltaRevolutions,
                    deltaTime,
                    timing.MissPenaltyNormalized);
                if (model.Result == FishingV3Result.LineBroken)
                {
                    return i * deltaTime;
                }
            }

            return float.PositiveInfinity;
        }

        private static void AssertPublicFieldsEqual<T>(T expected, T actual)
        {
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(expected)), field.Name);
            }
        }

        private readonly struct BehaviorMetrics
        {
            public float AverageTension { get; }
            public float RunShare { get; }

            public BehaviorMetrics(float averageTension, float runShare)
            {
                AverageTension = averageTension;
                RunShare = runShare;
            }
        }

        private readonly struct AccuratePlayMetrics
        {
            public FishingV3Result Result { get; }
            public float ElapsedSeconds { get; }
            public int SuccessfulInputs { get; }

            public AccuratePlayMetrics(
                FishingV3Result result,
                float elapsedSeconds,
                int successfulInputs)
            {
                Result = result;
                ElapsedSeconds = elapsedSeconds;
                SuccessfulInputs = successfulInputs;
            }
        }
    }
}
