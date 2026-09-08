using System;

namespace FishingMiniGame.Core
{
    public enum FishingV2StaminaBand
    {
        High,
        Mid,
        Low
    }

    public struct FishingV2FishAIWeights
    {
        public float Fight;
        public float Run;
        public float Rest;

        public FishingV2FishAIWeights(float fight, float run, float rest)
        {
            Fight = FishingMath.Max(0f, fight);
            Run = FishingMath.Max(0f, run);
            Rest = FishingMath.Max(0f, rest);
        }
    }

    public sealed class FishingV2FishAITuning
    {
        public float HighStaminaThreshold = 0.66f;
        public float LowStaminaThreshold = 0.33f;

        public float HighFightWeight = 0.40f;
        public float HighRunWeight = 0.50f;
        public float HighRestWeight = 0.10f;
        public float MidFightWeight = 0.34f;
        public float MidRunWeight = 0.33f;
        public float MidRestWeight = 0.33f;
        public float LowFightWeight = 0.35f;
        public float LowRunWeight = 0.15f;
        public float LowRestWeight = 0.50f;

        public float FightMinDuration = 1.0f;
        public float FightMaxDuration = 2.0f;
        public float RunMinDuration = 0.8f;
        public float RunMaxDuration = 1.6f;
        public float RestMinDuration = 0.8f;
        public float RestMaxDuration = 1.5f;
        public float HighRunDurationMultiplier = 1.10f;
        public float LowRunDurationMultiplier = 0.85f;
        public float HighRestDurationMultiplier = 0.90f;
        public float LowRestDurationMultiplier = 1.15f;

        public float FightForceHigh = 0.65f;
        public float FightForceMid = 0.52f;
        public float FightForceLow = 0.40f;
        public float RunForceHigh = 0.92f;
        public float RunForceMid = 0.78f;
        public float RunForceLow = 0.62f;
        public float RestForce = 0.18f;
        public float PullStrengthForceRange = 0.08f;

        public float RunTelegraphMinDuration = 0.25f;
        public float RunTelegraphMaxDuration = 0.45f;

        public float HeadShakeChance = 0.34f;
        public float HeadShakeCooldown = 2.2f;
        public float HeadShakeDuration = 0.38f;
        public float HeadShakeIntensity = 0.70f;

        public float FinalRunChance = 0.35f;
        public float FinalRunStaminaThreshold = 0.18f;
        public float FinalRunDistanceThreshold = 3.5f;
        public float FinalRunForce = 0.82f;
        public float PostFinalRunRestBias = 0.85f;

        public FishingV2FishAITuning Copy()
        {
            return (FishingV2FishAITuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            HighStaminaThreshold = FishingMath.Clamp01(HighStaminaThreshold);
            LowStaminaThreshold = FishingMath.Clamp(LowStaminaThreshold, 0f, HighStaminaThreshold);

            HighFightWeight = FishingMath.Max(0f, HighFightWeight);
            HighRunWeight = FishingMath.Max(0f, HighRunWeight);
            HighRestWeight = FishingMath.Max(0f, HighRestWeight);
            MidFightWeight = FishingMath.Max(0f, MidFightWeight);
            MidRunWeight = FishingMath.Max(0f, MidRunWeight);
            MidRestWeight = FishingMath.Max(0f, MidRestWeight);
            LowFightWeight = FishingMath.Max(0f, LowFightWeight);
            LowRunWeight = FishingMath.Max(0f, LowRunWeight);
            LowRestWeight = FishingMath.Max(0f, LowRestWeight);

            FightMinDuration = FishingMath.Max(0.05f, FightMinDuration);
            FightMaxDuration = FishingMath.Max(FightMinDuration, FightMaxDuration);
            RunMinDuration = FishingMath.Max(0.05f, RunMinDuration);
            RunMaxDuration = FishingMath.Max(RunMinDuration, RunMaxDuration);
            RestMinDuration = FishingMath.Max(0.05f, RestMinDuration);
            RestMaxDuration = FishingMath.Max(RestMinDuration, RestMaxDuration);
            HighRunDurationMultiplier = FishingMath.Max(0.1f, HighRunDurationMultiplier);
            LowRunDurationMultiplier = FishingMath.Max(0.1f, LowRunDurationMultiplier);
            HighRestDurationMultiplier = FishingMath.Max(0.1f, HighRestDurationMultiplier);
            LowRestDurationMultiplier = FishingMath.Max(0.1f, LowRestDurationMultiplier);

            FightForceHigh = FishingMath.Clamp01(FightForceHigh);
            FightForceMid = FishingMath.Clamp01(FightForceMid);
            FightForceLow = FishingMath.Clamp01(FightForceLow);
            RunForceHigh = FishingMath.Clamp01(RunForceHigh);
            RunForceMid = FishingMath.Clamp01(RunForceMid);
            RunForceLow = FishingMath.Clamp01(RunForceLow);
            RestForce = FishingMath.Clamp01(RestForce);
            PullStrengthForceRange = FishingMath.Max(0f, PullStrengthForceRange);

            RunTelegraphMinDuration = FishingMath.Max(0.01f, RunTelegraphMinDuration);
            RunTelegraphMaxDuration = FishingMath.Max(RunTelegraphMinDuration, RunTelegraphMaxDuration);
            HeadShakeChance = FishingMath.Clamp01(HeadShakeChance);
            HeadShakeCooldown = FishingMath.Max(0f, HeadShakeCooldown);
            HeadShakeDuration = FishingMath.Max(0.01f, HeadShakeDuration);
            HeadShakeIntensity = FishingMath.Clamp01(HeadShakeIntensity);

            // A Final Run must remain a genuine probability, never a hidden 0%/100% switch.
            FinalRunChance = FishingMath.Clamp(FinalRunChance, 0.01f, 0.99f);
            FinalRunStaminaThreshold = FishingMath.Clamp01(FinalRunStaminaThreshold);
            FinalRunDistanceThreshold = FishingMath.Max(0f, FinalRunDistanceThreshold);
            FinalRunForce = FishingMath.Clamp01(FinalRunForce);
            PostFinalRunRestBias = FishingMath.Max(0f, PostFinalRunRestBias);
        }
    }

