using System;

namespace FishingMiniGame.Core
{
    public enum FishingV2BehaviorState
    {
        None,
        Fight,
        Run,
        Rest
    }

    public enum FishingV2TensionZone
    {
        Slack,
        Low,
        Good,
        High,
        Danger
    }

    public enum FishingV2FailureCondition
    {
        None,
        LineBroken,
        SlackLine
    }

    public struct FishingV2BehaviorSample
    {
        public FishingV2BehaviorState State;
        public float ForceNormalized;
        public float DirectionNormalized;

        public FishingV2BehaviorSample(
            FishingV2BehaviorState state,
            float forceNormalized,
            float directionNormalized)
        {
            State = state;
            ForceNormalized = FishingMath.Clamp01(forceNormalized);
            DirectionNormalized = FishingMath.Clamp(directionNormalized, -1f, 1f);
        }
    }

    public sealed class FishingV2FightTuning
    {
        public float InitialDistanceMeters = 12f;
        public float CatchDistanceMeters = 2f;
        public float CatchStaminaNormalized = 0.15f;
        public float InitialVirtualTensionNormalized = 0.45f;

        public float RestBaseTension = 0.22f;
        public float FightBaseTension = 0.45f;
        public float RunBaseTension = 0.75f;
        public float FishForceTensionRange = 0.08f;

        public float RestReelStress = 0.08f;
        public float FightReelStress = 0.20f;
        public float RunReelStress = 0.35f;

        public float RestReelEfficiency = 1f;
        public float FightReelEfficiency = 0.50f;
        public float RunReelEfficiency = 0.05f;
        public float BaseRetrievalMetersPerSecond = 2.2f;
        public float RunDistanceGainMetersPerSecond = 0.90f;

        public float RunStaminaDrainPerSecond = 0.08f;
        public float FightStaminaDrainPerSecond = 0.065f;
        public float FightReelStaminaBonusPerSecond = 0.035f;
        public float RestStaminaDrainPerSecond = 0.004f;

        public float TensionRisePerSecond = 2.4f;
        public float TensionFallPerSecond = 1.25f;

        public float SlackThreshold = 0.15f;
        public float GoodThreshold = 0.30f;
        public float HighThreshold = 0.75f;
        public float DangerThreshold = 0.90f;
        public float SevereDangerThreshold = 0.97f;

        public float BreakStressPerSecond = 0.55f;
        public float SevereBreakStressBonusPerSecond = 0.75f;
        public float BreakStressRecoveryPerSecond = 0.45f;
        public float LooseRiskPerSecond = 0.50f;
        public float LooseRiskRecoveryThreshold = 0.25f;
        public float LooseRiskRecoveryPerSecond = 0.50f;

        public float RunGoodResponseTensionModifier = -0.18f;
        public float RunBadResponseTensionModifier = 0.10f;
        public float FightLowPitchTensionModifier = -0.32f;
        public float FightIdealPitch = 0.35f;
        public float FightIdealTensionModifier = -0.05f;
        public float FightHighPitchTensionModifier = 0.20f;
        public float RestLowPitchTensionModifier = -0.18f;
        public float HeadShakeMaxTensionModifier = 0.12f;

