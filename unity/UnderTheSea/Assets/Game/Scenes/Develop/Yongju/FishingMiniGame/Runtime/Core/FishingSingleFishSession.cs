using System;

namespace FishingMiniGame.Core
{
    public enum FishingGameMode
    {
        LegacyRound,
        SingleFishSession
    }

    public enum FishingSessionState
    {
        Uninitialized,
        Ready,
        Countdown,
        Playing,
        Completed,
        Aborted
    }

    public enum FishingSessionOutcome
    {
        None,
        Caught,
        Escaped
    }

    public sealed class FishingSessionContext
    {
        public string SessionId = "local-session";
        public string ParticipantId = "local-player";
        public float CountdownSeconds;
        public FishProfile Fish = new FishProfile();

        public FishingSessionContext Copy()
        {
            return new FishingSessionContext
            {
                SessionId = SessionId,
                ParticipantId = ParticipantId,
                CountdownSeconds = CountdownSeconds,
                Fish = Fish?.Copy()
            };
        }

        public void Sanitize()
        {
            SessionId = string.IsNullOrWhiteSpace(SessionId) ? "local-session" : SessionId;
            ParticipantId = string.IsNullOrWhiteSpace(ParticipantId) ? "local-player" : ParticipantId;
            CountdownSeconds = FishingMath.Max(0f, CountdownSeconds);
            Fish = (Fish ?? new FishProfile()).Copy();
            Fish.Sanitize();
        }
    }

    public sealed class FishingSessionSnapshot
    {
        public FishingSessionState State { get; internal set; } = FishingSessionState.Uninitialized;
        public string SessionId { get; internal set; }
        public string ParticipantId { get; internal set; }
        public string FishId { get; internal set; }
        public string FishDisplayName { get; internal set; }
        public float CountdownRemainingSeconds { get; internal set; }
        public float ElapsedSeconds { get; internal set; }
        public int PreHookFailureCount { get; internal set; }
        public FishingSessionOutcome Outcome { get; internal set; }
    }

    public sealed class FishingSessionResult
    {
        public string SessionId;
        public string ParticipantId;
        public string FishId;
        public string FishDisplayName;
        public FishingSessionOutcome Outcome;
        public float ElapsedSeconds;
        public int PreHookFailureCount;
        public FishingCycleResult CycleResult;
    }

    /// <summary>
    /// Owns only the lifetime of one selected fish. The fishing state machine remains
    /// responsible for casts, bites, hooks and fights; this layer decides whether a
    /// finished cycle is a retry or the terminal result of the session.
    /// </summary>
    public sealed class SingleFishSessionTracker
    {
        private FishingSessionContext _context;
        private FishingSessionResult _result;
        private bool _completionRaised;

        public FishingSessionSnapshot Current { get; private set; } = new FishingSessionSnapshot();
        public FishingSessionResult Result => _result;
        public FishProfile SelectedFish => _context?.Fish;
        public event Action<FishingSessionResult> Completed;

        public void Initialize(FishingSessionContext context)
        {
            _context = (context ?? throw new ArgumentNullException(nameof(context))).Copy();
            _context.Sanitize();
            _result = null;
            _completionRaised = false;
            Current = new FishingSessionSnapshot
            {
                State = FishingSessionState.Ready,
                SessionId = _context.SessionId,
                ParticipantId = _context.ParticipantId,
                FishId = _context.Fish.FishId,
                FishDisplayName = _context.Fish.DisplayName,
                CountdownRemainingSeconds = _context.CountdownSeconds,
                Outcome = FishingSessionOutcome.None
            };
        }

        public void Begin()
        {
            EnsureInitialized();
            if (Current.State != FishingSessionState.Ready) return;
            Current.State = _context.CountdownSeconds > 0f
                ? FishingSessionState.Countdown
                : FishingSessionState.Playing;
        }

        public void Tick(float deltaTime)
        {
            EnsureInitialized();
            float dt = FishingMath.Clamp(deltaTime, 0f, 0.25f);
            if (Current.State == FishingSessionState.Countdown)
            {
                Current.CountdownRemainingSeconds = FishingMath.Max(0f, Current.CountdownRemainingSeconds - dt);
                if (Current.CountdownRemainingSeconds <= 0f) Current.State = FishingSessionState.Playing;
                return;
            }

            if (Current.State == FishingSessionState.Playing)
            {
                Current.ElapsedSeconds += dt;
            }
        }

        public void RecordPreHookFailure(FishingCycleResult cycle)
        {
            EnsureInitialized();
            if (Current.State != FishingSessionState.Playing || cycle == null || cycle.WasCaught) return;
            Current.PreHookFailureCount++;
        }

        public void CompletePostHook(FishingCycleResult cycle)
        {
            EnsureInitialized();
            if (Current.State != FishingSessionState.Playing || cycle == null || _result != null) return;

            FishingCycleResult resultCopy = CopyCycleResult(cycle);
            Current.Outcome = resultCopy.WasCaught
                ? FishingSessionOutcome.Caught
                : FishingSessionOutcome.Escaped;
            _result = new FishingSessionResult
            {
                SessionId = Current.SessionId,
                ParticipantId = Current.ParticipantId,
                FishId = Current.FishId,
                FishDisplayName = Current.FishDisplayName,
                Outcome = Current.Outcome,
                ElapsedSeconds = Current.ElapsedSeconds,
                PreHookFailureCount = Current.PreHookFailureCount,
                CycleResult = resultCopy
            };
            Current.State = FishingSessionState.Completed;
            if (_completionRaised) return;
            _completionRaised = true;
            Completed?.Invoke(_result);
        }

        public void Abort()
        {
            EnsureInitialized();
            if (Current.State == FishingSessionState.Completed || Current.State == FishingSessionState.Aborted) return;
            Current.State = FishingSessionState.Aborted;
        }

        private static FishingCycleResult CopyCycleResult(FishingCycleResult source)
        {
            return new FishingCycleResult
            {
                CycleNumber = source.CycleNumber,
                WasCaught = source.WasCaught,
                EscapeReason = source.EscapeReason,
                FishId = source.FishId,
                FishDisplayName = source.FishDisplayName,
                DifficultyLabel = source.DifficultyLabel,
                AwardedScore = source.AwardedScore,
                TotalScoreAfterCycle = source.TotalScoreAfterCycle,
                CastPower = source.CastPower,
                RemainingLineDurability = source.RemainingLineDurability
            };
        }

        private void EnsureInitialized()
        {
            if (_context == null)
            {
                throw new InvalidOperationException("SingleFishSessionTracker.Initialize must be called before use.");
            }
        }
    }
}
