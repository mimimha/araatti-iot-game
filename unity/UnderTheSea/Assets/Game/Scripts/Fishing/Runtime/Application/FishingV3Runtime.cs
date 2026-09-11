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

    /// <summary>
    /// Read-only view of the device-independent V3 session.
    /// </summary>
    public sealed class FishingV3Snapshot
    {
        public FishingV3RuntimeState RuntimeState { get; internal set; }
        public FishingV3FishState FishState { get; internal set; }
        public float TargetTensionNormalized { get; internal set; }
        public float TensionNormalized { get; internal set; }
        public FishingV3TensionZone TensionZone { get; internal set; }
        public float CaptureProgressNormalized { get; internal set; }
        public float BreakStressNormalized { get; internal set; }
        public float EscapeRiskNormalized { get; internal set; }
        public FishingV3Result Result { get; internal set; }
    }

    /// <summary>
    /// Owns V3 session lifecycle and translates an already sampled legacy input
    /// frame into canonical reel revolutions before advancing the pure V3 model.
    /// Input sampling remains the responsibility of FishingGameController.
    /// </summary>
    public sealed class FishingV3Runtime
    {
        private readonly FishingV3Tuning _modelTuning;
        private readonly FishingV3ReelInputAdapter _reelInputAdapter;
        private readonly FishingV3Model _model;
        private FishingV3FishState _fishState;

        public FishingV3Snapshot Current { get; private set; }
        public FishingV3RuntimeState State => Current.RuntimeState;
        public FishingV3Result Result => Current.Result;

        public FishingV3Runtime(
            FishingV3Tuning modelTuning = null,
            FishingV3ReelInputTuning reelInputTuning = null)
        {
            _modelTuning = (modelTuning ?? new FishingV3Tuning()).Copy();
            _modelTuning.Sanitize();
            _reelInputAdapter = new FishingV3ReelInputAdapter(reelInputTuning);
            _model = new FishingV3Model(_modelTuning);
            Reset();
        }

        public void Reset()
        {
            _model.Reset(_modelTuning);
            _fishState = FishingV3FishState.Calm;
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

            _fishState = NormalizeFishState(fishState);
            RefreshSnapshot(State);
        }

        public void Tick(FishingInputFrame input, float deltaTime)
        {
            if (State != FishingV3RuntimeState.Running) return;

            FishingV3ReelInput reelInput =
                _reelInputAdapter.ConvertLegacyFrame(input, deltaTime);
            _model.Tick(_fishState, reelInput.ReelDeltaRevolutions, deltaTime);

            FishingV3RuntimeState nextState = _model.Result == FishingV3Result.Active
                ? FishingV3RuntimeState.Running
                : FishingV3RuntimeState.Completed;
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

            RefreshSnapshot(FishingV3RuntimeState.Aborted);
        }

        public void Shutdown()
        {
            RefreshSnapshot(FishingV3RuntimeState.Shutdown);
        }

        private void RefreshSnapshot(FishingV3RuntimeState runtimeState)
        {
            Current = new FishingV3Snapshot
            {
                RuntimeState = runtimeState,
                FishState = _fishState,
                TargetTensionNormalized = _model.TargetTensionNormalized,
                TensionNormalized = _model.TensionNormalized,
                TensionZone = _model.TensionZone,
                CaptureProgressNormalized = _model.CaptureProgressNormalized,
                BreakStressNormalized = _model.BreakStressNormalized,
                EscapeRiskNormalized = _model.EscapeRiskNormalized,
                Result = _model.Result
            };
        }

        private static FishingV3FishState NormalizeFishState(FishingV3FishState state)
        {
            return state == FishingV3FishState.Fight || state == FishingV3FishState.Run
                ? state
                : FishingV3FishState.Calm;
        }
    }
}
