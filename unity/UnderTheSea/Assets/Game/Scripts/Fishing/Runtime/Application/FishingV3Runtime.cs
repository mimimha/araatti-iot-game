using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    public enum FishingGameplayRuntimeMode
    {
        LegacyV2,
        V3
    }

    public enum FishingV3RuntimeState
    {
        Ready,
        Running,
        Paused,
        Completed,
        Aborted,
        Shutdown
    }

    public enum FishingV3SessionFlowMode
    {
        ImmediateFight,
        BiteHook
    }

    public enum FishingV3GameplayPhase
    {
        WaitingForBite,
        HookWindow,
        Fighting,
        Terminal
    }

    [Serializable]
    public sealed class FishingV3BiteHookTuning
    {
        public float BiteDelayMinSeconds = 1.5f;
        public float BiteDelayMaxSeconds = 3.5f;
        public float HookWindowMinSeconds = 0.6f;
        public float HookWindowMaxSeconds = 1f;
        public int RandomSeed = 3119;

        public FishingV3BiteHookTuning Copy()
        {
            return (FishingV3BiteHookTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            SanitizeRange(
                ref BiteDelayMinSeconds,
                ref BiteDelayMaxSeconds,
                1.5f,
                3.5f);
            SanitizeRange(
                ref HookWindowMinSeconds,
                ref HookWindowMaxSeconds,
                0.6f,
                1f);
        }

        private static void SanitizeRange(
            ref float minimum,
            ref float maximum,
            float fallbackMinimum,
            float fallbackMaximum)
        {
            minimum = IsFinite(minimum) && minimum >= 0.01f
                ? minimum
                : fallbackMinimum;
            maximum = IsFinite(maximum) && maximum >= minimum
                ? maximum
                : Math.Max(minimum, fallbackMaximum);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Read-only view of the device-independent V3 session.
    /// </summary>
    public sealed class FishingV3Snapshot
    {
        public FishingV3RuntimeState RuntimeState { get; internal set; }
        public FishingV3SessionFlowMode SessionFlowMode { get; internal set; }
        public FishingV3GameplayPhase GameplayPhase { get; internal set; }
        public float GameplayPhaseRemainingSeconds { get; internal set; }
        public bool IsTimingReelActive { get; internal set; }
        public FishingV3FishState FishState { get; internal set; }
        public float TargetTensionNormalized { get; internal set; }
        public float TensionNormalized { get; internal set; }
        public FishingV3TensionZone TensionZone { get; internal set; }
        public float CaptureProgressNormalized { get; internal set; }
        public float BreakStressNormalized { get; internal set; }
        public float EscapeRiskNormalized { get; internal set; }
        public FishingV3Result Result { get; internal set; }
        public float FishStateRemainingSeconds { get; internal set; }
        public int HeadShakeEventSequence { get; internal set; }
        public float HeadShakeIntensityNormalized { get; internal set; }
        public float BehaviorTensionOffsetNormalized { get; internal set; }
        public float PullBurstOffsetNormalized { get; internal set; }
        public FishingV3ReelControlMode ReelControlMode { get; internal set; }
        public float TimingPointerNormalized { get; internal set; }
        public float TimingPerfectHalfWidthNormalized { get; internal set; }
        public float TimingGoodHalfWidthNormalized { get; internal set; }
        public FishingV3TimingGrade LastTimingGrade { get; internal set; }
        public int TimingJudgementSequence { get; internal set; }
        public float TimingMissPenaltyNormalized { get; internal set; }
        public float SuccessfulReelSupportNormalized { get; internal set; }
        public FishingV3FishProfileId FishProfileId { get; internal set; }
        public string FishProfileDisplayName { get; internal set; }
        public string FishVisualId { get; internal set; }
    }

    /// <summary>
    /// Owns V3 session lifecycle and converts either legacy hold input or a
    /// device-independent timing press into canonical reel revolutions before
    /// advancing the pure V3 model. Input sampling remains the responsibility
    /// of FishingGameController.
    /// </summary>
    public sealed class FishingV3Runtime
    {
        private readonly FishingV3Tuning _modelTuning;
        private readonly FishingV3ReelInputAdapter _reelInputAdapter;
        private readonly FishingV3Model _model;
        private readonly FishingV3FishBehavior _fishBehavior;
        private readonly FishingV3TimingReel _timingReel;
        private readonly FishingV3ReelControlMode _reelControlMode;
        private readonly FishingV3BiteHookTuning _biteHookTuning;
        private readonly FishingV3SessionFlowMode _sessionFlowMode;
        private readonly FishingV3FishProfile _fishProfile;
        private Random _biteRandom;
        private FishingV3GameplayPhase _gameplayPhase;
        private float _gameplayPhaseRemainingSeconds;
        private FishingV3FishState _fishState;

        public FishingV3Snapshot Current { get; private set; }
        public FishingV3RuntimeState State => Current.RuntimeState;
        public FishingV3Result Result => Current.Result;
        public FishingV3FishProfile FishProfile => _fishProfile.Copy();

        public FishingV3Runtime(
            FishingV3Tuning modelTuning = null,
            FishingV3ReelInputTuning reelInputTuning = null,
            FishingV3FishBehaviorTuning behaviorTuning = null,
            FishingV3TimingReelTuning timingTuning = null,
            FishingV3ReelControlMode reelControlMode = FishingV3ReelControlMode.LegacyHold,
            FishingV3BiteHookTuning biteHookTuning = null,
            FishingV3SessionFlowMode sessionFlowMode =
                FishingV3SessionFlowMode.ImmediateFight,
            FishingV3FishProfile fishProfile = null)
        {
            FishingV3ProfileTuningSet profileTuning = FishingV3FishProfileTuning.Apply(
                fishProfile,
                modelTuning,
                behaviorTuning,
                timingTuning);
            _fishProfile = profileTuning.Profile;
            _modelTuning = profileTuning.Model;
            _reelInputAdapter = new FishingV3ReelInputAdapter(reelInputTuning);
            _model = new FishingV3Model(_modelTuning);
            _fishBehavior = new FishingV3FishBehavior(profileTuning.Behavior);
            _timingReel = new FishingV3TimingReel(profileTuning.Timing);
            _reelControlMode = reelControlMode == FishingV3ReelControlMode.Timing
                ? FishingV3ReelControlMode.Timing
                : FishingV3ReelControlMode.LegacyHold;
            _biteHookTuning = (biteHookTuning ?? new FishingV3BiteHookTuning()).Copy();
            _biteHookTuning.Sanitize();
            _sessionFlowMode = sessionFlowMode == FishingV3SessionFlowMode.BiteHook
                ? FishingV3SessionFlowMode.BiteHook
                : FishingV3SessionFlowMode.ImmediateFight;
            Reset();
        }

        public void Reset()
        {
            _model.Reset(_modelTuning);
            _fishBehavior.Reset(FishingV3FishState.Calm);
            _timingReel.Reset();
            _biteRandom = new Random(_biteHookTuning.RandomSeed);
            _fishState = _fishBehavior.State;
            if (_sessionFlowMode == FishingV3SessionFlowMode.BiteHook)
            {
                _gameplayPhase = FishingV3GameplayPhase.WaitingForBite;
                _gameplayPhaseRemainingSeconds = RandomRange(
                    _biteHookTuning.BiteDelayMinSeconds,
                    _biteHookTuning.BiteDelayMaxSeconds);
            }
            else
            {
                _gameplayPhase = FishingV3GameplayPhase.Fighting;
                _gameplayPhaseRemainingSeconds = 0f;
            }
            RefreshSnapshot(FishingV3RuntimeState.Ready);
        }

        public void Begin()
        {
            if (State == FishingV3RuntimeState.Shutdown) return;
            if (State == FishingV3RuntimeState.Completed ||
                State == FishingV3RuntimeState.Aborted)
            {
                Reset();
            }

            if (State == FishingV3RuntimeState.Ready ||
                State == FishingV3RuntimeState.Paused)
            {
                RefreshSnapshot(FishingV3RuntimeState.Running);
            }
        }

        public void SetFishState(FishingV3FishState fishState)
        {
            if (State == FishingV3RuntimeState.Completed ||
                State == FishingV3RuntimeState.Aborted ||
                State == FishingV3RuntimeState.Shutdown)
            {
                return;
            }

            _fishBehavior.SetState(NormalizeFishState(fishState));
            _fishState = _fishBehavior.State;
            RefreshSnapshot(State);
        }

        public void Tick(FishingInputFrame input, float deltaTime)
        {
            if (State != FishingV3RuntimeState.Running) return;
            if (!IsFinite(deltaTime) || deltaTime <= 0f) return;

            if (_sessionFlowMode == FishingV3SessionFlowMode.BiteHook &&
                _gameplayPhase != FishingV3GameplayPhase.Fighting)
            {
                TickPreFight(input, deltaTime);
                return;
            }

            TickFighting(input, deltaTime);
        }

        private void TickPreFight(FishingInputFrame input, float deltaTime)
        {
            if (_gameplayPhase == FishingV3GameplayPhase.WaitingForBite)
            {
                _gameplayPhaseRemainingSeconds = Math.Max(
                    0f,
                    _gameplayPhaseRemainingSeconds - deltaTime);
                if (_gameplayPhaseRemainingSeconds <= 0f)
                {
                    _gameplayPhase = FishingV3GameplayPhase.HookWindow;
                    _gameplayPhaseRemainingSeconds = RandomRange(
                        _biteHookTuning.HookWindowMinSeconds,
                        _biteHookTuning.HookWindowMaxSeconds);
                }

                RefreshSnapshot(FishingV3RuntimeState.Running);
                return;
            }

            if (_gameplayPhase != FishingV3GameplayPhase.HookWindow)
            {
                return;
            }

            if (input.HookPressed)
            {
                EnterFighting();
                RefreshSnapshot(FishingV3RuntimeState.Running);
                return;
            }

            _gameplayPhaseRemainingSeconds = Math.Max(
                0f,
                _gameplayPhaseRemainingSeconds - deltaTime);
            if (_gameplayPhaseRemainingSeconds <= 0f)
            {
                _model.ResolveFishEscaped();
                _gameplayPhase = FishingV3GameplayPhase.Terminal;
                RefreshSnapshot(FishingV3RuntimeState.Completed);
                return;
            }

            RefreshSnapshot(FishingV3RuntimeState.Running);
        }

        private void EnterFighting()
        {
            _gameplayPhase = FishingV3GameplayPhase.Fighting;
            _gameplayPhaseRemainingSeconds = 0f;
            _fishBehavior.SetState(_fishState);
            _timingReel.Reset();
        }

        private void TickFighting(FishingInputFrame input, float deltaTime)
        {

            _fishBehavior.Tick(deltaTime);
            _fishState = _fishBehavior.State;

            FishingV3ReelInput reelInput;
            float timingPenalty = 0f;
            float successfulReelSupport = 0f;
            if (_reelControlMode == FishingV3ReelControlMode.Timing)
            {
                _timingReel.Tick(_fishState, deltaTime);
                reelInput = input.TimingPressed
                    ? _timingReel.Judge(_fishState).ReelInput
                    : FishingV3ReelInput.Zero;
                timingPenalty = _timingReel.MissPenaltyNormalized;
                successfulReelSupport =
                    _timingReel.SuccessfulReelSupportNormalized;
                successfulReelSupport = LimitSupportBelowDanger(
                    successfulReelSupport,
                    _fishBehavior.TensionOffsetNormalized + timingPenalty);
            }
            else
            {
                reelInput = _reelInputAdapter.ConvertLegacyFrame(input, deltaTime);
            }

            _model.Tick(
                _fishState,
                reelInput.ReelDeltaRevolutions,
                deltaTime,
                _fishBehavior.TensionOffsetNormalized +
                successfulReelSupport +
                timingPenalty);

            FishingV3RuntimeState nextState = _model.Result == FishingV3Result.Active
                ? FishingV3RuntimeState.Running
                : FishingV3RuntimeState.Completed;
            if (nextState == FishingV3RuntimeState.Completed)
            {
                _gameplayPhase = FishingV3GameplayPhase.Terminal;
                _gameplayPhaseRemainingSeconds = 0f;
                _timingReel.ClearSuccessfulReelSupport();
            }
            RefreshSnapshot(nextState);
        }

        public void SetPaused(bool paused)
        {
            if (paused && State == FishingV3RuntimeState.Running)
            {
                RefreshSnapshot(FishingV3RuntimeState.Paused);
            }
            else if (!paused && State == FishingV3RuntimeState.Paused)
            {
                RefreshSnapshot(FishingV3RuntimeState.Running);
            }
        }

        public void Abort()
        {
            if (State == FishingV3RuntimeState.Completed ||
                State == FishingV3RuntimeState.Shutdown)
            {
                return;
            }

            _gameplayPhase = FishingV3GameplayPhase.Terminal;
            _gameplayPhaseRemainingSeconds = 0f;
            _timingReel.ClearSuccessfulReelSupport();
            RefreshSnapshot(FishingV3RuntimeState.Aborted);
        }

        public void Shutdown()
        {
            _gameplayPhase = FishingV3GameplayPhase.Terminal;
            _gameplayPhaseRemainingSeconds = 0f;
            _timingReel.ClearSuccessfulReelSupport();
            RefreshSnapshot(FishingV3RuntimeState.Shutdown);
        }

        private void RefreshSnapshot(FishingV3RuntimeState runtimeState)
        {
            Current = new FishingV3Snapshot
            {
                RuntimeState = runtimeState,
                SessionFlowMode = _sessionFlowMode,
                GameplayPhase = _gameplayPhase,
                GameplayPhaseRemainingSeconds = _gameplayPhaseRemainingSeconds,
                IsTimingReelActive = _gameplayPhase == FishingV3GameplayPhase.Fighting &&
                    _reelControlMode == FishingV3ReelControlMode.Timing &&
                    (runtimeState == FishingV3RuntimeState.Running ||
                     runtimeState == FishingV3RuntimeState.Paused),
                FishState = _fishState,
                TargetTensionNormalized = _model.TargetTensionNormalized,
                TensionNormalized = _model.TensionNormalized,
                TensionZone = _model.TensionZone,
                CaptureProgressNormalized = _model.CaptureProgressNormalized,
                BreakStressNormalized = _model.BreakStressNormalized,
                EscapeRiskNormalized = _model.EscapeRiskNormalized,
                Result = _model.Result,
                FishStateRemainingSeconds = _gameplayPhase == FishingV3GameplayPhase.Fighting
                    ? _fishBehavior.PhaseRemainingSeconds
                    : 0f,
                HeadShakeEventSequence = _fishBehavior.HeadShakeEventSequence,
                HeadShakeIntensityNormalized =
                    _fishBehavior.LastHeadShakeIntensityNormalized,
                BehaviorTensionOffsetNormalized =
                    _gameplayPhase == FishingV3GameplayPhase.Fighting
                        ? _fishBehavior.TensionOffsetNormalized
                        : 0f,
                PullBurstOffsetNormalized =
                    _gameplayPhase == FishingV3GameplayPhase.Fighting
                        ? _fishBehavior.PullBurstOffsetNormalized
                        : 0f,
                ReelControlMode = _reelControlMode,
                TimingPointerNormalized = _timingReel.PointerNormalized,
                TimingPerfectHalfWidthNormalized =
                    _timingReel.GetPerfectHalfWidth(_fishState),
                TimingGoodHalfWidthNormalized =
                    _timingReel.GetGoodHalfWidth(_fishState),
                LastTimingGrade = _timingReel.LastGrade,
                TimingJudgementSequence = _timingReel.JudgementSequence,
                TimingMissPenaltyNormalized = _timingReel.MissPenaltyNormalized,
                SuccessfulReelSupportNormalized =
                    _timingReel.SuccessfulReelSupportNormalized,
                FishProfileId = _fishProfile.Id,
                FishProfileDisplayName = _fishProfile.DisplayName,
                FishVisualId = _fishProfile.VisualId
            };
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (maximum <= minimum) return minimum;
            return minimum + (float)_biteRandom.NextDouble() * (maximum - minimum);
        }

        private float LimitSupportBelowDanger(float support, float otherOffset)
        {
            const float dangerHeadroom = 0.001f;
            float baseTension;
            switch (_fishState)
            {
                case FishingV3FishState.Run:
                    baseTension = _modelTuning.RunBaseTension;
                    break;
                case FishingV3FishState.Fight:
                    baseTension = _modelTuning.FightBaseTension;
                    break;
                default:
                    baseTension = _modelTuning.CalmBaseTension;
                    break;
            }

            float maximumTarget = Math.Max(
                0f,
                _modelTuning.HighUpperThreshold - dangerHeadroom);
            float availableHeadroom = Math.Max(
                0f,
                maximumTarget - baseTension - otherOffset);
            return Math.Min(support, availableHeadroom);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static FishingV3FishState NormalizeFishState(FishingV3FishState state)
        {
            return state == FishingV3FishState.Fight || state == FishingV3FishState.Run
                ? state
                : FishingV3FishState.Calm;
        }
    }
}
