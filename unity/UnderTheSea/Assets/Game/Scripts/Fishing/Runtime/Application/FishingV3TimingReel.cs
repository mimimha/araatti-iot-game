using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    public enum FishingV3ReelControlMode
    {
        LegacyHold,
        Timing
    }

    public enum FishingV3TimingGrade
    {
        None,
        Perfect,
        Good,
        Miss
    }

    [Serializable]
    public sealed class FishingV3TimingReelTuning
    {
        public float CalmPointerSpeedNormalizedPerSecond = 0.55f;
        public float FightPointerSpeedNormalizedPerSecond = 0.85f;
        public float RunPointerSpeedNormalizedPerSecond = 1.25f;

        public float CalmPerfectHalfWidthNormalized = 0.10f;
        public float FightPerfectHalfWidthNormalized = 0.075f;
        public float RunPerfectHalfWidthNormalized = 0.05f;

        public float CalmGoodHalfWidthNormalized = 0.24f;
        public float FightGoodHalfWidthNormalized = 0.18f;
        public float RunGoodHalfWidthNormalized = 0.13f;

        public float PerfectReelDeltaRevolutions = 0.16f;
        public float GoodReelDeltaRevolutions = 0.08f;

        public float PerfectSuccessfulReelSupportNormalized = 0.10f;
        public float GoodSuccessfulReelSupportNormalized = 0.05f;
        public float MaximumSuccessfulReelSupportNormalized = 0.12f;
        public float SuccessfulReelSupportDecayPerSecond = 0.025f;

        public float MissPenaltyPerInputNormalized = 0.10f;
        public float MaximumMissPenaltyNormalized = 0.22f;
        public float MissPenaltyRecoveryPerSecond = 0.28f;

        public FishingV3TimingReelTuning Copy()
        {
            return (FishingV3TimingReelTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            CalmPointerSpeedNormalizedPerSecond = NonNegative(
                CalmPointerSpeedNormalizedPerSecond,
                0.55f);
            FightPointerSpeedNormalizedPerSecond = NonNegative(
                FightPointerSpeedNormalizedPerSecond,
                0.85f);
            RunPointerSpeedNormalizedPerSecond = NonNegative(
                RunPointerSpeedNormalizedPerSecond,
                1.25f);

            CalmPerfectHalfWidthNormalized = HalfWidth(
                CalmPerfectHalfWidthNormalized,
                0.10f);
            FightPerfectHalfWidthNormalized = HalfWidth(
                FightPerfectHalfWidthNormalized,
                0.075f);
            RunPerfectHalfWidthNormalized = HalfWidth(
                RunPerfectHalfWidthNormalized,
                0.05f);

            CalmGoodHalfWidthNormalized = GoodHalfWidth(
                CalmGoodHalfWidthNormalized,
                CalmPerfectHalfWidthNormalized,
                0.24f);
            FightGoodHalfWidthNormalized = GoodHalfWidth(
                FightGoodHalfWidthNormalized,
                FightPerfectHalfWidthNormalized,
                0.18f);
            RunGoodHalfWidthNormalized = GoodHalfWidth(
                RunGoodHalfWidthNormalized,
                RunPerfectHalfWidthNormalized,
                0.13f);

            PerfectReelDeltaRevolutions = NonNegative(
                PerfectReelDeltaRevolutions,
                0.16f);
            GoodReelDeltaRevolutions = NonNegative(
                GoodReelDeltaRevolutions,
                0.10f);
            if (GoodReelDeltaRevolutions > PerfectReelDeltaRevolutions)
            {
                GoodReelDeltaRevolutions = PerfectReelDeltaRevolutions;
            }

            MaximumSuccessfulReelSupportNormalized = Normalized(
                MaximumSuccessfulReelSupportNormalized,
                0.12f);
            PerfectSuccessfulReelSupportNormalized = Normalized(
                PerfectSuccessfulReelSupportNormalized,
                0.08f);
            GoodSuccessfulReelSupportNormalized = Normalized(
                GoodSuccessfulReelSupportNormalized,
                0.05f);
            PerfectSuccessfulReelSupportNormalized = Math.Min(
                PerfectSuccessfulReelSupportNormalized,
                MaximumSuccessfulReelSupportNormalized);
            GoodSuccessfulReelSupportNormalized = Math.Min(
                GoodSuccessfulReelSupportNormalized,
                PerfectSuccessfulReelSupportNormalized);
            SuccessfulReelSupportDecayPerSecond = NonNegative(
                SuccessfulReelSupportDecayPerSecond,
                0.025f);

            MissPenaltyPerInputNormalized = Normalized(
                MissPenaltyPerInputNormalized,
                0.10f);
            MaximumMissPenaltyNormalized = Normalized(
                MaximumMissPenaltyNormalized,
                0.22f);
            if (MissPenaltyPerInputNormalized > MaximumMissPenaltyNormalized)
            {
                MissPenaltyPerInputNormalized = MaximumMissPenaltyNormalized;
            }
            MissPenaltyRecoveryPerSecond = NonNegative(
                MissPenaltyRecoveryPerSecond,
                0.28f);
        }

        public float GetPointerSpeed(FishingV3FishState state)
        {
            switch (state)
            {
                case FishingV3FishState.Run:
                    return RunPointerSpeedNormalizedPerSecond;
                case FishingV3FishState.Fight:
                    return FightPointerSpeedNormalizedPerSecond;
                default:
                    return CalmPointerSpeedNormalizedPerSecond;
            }
        }

        public float GetPerfectHalfWidth(FishingV3FishState state)
        {
            switch (state)
            {
                case FishingV3FishState.Run:
                    return RunPerfectHalfWidthNormalized;
                case FishingV3FishState.Fight:
                    return FightPerfectHalfWidthNormalized;
                default:
                    return CalmPerfectHalfWidthNormalized;
            }
        }

        public float GetGoodHalfWidth(FishingV3FishState state)
        {
            switch (state)
            {
                case FishingV3FishState.Run:
                    return RunGoodHalfWidthNormalized;
                case FishingV3FishState.Fight:
                    return FightGoodHalfWidthNormalized;
                default:
                    return CalmGoodHalfWidthNormalized;
            }
        }

        private static float GoodHalfWidth(float value, float perfect, float fallback)
        {
            float sanitized = HalfWidth(value, fallback);
            return Math.Max(perfect, sanitized);
        }

        private static float HalfWidth(float value, float fallback)
        {
            return IsFinite(value)
                ? FishingMath.Clamp(value, 0f, 0.5f)
                : fallback;
        }

        private static float Normalized(float value, float fallback)
        {
            return IsFinite(value) ? FishingMath.Clamp01(value) : fallback;
        }

        private static float NonNegative(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct FishingV3TimingJudgement
    {
        public FishingV3TimingGrade Grade { get; }
        public FishingV3ReelInput ReelInput { get; }
        public bool WasAccepted { get; }

        public FishingV3TimingJudgement(
            FishingV3TimingGrade grade,
            FishingV3ReelInput reelInput,
            bool wasAccepted = true)
        {
            Grade = grade;
            ReelInput = reelInput;
            WasAccepted = wasAccepted;
        }
    }

    /// <summary>
    /// Device-independent timing layer. A host supplies a one-shot press and the
    /// result is converted to the same canonical reel revolution unit used by
    /// physical and legacy reel input paths. Each one-way pointer traversal is
    /// one opportunity: only its first press can produce reel delta. Additional
    /// presses remain on the existing MISS penalty path and never create reward.
    /// </summary>
    public sealed class FishingV3TimingReel
    {
        private const float Center = 0.5f;
        private readonly FishingV3TimingReelTuning _tuning;
        private float _direction = 1f;
        private bool _hasJudgedCurrentOpportunity;

        public float PointerNormalized { get; private set; }
        public float SuccessfulReelSupportNormalized { get; private set; }
        public float MissPenaltyNormalized { get; private set; }
        public FishingV3TimingGrade LastGrade { get; private set; }
        public int JudgementSequence { get; private set; }
        public int OpportunitySequence { get; private set; }
        public bool HasJudgedCurrentOpportunity => _hasJudgedCurrentOpportunity;

        public FishingV3TimingReel(FishingV3TimingReelTuning tuning = null)
        {
            _tuning = (tuning ?? new FishingV3TimingReelTuning()).Copy();
            _tuning.Sanitize();
            Reset();
        }

        public void Reset()
        {
            PointerNormalized = 0f;
            _direction = 1f;
            SuccessfulReelSupportNormalized = 0f;
            MissPenaltyNormalized = 0f;
            LastGrade = FishingV3TimingGrade.None;
            JudgementSequence = 0;
            OpportunitySequence = 0;
            _hasJudgedCurrentOpportunity = false;
        }

        public void Tick(FishingV3FishState state, float deltaTime)
        {
            if (!IsFinite(deltaTime) || deltaTime <= 0f) return;

            if (MovePointer(_tuning.GetPointerSpeed(state), deltaTime))
            {
                if (OpportunitySequence < int.MaxValue) OpportunitySequence++;
                _hasJudgedCurrentOpportunity = false;
            }
            MissPenaltyNormalized = MoveTowardsZero(
                MissPenaltyNormalized,
                _tuning.MissPenaltyRecoveryPerSecond,
                deltaTime);
            SuccessfulReelSupportNormalized = MoveTowardsZero(
                SuccessfulReelSupportNormalized,
                _tuning.SuccessfulReelSupportDecayPerSecond,
                deltaTime);
        }

        public FishingV3TimingJudgement Judge(FishingV3FishState state)
        {
            if (_hasJudgedCurrentOpportunity)
            {
                ApplyMissPenalty();
                LastGrade = FishingV3TimingGrade.Miss;
                if (JudgementSequence < int.MaxValue) JudgementSequence++;
                return new FishingV3TimingJudgement(
                    FishingV3TimingGrade.Miss,
                    FishingV3ReelInput.Zero,
                    false);
            }

            _hasJudgedCurrentOpportunity = true;
            float distance = Math.Abs(PointerNormalized - Center);
            FishingV3TimingGrade grade;
            float reelDelta;
            if (distance <= _tuning.GetPerfectHalfWidth(state))
            {
                grade = FishingV3TimingGrade.Perfect;
                reelDelta = _tuning.PerfectReelDeltaRevolutions;
                ApplySuccessfulReelSupport(
                    _tuning.PerfectSuccessfulReelSupportNormalized);
            }
            else if (distance <= _tuning.GetGoodHalfWidth(state))
            {
                grade = FishingV3TimingGrade.Good;
                reelDelta = _tuning.GoodReelDeltaRevolutions;
                ApplySuccessfulReelSupport(
                    _tuning.GoodSuccessfulReelSupportNormalized);
            }
            else
            {
                grade = FishingV3TimingGrade.Miss;
                reelDelta = 0f;
                ApplyMissPenalty();
            }

            LastGrade = grade;
            if (JudgementSequence < int.MaxValue) JudgementSequence++;
            return new FishingV3TimingJudgement(
                grade,
                new FishingV3ReelInput(reelDelta),
                true);
        }

        public float GetPointerSpeed(FishingV3FishState state)
        {
            return _tuning.GetPointerSpeed(state);
        }

        public float GetPerfectHalfWidth(FishingV3FishState state)
        {
            return _tuning.GetPerfectHalfWidth(state);
        }

        public float GetGoodHalfWidth(FishingV3FishState state)
        {
            return _tuning.GetGoodHalfWidth(state);
        }

        private bool MovePointer(float speed, float deltaTime)
        {
            if (speed <= 0f) return false;

            double phase = _direction > 0f
                ? PointerNormalized
                : 2d - PointerNormalized;
            double advancedPhase = phase + (double)speed * deltaTime;
            bool crossedEndpoint = Math.Floor(advancedPhase) > Math.Floor(phase);
            double wrapped = advancedPhase % 2d;
            if (wrapped < 0d) wrapped += 2d;

            if (wrapped < 1d)
            {
                PointerNormalized = (float)wrapped;
                _direction = 1f;
            }
            else
            {
                PointerNormalized = (float)(2d - wrapped);
                _direction = -1f;
            }

            return crossedEndpoint;
        }

        private void ApplyMissPenalty()
        {
            MissPenaltyNormalized = FishingMath.Clamp(
                MissPenaltyNormalized + _tuning.MissPenaltyPerInputNormalized,
                0f,
                _tuning.MaximumMissPenaltyNormalized);
        }

        public void ClearSuccessfulReelSupport()
        {
            SuccessfulReelSupportNormalized = 0f;
        }

        private void ApplySuccessfulReelSupport(float support)
        {
            SuccessfulReelSupportNormalized = FishingMath.Clamp(
                SuccessfulReelSupportNormalized + support,
                0f,
                _tuning.MaximumSuccessfulReelSupportNormalized);
        }

        private static float MoveTowardsZero(float value, float rate, float deltaTime)
        {
            if (!IsFinite(value) || value <= 0f) return 0f;
            if (!IsFinite(rate) || rate <= 0f) return value;
            double next = value - (double)rate * deltaTime;
            return next <= 0d ? 0f : FishingMath.Clamp01((float)next);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