        public FishingV2FightTuning Copy()
        {
            return (FishingV2FightTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            InitialDistanceMeters = FishingMath.Max(0f, InitialDistanceMeters);
            CatchDistanceMeters = FishingMath.Clamp(CatchDistanceMeters, 0f, InitialDistanceMeters);
            CatchStaminaNormalized = FishingMath.Clamp01(CatchStaminaNormalized);
            InitialVirtualTensionNormalized = FishingMath.Clamp01(InitialVirtualTensionNormalized);
            RestBaseTension = FishingMath.Clamp01(RestBaseTension);
            FightBaseTension = FishingMath.Clamp01(FightBaseTension);
            RunBaseTension = FishingMath.Clamp01(RunBaseTension);
            FishForceTensionRange = FishingMath.Max(0f, FishForceTensionRange);
            RestReelStress = FishingMath.Max(0f, RestReelStress);
            FightReelStress = FishingMath.Max(0f, FightReelStress);
            RunReelStress = FishingMath.Max(0f, RunReelStress);
            RestReelEfficiency = FishingMath.Clamp01(RestReelEfficiency);
            FightReelEfficiency = FishingMath.Clamp01(FightReelEfficiency);
            RunReelEfficiency = FishingMath.Clamp01(RunReelEfficiency);
            BaseRetrievalMetersPerSecond = FishingMath.Max(0f, BaseRetrievalMetersPerSecond);
            RunDistanceGainMetersPerSecond = FishingMath.Max(0f, RunDistanceGainMetersPerSecond);
            RunStaminaDrainPerSecond = FishingMath.Max(0f, RunStaminaDrainPerSecond);
            FightStaminaDrainPerSecond = FishingMath.Max(0f, FightStaminaDrainPerSecond);
            FightReelStaminaBonusPerSecond = FishingMath.Max(0f, FightReelStaminaBonusPerSecond);
            RestStaminaDrainPerSecond = FishingMath.Max(0f, RestStaminaDrainPerSecond);
            TensionRisePerSecond = FishingMath.Max(0.01f, TensionRisePerSecond);
            TensionFallPerSecond = FishingMath.Max(0.01f, TensionFallPerSecond);
            SlackThreshold = FishingMath.Clamp01(SlackThreshold);
            GoodThreshold = FishingMath.Clamp(GoodThreshold, SlackThreshold, 1f);
            HighThreshold = FishingMath.Clamp(HighThreshold, GoodThreshold, 1f);
            DangerThreshold = FishingMath.Clamp(DangerThreshold, HighThreshold, 1f);
            SevereDangerThreshold = FishingMath.Clamp(SevereDangerThreshold, DangerThreshold, 1f);
            BreakStressPerSecond = FishingMath.Max(0f, BreakStressPerSecond);
            SevereBreakStressBonusPerSecond = FishingMath.Max(0f, SevereBreakStressBonusPerSecond);
            BreakStressRecoveryPerSecond = FishingMath.Max(0f, BreakStressRecoveryPerSecond);
            LooseRiskPerSecond = FishingMath.Max(0f, LooseRiskPerSecond);
            LooseRiskRecoveryThreshold = FishingMath.Clamp(LooseRiskRecoveryThreshold, SlackThreshold, 1f);
            LooseRiskRecoveryPerSecond = FishingMath.Max(0f, LooseRiskRecoveryPerSecond);
            FightIdealPitch = FishingMath.Clamp(FightIdealPitch, -0.99f, 0.99f);
            HeadShakeMaxTensionModifier = FishingMath.Clamp01(HeadShakeMaxTensionModifier);
        }
    }

    /// <summary>
    /// Pure fight metric model. It consumes player intent and an external behavior
    /// sample, but never owns fishing state transitions or behavior generation.
    /// </summary>
    public sealed class FishingV2FightModel
    {
        private FishingV2FightTuning _tuning;

        public float FishDistanceMeters { get; private set; }
        public float FishStaminaNormalized { get; private set; }
        public float VirtualLineTensionNormalized { get; private set; }
        public float TargetVirtualLineTensionNormalized { get; private set; }
        public float BreakStressNormalized { get; private set; }
        public float HookLooseRiskNormalized { get; private set; }
        public float RodResponseQualityNormalized { get; private set; }
        public float ReelEfficiencyNormalized { get; private set; }
        public FishingV2TensionZone TensionZone { get; private set; }
        public FishingV2FailureCondition FailureCondition { get; private set; }
        public bool CanCatch { get; private set; }

        public FishingV2FightModel(FishingV2FightTuning tuning = null)
        {
            Reset(tuning);
        }

        public void Reset(FishingV2FightTuning tuning = null)
        {
            _tuning = (tuning ?? new FishingV2FightTuning()).Copy();
            _tuning.Sanitize();
            FishDistanceMeters = _tuning.InitialDistanceMeters;
            FishStaminaNormalized = 1f;
            VirtualLineTensionNormalized = _tuning.InitialVirtualTensionNormalized;
            TargetVirtualLineTensionNormalized = VirtualLineTensionNormalized;
            BreakStressNormalized = 0f;
            HookLooseRiskNormalized = 0f;
            RodResponseQualityNormalized = 0.5f;
            ReelEfficiencyNormalized = 0f;
            TensionZone = ClassifyTension(VirtualLineTensionNormalized);
            FailureCondition = FishingV2FailureCondition.None;
            CanCatch = false;
        }

