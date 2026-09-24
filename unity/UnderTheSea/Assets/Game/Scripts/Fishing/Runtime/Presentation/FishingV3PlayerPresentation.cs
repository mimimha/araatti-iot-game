using System;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    public readonly struct FishingV3StrugglePose
    {
        public Vector3 LocalPositionOffset { get; }
        public Vector3 LocalEulerOffset { get; }

        public FishingV3StrugglePose(
            Vector3 localPositionOffset,
            Vector3 localEulerOffset)
        {
            LocalPositionOffset = localPositionOffset;
            LocalEulerOffset = localEulerOffset;
        }
    }

    public readonly struct FishingV3PlayerPresentationFrame
    {
        public bool IsOwnSession { get; }
        public bool IsPaused { get; }
        public FishingV3GameplayPhase GameplayPhase { get; }
        public FishingV3Result Result { get; }
        public float TensionNormalized { get; }
        public int TimingJudgementSequence { get; }
        public FishingV3TimingGrade TimingGrade { get; }
        public FishingV3TensionZone TensionZone { get; }

        public FishingV3PlayerPresentationFrame(
            bool isOwnSession,
            bool isPaused,
            FishingV3GameplayPhase gameplayPhase,
            FishingV3Result result,
            float tensionNormalized,
            int timingJudgementSequence,
            FishingV3TimingGrade timingGrade,
            FishingV3TensionZone tensionZone = FishingV3TensionZone.Safe)
        {
            IsOwnSession = isOwnSession;
            IsPaused = isPaused;
            GameplayPhase = gameplayPhase;
            Result = result;
            TensionNormalized = tensionNormalized;
            TimingJudgementSequence = timingJudgementSequence;
            TimingGrade = timingGrade;
            TensionZone = tensionZone;
        }
    }

    /// <summary>
    /// V3 angler presentation. Local sessions read their facade; replicated remote
    /// players can feed explicit presentation frames. Both paths reuse the player
    /// hand anchor without touching movement, input, or the Animator controller.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class FishingV3PlayerPresentation : MonoBehaviour
    {
        public const float DefaultTimingReactionSeconds = 0.22f;
        public const float DefaultBaseRodUpwardAngleDegrees = 15f;
        public const float DefaultMaximumRodBendMeters = 0.48f;
        public const float DefaultMinimumStruggleLeanDegrees = 3.5f;
        public const float DefaultMaximumStruggleLeanDegrees = 15f;
        public const float DefaultStruggleIntensityCurveExponent = 2.2f;
        public const float DefaultStruggleIntensityResponsePerSecond = 3.5f;
        public const float DefaultMinimumStruggleFrequencyHz = 0.7f;
        public const float DefaultMaximumStruggleFrequencyHz = 1.55f;
        public const float DefaultStruggleFrequencyCurveExponent = 1.5f;
        public const float DefaultStruggleFrequencyResponsePerSecond = 3f;

        private const string PreferredHandAnchorName = "RightHandProp";
        private const string FallbackHandAnchorName = "FishingRodHandAnchor";
        private const string PreferredStruggleTargetName = "Skeleton";

        [SerializeField] private FishingMiniGameFacade facade;
        [SerializeField] private FishingModeController modeController;
        [SerializeField] private FishingV3FishVisualPresenter fishVisualPresenter;
        [Header("Local Rod Presentation")]
        [SerializeField] private Vector3 handGripWorldOffset = Vector3.zero;
        [SerializeField] private float targetHeightOffset = 0.35f;
        [SerializeField, Range(0f, 45f)] private float baseRodUpwardAngleDegrees =
            DefaultBaseRodUpwardAngleDegrees;
        [SerializeField, Min(0.25f)] private float rodLength = 2.15f;
        [SerializeField, Min(0.005f)] private float rodBaseRadius = 0.035f;
        [SerializeField, Min(0f)] private float maximumRodBendMeters =
            DefaultMaximumRodBendMeters;
        [SerializeField, Min(0f)] private float timingReactionSeconds =
            DefaultTimingReactionSeconds;
        [SerializeField, Min(0f)] private float timingPullDegrees = 16f;
        [SerializeField] private Color rodColor = new Color(0.08f, 0.12f, 0.10f);
        [SerializeField] private Color reelColor = new Color(0.82f, 0.52f, 0.14f);
        [SerializeField] private Vector3 fallbackAnchorLocalPosition =
            new Vector3(0.24f, 1.05f, 0.28f);
        [Header("Local Character Struggle Presentation")]
        [SerializeField, Range(0f, 8f)] private float minimumStruggleLeanDegrees =
            DefaultMinimumStruggleLeanDegrees;
        [SerializeField, Range(0f, 25f)] private float maximumStruggleLeanDegrees =
            DefaultMaximumStruggleLeanDegrees;
        [SerializeField, Min(0.1f)] private float struggleIntensityCurveExponent =
            DefaultStruggleIntensityCurveExponent;
        [SerializeField, Min(0.1f)] private float struggleIntensityResponsePerSecond =
            DefaultStruggleIntensityResponsePerSecond;
        [SerializeField, Min(0.1f)] private float minimumStruggleFrequencyHz =
            DefaultMinimumStruggleFrequencyHz;
        [SerializeField, Min(0.1f)] private float maximumStruggleFrequencyHz =
            DefaultMaximumStruggleFrequencyHz;
        [SerializeField, Min(0.1f)] private float struggleFrequencyCurveExponent =
            DefaultStruggleFrequencyCurveExponent;
        [SerializeField, Min(0.1f)] private float struggleFrequencyResponsePerSecond =
            DefaultStruggleFrequencyResponsePerSecond;
        [SerializeField, Min(0f)] private float minimumStrugglePositionMeters = 0.007f;
        [SerializeField, Min(0f)] private float maximumStrugglePositionMeters = 0.035f;
        [SerializeField, Min(0f)] private float minimumStruggleRollDegrees = 0.7f;
        [SerializeField, Min(0f)] private float maximumStruggleRollDegrees = 4.2f;

        private Transform _localPlayerRoot;
        private Transform _targetAnchor;
        private Transform _handAnchor;
        private bool _ownsHandAnchor;
        private GameObject _rodRoot;
        private readonly Transform[] _rodSegments = new Transform[3];
        private Transform _rodTip;
        private Transform _reel;
        private Material _rodMaterial;
        private Material _reelMaterial;
        private int _lastTimingJudgementSequence;
        private float _timingReactionRemainingSeconds;
        private float _currentTensionNormalized;
        private float _currentRodBendMeters;
        private int _timingReactionCount;
        private Transform _struggleTarget;
        private Vector3 _struggleBaseLocalPosition;
        private Quaternion _struggleBaseLocalRotation;
        private bool _struggleActive;
        private float _currentStruggleIntensity;
        private float _targetStruggleIntensity;
        private float _currentStruggleFrequencyHz;
        private float _targetStruggleFrequencyHz;
        private float _strugglePhaseSeconds;
        private float _strugglePhaseRadians;
        private int _struggleActivationCount;
        private bool _externalPresentationDriven;

        public bool HasRodVisual => _rodRoot != null && _rodRoot.activeSelf;
        public bool IsFishingPoseActive => HasRodVisual;
        public bool IsTimingReactionActive =>
            HasRodVisual && _timingReactionRemainingSeconds > 0f;
        public GameObject RodRoot => _rodRoot;
        public Transform RodTip => _rodTip;
        public Transform HandAnchor => _handAnchor;
        public Transform LocalPlayerRoot => _localPlayerRoot;
        public float CurrentTensionNormalized => _currentTensionNormalized;
        public float CurrentRodBendMeters => _currentRodBendMeters;
        public float BaseRodUpwardAngleDegrees => GetBaseRodUpwardAngleDegrees();
        public int TimingReactionCount => _timingReactionCount;
        public int LastTimingJudgementSequence => _lastTimingJudgementSequence;
        public bool IsStruggleActive =>
            _struggleTarget != null && _struggleActive;
        public float CurrentStruggleIntensity => _currentStruggleIntensity;
        public float TargetStruggleIntensity => _targetStruggleIntensity;
        public float CurrentStruggleFrequencyHz => _currentStruggleFrequencyHz;
        public float TargetStruggleFrequencyHz => _targetStruggleFrequencyHz;
        public float StrugglePhaseSeconds => _strugglePhaseSeconds;
        public float StrugglePhaseRadians => _strugglePhaseRadians;
        public int StruggleActivationCount => _struggleActivationCount;
        public Transform StruggleTarget => _struggleTarget;
        public bool IsExternalPresentationDriven => _externalPresentationDriven;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void Update()
        {
            if (_externalPresentationDriven) return;
            RefreshLive(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            ApplyStrugglePose();
            if (HasRodVisual) ApplyRodPose();
        }

        private void OnDisable()
        {
            StopStruggle(true);
            CleanupRodVisual();
            ReleaseOwnedHandAnchor();
        }

        private void OnDestroy()
        {
            StopStruggle(true);
            CleanupRodVisual();
            ReleaseOwnedHandAnchor();
        }

        public void Configure(
            FishingMiniGameFacade value,
            FishingModeController controller,
            FishingV3FishVisualPresenter fishPresenter)
        {
            facade = value;
            modeController = controller;
            fishVisualPresenter = fishPresenter;
            if (_targetAnchor == null && fishVisualPresenter != null)
            {
                _targetAnchor = fishVisualPresenter.PresentationAnchor;
            }
        }

        public void ConfigureLocalPlayer(Transform playerRoot, Transform targetAnchor)
        {
            if (_localPlayerRoot != playerRoot)
            {
                ReleaseStruggleTarget();
                CleanupRodVisual();
                ReleaseOwnedHandAnchor();
                _localPlayerRoot = playerRoot;
                _lastTimingJudgementSequence = 0;
                _timingReactionCount = 0;
            }

            _targetAnchor = targetAnchor;
        }

        /// <summary>
        /// Configures this presenter for a replicated remote player. The caller
        /// supplies semantic frames explicitly, so the local facade is not read.
        /// </summary>
        public void ConfigureRemotePlayer(Transform playerRoot, Transform targetAnchor)
        {
            _externalPresentationDriven = true;
            ConfigureLocalPlayer(playerRoot, targetAnchor);
        }

        public void RefreshNow()
        {
            RefreshLive(0f);
        }

        public void StepPresentation(
            FishingV3PlayerPresentationFrame frame,
            float deltaTime)
        {
            Refresh(frame, deltaTime);
        }

        public void StepRemotePresentation(
            FishingV3RemotePresentationFrame frame,
            float deltaTime)
        {
            _externalPresentationDriven = true;
            StepPresentation(
                new FishingV3PlayerPresentationFrame(
                    frame.IsFishing,
                    frame.IsPaused,
                    frame.GameplayPhase,
                    frame.Result,
                    FishingV3NetworkPresentationState.RepresentativeTensionFor(
                        frame.TensionZone),
                    0,
                    FishingV3TimingGrade.None,
                    frame.TensionZone),
                deltaTime);
        }

        public void ClearRemotePresentation()
        {
            StopStruggle(true);
            CleanupRodVisual();
            ReleaseOwnedHandAnchor();
            ReleaseStruggleTarget();
            _localPlayerRoot = null;
            _targetAnchor = null;
        }

        public void CleanupForCaughtPresentation()
        {
            StopStruggle(true);
            CleanupRodVisual();
        }

        public static float EvaluateStruggleTargetIntensity(
            float tensionNormalized,
            float minimumLeanDegrees = DefaultMinimumStruggleLeanDegrees,
            float maximumLeanDegrees = DefaultMaximumStruggleLeanDegrees,
            float curveExponent = DefaultStruggleIntensityCurveExponent)
        {
            float tension = IsFinite(tensionNormalized)
                ? Mathf.Clamp01(tensionNormalized)
                : 0f;
            float maximumLean = Mathf.Max(0.001f, SanitizeNonNegative(
                maximumLeanDegrees,
                DefaultMaximumStruggleLeanDegrees));
            float minimumLean = Mathf.Clamp(SanitizeNonNegative(
                minimumLeanDegrees,
                DefaultMinimumStruggleLeanDegrees), 0f, maximumLean);
            float exponent = Mathf.Max(0.1f, SanitizeNonNegative(
                curveExponent,
                DefaultStruggleIntensityCurveExponent));
            float minimumIntensity = minimumLean / maximumLean;
            return Mathf.Lerp(
                minimumIntensity,
                1f,
                Mathf.Pow(tension, exponent));
        }

        public static FishingV3StrugglePose EvaluateStrugglePose(
            float intensityNormalized,
            float phaseRadians,
            float maximumLeanDegrees = DefaultMaximumStruggleLeanDegrees,
            float minimumPositionMeters = 0.007f,
            float maximumPositionMeters = 0.035f,
            float minimumRollDegrees = 0.7f,
            float maximumRollDegrees = 4.2f)
        {
            float intensity = IsFinite(intensityNormalized)
                ? Mathf.Clamp01(intensityNormalized)
                : 0f;
            if (intensity <= 0f)
            {
                return new FishingV3StrugglePose(Vector3.zero, Vector3.zero);
            }

            float lean = SanitizeNonNegative(
                maximumLeanDegrees,
                DefaultMaximumStruggleLeanDegrees) * intensity;
            float minimumPosition = SanitizeNonNegative(
                minimumPositionMeters,
                0.007f);
            float maximumPosition = Mathf.Max(minimumPosition, SanitizeNonNegative(
                maximumPositionMeters,
                0.035f));
            float position = Mathf.Lerp(minimumPosition, maximumPosition, intensity);
            float minimumRoll = SanitizeNonNegative(minimumRollDegrees, 0.7f);
            float maximumRoll = Mathf.Max(minimumRoll, SanitizeNonNegative(
                maximumRollDegrees,
                4.2f));
            float roll = Mathf.Lerp(minimumRoll, maximumRoll, intensity);
            float phase = IsFinite(phaseRadians) && phaseRadians > 0f
                ? phaseRadians
                : 0f;
            float pullWave =
                0.7f * Mathf.Sin(phase) -
                0.3f * Mathf.Sin(phase * 2f + 0.65f);
            float pull = Mathf.Clamp01(0.5f + 0.5f * pullWave);
            float strain = 0.62f + 0.38f * pull;
            float verticalWave = Mathf.Sin(phase + 0.4f);
            float lateralWave = Mathf.Sin(phase + 0.75f);
            float rollWave = Mathf.Sin(phase + 1.1f);

            Vector3 positionOffset = new Vector3(
                lateralWave * position * 0.22f,
                verticalWave * position * 0.28f,
                -position * (0.48f + 0.52f * pull));
            Vector3 eulerOffset = new Vector3(
                -lean * strain,
                lateralWave * roll * 0.25f,
                rollWave * roll);
            return new FishingV3StrugglePose(positionOffset, eulerOffset);
        }

        public static float EvaluateStruggleTargetFrequencyHz(
            float tensionNormalized,
            float minimumFrequencyHz = DefaultMinimumStruggleFrequencyHz,
            float maximumFrequencyHz = DefaultMaximumStruggleFrequencyHz,
            float curveExponent = DefaultStruggleFrequencyCurveExponent)
        {
            float tension = IsFinite(tensionNormalized)
                ? Mathf.Clamp01(tensionNormalized)
                : 0f;
            float minimum = Mathf.Max(0.1f, SanitizeNonNegative(
                minimumFrequencyHz,
                DefaultMinimumStruggleFrequencyHz));
            float maximum = Mathf.Max(minimum, SanitizeNonNegative(
                maximumFrequencyHz,
                DefaultMaximumStruggleFrequencyHz));
            float exponent = Mathf.Max(0.1f, SanitizeNonNegative(
                curveExponent,
                DefaultStruggleFrequencyCurveExponent));
            return Mathf.Lerp(minimum, maximum, Mathf.Pow(tension, exponent));
        }

        public static float EvaluateRodBendMeters(
            float tensionNormalized,
            float maximumBendMeters = DefaultMaximumRodBendMeters)
        {
            float tension = IsFinite(tensionNormalized)
                ? Mathf.Clamp01(tensionNormalized)
                : 0f;
            float maximum = IsFinite(maximumBendMeters) && maximumBendMeters >= 0f
                ? maximumBendMeters
                : DefaultMaximumRodBendMeters;
            return maximum * Mathf.Pow(tension, 1.35f);
        }

        private void RefreshLive(float deltaTime)
        {
            ResolveDependencies();
            FishingV3Snapshot snapshot = facade != null ? facade.V3Current : null;
            bool ownsSession = snapshot != null &&
                _localPlayerRoot != null &&
                modeController != null &&
                (modeController.State == FishingModeLifecycleState.Active ||
                 modeController.State == FishingModeLifecycleState.Paused) &&
                modeController.CurrentInteractor == _localPlayerRoot.gameObject;
            FishingV3PlayerPresentationFrame frame = snapshot != null
                ? new FishingV3PlayerPresentationFrame(
                    ownsSession,
                    modeController != null && modeController.IsPaused,
                    snapshot.GameplayPhase,
                    snapshot.Result,
                    snapshot.TensionNormalized,
                    snapshot.TimingJudgementSequence,
                    snapshot.LastTimingGrade,
                    snapshot.TensionZone)
                : default;
            Refresh(frame, deltaTime);
        }

        private void Refresh(FishingV3PlayerPresentationFrame frame, float deltaTime)
        {
            if (!ShouldShowRod(frame))
            {
                StopStruggle(true);
                CleanupRodVisual();
                if (frame.TimingJudgementSequence < _lastTimingJudgementSequence)
                {
                    _lastTimingJudgementSequence = frame.TimingJudgementSequence;
                }
                return;
            }

            if (!EnsureRodVisual()) return;

            UpdateStruggle(frame, deltaTime);

            _currentTensionNormalized = IsFinite(frame.TensionNormalized)
                ? Mathf.Clamp01(frame.TensionNormalized)
                : 0f;
            _currentRodBendMeters = EvaluateRodBendMeters(
                _currentTensionNormalized,
                maximumRodBendMeters);
            ConsumeTimingJudgement(frame);

            if (!frame.IsPaused)
            {
                float step = IsFinite(deltaTime) && deltaTime > 0f ? deltaTime : 0f;
                _timingReactionRemainingSeconds = Mathf.Max(
                    0f,
                    _timingReactionRemainingSeconds - step);
            }

            ApplyStrugglePose();
            ApplyRodPose();
        }

        private void UpdateStruggle(
            FishingV3PlayerPresentationFrame frame,
            float deltaTime)
        {
            bool shouldStruggle =
                frame.GameplayPhase == FishingV3GameplayPhase.Fighting;
            if (!shouldStruggle)
            {
                StopStruggle(true);
                return;
            }

            _targetStruggleIntensity = EvaluateStruggleTargetIntensity(
                frame.TensionNormalized,
                minimumStruggleLeanDegrees,
                maximumStruggleLeanDegrees,
                struggleIntensityCurveExponent);
            _targetStruggleFrequencyHz = EvaluateStruggleTargetFrequencyHz(
                frame.TensionNormalized,
                minimumStruggleFrequencyHz,
                maximumStruggleFrequencyHz,
                struggleFrequencyCurveExponent);
            if (!_struggleActive)
            {
                _struggleActive = true;
                _currentStruggleIntensity = _targetStruggleIntensity;
                _currentStruggleFrequencyHz = _targetStruggleFrequencyHz;
                _strugglePhaseSeconds = 0f;
                _strugglePhaseRadians = 0f;
                _struggleActivationCount++;
            }

            EnsureStruggleTarget();
            if (!frame.IsPaused)
            {
                float step = IsFinite(deltaTime) && deltaTime > 0f ? deltaTime : 0f;
                _strugglePhaseSeconds += step;
                float response = Mathf.Max(0.1f, SanitizeNonNegative(
                    struggleIntensityResponsePerSecond,
                    DefaultStruggleIntensityResponsePerSecond));
                float blend = 1f - Mathf.Exp(-response * step);
                _currentStruggleIntensity = Mathf.Lerp(
                    _currentStruggleIntensity,
                    _targetStruggleIntensity,
                    blend);
                float frequencyResponse = Mathf.Max(0.1f, SanitizeNonNegative(
                    struggleFrequencyResponsePerSecond,
                    DefaultStruggleFrequencyResponsePerSecond));
                float frequencyBlend = 1f - Mathf.Exp(-frequencyResponse * step);
                _currentStruggleFrequencyHz = Mathf.Lerp(
                    _currentStruggleFrequencyHz,
                    _targetStruggleFrequencyHz,
                    frequencyBlend);
                _strugglePhaseRadians +=
                    step * _currentStruggleFrequencyHz * Mathf.PI * 2f;
            }
        }

        private void ApplyStrugglePose()
        {
            if (!_struggleActive || !EnsureStruggleTarget())
            {
                return;
            }

            FishingV3StrugglePose pose = EvaluateStrugglePose(
                _currentStruggleIntensity,
                _strugglePhaseRadians,
                maximumStruggleLeanDegrees,
                minimumStrugglePositionMeters,
                maximumStrugglePositionMeters,
                minimumStruggleRollDegrees,
                maximumStruggleRollDegrees);
            _struggleTarget.localPosition =
                _struggleBaseLocalPosition + pose.LocalPositionOffset;
            _struggleTarget.localRotation =
                _struggleBaseLocalRotation * Quaternion.Euler(pose.LocalEulerOffset);
        }

        private bool EnsureStruggleTarget()
        {
            if (_struggleTarget != null) return true;
            if (_localPlayerRoot == null) return false;

            Transform target = FindDescendant(
                _localPlayerRoot,
                PreferredStruggleTargetName);
            if (target == _localPlayerRoot) target = null;
            if (target == null)
            {
                Animator animator = _localPlayerRoot.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman)
                {
                    target = animator.GetBoneTransform(HumanBodyBones.Hips);
                }
            }
            if (target == null || target == _localPlayerRoot) return false;

            _struggleTarget = target;
            _struggleBaseLocalPosition = target.localPosition;
            _struggleBaseLocalRotation = target.localRotation;
            return true;
        }

        private void StopStruggle(bool resetPhase)
        {
            RestoreStruggleBasePose();
            _struggleActive = false;
            _currentStruggleIntensity = 0f;
            _targetStruggleIntensity = 0f;
            _currentStruggleFrequencyHz = 0f;
            _targetStruggleFrequencyHz = 0f;
            if (resetPhase)
            {
                _strugglePhaseSeconds = 0f;
                _strugglePhaseRadians = 0f;
            }
        }

        private void ReleaseStruggleTarget()
        {
            StopStruggle(true);
            _struggleTarget = null;
        }

        private void RestoreStruggleBasePose()
        {
            if (_struggleTarget == null) return;
            _struggleTarget.localPosition = _struggleBaseLocalPosition;
            _struggleTarget.localRotation = _struggleBaseLocalRotation;
        }

        private static bool ShouldShowRod(FishingV3PlayerPresentationFrame frame)
        {
            if (!frame.IsOwnSession || frame.Result != FishingV3Result.Active)
            {
                return false;
            }

            return frame.GameplayPhase == FishingV3GameplayPhase.WaitingForBite ||
                   frame.GameplayPhase == FishingV3GameplayPhase.HookWindow ||
                   frame.GameplayPhase == FishingV3GameplayPhase.Fighting;
        }

        private void ConsumeTimingJudgement(FishingV3PlayerPresentationFrame frame)
        {
            int current = frame.TimingJudgementSequence;
            if (current < _lastTimingJudgementSequence)
            {
                _lastTimingJudgementSequence = current;
                _timingReactionRemainingSeconds = 0f;
                return;
            }

            if (current == _lastTimingJudgementSequence) return;
            _lastTimingJudgementSequence = current;
            if (frame.GameplayPhase != FishingV3GameplayPhase.Fighting ||
                (frame.TimingGrade != FishingV3TimingGrade.Perfect &&
                 frame.TimingGrade != FishingV3TimingGrade.Good))
            {
                return;
            }

            _timingReactionRemainingSeconds = SanitizeNonNegative(
                timingReactionSeconds,
                DefaultTimingReactionSeconds);
            _timingReactionCount++;
        }

        private bool EnsureRodVisual()
        {
            if (HasRodVisual) return true;
            if (_localPlayerRoot == null) return false;

            _handAnchor = ResolveHandAnchor(_localPlayerRoot);
            if (_handAnchor == null) return false;

            CreateMaterials();
            _rodRoot = new GameObject("FishingRodPresentation");
            _rodRoot.transform.SetParent(_handAnchor, false);

            for (int index = 0; index < _rodSegments.Length; index++)
            {
                GameObject segment = CreatePrimitive(
                    $"Fishing Rod Segment {index + 1}",
                    PrimitiveType.Cylinder,
                    _rodRoot.transform,
                    _rodMaterial);
                _rodSegments[index] = segment.transform;
            }

            GameObject reelObject = CreatePrimitive(
                "Fishing Reel",
                PrimitiveType.Cylinder,
                _rodRoot.transform,
                _reelMaterial);
            _reel = reelObject.transform;
            _reel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _reel.localScale = new Vector3(0.16f, 0.08f, 0.16f);

            _rodTip = new GameObject("Fishing Rod Tip").transform;
            _rodTip.SetParent(_rodRoot.transform, false);
            ApplyRodPose();
            return true;
        }

        private void ApplyRodPose()
        {
            if (!HasRodVisual || _handAnchor == null) return;

            Vector3 origin = _handAnchor.position + handGripWorldOffset;
            Vector3 target = ResolveTargetPosition(origin);
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = _localPlayerRoot != null
                    ? _localPlayerRoot.forward
                    : Vector3.forward;
            }

            float reactionDuration = SanitizeNonNegative(
                timingReactionSeconds,
                DefaultTimingReactionSeconds);
            float reaction = reactionDuration > 0f
                ? Mathf.Clamp01(_timingReactionRemainingSeconds / reactionDuration)
                : 0f;
            float easedReaction = reaction * reaction * (3f - 2f * reaction);

            Transform rodTransform = _rodRoot.transform;
            rodTransform.position = origin;
            rodTransform.rotation =
                Quaternion.LookRotation(direction.normalized, Vector3.up) *
                Quaternion.Euler(
                    -GetBaseRodUpwardAngleDegrees() -
                    easedReaction * timingPullDegrees,
                    0f,
                    0f);
            rodTransform.localScale = Vector3.one;

            float length = Mathf.Max(0.25f, rodLength);
            float bend = _currentRodBendMeters;
            Vector3 start = Vector3.zero;
            Vector3 lowerEnd = new Vector3(0f, -bend * 0.08f, length * 0.34f);
            Vector3 middleEnd = new Vector3(0f, -bend * 0.38f, length * 0.68f);
            Vector3 tip = new Vector3(0f, -bend, length);
            PositionCylinder(_rodSegments[0], start, lowerEnd, rodBaseRadius);
            PositionCylinder(_rodSegments[1], lowerEnd, middleEnd, rodBaseRadius * 0.78f);
            PositionCylinder(_rodSegments[2], middleEnd, tip, rodBaseRadius * 0.56f);
            _rodTip.localPosition = tip;
            _rodTip.localRotation = Quaternion.identity;
            _reel.localPosition = new Vector3(0.11f, -0.09f, 0.15f);
            _reel.localRotation = Quaternion.Euler(
                90f,
                0f,
                easedReaction * 24f);
        }

        private Vector3 ResolveTargetPosition(Vector3 origin)
        {
            Transform target = _externalPresentationDriven
                ? _targetAnchor
                : fishVisualPresenter != null
                    ? fishVisualPresenter.PresentationAnchor
                    : _targetAnchor;
            if (target == null && fishVisualPresenter != null)
            {
                target = fishVisualPresenter.PresentationAnchor;
            }
            if (target == null && modeController != null && modeController.CurrentSpot != null)
            {
                target = modeController.CurrentSpot.transform;
            }

            return target != null
                ? target.position + Vector3.up * targetHeightOffset
                : origin + (_localPlayerRoot != null
                    ? _localPlayerRoot.forward
                    : Vector3.forward) * Mathf.Max(1f, rodLength);
        }

        private Transform ResolveHandAnchor(Transform playerRoot)
        {
            if (playerRoot == null) return null;
            Transform named = FindDescendant(playerRoot, PreferredHandAnchorName);
            if (named != null) return named;

            Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand != null) return rightHand;
            }

            Transform existing = FindDescendant(playerRoot, FallbackHandAnchorName);
            if (existing != null) return existing;

            GameObject fallback = new GameObject(FallbackHandAnchorName);
            fallback.transform.SetParent(playerRoot, false);
            fallback.transform.localPosition = fallbackAnchorLocalPosition;
            fallback.transform.localRotation = Quaternion.identity;
            _ownsHandAnchor = true;
            return fallback.transform;
        }

        private void ResolveDependencies()
        {
            if (facade == null) facade = GetComponent<FishingMiniGameFacade>();
            if (modeController == null) modeController = GetComponent<FishingModeController>();
            if (fishVisualPresenter == null)
            {
                fishVisualPresenter = GetComponent<FishingV3FishVisualPresenter>();
            }
        }

        public void CleanupRodVisual()
        {
            if (_rodRoot != null)
            {
                _rodRoot.SetActive(false);
                DestroyUnityObject(_rodRoot);
            }

            _rodRoot = null;
            for (int index = 0; index < _rodSegments.Length; index++)
            {
                _rodSegments[index] = null;
            }
            _rodTip = null;
            _reel = null;
            DestroyMaterial(ref _rodMaterial);
            DestroyMaterial(ref _reelMaterial);
            _timingReactionRemainingSeconds = 0f;
            _currentTensionNormalized = 0f;
            _currentRodBendMeters = 0f;
        }

        private void ReleaseOwnedHandAnchor()
        {
            if (_ownsHandAnchor && _handAnchor != null)
            {
                GameObject owned = _handAnchor.gameObject;
                owned.SetActive(false);
                DestroyUnityObject(owned);
            }
            _handAnchor = null;
            _ownsHandAnchor = false;
        }

        private void CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Sprites/Default");
            if (shader == null) return;

            _rodMaterial = new Material(shader) { name = "Fishing Rod Runtime Material" };
            _reelMaterial = new Material(shader) { name = "Fishing Reel Runtime Material" };
            ApplyMaterialColor(_rodMaterial, rodColor);
            ApplyMaterialColor(_reelMaterial, reelColor);
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static GameObject CreatePrimitive(
            string objectName,
            PrimitiveType type,
            Transform parent,
            Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                DestroyUnityObject(collider);
            }
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return instance;
        }

        private static void PositionCylinder(
            Transform cylinder,
            Vector3 start,
            Vector3 end,
            float radius)
        {
            if (cylinder == null) return;
            Vector3 vector = end - start;
            float length = Mathf.Max(0.0001f, vector.magnitude);
            cylinder.localPosition = (start + end) * 0.5f;
            cylinder.localRotation = Quaternion.FromToRotation(Vector3.up, vector / length);
            float diameter = Mathf.Max(0.001f, radius * 2f);
            cylinder.localScale = new Vector3(diameter, length * 0.5f, diameter);
        }

        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrWhiteSpace(exactName)) return null;
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                Transform candidate = descendants[index];
                if (candidate != null &&
                    string.Equals(candidate.name, exactName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f ? value : fallback;
        }

        private float GetBaseRodUpwardAngleDegrees()
        {
            float angle = IsFinite(baseRodUpwardAngleDegrees)
                ? baseRodUpwardAngleDegrees
                : DefaultBaseRodUpwardAngleDegrees;
            return Mathf.Clamp(angle, 0f, 45f);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null) DestroyUnityObject(material);
            material = null;
        }

        private static void DestroyUnityObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
