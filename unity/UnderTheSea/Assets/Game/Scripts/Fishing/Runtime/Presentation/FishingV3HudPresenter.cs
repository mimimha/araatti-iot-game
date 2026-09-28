using FishingMiniGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Read-only presentation adapter for the public Fishing V3 snapshot.
    /// It never advances gameplay or derives gameplay values from input/state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingV3HudPresenter : MonoBehaviour
    {
        [SerializeField] private FishingMiniGameFacade facade;
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Image tensionFill;
        [SerializeField] private Image captureFill;
        [SerializeField] private Text tensionValueLabel;
        [SerializeField] private Text captureValueLabel;
        [SerializeField] private Font hudFont;
        [SerializeField] private Sprite roundedPanelSprite;
        [SerializeField] private Sprite capsuleSprite;
        [SerializeField] private Sprite gaugeTrackSprite;
        [SerializeField] private Sprite gaugeFillSprite;
        [SerializeField] private Sprite tensionMarkerSprite;
        [SerializeField, Min(0.1f)] private float resultDisplaySeconds = 1.75f;
        [SerializeField, Min(0f)] private float caughtResultDelaySeconds =
            FishingV3FishVisualPresenter.DefaultCaughtDisplaySeconds;

        private static readonly Color PanelColor = new Color(0.07f, 0.17f, 0.23f, 0.96f);
        private static readonly Color OuterFrameColor = new Color(0.57f, 0.84f, 0.93f, 1f);
        private static readonly Color InnerFrameColor = new Color(0.24f, 0.39f, 0.48f, 0.97f);
        private static readonly Color InnerHighlightColor = new Color(1f, 1f, 1f, 0.74f);
        private static readonly Color BorderColor = new Color(0.45f, 0.72f, 0.82f, 0.92f);
        private static readonly Color PrimaryTextColor = new Color(0.95f, 0.98f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new Color(0.68f, 0.79f, 0.83f, 1f);
        private static readonly Color CaptureColor = new Color(0.30f, 0.92f, 0.97f, 1f);
        private static readonly Color CaptureTextColor = new Color(0.32f, 0.92f, 0.98f, 1f);
        private static readonly Color GaugeTrackColor = new Color(0.43f, 0.56f, 0.62f, 1f);

        private GameObject _timingRoot;
        private RectTransform _timingGoodZone;
        private RectTransform _timingPerfectZone;
        private RectTransform _timingPointer;
        private RectTransform _timingPointerCap;
        private RectTransform _timingRail;
        private Text _timingPromptLabel;
        private Text _timingGoodLabel;
        private Text _timingPerfectLabel;
        private GameObject _eventMessageRoot;
        private RectTransform _eventMessageRect;
        private Image _eventMessageBackground;
        private Outline _eventMessageOutline;
        private Text _timingResultLabel;
        private GameObject _tensionStatusRoot;
        private Image _tensionStatusBackground;
        private Text _tensionZoneLabel;
        private Text _tensionHintLabel;
        private GameObject _fightingGaugeRoot;
        private RectTransform _tensionCardRoot;
        private RectTransform _captureCardRoot;
        private RectTransform _tensionRiskBand;
        private RectTransform _tensionMarker;
        private Image _tensionMarkerImage;
        private Text _tensionLowLabel;
        private Text _tensionDangerLabel;
        private Text _captureStartLabel;
        private Text _captureEndLabel;
        private GameObject _resultOverlayRoot;
        private Image _resultOverlayBackground;
        private Outline _resultOverlayOutline;
        private Text _resultTitleLabel;
        private Text _resultDescriptionLabel;
        private bool _hasConsumedTerminalResult;
        private float _resultTimeRemainingSeconds;
        private bool _isCaughtResultPending;
        private float _caughtResultDelayRemainingSeconds;

        public float DisplayedTensionNormalized { get; private set; }
        public float DisplayedCaptureProgressNormalized { get; private set; }
        public float DisplayedTensionMarkerNormalized { get; private set; }
        public float DisplayedTimingPointerNormalized { get; private set; }
        public FishingV3TimingGrade DisplayedTimingGrade { get; private set; }
        public FishingV3GameplayPhase DisplayedGameplayPhase { get; private set; }
        public string DisplayedPhaseMessage { get; private set; } = string.Empty;
        public FishingV3TensionZone DisplayedTensionZone { get; private set; }
        public string DisplayedTensionZoneLabel { get; private set; } = string.Empty;
        public string DisplayedTensionHint { get; private set; } = string.Empty;
        public Color DisplayedTensionColor { get; private set; } = Color.white;
        public FishingV3Result DisplayedResult { get; private set; } =
            FishingV3Result.Active;
        public string DisplayedResultTitle { get; private set; } = string.Empty;
        public string DisplayedResultDescription { get; private set; } = string.Empty;
        public float ResultDisplaySeconds => resultDisplaySeconds;
        public float CaughtResultDelaySeconds => caughtResultDelaySeconds;
        public bool IsCaughtResultPending => _isCaughtResultPending;
        public float CaughtResultDelayRemainingSeconds =>
            _caughtResultDelayRemainingSeconds;
        public float ResultTimeRemainingSeconds => _resultTimeRemainingSeconds;
        public int ResultPresentationSequence { get; private set; }
        public bool IsHudVisible => hudRoot != null && hudRoot.activeSelf;
        public bool IsTensionStatusVisible =>
            _tensionStatusRoot != null && _tensionStatusRoot.activeSelf;
        public bool AreFightGaugesVisible => _fightingGaugeRoot != null
            ? _fightingGaugeRoot.activeSelf
            : (tensionFill == null || tensionFill.gameObject.activeSelf) &&
              (captureFill == null || captureFill.gameObject.activeSelf);
        public bool HasConfiguredView => hudRoot != null && tensionFill != null && captureFill != null;
        public bool HasHudStyleAssets => roundedPanelSprite != null &&
            capsuleSprite != null &&
            gaugeTrackSprite != null &&
            gaugeFillSprite != null &&
            tensionMarkerSprite != null;
        public bool HasTensionRiskGauge => _tensionRiskBand != null &&
            _tensionMarker != null &&
            _tensionMarkerImage != null &&
            _tensionLowLabel != null &&
            _tensionDangerLabel != null;
        public bool HasCaptureProgressScale => _captureStartLabel != null &&
            _captureEndLabel != null;
        public bool HasTensionStatusView => _tensionStatusRoot != null &&
            _tensionStatusBackground != null &&
            _tensionZoneLabel != null &&
            _tensionHintLabel != null;
        public bool HasResultOverlayView => _resultOverlayRoot != null &&
            _resultOverlayBackground != null &&
            _resultTitleLabel != null &&
            _resultDescriptionLabel != null;
        public bool IsResultOverlayVisible =>
            _resultOverlayRoot != null && _resultOverlayRoot.activeSelf;
        public bool HasTimingView => _timingRoot != null &&
            _timingGoodZone != null &&
            _timingPerfectZone != null &&
            _timingPointer != null &&
            _timingPointerCap != null &&
            _timingRail != null &&
            _timingPromptLabel != null &&
            _eventMessageRoot != null &&
            _eventMessageRect != null &&
            _eventMessageBackground != null &&
            _timingResultLabel != null;

        public void Configure(
            FishingMiniGameFacade snapshotSource,
            GameObject root,
            Image tensionGaugeFill,
            Image captureGaugeFill,
            Text tensionLabel = null,
            Text captureLabel = null)
        {
            facade = snapshotSource;
            hudRoot = root;
            tensionFill = tensionGaugeFill;
            captureFill = captureGaugeFill;
            tensionValueLabel = tensionLabel;
            captureValueLabel = captureLabel;
            ConfigureFill(tensionFill);
            ConfigureFill(captureFill);
            ResolveFightingGaugeRoot();
            EnsureGaugeCardViews();
            EnsureTensionStatusView();
            EnsureTimingView();
            EnsureResultOverlayView();
            HideAllAndReset();
        }

        private void Awake()
        {
            ResolveFacade();
            ConfigureFill(tensionFill);
            ConfigureFill(captureFill);
            ResolveFightingGaugeRoot();
            EnsureGaugeCardViews();
            EnsureTensionStatusView();
            EnsureTimingView();
            EnsureResultOverlayView();
            RefreshNow();
        }

        private void OnEnable()
        {
            RefreshNow();
        }

        private void Update()
        {
            RefreshNow();
            AdvancePresentation(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            HideFightingAndReset();
            CancelPendingCaughtResult();
            HideResultOverlay();
            _hasConsumedTerminalResult = true;
            SetVisible(false);
        }

        public void RefreshNow()
        {
            ResolveFacade();
            FishingV3Snapshot snapshot = facade != null ? facade.V3Current : null;
            if (!HasConfiguredView ||
                facade == null ||
                facade.GameplayRuntimeMode != FishingGameplayRuntimeMode.V3 ||
                snapshot == null)
            {
                HideAllAndReset();
                return;
            }

            switch (snapshot.RuntimeState)
            {
                case FishingV3RuntimeState.Running:
                case FishingV3RuntimeState.Paused:
                    ResetResultForActiveSession();
                    SetVisible(true);
                    ApplySnapshot(snapshot);
                    break;

                case FishingV3RuntimeState.Completed:
                    PresentTerminalResult(snapshot.Result);
                    break;

                default:
                    HideAllAndReset();
                    break;
            }
        }

        public void ConfigureResultDisplayDuration(float seconds)
        {
            resultDisplaySeconds = SanitizeResultDisplaySeconds(seconds);
        }

        public void ConfigureCaughtResultDelay(float seconds)
        {
            caughtResultDelaySeconds = SanitizeCaughtResultDelay(seconds);
        }

        public void AdvancePresentation(float deltaTime)
        {
            if (float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime) ||
                deltaTime <= 0f)
            {
                return;
            }

            if (_isCaughtResultPending)
            {
                _caughtResultDelayRemainingSeconds = Mathf.Max(
                    0f,
                    _caughtResultDelayRemainingSeconds - deltaTime);
                if (_caughtResultDelayRemainingSeconds <= 0f)
                {
                    _isCaughtResultPending = false;
                    ShowResultOverlay(FishingV3Result.Caught);
                    SetVisible(IsResultOverlayVisible);
                }
                return;
            }

            if (!IsResultOverlayVisible) return;

            _resultTimeRemainingSeconds = Mathf.Max(
                0f,
                _resultTimeRemainingSeconds - deltaTime);
            if (_resultTimeRemainingSeconds > 0f) return;

            HideResultOverlay();
            SetVisible(false);
        }

        public static float NormalizeGaugeValue(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);
        }

        public static void ApplyGaugeFill(Image fill, float value)
        {
            if (fill == null) return;

            float normalized = NormalizeGaugeValue(value);
            fill.type = Image.Type.Simple;
            fill.fillAmount = normalized;

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(normalized, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
        }

        public static string GetTensionZoneLabel(FishingV3TensionZone zone)
        {
            switch (zone)
            {
                case FishingV3TensionZone.Safe:
                    return "안전";
                case FishingV3TensionZone.High:
                    return "주의";
                case FishingV3TensionZone.Danger:
                    return "위험";
                case FishingV3TensionZone.Low:
                case FishingV3TensionZone.Slack:
                default:
                    return "느슨함";
            }
        }

        public static string GetTensionHint(FishingV3TensionZone zone)
        {
            switch (zone)
            {
                case FishingV3TensionZone.Safe:
                    return "계속 감으세요!";
                case FishingV3TensionZone.High:
                    return "천천히!";
                case FishingV3TensionZone.Danger:
                    return "릴링을 멈추세요!";
                case FishingV3TensionZone.Low:
                case FishingV3TensionZone.Slack:
                default:
                    return "지금 감으세요!";
            }
        }

        public static Color GetTensionZoneColor(FishingV3TensionZone zone)
        {
            switch (zone)
            {
                case FishingV3TensionZone.Safe:
                    return new Color(0.31f, 0.88f, 0.56f, 1f);
                case FishingV3TensionZone.High:
                    return new Color(1f, 0.64f, 0.20f, 1f);
                case FishingV3TensionZone.Danger:
                    return new Color(0.95f, 0.24f, 0.31f, 1f);
                case FishingV3TensionZone.Low:
                case FishingV3TensionZone.Slack:
                default:
                    return new Color(0.29f, 0.63f, 1f, 1f);
            }
        }

        public static string GetResultTitle(FishingV3Result result)
        {
            switch (result)
            {
                case FishingV3Result.Caught:
                    return "잡았다!";
                case FishingV3Result.LineBroken:
                    return "줄이 끊어졌다!";
                case FishingV3Result.FishEscaped:
                    return "물고기가 도망쳤다!";
                default:
                    return string.Empty;
            }
        }

        public static string GetResultDescription(FishingV3Result result)
        {
            switch (result)
            {
                case FishingV3Result.Caught:
                    return "멋진 낚시였어요!";
                case FishingV3Result.LineBroken:
                    return "장력이 너무 높았어요!";
                case FishingV3Result.FishEscaped:
                    return "줄이 너무 느슨했어요!";
                default:
                    return string.Empty;
            }
        }

        public static Color GetResultColor(FishingV3Result result)
        {
            switch (result)
            {
                case FishingV3Result.Caught:
                    return new Color(0.31f, 0.88f, 0.56f, 1f);
                case FishingV3Result.LineBroken:
                    return new Color(0.95f, 0.24f, 0.31f, 1f);
                case FishingV3Result.FishEscaped:
                    return new Color(1f, 0.72f, 0.22f, 1f);
                default:
                    return PrimaryTextColor;
            }
        }

        private void ApplySnapshot(FishingV3Snapshot snapshot)
        {
            DisplayedTensionNormalized = NormalizeGaugeValue(snapshot.TensionNormalized);
            DisplayedCaptureProgressNormalized = NormalizeGaugeValue(
                snapshot.CaptureProgressNormalized);
            SetGauge(tensionFill, tensionValueLabel, DisplayedTensionNormalized);
            SetGauge(captureFill, captureValueLabel, DisplayedCaptureProgressNormalized);
            ApplyTensionMarker(DisplayedTensionNormalized);
            SetFightingGaugesVisible(
                snapshot.GameplayPhase == FishingV3GameplayPhase.Fighting);
            ApplyTensionSnapshot(snapshot);
            ApplyTimingSnapshot(snapshot);
        }

        private void HideFightingAndReset()
        {
            DisplayedTensionNormalized = 0f;
            DisplayedCaptureProgressNormalized = 0f;
            DisplayedTensionMarkerNormalized = 0f;
            DisplayedTimingPointerNormalized = 0f;
            DisplayedTimingGrade = FishingV3TimingGrade.None;
            DisplayedGameplayPhase = FishingV3GameplayPhase.Terminal;
            DisplayedPhaseMessage = string.Empty;
            DisplayedTensionZone = FishingV3TensionZone.Slack;
            DisplayedTensionZoneLabel = string.Empty;
            DisplayedTensionHint = string.Empty;
            DisplayedTensionColor = GetTensionZoneColor(FishingV3TensionZone.Slack);
            SetGauge(tensionFill, tensionValueLabel, 0f);
            SetGauge(captureFill, captureValueLabel, 0f);
            ApplyTensionMarker(0f);
            SetFightingGaugesVisible(false);
            if (_tensionStatusRoot != null) _tensionStatusRoot.SetActive(false);
            if (_timingRoot != null) _timingRoot.SetActive(false);
            if (_eventMessageRoot != null) _eventMessageRoot.SetActive(false);
        }

        private void HideAllAndReset()
        {
            HideFightingAndReset();
            CancelPendingCaughtResult();
            HideResultOverlay();
            _hasConsumedTerminalResult = false;
            SetVisible(false);
        }

        private void ResetResultForActiveSession()
        {
            CancelPendingCaughtResult();
            HideResultOverlay();
            _hasConsumedTerminalResult = false;
        }

        private void PresentTerminalResult(FishingV3Result result)
        {
            HideFightingAndReset();
            if (!IsTerminalResult(result))
            {
                CancelPendingCaughtResult();
                HideResultOverlay();
                SetVisible(false);
                return;
            }

            if (!_hasConsumedTerminalResult)
            {
                _hasConsumedTerminalResult = true;
                if (result == FishingV3Result.Caught &&
                    SanitizeCaughtResultDelay(caughtResultDelaySeconds) > 0f)
                {
                    ScheduleCaughtResult();
                }
                else
                {
                    ShowResultOverlay(result);
                }
            }

            SetVisible(IsResultOverlayVisible);
        }

        private void ScheduleCaughtResult()
        {
            HideResultOverlay();
            _caughtResultDelayRemainingSeconds = SanitizeCaughtResultDelay(
                caughtResultDelaySeconds);
            _isCaughtResultPending = _caughtResultDelayRemainingSeconds > 0f;
        }

        private void CancelPendingCaughtResult()
        {
            _isCaughtResultPending = false;
            _caughtResultDelayRemainingSeconds = 0f;
        }

        private void ShowResultOverlay(FishingV3Result result)
        {
            EnsureResultOverlayView();
            if (!HasResultOverlayView) return;

            DisplayedResult = result;
            DisplayedResultTitle = GetResultTitle(result);
            DisplayedResultDescription = GetResultDescription(result);
            Color accent = GetResultColor(result);
            _resultTitleLabel.text = DisplayedResultTitle;
            _resultTitleLabel.color = accent;
            _resultDescriptionLabel.text = DisplayedResultDescription;
            _resultOverlayBackground.color = new Color(
                PanelColor.r,
                PanelColor.g,
                PanelColor.b,
                0.96f);
            if (_resultOverlayOutline != null)
            {
                _resultOverlayOutline.effectColor = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    0.55f);
            }
            _resultTimeRemainingSeconds = SanitizeResultDisplaySeconds(
                resultDisplaySeconds);
            if (ResultPresentationSequence < int.MaxValue)
            {
                ResultPresentationSequence++;
            }
            _resultOverlayRoot.SetActive(true);
        }

        private void HideResultOverlay()
        {
            _resultTimeRemainingSeconds = 0f;
            DisplayedResult = FishingV3Result.Active;
            DisplayedResultTitle = string.Empty;
            DisplayedResultDescription = string.Empty;
            if (_resultTitleLabel != null) _resultTitleLabel.text = string.Empty;
            if (_resultDescriptionLabel != null) _resultDescriptionLabel.text = string.Empty;
            if (_resultOverlayRoot != null) _resultOverlayRoot.SetActive(false);
        }

        private void ApplyTensionSnapshot(FishingV3Snapshot snapshot)
        {
            EnsureTensionStatusView();
            DisplayedTensionZone = snapshot.TensionZone;
            DisplayedTensionZoneLabel = GetTensionZoneLabel(snapshot.TensionZone);
            DisplayedTensionHint = GetTensionHint(snapshot.TensionZone);
            DisplayedTensionColor = GetTensionZoneColor(snapshot.TensionZone);

            bool visible = snapshot.GameplayPhase == FishingV3GameplayPhase.Fighting;
            if (_tensionStatusRoot != null) _tensionStatusRoot.SetActive(visible);
            if (tensionFill != null) tensionFill.color = DisplayedTensionColor;
            if (_tensionZoneLabel != null)
            {
                _tensionZoneLabel.text = DisplayedTensionZoneLabel;
                _tensionZoneLabel.color = DisplayedTensionColor;
            }
            if (_tensionHintLabel != null)
            {
                _tensionHintLabel.text = DisplayedTensionHint;
                _tensionHintLabel.color = SecondaryTextColor;
            }
            if (_tensionStatusBackground != null)
            {
                _tensionStatusBackground.color = _tensionCardRoot != null
                    ? new Color(0f, 0f, 0f, 0f)
                    : PanelColor;
            }
        }

        private void ApplyTimingSnapshot(FishingV3Snapshot snapshot)
        {
            EnsureTimingView();
            bool visible = snapshot.ReelControlMode == FishingV3ReelControlMode.Timing &&
                snapshot.GameplayPhase != FishingV3GameplayPhase.Terminal;
            bool showTimingMeter = visible &&
                snapshot.GameplayPhase == FishingV3GameplayPhase.Fighting;
            bool showMomentaryMessage = visible &&
                (snapshot.GameplayPhase != FishingV3GameplayPhase.Fighting ||
                 snapshot.LastTimingGrade != FishingV3TimingGrade.None);
            if (_timingRoot != null) _timingRoot.SetActive(showTimingMeter);
            if (_timingPromptLabel != null)
            {
                _timingPromptLabel.gameObject.SetActive(showTimingMeter);
            }
            if (_eventMessageRoot != null)
            {
                _eventMessageRoot.SetActive(showMomentaryMessage);
                if (showMomentaryMessage) _eventMessageRoot.transform.SetAsLastSibling();
            }
            if (!visible || !HasTimingView) return;

            DisplayedGameplayPhase = snapshot.GameplayPhase;
            if (snapshot.GameplayPhase == FishingV3GameplayPhase.WaitingForBite ||
                snapshot.GameplayPhase == FishingV3GameplayPhase.HookWindow)
            {
                bool hookWindow = snapshot.GameplayPhase == FishingV3GameplayPhase.HookWindow;
                _timingGoodZone.gameObject.SetActive(false);
                _timingPerfectZone.gameObject.SetActive(false);
                _timingPointer.gameObject.SetActive(false);
                DisplayedTimingPointerNormalized = 0f;
                DisplayedTimingGrade = FishingV3TimingGrade.None;
                DisplayedPhaseMessage = hookWindow ? "지금!" : "입질을 기다리는 중...";
                _timingResultLabel.text = DisplayedPhaseMessage;
                ApplyTimingFeedbackStyle(snapshot.GameplayPhase, DisplayedTimingGrade);
                PositionEventMessage(snapshot.GameplayPhase, DisplayedTimingGrade);
                return;
            }

            _timingGoodZone.gameObject.SetActive(true);
            _timingPerfectZone.gameObject.SetActive(true);
            _timingPointer.gameObject.SetActive(true);

            DisplayedTimingPointerNormalized = NormalizeGaugeValue(
                snapshot.TimingPointerNormalized);
            DisplayedTimingGrade = snapshot.LastTimingGrade;
            DisplayedPhaseMessage = DisplayedTimingGrade == FishingV3TimingGrade.None
                ? "J를 누르세요"
                : DisplayedTimingGrade.ToString().ToUpperInvariant();

            SetZone(_timingGoodZone, snapshot.TimingGoodHalfWidthNormalized);
            SetZone(_timingPerfectZone, snapshot.TimingPerfectHalfWidthNormalized);
            _timingPointer.anchorMin = new Vector2(DisplayedTimingPointerNormalized, 0.5f);
            _timingPointer.anchorMax = new Vector2(DisplayedTimingPointerNormalized, 0.5f);
            _timingPointer.pivot = new Vector2(0.5f, 0.5f);
            _timingPointer.sizeDelta = new Vector2(4f, 38f);
            _timingPointer.anchoredPosition = Vector2.zero;
            _timingResultLabel.text = DisplayedPhaseMessage;
            ApplyTimingFeedbackStyle(snapshot.GameplayPhase, DisplayedTimingGrade);
            PositionEventMessage(snapshot.GameplayPhase, DisplayedTimingGrade);
        }

        private void EnsureTimingView()
        {
            if (hudRoot == null || HasTimingView) return;

            _timingRoot = CreateUiObject("TimingMeter", hudRoot.transform, out RectTransform meter);
            meter.anchorMin = new Vector2(0.5f, 0f);
            meter.anchorMax = new Vector2(0.5f, 0f);
            meter.pivot = new Vector2(0.5f, 0f);
            meter.anchoredPosition = new Vector2(0f, 34f);
            meter.sizeDelta = new Vector2(600f, 112f);
            EnsureLayeredPanel(_timingRoot, PanelColor, out _);

            GameObject prompt = CreateUiObject(
                "TimingPrompt",
                meter,
                out RectTransform promptRect);
            promptRect.anchorMin = new Vector2(0.5f, 1f);
            promptRect.anchorMax = new Vector2(0.5f, 1f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.anchoredPosition = new Vector2(0f, -8f);
            promptRect.sizeDelta = new Vector2(180f, 20f);
            _timingPromptLabel = prompt.AddComponent<Text>();
            ConfigureStatusText(
                _timingPromptLabel,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _timingPromptLabel.text = "J를 누르세요";
            _timingPromptLabel.color = PrimaryTextColor;

            GameObject rail = CreateUiObject(
                "TimingRail",
                meter,
                out _timingRail);
            _timingRail.anchorMin = new Vector2(0.5f, 0f);
            _timingRail.anchorMax = new Vector2(0.5f, 0f);
            _timingRail.pivot = new Vector2(0.5f, 0.5f);
            _timingRail.anchoredPosition = new Vector2(0f, 39f);
            _timingRail.sizeDelta = new Vector2(520f, 30f);
            Image railImage = rail.AddComponent<Image>();
            ApplyGaugeTrackStyle(railImage);
            railImage.raycastTarget = false;
            AddSubtleOutline(rail, new Color(0.62f, 0.80f, 0.86f, 0.66f));

            GameObject good = CreateUiObject("GoodZone", _timingRail, out _timingGoodZone);
            Image goodImage = good.AddComponent<Image>();
            goodImage.color = new Color(0.27f, 0.88f, 0.56f, 0.96f);

            GameObject perfect = CreateUiObject(
                "PerfectZone",
                _timingRail,
                out _timingPerfectZone);
            Image perfectImage = perfect.AddComponent<Image>();
            perfectImage.color = new Color(1f, 0.85f, 0.25f, 1f);
            EnsureGaugeShine(_timingRail, "TimingRailShine", 4f);

            GameObject pointer = CreateUiObject("Pointer", _timingRail, out _timingPointer);
            _timingPointer.sizeDelta = new Vector2(4f, 38f);
            Image pointerImage = pointer.AddComponent<Image>();
            pointerImage.color = new Color(0.96f, 0.97f, 0.93f, 1f);
            pointerImage.raycastTarget = false;
            AddSubtleOutline(pointer, new Color(0.01f, 0.03f, 0.04f, 0.9f));

            GameObject pointerCap = CreateUiObject(
                "PointerCap",
                _timingPointer,
                out _timingPointerCap);
            _timingPointerCap.anchorMin = new Vector2(0.5f, 1f);
            _timingPointerCap.anchorMax = new Vector2(0.5f, 1f);
            _timingPointerCap.pivot = new Vector2(0.5f, 0f);
            _timingPointerCap.anchoredPosition = new Vector2(0f, 1f);
            _timingPointerCap.sizeDelta = new Vector2(15f, 13f);
            Image pointerCapImage = pointerCap.AddComponent<Image>();
            pointerCapImage.sprite = tensionMarkerSprite;
            pointerCapImage.type = Image.Type.Simple;
            pointerCapImage.preserveAspect = true;
            pointerCapImage.color = new Color(1f, 0.94f, 0.72f, 1f);
            pointerCapImage.raycastTarget = false;
            AddSubtleOutline(pointerCap, new Color(0.16f, 0.11f, 0.03f, 0.92f));

            GameObject goodLegend = CreateUiObject(
                "GoodLabel",
                _timingRail,
                out RectTransform goodLegendRect);
            goodLegendRect.anchorMin = new Vector2(0.25f, 1f);
            goodLegendRect.anchorMax = new Vector2(0.43f, 1f);
            goodLegendRect.pivot = new Vector2(0.5f, 0f);
            goodLegendRect.anchoredPosition = new Vector2(0f, 4f);
            goodLegendRect.sizeDelta = new Vector2(0f, 14f);
            _timingGoodLabel = goodLegend.AddComponent<Text>();
            ConfigureStatusText(
                _timingGoodLabel,
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _timingGoodLabel.text = "GOOD";
            _timingGoodLabel.color = new Color(0.31f, 0.88f, 0.56f, 1f);

            GameObject perfectLegend = CreateUiObject(
                "PerfectLabel",
                _timingRail,
                out RectTransform perfectLegendRect);
            perfectLegendRect.anchorMin = new Vector2(0.43f, 1f);
            perfectLegendRect.anchorMax = new Vector2(0.57f, 1f);
            perfectLegendRect.pivot = new Vector2(0.5f, 0f);
            perfectLegendRect.anchoredPosition = new Vector2(0f, 4f);
            perfectLegendRect.sizeDelta = new Vector2(0f, 14f);
            _timingPerfectLabel = perfectLegend.AddComponent<Text>();
            ConfigureStatusText(
                _timingPerfectLabel,
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _timingPerfectLabel.text = "PERFECT";
            _timingPerfectLabel.color = new Color(1f, 0.82f, 0.24f, 1f);

            Text controls = FindNamedText(_fightingGaugeRoot, "Controls");
            if (controls != null)
            {
                controls.text = string.Empty;
                controls.gameObject.SetActive(false);
            }

            _eventMessageRoot = CreateUiObject(
                "FishingEventMessage",
                hudRoot.transform,
                out _eventMessageRect);
            _eventMessageRect.anchorMin = new Vector2(0.5f, 0.5f);
            _eventMessageRect.anchorMax = new Vector2(0.5f, 0.5f);
            _eventMessageRect.pivot = new Vector2(0.5f, 0.5f);
            _eventMessageRect.anchoredPosition = new Vector2(0f, 80f);
            _eventMessageRect.sizeDelta = new Vector2(300f, 76f);
            _eventMessageBackground = EnsureLayeredPanel(
                _eventMessageRoot,
                new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.84f),
                out _eventMessageOutline);

            GameObject eventText = CreateUiObject(
                "EventText",
                _eventMessageRect,
                out RectTransform eventTextRect);
            eventTextRect.anchorMin = Vector2.zero;
            eventTextRect.anchorMax = Vector2.one;
            eventTextRect.offsetMin = new Vector2(12f, 0f);
            eventTextRect.offsetMax = new Vector2(-12f, 0f);
            _timingResultLabel = eventText.AddComponent<Text>();
            ConfigureStatusText(
                _timingResultLabel,
                18,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            _timingResultLabel.color = PrimaryTextColor;
            Outline textOutline = AddSubtleOutline(
                eventText,
                new Color(0f, 0f, 0f, 0.20f));
            textOutline.effectDistance = new Vector2(1f, -1f);
            _timingRoot.SetActive(false);
            _eventMessageRoot.SetActive(false);
        }

        private void EnsureGaugeCardViews()
        {
            if (hudRoot == null || tensionFill == null || captureFill == null) return;

            _tensionCardRoot = ResolveGaugeCardRoot(tensionFill);
            _captureCardRoot = ResolveGaugeCardRoot(captureFill);
            StyleFightingGaugePanel();
            StyleGaugeCard(_tensionCardRoot, tensionFill, tensionValueLabel, true);
            StyleGaugeCard(_captureCardRoot, captureFill, captureValueLabel, false);
            EnsureTensionRiskGauge();
            EnsureCaptureProgressScale();
        }

        private void StyleFightingGaugePanel()
        {
            if (_fightingGaugeRoot == null) return;

            RectTransform panelRect = _fightingGaugeRoot.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 0f);
                panelRect.anchorMax = new Vector2(1f, 0f);
                panelRect.pivot = new Vector2(1f, 0f);
                panelRect.anchoredPosition = new Vector2(-40f, 64f);
                panelRect.sizeDelta = new Vector2(540f, 354f);
            }

            Image legacyPanel = _fightingGaugeRoot.GetComponent<Image>();
            if (legacyPanel != null) legacyPanel.enabled = false;
            Outline legacyOutline = _fightingGaugeRoot.GetComponent<Outline>();
            if (legacyOutline != null) legacyOutline.enabled = false;

            Text controls = FindNamedText(_fightingGaugeRoot, "Controls");
            if (controls != null)
            {
                controls.text = string.Empty;
                controls.gameObject.SetActive(false);
            }
        }

        private void StyleGaugeCard(
            RectTransform card,
            Image fill,
            Text valueLabel,
            bool tensionCard)
        {
            if (card == null || fill == null) return;

            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, tensionCard ? 0f : -194f);
            card.sizeDelta = new Vector2(0f, tensionCard ? 182f : 160f);
            EnsureLayeredPanel(card.gameObject, PanelColor, out _);

            Transform labelTransform = card.Find("Label");
            Text title = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (title != null)
            {
                title.text = tensionCard ? "장력" : "포획 진행도";
                title.font = ResolveHudFont();
                title.fontSize = tensionCard ? 22 : 21;
                title.fontStyle = FontStyle.Bold;
                title.alignment = TextAnchor.MiddleLeft;
                title.color = PrimaryTextColor;
                SetTopRowRect(title.rectTransform, false);
            }

            if (valueLabel != null)
            {
                valueLabel.font = ResolveHudFont();
                valueLabel.fontSize = 21;
                valueLabel.fontStyle = FontStyle.Bold;
                valueLabel.alignment = TextAnchor.MiddleRight;
                valueLabel.color = tensionCard ? PrimaryTextColor : CaptureTextColor;
                SetTopRowRect(valueLabel.rectTransform, true);
            }

            RectTransform track = fill.transform.parent as RectTransform;
            if (track == null) return;
            track.anchorMin = new Vector2(0f, 1f);
            track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(0.5f, 1f);
            track.anchoredPosition = new Vector2(0f, tensionCard ? -106f : -76f);
            track.sizeDelta = new Vector2(-48f, 32f);

            Image trackImage = track.GetComponent<Image>();
            if (trackImage != null)
            {
                ApplyGaugeTrackStyle(trackImage);
                trackImage.raycastTarget = false;
            }

            if (gaugeFillSprite != null) fill.sprite = gaugeFillSprite;
            if (tensionCard)
            {
                // The legacy fill remains updated for existing API consumers, while the
                // production card presents tension through the fixed risk band + marker.
                fill.enabled = false;
            }
            else
            {
                fill.enabled = true;
                fill.color = CaptureColor;
                EnsureGaugeShine(fill.rectTransform, "CaptureFillShine", 3f);
            }
        }

        private void EnsureTensionRiskGauge()
        {
            RectTransform track = tensionFill != null
                ? tensionFill.transform.parent as RectTransform
                : null;
            if (track == null) return;

            Transform existingBand = track.Find("RiskBand");
            if (existingBand != null)
            {
                _tensionRiskBand = existingBand as RectTransform;
            }
            else
            {
                GameObject band = CreateUiObject("RiskBand", track, out _tensionRiskBand);
                FishingV3Tuning zoneTuning = new FishingV3Tuning();
                zoneTuning.Sanitize();
                // This is a presentation ramp; the snapshot's TensionZone remains the
                // authoritative source for the label. Segment boundaries mirror the
                // current production V3 tuning rather than using decorative widths.
                CreateRiskSegment(
                    "SlackBand",
                    _tensionRiskBand,
                    0f,
                    zoneTuning.SlackUpperThreshold,
                    new Color(0.29f, 0.63f, 1f, 1f));
                CreateRiskSegment(
                    "LowBand",
                    _tensionRiskBand,
                    zoneTuning.SlackUpperThreshold,
                    zoneTuning.LowUpperThreshold,
                    new Color(0.26f, 0.82f, 0.88f, 1f));
                CreateRiskSegment(
                    "SafeBand",
                    _tensionRiskBand,
                    zoneTuning.LowUpperThreshold,
                    zoneTuning.SafeUpperThreshold,
                    new Color(0.31f, 0.88f, 0.56f, 1f));
                CreateRiskSegment(
                    "HighBand",
                    _tensionRiskBand,
                    zoneTuning.SafeUpperThreshold,
                    zoneTuning.HighUpperThreshold,
                    new Color(1f, 0.64f, 0.20f, 1f));
                CreateRiskSegment(
                    "DangerBand",
                    _tensionRiskBand,
                    zoneTuning.HighUpperThreshold,
                    1f,
                    new Color(0.95f, 0.24f, 0.31f, 1f));
            }

            _tensionRiskBand.anchorMin = Vector2.zero;
            _tensionRiskBand.anchorMax = Vector2.one;
            _tensionRiskBand.offsetMin = new Vector2(1f, 3f);
            _tensionRiskBand.offsetMax = new Vector2(-1f, -3f);
            Image bandMaskImage = _tensionRiskBand.GetComponent<Image>();
            if (bandMaskImage == null)
            {
                bandMaskImage = _tensionRiskBand.gameObject.AddComponent<Image>();
            }
            ApplySlicedSprite(bandMaskImage, capsuleSprite, Color.white);
            bandMaskImage.raycastTarget = false;
            Mask bandMask = _tensionRiskBand.GetComponent<Mask>();
            if (bandMask == null) bandMask = _tensionRiskBand.gameObject.AddComponent<Mask>();
            bandMask.showMaskGraphic = false;
            EnsureGaugeShine(track, "RiskBandShine", 4f);

            Transform existingMarker = track.Find("TensionMarker");
            if (existingMarker != null)
            {
                _tensionMarker = existingMarker as RectTransform;
            }
            else
            {
                GameObject marker = CreateUiObject(
                    "TensionMarker",
                    track,
                    out _tensionMarker);
            }
            EnsureTensionMarkerVisual();

            _tensionLowLabel = EnsureScaleLabel(
                track,
                "LowLabel",
                "낮음",
                TextAnchor.UpperLeft,
                0f,
                0.5f);
            _tensionDangerLabel = EnsureScaleLabel(
                track,
                "DangerLabel",
                "위험",
                TextAnchor.UpperRight,
                0.5f,
                1f);
            ApplyTensionMarker(DisplayedTensionNormalized);
        }

        private void EnsureCaptureProgressScale()
        {
            RectTransform track = captureFill != null
                ? captureFill.transform.parent as RectTransform
                : null;
            if (track == null) return;

            _captureStartLabel = EnsureScaleLabel(
                track,
                "StartLabel",
                "시작",
                TextAnchor.UpperLeft,
                0f,
                0.5f);
            _captureEndLabel = EnsureScaleLabel(
                track,
                "CatchLabel",
                "포획",
                TextAnchor.UpperRight,
                0.5f,
                1f);
        }

        private void ApplyTensionMarker(float value)
        {
            DisplayedTensionMarkerNormalized = NormalizeGaugeValue(value);
            if (_tensionMarker == null) return;

            _tensionMarker.anchorMin = new Vector2(
                DisplayedTensionMarkerNormalized,
                1f);
            _tensionMarker.anchorMax = new Vector2(
                DisplayedTensionMarkerNormalized,
                1f);
            _tensionMarker.pivot = new Vector2(
                DisplayedTensionMarkerNormalized,
                0f);
            _tensionMarker.sizeDelta = new Vector2(36f, 50f);
            _tensionMarker.anchoredPosition = new Vector2(0f, -3f);
            _tensionMarker.SetAsLastSibling();
        }

        private Text EnsureScaleLabel(
            RectTransform track,
            string objectName,
            string text,
            TextAnchor alignment,
            float anchorMinX,
            float anchorMaxX)
        {
            Transform existing = track.Find(objectName);
            Text label;
            RectTransform rect;
            if (existing != null)
            {
                rect = existing as RectTransform;
                label = existing.GetComponent<Text>();
            }
            else
            {
                GameObject labelObject = CreateUiObject(objectName, track, out rect);
                label = labelObject.AddComponent<Text>();
            }

            if (rect == null || label == null) return label;
            rect.anchorMin = new Vector2(anchorMinX, 0f);
            rect.anchorMax = new Vector2(anchorMaxX, 0f);
            rect.pivot = new Vector2(
                alignment == TextAnchor.UpperLeft ? 0f : 1f,
                1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 20f);
            rect.offsetMin = new Vector2(0f, -25f);
            rect.offsetMax = new Vector2(0f, -3f);
            ConfigureStatusText(label, 12, FontStyle.Bold, alignment);
            label.text = text;
            label.color = SecondaryTextColor;
            return label;
        }

        private static RectTransform ResolveGaugeCardRoot(Image fill)
        {
            if (fill == null || fill.transform.parent == null) return null;
            Transform track = fill.transform.parent;
            Transform card = track.parent;
            return card != null ? card as RectTransform : null;
        }

        private static void SetTopRowRect(RectTransform rect, bool alignRight)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(alignRight ? 1f : 0f, 1f);
            rect.anchorMax = new Vector2(alignRight ? 1f : 0f, 1f);
            rect.pivot = new Vector2(alignRight ? 1f : 0f, 1f);
            rect.anchoredPosition = new Vector2(alignRight ? -24f : 24f, -18f);
            rect.sizeDelta = new Vector2(alignRight ? 110f : 310f, 28f);
        }

        private static void CreateRiskSegment(
            string objectName,
            RectTransform parent,
            float start,
            float end,
            Color color)
        {
            GameObject segment = CreateUiObject(objectName, parent, out RectTransform rect);
            rect.anchorMin = new Vector2(start, 0f);
            rect.anchorMax = new Vector2(end, 1f);
            rect.offsetMin = new Vector2(start <= 0f ? 0f : 0.5f, 0f);
            rect.offsetMax = new Vector2(end >= 1f ? 0f : -0.5f, 0f);
            Image image = segment.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
        }

        private Image EnsureGaugeShine(
            RectTransform parent,
            string objectName,
            float horizontalInset)
        {
            Transform existing = parent.Find(objectName);
            RectTransform rect;
            GameObject shine;
            if (existing != null)
            {
                shine = existing.gameObject;
                rect = existing as RectTransform;
            }
            else
            {
                shine = CreateUiObject(objectName, parent, out rect);
            }

            rect.anchorMin = new Vector2(0f, 0.56f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, 0f);
            rect.offsetMax = new Vector2(-horizontalInset, -3f);
            rect.SetAsLastSibling();
            Image image = shine.GetComponent<Image>();
            if (image == null) image = shine.AddComponent<Image>();
            if (gaugeFillSprite != null)
            {
                image.sprite = gaugeFillSprite;
                image.type = Image.Type.Simple;
            }
            else
            {
                ApplySlicedSprite(image, capsuleSprite, Color.white);
            }
            image.color = new Color(0.91f, 0.98f, 1f, 0.18f);
            image.raycastTarget = false;
            return image;
        }

        private void EnsureTensionMarkerVisual()
        {
            if (_tensionMarker == null) return;

            Transform existingArrow = _tensionMarker.Find("Arrow");
            RectTransform arrowRect;
            GameObject arrow;
            if (existingArrow != null)
            {
                arrow = existingArrow.gameObject;
                arrowRect = existingArrow as RectTransform;
            }
            else
            {
                arrow = CreateUiObject("Arrow", _tensionMarker, out arrowRect);
            }

            arrowRect.anchorMin = new Vector2(0.5f, 0f);
            arrowRect.anchorMax = new Vector2(0.5f, 0f);
            arrowRect.pivot = new Vector2(0.5f, 0f);
            arrowRect.anchoredPosition = new Vector2(0f, -2f);
            arrowRect.sizeDelta = new Vector2(30f, 26f);
            _tensionMarkerImage = arrow.GetComponent<Image>();
            if (_tensionMarkerImage == null)
            {
                _tensionMarkerImage = arrow.AddComponent<Image>();
            }
            _tensionMarkerImage.sprite = tensionMarkerSprite;
            _tensionMarkerImage.type = Image.Type.Simple;
            _tensionMarkerImage.preserveAspect = true;
            _tensionMarkerImage.raycastTarget = false;
            _tensionMarkerImage.color = tensionMarkerSprite != null
                ? new Color(1f, 0.96f, 0.77f, 1f)
                : Color.clear;
            EnsureSingleOutline(
                arrow,
                new Color(0.22f, 0.14f, 0.03f, 0.95f),
                new Vector2(2f, -2f));

            Transform fallbackTransform = arrow.transform.Find("FallbackGlyph");
            Text fallbackArrow = fallbackTransform != null
                ? fallbackTransform.GetComponent<Text>()
                : null;
            if (tensionMarkerSprite == null)
            {
                if (fallbackArrow == null)
                {
                    GameObject fallback = CreateUiObject(
                        "FallbackGlyph",
                        arrow.transform,
                        out RectTransform fallbackRect);
                    fallbackRect.anchorMin = Vector2.zero;
                    fallbackRect.anchorMax = Vector2.one;
                    fallbackRect.offsetMin = Vector2.zero;
                    fallbackRect.offsetMax = Vector2.zero;
                    fallbackArrow = fallback.AddComponent<Text>();
                }
                ConfigureStatusText(fallbackArrow, 25, FontStyle.Bold, TextAnchor.MiddleCenter);
                fallbackArrow.text = "▼";
                fallbackArrow.color = new Color(1f, 0.96f, 0.77f, 1f);
            }
            else if (fallbackArrow != null)
            {
                fallbackArrow.enabled = false;
            }

            Color rayColor = new Color(1f, 0.83f, 0.40f, 0.92f);
            EnsureMarkerRay("RayCenter", new Vector2(0f, 34f), 0f, rayColor);
            EnsureMarkerRay("RayLeft", new Vector2(-10f, 30f), -34f, rayColor);
            EnsureMarkerRay("RayRight", new Vector2(10f, 30f), 34f, rayColor);
            arrowRect.SetAsLastSibling();
        }

        private void EnsureMarkerRay(
            string objectName,
            Vector2 position,
            float rotation,
            Color color)
        {
            Transform existing = _tensionMarker.Find(objectName);
            RectTransform rect;
            GameObject ray;
            if (existing != null)
            {
                ray = existing.gameObject;
                rect = existing as RectTransform;
            }
            else
            {
                ray = CreateUiObject(objectName, _tensionMarker, out rect);
            }

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(3f, 10f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = ray.GetComponent<Image>();
            if (image == null) image = ray.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
        }

        private Image EnsureLayeredPanel(
            GameObject root,
            Color surfaceColor,
            out Outline outerOutline)
        {
            Image outer = root.GetComponent<Image>();
            if (outer == null) outer = root.AddComponent<Image>();
            ApplySlicedSprite(outer, roundedPanelSprite, OuterFrameColor);
            outer.raycastTarget = false;
            outerOutline = EnsureSingleOutline(
                root,
                new Color(0.86f, 0.95f, 1f, 0.94f),
                new Vector2(2f, -2f));
            EnsureDropShadow(root);

            Image innerFrame = EnsurePanelLayer(
                root.transform,
                "InnerFrame",
                4f,
                InnerFrameColor,
                0);
            Image surface = EnsurePanelLayer(
                root.transform,
                "Surface",
                8f,
                surfaceColor,
                1);
            EnsureSingleOutline(
                surface.gameObject,
                InnerHighlightColor,
                new Vector2(1f, -1f));
            innerFrame.raycastTarget = false;
            surface.raycastTarget = false;
            return surface;
        }

        private Image EnsurePanelLayer(
            Transform parent,
            string objectName,
            float inset,
            Color color,
            int siblingIndex)
        {
            Transform existing = parent.Find(objectName);
            RectTransform rect;
            GameObject layer;
            if (existing != null)
            {
                layer = existing.gameObject;
                rect = existing as RectTransform;
            }
            else
            {
                layer = CreateUiObject(objectName, parent, out rect);
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
            Image image = layer.GetComponent<Image>();
            if (image == null) image = layer.AddComponent<Image>();
            ApplySlicedSprite(image, roundedPanelSprite, color);
            image.raycastTarget = false;
            return image;
        }

        private static Outline EnsureSingleOutline(
            GameObject target,
            Color color,
            Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
            return outline;
        }

        private static void EnsureDropShadow(GameObject target)
        {
            Shadow dropShadow = null;
            Shadow[] effects = target.GetComponents<Shadow>();
            foreach (Shadow effect in effects)
            {
                if (!(effect is Outline))
                {
                    dropShadow = effect;
                    break;
                }
            }

            if (dropShadow == null) dropShadow = target.AddComponent<Shadow>();
            dropShadow.enabled = true;
            dropShadow.effectColor = new Color(0.15f, 0.23f, 0.28f, 0.32f);
            dropShadow.effectDistance = new Vector2(0f, -5f);
            dropShadow.useGraphicAlpha = true;
        }

        private static void ApplySlicedSprite(Image image, Sprite sprite, Color color)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
        }

        private void ApplyGaugeTrackStyle(Image image)
        {
            if (image == null) return;

            if (gaugeFillSprite != null)
            {
                // The fill sprite is neutral white, so the rail can use the HUD's muted
                // blue-gray instead of inheriting the navy baked into bar-track.png.
                image.sprite = gaugeFillSprite;
                image.type = Image.Type.Simple;
            }
            else
            {
                image.sprite = gaugeTrackSprite;
                image.type = gaugeTrackSprite != null
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
            }

            image.color = GaugeTrackColor;
        }

        private void EnsureTensionStatusView()
        {
            if (hudRoot == null || HasTensionStatusView) return;

            Transform statusParent = _tensionCardRoot != null
                ? _tensionCardRoot
                : (_fightingGaugeRoot != null
                    ? _fightingGaugeRoot.transform
                    : hudRoot.transform);
            _tensionStatusRoot = CreateUiObject(
                "TensionStatus",
                statusParent,
                out RectTransform statusRect);
            if (_tensionCardRoot != null)
            {
                statusRect.anchorMin = new Vector2(0f, 1f);
                statusRect.anchorMax = new Vector2(1f, 1f);
                statusRect.pivot = new Vector2(0.5f, 1f);
                statusRect.anchoredPosition = new Vector2(0f, -51f);
                statusRect.sizeDelta = new Vector2(-48f, 30f);
            }
            else if (_fightingGaugeRoot != null)
            {
                statusRect.anchorMin = new Vector2(0f, 1f);
                statusRect.anchorMax = new Vector2(1f, 1f);
                statusRect.pivot = new Vector2(0.5f, 1f);
                statusRect.anchoredPosition = new Vector2(0f, -12f);
                statusRect.sizeDelta = new Vector2(-40f, 28f);
            }
            else
            {
                statusRect.anchorMin = new Vector2(1f, 0f);
                statusRect.anchorMax = new Vector2(1f, 0f);
                statusRect.pivot = new Vector2(1f, 0f);
                statusRect.anchoredPosition = new Vector2(-32f, 204f);
                statusRect.sizeDelta = new Vector2(380f, 28f);
            }
            _tensionStatusBackground = _tensionStatusRoot.AddComponent<Image>();
            _tensionStatusBackground.raycastTarget = false;
            _tensionStatusBackground.color = _tensionCardRoot != null
                ? new Color(0f, 0f, 0f, 0f)
                : PanelColor;
            if (_tensionCardRoot == null)
            {
                AddSubtleOutline(_tensionStatusRoot, BorderColor);
            }

            GameObject zoneLabel = CreateUiObject(
                "ZoneLabel",
                statusRect,
                out RectTransform zoneRect);
            zoneRect.anchorMin = new Vector2(0f, 0f);
            zoneRect.anchorMax = new Vector2(0.34f, 1f);
            zoneRect.offsetMin = new Vector2(14f, 0f);
            zoneRect.offsetMax = new Vector2(-6f, 0f);
            _tensionZoneLabel = zoneLabel.AddComponent<Text>();
            ConfigureStatusText(_tensionZoneLabel, 18, FontStyle.Bold, TextAnchor.MiddleLeft);

            GameObject hintLabel = CreateUiObject(
                "ActionHint",
                statusRect,
                out RectTransform hintRect);
            hintRect.anchorMin = new Vector2(0.34f, 0f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.offsetMin = new Vector2(6f, 0f);
            hintRect.offsetMax = new Vector2(-14f, 0f);
            _tensionHintLabel = hintLabel.AddComponent<Text>();
            ConfigureStatusText(_tensionHintLabel, 15, FontStyle.Bold, TextAnchor.MiddleRight);
            _tensionHintLabel.color = SecondaryTextColor;
            _tensionStatusRoot.SetActive(false);
        }

        private void EnsureResultOverlayView()
        {
            if (hudRoot == null || HasResultOverlayView) return;

            _resultOverlayRoot = CreateUiObject(
                "ResultOverlay",
                hudRoot.transform,
                out RectTransform overlayRect);
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = new Vector2(500f, 126f);
            _resultOverlayBackground = EnsureLayeredPanel(
                _resultOverlayRoot,
                new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.96f),
                out _resultOverlayOutline);

            GameObject title = CreateUiObject(
                "ResultTitle",
                overlayRect,
                out RectTransform titleRect);
            titleRect.anchorMin = new Vector2(0f, 0.45f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = new Vector2(-24f, -8f);
            _resultTitleLabel = title.AddComponent<Text>();
            ConfigureStatusText(
                _resultTitleLabel,
                28,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);

            GameObject description = CreateUiObject(
                "ResultDescription",
                overlayRect,
                out RectTransform descriptionRect);
            descriptionRect.anchorMin = new Vector2(0f, 0f);
            descriptionRect.anchorMax = new Vector2(1f, 0.45f);
            descriptionRect.offsetMin = new Vector2(24f, 10f);
            descriptionRect.offsetMax = new Vector2(-24f, 0f);
            _resultDescriptionLabel = description.AddComponent<Text>();
            ConfigureStatusText(
                _resultDescriptionLabel,
                16,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            _resultDescriptionLabel.color = PrimaryTextColor;
            _resultOverlayRoot.SetActive(false);
        }

        private void ApplyTimingFeedbackStyle(
            FishingV3GameplayPhase phase,
            FishingV3TimingGrade grade)
        {
            if (_timingResultLabel == null) return;

            if (phase == FishingV3GameplayPhase.WaitingForBite)
            {
                _timingResultLabel.fontSize = 16;
                _timingResultLabel.fontStyle = FontStyle.Normal;
                _timingResultLabel.color = PrimaryTextColor;
                SetEventPanelStyle(SecondaryTextColor, 0.78f);
                return;
            }

            if (phase == FishingV3GameplayPhase.HookWindow)
            {
                Color hookAccent = new Color(1f, 0.75f, 0.25f, 1f);
                _timingResultLabel.fontSize = 24;
                _timingResultLabel.fontStyle = FontStyle.Normal;
                _timingResultLabel.color = hookAccent;
                SetEventPanelStyle(hookAccent, 0.86f);
                return;
            }

            _timingResultLabel.fontStyle = FontStyle.Normal;
            switch (grade)
            {
                case FishingV3TimingGrade.Perfect:
                    Color perfect = new Color(1f, 0.82f, 0.24f, 1f);
                    _timingResultLabel.fontSize = 22;
                    _timingResultLabel.color = perfect;
                    SetEventPanelStyle(perfect, 0.78f);
                    break;
                case FishingV3TimingGrade.Good:
                    Color good = new Color(0.31f, 0.88f, 0.56f, 1f);
                    _timingResultLabel.fontSize = 19;
                    _timingResultLabel.color = good;
                    SetEventPanelStyle(good, 0.74f);
                    break;
                case FishingV3TimingGrade.Miss:
                    Color miss = new Color(0.95f, 0.24f, 0.31f, 1f);
                    _timingResultLabel.fontSize = 17;
                    _timingResultLabel.color = miss;
                    SetEventPanelStyle(miss, 0.74f);
                    break;
                default:
                    _timingResultLabel.fontSize = 12;
                    _timingResultLabel.color = PrimaryTextColor;
                    SetEventPanelStyle(BorderColor, 0.64f);
                    break;
            }
        }

        private void SetEventPanelStyle(Color accent, float backgroundAlpha)
        {
            if (_eventMessageBackground != null)
            {
                _eventMessageBackground.color = new Color(
                    PanelColor.r,
                    PanelColor.g,
                    PanelColor.b,
                    backgroundAlpha);
            }

            if (_eventMessageOutline != null)
            {
                _eventMessageOutline.effectColor = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    0.62f);
            }
        }

        private void PositionEventMessage(
            FishingV3GameplayPhase phase,
            FishingV3TimingGrade grade)
        {
            if (_eventMessageRect == null) return;

            bool isMomentaryEvent = phase == FishingV3GameplayPhase.WaitingForBite ||
                phase == FishingV3GameplayPhase.HookWindow ||
                grade != FishingV3TimingGrade.None;
            if (isMomentaryEvent)
            {
                _eventMessageRect.anchorMin = new Vector2(0.5f, 0.5f);
                _eventMessageRect.anchorMax = new Vector2(0.5f, 0.5f);
                _eventMessageRect.pivot = new Vector2(0.5f, 0.5f);
                _eventMessageRect.anchoredPosition = new Vector2(0f, 80f);
                _eventMessageRect.sizeDelta = new Vector2(300f, 76f);
                return;
            }

            _eventMessageRect.anchorMin = new Vector2(0.5f, 0f);
            _eventMessageRect.anchorMax = new Vector2(0.5f, 0f);
            _eventMessageRect.pivot = new Vector2(0.5f, 0f);
            _eventMessageRect.anchoredPosition = new Vector2(0f, 92f);
            _eventMessageRect.sizeDelta = new Vector2(140f, 24f);
        }

        private void ConfigureStatusText(
            Text label,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            label.font = ResolveHudFont();
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.raycastTarget = false;
        }

        private Font ResolveHudFont()
        {
            return hudFont != null
                ? hudFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Outline AddSubtleOutline(GameObject target, Color color)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            return outline;
        }

        private static Text FindNamedText(GameObject root, string objectName)
        {
            if (root == null) return null;
            Text[] labels = root.GetComponentsInChildren<Text>(true);
            foreach (Text label in labels)
            {
                if (label.name == objectName) return label;
            }

            return null;
        }

        private static bool IsTerminalResult(FishingV3Result result)
        {
            return result == FishingV3Result.Caught ||
                result == FishingV3Result.LineBroken ||
                result == FishingV3Result.FishEscaped;
        }

        private static float SanitizeResultDisplaySeconds(float seconds)
        {
            return float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0.1f
                ? 1.75f
                : seconds;
        }

        private static float SanitizeCaughtResultDelay(float seconds)
        {
            return float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f
                ? FishingV3FishVisualPresenter.DefaultCaughtDisplaySeconds
                : seconds;
        }

        private static GameObject CreateUiObject(
            string objectName,
            Transform parent,
            out RectTransform rectTransform)
        {
            GameObject instance = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer));
            rectTransform = instance.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return instance;
        }

        private static void SetZone(RectTransform zone, float halfWidth)
        {
            if (zone == null) return;
            float width = NormalizeGaugeValue(halfWidth);
            zone.anchorMin = new Vector2(Mathf.Clamp01(0.5f - width), 0f);
            zone.anchorMax = new Vector2(Mathf.Clamp01(0.5f + width), 1f);
            zone.offsetMin = new Vector2(2f, 3f);
            zone.offsetMax = new Vector2(-2f, -3f);
        }

        private void ResolveFacade()
        {
            if (facade == null) facade = GetComponentInParent<FishingMiniGameFacade>();
        }

        private void ResolveFightingGaugeRoot()
        {
            if (_fightingGaugeRoot != null || hudRoot == null ||
                tensionFill == null || captureFill == null)
            {
                return;
            }

            Transform common = FindNearestCommonAncestor(
                tensionFill.transform,
                captureFill.transform);
            if (common != null && common != hudRoot.transform)
            {
                _fightingGaugeRoot = common.gameObject;
            }
        }

        private void SetFightingGaugesVisible(bool visible)
        {
            ResolveFightingGaugeRoot();
            if (_fightingGaugeRoot != null)
            {
                _fightingGaugeRoot.SetActive(visible);
                return;
            }

            SetElementVisible(tensionFill, visible);
            SetElementVisible(captureFill, visible);
            SetElementVisible(tensionValueLabel, visible);
            SetElementVisible(captureValueLabel, visible);
            SetElementVisible(_tensionRiskBand, visible);
            SetElementVisible(_tensionMarker, visible);
            SetElementVisible(_tensionLowLabel, visible);
            SetElementVisible(_tensionDangerLabel, visible);
            SetElementVisible(_captureStartLabel, visible);
            SetElementVisible(_captureEndLabel, visible);
        }

        private static Transform FindNearestCommonAncestor(Transform first, Transform second)
        {
            for (Transform candidate = first; candidate != null; candidate = candidate.parent)
            {
                for (Transform other = second; other != null; other = other.parent)
                {
                    if (candidate == other) return candidate;
                }
            }

            return null;
        }

        private static void SetElementVisible(Component element, bool visible)
        {
            if (element != null && element.gameObject.activeSelf != visible)
            {
                element.gameObject.SetActive(visible);
            }
        }

        private void SetVisible(bool visible)
        {
            if (hudRoot != null && hudRoot != gameObject && hudRoot.activeSelf != visible)
            {
                hudRoot.SetActive(visible);
            }
        }

        private static void ConfigureFill(Image fill)
        {
            if (fill == null) return;
            ApplyGaugeFill(fill, fill.fillAmount);
        }

        private static void SetGauge(Image fill, Text label, float value)
        {
            float normalized = NormalizeGaugeValue(value);
            ApplyGaugeFill(fill, normalized);
            if (label != null) label.text = $"{normalized * 100f:0}%";
        }
    }
}