    public struct FishingV2FishAIOutput
    {
        public FishingV2BehaviorSample Behavior;
        public FishingV2StaminaBand StaminaBand;
        public float PhaseRemainingSeconds;
        public bool IsRunTelegraphing;
        public float RunTelegraphDirectionNormalized;
        public float RunTelegraphRemainingSeconds;
        public bool HeadShakeActive;
        public float HeadShakeIntensityNormalized;
        public int HeadShakeEventSequence;
        public bool FinalRunDecisionMade;
        public bool FinalRunPending;
        public bool IsFinalRun;
        public bool FinalRunUsed;
    }

    /// <summary>
    /// Pure, deterministic producer of V2 fish behavior. It owns behavioral
    /// decisions and signals only; fight metrics and fishing outcomes remain in
    /// FishingV2FightModel and FishingStateMachine.
    /// </summary>
    public sealed class FishingV2FishAI
    {
        private FishingV2FishAITuning _tuning;
        private Random _random;
        private float _pullStrength;
        private FishingV2BehaviorState _currentState;
        private FishingV2BehaviorState _previousState;
        private float _phaseRemaining;
        private float _currentForce;
        private float _currentDirection;
        private FishingV2StaminaBand _staminaBand;

        private bool _isRunTelegraphing;
        private float _runTelegraphRemaining;
        private float _runTelegraphDirection;
        private float _pendingRunDuration;
        private float _pendingRunForce;
        private bool _pendingRunIsFinal;

        private bool _headShakeActive;
        private float _headShakeRemaining;
        private float _headShakeIntensity;
        private float _headShakeCooldownRemaining;
        private float _scheduledHeadShakeDelay;
        private int _headShakeEventSequence;

        private bool _finalRunDecisionMade;
        private bool _finalRunPending;
        private bool _isFinalRun;
        private bool _finalRunUsed;

        public FishingV2FishAIOutput Current { get; private set; }

        public FishingV2FishAI(FishingV2FishAITuning tuning = null, int seed = 20260821,
            float pullStrengthNormalized = 0.5f)
        {
            Reset(tuning, seed, pullStrengthNormalized);
        }

