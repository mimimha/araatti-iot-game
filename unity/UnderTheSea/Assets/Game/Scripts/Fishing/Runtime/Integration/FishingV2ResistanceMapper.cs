using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Maps only the public FishingSnapshot contract to a normalized resistance
    /// command. Runtime safety policy (mode, session, pause and connection) remains
    /// owned by FishingGameController.
    /// </summary>
    public sealed class FishingV2ResistanceMapper
    {
        public const float RestMinimum = 0.15f;
        public const float RestMaximum = 0.25f;
        public const float FightMinimum = 0.35f;
        public const float FightMaximum = 0.50f;
        public const float RunMinimum = 0.65f;
        public const float RunMaximum = 0.82f;
        public const float FinalRunMultiplier = 1.12f;
        public const float FinalRunBaseCap = 0.92f;
        public const float HeadShakeDurationSeconds = 0.36f;
        public const float HeadShakeFrequencyHz = 8f;
        public const float HeadShakeMaximumOverlay = 0.18f;
        public const float RiseRatePerSecond = 1.8f;
        public const float FallRatePerSecond = 2.8f;
        public const float MaximumDeltaTimeSeconds = 0.1f;

        private float _smoothedBase;
        private float _headShakePulseRemaining;
        private float _headShakePulseElapsed;
        private int _lastHeadShakeSequence;

        public FishingResistanceCommand CurrentCommand { get; private set; } = FishingResistanceCommand.Zero;
        public float SmoothedBaseResistanceNormalized => _smoothedBase;
        public float HeadShakePulseRemainingSeconds => _headShakePulseRemaining;
        public int LastHeadShakeSequence => _lastHeadShakeSequence;

        public FishingResistanceCommand Tick(FishingSnapshot snapshot, float deltaTime)
        {
            if (snapshot == null || snapshot.State != FishingPlayerState.Fighting)
            {
                ResetToSafe(snapshot);
                return CurrentCommand;
            }

            float dt = SanitizeDeltaTime(deltaTime);
            ConsumeHeadShakeSequence(snapshot.HeadShakeEventSequence);

            float targetBase = CalculateTargetBase(snapshot);
            float rate = targetBase >= _smoothedBase ? RiseRatePerSecond : FallRatePerSecond;
            _smoothedBase = MoveTowards(_smoothedBase, targetBase, rate * dt);
            _smoothedBase = SanitizeNormalized(_smoothedBase);

            float overlay = EvaluateHeadShakeOverlay(snapshot.HeadShakeIntensityNormalized, dt);
            float total = SanitizeNormalized(_smoothedBase + overlay);
            bool pulseActive = _headShakePulseRemaining > 0f;
            bool finalRun = snapshot.V2BehaviorState == FishingV2BehaviorState.Run && snapshot.IsFinalRun;
            CurrentCommand = new FishingResistanceCommand(
                total,
                _smoothedBase,
                overlay,
                pulseActive,
                snapshot.V2BehaviorState,
                finalRun);
            return CurrentCommand;
        }

        /// <summary>
        /// Clears all temporal state immediately and synchronizes the event sequence.
        /// Null is accepted for lifecycle paths where no snapshot exists yet.
        /// </summary>
        public void ResetToSafe(FishingSnapshot snapshot)
        {
            _smoothedBase = 0f;
            _headShakePulseRemaining = 0f;
            _headShakePulseElapsed = 0f;
            _lastHeadShakeSequence = snapshot == null
                ? 0
                : Math.Max(0, snapshot.HeadShakeEventSequence);
            CurrentCommand = FishingResistanceCommand.Zero;
        }

        private static float CalculateTargetBase(FishingSnapshot snapshot)
        {
            float force = SanitizeNormalized(snapshot.V2FishForceNormalized);
            float target;
            switch (snapshot.V2BehaviorState)
            {
                case FishingV2BehaviorState.Rest:
                    target = Lerp(RestMinimum, RestMaximum, force);
                    break;
                case FishingV2BehaviorState.Fight:
                    target = Lerp(FightMinimum, FightMaximum, force);
                    break;
                case FishingV2BehaviorState.Run:
                    target = Lerp(RunMinimum, RunMaximum, force);
                    if (snapshot.IsFinalRun)
                    {
                        target = Math.Min(target * FinalRunMultiplier, FinalRunBaseCap);
                    }
                    break;
                default:
                    // A Fighting snapshot without a recognized behavior is a valid
                    // active-control request for zero, so its fall uses normal slew.
                    target = 0f;
                    break;
            }

            return SanitizeNormalized(target);
        }

        private void ConsumeHeadShakeSequence(int rawSequence)
        {
            int sequence = Math.Max(0, rawSequence);
            if (sequence == _lastHeadShakeSequence) return;

            if (sequence > _lastHeadShakeSequence)
            {
                _lastHeadShakeSequence = sequence;
                _headShakePulseRemaining = HeadShakeDurationSeconds;
                _headShakePulseElapsed = 0f;
                return;
            }

            _lastHeadShakeSequence = sequence;
            _headShakePulseRemaining = 0f;
            _headShakePulseElapsed = 0f;
        }

        private float EvaluateHeadShakeOverlay(float rawIntensity, float deltaTime)
        {
            if (_headShakePulseRemaining <= 0f)
            {
                _headShakePulseRemaining = 0f;
                _headShakePulseElapsed = 0f;
                return 0f;
            }

            _headShakePulseElapsed = Math.Min(
                HeadShakeDurationSeconds,
                _headShakePulseElapsed + deltaTime);
            _headShakePulseRemaining = Math.Max(
                0f,
                HeadShakeDurationSeconds - _headShakePulseElapsed);
            if (_headShakePulseRemaining <= 0f) return 0f;

            float normalizedTime = _headShakePulseElapsed / HeadShakeDurationSeconds;
            float envelope = (float)Math.Sin(Math.PI * normalizedTime);
            float carrier = 0.30f + 0.70f * (float)Math.Abs(
                Math.Sin(2d * Math.PI * HeadShakeFrequencyHz * _headShakePulseElapsed));
            float intensity = SanitizeNormalized(rawIntensity);
            return SanitizeNormalized(
                HeadShakeMaximumOverlay * intensity * envelope * carrier);
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return 0f;
            return Math.Max(0f, Math.Min(MaximumDeltaTimeSeconds, deltaTime));
        }

        private static float SanitizeNormalized(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return FishingMath.Clamp01(value);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            current = SanitizeNormalized(current);
            target = SanitizeNormalized(target);
            maxDelta = float.IsNaN(maxDelta) || float.IsInfinity(maxDelta)
                ? 0f
                : Math.Max(0f, maxDelta);
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * SanitizeNormalized(amount);
        }
    }
}
