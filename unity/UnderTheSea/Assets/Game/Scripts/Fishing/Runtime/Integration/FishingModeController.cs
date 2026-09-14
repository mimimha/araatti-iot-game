using System;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    public enum FishingModeLifecycleState
    {
        Inactive,
        Active,
        Paused
    }

    /// <summary>
    /// Orchestrates entry to and exit from the V3 fishing runtime. World input,
    /// player control, gameplay ticks, presentation, and resistance remain owned
    /// by their existing layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingModeController : MonoBehaviour
    {
        [SerializeField] private FishingMiniGameFacade facade;
        [SerializeField] private FishingSpot fishingSpot;
        [SerializeField] private FishingV3FishState initialFishState = FishingV3FishState.Fight;

        private FishingSpot _subscribedSpot;
        private FishingModeLifecycleState _state;
        private FishingSpot _currentSpot;
        private GameObject _currentInteractor;
        private FishingV3Result _lastResult = FishingV3Result.Active;
        private bool _hasLastResult;

        public FishingModeLifecycleState State => _state;

        public bool IsFishing => _state != FishingModeLifecycleState.Inactive;

        public bool IsPaused => _state == FishingModeLifecycleState.Paused;

        public FishingSpot CurrentSpot => _currentSpot;

        public GameObject CurrentInteractor => _currentInteractor;

        public bool HasLastResult => _hasLastResult;

        public FishingV3Result LastResult => _lastResult;

        private void Awake()
        {
            ResolveFacade();
        }

        private void OnEnable()
        {
            ResolveFacade();
            Subscribe(fishingSpot);
        }

        private void Update()
        {
            ObserveTerminalResult();
        }

        private void OnDisable()
        {
            Unsubscribe();
            CleanupActiveSession(FishingAbortReason.IntegrationShutdown);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            CleanupActiveSession(FishingAbortReason.IntegrationShutdown);
        }

        /// <summary>
        /// Selects the one spot observed by this controller. Rebinding is symmetric
        /// and does not change an already active session's stored spot context.
        /// </summary>
        public void Bind(FishingSpot spot)
        {
            if (ReferenceEquals(fishingSpot, spot))
            {
                if (isActiveAndEnabled) Subscribe(spot);
                return;
            }

            Unsubscribe();
            fishingSpot = spot;
            if (isActiveAndEnabled) Subscribe(fishingSpot);
        }

        public void Unbind()
        {
            Unsubscribe();
            fishingSpot = null;
        }

        public bool RequestPause()
        {
            if (_state != FishingModeLifecycleState.Active || facade == null)
            {
                return false;
            }

            facade.SetPaused(true);
            if (facade.V3Current == null ||
                facade.V3Current.RuntimeState != FishingV3RuntimeState.Paused)
            {
                return false;
            }

            _state = FishingModeLifecycleState.Paused;
            return true;
        }

        public bool RequestResume()
        {
            if (_state != FishingModeLifecycleState.Paused || facade == null)
            {
                return false;
            }

            facade.SetPaused(false);
            if (facade.V3Current == null ||
                facade.V3Current.RuntimeState != FishingV3RuntimeState.Running)
            {
                return false;
            }

            _state = FishingModeLifecycleState.Active;
            return true;
        }

        public bool Abort()
        {
            if (_state == FishingModeLifecycleState.Inactive)
            {
                return false;
            }

            CleanupActiveSession(FishingAbortReason.UserRequested);
            return true;
        }

        private void ResolveFacade()
        {
            if (facade == null) facade = GetComponent<FishingMiniGameFacade>();
        }

        private void Subscribe(FishingSpot spot)
        {
            if (spot == null || ReferenceEquals(_subscribedSpot, spot)) return;

            Unsubscribe();
            _subscribedSpot = spot;
            _subscribedSpot.FishingRequested += OnFishingRequested;
        }

        private void Unsubscribe()
        {
            if (_subscribedSpot == null) return;

            _subscribedSpot.FishingRequested -= OnFishingRequested;
            _subscribedSpot = null;
        }

        private void OnFishingRequested(FishingSpotInteractionRequest request)
        {
            if (request.Spot == null || request.Interactor == null)
            {
                request.Spot?.Release();
                return;
            }

            if (_state != FishingModeLifecycleState.Inactive)
            {
                if (!ReferenceEquals(request.Spot, _currentSpot))
                {
                    request.Spot.Release();
                }

                return;
            }

            BeginSession(request);
        }

        private void BeginSession(FishingSpotInteractionRequest request)
        {
            ResolveFacade();
            _currentSpot = request.Spot;
            _currentInteractor = request.Interactor;
            _hasLastResult = false;
            _lastResult = FishingV3Result.Active;

            if (facade == null)
            {
                ClearContextAndRelease();
                return;
            }

            try
            {
                facade.ConfigureV3Runtime();
                facade.BeginRound();
                facade.SetV3FishState(initialFishState);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                FailBegin();
                return;
            }

            FishingV3Snapshot snapshot = facade.V3Current;
            if (facade.GameplayRuntimeMode != FishingGameplayRuntimeMode.V3 ||
                snapshot == null ||
                snapshot.RuntimeState != FishingV3RuntimeState.Running ||
                snapshot.Result != FishingV3Result.Active ||
                snapshot.FishState != initialFishState)
            {
                FailBegin();
                return;
            }

            _state = FishingModeLifecycleState.Active;
        }

        private void FailBegin()
        {
            if (facade != null &&
                facade.GameplayRuntimeMode == FishingGameplayRuntimeMode.V3)
            {
                facade.Abort(FishingAbortReason.RuntimeError);
            }

            ClearContextAndRelease();
        }

        private void ObserveTerminalResult()
        {
            if (_state == FishingModeLifecycleState.Inactive ||
                !TryReadTerminalResult(out FishingV3Result result))
            {
                return;
            }

            _lastResult = result;
            _hasLastResult = true;
            ClearContextAndRelease();
        }

        private void CleanupActiveSession(FishingAbortReason abortReason)
        {
            if (_state == FishingModeLifecycleState.Inactive) return;

            if (TryReadTerminalResult(out FishingV3Result result))
            {
                _lastResult = result;
                _hasLastResult = true;
            }
            else if (facade != null)
            {
                facade.Abort(abortReason);
            }

            ClearContextAndRelease();
        }

        private bool TryReadTerminalResult(out FishingV3Result result)
        {
            FishingV3Snapshot snapshot = facade != null ? facade.V3Current : null;
            result = snapshot != null ? snapshot.Result : FishingV3Result.Active;
            return result == FishingV3Result.Caught ||
                   result == FishingV3Result.LineBroken ||
                   result == FishingV3Result.FishEscaped;
        }

        private void ClearContextAndRelease()
        {
            FishingSpot spot = _currentSpot;
            _state = FishingModeLifecycleState.Inactive;
            _currentSpot = null;
            _currentInteractor = null;
            spot?.Release();
        }
    }
}
