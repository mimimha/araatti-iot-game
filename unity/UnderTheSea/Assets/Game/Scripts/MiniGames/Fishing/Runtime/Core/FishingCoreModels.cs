using System;

namespace FishingMiniGame.Core
{
    public enum FishingPlayerState
    {
        Idle,
        Casting,
        Waiting,
        BiteWindow,
        Hooked,
        Fighting,
        Caught,
        Escaped,
        Cooldown
    }

    public enum FishingEscapeReason
    {
        None,
        MissedBite,
        SlackLine,
        LineBroken,
        FightTimedOut,
        InputDisconnected
    }

    public enum FishingFeedbackState
    {
        None,
        Bite,
        Fight,
        Run,
        Rest,
        Caught,
        Escaped
    }

    public enum FishingRoundState
    {
        Uninitialized,
        Ready,
        Countdown,
        Playing,
        Finishing,
        Completed,
        Aborted
    }

    public enum FishingRoundEndReason
    {
        None,
        TimeExpired,
        ManualStop,
        Aborted
    }

    public struct FishingInputFrame
    {
        public string ParticipantId;
        public long Sequence;
        public double TimestampSeconds;
        public bool CastPressed;
        public bool CastReleased;
        public bool HookPressed;
        public float ReelDelta;
        public float TensionNormalized;
        public float RodPitch;
        public float RodYaw;
        public float MotionStrength;
        public bool IsDeviceConnected;
    }

    public struct FishingFeedbackFrame
    {
        public FishingFeedbackState State;
        public float Intensity;

        public FishingFeedbackFrame(FishingFeedbackState state, float intensity)
        {
            State = state;
            Intensity = FishingMath.Clamp01(intensity);
        }
    }

    public sealed class FishingRules
    {
        public float MaxCastChargeSeconds = 1.5f;
        public float MinBiteDelaySeconds = 1.5f;
        public float MaxBiteDelaySeconds = 3.5f;
        public float HookWindowSeconds = 0.9f;
        public float HookSettleSeconds = 0.35f;
        public float FightTimeoutSeconds = 25f;
        public float SafeTensionMin = 0.30f;
        public float SafeTensionMax = 0.75f;
        public float SlackEscapeSeconds = 2.0f;
        public float HighTensionGraceSeconds = 1.2f;
        public float HighTensionDamagePerSecond = 60f;
        public float FishDamagePerSecond = 36f;
        public float OutcomeDisplaySeconds = 1.3f;
        public float CooldownSeconds = 1.0f;

        public FishingRules Copy()
        {
            return (FishingRules)MemberwiseClone();
        }

        public void Sanitize()
        {
            MaxCastChargeSeconds = FishingMath.Max(0.1f, MaxCastChargeSeconds);
            MinBiteDelaySeconds = FishingMath.Max(0.05f, MinBiteDelaySeconds);
            MaxBiteDelaySeconds = FishingMath.Max(MinBiteDelaySeconds, MaxBiteDelaySeconds);
            HookWindowSeconds = FishingMath.Max(0.05f, HookWindowSeconds);
            HookSettleSeconds = FishingMath.Max(0f, HookSettleSeconds);
            FightTimeoutSeconds = FishingMath.Max(1f, FightTimeoutSeconds);
            SafeTensionMin = FishingMath.Clamp01(SafeTensionMin);
            SafeTensionMax = FishingMath.Clamp(SafeTensionMax, SafeTensionMin, 1f);
            SlackEscapeSeconds = FishingMath.Max(0.1f, SlackEscapeSeconds);
            HighTensionGraceSeconds = FishingMath.Max(0.1f, HighTensionGraceSeconds);
            HighTensionDamagePerSecond = FishingMath.Max(0f, HighTensionDamagePerSecond);
            FishDamagePerSecond = FishingMath.Max(0.1f, FishDamagePerSecond);
            OutcomeDisplaySeconds = FishingMath.Max(0f, OutcomeDisplaySeconds);
            CooldownSeconds = FishingMath.Max(0f, CooldownSeconds);
        }
    }

