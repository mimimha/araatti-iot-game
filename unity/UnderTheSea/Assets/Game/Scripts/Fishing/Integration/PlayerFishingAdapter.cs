using Fusion;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Connects the local Lobby player and the project-wide Interact action to the
    /// public fishing world contracts. Gameplay, results, HUD, and feedback remain
    /// owned by their existing fishing layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFishingAdapter : MonoBehaviour
    {
        private const string InteractActionPath = "Player/Interact";

        [SerializeField] private FishingModeController fishingModeController;
        [SerializeField] private FishingV3FishVisualPresenter fishVisualPresenter;
        [SerializeField] private FishingV3PlayerPresentation playerPresentation;
        [SerializeField] private FishingSpot[] fishingSpots = new FishingSpot[0];
        [SerializeField, Min(0f)] private float interactionDistance = 2.5f;
        [SerializeField] private bool ignoreHeight = true;

        private NetworkObject _localPlayer;
        private GameObject _localPlayerGameObject;
        private PlayerInputProvider _inputProvider;
        private PlayerInputProvider _lockedProvider;
        private InputAction _interactAction;
        private FishingSpot _currentFishingSpot;
        private bool _interactSubscribed;
        private bool _localPlayerSubscribed;
        private bool _handlingInteract;
        private bool _ownsMovementLock;
        private bool _movementLockBeforeFishing;
        private bool _tearingDown;

        public FishingSpot CurrentFishingSpot => _currentFishingSpot;

        public GameObject LocalPlayerGameObject => _localPlayerGameObject;

        public bool IsInteractSubscribed => _interactSubscribed;

        public bool OwnsMovementLock => _ownsMovementLock;

        private void Awake()
        {
            ResolveModeController();
            ResolveFishVisualPresenter();
            ResolvePlayerPresentation();
        }

        private void OnEnable()
        {
            _tearingDown = false;
            ResolveModeController();
            ResolveFishVisualPresenter();
            ResolvePlayerPresentation();
            SubscribeLocalPlayer();
            SubscribeInteractAction();

            if (LocalPlayer.Object != null)
            {
                BindLocalPlayer(LocalPlayer.Object);
            }

            RefreshCurrentSpot();
            SynchronizeMovementLock();
        }

        private void Update()
        {
            if (_localPlayer == null && LocalPlayer.Object != null)
            {
                BindLocalPlayer(LocalPlayer.Object);
            }

            if (_localPlayer != null && _inputProvider == null)
            {
                TryBindInputProvider();
            }

            RefreshCurrentSpot();
            SynchronizeMovementLock();
        }

        private void OnDisable()
        {
            Teardown();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void ResolveModeController()
        {
            if (fishingModeController == null)
            {
                fishingModeController = GetComponent<FishingModeController>();
            }
        }

        private void ResolveFishVisualPresenter()
        {
            if (fishVisualPresenter == null)
            {
                fishVisualPresenter = GetComponent<FishingV3FishVisualPresenter>();
            }
        }

        private void ResolvePlayerPresentation()
        {
            if (playerPresentation == null)
            {
                playerPresentation = GetComponent<FishingV3PlayerPresentation>();
            }

            if (playerPresentation == null)
            {
                playerPresentation = gameObject.AddComponent<FishingV3PlayerPresentation>();
            }

            playerPresentation.Configure(
                GetComponent<FishingMiniGameFacade>(),
                fishingModeController,
                fishVisualPresenter);
        }

        private void SubscribeLocalPlayer()
        {
            if (_localPlayerSubscribed)
            {
                return;
            }

            LocalPlayer.Registered += BindLocalPlayer;
            LocalPlayer.Unregistered += HandleLocalPlayerUnregistered;
            _localPlayerSubscribed = true;
        }

        private void UnsubscribeLocalPlayer()
        {
            if (!_localPlayerSubscribed)
            {
                return;
            }

            LocalPlayer.Registered -= BindLocalPlayer;
            LocalPlayer.Unregistered -= HandleLocalPlayerUnregistered;
            _localPlayerSubscribed = false;
        }

        private void SubscribeInteractAction()
        {
            if (_interactSubscribed)
            {
                return;
            }

            InputActionAsset actions = InputSystem.actions;
            _interactAction = actions != null
                ? actions.FindAction(InteractActionPath, false)
                : null;

            if (_interactAction == null)
            {
                Debug.LogError(
                    $"[PlayerFishingAdapter] Project-wide action '{InteractActionPath}'을 찾지 못했습니다.",
                    this);
                return;
            }

            _interactAction.performed += OnInteractPerformed;
            _interactSubscribed = true;
        }

        private void UnsubscribeInteractAction()
        {
            if (_interactSubscribed && _interactAction != null)
            {
                _interactAction.performed -= OnInteractPerformed;
            }

            _interactAction = null;
            _interactSubscribed = false;
        }

        private void BindLocalPlayer(NetworkObject player)
        {
            if (player == null || !player.HasInputAuthority)
            {
                return;
            }

            if (_localPlayer != null && _localPlayer != player)
            {
                AbortOwnSessionIfNecessary();
                RestoreMovementLock();
            }

            _localPlayer = player;
            _localPlayerGameObject = player.gameObject;
            ResolveFishVisualPresenter();
            ResolvePlayerPresentation();
            fishVisualPresenter?.ConfigureCaughtPresentation(
                _localPlayerGameObject.transform);
            playerPresentation?.ConfigureLocalPlayer(
                _localPlayerGameObject.transform,
                fishVisualPresenter != null
                    ? fishVisualPresenter.PresentationAnchor
                    : null);
            _inputProvider = null;
            TryBindInputProvider();
        }

        private void TryBindInputProvider()
        {
            NetworkRunner runner = _localPlayer != null ? _localPlayer.Runner : null;
            _inputProvider = runner != null ? runner.GetComponent<PlayerInputProvider>() : null;
        }

        private void HandleLocalPlayerUnregistered()
        {
            AbortOwnSessionIfNecessary();
            RestoreMovementLock();
            ClearLocalPlayerBinding();
            _currentFishingSpot = null;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            TryInteractCurrentSpot();
        }

        private bool TryInteractCurrentSpot()
        {
            ResolveModeController();

            if (_handlingInteract ||
                _localPlayerGameObject == null ||
                _inputProvider == null ||
                _currentFishingSpot == null ||
                !_currentFishingSpot.CanInteract ||
                fishingModeController == null ||
                !fishingModeController.isActiveAndEnabled ||
                fishingModeController.State != FishingModeLifecycleState.Inactive)
            {
                return false;
            }

            _handlingInteract = true;
            try
            {
                fishingModeController.Bind(_currentFishingSpot);
                bool requested = _currentFishingSpot.TryInteract(_localPlayerGameObject);
                SynchronizeMovementLock();
                return requested;
            }
            finally
            {
                _handlingInteract = false;
            }
        }

        private void RefreshCurrentSpot()
        {
            if (_localPlayerGameObject == null ||
                fishingModeController == null ||
                fishingModeController.State != FishingModeLifecycleState.Inactive)
            {
                _currentFishingSpot = null;
                return;
            }

            float maximumDistanceSquared = interactionDistance * interactionDistance;
            float nearestDistanceSquared = float.PositiveInfinity;
            FishingSpot nearest = null;

            if (fishingSpots != null)
            {
                Vector3 playerPosition = _localPlayerGameObject.transform.position;

                for (int index = 0; index < fishingSpots.Length; index++)
                {
                    FishingSpot candidate = fishingSpots[index];
                    if (candidate == null || !candidate.CanInteract)
                    {
                        continue;
                    }

                    Vector3 delta = playerPosition - candidate.transform.position;
                    if (ignoreHeight)
                    {
                        delta.y = 0f;
                    }

                    float distanceSquared = delta.sqrMagnitude;
                    if (distanceSquared > maximumDistanceSquared ||
                        distanceSquared >= nearestDistanceSquared)
                    {
                        continue;
                    }

                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            _currentFishingSpot = nearest;
        }

        private void SynchronizeMovementLock()
        {
            bool ownsSession = IsOwnFishingSession();
            if (!ownsSession)
            {
                RestoreMovementLock();
                return;
            }

            if (_inputProvider == null)
            {
                TryBindInputProvider();
            }

            if (_inputProvider == null)
            {
                return;
            }

            if (!_ownsMovementLock)
            {
                _lockedProvider = _inputProvider;
                _movementLockBeforeFishing = _lockedProvider.IsMovementLocked;
                _ownsMovementLock = true;
            }

            _lockedProvider.SetMovementLocked(true);
        }

        private bool IsOwnFishingSession()
        {
            if (fishingModeController == null || _localPlayerGameObject == null)
            {
                return false;
            }

            FishingModeLifecycleState state = fishingModeController.State;
            return (state == FishingModeLifecycleState.Active ||
                    state == FishingModeLifecycleState.Paused) &&
                   fishingModeController.CurrentInteractor == _localPlayerGameObject;
        }

        private void AbortOwnSessionIfNecessary()
        {
            if (IsOwnFishingSession())
            {
                fishingModeController.Abort();
            }
        }

        private void RestoreMovementLock()
        {
            if (!_ownsMovementLock)
            {
                return;
            }

            if (_lockedProvider != null)
            {
                _lockedProvider.SetMovementLocked(_movementLockBeforeFishing);
            }

            _lockedProvider = null;
            _movementLockBeforeFishing = false;
            _ownsMovementLock = false;
        }

        private void ClearLocalPlayerBinding()
        {
            playerPresentation?.ConfigureLocalPlayer(null, null);
            fishVisualPresenter?.ConfigureCaughtPresentation(null);
            _localPlayer = null;
            _localPlayerGameObject = null;
            _inputProvider = null;
        }

        private void Teardown()
        {
            if (_tearingDown)
            {
                return;
            }

            _tearingDown = true;
            UnsubscribeInteractAction();
            UnsubscribeLocalPlayer();
            _currentFishingSpot = null;
            AbortOwnSessionIfNecessary();
            RestoreMovementLock();
            ClearLocalPlayerBinding();
            _handlingInteract = false;
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0f, interactionDistance);
        }
    }
}
