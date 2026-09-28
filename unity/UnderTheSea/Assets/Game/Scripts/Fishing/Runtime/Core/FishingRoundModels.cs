using System;
using System.Collections.Generic;

namespace FishingMiniGame.Core
{
    public enum FishingAbortReason
    {
        UserRequested,
        IntegrationShutdown,
        RuntimeError
    }

    public sealed class FishingRuntimeError
    {
        public string Code;
        public string Message;
    }

    public sealed class FishingLaunchContext
    {
        public string RoundId = "local-round";
        public string LocalParticipantId = "local-player";
        public float RoundDurationSeconds = 180f;
        public float CountdownSeconds = 3f;
        public int Seed = 20260821;

        public FishingLaunchContext Copy()
        {
            return (FishingLaunchContext)MemberwiseClone();
        }

        public void Sanitize()
        {
            RoundId = string.IsNullOrWhiteSpace(RoundId) ? "local-round" : RoundId;
            LocalParticipantId = string.IsNullOrWhiteSpace(LocalParticipantId) ? "local-player" : LocalParticipantId;
            RoundDurationSeconds = FishingMath.Max(0.1f, RoundDurationSeconds);
            CountdownSeconds = FishingMath.Max(0f, CountdownSeconds);
        }
    }

    public sealed class FishingCatchRecord
    {
        public int CycleNumber;
        public string FishId;
        public string FishDisplayName;
        public string DifficultyLabel;
        public int Score;
        public float CaughtAtSeconds;
        public float CastPower;
        public float RemainingLineDurability;
    }

    public sealed class FishingRoundResult
    {
        public string RoundId;
        public string ParticipantId;
        public FishingRoundEndReason EndReason;
        public bool CompletedNormally;
        public float RoundDurationSeconds;
        public float ElapsedSeconds;
        public int TotalScore;
        public int Attempts;
        public int CaughtCount;
        public FishingCatchRecord[] Catches = Array.Empty<FishingCatchRecord>();
    }

    public sealed class FishingRoundSnapshot
    {
        public FishingRoundState State { get; internal set; } = FishingRoundState.Uninitialized;
        public float DurationSeconds { get; internal set; }
        public float RemainingSeconds { get; internal set; }
        public float ElapsedSeconds { get; internal set; }
        public float CountdownRemainingSeconds { get; internal set; }
        public int TotalScore { get; internal set; }
        public int Attempts { get; internal set; }
        public int CaughtCount { get; internal set; }
    }

    public sealed class FishingRoundTracker
    {
        private FishingLaunchContext _context;
        private readonly List<FishingCatchRecord> _catches = new List<FishingCatchRecord>();
        private FishingRoundResult _result;
        private bool _completionRaised;

        public FishingRoundSnapshot Current { get; private set; } = new FishingRoundSnapshot();
        public FishingRoundResult Result => _result;
        public IReadOnlyList<FishingCatchRecord> Catches => _catches;
        public event Action<FishingRoundResult> Completed;

        public void Initialize(FishingLaunchContext context)
        {
            _context = (context ?? new FishingLaunchContext()).Copy();
            _context.Sanitize();
            _catches.Clear();
            _result = null;
            _completionRaised = false;
            Current = new FishingRoundSnapshot
            {
                State = FishingRoundState.Ready,
                DurationSeconds = _context.RoundDurationSeconds,
                RemainingSeconds = _context.RoundDurationSeconds,
                CountdownRemainingSeconds = _context.CountdownSeconds
            };
        }

        public void Begin()
        {
            EnsureInitialized();
            if (Current.State != FishingRoundState.Ready) return;
            Current.State = _context.CountdownSeconds > 0f
                ? FishingRoundState.Countdown
                : FishingRoundState.Playing;
        }

        public void Tick(float deltaTime)
        {
            EnsureInitialized();
            float dt = FishingMath.Clamp(deltaTime, 0f, 0.25f);
            if (Current.State == FishingRoundState.Countdown)
            {
                Current.CountdownRemainingSeconds = FishingMath.Max(0f, Current.CountdownRemainingSeconds - dt);
                if (Current.CountdownRemainingSeconds <= 0f)
                {
                    Current.State = FishingRoundState.Playing;
                }
                return;
            }

            if (Current.State != FishingRoundState.Playing) return;
            Current.ElapsedSeconds = FishingMath.Min(Current.DurationSeconds, Current.ElapsedSeconds + dt);
            Current.RemainingSeconds = FishingMath.Max(0f, Current.DurationSeconds - Current.ElapsedSeconds);
            if (Current.RemainingSeconds <= 0f)
            {
                Finish(FishingRoundEndReason.TimeExpired, true, FishingRoundState.Completed);
            }
        }

        public void RecordCycle(FishingCycleResult cycle)
        {
            EnsureInitialized();
            if (cycle == null || Current.State != FishingRoundState.Playing) return;
            Current.Attempts++;
            Current.TotalScore = cycle.TotalScoreAfterCycle;
            if (!cycle.WasCaught) return;

            Current.CaughtCount++;
            _catches.Add(new FishingCatchRecord
            {
                CycleNumber = cycle.CycleNumber,
                FishId = cycle.FishId,
                FishDisplayName = cycle.FishDisplayName,
                DifficultyLabel = cycle.DifficultyLabel,
                Score = cycle.AwardedScore,
                CaughtAtSeconds = Current.ElapsedSeconds,
                CastPower = cycle.CastPower,
                RemainingLineDurability = cycle.RemainingLineDurability
            });
        }

        public void Stop()
        {
            EnsureInitialized();
            Finish(FishingRoundEndReason.ManualStop, true, FishingRoundState.Completed);
        }

        public void Abort()
        {
            EnsureInitialized();
            Finish(FishingRoundEndReason.Aborted, false, FishingRoundState.Aborted);
        }

        private void Finish(FishingRoundEndReason reason, bool completedNormally, FishingRoundState finalState)
        {
            if (_result != null) return;
            Current.State = FishingRoundState.Finishing;
            _result = new FishingRoundResult
            {
                RoundId = _context.RoundId,
                ParticipantId = _context.LocalParticipantId,
                EndReason = reason,
                CompletedNormally = completedNormally,
                RoundDurationSeconds = Current.DurationSeconds,
                ElapsedSeconds = Current.ElapsedSeconds,
                TotalScore = Current.TotalScore,
                Attempts = Current.Attempts,
                CaughtCount = Current.CaughtCount,
                Catches = _catches.ToArray()
            };
            Current.State = finalState;
            if (_completionRaised) return;
            _completionRaised = true;
            Completed?.Invoke(_result);
        }

        private void EnsureInitialized()
        {
            if (_context == null)
            {
                throw new InvalidOperationException("FishingRoundTracker.Initialize must be called before use.");
            }
        }
    }
}