    public sealed class FishProfile
    {
        public string FishId = "training_carp";
        public string DisplayName = "Training Carp";
        public string DifficultyLabel = "Normal";
        public int BaseScore = 100;
        public float MaxStamina = 100f;
        public float PullStrength = 0.5f;
        public bool UseSpeciesRuleOverrides;
        public float MinBiteDelaySeconds = 1.5f;
        public float MaxBiteDelaySeconds = 3.5f;
        public float HookWindowSeconds = 0.9f;
        public float FightTimeoutSeconds = 25f;
        public float ReelEfficiencyMultiplier = 1f;
        public float DifficultyScoreMultiplier = 1f;
        public float RarityWeight = 1f;
        public string VisualKey = "training_carp";
        public float MinBehaviorPhaseSeconds = 0.8f;
        public float MaxBehaviorPhaseSeconds = 1.8f;
        public float RunChance = 0.45f;
        public float RestChance = 0.25f;
        public float MaxTensionShift = 0.18f;
        public float DirectionChangeChance = 0.6f;

        public FishProfile Copy()
        {
            return (FishProfile)MemberwiseClone();
        }

        public void Sanitize()
        {
            FishId = string.IsNullOrWhiteSpace(FishId) ? "training_carp" : FishId;
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? FishId : DisplayName;
            DifficultyLabel = string.IsNullOrWhiteSpace(DifficultyLabel) ? "Normal" : DifficultyLabel;
            BaseScore = FishingMath.Max(0, BaseScore);
            MaxStamina = FishingMath.Max(1f, MaxStamina);
            PullStrength = FishingMath.Clamp01(PullStrength);
            MinBiteDelaySeconds = FishingMath.Max(0.05f, MinBiteDelaySeconds);
            MaxBiteDelaySeconds = FishingMath.Max(MinBiteDelaySeconds, MaxBiteDelaySeconds);
            HookWindowSeconds = FishingMath.Max(0.05f, HookWindowSeconds);
            FightTimeoutSeconds = FishingMath.Max(1f, FightTimeoutSeconds);
            ReelEfficiencyMultiplier = FishingMath.Max(0.1f, ReelEfficiencyMultiplier);
            DifficultyScoreMultiplier = FishingMath.Max(0.1f, DifficultyScoreMultiplier);
            RarityWeight = FishingMath.Max(0.01f, RarityWeight);
            VisualKey = string.IsNullOrWhiteSpace(VisualKey) ? FishId : VisualKey;
            MinBehaviorPhaseSeconds = FishingMath.Max(0.35f, MinBehaviorPhaseSeconds);
            MaxBehaviorPhaseSeconds = FishingMath.Max(MinBehaviorPhaseSeconds, MaxBehaviorPhaseSeconds);
            RunChance = FishingMath.Clamp01(RunChance);
            RestChance = FishingMath.Clamp(RestChance, 0f, 1f - RunChance);
            MaxTensionShift = FishingMath.Clamp(MaxTensionShift, 0.02f, 0.42f);
            DirectionChangeChance = FishingMath.Clamp01(DirectionChangeChance);
        }
    }

    public sealed class FishingCycleResult
    {
        public int CycleNumber;
        public bool WasCaught;
        public FishingEscapeReason EscapeReason;
        public string FishId;
        public string FishDisplayName;
        public string DifficultyLabel;
        public int AwardedScore;
        public int TotalScoreAfterCycle;
        public float CastPower;
        public float RemainingLineDurability;
    }

