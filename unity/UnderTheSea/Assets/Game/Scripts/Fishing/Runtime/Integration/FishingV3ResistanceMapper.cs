using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Game-feel tuning for the transport-independent V3 resistance request.
    /// Hardware safety limits remain the responsibility of the future device layer.
    /// </summary>
    public sealed class FishingV3ResistanceTuning
    {
        public float MinimumResistanceNormalized = 0f;
        public float MaximumResistanceNormalized = 1f;
        public float RiseRatePerSecond = 1.5f;
        public float FallRatePerSecond = 2.5f;
        public float HeadShakeAmplitudeNormalized = 0.16f;
        public float HeadShakeDurationSeconds = 0.25f;

        public FishingV3ResistanceTuning Copy()
        {
            return (FishingV3ResistanceTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            MinimumResistanceNormalized = SanitizeNormalized(MinimumResistanceNormalized, 0f);
            MaximumResistanceNormalized = SanitizeNormalized(MaximumResistanceNormalized, 1f);
            MaximumResistanceNormalized = Math.Max(
                MinimumResistanceNormalized,
                MaximumResistanceNormalized);
            RiseRatePerSecond = SanitizeNonNegative(RiseRatePerSecond, 1.5f);
            FallRatePerSecond = SanitizeNonNegative(FallRatePerSecond, 2.5f);
            HeadShakeAmplitudeNormalized = SanitizeNormalized(
                HeadShakeAmplitudeNormalized,
                0.16f);
            HeadShakeDurationSeconds = SanitizeNonNegative(
                HeadShakeDurationSeconds,
                0.25f);
        }

        private static float SanitizeNormalized(float value, float fallback)
        {
            return IsFinite(value) ? FishingMath.Clamp01(value) : fallback;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Maps V3 tension to normalized resistance. Fish state, result calculation,
    /// lifecycle safety and transport are deliberately owned by other layers.
    /// </summary>
    public sealed class FishingV3ResistanceMapper
    {
        private readonly FishingV3ResistanceTuning _tuning;
        private float _smoothedBase;
        private float _headShakeRemaining;
        private float _headShakeAmplitude;

        public FishingResistanceCommand CurrentCommand { get; private set; } =
            FishingResistanceCommand.Zero;
        public float SmoothedBaseResistanceNormalized => _smoothedBase;
        public float HeadShakePulseRemainingSeconds => _headShakeRemaining;

        public FishingV3ResistanceMapper(FishingV3ResistanceTuning tuning = null)
        {
            _tuning = (tuning ?? new FishingV3ResistanceTuning()).Copy();
            _tuning.Sanitize();
            Reset();
        }

        public float CalculateBaseResistance(float tensionNormalized)
        {
            float tension = SanitizeNormalized(tensionNormalized);
            return SanitizeNormalized(
                _tuning.MinimumResistanceNormalized +
                (_tuning.MaximumResistanceNormalized - _tuning.MinimumResistanceNormalized) *
                tension);
        }

        public FishingResistanceCommand Tick(float tensionNormalized, float deltaTime)
        {
            float dt = SanitizeDeltaTime(deltaTime);
            float targetBase = CalculateBaseResistance(tensionNormalized);
            float rate = targetBase >= _smoothedBase
                ? _tuning.RiseRatePerSecond
                : _tuning.FallRatePerSecond;
            _smoothedBase = MoveTowards(_smoothedBase, targetBase, rate * dt);
            _smoothedBase = SanitizeNormalized(_smoothedBase);

            bool pulseActive = _headShakeRemaining > 0f && _headShakeAmplitude > 0f;
            float overlay = pulseActive ? _headShakeAmplitude : 0f;
            float total = SanitizeNormalized(_smoothedBase + overlay);

            if (_headShakeRemaining > 0f)
            {
                _headShakeRemaining = Math.Max(0f, _headShakeRemaining - dt);
                if (_headShakeRemaining <= 0f) _headShakeAmplitude = 0f;
            }

            CurrentCommand = new FishingResistanceCommand(
                total,
                _smoothedBase,
                overlay,
                pulseActive);
            return CurrentCommand;
        }

        public void TriggerHeadShake(float intensityNormalized = 1f)
        {
            float intensity = SanitizeNormalized(intensityNormalized);
            float amplitude = SanitizeNormalized(
                _tuning.HeadShakeAmplitudeNormalized * intensity);
            if (_tuning.HeadShakeDurationSeconds <= 0f || amplitude <= 0f) return;

            _headShakeRemaining = _tuning.HeadShakeDurationSeconds;
            _headShakeAmplitude = amplitude;
        }

        public void Reset()
        {
            _smoothedBase = 0f;
            _headShakeRemaining = 0f;
            _headShakeAmplitude = 0f;
            CurrentCommand = FishingResistanceCommand.Zero;
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return IsFinite(deltaTime) && deltaTime > 0f ? deltaTime : 0f;
        }

        private static float SanitizeNormalized(float value)
        {
            return IsFinite(value) ? FishingMath.Clamp01(value) : 0f;
        }

        private static float MoveTowards(float current, float target, float maximumDelta)
        {
            current = SanitizeNormalized(current);
            target = SanitizeNormalized(target);
            maximumDelta = IsFinite(maximumDelta) && maximumDelta > 0f
                ? maximumDelta
                : 0f;
            if (Math.Abs(target - current) <= maximumDelta) return target;
            return current + Math.Sign(target - current) * maximumDelta;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
