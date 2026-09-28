using System;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    public enum FishingV3AudioCue
    {
        None,
        Bite,
        HookSuccess,
        Perfect,
        Good,
        Miss,
        Caught,
        LineBroken,
        FishEscaped
    }

    /// <summary>
    /// Device-independent snapshot subset consumed by the fishing SFX presenter.
    /// Keeping this frame separate makes event detection deterministic and testable.
    /// </summary>
    public readonly struct FishingV3AudioFrame
    {
        public FishingV3RuntimeState RuntimeState { get; }
        public FishingV3GameplayPhase GameplayPhase { get; }
        public FishingV3Result Result { get; }
        public FishingV3TimingGrade TimingGrade { get; }
        public int TimingJudgementSequence { get; }

        public FishingV3AudioFrame(
            FishingV3RuntimeState runtimeState,
            FishingV3GameplayPhase gameplayPhase,
            FishingV3Result result,
            FishingV3TimingGrade timingGrade,
            int timingJudgementSequence)
        {
            RuntimeState = runtimeState;
            GameplayPhase = gameplayPhase;
            Result = result;
            TimingGrade = timingGrade;
            TimingJudgementSequence = Math.Max(0, timingJudgementSequence);
        }

        public static FishingV3AudioFrame FromSnapshot(FishingV3Snapshot snapshot)
        {
            return new FishingV3AudioFrame(
                snapshot.RuntimeState,
                snapshot.GameplayPhase,
                snapshot.Result,
                snapshot.LastTimingGrade,
                snapshot.TimingJudgementSequence);
        }
    }

    /// <summary>
    /// Read-only Fishing V3 audio presentation. It observes facade snapshots and
    /// translates phase, judgement, and terminal transitions into one-shot cues.
    /// It never samples input or changes gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingV3AudioPresenter : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private FishingMiniGameFacade facade;

        [Header("Fishing SFX Clips")]
        [SerializeField] private AudioClip biteClip;
        [SerializeField] private AudioClip hookSuccessClip;
        [SerializeField] private AudioClip perfectClip;
        [SerializeField] private AudioClip goodClip;
        [SerializeField] private AudioClip missClip;
        [SerializeField] private AudioClip caughtClip;
        [SerializeField] private AudioClip lineBrokenClip;
        [SerializeField] private AudioClip fishEscapedClip;

        [Header("Fishing SFX Volume")]
        [Range(0f, 1f)] [SerializeField] private float biteVolume = 0.70f;
        [Range(0f, 1f)] [SerializeField] private float hookSuccessVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float perfectVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float goodVolume = 0.60f;
        [Range(0f, 1f)] [SerializeField] private float missVolume = 0.50f;
        [Range(0f, 1f)] [SerializeField] private float caughtVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float lineBrokenVolume = 0.80f;
        [Range(0f, 1f)] [SerializeField] private float fishEscapedVolume = 0.75f;

        private bool _hasObservation;
        private FishingV3RuntimeState _lastRuntimeState;
        private FishingV3GameplayPhase _lastGameplayPhase;
        private FishingV3Result _lastResult;
        private int _lastTimingJudgementSequence;

        public bool HasFacade => facade != null;
        public FishingV3AudioCue LastRequestedCue { get; private set; }
        public int CueRequestSequence { get; private set; }
        public int AssignedClipCount =>
            CountAssigned(biteClip) +
            CountAssigned(hookSuccessClip) +
            CountAssigned(perfectClip) +
            CountAssigned(goodClip) +
            CountAssigned(missClip) +
            CountAssigned(caughtClip) +
            CountAssigned(lineBrokenClip) +
            CountAssigned(fishEscapedClip);

        public event Action<FishingV3AudioCue> CueRequested;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResetObservation();
        }

        private void Update()
        {
            RefreshNow();
        }

        private void OnDisable()
        {
            ResetObservation();
        }

        public void Configure(FishingMiniGameFacade sourceFacade)
        {
            facade = sourceFacade;
            ResetObservation();
        }

        public void ConfigureClips(
            AudioClip bite,
            AudioClip hookSuccess,
            AudioClip perfect,
            AudioClip good,
            AudioClip miss,
            AudioClip caught,
            AudioClip lineBroken,
            AudioClip fishEscaped)
        {
            biteClip = bite;
            hookSuccessClip = hookSuccess;
            perfectClip = perfect;
            goodClip = good;
            missClip = miss;
            caughtClip = caught;
            lineBrokenClip = lineBroken;
            fishEscapedClip = fishEscaped;
        }

        public void RefreshNow()
        {
            ResolveReferences();
            if (facade == null ||
                facade.GameplayRuntimeMode != FishingGameplayRuntimeMode.V3 ||
                facade.V3Current == null)
            {
                ResetObservation();
                return;
            }

            StepPresentation(FishingV3AudioFrame.FromSnapshot(facade.V3Current));
        }

        public void StepPresentation(FishingV3AudioFrame frame)
        {
            if (!_hasObservation)
            {
                Synchronize(frame);
                return;
            }

            if (IsTrackingReset(frame))
            {
                Synchronize(frame);
                return;
            }

            if (frame.RuntimeState == FishingV3RuntimeState.Ready ||
                frame.RuntimeState == FishingV3RuntimeState.Aborted ||
                frame.RuntimeState == FishingV3RuntimeState.Shutdown)
            {
                Synchronize(frame);
                return;
            }

            bool active = frame.RuntimeState == FishingV3RuntimeState.Running ||
                          frame.RuntimeState == FishingV3RuntimeState.Paused;
            if (active && frame.Result == FishingV3Result.Active)
            {
                if (_lastGameplayPhase != FishingV3GameplayPhase.HookWindow &&
                    frame.GameplayPhase == FishingV3GameplayPhase.HookWindow)
                {
                    RequestCue(FishingV3AudioCue.Bite);
                }

                if (_lastGameplayPhase == FishingV3GameplayPhase.HookWindow &&
                    frame.GameplayPhase == FishingV3GameplayPhase.Fighting)
                {
                    RequestCue(FishingV3AudioCue.HookSuccess);
                }
            }

            ConsumeTimingJudgement(frame);

            if (frame.RuntimeState == FishingV3RuntimeState.Completed &&
                IsTerminalResult(frame.Result) &&
                (_lastRuntimeState != FishingV3RuntimeState.Completed ||
                 _lastResult != frame.Result))
            {
                RequestCue(ToTerminalCue(frame.Result));
            }

            Synchronize(frame);
        }

        public float GetConfiguredVolume(FishingV3AudioCue cue)
        {
            return SanitizeVolume(GetVolume(cue));
        }

        public AudioClip GetConfiguredClip(FishingV3AudioCue cue)
        {
            return GetClip(cue);
        }

        private void ResolveReferences()
        {
            if (facade == null)
            {
                facade = GetComponent<FishingMiniGameFacade>();
            }

        }

        private bool IsTrackingReset(FishingV3AudioFrame frame)
        {
            if (frame.TimingJudgementSequence < _lastTimingJudgementSequence)
            {
                return true;
            }

            bool currentIsActive = frame.RuntimeState == FishingV3RuntimeState.Running ||
                                   frame.RuntimeState == FishingV3RuntimeState.Paused;
            bool previousWasTerminal =
                _lastRuntimeState == FishingV3RuntimeState.Completed ||
                _lastRuntimeState == FishingV3RuntimeState.Aborted ||
                _lastRuntimeState == FishingV3RuntimeState.Shutdown;
            if (currentIsActive && previousWasTerminal)
            {
                return true;
            }

            return currentIsActive &&
                   frame.GameplayPhase == FishingV3GameplayPhase.WaitingForBite &&
                   _lastGameplayPhase != FishingV3GameplayPhase.WaitingForBite;
        }

        private void ConsumeTimingJudgement(FishingV3AudioFrame frame)
        {
            if (frame.TimingJudgementSequence <= _lastTimingJudgementSequence)
            {
                return;
            }

            switch (frame.TimingGrade)
            {
                case FishingV3TimingGrade.Perfect:
                    RequestCue(FishingV3AudioCue.Perfect);
                    break;
                case FishingV3TimingGrade.Good:
                    RequestCue(FishingV3AudioCue.Good);
                    break;
                case FishingV3TimingGrade.Miss:
                    RequestCue(FishingV3AudioCue.Miss);
                    break;
            }
        }

        private void RequestCue(FishingV3AudioCue cue)
        {
            if (cue == FishingV3AudioCue.None) return;

            LastRequestedCue = cue;
            CueRequestSequence++;
            CueRequested?.Invoke(cue);
        }

        private void Synchronize(FishingV3AudioFrame frame)
        {
            _hasObservation = true;
            _lastRuntimeState = frame.RuntimeState;
            _lastGameplayPhase = frame.GameplayPhase;
            _lastResult = frame.Result;
            _lastTimingJudgementSequence = frame.TimingJudgementSequence;
        }

        private void ResetObservation()
        {
            _hasObservation = false;
            _lastRuntimeState = FishingV3RuntimeState.Ready;
            _lastGameplayPhase = FishingV3GameplayPhase.WaitingForBite;
            _lastResult = FishingV3Result.Active;
            _lastTimingJudgementSequence = 0;
        }

        private AudioClip GetClip(FishingV3AudioCue cue)
        {
            switch (cue)
            {
                case FishingV3AudioCue.Bite: return biteClip;
                case FishingV3AudioCue.HookSuccess: return hookSuccessClip;
                case FishingV3AudioCue.Perfect: return perfectClip;
                case FishingV3AudioCue.Good: return goodClip;
                case FishingV3AudioCue.Miss: return missClip;
                case FishingV3AudioCue.Caught: return caughtClip;
                case FishingV3AudioCue.LineBroken: return lineBrokenClip;
                case FishingV3AudioCue.FishEscaped: return fishEscapedClip;
                default: return null;
            }
        }

        private float GetVolume(FishingV3AudioCue cue)
        {
            switch (cue)
            {
                case FishingV3AudioCue.Bite: return biteVolume;
                case FishingV3AudioCue.HookSuccess: return hookSuccessVolume;
                case FishingV3AudioCue.Perfect: return perfectVolume;
                case FishingV3AudioCue.Good: return goodVolume;
                case FishingV3AudioCue.Miss: return missVolume;
                case FishingV3AudioCue.Caught: return caughtVolume;
                case FishingV3AudioCue.LineBroken: return lineBrokenVolume;
                case FishingV3AudioCue.FishEscaped: return fishEscapedVolume;
                default: return 0f;
            }
        }

        private static bool IsTerminalResult(FishingV3Result result)
        {
            return result == FishingV3Result.Caught ||
                   result == FishingV3Result.LineBroken ||
                   result == FishingV3Result.FishEscaped;
        }

        private static FishingV3AudioCue ToTerminalCue(FishingV3Result result)
        {
            switch (result)
            {
                case FishingV3Result.Caught: return FishingV3AudioCue.Caught;
                case FishingV3Result.LineBroken: return FishingV3AudioCue.LineBroken;
                case FishingV3Result.FishEscaped: return FishingV3AudioCue.FishEscaped;
                default: return FishingV3AudioCue.None;
            }
        }

        private static float SanitizeVolume(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);
        }

        private static int CountAssigned(AudioClip clip)
        {
            return clip != null ? 1 : 0;
        }
    }
}