        public void Reset(FishingV2FishAITuning tuning, int seed, float pullStrengthNormalized = 0.5f)
        {
            _tuning = (tuning ?? new FishingV2FishAITuning()).Copy();
            _tuning.Sanitize();
            _random = new Random(seed);
            _pullStrength = FishingMath.Clamp01(pullStrengthNormalized);
            _currentState = FishingV2BehaviorState.None;
            _previousState = FishingV2BehaviorState.None;
            _staminaBand = FishingV2StaminaBand.High;
            _isRunTelegraphing = false;
            _runTelegraphRemaining = 0f;
            _runTelegraphDirection = 0f;
            _pendingRunDuration = 0f;
            _pendingRunForce = 0f;
            _pendingRunIsFinal = false;
            _headShakeActive = false;
            _headShakeRemaining = 0f;
            _headShakeIntensity = 0f;
            _headShakeCooldownRemaining = 0f;
            _scheduledHeadShakeDelay = -1f;
            _headShakeEventSequence = 0;
            _finalRunDecisionMade = false;
            _finalRunPending = false;
            _isFinalRun = false;
            _finalRunUsed = false;

            // Every hooked fish starts in a formal Fight phase.
            BeginPhase(FishingV2BehaviorState.Fight, FishingV2StaminaBand.High, false);
            PublishOutput();
        }

        public FishingV2FishAIOutput Tick(
            float deltaTime,
            float fishStaminaNormalized,
            float fishDistanceMeters)
        {
            float dt = FishingMath.Clamp(deltaTime, 0f, 0.25f);
            _staminaBand = ClassifyStamina(fishStaminaNormalized);
            if (dt <= 0f)
            {
                PublishOutput();
                return Current;
            }

            _headShakeCooldownRemaining = FishingMath.Max(0f, _headShakeCooldownRemaining - dt);
            UpdateHeadShake(dt);

            if (_isRunTelegraphing)
            {
                _runTelegraphRemaining -= dt;
                if (_runTelegraphRemaining <= 0f) StartTelegraphedRun();
                PublishOutput();
                return Current;
            }

            if (_scheduledHeadShakeDelay >= 0f && !_headShakeActive &&
                _currentState == FishingV2BehaviorState.Fight)
            {
                _scheduledHeadShakeDelay -= dt;
                if (_scheduledHeadShakeDelay <= 0f && _headShakeCooldownRemaining <= 0f)
                {
                    StartHeadShake();
                }
            }

            _phaseRemaining -= dt;
            if (_phaseRemaining <= 0f && !_headShakeActive)
            {
                FishingV2BehaviorState completedState = _currentState;
                bool completedFinalRun = _isFinalRun;
                _isFinalRun = false;

                // Final Run is a next-action decision, never a mid-phase interrupt.
                // A completed normal Run must first transition through Fight or Rest.
                if (completedState == FishingV2BehaviorState.Fight ||
                    completedState == FishingV2BehaviorState.Rest)
                {
                    EvaluateFinalRun(fishStaminaNormalized, fishDistanceMeters);
                }

                if (_finalRunPending)
                {
                    StartRunTelegraph(true, _staminaBand);
                }
                else
                {
                    FishingV2BehaviorState next = SelectNextState(_staminaBand, completedFinalRun);
                    if (next == FishingV2BehaviorState.Run)
                    {
                        StartRunTelegraph(false, _staminaBand);
                    }
                    else
                    {
                        BeginPhase(next, _staminaBand, false);
                    }
                }
            }

            PublishOutput();
            return Current;
        }

        public FishingV2StaminaBand ClassifyStamina(float staminaNormalized)
        {
            float stamina = FishingMath.Clamp01(staminaNormalized);
            if (stamina > _tuning.HighStaminaThreshold) return FishingV2StaminaBand.High;
            if (stamina > _tuning.LowStaminaThreshold) return FishingV2StaminaBand.Mid;
            return FishingV2StaminaBand.Low;
        }

        public FishingV2FishAIWeights GetTransitionWeights(
            FishingV2StaminaBand band,
            FishingV2BehaviorState current,
            FishingV2BehaviorState previous,
            bool afterFinalRun)
        {
            FishingV2FishAIWeights weights;
            switch (band)
            {
                case FishingV2StaminaBand.High:
                    weights = new FishingV2FishAIWeights(
                        _tuning.HighFightWeight, _tuning.HighRunWeight, _tuning.HighRestWeight);
                    break;
                case FishingV2StaminaBand.Low:
                    weights = new FishingV2FishAIWeights(
                        _tuning.LowFightWeight, _tuning.LowRunWeight, _tuning.LowRestWeight);
                    break;
                default:
                    weights = new FishingV2FishAIWeights(
                        _tuning.MidFightWeight, _tuning.MidRunWeight, _tuning.MidRestWeight);
                    break;
            }

            if (current == FishingV2BehaviorState.Run) weights.Run = 0f;
            if (current == FishingV2BehaviorState.Rest) weights.Rest = 0f;
            if (current == FishingV2BehaviorState.Fight && previous == FishingV2BehaviorState.Fight)
                weights.Fight *= 0.65f;
            if (afterFinalRun)
            {
                weights.Run = 0f;
                weights.Rest += _tuning.PostFinalRunRestBias;
            }
            return weights;
        }

