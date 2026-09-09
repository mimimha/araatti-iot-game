using System;

namespace FishingMiniGame.Core
{
    public sealed class FishingStateMachine
    {
        private FishingRules _baseRules;
        private FishingRules _rules;
        private FishProfile _fish;
        private Random _random;
        private float _stateElapsed;
        private float _biteDelay;
        private float _fightElapsed;
        private float _slackElapsed;
        private float _highTensionElapsed;
        private float _nibbleAt;
        private float _nibbleUntil;
        private float _earlyStrikeHintUntil;
        private int _falseStrikeCount;
        private bool _useV2PreFightFlow;
        private FishingV2PreFightTuning _v2PreFightTuning;
        private float _v2ScheduledNibbleStartTime;
        private float _v2ScheduledBiteTime;
        private float _v2EarliestBiteAllowedTime;
        private float _v2NibbleRemaining;
        private float _v2ActualNibbleStartTime;
        private float _v2ActualNibbleEndTime;
        private bool _v2NibbleStarted;
        private FishingFeedbackState _fightPhase;
        private float _fightPhaseRemaining;
        private float _fightPhaseDuration;
        private float _fightDirection;
        private float _phaseIntensity;
        private float _targetTensionShift;
        private float _smoothedTensionShift;
        private int _cycleNumber;
        private bool _useV2FightModel;
        private int _v2AISeed;
        private FishingV2FightTuning _v2FightTuning;
        private FishingV2FightModel _v2FightModel;
        private FishingV2FishAITuning _v2FishAITuning;
        private FishingV2FishAI _v2FishAI;
        private FishingV2FishAIOutput _v2FishAIOutput;

        public FishingSnapshot Current { get; private set; }
        public FishingCycleResult LastResult { get; private set; }
        public event Action<FishingCycleResult> CycleFinished;

        public FishingStateMachine()
        {
            Current = new FishingSnapshot();
        }

        public void Initialize(FishingRoundContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            _baseRules = (context.Rules ?? new FishingRules()).Copy();
            _fish = (context.Fish ?? new FishProfile()).Copy();
            _useV2PreFightFlow = context.UseV2PreFightFlow;
            _v2PreFightTuning = (context.V2PreFightTuning ?? new FishingV2PreFightTuning()).Copy();
            _v2PreFightTuning.Sanitize();
            _useV2FightModel = context.UseV2FightModel;
            _v2AISeed = context.Seed;
            _v2FightTuning = (context.V2FightTuning ?? new FishingV2FightTuning()).Copy();
            _v2FightTuning.Sanitize();
            _v2FightModel = new FishingV2FightModel(_v2FightTuning);
            _v2FishAITuning = (context.V2FishAITuning ?? new FishingV2FishAITuning()).Copy();
            _v2FishAITuning.Sanitize();
            _v2FishAI = new FishingV2FishAI(_v2FishAITuning, _v2AISeed, _fish.PullStrength);
            _v2FishAIOutput = _v2FishAI.Current;
            _baseRules.Sanitize();
            _fish.Sanitize();
            ApplyFishRules();
            _random = new Random(context.Seed);
            _cycleNumber = 0;
            Current = new FishingSnapshot
            {
                State = FishingPlayerState.Idle,
                FishId = _fish.FishId,
                FishDisplayName = _fish.DisplayName,
                DifficultyLabel = _fish.DifficultyLabel,
                FishMaxHealth = _fish.MaxStamina,
                FishHealth = _fish.MaxStamina,
                LineDurability = 100f,
                Hint = "Hold SPACE to charge a cast",
                Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f)
            };
            LastResult = null;
            ResetTransientValues();
        }

        public void PrepareCycle(FishProfile fish, FishingRules rules = null)
        {
            EnsureInitialized();
            if (Current.State != FishingPlayerState.Idle)
            {
                throw new InvalidOperationException("A new fish can only be prepared while the player is Idle.");
            }

            if (rules != null)
            {
                _baseRules = rules.Copy();
                _baseRules.Sanitize();
            }
            _fish = (fish ?? new FishProfile()).Copy();
            _fish.Sanitize();
            ApplyFishRules();
            ResetTransientValues();
        }

        public void Tick(FishingInputFrame input, float deltaTime)
        {
            EnsureInitialized();
            float dt = FishingMath.Clamp(deltaTime, 0f, 0.25f);
            Current.RawTensionNormalized = SanitizeNormalized(input.TensionNormalized);
            Current.TensionNormalized = Current.RawTensionNormalized;
            _stateElapsed += dt;
            Current.StateElapsedSeconds = _stateElapsed;

            switch (Current.State)
            {
                case FishingPlayerState.Idle:
                    TickIdle(input);
                    break;
                case FishingPlayerState.Casting:
                    TickCasting(input);
                    break;
                case FishingPlayerState.Waiting:
                    TickWaiting(input, dt);
                    break;
                case FishingPlayerState.BiteWindow:
                    TickBiteWindow(input);
                    break;
                case FishingPlayerState.Hooked:
                    TickHooked(input);
                    break;
                case FishingPlayerState.Fighting:
                    TickFighting(input, dt);
                    break;
                case FishingPlayerState.Caught:
                case FishingPlayerState.Escaped:
                    TickOutcome();
                    break;
                case FishingPlayerState.Cooldown:
                    TickCooldown();
                    break;
            }
        }