        public void Tick(
            float deltaTime,
            float reelInput,
            float rodPitch,
            float rodYaw,
            FishingV2BehaviorSample behavior,
            float eventTensionModifier = 0f)
        {
            if (FailureCondition != FishingV2FailureCondition.None) return;

            float dt = FishingMath.Clamp(deltaTime, 0f, 0.25f);
            if (dt <= 0f) return;
            float reel = FishingMath.Clamp01(reelInput);
            float pitch = FishingMath.Clamp(rodPitch, -1f, 1f);
            float yaw = FishingMath.Clamp(rodYaw, -1f, 1f);
            float force = FishingMath.Clamp01(behavior.ForceNormalized);
            FishingV2BehaviorState state = NormalizeBehaviorState(behavior.State);

            float baseTension;
            float reelStress;
            float rodModifier;
            float responseQuality;
            CalculateResponse(state, pitch, yaw, behavior.DirectionNormalized,
                out responseQuality, out rodModifier);
            RodResponseQualityNormalized = responseQuality;

            switch (state)
            {
                case FishingV2BehaviorState.Run:
                    baseTension = _tuning.RunBaseTension;
                    reelStress = _tuning.RunReelStress;
                    ReelEfficiencyNormalized = _tuning.RunReelEfficiency;
                    break;
                case FishingV2BehaviorState.Rest:
                    baseTension = _tuning.RestBaseTension;
                    reelStress = _tuning.RestReelStress;
                    ReelEfficiencyNormalized = _tuning.RestReelEfficiency;
                    break;
                default:
                    baseTension = _tuning.FightBaseTension;
                    reelStress = _tuning.FightReelStress;
                    ReelEfficiencyNormalized = _tuning.FightReelEfficiency;
                    break;
            }

            float forceOffset = (force - 0.5f) * _tuning.FishForceTensionRange;
            TargetVirtualLineTensionNormalized = FishingMath.Clamp01(
                baseTension + forceOffset + reelStress * reel + rodModifier +
                FishingMath.Clamp(eventTensionModifier, 0f, 1f));
            float tensionRate = TargetVirtualLineTensionNormalized > VirtualLineTensionNormalized
                ? _tuning.TensionRisePerSecond
                : _tuning.TensionFallPerSecond;
            VirtualLineTensionNormalized = MoveTowards(
                VirtualLineTensionNormalized,
                TargetVirtualLineTensionNormalized,
                tensionRate * dt);
            VirtualLineTensionNormalized = FishingMath.Clamp01(VirtualLineTensionNormalized);
            TensionZone = ClassifyTension(VirtualLineTensionNormalized);

            float retrieval = _tuning.BaseRetrievalMetersPerSecond * ReelEfficiencyNormalized * reel;
            float distanceGain = state == FishingV2BehaviorState.Run
                ? _tuning.RunDistanceGainMetersPerSecond * force
                : 0f;
            FishDistanceMeters = FishingMath.Max(0f, FishDistanceMeters + (distanceGain - retrieval) * dt);

            float staminaDrain = CalculateStaminaDrain(state, force, reel);
            FishStaminaNormalized = FishingMath.Clamp01(FishStaminaNormalized - staminaDrain * dt);
            UpdateRisks(dt);

            FailureCondition = BreakStressNormalized >= 1f
                ? FishingV2FailureCondition.LineBroken
                : HookLooseRiskNormalized >= 1f
                    ? FishingV2FailureCondition.SlackLine
                    : FishingV2FailureCondition.None;
            CanCatch = FailureCondition == FishingV2FailureCondition.None &&
                FishStaminaNormalized <= _tuning.CatchStaminaNormalized &&
                FishDistanceMeters <= _tuning.CatchDistanceMeters &&
                TensionZone != FishingV2TensionZone.Slack &&
                TensionZone != FishingV2TensionZone.Danger &&
                state != FishingV2BehaviorState.Run;
        }

        public FishingV2TensionZone ClassifyTension(float normalizedTension)
        {
            float tension = FishingMath.Clamp01(normalizedTension);
            if (tension < _tuning.SlackThreshold) return FishingV2TensionZone.Slack;
            if (tension < _tuning.GoodThreshold) return FishingV2TensionZone.Low;
            if (tension < _tuning.HighThreshold) return FishingV2TensionZone.Good;
            if (tension < _tuning.DangerThreshold) return FishingV2TensionZone.High;
            return FishingV2TensionZone.Danger;
        }

