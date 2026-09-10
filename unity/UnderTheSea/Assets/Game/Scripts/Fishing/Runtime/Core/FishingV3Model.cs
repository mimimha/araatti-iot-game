using System;

namespace FishingMiniGame.Core
{
    public enum FishingV3FishState
    {
        Calm,
        Fight,
        Run
    }

    public enum FishingV3TensionZone
    {
        Slack,
        Low,
        Safe,
        High,
        Danger
    }

    public enum FishingV3Result
    {
        Active,
        Caught,
        LineBroken,
        FishEscaped
    }

    /// <summary>
    /// Tunable constants for the device-independent V3 fishing calculation.
    /// ReelDelta conversion, fish state transitions and presentation are owned by
    /// higher layers and deliberately do not belong here.
    /// </summary>
    public sealed class FishingV3Tuning
    {
        public float InitialTensionNormalized = 0.45f;

        public float CalmBaseTension = 0.20f;
        public float FightBaseTension = 0.45f;
        public float RunBaseTension = 0.75f;

        public float ReferenceReelRate = 1f;
        public float ReelTensionGain = 0.25f;
        public float TensionRisePerSecond = 1.5f;
        public float TensionFallPerSecond = 1.2f;

        public float SlackUpperThreshold = 0.15f;
        public float LowUpperThreshold = 0.30f;
        public float SafeUpperThreshold = 0.75f;
        public float HighUpperThreshold = 0.90f;

        public float CaptureScale = 1f;
        public float BreakStressPerSecond = 0.5f;
        public float BreakStressRecoveryPerSecond = 0.4f;
        public float EscapeRiskPerSecond = 0.5f;
        public float EscapeRiskRecoveryPerSecond = 0.4f;