        public void ResetCycle()
        {
            EnsureInitialized();
            ResetTransientValues();
            TransitionTo(FishingPlayerState.Idle);
        }

        private void TickIdle(FishingInputFrame input)
        {
            Current.Hint = "Hold SPACE to charge a cast";
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f);
            if (input.CastPressed)
            {
                Current.CastPower = 0f;
                Current.LastOutcome = string.Empty;
                Current.EscapeReason = FishingEscapeReason.None;
                TransitionTo(FishingPlayerState.Casting);
            }
        }

        private void TickCasting(FishingInputFrame input)
        {
            Current.CastPower = FishingMath.Clamp01(_stateElapsed / _rules.MaxCastChargeSeconds);
            Current.Hint = "Release SPACE to cast";
            if (input.CastReleased || Current.CastPower >= 1f)
            {
                if (_useV2PreFightFlow)
                    BeginV2WaitingAttempt(FishingV2WaitingAttemptReason.Initial);
                else
                    BeginLegacyWaiting();
            }
        }

        private void TickWaiting(FishingInputFrame input, float dt)
        {
            if (_useV2PreFightFlow)
                TickV2Waiting(input, dt);
            else
                TickLegacyWaiting(input);
        }

        private void TickLegacyWaiting(FishingInputFrame input)
        {
            Current.BiteDelayRemainingSeconds = FishingMath.Max(0f, _biteDelay - _stateElapsed);
            Current.IsNibbling = _nibbleAt >= 0f && _stateElapsed >= _nibbleAt && _stateElapsed <= _nibbleUntil;
            if (Current.IsNibbling)
            {
                Current.Hint = "A light nibble... wait for the committed pull";
                Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.Bite, 0.28f);
            }
            else if (_stateElapsed < _earlyStrikeHintUntil)
            {
                Current.Hint = "Too early - the fish backed away";
                Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f);
            }
            else
            {
                Current.Hint = "Wait for the committed bite...";
                Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f);
            }

            if (input.HookPressed)
            {
                _falseStrikeCount++;
                Current.FalseStrikeCount = _falseStrikeCount;
                Current.IsNibbling = false;
                if (_falseStrikeCount >= 2)
                {
                    Escape(FishingEscapeReason.MissedBite, "Too many false strikes scared the fish away");
                    return;
                }

                float retreat = 0.45f + (float)_random.NextDouble() * 0.45f;
                _biteDelay += retreat;
                _earlyStrikeHintUntil = _stateElapsed + 0.75f;
                ScheduleNibble(_stateElapsed + 0.30f);
            }

            if (_stateElapsed >= _biteDelay)
            {
                TransitionTo(FishingPlayerState.BiteWindow);
            }
        }

        private void TickV2Waiting(FishingInputFrame input, float dt)
        {
            UpdateV2BiteDelayRemaining();

            if (!_v2NibbleStarted && _stateElapsed >= _v2ScheduledNibbleStartTime)
            {
                StartV2Nibble();
                return;
            }

            if (Current.IsNibbling)
            {
                if (input.HookPressed)
                {
                    HandleV2EarlyHook();
                    return;
                }

                _v2NibbleRemaining = FishingMath.Max(0f, _v2NibbleRemaining - dt);
                Current.NibbleRemainingSeconds = _v2NibbleRemaining;
                Current.Hint = "NIBBLE... wait";
                Current.Feedback = new FishingFeedbackFrame(
                    FishingFeedbackState.Bite, Current.NibbleIntensityNormalized);

                if (_v2NibbleRemaining > 0f)
                {
                    return;
                }

                // The active window ends when consumers can actually observe it ending.
                // Starting the gap here prevents a coarse Tick from consuming the active
                // duration and the post-nibble gap in the same update.
                Current.IsNibbling = false;
                Current.NibbleRemainingSeconds = 0f;
                Current.NibbleIntensityNormalized = 0f;
                _v2ActualNibbleEndTime = _stateElapsed;
                _v2EarliestBiteAllowedTime = FishingMath.Max(
                    _v2ScheduledBiteTime,
                    _v2ActualNibbleEndTime + _v2PreFightTuning.NibbleToBiteGapSeconds);
                Current.Hint = "Wait for a bite...";
                Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f);
                UpdateV2BiteDelayRemaining();
                return;
            }

            if (_v2NibbleStarted && _stateElapsed >= _v2EarliestBiteAllowedTime)
            {
                BeginV2BiteWindow();
                return;
            }

            Current.Hint = "Wait for a bite...";
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f);
            if (input.HookPressed) HandleV2EarlyHook();
        }

        private void TickBiteWindow(FishingInputFrame input)
        {
            Current.HookWindowRemainingSeconds = FishingMath.Max(0f, _rules.HookWindowSeconds - _stateElapsed);
            Current.Hint = "BITE! Press F now";
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.Bite, 1f);
            Current.IsNibbling = false;

            if (input.HookPressed)
            {
                if (_useV2PreFightFlow) ClearV2AttemptSchedule();
                TransitionTo(FishingPlayerState.Hooked);
            }
            else if (_stateElapsed >= _rules.HookWindowSeconds)
            {
                if (_useV2PreFightFlow)
                {
                    Current.MissedBiteRetryCount++;
                    BeginV2WaitingAttempt(FishingV2WaitingAttemptReason.MissedBite);
                }
                else
                {
                    Escape(FishingEscapeReason.MissedBite, "The fish stole the bait");
                }
            }
        }

        private void TickHooked(FishingInputFrame input)
        {
            Current.Hint = "Hook set! Get ready to reel";
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.Fight, _fish.PullStrength);
            if (!input.IsDeviceConnected)
            {
                Escape(FishingEscapeReason.InputDisconnected, "Input disconnected");
            }
            else if (_stateElapsed >= _rules.HookSettleSeconds)
            {
                _fightElapsed = 0f;
                if (_useV2FightModel)
                {
                    _v2FightModel.Reset(_v2FightTuning);
                    _v2FishAI.Reset(_v2FishAITuning, _v2AISeed + _cycleNumber, _fish.PullStrength);
                    _v2FishAIOutput = _v2FishAI.Current;
                    SyncV2Snapshot(_v2FishAIOutput);
                }
                else
                {
                    BeginNextFightPhase(true);
                }
                TransitionTo(FishingPlayerState.Fighting);
            }
        }

        private void TickFighting(FishingInputFrame input, float dt)
        {
            if (!input.IsDeviceConnected)
            {
                Escape(FishingEscapeReason.InputDisconnected, "Input disconnected");
                return;
            }

            _fightElapsed += dt;
            Current.FightElapsedSeconds = _fightElapsed;
            Current.FightRemainingSeconds = _useV2FightModel
                ? 0f
                : FishingMath.Max(0f, _rules.FightTimeoutSeconds - _fightElapsed);

            if (_useV2FightModel)
            {
                TickV2Fighting(input, dt);
                return;
            }

            UpdateFightBehavior(dt);
            TickLegacyFighting(input, dt);
        }

        private void TickLegacyFighting(FishingInputFrame input, float dt)
        {

            float directionalControl = 0.5f;
            if (_fightPhase == FishingFeedbackState.Run)
            {
                float requiredCounter = -_fightDirection;
                directionalControl = FishingMath.Clamp01(1f - Math.Abs(input.RodYaw - requiredCounter) * 0.5f);
            }
            Current.DirectionalControlNormalized = directionalControl;

            float wave = _fightPhase == FishingFeedbackState.Fight
                ? (float)Math.Sin(_fightElapsed * (4.2f + _fish.PullStrength * 2.5f)) * _fish.MaxTensionShift * 0.22f
                : 0f;
            float directionMultiplier = _fightPhase == FishingFeedbackState.Run
                ? 1.28f - directionalControl * 0.62f
                : 1f;
            float desiredShift = _targetTensionShift * directionMultiplier + wave;
            float shiftStep = dt * (0.28f + _fish.PullStrength * 0.55f);
            if (_smoothedTensionShift < desiredShift)
            {
                _smoothedTensionShift = FishingMath.Min(desiredShift, _smoothedTensionShift + shiftStep);
            }
            else
            {
                _smoothedTensionShift = FishingMath.Max(desiredShift, _smoothedTensionShift - shiftStep);
            }
            Current.FishTensionShift = _smoothedTensionShift;
            Current.TensionNormalized = FishingMath.Clamp01(Current.RawTensionNormalized + _smoothedTensionShift);

            float tension = Current.TensionNormalized;
            if (tension < _rules.SafeTensionMin)
            {
                _slackElapsed += dt;
            }
            else
            {
                _slackElapsed = FishingMath.Max(0f, _slackElapsed - (dt * 1.5f));
            }

            if (tension > _rules.SafeTensionMax)
            {
                _highTensionElapsed += dt;
                if (_highTensionElapsed > _rules.HighTensionGraceSeconds)
                {
                    Current.LineDurability -= _rules.HighTensionDamagePerSecond * dt;
                }
            }
            else
            {
                _highTensionElapsed = FishingMath.Max(0f, _highTensionElapsed - (dt * 1.25f));
            }

            bool safeTension = tension >= _rules.SafeTensionMin && tension <= _rules.SafeTensionMax;
            float reel = FishingMath.Clamp01(input.ReelDelta);
            if (_fightPhase == FishingFeedbackState.Run && reel > 0f && directionalControl < 0.25f)
            {
                Current.LineDurability -= (5f + _fish.PullStrength * 9f) * reel * dt;
            }
            if (safeTension && reel > 0f)
            {
                float phaseEfficiency = Current.Feedback.State == FishingFeedbackState.Rest ? 1.45f :
                    Current.Feedback.State == FishingFeedbackState.Run ? 0.42f : 0.88f;
                Current.FishHealth -= _rules.FishDamagePerSecond * _fish.ReelEfficiencyMultiplier * reel * phaseEfficiency * dt;
            }

            Current.FishHealth = FishingMath.Clamp(Current.FishHealth, 0f, Current.FishMaxHealth);
            Current.LineDurability = FishingMath.Clamp(Current.LineDurability, 0f, 100f);
            Current.SlackDangerNormalized = FishingMath.Clamp01(_slackElapsed / _rules.SlackEscapeSeconds);
            Current.HighTensionDangerNormalized = FishingMath.Clamp01(_highTensionElapsed / _rules.HighTensionGraceSeconds);
            Current.Hint = BuildFightHint(tension, safeTension, reel);

            if (Current.FishHealth <= 0f)
            {
                CatchFish();
            }
            else if (Current.LineDurability <= 0f)
            {
                Escape(FishingEscapeReason.LineBroken, "The line snapped");
            }
            else if (_slackElapsed >= _rules.SlackEscapeSeconds)
            {
                Escape(FishingEscapeReason.SlackLine, "The fish escaped from a slack line");
            }
            else if (_fightElapsed >= _rules.FightTimeoutSeconds)
            {
                Escape(FishingEscapeReason.FightTimedOut, "The fish exhausted your attempt");
            }
        }

        private void TickV2Fighting(FishingInputFrame input, float dt)
        {
            _v2FishAIOutput = _v2FishAI.Tick(
                dt,
                _v2FightModel.FishStaminaNormalized,
                _v2FightModel.FishDistanceMeters);
            float eventTensionModifier = _v2FishAIOutput.HeadShakeActive
                ? _v2FishAIOutput.HeadShakeIntensityNormalized *
                  _v2FightTuning.HeadShakeMaxTensionModifier
                : 0f;
            _v2FightModel.Tick(
                dt,
                SanitizeNormalized(input.ReelDelta),
                SanitizeSigned(input.RodPitch),
                SanitizeSigned(input.RodYaw),
                _v2FishAIOutput.Behavior,
                eventTensionModifier);
            SyncV2Snapshot(_v2FishAIOutput);
            Current.Hint = BuildV2FightHint();

            if (_v2FightModel.FailureCondition == FishingV2FailureCondition.LineBroken)
            {
                Escape(FishingEscapeReason.LineBroken, "The line snapped under sustained pressure");
            }
            else if (_v2FightModel.FailureCondition == FishingV2FailureCondition.SlackLine)
            {
                Escape(FishingEscapeReason.SlackLine, "The hook came loose from sustained slack");
            }
            else if (_v2FishAIOutput.FinalRunPending)
            {
                // A successful final-run roll owns the next beat. The metric
                // model remains unchanged; only the state transition is gated.
            }
            else if (_v2FightModel.CanCatch)
            {
                CatchFish();
            }
        }

        private void SyncV2Snapshot(FishingV2FishAIOutput aiOutput)
        {
            FishingV2BehaviorSample behavior = aiOutput.Behavior;
            Current.FishDistanceMeters = _v2FightModel.FishDistanceMeters;
            Current.FishStaminaNormalized = _v2FightModel.FishStaminaNormalized;
            Current.VirtualLineTensionNormalized = _v2FightModel.VirtualLineTensionNormalized;
            Current.BreakStressNormalized = _v2FightModel.BreakStressNormalized;
            Current.HookLooseRiskNormalized = _v2FightModel.HookLooseRiskNormalized;
            Current.RodResponseQualityNormalized = _v2FightModel.RodResponseQualityNormalized;
            Current.ReelEfficiencyNormalized = _v2FightModel.ReelEfficiencyNormalized;
            Current.VirtualTensionZone = _v2FightModel.TensionZone;
            Current.V2BehaviorState = behavior.State;
            Current.V2FishForceNormalized = FishingMath.Clamp01(behavior.ForceNormalized);
            Current.V2FishDirectionNormalized = FishingMath.Clamp(behavior.DirectionNormalized, -1f, 1f);
            Current.AIStaminaBand = aiOutput.StaminaBand;
            Current.AIPhaseRemainingSeconds = aiOutput.PhaseRemainingSeconds;
            Current.IsRunTelegraphing = aiOutput.IsRunTelegraphing;
            Current.RunTelegraphDirectionNormalized = aiOutput.RunTelegraphDirectionNormalized;
            Current.RunTelegraphRemainingSeconds = aiOutput.RunTelegraphRemainingSeconds;
            Current.HeadShakeActive = aiOutput.HeadShakeActive;
            Current.HeadShakeIntensityNormalized = aiOutput.HeadShakeIntensityNormalized;
            Current.HeadShakeEventSequence = aiOutput.HeadShakeEventSequence;
            Current.FinalRunDecisionMade = aiOutput.FinalRunDecisionMade;
            Current.FinalRunPending = aiOutput.FinalRunPending;
            Current.IsFinalRun = aiOutput.IsFinalRun;
            Current.FinalRunUsed = aiOutput.FinalRunUsed;

            Current.FishHealth = Current.FishMaxHealth * Current.FishStaminaNormalized;
            Current.LineDurability = 100f * (1f - Current.BreakStressNormalized);
            Current.TensionNormalized = Current.VirtualLineTensionNormalized;
            Current.FishTensionShift = Current.VirtualLineTensionNormalized - 0.5f;
            Current.SlackDangerNormalized = Current.HookLooseRiskNormalized;
            Current.HighTensionDangerNormalized = Current.BreakStressNormalized;
            Current.DirectionalControlNormalized = Current.RodResponseQualityNormalized;
            Current.FightPhaseRemainingSeconds = aiOutput.PhaseRemainingSeconds;
            Current.FightDirection = behavior.DirectionNormalized;
            Current.Feedback = new FishingFeedbackFrame(
                ToFeedbackState(behavior.State), behavior.ForceNormalized);
        }

        private string BuildV2FightHint()
        {
            if (Current.IsRunTelegraphing)
            {
                return Current.RunTelegraphDirectionNormalized < 0f
                    ? "RUN INCOMING LEFT"
                    : "RUN INCOMING RIGHT";
            }
            if (Current.VirtualTensionZone == FishingV2TensionZone.Slack)
            {
                return "SLACK! Raise the rod and restore line pressure";
            }
            if (Current.VirtualTensionZone == FishingV2TensionZone.Danger)
            {
                return "DANGER! Stop reeling and move with the fish";
            }
            if (Current.V2BehaviorState == FishingV2BehaviorState.Run)
            {
                return Current.V2FishDirectionNormalized < 0f
                    ? "Fish runs LEFT - follow LEFT and ease the reel"
                    : "Fish runs RIGHT - follow RIGHT and ease the reel";
            }
            if (Current.V2BehaviorState == FishingV2BehaviorState.Rest)
            {
                return "Fish is resting - reel while pressure is safe";
            }
            if (Current.HeadShakeActive)
            {
                return "HEAD SHAKE - steady the rod through the tension bump";
            }
            return "Keep a moderate rod angle and reel through stable pressure";
        }

        private static FishingFeedbackState ToFeedbackState(FishingV2BehaviorState state)
        {
            if (state == FishingV2BehaviorState.Run) return FishingFeedbackState.Run;
            if (state == FishingV2BehaviorState.Rest) return FishingFeedbackState.Rest;
            return FishingFeedbackState.Fight;
        }

        private void TickOutcome()
        {
            if (_stateElapsed >= _rules.OutcomeDisplaySeconds)
            {
                TransitionTo(FishingPlayerState.Cooldown);
            }
        }

        private void TickCooldown()
        {
            Current.Hint = "Preparing the next cast...";
            if (_stateElapsed >= _rules.CooldownSeconds)
            {
                ResetTransientValues();
                TransitionTo(FishingPlayerState.Idle);
            }
        }

        private void BeginLegacyWaiting()
        {
            double range = _rules.MaxBiteDelaySeconds - _rules.MinBiteDelaySeconds;
            _biteDelay = _rules.MinBiteDelaySeconds + ((float)_random.NextDouble() * (float)range);
            ScheduleNibble(0.15f);
            Current.BiteDelayRemainingSeconds = _biteDelay;
            TransitionTo(FishingPlayerState.Waiting);
        }

        private void BeginV2WaitingAttempt(FishingV2WaitingAttemptReason reason)
        {
            ClearV2AttemptSchedule();

            float rawBiteDelay;
            if (reason == FishingV2WaitingAttemptReason.MissedBite)
            {
                rawBiteDelay = RandomRange(
                    _v2PreFightTuning.MissedBiteRetryMinSeconds,
                    _v2PreFightTuning.MissedBiteRetryMaxSeconds);
            }
            else
            {
                rawBiteDelay = RandomRange(
                    _rules.MinBiteDelaySeconds,
                    _rules.MaxBiteDelaySeconds);
                if (reason == FishingV2WaitingAttemptReason.EarlyHook)
                {
                    rawBiteDelay += RandomRange(
                        _v2PreFightTuning.EarlyHookPenaltyMinSeconds,
                        _v2PreFightTuning.EarlyHookPenaltyMaxSeconds);
                }
            }

            float lead = RandomRange(
                _v2PreFightTuning.NibbleLeadMinSeconds,
                _v2PreFightTuning.NibbleLeadMaxSeconds);
            _v2ScheduledNibbleStartTime = FishingMath.Max(0f, rawBiteDelay - lead);
            _v2ScheduledBiteTime = FishingMath.Max(
                rawBiteDelay,
                _v2ScheduledNibbleStartTime +
                _v2PreFightTuning.NibbleDurationSeconds +
                _v2PreFightTuning.NibbleToBiteGapSeconds);
            _v2EarliestBiteAllowedTime = _v2ScheduledBiteTime;
            _biteDelay = _v2ScheduledBiteTime;

            TransitionTo(FishingPlayerState.Waiting);
            Current.BiteDelayRemainingSeconds = _v2EarliestBiteAllowedTime;
            if (reason == FishingV2WaitingAttemptReason.EarlyHook)
                Current.Hint = "Too early - wait for the bite";
            else if (reason == FishingV2WaitingAttemptReason.MissedBite)
                Current.Hint = "Missed! Wait for another bite";
            else
                Current.Hint = "Wait for a bite...";
        }

        private void StartV2Nibble()
        {
            _v2NibbleStarted = true;
            _v2ActualNibbleStartTime = _stateElapsed;
            _v2ActualNibbleEndTime = -1f;
            _v2NibbleRemaining = _v2PreFightTuning.NibbleDurationSeconds;
            _v2EarliestBiteAllowedTime = FishingMath.Max(
                _v2ScheduledBiteTime,
                _v2ActualNibbleStartTime +
                _v2PreFightTuning.NibbleDurationSeconds +
                _v2PreFightTuning.NibbleToBiteGapSeconds);
            Current.IsNibbling = true;
            Current.NibbleRemainingSeconds = _v2NibbleRemaining;
            Current.NibbleIntensityNormalized = _v2PreFightTuning.NibbleIntensityNormalized;
            Current.NibbleEventSequence++;
            Current.Hint = "NIBBLE... wait";
            Current.Feedback = new FishingFeedbackFrame(
                FishingFeedbackState.Bite, Current.NibbleIntensityNormalized);
            UpdateV2BiteDelayRemaining();
        }

        private void BeginV2BiteWindow()
        {
            Current.IsNibbling = false;
            Current.NibbleRemainingSeconds = 0f;
            Current.NibbleIntensityNormalized = 0f;
            Current.BiteDelayRemainingSeconds = 0f;
            Current.HookWindowRemainingSeconds = _rules.HookWindowSeconds;
            Current.BiteEventSequence++;
            Current.Hint = "BITE! HOOK NOW";
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.Bite, 1f);
            TransitionTo(FishingPlayerState.BiteWindow);
        }

        private void HandleV2EarlyHook()
        {
            Current.EarlyHookCount++;
            BeginV2WaitingAttempt(FishingV2WaitingAttemptReason.EarlyHook);
        }

        private void UpdateV2BiteDelayRemaining()
        {
            Current.BiteDelayRemainingSeconds = FishingMath.Max(
                0f, _v2EarliestBiteAllowedTime - _stateElapsed);
        }

        private void ClearV2AttemptSchedule()
        {
            _v2ScheduledNibbleStartTime = -1f;
            _v2ScheduledBiteTime = -1f;
            _v2EarliestBiteAllowedTime = -1f;
            _v2NibbleRemaining = 0f;
            _v2ActualNibbleStartTime = -1f;
            _v2ActualNibbleEndTime = -1f;
            _v2NibbleStarted = false;
            Current.IsNibbling = false;
            Current.NibbleRemainingSeconds = 0f;
            Current.NibbleIntensityNormalized = 0f;
            Current.HookWindowRemainingSeconds = 0f;
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (maximum <= minimum) return minimum;
            return minimum + (float)_random.NextDouble() * (maximum - minimum);
        }

        private void ScheduleNibble(float earliestTime)
        {
            if (_random.NextDouble() > 0.82d)
            {
                _nibbleAt = -1f;
                _nibbleUntil = -1f;
                return;
            }

            float lead = 0.38f + (float)_random.NextDouble() * 0.52f;
            _nibbleAt = FishingMath.Max(earliestTime, _biteDelay - lead);
            _nibbleUntil = FishingMath.Min(_biteDelay - 0.08f, _nibbleAt + 0.28f);
            if (_nibbleUntil <= _nibbleAt) _nibbleAt = _nibbleUntil = -1f;
        }

        private void CatchFish()
        {
            int rawScore = _fish.BaseScore + (int)(Current.CastPower * 50f) + (int)(Current.LineDurability * 0.5f);
            int awarded = (int)(rawScore * _fish.DifficultyScoreMultiplier);
            Current.TotalScore += awarded;
            Current.LastOutcome = $"Caught {_fish.DisplayName}! +{awarded}";
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.Caught, 1f);
            Current.EscapeReason = FishingEscapeReason.None;
            CompleteCycle(true, FishingEscapeReason.None, awarded);
            TransitionTo(FishingPlayerState.Caught);
        }

        private void Escape(FishingEscapeReason reason, string message)
        {
            Current.LastOutcome = message;
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.Escaped, 0.8f);
            Current.EscapeReason = reason;
            CompleteCycle(false, reason, 0);
            TransitionTo(FishingPlayerState.Escaped);
        }

        private void CompleteCycle(bool wasCaught, FishingEscapeReason reason, int awardedScore)
        {
            _cycleNumber++;
            Current.CompletedCycles = _cycleNumber;
            LastResult = new FishingCycleResult
            {
                CycleNumber = _cycleNumber,
                WasCaught = wasCaught,
                EscapeReason = reason,
                FishId = _fish.FishId,
                FishDisplayName = _fish.DisplayName,
                DifficultyLabel = _fish.DifficultyLabel,
                AwardedScore = awardedScore,
                TotalScoreAfterCycle = Current.TotalScore,
                CastPower = Current.CastPower,
                RemainingLineDurability = Current.LineDurability
            };
            CycleFinished?.Invoke(LastResult);
        }

        private void BeginNextFightPhase(bool initial)
        {
            double roll = _random.NextDouble();
            if (roll < _fish.RunChance)
            {
                _fightPhase = FishingFeedbackState.Run;
                _phaseIntensity = FishingMath.Clamp01(0.58f + _fish.PullStrength * 0.34f + (float)_random.NextDouble() * 0.08f);
                _targetTensionShift = _fish.MaxTensionShift * (0.74f + (float)_random.NextDouble() * 0.26f);
            }
            else if (roll < _fish.RunChance + _fish.RestChance)
            {
                _fightPhase = FishingFeedbackState.Rest;
                _phaseIntensity = FishingMath.Clamp01(0.12f + _fish.PullStrength * 0.18f);
                _targetTensionShift = -_fish.MaxTensionShift * (0.30f + (float)_random.NextDouble() * 0.18f);
            }
            else
            {
                _fightPhase = FishingFeedbackState.Fight;
                _phaseIntensity = FishingMath.Clamp01(0.34f + _fish.PullStrength * 0.42f + (float)_random.NextDouble() * 0.08f);
                _targetTensionShift = ((float)_random.NextDouble() * 2f - 1f) * _fish.MaxTensionShift * 0.28f;
            }

            if (initial || _random.NextDouble() < _fish.DirectionChangeChance)
            {
                _fightDirection = _random.NextDouble() < 0.5d ? -1f : 1f;
            }

            _fightPhaseDuration = _fish.MinBehaviorPhaseSeconds +
                (float)_random.NextDouble() * (_fish.MaxBehaviorPhaseSeconds - _fish.MinBehaviorPhaseSeconds);
            _fightPhaseRemaining = _fightPhaseDuration;
            Current.Feedback = new FishingFeedbackFrame(_fightPhase, _phaseIntensity);
            Current.FightDirection = _fightDirection;
            Current.FightPhaseRemainingSeconds = _fightPhaseRemaining;
        }

        private void UpdateFightBehavior(float dt)
        {
            _fightPhaseRemaining -= dt;
            if (_fightPhaseRemaining <= 0f) BeginNextFightPhase(false);
            Current.Feedback = new FishingFeedbackFrame(_fightPhase, _phaseIntensity);
            Current.FightDirection = _fightDirection;
            Current.FightPhaseRemainingSeconds = FishingMath.Max(0f, _fightPhaseRemaining);
        }

        private string BuildFightHint(float tension, bool safeTension, float reel)
        {
            if (tension < _rules.SafeTensionMin) return "SLACK! Raise mock tension with E";
            if (tension > _rules.SafeTensionMax) return "TOO TIGHT! Lower mock tension with Q";
            if (Current.Feedback.State == FishingFeedbackState.Run)
            {
                return _fightDirection < 0f
                    ? "Fish runs LEFT - hold D, ease reel, lower tension"
                    : "Fish runs RIGHT - hold A, ease reel, lower tension";
            }
            if (Current.Feedback.State == FishingFeedbackState.Rest) return "Fish is resting - reel hard now";
            if (reel <= 0f) return "Fish is struggling - keep tension safe";
            return "Reeling through the struggle";
        }

        private void TransitionTo(FishingPlayerState next)
        {
            Current.State = next;
            _stateElapsed = 0f;
            Current.StateElapsedSeconds = 0f;

            if (next == FishingPlayerState.Idle)
            {
                Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f);
            }
        }

        private void ResetTransientValues()
        {
            _stateElapsed = 0f;
            _biteDelay = 0f;
            _fightElapsed = 0f;
            _slackElapsed = 0f;
            _highTensionElapsed = 0f;
            _nibbleAt = -1f;
            _nibbleUntil = -1f;
            _earlyStrikeHintUntil = -1f;
            _falseStrikeCount = 0;
            _v2ScheduledNibbleStartTime = -1f;
            _v2ScheduledBiteTime = -1f;
            _v2EarliestBiteAllowedTime = -1f;
            _v2NibbleRemaining = 0f;
            _v2ActualNibbleStartTime = -1f;
            _v2ActualNibbleEndTime = -1f;
            _v2NibbleStarted = false;
            _fightPhase = FishingFeedbackState.None;
            _fightPhaseRemaining = 0f;
            _fightPhaseDuration = 0f;
            _fightDirection = 0f;
            _phaseIntensity = 0f;
            _targetTensionShift = 0f;
            _smoothedTensionShift = 0f;
            Current.CastPower = 0f;
            Current.BiteDelayRemainingSeconds = 0f;
            Current.HookWindowRemainingSeconds = 0f;
            Current.FightRemainingSeconds = 0f;
            Current.FightElapsedSeconds = 0f;
            Current.TensionNormalized = 0.5f;
            Current.RawTensionNormalized = 0.5f;
            Current.FishTensionShift = 0f;
            Current.FightPhaseRemainingSeconds = 0f;
            Current.FightDirection = 0f;
            Current.DirectionalControlNormalized = 0.5f;
            Current.FishMaxHealth = _fish.MaxStamina;
            Current.FishHealth = _fish.MaxStamina;
            Current.LineDurability = 100f;
            Current.SlackDangerNormalized = 0f;
            Current.HighTensionDangerNormalized = 0f;
            if (_useV2FightModel && _v2FightModel != null)
            {
                _v2FightModel.Reset(_v2FightTuning);
                _v2FishAI.Reset(_v2FishAITuning, _v2AISeed + _cycleNumber, _fish.PullStrength);
                _v2FishAIOutput = _v2FishAI.Current;
                Current.FishDistanceMeters = _v2FightModel.FishDistanceMeters;
                Current.FishStaminaNormalized = _v2FightModel.FishStaminaNormalized;
                Current.VirtualLineTensionNormalized = _v2FightModel.VirtualLineTensionNormalized;
                Current.BreakStressNormalized = _v2FightModel.BreakStressNormalized;
                Current.HookLooseRiskNormalized = _v2FightModel.HookLooseRiskNormalized;
                Current.RodResponseQualityNormalized = _v2FightModel.RodResponseQualityNormalized;
                Current.ReelEfficiencyNormalized = _v2FightModel.ReelEfficiencyNormalized;
                Current.VirtualTensionZone = _v2FightModel.TensionZone;
                ClearV2AIFields();
            }
            else
            {
                Current.FishDistanceMeters = 0f;
                Current.FishStaminaNormalized = 1f;
                Current.VirtualLineTensionNormalized = 0.5f;
                Current.BreakStressNormalized = 0f;
                Current.HookLooseRiskNormalized = 0f;
                Current.RodResponseQualityNormalized = 0.5f;
                Current.ReelEfficiencyNormalized = 0f;
                Current.V2BehaviorState = FishingV2BehaviorState.None;
                Current.VirtualTensionZone = FishingV2TensionZone.Good;
                Current.V2FishForceNormalized = 0f;
                Current.V2FishDirectionNormalized = 0f;
                ClearV2AIFields();
            }
            Current.FishId = _fish.FishId;
            Current.FishDisplayName = _fish.DisplayName;
            Current.DifficultyLabel = _fish.DifficultyLabel;
            Current.Hint = "Hold SPACE to charge a cast";
            Current.Feedback = new FishingFeedbackFrame(FishingFeedbackState.None, 0f);
            Current.EscapeReason = FishingEscapeReason.None;
            Current.IsNibbling = false;
            Current.NibbleEventSequence = 0;
            Current.NibbleRemainingSeconds = 0f;
            Current.NibbleIntensityNormalized = 0f;
            Current.BiteEventSequence = 0;
            Current.EarlyHookCount = 0;
            Current.MissedBiteRetryCount = 0;
            Current.FalseStrikeCount = 0;
        }

        private void ClearV2AIFields()
        {
            Current.V2BehaviorState = FishingV2BehaviorState.None;
            Current.V2FishForceNormalized = 0f;
            Current.V2FishDirectionNormalized = 0f;
            Current.AIStaminaBand = FishingV2StaminaBand.High;
            Current.AIPhaseRemainingSeconds = 0f;
            Current.IsRunTelegraphing = false;
            Current.RunTelegraphDirectionNormalized = 0f;
            Current.RunTelegraphRemainingSeconds = 0f;
            Current.HeadShakeActive = false;
            Current.HeadShakeIntensityNormalized = 0f;
            Current.HeadShakeEventSequence = 0;
            Current.FinalRunDecisionMade = false;
            Current.FinalRunPending = false;
            Current.IsFinalRun = false;
            Current.FinalRunUsed = false;
        }

        private static float SanitizeNormalized(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return FishingMath.Clamp01(value);
        }

        private static float SanitizeSigned(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return FishingMath.Clamp(value, -1f, 1f);
        }

        private void EnsureInitialized()
        {
            if (_baseRules == null || _rules == null || _fish == null || _random == null)
            {
                throw new InvalidOperationException("FishingStateMachine.Initialize must be called before use.");
            }
        }

        private void ApplyFishRules()
        {
            _rules = _baseRules.Copy();
            if (_fish.UseSpeciesRuleOverrides)
            {
                _rules.MinBiteDelaySeconds = _fish.MinBiteDelaySeconds;
                _rules.MaxBiteDelaySeconds = _fish.MaxBiteDelaySeconds;
                _rules.HookWindowSeconds = _fish.HookWindowSeconds;
                _rules.FightTimeoutSeconds = _fish.FightTimeoutSeconds;
            }
            _rules.Sanitize();
        }
    }
}