    public sealed class FishingSnapshot
    {
        public FishingPlayerState State { get; internal set; }
        public float StateElapsedSeconds { get; internal set; }
        public float CastPower { get; internal set; }
        public float BiteDelayRemainingSeconds { get; internal set; }
        public float HookWindowRemainingSeconds { get; internal set; }
        public float FightRemainingSeconds { get; internal set; }
        public float TensionNormalized { get; internal set; }
        public float RawTensionNormalized { get; internal set; }
        public float FishTensionShift { get; internal set; }
        public float FightPhaseRemainingSeconds { get; internal set; }
        public float FightDirection { get; internal set; }
        public float DirectionalControlNormalized { get; internal set; }
        public float FishHealth { get; internal set; }
        public float FishMaxHealth { get; internal set; }
        public float LineDurability { get; internal set; }
        public float SlackDangerNormalized { get; internal set; }
        public float HighTensionDangerNormalized { get; internal set; }
        public float FishDistanceMeters { get; internal set; }
        public float FishStaminaNormalized { get; internal set; }
        public float VirtualLineTensionNormalized { get; internal set; }
        public float BreakStressNormalized { get; internal set; }
        public float HookLooseRiskNormalized { get; internal set; }
        public float RodResponseQualityNormalized { get; internal set; }
        public float ReelEfficiencyNormalized { get; internal set; }
        public float FightElapsedSeconds { get; internal set; }
        public float V2FishForceNormalized { get; internal set; }
        public float V2FishDirectionNormalized { get; internal set; }
        public FishingV2BehaviorState V2BehaviorState { get; internal set; }
        public FishingV2StaminaBand AIStaminaBand { get; internal set; }
        public float AIPhaseRemainingSeconds { get; internal set; }
        public bool IsRunTelegraphing { get; internal set; }
        public float RunTelegraphDirectionNormalized { get; internal set; }
        public float RunTelegraphRemainingSeconds { get; internal set; }
        public bool HeadShakeActive { get; internal set; }
        public float HeadShakeIntensityNormalized { get; internal set; }
        public int HeadShakeEventSequence { get; internal set; }
        public bool FinalRunDecisionMade { get; internal set; }
        public bool FinalRunPending { get; internal set; }
        public bool IsFinalRun { get; internal set; }
        public bool FinalRunUsed { get; internal set; }
        public FishingV2TensionZone VirtualTensionZone { get; internal set; }
        public int TotalScore { get; internal set; }
        public int CompletedCycles { get; internal set; }
        public string FishId { get; internal set; }
        public string FishDisplayName { get; internal set; }
        public string DifficultyLabel { get; internal set; }
        public string Hint { get; internal set; }
        public string LastOutcome { get; internal set; }
        public FishingFeedbackFrame Feedback { get; internal set; }
        public FishingEscapeReason EscapeReason { get; internal set; }
        public bool IsNibbling { get; internal set; }
        public int NibbleEventSequence { get; internal set; }
        public float NibbleRemainingSeconds { get; internal set; }
        public float NibbleIntensityNormalized { get; internal set; }
        public int BiteEventSequence { get; internal set; }
        public int EarlyHookCount { get; internal set; }
        public int MissedBiteRetryCount { get; internal set; }
        public int FalseStrikeCount { get; internal set; }
    }

    public sealed class FishingRoundContext
    {
        public string ParticipantId = "local-player";
        public int Seed = 20260821;
        public FishingRules Rules = new FishingRules();
        public FishProfile Fish = new FishProfile();
        public bool UseV2PreFightFlow;
        public FishingV2PreFightTuning V2PreFightTuning = new FishingV2PreFightTuning();
        public bool UseV2FightModel;
        public FishingV2FightTuning V2FightTuning = new FishingV2FightTuning();
        public FishingV2FishAITuning V2FishAITuning = new FishingV2FishAITuning();
    }

    public static class FishingMath
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        public static float Max(float left, float right)
        {
            return left > right ? left : right;
        }

        public static int Max(int left, int right)
        {
            return left > right ? left : right;
        }

        public static float Min(float left, float right)
        {
            return left < right ? left : right;
        }
    }
}