        public FishingV3Tuning Copy()
        {
            return (FishingV3Tuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            InitialTensionNormalized = SanitizeNormalized(InitialTensionNormalized, 0.45f);
            CalmBaseTension = SanitizeNormalized(CalmBaseTension, 0.20f);
            FightBaseTension = SanitizeNormalized(FightBaseTension, 0.45f);
            RunBaseTension = SanitizeNormalized(RunBaseTension, 0.75f);

            ReferenceReelRate = SanitizePositive(ReferenceReelRate, 1f);
            ReelTensionGain = SanitizeNonNegative(ReelTensionGain, 0.25f);
            TensionRisePerSecond = SanitizeNonNegative(TensionRisePerSecond, 1.5f);
            TensionFallPerSecond = SanitizeNonNegative(TensionFallPerSecond, 1.2f);

            SlackUpperThreshold = SanitizeThreshold(
                SlackUpperThreshold,
                0.15f,
                0.001f,
                0.96f);
            LowUpperThreshold = SanitizeThreshold(
                LowUpperThreshold,
                0.30f,
                SlackUpperThreshold + 0.001f,
                0.97f);
            SafeUpperThreshold = SanitizeThreshold(
                SafeUpperThreshold,
                0.75f,
                LowUpperThreshold + 0.001f,
                0.98f);
            HighUpperThreshold = SanitizeThreshold(
                HighUpperThreshold,
                0.90f,
                SafeUpperThreshold + 0.001f,
                0.999f);

            CaptureScale = SanitizeNonNegative(CaptureScale, 1f);
            BreakStressPerSecond = SanitizeNonNegative(BreakStressPerSecond, 0.5f);
            BreakStressRecoveryPerSecond = SanitizeNonNegative(
                BreakStressRecoveryPerSecond,
                0.4f);
            EscapeRiskPerSecond = SanitizeNonNegative(EscapeRiskPerSecond, 0.5f);
            EscapeRiskRecoveryPerSecond = SanitizeNonNegative(
                EscapeRiskRecoveryPerSecond,
                0.4f);
        }

        private static float SanitizeNormalized(float value, float fallback)
        {
            return IsFinite(value) ? FishingMath.Clamp01(value) : fallback;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return IsFinite(value) && value > 0f ? value : fallback;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f ? value : fallback;
        }

        private static float SanitizeThreshold(
            float value,
            float fallback,
            float minimum,
            float maximum)
        {
            float finiteValue = IsFinite(value) ? value : fallback;
            return FishingMath.Clamp(finiteValue, minimum, maximum);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Pure V3 calculation model. The caller supplies the current fish state and
    /// canonical amount reeled during the tick; the model owns only tension,
    /// capture, risk accumulation and the terminal result.
    /// </summary>
    public sealed class FishingV3Model
    {
        private FishingV3Tuning _tuning;

        public FishingV3FishState FishState { get; private set; }
        public float ReelRateNormalized { get; private set; }
        public float TargetTensionNormalized { get; private set; }
        public float TensionNormalized { get; private set; }
        public FishingV3TensionZone TensionZone { get; private set; }
        public float CaptureProgressNormalized { get; private set; }
        public float BreakStressNormalized { get; private set; }
        public float EscapeRiskNormalized { get; private set; }
        public FishingV3Result Result { get; private set; }

        public FishingV3Model(FishingV3Tuning tuning = null)
        {
            Reset(tuning);
        }

        public void Reset(FishingV3Tuning tuning = null)
        {
            _tuning = (tuning ?? new FishingV3Tuning()).Copy();
            _tuning.Sanitize();

            FishState = FishingV3FishState.Calm;
            ReelRateNormalized = 0f;
            TargetTensionNormalized = GetBaseTension(FishState);
            TensionNormalized = _tuning.InitialTensionNormalized;
            TensionZone = ClassifyTension(TensionNormalized);
            CaptureProgressNormalized = 0f;
            BreakStressNormalized = 0f;
            EscapeRiskNormalized = 0f;
            Result = FishingV3Result.Active;
        }

        public void Tick(FishingV3FishState fishState, float reelDelta, float deltaTime)
        {
            if (Result != FishingV3Result.Active) return;
            if (!IsFinite(deltaTime) || deltaTime <= 0f) return;

            FishState = NormalizeFishState(fishState);
            float positiveReelDelta = IsFinite(reelDelta) && reelDelta > 0f
                ? reelDelta
                : 0f;
            ReelRateNormalized = CalculateNormalizedReelRate(positiveReelDelta, deltaTime);

            TargetTensionNormalized = FishingMath.Clamp01(
                GetBaseTension(FishState) + _tuning.ReelTensionGain * ReelRateNormalized);
            float smoothingRate = TargetTensionNormalized > TensionNormalized
                ? _tuning.TensionRisePerSecond
                : _tuning.TensionFallPerSecond;
            TensionNormalized = MoveTowardsNormalized(
                TensionNormalized,
                TargetTensionNormalized,
                smoothingRate,
                deltaTime);
            TensionZone = ClassifyTension(TensionNormalized);

            if (TensionZone == FishingV3TensionZone.Safe && positiveReelDelta > 0f)
            {
                CaptureProgressNormalized = FishingMath.Clamp01(
                    CaptureProgressNormalized + positiveReelDelta * _tuning.CaptureScale);
            }

            UpdateRisks(deltaTime);
            ResolveTerminalResult();
        }

        public FishingV3TensionZone ClassifyTension(float normalizedTension)
        {
            float tension = IsFinite(normalizedTension)
                ? FishingMath.Clamp01(normalizedTension)
                : 0f;
            if (tension < _tuning.SlackUpperThreshold) return FishingV3TensionZone.Slack;
            if (tension < _tuning.LowUpperThreshold) return FishingV3TensionZone.Low;
            if (tension < _tuning.SafeUpperThreshold) return FishingV3TensionZone.Safe;
            if (tension < _tuning.HighUpperThreshold) return FishingV3TensionZone.High;
            return FishingV3TensionZone.Danger;
        }

        private float CalculateNormalizedReelRate(float positiveReelDelta, float deltaTime)
        {
            double reelRate = (double)positiveReelDelta / deltaTime;
            if (reelRate <= 0d) return 0f;
            if (reelRate >= _tuning.ReferenceReelRate) return 1f;
            return FishingMath.Clamp01((float)(reelRate / _tuning.ReferenceReelRate));
        }

        private float GetBaseTension(FishingV3FishState state)
        {
            switch (state)
            {
                case FishingV3FishState.Run:
                    return _tuning.RunBaseTension;
                case FishingV3FishState.Fight:
                    return _tuning.FightBaseTension;
                default:
                    return _tuning.CalmBaseTension;
            }
        }

        private void UpdateRisks(float deltaTime)
        {
            if (TensionZone == FishingV3TensionZone.Danger)
            {
                BreakStressNormalized += _tuning.BreakStressPerSecond * deltaTime;
            }
            else
            {
                BreakStressNormalized -= _tuning.BreakStressRecoveryPerSecond * deltaTime;
            }

            if (TensionZone == FishingV3TensionZone.Slack)
            {
                EscapeRiskNormalized += _tuning.EscapeRiskPerSecond * deltaTime;
            }
            else
            {
                EscapeRiskNormalized -= _tuning.EscapeRiskRecoveryPerSecond * deltaTime;
            }

            BreakStressNormalized = FishingMath.Clamp01(BreakStressNormalized);
            EscapeRiskNormalized = FishingMath.Clamp01(EscapeRiskNormalized);
        }

        private void ResolveTerminalResult()
        {
            if (BreakStressNormalized >= 1f)
            {
                Result = FishingV3Result.LineBroken;
            }
            else if (EscapeRiskNormalized >= 1f)
            {
                Result = FishingV3Result.FishEscaped;
            }
            else if (CaptureProgressNormalized >= 1f)
            {
                Result = FishingV3Result.Caught;
            }
        }

        private static FishingV3FishState NormalizeFishState(FishingV3FishState state)
        {
            return state == FishingV3FishState.Fight || state == FishingV3FishState.Run
                ? state
                : FishingV3FishState.Calm;
        }

        private static float MoveTowardsNormalized(
            float current,
            float target,
            float ratePerSecond,
            float deltaTime)
        {
            current = IsFinite(current) ? FishingMath.Clamp01(current) : 0f;
            target = IsFinite(target) ? FishingMath.Clamp01(target) : 0f;
            if (!IsFinite(ratePerSecond) || ratePerSecond <= 0f) return current;

            double maximumDelta = (double)ratePerSecond * deltaTime;
            if (maximumDelta >= 1d || Math.Abs(target - current) <= maximumDelta)
            {
                return target;
            }

            return FishingMath.Clamp01(
                current + (target > current ? (float)maximumDelta : -(float)maximumDelta));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
