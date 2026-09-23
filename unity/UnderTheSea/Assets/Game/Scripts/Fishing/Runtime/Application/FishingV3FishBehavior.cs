using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Small, deterministic-friendly scheduler for the three V3 fish states.
    /// It owns only state timing and head-shake requests; tension, capture,
    /// failure, resistance mapping and hardware safety remain in their existing layers.
    /// </summary>
    public sealed class FishingV3FishBehaviorTuning
    {
        public float CalmDurationMinSeconds = 2f;
        public float CalmDurationMaxSeconds = 4f;
        public float FightDurationMinSeconds = 2f;
        public float FightDurationMaxSeconds = 4f;
        public float RunDurationMinSeconds = 1f;
        public float RunDurationMaxSeconds = 3f;
        public float RunHeadShakeDelayMinSeconds = 0.25f;
        public float RunHeadShakeDelayMaxSeconds = 0.8f;
        public float HeadShakeIntensityMinNormalized = 0.65f;
        public float HeadShakeIntensityMaxNormalized = 1f;
        public float CalmOscillationAmplitudeNormalized = 0.045f;
        public float CalmOscillationFrequencyHz = 0.22f;
        public float FightOscillationAmplitudeNormalized = 0.065f;
        public float FightOscillationFrequencyHz = 0.65f;
        public float RunOscillationAmplitudeNormalized = 0.08f;
        public float RunOscillationFrequencyHz = 1.05f;
        public float FightPullBurstAmplitudeNormalized = 0.075f;
        public float FightPullBurstDurationSeconds = 0.65f;
        public float FightPullBurstDelayMinSeconds = 0.6f;
        public float FightPullBurstDelayMaxSeconds = 1.8f;
        public float RunPullBurstAmplitudeNormalized = 0.1f;
        public float RunPullBurstDurationSeconds = 0.75f;
        public float RunPullBurstDelayMinSeconds = 0.25f;
        public float RunPullBurstDelayMaxSeconds = 0.75f;
        public int RandomSeed = 3107;

        public FishingV3FishBehaviorTuning Copy()
        {
            return (FishingV3FishBehaviorTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            SanitizeRange(
                ref CalmDurationMinSeconds,
                ref CalmDurationMaxSeconds,
                2f,
                4f,
                0.01f);
            SanitizeRange(
                ref FightDurationMinSeconds,
                ref FightDurationMaxSeconds,
                2f,
                4f,
                0.01f);
            SanitizeRange(
                ref RunDurationMinSeconds,
                ref RunDurationMaxSeconds,
                1f,
                3f,
                0.01f);
            SanitizeRange(
                ref RunHeadShakeDelayMinSeconds,
                ref RunHeadShakeDelayMaxSeconds,
                0.25f,
                0.8f,
                0f);
            SanitizeRange(
                ref HeadShakeIntensityMinNormalized,
                ref HeadShakeIntensityMaxNormalized,
                0.65f,
                1f,
                0f,
                1f);
            CalmOscillationAmplitudeNormalized = SanitizeNormalized(
                CalmOscillationAmplitudeNormalized,
                0.045f);
            CalmOscillationFrequencyHz = SanitizeNonNegative(
                CalmOscillationFrequencyHz,
                0.22f);
            FightOscillationAmplitudeNormalized = SanitizeNormalized(
                FightOscillationAmplitudeNormalized,
                0.065f);
            FightOscillationFrequencyHz = SanitizeNonNegative(
                FightOscillationFrequencyHz,
                0.65f);
            RunOscillationAmplitudeNormalized = SanitizeNormalized(
                RunOscillationAmplitudeNormalized,
                0.08f);
            RunOscillationFrequencyHz = SanitizeNonNegative(
                RunOscillationFrequencyHz,
                1.05f);
            FightPullBurstAmplitudeNormalized = SanitizeNormalized(
                FightPullBurstAmplitudeNormalized,
                0.075f);
            FightPullBurstDurationSeconds = SanitizeNonNegative(
                FightPullBurstDurationSeconds,
                0.65f);
            SanitizeRange(
                ref FightPullBurstDelayMinSeconds,
                ref FightPullBurstDelayMaxSeconds,
                0.6f,
                1.8f,
                0f);
            RunPullBurstAmplitudeNormalized = SanitizeNormalized(
                RunPullBurstAmplitudeNormalized,
                0.1f);
            RunPullBurstDurationSeconds = SanitizeNonNegative(
                RunPullBurstDurationSeconds,
                0.75f);
            SanitizeRange(
                ref RunPullBurstDelayMinSeconds,
                ref RunPullBurstDelayMaxSeconds,
                0.25f,
                0.75f,
                0f);
        }

        private static void SanitizeRange(
            ref float minimum,
            ref float maximum,
            float fallbackMinimum,
            float fallbackMaximum,
            float lowerBound,
            float upperBound = float.MaxValue)
        {
            minimum = IsFinite(minimum)
                ? FishingMath.Clamp(minimum, lowerBound, upperBound)
                : fallbackMinimum;
            maximum = IsFinite(maximum)
                ? FishingMath.Clamp(maximum, lowerBound, upperBound)
                : fallbackMaximum;
            if (maximum < minimum) maximum = minimum;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float SanitizeNormalized(float value, float fallback)
        {
            return IsFinite(value) ? FishingMath.Clamp01(value) : fallback;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f ? value : fallback;
        }
    }

    public sealed class FishingV3FishBehavior
    {
        private const int MaximumTransitionsPerTick = 256;

        private readonly FishingV3FishBehaviorTuning _tuning;
        private Random _random;
        private bool _nextFightTransitionsToRun;
        private float _phaseRemainingSeconds;
        private float _phaseElapsedSeconds;
        private float _headShakeDelaySeconds;
        private float _oscillationPhaseRadians;
        private float _pullBurstStartSeconds;
        private float _pullBurstDurationSeconds;
        private float _pullBurstAmplitudeNormalized;

        public FishingV3FishState State { get; private set; }
        public float PhaseRemainingSeconds => _phaseRemainingSeconds;
        public int HeadShakeEventSequence { get; private set; }
        public float LastHeadShakeIntensityNormalized { get; private set; }
        public float OscillationOffsetNormalized { get; private set; }
        public float PullBurstOffsetNormalized { get; private set; }
        public float TensionOffsetNormalized { get; private set; }

        public FishingV3FishBehavior(FishingV3FishBehaviorTuning tuning = null)
        {
            _tuning = (tuning ?? new FishingV3FishBehaviorTuning()).Copy();
            _tuning.Sanitize();
            Reset();
        }

        public void Reset(FishingV3FishState initialState = FishingV3FishState.Calm)
        {
            _random = new Random(_tuning.RandomSeed);
            _nextFightTransitionsToRun = true;
            HeadShakeEventSequence = 0;
            LastHeadShakeIntensityNormalized = 0f;
            BeginPhase(NormalizeState(initialState));
        }

        public void SetState(FishingV3FishState state)
        {
            BeginPhase(NormalizeState(state));
        }

        public void Tick(float deltaTime)
        {
            if (!IsFinite(deltaTime) || deltaTime <= 0f) return;

            float remaining = deltaTime;
            int transitions = 0;
            while (remaining > 0f && transitions < MaximumTransitionsPerTick)
            {
                float step = Math.Min(remaining, _phaseRemainingSeconds);
                AdvanceHeadShake(step);
                _phaseElapsedSeconds += step;
                _phaseRemainingSeconds = Math.Max(0f, _phaseRemainingSeconds - step);
                remaining -= step;
                EvaluateTensionOffsets();

                if (_phaseRemainingSeconds > 0f) break;

                BeginPhase(SelectNextState());
                transitions++;
            }
        }

        private void BeginPhase(FishingV3FishState state)
        {
            State = state;
            _phaseRemainingSeconds = RandomDurationFor(state);
            _phaseElapsedSeconds = 0f;
            _headShakeDelaySeconds = -1f;
            _oscillationPhaseRadians =
                (float)(_random.NextDouble() * Math.PI * 2d);
            ConfigurePullBurst(state);
            EvaluateTensionOffsets();

            if (state != FishingV3FishState.Run) return;

            float latestDelay = Math.Min(
                _tuning.RunHeadShakeDelayMaxSeconds,
                _phaseRemainingSeconds * 0.8f);
            float earliestDelay = Math.Min(
                _tuning.RunHeadShakeDelayMinSeconds,
                latestDelay);
            _headShakeDelaySeconds = RandomRange(earliestDelay, latestDelay);
        }

        private void ConfigurePullBurst(FishingV3FishState state)
        {
            _pullBurstStartSeconds = -1f;
            _pullBurstDurationSeconds = 0f;
            _pullBurstAmplitudeNormalized = 0f;

            float delayMinimum;
            float delayMaximum;
            switch (state)
            {
                case FishingV3FishState.Fight:
                    _pullBurstDurationSeconds =
                        _tuning.FightPullBurstDurationSeconds;
                    _pullBurstAmplitudeNormalized =
                        _tuning.FightPullBurstAmplitudeNormalized;
                    delayMinimum = _tuning.FightPullBurstDelayMinSeconds;
                    delayMaximum = _tuning.FightPullBurstDelayMaxSeconds;
                    break;
                case FishingV3FishState.Run:
                    _pullBurstDurationSeconds =
                        _tuning.RunPullBurstDurationSeconds;
                    _pullBurstAmplitudeNormalized =
                        _tuning.RunPullBurstAmplitudeNormalized;
                    delayMinimum = _tuning.RunPullBurstDelayMinSeconds;
                    delayMaximum = _tuning.RunPullBurstDelayMaxSeconds;
                    break;
                default:
                    return;
            }

            if (_pullBurstDurationSeconds <= 0f ||
                _pullBurstAmplitudeNormalized <= 0f)
            {
                return;
            }

            float latestStart = Math.Max(
                0f,
                _phaseRemainingSeconds - _pullBurstDurationSeconds);
            float maximum = Math.Min(delayMaximum, latestStart);
            float minimum = Math.Min(delayMinimum, maximum);
            _pullBurstStartSeconds = RandomRange(minimum, maximum);
        }

        private void EvaluateTensionOffsets()
        {
            float amplitude;
            float frequency;
            switch (State)
            {
                case FishingV3FishState.Run:
                    amplitude = _tuning.RunOscillationAmplitudeNormalized;
                    frequency = _tuning.RunOscillationFrequencyHz;
                    break;
                case FishingV3FishState.Fight:
                    amplitude = _tuning.FightOscillationAmplitudeNormalized;
                    frequency = _tuning.FightOscillationFrequencyHz;
                    break;
                default:
                    amplitude = _tuning.CalmOscillationAmplitudeNormalized;
                    frequency = _tuning.CalmOscillationFrequencyHz;
                    break;
            }

            OscillationOffsetNormalized = amplitude * (float)Math.Sin(
                Math.PI * 2d * frequency * _phaseElapsedSeconds +
                _oscillationPhaseRadians);
            PullBurstOffsetNormalized = 0f;
            if (_pullBurstStartSeconds >= 0f &&
                _phaseElapsedSeconds >= _pullBurstStartSeconds &&
                _phaseElapsedSeconds <=
                _pullBurstStartSeconds + _pullBurstDurationSeconds)
            {
                float progress = (_phaseElapsedSeconds - _pullBurstStartSeconds) /
                    _pullBurstDurationSeconds;
                PullBurstOffsetNormalized = _pullBurstAmplitudeNormalized *
                    (float)Math.Sin(Math.PI * FishingMath.Clamp01(progress));
            }

            TensionOffsetNormalized =
                OscillationOffsetNormalized + PullBurstOffsetNormalized;
        }

        private FishingV3FishState SelectNextState()
        {
            switch (State)
            {
                case FishingV3FishState.Calm:
                    return FishingV3FishState.Fight;
                case FishingV3FishState.Run:
                    return FishingV3FishState.Fight;
                default:
                    FishingV3FishState next = _nextFightTransitionsToRun
                        ? FishingV3FishState.Run
                        : FishingV3FishState.Calm;
                    _nextFightTransitionsToRun = !_nextFightTransitionsToRun;
                    return next;
            }
        }

        private void AdvanceHeadShake(float deltaTime)
        {
            if (_headShakeDelaySeconds < 0f || deltaTime <= 0f) return;

            _headShakeDelaySeconds -= deltaTime;
            if (_headShakeDelaySeconds > 0f) return;

            _headShakeDelaySeconds = -1f;
            LastHeadShakeIntensityNormalized = RandomRange(
                _tuning.HeadShakeIntensityMinNormalized,
                _tuning.HeadShakeIntensityMaxNormalized);
            HeadShakeEventSequence = HeadShakeEventSequence == int.MaxValue
                ? 1
                : HeadShakeEventSequence + 1;
        }

        private float RandomDurationFor(FishingV3FishState state)
        {
            switch (state)
            {
                case FishingV3FishState.Run:
                    return RandomRange(
                        _tuning.RunDurationMinSeconds,
                        _tuning.RunDurationMaxSeconds);
                case FishingV3FishState.Fight:
                    return RandomRange(
                        _tuning.FightDurationMinSeconds,
                        _tuning.FightDurationMaxSeconds);
                default:
                    return RandomRange(
                        _tuning.CalmDurationMinSeconds,
                        _tuning.CalmDurationMaxSeconds);
            }
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (maximum <= minimum) return minimum;
            return minimum + (float)_random.NextDouble() * (maximum - minimum);
        }

        private static FishingV3FishState NormalizeState(FishingV3FishState state)
        {
            return state == FishingV3FishState.Fight || state == FishingV3FishState.Run
                ? state
                : FishingV3FishState.Calm;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