        private void EvaluateFinalRun(float staminaNormalized, float distanceMeters)
        {
            if (_finalRunDecisionMade || _finalRunUsed) return;
            if (FishingMath.Clamp01(staminaNormalized) > _tuning.FinalRunStaminaThreshold) return;
            if (FishingMath.Max(0f, distanceMeters) > _tuning.FinalRunDistanceThreshold) return;

            _finalRunDecisionMade = true;
            _finalRunPending = _random.NextDouble() < _tuning.FinalRunChance;
            if (!_finalRunPending) return;

            // Eligibility is evaluated only at a completed Fight/Rest boundary, so
            // a successful decision can move directly into the mandatory telegraph.
            _scheduledHeadShakeDelay = -1f;
        }

        private void UpdateHeadShake(float dt)
        {
            if (!_headShakeActive) return;
            _headShakeRemaining -= dt;
            if (_headShakeRemaining > 0f) return;
            _headShakeActive = false;
            _headShakeRemaining = 0f;
            _headShakeIntensity = 0f;
        }

        private void StartHeadShake()
        {
            _headShakeActive = true;
            _headShakeRemaining = _tuning.HeadShakeDuration;
            _headShakeIntensity = FishingMath.Clamp01(
                _tuning.HeadShakeIntensity * (0.75f + (float)_random.NextDouble() * 0.25f));
            _headShakeEventSequence++;
            _headShakeCooldownRemaining = _tuning.HeadShakeDuration + _tuning.HeadShakeCooldown;
            _scheduledHeadShakeDelay = -1f;
        }

        private void StartRunTelegraph(bool isFinal, FishingV2StaminaBand band)
        {
            _isRunTelegraphing = true;
            _runTelegraphRemaining = RandomRange(
                _tuning.RunTelegraphMinDuration, _tuning.RunTelegraphMaxDuration);
            _runTelegraphDirection = _random.NextDouble() < 0.5d ? -1f : 1f;
            _pendingRunDuration = SelectDuration(FishingV2BehaviorState.Run, band);
            _pendingRunForce = isFinal
                ? ApplyPullStrength(_tuning.FinalRunForce)
                : SelectForce(FishingV2BehaviorState.Run, band);
            _pendingRunIsFinal = isFinal;
            _scheduledHeadShakeDelay = -1f;
            _headShakeActive = false;
            _headShakeRemaining = 0f;
            _headShakeIntensity = 0f;

            // Telegraph behavior must remain Fight/Rest, never Run.
            if (_currentState == FishingV2BehaviorState.Run)
            {
                _previousState = _currentState;
                _currentState = FishingV2BehaviorState.Rest;
                _currentForce = SelectForce(FishingV2BehaviorState.Rest, band);
                _currentDirection = 0f;
            }
            _phaseRemaining = 0f;
        }

        private void StartTelegraphedRun()
        {
            _isRunTelegraphing = false;
            _runTelegraphRemaining = 0f;
            _previousState = _currentState;
            _currentState = FishingV2BehaviorState.Run;
            _phaseRemaining = _pendingRunDuration;
            _currentForce = _pendingRunForce;
            _currentDirection = _runTelegraphDirection;
            _isFinalRun = _pendingRunIsFinal;
            if (_pendingRunIsFinal)
            {
                _finalRunPending = false;
                _finalRunUsed = true;
            }
            _pendingRunIsFinal = false;
        }

        private void BeginPhase(
            FishingV2BehaviorState state,
            FishingV2StaminaBand band,
            bool isFinalRun)
        {
            _previousState = _currentState;
            _currentState = state;
            _phaseRemaining = SelectDuration(state, band);
            _currentForce = SelectForce(state, band);
            _currentDirection = state == FishingV2BehaviorState.Run
                ? (_random.NextDouble() < 0.5d ? -1f : 1f)
                : 0f;
            _isFinalRun = isFinalRun;
            _scheduledHeadShakeDelay = -1f;
            if (state == FishingV2BehaviorState.Fight) ScheduleHeadShake();
        }

