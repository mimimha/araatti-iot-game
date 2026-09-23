using System;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    public readonly struct FishingV3FishVisualMotionSettings
    {
        public float LateralAmplitude { get; }
        public float VerticalAmplitude { get; }
        public float ForwardAmplitude { get; }
        public float FrequencyHz { get; }
        public float YawAmplitudeDegrees { get; }
        public float RollAmplitudeDegrees { get; }

        public FishingV3FishVisualMotionSettings(
            float lateralAmplitude,
            float verticalAmplitude,
            float forwardAmplitude,
            float frequencyHz,
            float yawAmplitudeDegrees,
            float rollAmplitudeDegrees)
        {
            LateralAmplitude = lateralAmplitude;
            VerticalAmplitude = verticalAmplitude;
            ForwardAmplitude = forwardAmplitude;
            FrequencyHz = frequencyHz;
            YawAmplitudeDegrees = yawAmplitudeDegrees;
            RollAmplitudeDegrees = rollAmplitudeDegrees;
        }
    }

    public readonly struct FishingV3FishVisualMotionPose
    {
        public static readonly FishingV3FishVisualMotionPose Zero =
            new FishingV3FishVisualMotionPose(Vector3.zero, Vector3.zero);

        public Vector3 LocalPositionOffset { get; }
        public Vector3 LocalEulerAnglesOffset { get; }

        public FishingV3FishVisualMotionPose(
            Vector3 localPositionOffset,
            Vector3 localEulerAnglesOffset)
        {
            LocalPositionOffset = localPositionOffset;
            LocalEulerAnglesOffset = localEulerAnglesOffset;
        }

        public static FishingV3FishVisualMotionPose Lerp(
            FishingV3FishVisualMotionPose from,
            FishingV3FishVisualMotionPose to,
            float amount)
        {
            float t = Mathf.Clamp01(amount);
            return new FishingV3FishVisualMotionPose(
                Vector3.Lerp(from.LocalPositionOffset, to.LocalPositionOffset, t),
                Vector3.Lerp(from.LocalEulerAnglesOffset, to.LocalEulerAnglesOffset, t));
        }

        public static FishingV3FishVisualMotionPose Add(
            FishingV3FishVisualMotionPose left,
            FishingV3FishVisualMotionPose right)
        {
            return new FishingV3FishVisualMotionPose(
                left.LocalPositionOffset + right.LocalPositionOffset,
                left.LocalEulerAnglesOffset + right.LocalEulerAnglesOffset);
        }
    }

    /// <summary>
    /// Resolves the V3 profile's stable visual id through the shared fishing
    /// visual catalog and owns the single in-world fish shown during Fighting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingV3FishVisualPresenter : MonoBehaviour
    {
        public const float StateTransitionSeconds = 0.35f;
        public const float HeadShakeDurationSeconds = 0.3f;
        public const float MaximumLateralOffset = 0.9f;
        public const float MaximumVerticalOffset = 0.22f;
        public const float MaximumForwardOffset = 0.6f;
        public const float MaximumYawDegrees = 28f;
        public const float MaximumRollDegrees = 22f;
        public const float DefaultCaughtLiftSeconds = 0.7f;
        public const float DefaultCaughtHoldSeconds = 1.05f;
        public const float DefaultCaughtArcHeight = 0.6f;
        public const float DefaultCaughtDisplaySeconds =
            DefaultCaughtLiftSeconds + DefaultCaughtHoldSeconds;
        public const float DefaultLineBrokenMotionSeconds = 0.42f;
        public const float DefaultFishEscapedMotionSeconds = 0.7f;

        private const string PreferredHandAnchorName = "RightHandProp";
        private const string FallbackCaughtAnchorName = "CaughtFishAnchor";

        [SerializeField] private FishingMiniGameFacade facade;
        [SerializeField] private FishingModeController modeController;
        [SerializeField] private FishingSpot fishingSpot;
        [SerializeField] private FishingVisualSet visualSet;
        [SerializeField] private Transform presentationAnchor;
        [SerializeField] private Vector3 spotLocalPosition = new Vector3(0f, -0.55f, 3f);
        [SerializeField] private Vector3 spotLocalEulerAngles = new Vector3(0f, 90f, 0f);
        [SerializeField] private Vector3 visualScale = Vector3.one;
        [Header("Caught Fish Presentation")]
        [SerializeField, Min(0f)] private float caughtLiftSeconds =
            DefaultCaughtLiftSeconds;
        [SerializeField, Min(0f)] private float caughtHoldSeconds =
            DefaultCaughtHoldSeconds;
        [SerializeField, Min(0f)] private float caughtArcHeight =
            DefaultCaughtArcHeight;
        [SerializeField] private Vector3 caughtLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 caughtLocalEulerAngles = Vector3.zero;
        [SerializeField, Min(0.01f)] private float caughtSmallScale = 0.55f;
        [SerializeField, Min(0.01f)] private float caughtNormalScale = 0.7f;
        [SerializeField, Min(0.01f)] private float caughtStrongScale = 0.85f;
        [SerializeField] private Vector3 fallbackAnchorLocalPosition =
            new Vector3(0.35f, 1.15f, 0.3f);
        [SerializeField] private Vector3 fallbackAnchorLocalEulerAngles =
            new Vector3(0f, 90f, 0f);
        [Header("Failure Fish Presentation")]
        [SerializeField, Min(0f)] private float lineBrokenMotionSeconds =
            DefaultLineBrokenMotionSeconds;
        [SerializeField, Min(0f)] private float fishEscapedMotionSeconds =
            DefaultFishEscapedMotionSeconds;
        [SerializeField] private Vector3 lineBrokenLocalTravel =
            new Vector3(0.8f, 0.35f, 1.75f);
        [SerializeField] private Vector3 fishEscapedLocalTravel =
            new Vector3(-0.55f, -0.55f, 2.6f);
        [SerializeField] private Vector3 lineBrokenRotationDegrees =
            new Vector3(10f, 45f, -35f);
        [SerializeField] private Vector3 fishEscapedRotationDegrees =
            new Vector3(-12f, -20f, 8f);

        private GameObject _activeVisual;
        private GameObject _caughtVisual;
        private string _activeVisualId = string.Empty;
        private string _caughtVisualId = string.Empty;
        private string _lastSessionVisualId = string.Empty;
        private string _lastCaughtVisualId = string.Empty;
        private float _caughtTimeRemainingSeconds;
        private float _caughtPresentationElapsedSeconds;
        private bool _caughtResultConsumed;
        private bool _caughtAttachedToHand;
        private Transform _caughtPlayerOverride;
        private Transform _caughtHandAnchor;
        private bool _ownsCaughtHandAnchor;
        private Vector3 _caughtLiftStartWorldPosition;
        private Quaternion _caughtLiftStartWorldRotation = Quaternion.identity;
        private Vector3 _caughtLiftStartWorldScale = Vector3.one;
        private Vector3 _caughtTargetLocalScale = Vector3.one;
        private bool _failureResultConsumed;
        private FishingV3Result _failurePresentationResult = FishingV3Result.Active;
        private float _failurePresentationElapsedSeconds;
        private Vector3 _failureStartWorldPosition;
        private Quaternion _failureStartWorldRotation = Quaternion.identity;
        private Vector3 _failureTargetWorldPosition;
        private Quaternion _failureTargetWorldRotation = Quaternion.identity;
        private float _motionElapsedSeconds;
        private float _stateTransitionElapsedSeconds = StateTransitionSeconds;
        private float _headShakeElapsedSeconds = HeadShakeDurationSeconds;
        private float _headShakeIntensityNormalized;
        private int _lastHeadShakeSequence;
        private bool _hasMotionState;
        private FishingV3FishState _previousMotionState = FishingV3FishState.Calm;
        private FishingV3FishState _currentMotionState = FishingV3FishState.Calm;
        private FishingV3FishVisualMotionPose _currentMotionPose =
            FishingV3FishVisualMotionPose.Zero;

        public bool HasActiveVisual => _activeVisual != null && _activeVisual.activeSelf;
        public int ActiveVisualCount => HasActiveVisual ? 1 : 0;
        public GameObject ActiveVisual => _activeVisual;
        public string ActiveVisualId => _activeVisualId;
        public string LastSessionVisualId => _lastSessionVisualId;
        public string LastCaughtVisualId => _lastCaughtVisualId;
        public bool HasCaughtVisual => _caughtVisual != null && _caughtVisual.activeSelf;
        public int CaughtVisualCount => HasCaughtVisual ? 1 : 0;
        public GameObject CaughtVisual => _caughtVisual;
        public string CaughtVisualId => _caughtVisualId;
        public float CaughtTimeRemainingSeconds => _caughtTimeRemainingSeconds;
        public Transform CaughtHandAnchor => _caughtHandAnchor;
        public bool IsCaughtLiftActive => HasCaughtVisual && !_caughtAttachedToHand;
        public bool IsCaughtHoldActive => HasCaughtVisual && _caughtAttachedToHand;
        public float CaughtLiftProgressNormalized => GetCaughtLiftProgress();
        public Vector3 CaughtLiftStartWorldPosition => _caughtLiftStartWorldPosition;
        public bool IsFailurePresentationActive =>
            HasActiveVisual && IsFailureResult(_failurePresentationResult);
        public bool IsLineBrokenPresentationActive =>
            IsFailurePresentationActive &&
            _failurePresentationResult == FishingV3Result.LineBroken;
        public bool IsFishEscapedPresentationActive =>
            IsFailurePresentationActive &&
            _failurePresentationResult == FishingV3Result.FishEscaped;
        public FishingV3Result FailurePresentationResult => _failurePresentationResult;
        public float FailureProgressNormalized => GetFailureProgress();
        public Vector3 FailureStartWorldPosition => _failureStartWorldPosition;
        public Vector3 FailureTargetWorldPosition => _failureTargetWorldPosition;
        public bool HasConfiguredCatalog => visualSet != null;
        public bool HasPresentationAnchor => presentationAnchor != null;
        public Transform PresentationAnchor => presentationAnchor;
        public float MotionElapsedSeconds => _motionElapsedSeconds;
        public bool IsHeadShakeActive =>
            _headShakeElapsedSeconds < HeadShakeDurationSeconds;
        public FishingV3FishState CurrentMotionState => _currentMotionState;
        public Vector3 CurrentMotionOffset => _currentMotionPose.LocalPositionOffset;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void Update()
        {
            Refresh(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            RemoveActiveVisual();
            ResetFailurePresentationState();
            ResetCaughtPresentation();
            ReleaseOwnedCaughtAnchor();
        }

        private void OnDestroy()
        {
            RemoveActiveVisual();
            ResetFailurePresentationState();
            ResetCaughtPresentation();
            ReleaseOwnedCaughtAnchor();
        }

        public void Configure(
            FishingMiniGameFacade value,
            FishingSpot spot,
            FishingVisualSet catalog,
            FishingModeController controller = null,
            Transform anchor = null)
        {
            RemoveActiveVisual();
            ResetFailurePresentationState();
            ResetCaughtPresentation();
            ReleaseOwnedCaughtAnchor();
            facade = value;
            fishingSpot = spot;
            visualSet = catalog;
            modeController = controller;
            presentationAnchor = anchor;
        }

        public void ConfigureCaughtPresentation(
            Transform localPlayerRoot,
            float displaySeconds = DefaultCaughtDisplaySeconds)
        {
            float total = SanitizeCaughtDuration(displaySeconds);
            float lift = Mathf.Min(
                total,
                SanitizeNonNegative(
                    caughtLiftSeconds,
                    DefaultCaughtLiftSeconds));
            ConfigureCaughtTransition(
                localPlayerRoot,
                lift,
                Mathf.Max(0f, total - lift),
                caughtArcHeight);
        }

        public void ConfigureCaughtTransition(
            Transform localPlayerRoot,
            float liftSeconds,
            float holdSeconds,
            float arcHeight)
        {
            ResetCaughtPresentation();
            ReleaseOwnedCaughtAnchor();
            _caughtPlayerOverride = localPlayerRoot;
            caughtLiftSeconds = SanitizeNonNegative(
                liftSeconds,
                DefaultCaughtLiftSeconds);
            caughtHoldSeconds = SanitizeNonNegative(
                holdSeconds,
                DefaultCaughtHoldSeconds);
            caughtArcHeight = SanitizeNonNegative(
                arcHeight,
                DefaultCaughtArcHeight);
        }

        public void ConfigurePlacement(
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 scale)
        {
            spotLocalPosition = localPosition;
            spotLocalEulerAngles = localEulerAngles;
            visualScale = SanitizeScale(scale);
            if (_activeVisual != null &&
                visualSet != null &&
                visualSet.TryGetFish(_activeVisualId, out FishingFishVisualEntry entry))
            {
                ApplyPlacement(
                    _activeVisual.transform,
                    ResolveAnchor(),
                    entry,
                    presentationAnchor != null);
            }
        }

        public void RefreshNow()
        {
            Refresh(0f, 0f);
        }

        public void StepPresentation(float deltaTime)
        {
            Refresh(deltaTime, deltaTime);
        }

        private void Refresh(float motionDeltaTime, float presentationDeltaTime)
        {
            ResolveDependencies();
            FishingV3Snapshot snapshot = facade != null ? facade.V3Current : null;
            RememberIdentity(snapshot);
            RefreshCaughtPresentation(snapshot, presentationDeltaTime);
            if (RefreshFailurePresentation(snapshot, presentationDeltaTime))
            {
                return;
            }

            if (!ShouldShowFish(snapshot))
            {
                RemoveActiveVisual();
                return;
            }

            string visualId = snapshot.FishVisualId;
            if (visualSet == null ||
                !visualSet.TryGetFish(visualId, out FishingFishVisualEntry entry))
            {
                RemoveActiveVisual();
                return;
            }

            Transform anchor = ResolveAnchor();
            if (_activeVisual != null &&
                string.Equals(_activeVisualId, visualId, StringComparison.OrdinalIgnoreCase))
            {
                ApplyPlacement(
                    _activeVisual.transform,
                    anchor,
                    entry,
                    presentationAnchor != null);
                ApplyMotion(_activeVisual.transform, snapshot, motionDeltaTime);
                return;
            }

            RemoveActiveVisual();
            _activeVisual = Instantiate(entry.Prefab, anchor, false);
            _activeVisual.name = $"{entry.Prefab.name} (V3 {visualId})";
            _activeVisualId = visualId;
            ApplyPlacement(
                _activeVisual.transform,
                anchor,
                entry,
                presentationAnchor != null);
            ResetMotion(snapshot);
            ApplyMotion(_activeVisual.transform, snapshot, motionDeltaTime);

            FishVisualAdapter adapter = _activeVisual.GetComponent<FishVisualAdapter>();
            if (adapter != null) adapter.ApplyTint(entry.Tint);
        }

        private bool RefreshFailurePresentation(
            FishingV3Snapshot snapshot,
            float deltaTime)
        {
            if (snapshot == null)
            {
                ResetFailurePresentationState();
                return false;
            }

            if (snapshot.Result == FishingV3Result.Active)
            {
                _failureResultConsumed = false;
                if (IsFailureResult(_failurePresentationResult))
                {
                    RemoveActiveVisual();
                    ResetFailurePresentationState();
                }
                return false;
            }

            if (!IsFailureResult(snapshot.Result))
            {
                ResetFailurePresentationState();
                return false;
            }

            if (!_failureResultConsumed)
            {
                _failureResultConsumed = true;
                if (!TryBeginFailurePresentation(snapshot.Result))
                {
                    RemoveActiveVisual();
                }
            }

            if (!IsFailurePresentationActive) return true;

            _failurePresentationElapsedSeconds += SanitizeDeltaTime(deltaTime);
            ApplyFailurePresentationPose(GetFailureProgress());
            if (FailureProgressNormalized >= 1f)
            {
                RemoveActiveVisual();
                ClearFailureMotionState();
            }

            return true;
        }

        private bool TryBeginFailurePresentation(FishingV3Result result)
        {
            if (!HasActiveVisual || !IsFailureResult(result)) return false;

            Transform activeTransform = _activeVisual.transform;
            Transform anchor = ResolveAnchor();
            Vector3 localTravel = result == FishingV3Result.LineBroken
                ? lineBrokenLocalTravel
                : fishEscapedLocalTravel;
            Vector3 rotationDegrees = result == FishingV3Result.LineBroken
                ? lineBrokenRotationDegrees
                : fishEscapedRotationDegrees;

            _failurePresentationResult = result;
            _failurePresentationElapsedSeconds = 0f;
            _failureStartWorldPosition = activeTransform.position;
            _failureStartWorldRotation = activeTransform.rotation;
            _failureTargetWorldPosition = _failureStartWorldPosition +
                (anchor != null
                    ? anchor.TransformDirection(localTravel)
                    : localTravel);
            _failureTargetWorldRotation =
                _failureStartWorldRotation * Quaternion.Euler(rotationDegrees);

            if (result == FishingV3Result.LineBroken)
            {
                foreach (Animator animator in
                         _activeVisual.GetComponentsInChildren<Animator>(true))
                {
                    animator.speed = 0f;
                }
            }

            ApplyFailurePresentationPose(0f);
            return true;
        }

        private void RefreshCaughtPresentation(
            FishingV3Snapshot snapshot,
            float deltaTime)
        {
            if (snapshot == null)
            {
                ResetCaughtPresentation();
                return;
            }

            if (snapshot.Result == FishingV3Result.Active)
            {
                _caughtResultConsumed = false;
                RemoveCaughtVisual();
                return;
            }

            if (snapshot.Result != FishingV3Result.Caught)
            {
                _caughtResultConsumed = true;
                RemoveCaughtVisual();
                return;
            }

            if (!_caughtResultConsumed)
            {
                _caughtResultConsumed = TryCreateCaughtVisual(snapshot);
            }

            if (!HasCaughtVisual) return;

            if (_caughtHandAnchor == null)
            {
                RemoveCaughtVisual();
                return;
            }

            float step = SanitizeDeltaTime(deltaTime);
            _caughtPresentationElapsedSeconds += step;
            float liftDuration = GetCaughtLiftDuration();
            float totalDuration = liftDuration + GetCaughtHoldDuration();
            _caughtTimeRemainingSeconds = Mathf.Max(
                0f,
                totalDuration - _caughtPresentationElapsedSeconds);

            if (!_caughtAttachedToHand)
            {
                ApplyCaughtLiftPose(GetCaughtLiftProgress());
                if (CaughtLiftProgressNormalized >= 1f)
                {
                    AttachCaughtVisualToHand();
                }
            }

            if (_caughtPresentationElapsedSeconds >= totalDuration)
            {
                RemoveCaughtVisual();
            }
        }

        private bool TryCreateCaughtVisual(FishingV3Snapshot snapshot)
        {
            if (snapshot == null || visualSet == null) return false;

            string visualId = string.IsNullOrWhiteSpace(snapshot.FishVisualId)
                ? _lastCaughtVisualId
                : snapshot.FishVisualId;
            if (string.IsNullOrWhiteSpace(visualId) ||
                !visualSet.TryGetFish(visualId, out FishingFishVisualEntry entry))
            {
                return false;
            }

            Transform playerRoot = _caughtPlayerOverride != null
                ? _caughtPlayerOverride
                : null;
            if (playerRoot == null) return false;

            Transform handAnchor = ResolveCaughtHandAnchor(playerRoot);
            if (handAnchor == null) return false;

            FishingV3PlayerPresentation playerPresentation =
                GetComponent<FishingV3PlayerPresentation>();
            playerPresentation?.CleanupForCaughtPresentation();

            bool hasFightingPose = HasActiveVisual;
            Vector3 startPosition = hasFightingPose
                ? _activeVisual.transform.position
                : handAnchor.TransformPoint(caughtLocalPosition);
            Quaternion startRotation = hasFightingPose
                ? _activeVisual.transform.rotation
                : handAnchor.rotation * Quaternion.Euler(caughtLocalEulerAngles);
            Vector3 startScale = hasFightingPose
                ? SanitizeScale(_activeVisual.transform.lossyScale)
                : GetCaughtTargetWorldScale(handAnchor, entry, snapshot.FishProfileId);

            RemoveCaughtVisual();
            _caughtVisual = Instantiate(entry.Prefab);
            _caughtVisual.name = $"{entry.Prefab.name} (Caught {visualId})";
            _caughtVisualId = visualId;
            _caughtHandAnchor = handAnchor;
            _caughtPresentationElapsedSeconds = 0f;
            _caughtTimeRemainingSeconds =
                GetCaughtLiftDuration() + GetCaughtHoldDuration();
            _caughtAttachedToHand = false;
            _caughtLiftStartWorldPosition = startPosition;
            _caughtLiftStartWorldRotation = startRotation;
            _caughtLiftStartWorldScale = startScale;
            _caughtTargetLocalScale = GetCaughtLocalScale(
                entry,
                snapshot.FishProfileId);
            _caughtVisual.transform.position = startPosition;
            _caughtVisual.transform.rotation = startRotation;
            _caughtVisual.transform.localScale = startScale;

            FishVisualAdapter adapter = _caughtVisual.GetComponent<FishVisualAdapter>();
            if (adapter != null) adapter.ApplyTint(entry.Tint);

            foreach (Animator animator in _caughtVisual.GetComponentsInChildren<Animator>(true))
            {
                animator.speed = 0f;
            }

            ApplyCaughtLiftPose(0f);

            return true;
        }

        public static FishingV3FishVisualMotionSettings GetMotionSettings(
            FishingV3FishState state,
            FishingV3FishProfileId profileId)
        {
            FishingV3FishVisualMotionSettings settings;
            switch (state)
            {
                case FishingV3FishState.Run:
                    settings = new FishingV3FishVisualMotionSettings(
                        0.58f, 0.12f, 0.38f, 1.05f, 16f, 12f);
                    break;
                case FishingV3FishState.Fight:
                    settings = new FishingV3FishVisualMotionSettings(
                        0.34f, 0.09f, 0.14f, 0.65f, 10f, 7f);
                    break;
                default:
                    settings = new FishingV3FishVisualMotionSettings(
                        0.16f, 0.05f, 0.06f, 0.22f, 4f, 2f);
                    break;
            }

            float amplitudeScale = 1f;
            float frequencyScale = 1f;
            float rotationScale = 1f;
            if (profileId == FishingV3FishProfileId.Small)
            {
                amplitudeScale = 0.8f;
                frequencyScale = 1.15f;
                rotationScale = 0.9f;
            }
            else if (profileId == FishingV3FishProfileId.Strong)
            {
                amplitudeScale = 1.15f;
                frequencyScale = 0.9f;
                rotationScale = 1.1f;
            }

            return new FishingV3FishVisualMotionSettings(
                settings.LateralAmplitude * amplitudeScale,
                settings.VerticalAmplitude * amplitudeScale,
                settings.ForwardAmplitude * amplitudeScale,
                settings.FrequencyHz * frequencyScale,
                settings.YawAmplitudeDegrees * rotationScale,
                settings.RollAmplitudeDegrees * rotationScale);
        }

        public static FishingV3FishVisualMotionPose EvaluateStateMotion(
            FishingV3FishState state,
            FishingV3FishProfileId profileId,
            float elapsedSeconds)
        {
            FishingV3FishVisualMotionSettings settings =
                GetMotionSettings(state, profileId);
            float safeTime = IsFinite(elapsedSeconds) && elapsedSeconds > 0f
                ? elapsedSeconds
                : 0f;
            float phase = safeTime * settings.FrequencyHz * Mathf.PI * 2f;
            float lateral = settings.LateralAmplitude * Mathf.Sin(phase);
            float vertical = settings.VerticalAmplitude *
                Mathf.Sin(phase * 0.73f);
            float forward = state == FishingV3FishState.Run
                ? settings.ForwardAmplitude *
                  (0.5f - 0.5f * Mathf.Cos(phase * 0.45f))
                : settings.ForwardAmplitude * Mathf.Sin(phase * 0.55f);
            float yaw = settings.YawAmplitudeDegrees * Mathf.Sin(phase * 0.9f);
            float roll = settings.RollAmplitudeDegrees * Mathf.Sin(phase * 1.15f);
            return ClampMotion(new FishingV3FishVisualMotionPose(
                new Vector3(lateral, vertical, forward),
                new Vector3(0f, yaw, roll)));
        }

        public static FishingV3FishVisualMotionPose EvaluateHeadShake(
            float elapsedSeconds,
            float intensityNormalized)
        {
            if (!IsFinite(elapsedSeconds) ||
                elapsedSeconds < 0f ||
                elapsedSeconds >= HeadShakeDurationSeconds)
            {
                return FishingV3FishVisualMotionPose.Zero;
            }

            float intensity = Mathf.Clamp01(intensityNormalized);
            float progress = elapsedSeconds / HeadShakeDurationSeconds;
            float envelope = Mathf.Sin(Mathf.PI * progress);
            float wave = Mathf.Sin(Mathf.PI * 2f * 12f * elapsedSeconds);
            float amount = intensity * envelope * wave;
            return new FishingV3FishVisualMotionPose(
                new Vector3(0.12f * amount, 0f, 0f),
                new Vector3(0f, 10f * amount, -8f * amount));
        }

        public static bool ShouldShowFish(FishingV3Snapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Result != FishingV3Result.Active ||
                snapshot.GameplayPhase != FishingV3GameplayPhase.Fighting)
            {
                return false;
            }

            return snapshot.RuntimeState == FishingV3RuntimeState.Running ||
                   snapshot.RuntimeState == FishingV3RuntimeState.Paused;
        }

        private void ResolveDependencies()
        {
            if (facade == null) facade = GetComponent<FishingMiniGameFacade>();
            if (modeController == null) modeController = GetComponent<FishingModeController>();
            if (visualSet == null)
                visualSet = Resources.Load<FishingVisualSet>("Fishing/FishingVisualSet");
        }

        private Transform ResolveAnchor()
        {
            if (presentationAnchor != null) return presentationAnchor;
            if (modeController != null && modeController.CurrentSpot != null)
                return modeController.CurrentSpot.transform;
            if (fishingSpot != null) return fishingSpot.transform;
            return transform;
        }

        private void RememberIdentity(FishingV3Snapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.FishVisualId)) return;

            _lastSessionVisualId = snapshot.FishVisualId;
            if (snapshot.Result == FishingV3Result.Caught)
                _lastCaughtVisualId = snapshot.FishVisualId;
        }

        private void ApplyPlacement(
            Transform instance,
            Transform anchor,
            FishingFishVisualEntry entry,
            bool usePresentationAnchor)
        {
            if (instance == null || entry == null) return;

            Transform safeAnchor = anchor != null ? anchor : transform;
            if (instance.parent != safeAnchor) instance.SetParent(safeAnchor, false);
            instance.localPosition = usePresentationAnchor
                ? entry.LocalPosition
                : spotLocalPosition + entry.LocalPosition;
            instance.localRotation = usePresentationAnchor
                ? Quaternion.Euler(entry.LocalEulerAngles)
                : Quaternion.Euler(spotLocalEulerAngles) *
                  Quaternion.Euler(entry.LocalEulerAngles);
            instance.localScale = Vector3.Scale(
                SanitizeScale(visualScale),
                SanitizeScale(entry.LocalScale));
        }

        private void ResetMotion(FishingV3Snapshot snapshot)
        {
            _motionElapsedSeconds = 0f;
            _stateTransitionElapsedSeconds = StateTransitionSeconds;
            _headShakeElapsedSeconds = HeadShakeDurationSeconds;
            _headShakeIntensityNormalized = 0f;
            _lastHeadShakeSequence = snapshot != null
                ? snapshot.HeadShakeEventSequence
                : 0;
            _currentMotionState = snapshot != null
                ? snapshot.FishState
                : FishingV3FishState.Calm;
            _previousMotionState = _currentMotionState;
            _hasMotionState = snapshot != null;
            _currentMotionPose = FishingV3FishVisualMotionPose.Zero;
        }

        private void ApplyMotion(
            Transform instance,
            FishingV3Snapshot snapshot,
            float deltaTime)
        {
            if (instance == null || snapshot == null) return;

            float step = snapshot.RuntimeState == FishingV3RuntimeState.Paused
                ? 0f
                : SanitizeDeltaTime(deltaTime);
            UpdateMotionState(snapshot.FishState);
            ConsumeHeadShake(snapshot);
            _motionElapsedSeconds += step;
            _stateTransitionElapsedSeconds = Mathf.Min(
                StateTransitionSeconds,
                _stateTransitionElapsedSeconds + step);
            if (IsHeadShakeActive)
            {
                _headShakeElapsedSeconds = Mathf.Min(
                    HeadShakeDurationSeconds,
                    _headShakeElapsedSeconds + step);
            }

            FishingV3FishVisualMotionPose previous = EvaluateStateMotion(
                _previousMotionState,
                snapshot.FishProfileId,
                _motionElapsedSeconds);
            FishingV3FishVisualMotionPose current = EvaluateStateMotion(
                _currentMotionState,
                snapshot.FishProfileId,
                _motionElapsedSeconds);
            float blend = StateTransitionSeconds <= 0f
                ? 1f
                : Mathf.SmoothStep(
                    0f,
                    1f,
                    _stateTransitionElapsedSeconds / StateTransitionSeconds);
            FishingV3FishVisualMotionPose statePose =
                FishingV3FishVisualMotionPose.Lerp(previous, current, blend);
            FishingV3FishVisualMotionPose shakePose = EvaluateHeadShake(
                _headShakeElapsedSeconds,
                _headShakeIntensityNormalized);
            _currentMotionPose = ClampMotion(
                FishingV3FishVisualMotionPose.Add(statePose, shakePose));

            instance.localPosition += _currentMotionPose.LocalPositionOffset;
            instance.localRotation *= Quaternion.Euler(
                _currentMotionPose.LocalEulerAnglesOffset);
        }

        private void UpdateMotionState(FishingV3FishState state)
        {
            if (!_hasMotionState)
            {
                _previousMotionState = state;
                _currentMotionState = state;
                _stateTransitionElapsedSeconds = StateTransitionSeconds;
                _hasMotionState = true;
                return;
            }

            if (_currentMotionState == state) return;
            _previousMotionState = _currentMotionState;
            _currentMotionState = state;
            _stateTransitionElapsedSeconds = 0f;
        }

        private void ConsumeHeadShake(FishingV3Snapshot snapshot)
        {
            int current = snapshot.HeadShakeEventSequence;
            if (current < _lastHeadShakeSequence)
            {
                _lastHeadShakeSequence = current;
                _headShakeElapsedSeconds = HeadShakeDurationSeconds;
                _headShakeIntensityNormalized = 0f;
                return;
            }

            if (current == _lastHeadShakeSequence) return;
            _lastHeadShakeSequence = current;
            _headShakeElapsedSeconds = 0f;
            _headShakeIntensityNormalized = Mathf.Clamp01(
                snapshot.HeadShakeIntensityNormalized);
        }

        private Transform ResolveCaughtHandAnchor(Transform playerRoot)
        {
            if (playerRoot == null) return null;
            if (_caughtHandAnchor != null &&
                (_caughtHandAnchor == playerRoot || _caughtHandAnchor.IsChildOf(playerRoot)))
            {
                return _caughtHandAnchor;
            }

            ReleaseOwnedCaughtAnchor();

            Transform namedAnchor = FindDescendant(playerRoot, PreferredHandAnchorName);
            if (namedAnchor != null)
            {
                _caughtHandAnchor = namedAnchor;
                return _caughtHandAnchor;
            }

            Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand != null)
                {
                    _caughtHandAnchor = rightHand;
                    return _caughtHandAnchor;
                }
            }

            Transform existingFallback = FindDescendant(
                playerRoot,
                FallbackCaughtAnchorName);
            if (existingFallback != null)
            {
                _caughtHandAnchor = existingFallback;
                return _caughtHandAnchor;
            }

            GameObject fallback = new GameObject(FallbackCaughtAnchorName);
            _caughtHandAnchor = fallback.transform;
            _caughtHandAnchor.SetParent(playerRoot, false);
            _caughtHandAnchor.localPosition = fallbackAnchorLocalPosition;
            _caughtHandAnchor.localRotation = Quaternion.Euler(
                fallbackAnchorLocalEulerAngles);
            _caughtHandAnchor.localScale = Vector3.one;
            _ownsCaughtHandAnchor = true;
            return _caughtHandAnchor;
        }

        private Vector3 GetCaughtLocalScale(
            FishingFishVisualEntry entry,
            FishingV3FishProfileId profileId)
        {
            if (entry == null) return Vector3.one;
            float profileScale = GetCaughtProfileScale(profileId);
            return Vector3.Scale(
                SanitizeScale(entry.LocalScale),
                Vector3.one * profileScale);
        }

        private Vector3 GetCaughtTargetWorldScale(
            Transform handAnchor,
            FishingFishVisualEntry entry,
            FishingV3FishProfileId profileId)
        {
            if (handAnchor == null) return GetCaughtLocalScale(entry, profileId);
            return Vector3.Scale(
                SanitizeScale(handAnchor.lossyScale),
                GetCaughtLocalScale(entry, profileId));
        }

        private float GetCaughtLiftDuration()
        {
            return SanitizeNonNegative(
                caughtLiftSeconds,
                DefaultCaughtLiftSeconds);
        }

        private float GetCaughtHoldDuration()
        {
            return SanitizeNonNegative(
                caughtHoldSeconds,
                DefaultCaughtHoldSeconds);
        }

        private float GetCaughtLiftProgress()
        {
            if (!HasCaughtVisual) return 0f;
            float duration = GetCaughtLiftDuration();
            return duration <= 0f
                ? 1f
                : Mathf.Clamp01(_caughtPresentationElapsedSeconds / duration);
        }

        private void ApplyCaughtLiftPose(float progress)
        {
            if (_caughtVisual == null || _caughtHandAnchor == null) return;

            float linearProgress = Mathf.Clamp01(progress);
            float easedProgress = Mathf.SmoothStep(0f, 1f, linearProgress);
            Vector3 targetPosition = _caughtHandAnchor.TransformPoint(
                caughtLocalPosition);
            Quaternion targetRotation =
                _caughtHandAnchor.rotation *
                Quaternion.Euler(caughtLocalEulerAngles);
            Vector3 targetWorldScale = Vector3.Scale(
                SanitizeScale(_caughtHandAnchor.lossyScale),
                _caughtTargetLocalScale);

            Vector3 position = Vector3.Lerp(
                _caughtLiftStartWorldPosition,
                targetPosition,
                easedProgress);
            position += Vector3.up * (
                Mathf.Sin(Mathf.PI * linearProgress) *
                SanitizeNonNegative(caughtArcHeight, DefaultCaughtArcHeight));

            Transform caughtTransform = _caughtVisual.transform;
            caughtTransform.position = position;
            caughtTransform.rotation = Quaternion.Slerp(
                _caughtLiftStartWorldRotation,
                targetRotation,
                easedProgress);
            caughtTransform.localScale = Vector3.Lerp(
                _caughtLiftStartWorldScale,
                targetWorldScale,
                easedProgress);
        }

        private void AttachCaughtVisualToHand()
        {
            if (_caughtVisual == null || _caughtHandAnchor == null) return;

            ApplyCaughtLiftPose(1f);
            Transform caughtTransform = _caughtVisual.transform;
            caughtTransform.SetParent(_caughtHandAnchor, false);
            caughtTransform.localPosition = caughtLocalPosition;
            caughtTransform.localRotation = Quaternion.Euler(
                caughtLocalEulerAngles);
            caughtTransform.localScale = _caughtTargetLocalScale;
            _caughtAttachedToHand = true;
        }

        private float GetCaughtProfileScale(FishingV3FishProfileId profileId)
        {
            switch (profileId)
            {
                case FishingV3FishProfileId.Small:
                    return SanitizeScaleAxis(caughtSmallScale);
                case FishingV3FishProfileId.Strong:
                    return SanitizeScaleAxis(caughtStrongScale);
                default:
                    return SanitizeScaleAxis(caughtNormalScale);
            }
        }

        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrWhiteSpace(exactName)) return null;

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate != null &&
                    string.Equals(candidate.name, exactName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private float GetFailureDuration()
        {
            switch (_failurePresentationResult)
            {
                case FishingV3Result.LineBroken:
                    return SanitizeNonNegative(
                        lineBrokenMotionSeconds,
                        DefaultLineBrokenMotionSeconds);
                case FishingV3Result.FishEscaped:
                    return SanitizeNonNegative(
                        fishEscapedMotionSeconds,
                        DefaultFishEscapedMotionSeconds);
                default:
                    return 0f;
            }
        }

        private float GetFailureProgress()
        {
            if (!IsFailurePresentationActive) return 0f;
            float duration = GetFailureDuration();
            return duration <= 0f
                ? 1f
                : Mathf.Clamp01(_failurePresentationElapsedSeconds / duration);
        }

        private void ApplyFailurePresentationPose(float progress)
        {
            if (_activeVisual == null) return;

            float linearProgress = Mathf.Clamp01(progress);
            float movementProgress = _failurePresentationResult ==
                FishingV3Result.LineBroken
                ? 1f - Mathf.Pow(1f - linearProgress, 3f)
                : Mathf.SmoothStep(0f, 1f, linearProgress);
            Vector3 position = Vector3.Lerp(
                _failureStartWorldPosition,
                _failureTargetWorldPosition,
                movementProgress);

            if (_failurePresentationResult == FishingV3Result.LineBroken)
            {
                position += Vector3.up * (
                    Mathf.Sin(Mathf.PI * linearProgress) * 0.18f);
            }

            Transform activeTransform = _activeVisual.transform;
            activeTransform.position = position;
            activeTransform.rotation = Quaternion.Slerp(
                _failureStartWorldRotation,
                _failureTargetWorldRotation,
                movementProgress);
        }

        private static bool IsFailureResult(FishingV3Result result)
        {
            return result == FishingV3Result.LineBroken ||
                   result == FishingV3Result.FishEscaped;
        }

        private void ClearFailureMotionState()
        {
            _failurePresentationResult = FishingV3Result.Active;
            _failurePresentationElapsedSeconds = 0f;
            _failureStartWorldPosition = Vector3.zero;
            _failureStartWorldRotation = Quaternion.identity;
            _failureTargetWorldPosition = Vector3.zero;
            _failureTargetWorldRotation = Quaternion.identity;
        }

        private void ResetFailurePresentationState()
        {
            _failureResultConsumed = false;
            ClearFailureMotionState();
        }

        private void RemoveCaughtVisual()
        {
            if (_caughtVisual != null)
            {
                _caughtVisual.SetActive(false);
                if (Application.isPlaying) Destroy(_caughtVisual);
                else DestroyImmediate(_caughtVisual);
            }

            _caughtVisual = null;
            _caughtVisualId = string.Empty;
            _caughtTimeRemainingSeconds = 0f;
            _caughtPresentationElapsedSeconds = 0f;
            _caughtAttachedToHand = false;
            _caughtLiftStartWorldPosition = Vector3.zero;
            _caughtLiftStartWorldRotation = Quaternion.identity;
            _caughtLiftStartWorldScale = Vector3.one;
            _caughtTargetLocalScale = Vector3.one;
        }

        private void ResetCaughtPresentation()
        {
            RemoveCaughtVisual();
            _caughtResultConsumed = false;
        }

        private void ReleaseOwnedCaughtAnchor()
        {
            if (_ownsCaughtHandAnchor && _caughtHandAnchor != null)
            {
                _caughtHandAnchor.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_caughtHandAnchor.gameObject);
                else DestroyImmediate(_caughtHandAnchor.gameObject);
            }

            _caughtHandAnchor = null;
            _ownsCaughtHandAnchor = false;
        }

        private void RemoveActiveVisual()
        {
            if (_activeVisual != null)
            {
                _activeVisual.SetActive(false);
                if (Application.isPlaying) Destroy(_activeVisual);
                else DestroyImmediate(_activeVisual);
            }

            _activeVisual = null;
            _activeVisualId = string.Empty;
            ResetMotion(null);
        }

        private static FishingV3FishVisualMotionPose ClampMotion(
            FishingV3FishVisualMotionPose pose)
        {
            Vector3 position = pose.LocalPositionOffset;
            Vector3 rotation = pose.LocalEulerAnglesOffset;
            position.x = Mathf.Clamp(
                position.x, -MaximumLateralOffset, MaximumLateralOffset);
            position.y = Mathf.Clamp(
                position.y, -MaximumVerticalOffset, MaximumVerticalOffset);
            position.z = Mathf.Clamp(
                position.z, -MaximumForwardOffset, MaximumForwardOffset);
            rotation.y = Mathf.Clamp(rotation.y, -MaximumYawDegrees, MaximumYawDegrees);
            rotation.z = Mathf.Clamp(rotation.z, -MaximumRollDegrees, MaximumRollDegrees);
            return new FishingV3FishVisualMotionPose(position, rotation);
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return IsFinite(deltaTime) && deltaTime > 0f ? deltaTime : 0f;
        }

        private static float SanitizeCaughtDuration(float seconds)
        {
            return IsFinite(seconds) && seconds >= 0f
                ? seconds
                : DefaultCaughtDisplaySeconds;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f
                ? value
                : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return new Vector3(
                SanitizeScaleAxis(scale.x),
                SanitizeScaleAxis(scale.y),
                SanitizeScaleAxis(scale.z));
        }

        private static float SanitizeScaleAxis(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? 1f
                : value;
        }
    }
}
