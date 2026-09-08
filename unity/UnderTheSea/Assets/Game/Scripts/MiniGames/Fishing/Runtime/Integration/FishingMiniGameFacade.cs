using System;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [RequireComponent(typeof(FishingGameController))]
    [DisallowMultipleComponent]
    public sealed class FishingMiniGameFacade : MonoBehaviour
    {
        [SerializeField] private FishingGameController controller;
        private bool _initialized;
        private bool _failedRaised;

        public FishingSnapshot Current => controller != null ? controller.Snapshot : null;
        public FishingRoundSnapshot Round => controller != null ? controller.RoundSnapshot : null;
        public FishingSessionSnapshot Session => controller != null ? controller.SessionSnapshot : null;
        public FishingSessionResult LastSessionResult => controller != null ? controller.LastSessionResult : null;
        public FishingGameMode Mode => controller != null ? controller.Mode : FishingGameMode.LegacyRound;
        public event Action<FishingRoundResult> Completed;
        public event Action<FishingSessionResult> SessionCompleted;
        public event Action<FishingRuntimeError> Failed;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<FishingGameController>();
            controller.RoundFinished += OnRoundFinished;
            controller.SessionFinished += OnSessionFinished;
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.RoundFinished -= OnRoundFinished;
                controller.SessionFinished -= OnSessionFinished;
            }
        }

        public void ConfigureFlowMode(FishingGameMode mode, FishProfile sessionFish = null)
        {
            controller.ConfigureFlowMode(mode, sessionFish);
        }

        public void Initialize(FishingLaunchContext context)
        {
            ExecuteSafely("initialize_failed", () =>
            {
                controller.ConfigureLaunchContext(context);
                _initialized = true;
                _failedRaised = false;
            });
        }

        public void BeginRound()
        {
            ExecuteSafely("begin_failed", () =>
            {
                if (!_initialized)
                {
                    Initialize(controller.Config != null
                        ? controller.Config.BuildLaunchContext()
                        : new FishingLaunchContext());
                }
                controller.BeginRound();
            });
        }

        public void BeginCycle()
        {
            controller.ResetCycle();
        }

        public void SetPaused(bool paused)
        {
            controller.SetPaused(paused);
        }

        public void Abort(FishingAbortReason reason)
        {
            if (controller == null) return;
            controller.AbortRound();
        }

        public void Shutdown()
        {
            if (controller != null && controller.Mode == FishingGameMode.SingleFishSession &&
                controller.SessionSnapshot != null &&
                (controller.SessionSnapshot.State == FishingSessionState.Playing ||
                 controller.SessionSnapshot.State == FishingSessionState.Countdown))
            {
                controller.AbortRound();
            }
            else if (controller != null && controller.RoundSnapshot != null &&
                     (controller.RoundSnapshot.State == FishingRoundState.Playing ||
                      controller.RoundSnapshot.State == FishingRoundState.Countdown))
            {
                controller.AbortRound();
            }
            if (controller != null) controller.SetPaused(true);
        }

        private void OnRoundFinished(FishingRoundResult result)
        {
            Completed?.Invoke(result);
        }

        private void OnSessionFinished(FishingSessionResult result)
        {
            SessionCompleted?.Invoke(result);
        }

        private void ExecuteSafely(string code, Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                if (_failedRaised) return;
                _failedRaised = true;
                Failed?.Invoke(new FishingRuntimeError
                {
                    Code = code,
                    Message = exception.Message
                });
                Debug.LogException(exception, this);
            }
        }
    }
}