        private void ScheduleHeadShake()
        {
            if (_tuning.HeadShakeChance <= 0f || _random.NextDouble() >= _tuning.HeadShakeChance) return;
            float earliest = FishingMath.Max(0.10f, _headShakeCooldownRemaining);
            float latest = _phaseRemaining - _tuning.HeadShakeDuration;
            if (latest <= earliest) return;
            _scheduledHeadShakeDelay = RandomRange(earliest, latest);
        }

        private FishingV2BehaviorState SelectNextState(FishingV2StaminaBand band, bool afterFinalRun)
        {
            FishingV2FishAIWeights weights = GetTransitionWeights(
                band, _currentState, _previousState, afterFinalRun);
            float total = weights.Fight + weights.Run + weights.Rest;
            if (total <= 0f) return FishingV2BehaviorState.Fight;
            float roll = (float)_random.NextDouble() * total;
            if (roll < weights.Fight) return FishingV2BehaviorState.Fight;
            roll -= weights.Fight;
            return roll < weights.Run ? FishingV2BehaviorState.Run : FishingV2BehaviorState.Rest;
        }

        private float SelectDuration(FishingV2BehaviorState state, FishingV2StaminaBand band)
        {
            float duration;
            if (state == FishingV2BehaviorState.Run)
            {
                duration = RandomRange(_tuning.RunMinDuration, _tuning.RunMaxDuration);
                if (band == FishingV2StaminaBand.High) duration *= _tuning.HighRunDurationMultiplier;
                else if (band == FishingV2StaminaBand.Low) duration *= _tuning.LowRunDurationMultiplier;
                return FishingMath.Clamp(duration, _tuning.RunMinDuration, _tuning.RunMaxDuration);
            }
            if (state == FishingV2BehaviorState.Rest)
            {
                duration = RandomRange(_tuning.RestMinDuration, _tuning.RestMaxDuration);
                if (band == FishingV2StaminaBand.High) duration *= _tuning.HighRestDurationMultiplier;
                else if (band == FishingV2StaminaBand.Low) duration *= _tuning.LowRestDurationMultiplier;
                return FishingMath.Clamp(duration, _tuning.RestMinDuration, _tuning.RestMaxDuration);
            }
            return RandomRange(_tuning.FightMinDuration, _tuning.FightMaxDuration);
        }

        private float SelectForce(FishingV2BehaviorState state, FishingV2StaminaBand band)
        {
            float value;
            if (state == FishingV2BehaviorState.Run)
            {
                value = band == FishingV2StaminaBand.High ? _tuning.RunForceHigh :
                    band == FishingV2StaminaBand.Low ? _tuning.RunForceLow : _tuning.RunForceMid;
            }
            else if (state == FishingV2BehaviorState.Rest)
            {
                value = _tuning.RestForce;
            }
            else
            {
                value = band == FishingV2StaminaBand.High ? _tuning.FightForceHigh :
                    band == FishingV2StaminaBand.Low ? _tuning.FightForceLow : _tuning.FightForceMid;
            }
            if (state == FishingV2BehaviorState.Rest) return value;
            float adjusted = ApplyPullStrength(value);
            return state == FishingV2BehaviorState.Fight
                ? FishingMath.Clamp(adjusted, 0.40f, 0.65f)
                : adjusted;
        }

        private float ApplyPullStrength(float value)
        {
            return FishingMath.Clamp01(value + (_pullStrength - 0.5f) * _tuning.PullStrengthForceRange);
        }

        private float RandomRange(float min, float max)
        {
            if (max <= min) return min;
            return min + (float)_random.NextDouble() * (max - min);
        }

        private void PublishOutput()
        {
            Current = new FishingV2FishAIOutput
            {
                Behavior = new FishingV2BehaviorSample(_currentState, _currentForce, _currentDirection),
                StaminaBand = _staminaBand,
                PhaseRemainingSeconds = FishingMath.Max(0f, _phaseRemaining),
                IsRunTelegraphing = _isRunTelegraphing,
                RunTelegraphDirectionNormalized = _isRunTelegraphing ? _runTelegraphDirection : 0f,
                RunTelegraphRemainingSeconds = FishingMath.Max(0f, _runTelegraphRemaining),
                HeadShakeActive = _headShakeActive,
                HeadShakeIntensityNormalized = _headShakeActive ? _headShakeIntensity : 0f,
                HeadShakeEventSequence = _headShakeEventSequence,
                FinalRunDecisionMade = _finalRunDecisionMade,
                FinalRunPending = _finalRunPending,
                IsFinalRun = _isFinalRun,
                FinalRunUsed = _finalRunUsed
            };
        }
    }
}
