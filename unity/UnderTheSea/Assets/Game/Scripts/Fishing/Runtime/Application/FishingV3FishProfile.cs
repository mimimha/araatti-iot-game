using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    public enum FishingV3FishProfileId
    {
        Small,
        Normal,
        Strong
    }

    public enum FishingV3FishProfileSelectionMode
    {
        SeededRandom,
        ForceSmall,
        ForceNormal,
        ForceStrong
    }

    /// <summary>
    /// Data-only difficulty profile. It modifies copies of the existing V3
    /// tunings; no profile owns a separate gameplay, timing or failure model.
    /// VisualId is the stable extension point for a future fish prefab catalog.
    /// </summary>
    [Serializable]
    public sealed class FishingV3FishProfile
    {
        public FishingV3FishProfileId Id = FishingV3FishProfileId.Normal;
        public string DisplayName = "Normal Fish";
        public string VisualId = "normal";

        public float CalmDurationScale = 1f;
        public float FightDurationScale = 1f;
        public float RunDurationScale = 1f;

        public float InitialTensionScale = 1f;
        public float CalmBaseTensionScale = 1f;
        public float FightBaseTensionScale = 1f;
        public float RunBaseTensionScale = 1f;
        public float OscillationStrengthScale = 1f;
        public float PullBurstStrengthScale = 1f;
        public float PullBurstFrequencyScale = 1f;

        public float TimingSpeedScale = 1f;
        public float PerfectWindowScale = 1f;
        public float GoodWindowScale = 1f;
        public float CaptureScale = 1f;

        public FishingV3FishProfile Copy()
        {
            return (FishingV3FishProfile)MemberwiseClone();
        }

        public void Sanitize()
        {
            DisplayName = string.IsNullOrWhiteSpace(DisplayName)
                ? DefaultDisplayName(Id)
                : DisplayName.Trim();
            VisualId = string.IsNullOrWhiteSpace(VisualId)
                ? Id.ToString().ToLowerInvariant()
                : VisualId.Trim();

            CalmDurationScale = PositiveScale(CalmDurationScale);
            FightDurationScale = PositiveScale(FightDurationScale);
            RunDurationScale = PositiveScale(RunDurationScale);
            InitialTensionScale = PositiveScale(InitialTensionScale);
            CalmBaseTensionScale = PositiveScale(CalmBaseTensionScale);
            FightBaseTensionScale = PositiveScale(FightBaseTensionScale);
            RunBaseTensionScale = PositiveScale(RunBaseTensionScale);
            OscillationStrengthScale = PositiveScale(OscillationStrengthScale);
            PullBurstStrengthScale = PositiveScale(PullBurstStrengthScale);
            PullBurstFrequencyScale = PositiveScale(PullBurstFrequencyScale);
            TimingSpeedScale = PositiveScale(TimingSpeedScale);
            PerfectWindowScale = PositiveScale(PerfectWindowScale);
            GoodWindowScale = PositiveScale(GoodWindowScale);
            CaptureScale = PositiveScale(CaptureScale);
        }

        private static float PositiveScale(float value)
        {
            return IsFinite(value) && value > 0f
                ? FishingMath.Clamp(value, 0.05f, 3f)
                : 1f;
        }

        private static string DefaultDisplayName(FishingV3FishProfileId id)
        {
            switch (id)
            {
                case FishingV3FishProfileId.Small:
                    return "Small Fish";
                case FishingV3FishProfileId.Strong:
                    return "Strong Fish";
                default:
                    return "Normal Fish";
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class FishingV3FishProfiles
    {
        public static FishingV3FishProfile Create(FishingV3FishProfileId id)
        {
            FishingV3FishProfile profile;
            switch (id)
            {
                case FishingV3FishProfileId.Small:
                    profile = new FishingV3FishProfile
                    {
                        Id = id,
                        DisplayName = "Small Fish",
                        VisualId = "small",
                        CalmDurationScale = 1.25f,
                        FightDurationScale = 0.80f,
                        RunDurationScale = 0.65f,
                        InitialTensionScale = 0.88f,
                        CalmBaseTensionScale = 0.85f,
                        FightBaseTensionScale = 0.88f,
                        RunBaseTensionScale = 0.80f,
                        OscillationStrengthScale = 0.72f,
                        PullBurstStrengthScale = 0.65f,
                        PullBurstFrequencyScale = 0.75f,
                        TimingSpeedScale = 0.80f,
                        PerfectWindowScale = 1.25f,
                        GoodWindowScale = 1.25f,
                        CaptureScale = 1.30f
                    };
                    break;

                case FishingV3FishProfileId.Strong:
                    profile = new FishingV3FishProfile
                    {
                        Id = id,
                        DisplayName = "Strong Fish",
                        VisualId = "strong",
                        CalmDurationScale = 0.75f,
                        FightDurationScale = 1.20f,
                        RunDurationScale = 1.35f,
                        InitialTensionScale = 1.12f,
                        CalmBaseTensionScale = 1.05f,
                        FightBaseTensionScale = 1.12f,
                        RunBaseTensionScale = 1.05f,
                        OscillationStrengthScale = 1.30f,
                        PullBurstStrengthScale = 1.35f,
                        PullBurstFrequencyScale = 1.30f,
                        TimingSpeedScale = 1.20f,
                        PerfectWindowScale = 0.78f,
                        GoodWindowScale = 0.78f,
                        CaptureScale = 0.72f
                    };
                    break;

                default:
                    profile = new FishingV3FishProfile();
                    break;
            }

            profile.Sanitize();
            return profile;
        }
    }

    public sealed class FishingV3FishProfileSelector
    {
        private readonly FishingV3FishProfileSelectionMode _mode;
        private readonly Random _random;

        public FishingV3FishProfileSelector(
            FishingV3FishProfileSelectionMode mode,
            int seed)
        {
            _mode = mode;
            _random = new Random(seed);
        }

        public FishingV3FishProfile SelectNext()
        {
            return FishingV3FishProfiles.Create(SelectNextId());
        }

        private FishingV3FishProfileId SelectNextId()
        {
            switch (_mode)
            {
                case FishingV3FishProfileSelectionMode.ForceSmall:
                    return FishingV3FishProfileId.Small;
                case FishingV3FishProfileSelectionMode.ForceStrong:
                    return FishingV3FishProfileId.Strong;
                case FishingV3FishProfileSelectionMode.ForceNormal:
                    return FishingV3FishProfileId.Normal;
                default:
                    return (FishingV3FishProfileId)_random.Next(0, 3);
            }
        }
    }

    public sealed class FishingV3ProfileTuningSet
    {
        public FishingV3FishProfile Profile { get; }
        public FishingV3Tuning Model { get; }
        public FishingV3FishBehaviorTuning Behavior { get; }
        public FishingV3TimingReelTuning Timing { get; }

        internal FishingV3ProfileTuningSet(
            FishingV3FishProfile profile,
            FishingV3Tuning model,
            FishingV3FishBehaviorTuning behavior,
            FishingV3TimingReelTuning timing)
        {
            Profile = profile;
            Model = model;
            Behavior = behavior;
            Timing = timing;
        }
    }

    public static class FishingV3FishProfileTuning
    {
        public static FishingV3ProfileTuningSet Apply(
            FishingV3FishProfile profile,
            FishingV3Tuning modelTuning = null,
            FishingV3FishBehaviorTuning behaviorTuning = null,
            FishingV3TimingReelTuning timingTuning = null)
        {
            FishingV3FishProfile safeProfile =
                (profile ?? FishingV3FishProfiles.Create(FishingV3FishProfileId.Normal)).Copy();
            safeProfile.Sanitize();

            FishingV3Tuning model = (modelTuning ?? new FishingV3Tuning()).Copy();
            FishingV3FishBehaviorTuning behavior =
                (behaviorTuning ?? new FishingV3FishBehaviorTuning()).Copy();
            FishingV3TimingReelTuning timing =
                (timingTuning ?? new FishingV3TimingReelTuning()).Copy();

            model.InitialTensionNormalized *= safeProfile.InitialTensionScale;
            model.CalmBaseTension *= safeProfile.CalmBaseTensionScale;
            model.FightBaseTension *= safeProfile.FightBaseTensionScale;
            model.RunBaseTension *= safeProfile.RunBaseTensionScale;
            model.CaptureScale *= safeProfile.CaptureScale;

            behavior.CalmDurationMinSeconds *= safeProfile.CalmDurationScale;
            behavior.CalmDurationMaxSeconds *= safeProfile.CalmDurationScale;
            behavior.FightDurationMinSeconds *= safeProfile.FightDurationScale;
            behavior.FightDurationMaxSeconds *= safeProfile.FightDurationScale;
            behavior.RunDurationMinSeconds *= safeProfile.RunDurationScale;
            behavior.RunDurationMaxSeconds *= safeProfile.RunDurationScale;
            behavior.CalmOscillationAmplitudeNormalized *=
                safeProfile.OscillationStrengthScale;
            behavior.FightOscillationAmplitudeNormalized *=
                safeProfile.OscillationStrengthScale;
            behavior.RunOscillationAmplitudeNormalized *=
                safeProfile.OscillationStrengthScale;
            behavior.FightPullBurstAmplitudeNormalized *=
                safeProfile.PullBurstStrengthScale;
            behavior.RunPullBurstAmplitudeNormalized *=
                safeProfile.PullBurstStrengthScale;
            ScaleBurstDelay(
                ref behavior.FightPullBurstDelayMinSeconds,
                ref behavior.FightPullBurstDelayMaxSeconds,
                safeProfile.PullBurstFrequencyScale);
            ScaleBurstDelay(
                ref behavior.RunPullBurstDelayMinSeconds,
                ref behavior.RunPullBurstDelayMaxSeconds,
                safeProfile.PullBurstFrequencyScale);

            timing.CalmPointerSpeedNormalizedPerSecond *= safeProfile.TimingSpeedScale;
            timing.FightPointerSpeedNormalizedPerSecond *= safeProfile.TimingSpeedScale;
            timing.RunPointerSpeedNormalizedPerSecond *= safeProfile.TimingSpeedScale;
            timing.CalmPerfectHalfWidthNormalized *= safeProfile.PerfectWindowScale;
            timing.FightPerfectHalfWidthNormalized *= safeProfile.PerfectWindowScale;
            timing.RunPerfectHalfWidthNormalized *= safeProfile.PerfectWindowScale;
            timing.CalmGoodHalfWidthNormalized *= safeProfile.GoodWindowScale;
            timing.FightGoodHalfWidthNormalized *= safeProfile.GoodWindowScale;
            timing.RunGoodHalfWidthNormalized *= safeProfile.GoodWindowScale;
            timing.PerfectSuccessfulReelSupportNormalized *=
                Math.Max(1f, safeProfile.PerfectWindowScale);
            timing.GoodSuccessfulReelSupportNormalized *=
                Math.Max(1f, safeProfile.GoodWindowScale);
            timing.SuccessfulReelSupportDecayPerSecond *=
                Math.Min(1f, safeProfile.TimingSpeedScale);

            model.Sanitize();
            behavior.Sanitize();
            timing.Sanitize();
            return new FishingV3ProfileTuningSet(safeProfile, model, behavior, timing);
        }

        private static void ScaleBurstDelay(
            ref float minimum,
            ref float maximum,
            float frequencyScale)
        {
            minimum /= frequencyScale;
            maximum /= frequencyScale;
        }
    }
}