        private void CalculateResponse(
            FishingV2BehaviorState state,
            float pitch,
            float yaw,
            float direction,
            out float quality,
            out float tensionModifier)
        {
            if (state == FishingV2BehaviorState.Run)
            {
                float runDirection = FishingMath.Clamp(direction, -1f, 1f);
                quality = FishingMath.Clamp01(1f - Math.Abs(yaw - runDirection) * 0.5f);
                tensionModifier = Lerp(
                    _tuning.RunBadResponseTensionModifier,
                    _tuning.RunGoodResponseTensionModifier,
                    quality);
                return;
            }

            if (state == FishingV2BehaviorState.Rest)
            {
                quality = pitch < 0f
                    ? FishingMath.Clamp01(0.7f + pitch * 0.7f)
                    : FishingMath.Clamp01(0.7f + pitch * 0.2f);
                tensionModifier = pitch < 0f
                    ? -pitch * _tuning.RestLowPitchTensionModifier
                    : pitch * 0.04f;
                return;
            }

            float ideal = _tuning.FightIdealPitch;
            if (pitch <= ideal)
            {
                float range = ideal + 1f;
                quality = FishingMath.Clamp01(1f - (ideal - pitch) / range);
            }
            else
            {
                float range = 1f - ideal;
                quality = FishingMath.Clamp01(1f - (pitch - ideal) / range);
            }

            if (pitch < 0f)
            {
                tensionModifier = -pitch * _tuning.FightLowPitchTensionModifier;
            }
            else if (pitch <= ideal)
            {
                float amount = ideal <= 0f ? 1f : pitch / ideal;
                tensionModifier = Lerp(0f, _tuning.FightIdealTensionModifier, amount);
            }
            else
            {
                float amount = (pitch - ideal) / (1f - ideal);
                tensionModifier = Lerp(
                    _tuning.FightIdealTensionModifier,
                    _tuning.FightHighPitchTensionModifier,
                    amount);
            }
        }

        private float CalculateStaminaDrain(
            FishingV2BehaviorState state,
            float force,
            float reel)
        {
            if (state == FishingV2BehaviorState.Run)
            {
                return _tuning.RunStaminaDrainPerSecond * (0.55f + force * 0.45f);
            }
            if (state == FishingV2BehaviorState.Rest)
            {
                return _tuning.RestStaminaDrainPerSecond;
            }

            float goodPressure = TensionZone == FishingV2TensionZone.Good
                ? 0.45f + RodResponseQualityNormalized * 0.55f
                : RodResponseQualityNormalized * 0.15f;
            return _tuning.FightStaminaDrainPerSecond * goodPressure +
                (TensionZone == FishingV2TensionZone.Good
                    ? _tuning.FightReelStaminaBonusPerSecond * reel * RodResponseQualityNormalized
                    : 0f);
        }

        private void UpdateRisks(float dt)
        {
            if (VirtualLineTensionNormalized > _tuning.DangerThreshold)
            {
                float rate = _tuning.BreakStressPerSecond;
                if (VirtualLineTensionNormalized > _tuning.SevereDangerThreshold)
                {
                    rate += _tuning.SevereBreakStressBonusPerSecond;
                }
                BreakStressNormalized += rate * dt;
            }
            else if (VirtualLineTensionNormalized <= _tuning.HighThreshold)
            {
                BreakStressNormalized -= _tuning.BreakStressRecoveryPerSecond * dt;
            }

            if (VirtualLineTensionNormalized < _tuning.SlackThreshold)
            {
                HookLooseRiskNormalized += _tuning.LooseRiskPerSecond * dt;
            }
            else if (VirtualLineTensionNormalized >= _tuning.LooseRiskRecoveryThreshold)
            {
                HookLooseRiskNormalized -= _tuning.LooseRiskRecoveryPerSecond * dt;
            }

            BreakStressNormalized = FishingMath.Clamp01(BreakStressNormalized);
            HookLooseRiskNormalized = FishingMath.Clamp01(HookLooseRiskNormalized);
        }

        private static FishingV2BehaviorState NormalizeBehaviorState(FishingV2BehaviorState state)
        {
            return state == FishingV2BehaviorState.Run || state == FishingV2BehaviorState.Rest
                ? state
                : FishingV2BehaviorState.Fight;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + (target > current ? maxDelta : -maxDelta);
        }

        private static float Lerp(float from, float to, float amount)
        {
            return from + (to - from) * FishingMath.Clamp01(amount);
        }
    }
}
