using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// A compact, device-independent description of the visuals another client
    /// needs to reproduce. It intentionally excludes input, capture progress,
    /// exact tension and transforms.
    /// </summary>
    public readonly struct FishingV3NetworkPresentationState : IEquatable<FishingV3NetworkPresentationState>
    {
        public static readonly FishingV3NetworkPresentationState Inactive = new(
            0,
            false,
            false,
            FishingV3GameplayPhase.WaitingForBite,
            FishingV3Result.Active,
            FishingV3FishProfileId.Normal,
            string.Empty,
            FishingV3FishState.Calm,
            FishingV3TensionZone.Safe,
            0,
            0f);

        public int SessionSequence { get; }
        public bool IsFishing { get; }
        public bool IsPaused { get; }
        public FishingV3GameplayPhase GameplayPhase { get; }
        public FishingV3Result Result { get; }
        public FishingV3FishProfileId FishProfileId { get; }
        public string FishVisualId { get; }
        public FishingV3FishState FishState { get; }
        public FishingV3TensionZone TensionZone { get; }
        public int HeadShakeEventSequence { get; }
        public float HeadShakeIntensityNormalized { get; }

        public bool HasPresentation => IsFishing || IsTerminalResult(Result);

        public float RepresentativeTensionNormalized =>
            RepresentativeTensionFor(TensionZone);

        public FishingV3NetworkPresentationState(
            int sessionSequence,
            bool isFishing,
            bool isPaused,
            FishingV3GameplayPhase gameplayPhase,
            FishingV3Result result,
            FishingV3FishProfileId fishProfileId,
            string fishVisualId,
            FishingV3FishState fishState,
            FishingV3TensionZone tensionZone,
            int headShakeEventSequence,
            float headShakeIntensityNormalized)
        {
            SessionSequence = Math.Max(0, sessionSequence);
            IsFishing = isFishing;
            IsPaused = isFishing && isPaused;
            GameplayPhase = isFishing
                ? SanitizeGameplayPhase(gameplayPhase)
                : IsTerminalResult(result)
                    ? FishingV3GameplayPhase.Terminal
                    : FishingV3GameplayPhase.WaitingForBite;
            Result = isFishing ? FishingV3Result.Active : SanitizeResult(result);
            FishProfileId = SanitizeProfileId(fishProfileId);
            FishVisualId = NormalizeVisualId(fishVisualId);
            FishState = SanitizeFishState(fishState);
            TensionZone = SanitizeTensionZone(tensionZone);
            HeadShakeEventSequence = Math.Max(0, headShakeEventSequence);
            HeadShakeIntensityNormalized = IsFinite(headShakeIntensityNormalized)
                ? Math.Clamp(headShakeIntensityNormalized, 0f, 1f)
                : 0f;
        }

        public static FishingV3NetworkPresentationState FromSnapshot(
            int sessionSequence,
            bool isFishing,
            bool isPaused,
            FishingV3Snapshot snapshot)
        {
            if (snapshot == null)
            {
                return new FishingV3NetworkPresentationState(
                    sessionSequence,
                    false,
                    false,
                    FishingV3GameplayPhase.WaitingForBite,
                    FishingV3Result.Active,
                    FishingV3FishProfileId.Normal,
                    string.Empty,
                    FishingV3FishState.Calm,
                    FishingV3TensionZone.Safe,
                    0,
                    0f);
            }

            return new FishingV3NetworkPresentationState(
                sessionSequence,
                isFishing,
                isPaused,
                snapshot.GameplayPhase,
                snapshot.Result,
                snapshot.FishProfileId,
                snapshot.FishVisualId,
                snapshot.FishState,
                snapshot.TensionZone,
                snapshot.HeadShakeEventSequence,
                snapshot.HeadShakeIntensityNormalized);
        }

        public FishingV3NetworkPresentationState WithSessionSequence(int value)
        {
            return new FishingV3NetworkPresentationState(
                value,
                IsFishing,
                IsPaused,
                GameplayPhase,
                Result,
                FishProfileId,
                FishVisualId,
                FishState,
                TensionZone,
                HeadShakeEventSequence,
                HeadShakeIntensityNormalized);
        }

        public FishingV3RemotePresentationFrame ToRemoteFrame()
        {
            return new FishingV3RemotePresentationFrame(
                HasPresentation,
                IsFishing,
                IsPaused,
                GameplayPhase,
                Result,
                FishProfileId,
                FishVisualId,
                FishState,
                TensionZone,
                HeadShakeEventSequence,
                HeadShakeIntensityNormalized);
        }

        public bool Equals(FishingV3NetworkPresentationState other)
        {
            return SessionSequence == other.SessionSequence &&
                   IsFishing == other.IsFishing &&
                   IsPaused == other.IsPaused &&
                   GameplayPhase == other.GameplayPhase &&
                   Result == other.Result &&
                   FishProfileId == other.FishProfileId &&
                   string.Equals(FishVisualId, other.FishVisualId, StringComparison.Ordinal) &&
                   FishState == other.FishState &&
                   TensionZone == other.TensionZone &&
                   HeadShakeEventSequence == other.HeadShakeEventSequence &&
                   Math.Abs(HeadShakeIntensityNormalized - other.HeadShakeIntensityNormalized) <= 0.001f;
        }

        public override bool Equals(object obj)
        {
            return obj is FishingV3NetworkPresentationState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SessionSequence;
                hash = (hash * 397) ^ IsFishing.GetHashCode();
                hash = (hash * 397) ^ IsPaused.GetHashCode();
                hash = (hash * 397) ^ (int)GameplayPhase;
                hash = (hash * 397) ^ (int)Result;
                hash = (hash * 397) ^ (int)FishProfileId;
                hash = (hash * 397) ^ (FishVisualId != null ? FishVisualId.GetHashCode() : 0);
                hash = (hash * 397) ^ (int)FishState;
                hash = (hash * 397) ^ (int)TensionZone;
                hash = (hash * 397) ^ HeadShakeEventSequence;
                return hash;
            }
        }

        public static bool IsTerminalResult(FishingV3Result result)
        {
            return result == FishingV3Result.Caught ||
                   result == FishingV3Result.LineBroken ||
                   result == FishingV3Result.FishEscaped;
        }

        public static float RepresentativeTensionFor(FishingV3TensionZone zone)
        {
            return zone switch
            {
                FishingV3TensionZone.Slack => 0.1f,
                FishingV3TensionZone.Low => 0.25f,
                FishingV3TensionZone.High => 0.75f,
                FishingV3TensionZone.Danger => 0.95f,
                _ => 0.5f
            };
        }

        public static string NormalizeVisualId(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            return trimmed.Length <= 16 ? trimmed : trimmed.Substring(0, 16);
        }

        private static FishingV3GameplayPhase SanitizeGameplayPhase(
            FishingV3GameplayPhase value)
        {
            return value >= FishingV3GameplayPhase.WaitingForBite &&
                   value <= FishingV3GameplayPhase.Terminal
                ? value
                : FishingV3GameplayPhase.WaitingForBite;
        }

        private static FishingV3Result SanitizeResult(FishingV3Result value)
        {
            return value >= FishingV3Result.Active && value <= FishingV3Result.FishEscaped
                ? value
                : FishingV3Result.Active;
        }

        private static FishingV3FishProfileId SanitizeProfileId(FishingV3FishProfileId value)
        {
            return value >= FishingV3FishProfileId.Small &&
                   value <= FishingV3FishProfileId.Strong
                ? value
                : FishingV3FishProfileId.Normal;
        }

        private static FishingV3FishState SanitizeFishState(FishingV3FishState value)
        {
            return value >= FishingV3FishState.Calm && value <= FishingV3FishState.Run
                ? value
                : FishingV3FishState.Calm;
        }

        private static FishingV3TensionZone SanitizeTensionZone(FishingV3TensionZone value)
        {
            return value >= FishingV3TensionZone.Slack && value <= FishingV3TensionZone.Danger
                ? value
                : FishingV3TensionZone.Safe;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Runtime presentation input reconstructed from replicated semantic state.
    /// </summary>
    public readonly struct FishingV3RemotePresentationFrame
    {
        public bool HasPresentation { get; }
        public bool IsFishing { get; }
        public bool IsPaused { get; }
        public FishingV3GameplayPhase GameplayPhase { get; }
        public FishingV3Result Result { get; }
        public FishingV3FishProfileId FishProfileId { get; }
        public string FishVisualId { get; }
        public FishingV3FishState FishState { get; }
        public FishingV3TensionZone TensionZone { get; }
        public int HeadShakeEventSequence { get; }
        public float HeadShakeIntensityNormalized { get; }

        public FishingV3RemotePresentationFrame(
            bool hasPresentation,
            bool isFishing,
            bool isPaused,
            FishingV3GameplayPhase gameplayPhase,
            FishingV3Result result,
            FishingV3FishProfileId fishProfileId,
            string fishVisualId,
            FishingV3FishState fishState,
            FishingV3TensionZone tensionZone,
            int headShakeEventSequence,
            float headShakeIntensityNormalized)
        {
            HasPresentation = hasPresentation;
            IsFishing = isFishing;
            IsPaused = isFishing && isPaused;
            GameplayPhase = gameplayPhase;
            Result = result;
            FishProfileId = fishProfileId;
            FishVisualId = fishVisualId ?? string.Empty;
            FishState = fishState;
            TensionZone = tensionZone;
            HeadShakeEventSequence = Math.Max(0, headShakeEventSequence);
            HeadShakeIntensityNormalized = IsFinite(headShakeIntensityNormalized)
                ? Math.Clamp(headShakeIntensityNormalized, 0f, 1f)
                : 0f;
        }

        internal FishingV3Snapshot ToSnapshot()
        {
            if (!HasPresentation) return null;

            bool terminal = FishingV3NetworkPresentationState.IsTerminalResult(Result);
            return new FishingV3Snapshot
            {
                RuntimeState = terminal
                    ? FishingV3RuntimeState.Completed
                    : IsPaused
                        ? FishingV3RuntimeState.Paused
                        : FishingV3RuntimeState.Running,
                SessionFlowMode = FishingV3SessionFlowMode.BiteHook,
                GameplayPhase = terminal
                    ? FishingV3GameplayPhase.Terminal
                    : GameplayPhase,
                FishState = FishState,
                TensionNormalized =
                    FishingV3NetworkPresentationState.RepresentativeTensionFor(TensionZone),
                TensionZone = TensionZone,
                Result = terminal ? Result : FishingV3Result.Active,
                HeadShakeEventSequence = HeadShakeEventSequence,
                HeadShakeIntensityNormalized = HeadShakeIntensityNormalized,
                FishProfileId = FishProfileId,
                FishVisualId = FishVisualId
            };
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
