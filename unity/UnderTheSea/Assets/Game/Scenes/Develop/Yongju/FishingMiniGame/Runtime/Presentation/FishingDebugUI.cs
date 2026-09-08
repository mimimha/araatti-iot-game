using FishingMiniGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishingMiniGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FishingDebugUI : MonoBehaviour
    {
        private static readonly Color DeepNavy = new Color(0.025f, 0.075f, 0.105f, 0.96f);
        private static readonly Color PanelNavy = new Color(0.035f, 0.12f, 0.16f, 0.93f);
        private static readonly Color SoftPanel = new Color(0.055f, 0.18f, 0.22f, 0.88f);
        private static readonly Color Aqua = new Color(0.20f, 0.88f, 0.82f, 1f);
        private static readonly Color Gold = new Color(1f, 0.74f, 0.23f, 1f);
        private static readonly Color Coral = new Color(1f, 0.35f, 0.26f, 1f);
        private static readonly Color TextPrimary = new Color(0.94f, 0.98f, 0.98f, 1f);
        private static readonly Color TextSecondary = new Color(0.66f, 0.79f, 0.80f, 1f);

        [SerializeField] private FishingGameController controller;
        [SerializeField] private bool allowLocalStart;
        [SerializeField, Min(0f)] private float caughtPresentationHoldSeconds = 1.25f;

        private Font _font;
        private GameObject _canvasRoot;
        private GameObject _gameplayRoot;
        private GameObject _startPanel;
        private GameObject _countdownPanel;
        private GameObject _resultPanel;
        private GameObject _debugPanel;
        private GameObject _centerAlertPanel;
        private RawImage _startBackdrop;
        private Text _timerLabelText;
        private Text _timerText;
        private Text _scoreText;
        private Text _catchText;
        private Text _fishText;
        private Text _difficultyText;
        private Text _stateText;
        private Text _behaviorText;
        private Text _actionTitleText;
        private Text _actionDetailText;
        private Text _centerAlertText;
        private Text _countdownText;
        private Text _resultTitleText;
        private Text _resultScoreLabelText;
        private Text _resultScoreText;
        private Text _resultDetailText;
        private Text _resultFootnoteText;
        private Text _recentCatchText;
        private Text _debugText;
        private Text _controlsText;
        private Text _howToText;
        private BarWidgets _castBar;
        private BarWidgets _tensionBar;
        private BarWidgets _healthBar;
        private BarWidgets _lineBar;
        private FishingPlayerState _lastState;
        private FishingFeedbackState _lastFeedbackState;
        private string _lastOutcome;
        private float _alertUntil;
        private bool _debugVisible;
        private bool _lastNibbling;
        private bool _lastRunTelegraphing;
        private bool _caughtPresentationStarted;
        private float _caughtPresentationRemainingSeconds;
        private bool _built;
        private FishingStandaloneBootstrap _standaloneBootstrap;

        public void Configure(FishingGameController gameController, bool canStartLocally)
        {
            controller = gameController;
            allowLocalStart = canStartLocally;
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<FishingGameController>();
            _standaloneBootstrap = GetComponent<FishingStandaloneBootstrap>();
            if (_standaloneBootstrap != null) allowLocalStart = true;
            BuildCanvas();
        }

        private void OnEnable()
        {
            if (!_built) BuildCanvas();
        }

        private void Update()
        {
            if (!_built || _canvasRoot == null) BuildCanvas();
            if (controller == null || controller.Snapshot == null || controller.RoundSnapshot == null ||
                controller.SessionSnapshot == null)
            {
                if (_canvasRoot != null) _canvasRoot.SetActive(false);
                return;
            }

            _canvasRoot.SetActive(true);
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.F3))
                _debugVisible = !_debugVisible;
            Refresh(controller.Snapshot, controller.RoundSnapshot, controller.SessionSnapshot);
            if (_startPanel != null && _startPanel.activeSelf) UpdateStartBackdropCrop();
        }

        private void Refresh(
            FishingSnapshot snapshot,
            FishingRoundSnapshot round,
            FishingSessionSnapshot session)
        {
            bool singleSession = controller.Mode == FishingGameMode.SingleFishSession;
            bool ready = singleSession
                ? session.State == FishingSessionState.Ready
                : round.State == FishingRoundState.Ready;
            bool countdown = singleSession
                ? session.State == FishingSessionState.Countdown
                : round.State == FishingRoundState.Countdown;
            bool finished = singleSession
                ? session.State == FishingSessionState.Completed || session.State == FishingSessionState.Aborted
                : round.State == FishingRoundState.Completed || round.State == FishingRoundState.Aborted;
            bool playing = singleSession
                ? session.State == FishingSessionState.Playing
                : round.State == FishingRoundState.Playing;
            bool caughtPresentation = UpdateCaughtPresentation(snapshot, singleSession, finished);

            _startPanel.SetActive(ready);
            _countdownPanel.SetActive(countdown);
            _resultPanel.SetActive(finished && !caughtPresentation);
            _gameplayRoot.SetActive(playing || caughtPresentation);
            _debugPanel.SetActive(_debugVisible && playing);

            if (ready)
            {
                Text readyHint = _startPanel.transform.Find("StartCard/ReadyHint")?.GetComponent<Text>();
                if (readyHint != null)
                {
                    readyHint.text = allowLocalStart
                        ? (singleSession
                            ? $"Land one {session.FishDisplayName} to complete the session"
                            : "A three-minute coastal fishing challenge")
                        : "Waiting for the host project to begin fishing";
                }
                Button button = _startPanel.transform.Find("StartCard/StartButton")?.GetComponent<Button>();
                if (button != null) button.gameObject.SetActive(allowLocalStart);
            }

            if (countdown)
            {
                float countdownRemaining = singleSession
                    ? session.CountdownRemainingSeconds
                    : round.CountdownRemainingSeconds;
                _countdownText.text = Mathf.Max(1, Mathf.CeilToInt(countdownRemaining)).ToString();
                return;
            }

            if (finished && !caughtPresentation)
            {
                if (singleSession) RefreshSessionResult(session);
                else RefreshResult(round);
                return;
            }

            if (!playing && !caughtPresentation) return;

            _timerLabelText.text = singleSession ? "SESSION ELAPSED" : "TIME LEFT";
            _timerText.text = FormatTime(singleSession ? session.ElapsedSeconds : round.RemainingSeconds);
            _scoreText.text = (singleSession ? snapshot.TotalScore : round.TotalScore).ToString("N0");
            int preFightRetries = snapshot.EarlyHookCount + snapshot.MissedBiteRetryCount;
            _catchText.text = singleSession
                ? $"ONE FISH SESSION  /  {preFightRetries} RETRIES"
                : $"{round.CaughtCount} CAUGHT  /  {round.Attempts} ATTEMPTS";
            _fishText.text = string.IsNullOrWhiteSpace(snapshot.FishDisplayName) ? "UNKNOWN FISH" : snapshot.FishDisplayName.ToUpperInvariant();
            _difficultyText.text = string.IsNullOrWhiteSpace(snapshot.DifficultyLabel) ? "NORMAL" : snapshot.DifficultyLabel.ToUpperInvariant();
            _difficultyText.color = DifficultyColor(snapshot.DifficultyLabel);
            _stateText.text = FriendlyState(snapshot.State);
            _behaviorText.text = BehaviorLabel(snapshot);
            _behaviorText.color = BehaviorColor(snapshot);

            SetBar(_castBar, snapshot.CastPower, Aqua, $"{snapshot.CastPower * 100f:0}%");
            SetBar(_tensionBar, snapshot.TensionNormalized, TensionColor(snapshot.TensionNormalized), $"{snapshot.TensionNormalized * 100f:0}%");
            SetBar(_healthBar, NormalizedHealth(snapshot), DifficultyColor(snapshot.DifficultyLabel), singleSession
                ? $"{snapshot.FishStaminaNormalized * 100f:0}%"
                : $"{snapshot.FishHealth:0} / {snapshot.FishMaxHealth:0}");
            SetBar(_lineBar, snapshot.LineDurability / 100f, snapshot.LineDurability < 35f ? Coral : Aqua, $"{snapshot.LineDurability:0}%");
            _castBar.Root.SetActive(true);
            if (_controlsText != null)
            {
                _controlsText.text = singleSession
                    ? "SPACE  Cast   •   F  Hook   •   R / LMB  Reel\nARROWS  Rod pose / follow fish   •   T  Recenter"
                    : "SPACE  Cast   •   F  Hook   •   R / LMB  Reel\nARROWS  Rod pose   •   T  Recenter\nQ / E  Lower / Raise tension";
            }
            if (_howToText != null)
            {
                _howToText.text = singleSession
                    ? "1   Hold and release SPACE to cast\n2   Press F only on BITE to set the hook\n3   Follow RUN direction with ARROWS\n4   Reel during safe pressure; stop in danger"
                    : "1   Hold and release SPACE to cast\n2   Press F only on BITE to set the hook\n3   Use ARROWS against runs\n4   Use Q / E for tension and R to reel";
            }

            UpdateGuidance(snapshot, singleSession);
            UpdateAlert(snapshot, singleSession);
            UpdateRecentCatch(singleSession, session);
            UpdateDebug(snapshot, round, session);
        }

        private bool UpdateCaughtPresentation(
            FishingSnapshot snapshot,
            bool singleSession,
            bool finished)
        {
            bool caught = singleSession && finished &&
                snapshot.State == FishingPlayerState.Caught;
            if (!caught)
            {
                _caughtPresentationStarted = false;
                _caughtPresentationRemainingSeconds = 0f;
                return false;
            }

            if (!_caughtPresentationStarted)
            {
                _caughtPresentationStarted = true;
                _caughtPresentationRemainingSeconds = Mathf.Max(0f, caughtPresentationHoldSeconds);
            }

            bool active = _caughtPresentationRemainingSeconds > 0f;
            if (active && !controller.IsPaused)
            {
                _caughtPresentationRemainingSeconds = Mathf.Max(
                    0f,
                    _caughtPresentationRemainingSeconds - Time.unscaledDeltaTime);
            }
            return active;
        }

        private void UpdateGuidance(FishingSnapshot snapshot, bool singleSession)
        {
            switch (snapshot.State)
            {
                case FishingPlayerState.Idle:
                    SetGuidance("READY TO CAST", "Hold SPACE to build power, then release");
                    break;
                case FishingPlayerState.Casting:
                    SetGuidance("CHARGING CAST", "Release SPACE at the power you want");
                    break;
                case FishingPlayerState.Waiting:
                    if (snapshot.IsNibbling)
                        SetGuidance("NIBBLE...", "Wait — this is not the committed bite");
                    else
                        SetGuidance("WATCH THE FLOAT", "Wait for the bite signal — do not press early");
                    break;
                case FishingPlayerState.BiteWindow:
                    SetGuidance("BITE!", "Press F now to set the hook");
                    break;
                case FishingPlayerState.Hooked:
                    SetGuidance("HOOK SET", singleSession
                        ? "Get ready: use rod angle and reel timing to manage pressure"
                        : "Get ready: hold R to reel and Q / E to control tension");
                    break;
                case FishingPlayerState.Fighting:
                    SetGuidance("FIGHT THE FISH", FightGuidance(snapshot, singleSession));
                    break;
                case FishingPlayerState.Caught:
                    SetGuidance("GREAT CATCH!", snapshot.LastOutcome);
                    break;
                case FishingPlayerState.Escaped:
                    SetGuidance("FISH ESCAPED", snapshot.LastOutcome);
                    break;
                default:
                    SetGuidance("NEXT CAST", "Preparing a new fish...");
                    break;
            }
        }

        private static string FightGuidance(FishingSnapshot snapshot, bool singleSession)
        {
            if (singleSession)
            {
                if (snapshot.IsRunTelegraphing)
                {
                    return snapshot.RunTelegraphDirectionNormalized < 0f
                        ? "RUN INCOMING LEFT — prepare to follow LEFT"
                        : "RUN INCOMING RIGHT — prepare to follow RIGHT";
                }
                if (snapshot.VirtualTensionZone == FishingV2TensionZone.Slack)
                    return "SLACK — raise the rod and restore pressure";
                if (snapshot.VirtualTensionZone == FishingV2TensionZone.Danger)
                    return "DANGER — stop reeling and move with the fish";
                if (snapshot.V2BehaviorState == FishingV2BehaviorState.Run)
                {
                    return snapshot.V2FishDirectionNormalized < 0f
                        ? "FOLLOW LEFT — move the rod LEFT and ease the reel"
                        : "FOLLOW RIGHT — move the rod RIGHT and ease the reel";
                }
                if (snapshot.V2BehaviorState == FishingV2BehaviorState.Rest)
                    return "REST — reel while line pressure is safe";
                return "STRUGGLE — keep a moderate rod angle and reel when stable";
            }

            if (snapshot.TensionNormalized < 0.30f) return "Line is slack — press E to raise tension";
            if (snapshot.TensionNormalized > 0.75f) return "Line is too tight — press Q to lower tension";
            if (snapshot.Feedback.State == FishingFeedbackState.Run)
            {
                return snapshot.FightDirection < 0f
                    ? "RUN LEFT — hold RIGHT ARROW to counter, ease R and watch tension"
                    : "RUN RIGHT — hold LEFT ARROW to counter, ease R and watch tension";
            }
            if (snapshot.Feedback.State == FishingFeedbackState.Rest) return "REST — reel hard with R while the fish recovers";
            return "STRUGGLE — follow the changing tension and reel when stable";
        }

        private void UpdateAlert(FishingSnapshot snapshot, bool singleSession)
        {
            if (snapshot.State != _lastState)
            {
                _lastState = snapshot.State;
                if (snapshot.State == FishingPlayerState.BiteWindow) ShowAlert("BITE!  PRESS F", Gold, 1.2f);
                else if (snapshot.State == FishingPlayerState.Hooked) ShowAlert("HOOK SET!", Aqua, 0.9f);
                else if (snapshot.State == FishingPlayerState.Caught) ShowAlert("FISH CAUGHT!", Aqua, 1.5f);
                else if (snapshot.State == FishingPlayerState.Escaped) ShowAlert("THE FISH ESCAPED", Coral, 1.5f);
            }

            if (snapshot.State == FishingPlayerState.Fighting && snapshot.Feedback.State != _lastFeedbackState)
            {
                _lastFeedbackState = snapshot.Feedback.State;
                if (snapshot.Feedback.State == FishingFeedbackState.Run)
                {
                    if (singleSession)
                    {
                        ShowAlert(snapshot.FightDirection < 0f
                            ? "FOLLOW LEFT  •  LEFT ARROW"
                            : "FOLLOW RIGHT  •  RIGHT ARROW", Coral, 0.75f);
                    }
                    else
                    {
                        ShowAlert(snapshot.FightDirection < 0f
                            ? "RUN LEFT  •  RIGHT ARROW"
                            : "RUN RIGHT  •  LEFT ARROW", Coral, 0.75f);
                    }
                }
                else if (snapshot.Feedback.State == FishingFeedbackState.Rest)
                {
                    ShowAlert("FISH RESTING  •  REEL NOW", Aqua, 0.70f);
                }
                else if (snapshot.Feedback.State == FishingFeedbackState.Fight)
                {
                    ShowAlert("FISH STRUGGLING", Gold, 0.55f);
                }
            }

            if (singleSession && snapshot.State == FishingPlayerState.Fighting &&
                snapshot.IsRunTelegraphing && !_lastRunTelegraphing)
            {
                ShowAlert(snapshot.RunTelegraphDirectionNormalized < 0f
                    ? "RUN INCOMING LEFT"
                    : "RUN INCOMING RIGHT", Coral, snapshot.RunTelegraphRemainingSeconds);
            }
            _lastRunTelegraphing = snapshot.State == FishingPlayerState.Fighting &&
                snapshot.IsRunTelegraphing;

            if (snapshot.IsNibbling && !_lastNibbling)
            {
                ShowAlert("NIBBLE... WAIT", TextSecondary, 0.55f);
            }
            _lastNibbling = snapshot.IsNibbling;

            if (!string.IsNullOrWhiteSpace(snapshot.LastOutcome) && snapshot.LastOutcome != _lastOutcome)
            {
                _lastOutcome = snapshot.LastOutcome;
                _alertUntil = Mathf.Max(_alertUntil, Time.unscaledTime + 1.5f);
            }

            bool persistentBite = snapshot.State == FishingPlayerState.BiteWindow;
            _centerAlertPanel.SetActive(persistentBite || Time.unscaledTime < _alertUntil);
            _centerAlertPanel.transform.localScale = persistentBite
                ? Vector3.one * (0.94f + (Mathf.Sin(Time.unscaledTime * 12f) * 0.06f))
                : Vector3.one;
        }

        private void ShowAlert(string message, Color color, float seconds)
        {
            _centerAlertText.text = message;
            _centerAlertText.color = color;
            _alertUntil = Time.unscaledTime + seconds;
        }

        private void UpdateRecentCatch(bool singleSession, FishingSessionSnapshot session)
        {
            if (singleSession)
            {
                int retryCount = controller.Snapshot.EarlyHookCount +
                    controller.Snapshot.MissedBiteRetryCount;
                _recentCatchText.text = retryCount == 0
                    ? "ONE TARGET  •  LAND IT TO COMPLETE THE SESSION"
                    : $"SAME TARGET  •  RETRY {retryCount + 1}";
                return;
            }

            if (controller.CatchHistory == null || controller.CatchHistory.Count == 0)
            {
                _recentCatchText.text = "No catches yet — your first one is waiting";
                return;
            }

            FishingCatchRecord latest = controller.CatchHistory[controller.CatchHistory.Count - 1];
            _recentCatchText.text = $"LAST CATCH   {latest.FishDisplayName.ToUpperInvariant()}   +{latest.Score}";
        }

        private void UpdateDebug(
            FishingSnapshot snapshot,
            FishingRoundSnapshot round,
            FishingSessionSnapshot session)
        {
            FishingFeedbackFrame feedback = controller.LastFeedback;
            string flow = controller.Mode == FishingGameMode.SingleFishSession
                ? $"Session     {session.State}"
                : $"Round       {round.State}";
            _debugText.text =
                "DEVELOPER OVERLAY  [F1 / F3]\n" +
                $"Mode        {(controller.Mode == FishingGameMode.SingleFishSession ? "V2" : "Legacy")}\n" +
                flow + "\n" +
                $"Player      {snapshot.State}\n" +
                $"Fish ID     {snapshot.FishId}\n" +
                $"State time  {snapshot.StateElapsedSeconds:0.00}s\n" +
                $"Cast power  {snapshot.CastPower:0.00}\n" +
                $"Bite left   {snapshot.BiteDelayRemainingSeconds:0.00}s\n" +
                $"Hook left   {snapshot.HookWindowRemainingSeconds:0.00}s\n" +
                (controller.Mode == FishingGameMode.SingleFishSession
                    ? $"Fight time  {snapshot.FightElapsedSeconds:0.00}s\n"
                    : $"Fight left  {snapshot.FightRemainingSeconds:0.00}s\n") +
                $"Behavior    {snapshot.Feedback.State} {snapshot.FightPhaseRemainingSeconds:0.00}s\n" +
                $"Fish shift  {snapshot.FishTensionShift:+0.00;-0.00;0.00}\n" +
                $"Direction   {snapshot.FightDirection:+0;-0;0} / control {snapshot.DirectionalControlNormalized:0.00}\n" +
                $"Slack risk  {snapshot.SlackDangerNormalized:0.00}\n" +
                $"Tight risk  {snapshot.HighTensionDangerNormalized:0.00}\n" +
                $"Mock output {feedback.State} / {feedback.Intensity:0.00}";
            if (controller.Mode == FishingGameMode.SingleFishSession)
            {
                _debugText.text +=
                    $"\nNibble      {snapshot.IsNibbling} / {snapshot.NibbleRemainingSeconds:0.00}s / intensity {snapshot.NibbleIntensityNormalized:0.00} / seq {snapshot.NibbleEventSequence}" +
                    $"\nBite seq    {snapshot.BiteEventSequence}" +
                    $"\nRetries     early {snapshot.EarlyHookCount} / missed {snapshot.MissedBiteRetryCount}" +
                    $"\nV2 sample   {snapshot.V2BehaviorState} force {snapshot.V2FishForceNormalized:0.00} dir {snapshot.V2FishDirectionNormalized:+0.00;-0.00;0.00}" +
                    $"\nAI band     {snapshot.AIStaminaBand} / phase {snapshot.AIPhaseRemainingSeconds:0.00}s" +
                    $"\nTelegraph   {snapshot.IsRunTelegraphing} dir {snapshot.RunTelegraphDirectionNormalized:+0;-0;0} / {snapshot.RunTelegraphRemainingSeconds:0.00}s" +
                    $"\nHead shake  {snapshot.HeadShakeActive} intensity {snapshot.HeadShakeIntensityNormalized:0.00} / seq {snapshot.HeadShakeEventSequence}" +
                    $"\nFinal run   decided {snapshot.FinalRunDecisionMade} / pending {snapshot.FinalRunPending} / active {snapshot.IsFinalRun} / used {snapshot.FinalRunUsed}" +
                    $"\nDistance    {snapshot.FishDistanceMeters:0.00} m" +
                    $"\nStamina     {snapshot.FishStaminaNormalized:0.00}" +
                    $"\nVirtual T   {snapshot.VirtualLineTensionNormalized:0.00} / {snapshot.VirtualTensionZone}" +
                    $"\nBreak       {snapshot.BreakStressNormalized:0.00}" +
                    $"\nHook loose  {snapshot.HookLooseRiskNormalized:0.00}" +
                    $"\nRod quality {snapshot.RodResponseQualityNormalized:0.00}" +
                    $"\nReel eff    {snapshot.ReelEfficiencyNormalized:0.00} / input {controller.LastInputFrame.ReelDelta:0.00}";
            }
            FishingInputFrame input = controller.LastInputFrame;
            _debugText.text +=
                $"\nRod pose    P {input.RodPitch:+0.00;-0.00;0.00} / Y {input.RodYaw:+0.00;-0.00;0.00}" +
                $"\nRod motion  {input.MotionStrength:0.00}";
        }

        private void RefreshResult(FishingRoundSnapshot round)
        {
            FishingRoundResult result = controller.LastRoundResult;
            if (result == null)
            {
                _resultTitleText.text = round.State == FishingRoundState.Aborted ? "ROUND ABORTED" : "ROUND COMPLETE";
                _resultScoreText.text = round.TotalScore.ToString("N0");
                _resultDetailText.text = $"{round.CaughtCount} fish caught";
                return;
            }

            _resultTitleText.text = result.EndReason == FishingRoundEndReason.TimeExpired ? "TIME'S UP!" : "ROUND COMPLETE";
            _resultScoreText.text = result.TotalScore.ToString("N0");
            _resultDetailText.text = $"{result.CaughtCount} FISH CAUGHT   •   {result.Attempts} ATTEMPTS\n{BuildCatchSummary(result)}";
            _resultScoreLabelText.text = "FINAL SCORE";
            _resultFootnoteText.text = "Each new round reshuffles species and fight behavior";
        }

        private void RefreshSessionResult(FishingSessionSnapshot session)
        {
            FishingSessionResult result = controller.LastSessionResult;
            _resultScoreLabelText.text = "CATCH SCORE";
            _resultFootnoteText.text = "FISH AGAIN starts a fresh single-fish session";
            if (result == null)
            {
                _resultTitleText.text = session.State == FishingSessionState.Aborted
                    ? "SESSION ABORTED"
                    : "SESSION COMPLETE";
                _resultScoreText.text = "0";
                _resultDetailText.text = session.FishDisplayName;
                return;
            }

            bool caught = result.Outcome == FishingSessionOutcome.Caught;
            _resultTitleText.text = caught ? "FISH CAUGHT!" : "FISH ESCAPED";
            _resultScoreText.text = (result.CycleResult?.AwardedScore ?? 0).ToString("N0");
            string retryText = result.PreHookFailureCount == 1
                ? "1 PRE-HOOK RETRY"
                : $"{result.PreHookFailureCount} PRE-HOOK RETRIES";
            string outcome = caught
                ? "TARGET LANDED"
                : $"ESCAPED  •  {result.CycleResult?.EscapeReason}";
            _resultDetailText.text =
                $"{result.FishDisplayName.ToUpperInvariant()}  •  {outcome}\n{retryText}  •  {FormatTime(result.ElapsedSeconds)}";
        }

        private static string BuildCatchSummary(FishingRoundResult result)
        {
            if (result.Catches == null || result.Catches.Length == 0) return "The sea wins this time. Cast again!";
            int start = Mathf.Max(0, result.Catches.Length - 3);
            string summary = string.Empty;
            for (int i = start; i < result.Catches.Length; i++)
            {
                if (summary.Length > 0) summary += "   •   ";
                summary += $"{result.Catches[i].FishDisplayName} +{result.Catches[i].Score}";
            }
            return summary;
        }

        private void SetGuidance(string title, string detail)
        {
            _actionTitleText.text = title;
            _actionDetailText.text = string.IsNullOrWhiteSpace(detail) ? " " : detail;
        }

        private void BuildCanvas()
        {
            if (_built && _canvasRoot != null) return;
            _built = true;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _canvasRoot = new GameObject("FishingHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasRoot.transform.SetParent(transform, false);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            BuildGameplayHud();
            BuildStartPanel();
            BuildCountdownPanel();
            BuildResultPanel();
            BuildDebugPanel();
        }

        private void BuildGameplayHud()
        {
            _gameplayRoot = CreateRect("Gameplay", _canvasRoot.transform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero).gameObject;

            RectTransform header = CreatePanel("Header", _gameplayRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(-56f, 104f), DeepNavy);
            AddOutline(header.gameObject, new Color(0.18f, 0.60f, 0.61f, 0.35f));
            Text brand = CreateText("Brand", header, "SEA FISHING", 30, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            SetRect(brand.rectTransform, new Vector2(0f, 0f), new Vector2(0.32f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, 12f), new Vector2(-28f, -24f));
            Text subtitle = CreateText("Subtitle", header, "COASTAL CHALLENGE", 14, FontStyle.Bold, Aqua, TextAnchor.MiddleLeft);
            SetRect(subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.32f, 1f), new Vector2(0f, 0.5f), new Vector2(30f, -25f), new Vector2(-30f, -68f));

            RectTransform timerCard = CreatePanel("TimerCard", header, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 76f), SoftPanel);
            _timerLabelText = CreateCenteredText("TimerLabel", timerCard, "TIME LEFT", 13, FontStyle.Bold, TextSecondary, TextAnchor.MiddleCenter, new Vector2(0f, 22f), new Vector2(210f, 22f));
            _timerText = CreateCenteredText("Timer", timerCard, "03:00", 34, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(210f, 44f));

            RectTransform scoreArea = CreateRect("ScoreArea", header, new Vector2(0.68f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(-24f, 0f));
            CreateTextAt("ScoreLabel", scoreArea, "SCORE", 13, FontStyle.Bold, TextSecondary, TextAnchor.MiddleRight, new Vector2(351f, -9f), new Vector2(210f, 22f));
            _scoreText = CreateTextAt("Score", scoreArea, "0", 34, FontStyle.Bold, Gold, TextAnchor.MiddleRight, new Vector2(351f, -31f), new Vector2(210f, 48f));
            _catchText = CreateTextAt("CatchCount", scoreArea, "0 CAUGHT / 0 ATTEMPTS", 14, FontStyle.Normal, TextSecondary, TextAnchor.MiddleRight, new Vector2(0f, -39f), new Vector2(330f, 30f));

            RectTransform fishCard = CreatePanel("FishCard", _gameplayRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -150f), new Vector2(390f, 112f), PanelNavy);
            AddAccent(fishCard, Aqua);
            CreateTextAt("FishLabel", fishCard, "CURRENT TARGET", 13, FontStyle.Bold, TextSecondary, TextAnchor.MiddleLeft, new Vector2(24f, -10f), new Vector2(330f, 22f));
            _fishText = CreateTextAt("FishName", fishCard, "POND CRUCIAN", 25, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft, new Vector2(24f, -36f), new Vector2(330f, 38f));
            _difficultyText = CreateTextAt("Difficulty", fishCard, "EASY", 14, FontStyle.Bold, Aqua, TextAnchor.MiddleLeft, new Vector2(24f, -78f), new Vector2(330f, 24f));

            RectTransform actionCard = CreatePanel("ActionCard", _gameplayRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(760f, 142f), DeepNavy);
            AddOutline(actionCard.gameObject, new Color(Aqua.r, Aqua.g, Aqua.b, 0.45f));
            _stateText = CreateTextAt("StateChip", actionCard, "READY", 13, FontStyle.Bold, Aqua, TextAnchor.MiddleCenter, new Vector2(25f, -10f), new Vector2(710f, 22f));
            _actionTitleText = CreateTextAt("ActionTitle", actionCard, "READY TO CAST", 30, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter, new Vector2(25f, -37f), new Vector2(710f, 46f));
            _actionDetailText = CreateTextAt("ActionDetail", actionCard, "Hold SPACE to build power, then release", 17, FontStyle.Normal, TextSecondary, TextAnchor.MiddleCenter, new Vector2(25f, -91f), new Vector2(710f, 31f));

            RectTransform controlsCard = CreatePanel("ControlsCard", _gameplayRoot.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 34f), new Vector2(430f, 176f), PanelNavy);
            AddAccent(controlsCard, Gold);
            CreateTextAt("ControlsTitle", controlsCard, "QUICK CONTROLS", 14, FontStyle.Bold, Gold, TextAnchor.UpperLeft, new Vector2(24f, -15f), new Vector2(380f, 24f));
            _controlsText = CreateTextAt("Controls", controlsCard, "SPACE  Cast   •   F  Hook   •   R / LMB  Reel\nARROWS  Rod pose   •   T  Recenter", 16, FontStyle.Normal, TextPrimary, TextAnchor.UpperLeft, new Vector2(24f, -47f), new Vector2(380f, 112f));

            RectTransform gaugeCard = CreatePanel("GaugeCard", _gameplayRoot.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 34f), new Vector2(500f, 326f), PanelNavy);
            AddAccent(gaugeCard, Aqua);
            CreateTextAt("GaugeTitle", gaugeCard, "FIGHT STATUS", 14, FontStyle.Bold, Aqua, TextAnchor.UpperLeft, new Vector2(24f, -13f), new Vector2(450f, 24f));
            _behaviorText = CreateTextAt("FishBehavior", gaugeCard, "FISH MOVE  •  WAITING FOR HOOK", 13, FontStyle.Bold, TextSecondary, TextAnchor.UpperLeft, new Vector2(24f, -42f), new Vector2(452f, 24f));
            _castBar = CreateBar("CastPower", gaugeCard, "CAST POWER", new Vector2(24f, -82f), new Vector2(452f, 44f), false);
            _tensionBar = CreateBar("Tension", gaugeCard, "LINE TENSION", new Vector2(24f, -138f), new Vector2(452f, 44f), true);
            _healthBar = CreateBar("FishHealth", gaugeCard, "FISH STAMINA", new Vector2(24f, -194f), new Vector2(452f, 44f), false);
            _lineBar = CreateBar("Line", gaugeCard, "LINE CONDITION", new Vector2(24f, -250f), new Vector2(452f, 44f), false);

            RectTransform recentCatchChip = CreatePanel("RecentCatchChip", _gameplayRoot.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -139f), new Vector2(610f, 38f), new Color(0.025f, 0.09f, 0.115f, 0.82f));
            _recentCatchText = CreateText("RecentCatch", recentCatchChip, "No catches yet — your first one is waiting", 14, FontStyle.Bold, TextSecondary, TextAnchor.MiddleCenter);
            Stretch(_recentCatchText.rectTransform, 4f);

            _centerAlertPanel = CreatePanel("CenterAlert", _gameplayRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(690f, 100f), DeepNavy).gameObject;
            AddOutline(_centerAlertPanel, new Color(1f, 0.75f, 0.25f, 0.7f));
            _centerAlertText = CreateText("Alert", _centerAlertPanel.transform, "BITE!  PRESS F", 36, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Stretch(_centerAlertText.rectTransform, 16f);
            _centerAlertPanel.SetActive(false);

            Text debugHint = CreateText("DebugHint", _gameplayRoot.transform, "F3  DEVELOPER INFO", 12, FontStyle.Bold, new Color(0.65f, 0.78f, 0.79f, 0.8f), TextAnchor.MiddleRight);
            SetRect(debugHint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-38f, -128f), new Vector2(210f, 28f));
        }

        private void BuildStartPanel()
        {
            _startPanel = CreatePanel("StartScreen", _canvasRoot.transform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, Color.black).gameObject;

            GameObject backdropObject = new GameObject("SeaBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            backdropObject.transform.SetParent(_startPanel.transform, false);
            _startBackdrop = backdropObject.GetComponent<RawImage>();
            _startBackdrop.texture = Resources.Load<Texture2D>("SeaFishingTitleBackground");
            _startBackdrop.color = Color.white;
            _startBackdrop.raycastTarget = false;
            Stretch(_startBackdrop.rectTransform, 0f);
            CreatePanel("BackdropShade", _startPanel.transform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, new Color(0.01f, 0.035f, 0.055f, 0.34f));

            RectTransform card = CreatePanel("StartCard", _startPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 590f), new Color(0.018f, 0.075f, 0.105f, 0.94f));
            AddOutline(card.gameObject, new Color(Aqua.r, Aqua.g, Aqua.b, 0.55f));
            CreateCenteredText("Eyebrow", card, "LOCAL SEA-FISHING PROTOTYPE", 15, FontStyle.Bold, Aqua, TextAnchor.MiddleCenter, new Vector2(0f, 242f), new Vector2(680f, 30f));
            CreateCenteredText("Title", card, "OPEN SEA", 58, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, 172f), new Vector2(680f, 78f));
            CreateCenteredText("Tagline", card, "CAST  •  HOOK  •  FIGHT  •  CATCH", 18, FontStyle.Bold, Gold, TextAnchor.MiddleCenter, new Vector2(0f, 116f), new Vector2(680f, 34f));
            CreateCenteredText("ReadyHint", card, "A three-minute coastal fishing challenge", 18, FontStyle.Normal, TextSecondary, TextAnchor.MiddleCenter, new Vector2(0f, 64f), new Vector2(680f, 34f));

            RectTransform howTo = CreatePanel("HowTo", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(650f, 166f), SoftPanel);
            CreateTextAt("HowToTitle", howTo, "HOW TO PLAY", 14, FontStyle.Bold, Aqua, TextAnchor.UpperCenter, new Vector2(0f, -14f), new Vector2(600f, 26f));
            _howToText = CreateTextAt("HowToText", howTo, "1   Hold and release SPACE to cast\n2   Press F only on BITE to set the hook\n3   Follow RUN direction with ARROWS\n4   Reel during safe pressure; stop in danger", 16, FontStyle.Normal, TextPrimary, TextAnchor.UpperLeft, new Vector2(34f, -46f), new Vector2(582f, 112f));

            Button startButton = CreateButton("StartButton", card, "START FISHING", new Vector2(0f, -202f), new Vector2(360f, 66f), Aqua);
            startButton.onClick.AddListener(() =>
            {
                StartLocalRound();
            });
            CreateCenteredText("Footnote", card, "Keyboard / Mock input • IoT and network intentionally disconnected", 13, FontStyle.Normal, TextSecondary, TextAnchor.MiddleCenter, new Vector2(0f, -260f), new Vector2(680f, 28f));
        }

        private void UpdateStartBackdropCrop()
        {
            if (_startBackdrop == null || _startBackdrop.texture == null || Screen.height <= 0) return;
            float textureAspect = (float)_startBackdrop.texture.width / _startBackdrop.texture.height;
            float screenAspect = (float)Screen.width / Screen.height;
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            if (screenAspect > textureAspect)
            {
                uv.height = textureAspect / screenAspect;
                uv.y = (1f - uv.height) * 0.5f;
            }
            else
            {
                uv.width = screenAspect / textureAspect;
                uv.x = (1f - uv.width) * 0.5f;
            }
            _startBackdrop.uvRect = uv;
        }

        private void BuildCountdownPanel()
        {
            _countdownPanel = CreatePanel("Countdown", _canvasRoot.transform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, new Color(0.01f, 0.05f, 0.07f, 0.62f)).gameObject;
            CreateCenteredText("GetReady", _countdownPanel.transform, "GET READY", 25, FontStyle.Bold, Aqua, TextAnchor.MiddleCenter, new Vector2(0f, 130f), new Vector2(700f, 60f));
            _countdownText = CreateCenteredText("Count", _countdownPanel.transform, "3", 150, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(500f, 190f));
            CreateCenteredText("CountdownHint", _countdownPanel.transform, "Your first target is waiting below the surface", 18, FontStyle.Normal, TextSecondary, TextAnchor.MiddleCenter, new Vector2(0f, -130f), new Vector2(700f, 46f));
        }

        private void BuildResultPanel()
        {
            _resultPanel = CreatePanel("ResultScreen", _canvasRoot.transform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, new Color(0.01f, 0.045f, 0.065f, 0.78f)).gameObject;
            RectTransform card = CreatePanel("ResultCard", _resultPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f), DeepNavy);
            AddOutline(card.gameObject, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
            _resultTitleText = CreateCenteredText("ResultTitle", card, "TIME'S UP!", 40, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, 188f), new Vector2(680f, 60f));
            _resultScoreLabelText = CreateCenteredText("ScoreLabel", card, "FINAL SCORE", 15, FontStyle.Bold, TextSecondary, TextAnchor.MiddleCenter, new Vector2(0f, 118f), new Vector2(680f, 30f));
            _resultScoreText = CreateCenteredText("ResultScore", card, "0", 72, FontStyle.Bold, Gold, TextAnchor.MiddleCenter, new Vector2(0f, 54f), new Vector2(680f, 90f));
            _resultDetailText = CreateCenteredText("ResultDetail", card, "0 FISH CAUGHT", 18, FontStyle.Normal, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, -55f), new Vector2(650f, 100f));
            Button restartButton = CreateButton("RestartButton", card, "FISH AGAIN", new Vector2(0f, -168f), new Vector2(330f, 62f), Aqua);
            restartButton.onClick.AddListener(() =>
            {
                StartLocalRound();
            });
            _resultFootnoteText = CreateCenteredText("ResultFootnote", card, "Each new round reshuffles species and fight behavior", 13, FontStyle.Normal, TextSecondary, TextAnchor.MiddleCenter, new Vector2(0f, -224f), new Vector2(680f, 28f));
        }

        private void StartLocalRound()
        {
            if (_standaloneBootstrap == null) _standaloneBootstrap = GetComponent<FishingStandaloneBootstrap>();
            if (_standaloneBootstrap != null)
            {
                _standaloneBootstrap.RestartRound();
                return;
            }

            if (controller != null) controller.BeginRound();
        }

        private void BuildDebugPanel()
        {
            _debugPanel = CreatePanel("DebugPanel", _canvasRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -284f), new Vector2(430f, 600f), new Color(0.01f, 0.025f, 0.035f, 0.94f)).gameObject;
            AddOutline(_debugPanel, new Color(1f, 1f, 1f, 0.18f));
            _debugText = CreateText("DebugText", _debugPanel.transform, "DEVELOPER OVERLAY  [F3]", 14, FontStyle.Normal, TextPrimary, TextAnchor.UpperLeft);
            Stretch(_debugText.rectTransform, 20f);
            _debugPanel.SetActive(false);
        }

        private BarWidgets CreateBar(string name, Transform parent, string label, Vector2 position, Vector2 size, bool showSafeZone)
        {
            RectTransform root = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            Text labelText = CreateTextAt("Label", root, label, 13, FontStyle.Bold, TextSecondary, TextAnchor.UpperLeft, Vector2.zero, new Vector2(size.x * 0.68f, 20f));
            Text valueText = CreateTextAt("Value", root, "0%", 13, FontStyle.Bold, TextPrimary, TextAnchor.UpperRight, new Vector2(size.x * 0.68f, 0f), new Vector2(size.x * 0.32f, 20f));
            RectTransform track = CreatePanel("Track", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 15f), new Color(0.01f, 0.055f, 0.07f, 1f));
            if (showSafeZone)
            {
                RectTransform safe = CreatePanel("SafeZone", track, new Vector2(0.30f, 0f), new Vector2(0.75f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.15f, 0.55f, 0.35f, 0.30f));
                safe.SetAsFirstSibling();
            }
            RectTransform fill = CreatePanel("Fill", track, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, Aqua);
            return new BarWidgets { Root = root.gameObject, Label = labelText, Value = valueText, Fill = fill.GetComponent<Image>() };
        }

        private static void SetBar(BarWidgets widgets, float value, Color color, string valueText)
        {
            value = Mathf.Clamp01(value);
            widgets.Fill.color = color;
            RectTransform rect = widgets.Fill.rectTransform;
            rect.anchorMax = new Vector2(value, 1f);
            widgets.Value.text = valueText;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreatePanel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, color);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            Text text = CreateText("Label", rect, label, 18, FontStyle.Bold, DeepNavy, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6f);
            return button;
        }

        private Text CreateTextAt(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor alignment, Vector2 position, Vector2 dimensions)
        {
            Text text = CreateText(name, parent, value, size, style, color, alignment);
            SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, dimensions);
            return text;
        }

        private Text CreateCenteredText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor alignment, Vector2 position, Vector2 dimensions)
        {
            Text text = CreateText(name, parent, value, size, style, color, alignment);
            SetRect(text.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, position, dimensions);
            return text;
        }

        private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            instance.transform.SetParent(parent, false);
            Text text = instance.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax, pivot, position, size);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            SetRect(rect, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, new Vector2(-inset * 2f, -inset * 2f));
        }

        private static void AddAccent(RectTransform panel, Color color)
        {
            CreatePanel("Accent", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 0f), color);
        }

        private static void AddOutline(GameObject target, Color color)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            GameObject eventSystem = new GameObject("FishingUIEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(transform, false);
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static float NormalizedHealth(FishingSnapshot snapshot)
        {
            return snapshot.FishMaxHealth <= 0f ? 0f : snapshot.FishHealth / snapshot.FishMaxHealth;
        }

        private static string BehaviorLabel(FishingSnapshot snapshot)
        {
            if (snapshot.State == FishingPlayerState.Waiting && snapshot.IsNibbling) return "FISH MOVE  •  LIGHT NIBBLE — WAIT";
            if (snapshot.State != FishingPlayerState.Fighting) return "FISH MOVE  •  WAITING FOR HOOK";
            if (snapshot.IsRunTelegraphing)
            {
                return snapshot.RunTelegraphDirectionNormalized < 0f
                    ? $"FISH MOVE  •  RUN INCOMING LEFT  {snapshot.RunTelegraphRemainingSeconds:0.0}s"
                    : $"FISH MOVE  •  RUN INCOMING RIGHT  {snapshot.RunTelegraphRemainingSeconds:0.0}s";
            }
            if (snapshot.Feedback.State == FishingFeedbackState.Run)
            {
                return snapshot.FightDirection < 0f
                    ? $"FISH MOVE  •  RUN LEFT  {snapshot.FightPhaseRemainingSeconds:0.0}s"
                    : $"FISH MOVE  •  RUN RIGHT  {snapshot.FightPhaseRemainingSeconds:0.0}s";
            }
            if (snapshot.Feedback.State == FishingFeedbackState.Rest) return $"FISH MOVE  •  REST  {snapshot.FightPhaseRemainingSeconds:0.0}s";
            return $"FISH MOVE  •  STRUGGLE  {snapshot.FightPhaseRemainingSeconds:0.0}s";
        }

        private static Color BehaviorColor(FishingSnapshot snapshot)
        {
            if (snapshot.State == FishingPlayerState.Waiting && snapshot.IsNibbling) return TextSecondary;
            if (snapshot.IsRunTelegraphing) return Coral;
            if (snapshot.Feedback.State == FishingFeedbackState.Run) return Coral;
            if (snapshot.Feedback.State == FishingFeedbackState.Rest) return Aqua;
            if (snapshot.Feedback.State == FishingFeedbackState.Fight) return Gold;
            return TextSecondary;
        }

        private static Color TensionColor(float tension)
        {
            if (tension < 0.30f) return new Color(0.30f, 0.60f, 1f, 1f);
            if (tension > 0.75f) return Coral;
            return new Color(0.25f, 0.90f, 0.52f, 1f);
        }

        private static Color DifficultyColor(string difficulty)
        {
            if (difficulty == "Easy") return new Color(0.30f, 0.92f, 0.60f, 1f);
            if (difficulty == "Hard") return Coral;
            return Gold;
        }

        private static string FriendlyState(FishingPlayerState state)
        {
            switch (state)
            {
                case FishingPlayerState.Idle: return "CAST READY";
                case FishingPlayerState.Casting: return "CASTING";
                case FishingPlayerState.Waiting: return "WAITING FOR BITE";
                case FishingPlayerState.BiteWindow: return "BITE WINDOW";
                case FishingPlayerState.Hooked: return "HOOKED";
                case FishingPlayerState.Fighting: return "FISH ON THE LINE";
                case FishingPlayerState.Caught: return "CATCH CONFIRMED";
                case FishingPlayerState.Escaped: return "ATTEMPT FAILED";
                default: return "PREPARING NEXT CAST";
            }
        }

        private struct BarWidgets
        {
            public GameObject Root;
            public Text Label;
            public Text Value;
            public Image Fill;
        }
    }
}
