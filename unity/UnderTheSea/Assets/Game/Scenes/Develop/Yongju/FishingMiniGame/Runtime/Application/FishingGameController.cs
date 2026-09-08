using System;
using System.Collections.Generic;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FishingGameController : MonoBehaviour
    {
        [SerializeField] private FishingGameConfigAsset config;
        [SerializeField] private bool autoStartRound;
        [SerializeField] private bool logStateChanges = true;

        private IFishingInputSource _inputSource;
        private IFishingFeedbackOutput _feedbackOutput;
        private IFishingResistanceOutput _resistanceOutput;
        private FishingV2ResistanceMapper _resistanceMapper;
        private LocalFishingAuthority _authority;
        private FishingRoundTracker _roundTracker;
        private SingleFishSessionTracker _sessionTracker;
        private FishingLaunchContext _launchContext;
        private FishingRules _baseRules;
        private FishProfile[] _fishCatalog;
        private FishProfile _sessionFishOverride;
        private System.Random _fishRandom;
        private FishingPlayerState _lastLoggedState;
        private FishingGameMode? _modeOverride;
        private FishingGameMode _gameMode;
        private int _nextFishIndex;
        private bool _prepareNextFish;
        private bool _sessionHookCommitted;
        private bool _paused;
        private bool _initialized;

        public FishingSnapshot Snapshot => _authority?.Current;
        public FishingCycleResult LastResult => _authority?.LastResult;
        public FishingRoundSnapshot RoundSnapshot => _roundTracker?.Current;
        public FishingRoundResult LastRoundResult => _roundTracker?.Result;
        public FishingSessionSnapshot SessionSnapshot => _sessionTracker?.Current;
        public FishingSessionResult LastSessionResult => _sessionTracker?.Result;
        public FishingInputFrame LastInputFrame { get; private set; }
        public FishingResistanceCommand LastResistanceCommand { get; private set; } = FishingResistanceCommand.Zero;
        public bool IsResistanceStopped { get; private set; } = true;
        public IReadOnlyList<FishingCatchRecord> CatchHistory => _roundTracker?.Catches;
        public FishingFeedbackFrame LastFeedback => _feedbackOutput is MockFishingFeedbackOutput mock
            ? mock.LastFrame
            : Snapshot?.Feedback ?? default;
        public FishingGameConfigAsset Config => config;
        public FishingGameMode Mode => _gameMode;
        public bool IsPaused => _paused;
        public event Action<FishingCycleResult> CycleFinished;
        public event Action<FishingRoundResult> RoundFinished;
        public event Action<FishingSessionResult> SessionFinished;

        private void Awake()
        {
            InitializeRuntime();
        }

        private void OnEnable()
        {
            InitializeRuntime();
        }

        private void Start()
        {
            if (autoStartRound) BeginRound();
        }

        private void Update()
        {
            if (!HasRuntimeDependencies()) InitializeRuntime();
            if (!HasRuntimeDependencies() || _paused) return;

            float deltaTime = Time.unscaledDeltaTime;
            if (_gameMode == FishingGameMode.SingleFishSession) UpdateSingleFishSession(deltaTime);
            else UpdateLegacyRound(deltaTime);
        }

        private void OnDisable()
        {
            _feedbackOutput?.StopFeedback();
            StopResistanceFeedback(Snapshot);
        }

        private void OnDestroy()
        {
            if (_authority != null) _authority.CycleFinished -= OnCycleFinished;
            if (_roundTracker != null) _roundTracker.Completed -= OnRoundFinished;
            if (_sessionTracker != null) _sessionTracker.Completed -= OnSessionFinished;
            _feedbackOutput?.StopFeedback();
            StopResistanceFeedback(Snapshot);
        }

        public void Configure(FishingGameConfigAsset gameConfig, bool shouldAutoStart)
        {
            config = gameConfig;
            autoStartRound = shouldAutoStart;
        }

        public void ConfigureFlowMode(FishingGameMode mode, FishProfile sessionFish = null)
        {
            InitializeRuntime();
            _modeOverride = mode;
            _sessionFishOverride = sessionFish?.Copy();
            RebuildRound(_launchContext);
        }

        public void InitializeRuntime()
        {
            if (HasRuntimeDependencies()) return;

            if (_authority != null) _authority.CycleFinished -= OnCycleFinished;
            if (_roundTracker != null) _roundTracker.Completed -= OnRoundFinished;
            if (_sessionTracker != null) _sessionTracker.Completed -= OnSessionFinished;
            _feedbackOutput?.StopFeedback();
            StopResistanceFeedback(Snapshot);

            _inputSource = new KeyboardFishingInputSource("local-player");
            _feedbackOutput = new MockFishingFeedbackOutput();
            _resistanceMapper = new FishingV2ResistanceMapper();
            _resistanceOutput = new MockFishingResistanceOutput();
            LastResistanceCommand = FishingResistanceCommand.Zero;
            IsResistanceStopped = true;
            _authority = new LocalFishingAuthority();
            _authority.CycleFinished += OnCycleFinished;
            _roundTracker = new FishingRoundTracker();
            _roundTracker.Completed += OnRoundFinished;
            _sessionTracker = new SingleFishSessionTracker();
            _sessionTracker.Completed += OnSessionFinished;
            _initialized = true;
            RebuildRound(config != null ? config.BuildLaunchContext() : new FishingLaunchContext());
        }

        public void ConfigureLaunchContext(FishingLaunchContext context)
        {
            InitializeRuntime();
            RebuildRound(context ?? (config != null ? config.BuildLaunchContext() : new FishingLaunchContext()));
        }

        public void BeginRound()
        {
            InitializeRuntime();
            _paused = false;

            if (_gameMode == FishingGameMode.SingleFishSession)
            {
                if (_sessionTracker.Current.State == FishingSessionState.Completed ||
                    _sessionTracker.Current.State == FishingSessionState.Aborted)
                {
                    RebuildRound(_launchContext);
                }
                _sessionTracker.Begin();
                return;
            }

            if (_roundTracker.Current.State == FishingRoundState.Completed ||
                _roundTracker.Current.State == FishingRoundState.Aborted)
            {
                RebuildRound(_launchContext);
            }
            _roundTracker.Begin();
        }

        public void StopRound()
        {
            if (!HasRuntimeDependencies()) return;
            if (_gameMode == FishingGameMode.SingleFishSession) _sessionTracker.Abort();
            else _roundTracker.Stop();
            StopResistanceFeedback(Snapshot);
        }

        public void AbortRound()
        {
            if (!HasRuntimeDependencies()) return;
            if (_gameMode == FishingGameMode.SingleFishSession) _sessionTracker.Abort();
            else _roundTracker.Abort();
            StopResistanceFeedback(Snapshot);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                _feedbackOutput?.StopFeedback();
                StopResistanceFeedback(Snapshot);
            }
        }

        public void SetInputSource(IFishingInputSource inputSource)
        {
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            _inputSource.ResetState();
            LastInputFrame = NeutralInputFrame();
            StopResistanceFeedback(Snapshot);
        }

        public void SetFeedbackOutput(IFishingFeedbackOutput feedbackOutput)
        {
            _feedbackOutput?.StopFeedback();
            _feedbackOutput = feedbackOutput ?? throw new ArgumentNullException(nameof(feedbackOutput));
        }

        public void SetResistanceOutput(IFishingResistanceOutput resistanceOutput)
        {
            if (resistanceOutput == null) throw new ArgumentNullException(nameof(resistanceOutput));
            StopResistanceFeedback(Snapshot);
            _resistanceOutput = resistanceOutput;
            LastResistanceCommand = FishingResistanceCommand.Zero;
            IsResistanceStopped = true;
            _resistanceOutput.Stop();
        }

        public void ResetCycle()
        {
            StopResistanceFeedback(Snapshot);
            if (!IsActivePlayFlow()) return;
            _inputSource?.ResetState();
            _authority?.ResetCycle();
            _feedbackOutput?.StopFeedback();
            _prepareNextFish = false;
            _sessionHookCommitted = false;
            if (_authority?.Current != null) _lastLoggedState = _authority.Current.State;
        }

        private void UpdateLegacyRound(float deltaTime)
        {
            _roundTracker.Tick(deltaTime);
            if (_roundTracker.Current.State != FishingRoundState.Playing)
            {
                StopResistanceFeedback(Snapshot);
                if (_roundTracker.Current.State == FishingRoundState.Completed ||
                    _roundTracker.Current.State == FishingRoundState.Aborted)
                {
                    _feedbackOutput.StopFeedback();
                }
                return;
            }

            TickFishing(deltaTime);
            PrepareNextFishIfReady();
        }

        private void UpdateSingleFishSession(float deltaTime)
        {
            _sessionTracker.Tick(deltaTime);
            if (_sessionTracker.Current.State != FishingSessionState.Playing)
            {
                StopResistanceFeedback(Snapshot);
                if (_sessionTracker.Current.State == FishingSessionState.Completed ||
                    _sessionTracker.Current.State == FishingSessionState.Aborted)
                {
                    _feedbackOutput.StopFeedback();
                }
                return;
            }

            if (_authority.Current.State == FishingPlayerState.Hooked ||
                _authority.Current.State == FishingPlayerState.Fighting)
            {
                _sessionHookCommitted = true;
            }

            TickFishing(deltaTime);

            if (_authority.Current.State == FishingPlayerState.Hooked ||
                _authority.Current.State == FishingPlayerState.Fighting)
            {
                _sessionHookCommitted = true;
            }
            else if (_authority.Current.State == FishingPlayerState.Idle)
            {
                _sessionHookCommitted = false;
            }
        }

        private void TickFishing(float deltaTime)
        {
            LastInputFrame = _inputSource.ReadFrame();
            _authority.Tick(LastInputFrame, deltaTime);
            _feedbackOutput.ApplyFeedback(_authority.Current.Feedback);
            UpdateResistanceFeedback(deltaTime);
            LogStateTransitionIfNeeded();
        }

        private bool HasRuntimeDependencies()
        {
            return _initialized && _inputSource != null && _feedbackOutput != null &&
                _resistanceOutput != null && _resistanceMapper != null &&
                _authority != null && _roundTracker != null && _sessionTracker != null &&
                _fishCatalog != null;
        }

        private bool IsActivePlayFlow()
        {
            if (_gameMode == FishingGameMode.SingleFishSession)
            {
                return _sessionTracker?.Current.State == FishingSessionState.Playing;
            }
            return _roundTracker?.Current.State == FishingRoundState.Playing;
        }

        private void RebuildRound(FishingLaunchContext context)
        {
            StopResistanceFeedback(Snapshot);
            _launchContext = (context ?? new FishingLaunchContext()).Copy();
            _launchContext.Sanitize();
            _gameMode = _modeOverride ?? (config != null ? config.GameMode : FishingGameMode.LegacyRound);
            _baseRules = config != null ? config.BuildRules() : new FishingRules();
            _baseRules.Sanitize();
            _fishCatalog = config != null ? config.BuildFishProfiles() : new[] { new FishProfile() };
            if (_fishCatalog.Length == 0) _fishCatalog = new[] { new FishProfile() };
            _fishRandom = new System.Random(_launchContext.Seed);
            _nextFishIndex = 0;
            _prepareNextFish = false;
            _sessionHookCommitted = false;
            _paused = false;

            FishProfile firstFish;
            if (_gameMode == FishingGameMode.SingleFishSession)
            {
                firstFish = _sessionFishOverride?.Copy() ??
                    (config != null ? config.BuildSessionFishProfile() : _fishCatalog[0].Copy());
                firstFish.Sanitize();
            }
            else
            {
                firstFish = SelectNextFish();
            }

            _authority.Initialize(new FishingRoundContext
            {
                ParticipantId = _launchContext.LocalParticipantId,
                Seed = _launchContext.Seed,
                Rules = _baseRules,
                Fish = firstFish,
                UseV2PreFightFlow = _gameMode == FishingGameMode.SingleFishSession,
                V2PreFightTuning = config != null
                    ? config.BuildV2PreFightTuning()
                    : new FishingV2PreFightTuning(),
                UseV2FightModel = _gameMode == FishingGameMode.SingleFishSession,
                V2FightTuning = new FishingV2FightTuning(),
                V2FishAITuning = new FishingV2FishAITuning()
            });
            _roundTracker.Initialize(_launchContext);
            _sessionTracker.Initialize(new FishingSessionContext
            {
                SessionId = _launchContext.RoundId,
                ParticipantId = _launchContext.LocalParticipantId,
                CountdownSeconds = _launchContext.CountdownSeconds,
                Fish = firstFish
            });
            _inputSource.ResetState();
            LastInputFrame = NeutralInputFrame();
            _feedbackOutput.StopFeedback();
            StopResistanceFeedback(_authority.Current);
            _lastLoggedState = _authority.Current.State;
        }

        private FishProfile SelectNextFish()
        {
            if (config == null || config.CycleFishInCatalogOrder)
            {
                FishProfile selected = _fishCatalog[_nextFishIndex % _fishCatalog.Length];
                _nextFishIndex++;
                return selected;
            }

            float totalWeight = 0f;
            for (int i = 0; i < _fishCatalog.Length; i++) totalWeight += _fishCatalog[i].RarityWeight;
            double roll = _fishRandom.NextDouble() * totalWeight;
            for (int i = 0; i < _fishCatalog.Length; i++)
            {
                roll -= _fishCatalog[i].RarityWeight;
                if (roll <= 0d) return _fishCatalog[i];
            }
            return _fishCatalog[_fishCatalog.Length - 1];
        }

        private void PrepareNextFishIfReady()
        {
            if (!_prepareNextFish || _authority.Current.State != FishingPlayerState.Idle) return;
            _authority.PrepareCycle(SelectNextFish(), _baseRules);
            _prepareNextFish = false;
            _lastLoggedState = _authority.Current.State;
        }

        private void OnCycleFinished(FishingCycleResult result)
        {
            if (_gameMode == FishingGameMode.SingleFishSession)
            {
                bool postHook = result.WasCaught || _sessionHookCommitted ||
                    _authority.Current.State == FishingPlayerState.Hooked ||
                    _authority.Current.State == FishingPlayerState.Fighting;
                if (postHook) _sessionTracker.CompletePostHook(result);
                else _sessionTracker.RecordPreHookFailure(result);
            }
            else
            {
                _roundTracker.RecordCycle(result);
                _prepareNextFish = true;
            }

            CycleFinished?.Invoke(result);
            if (logStateChanges)
            {
                Debug.Log(result.WasCaught
                    ? $"[Fishing] Cycle {result.CycleNumber} caught {result.FishId}, score +{result.AwardedScore}."
                    : $"[Fishing] Cycle {result.CycleNumber} escaped: {result.EscapeReason}.", this);
            }
        }

        private void OnRoundFinished(FishingRoundResult result)
        {
            _feedbackOutput?.StopFeedback();
            StopResistanceFeedback(Snapshot);
            RoundFinished?.Invoke(result);
            if (logStateChanges)
            {
                Debug.Log($"[Fishing] Round finished: {result.EndReason}, score {result.TotalScore}, caught {result.CaughtCount}.", this);
            }
        }

        private void OnSessionFinished(FishingSessionResult result)
        {
            _feedbackOutput?.StopFeedback();
            StopResistanceFeedback(Snapshot);
            SessionFinished?.Invoke(result);
            if (logStateChanges)
            {
                Debug.Log($"[Fishing] Single fish session finished: {result.Outcome}, fish {result.FishId}, retries {result.PreHookFailureCount}.", this);
            }
        }

        private void LogStateTransitionIfNeeded()
        {
            if (!logStateChanges || _authority.Current.State == _lastLoggedState) return;
            _lastLoggedState = _authority.Current.State;
            Debug.Log($"[Fishing] State -> {_lastLoggedState}", this);
        }

        private void UpdateResistanceFeedback(float deltaTime)
        {
            FishingSnapshot snapshot = Snapshot;
            if (!CanApplyResistance(snapshot))
            {
                StopResistanceFeedback(snapshot);
                return;
            }

            FishingResistanceCommand command = _resistanceMapper.Tick(snapshot, deltaTime);
            LastResistanceCommand = command;
            IsResistanceStopped = false;
            _resistanceOutput.ApplyCommand(command);
        }

        private bool CanApplyResistance(FishingSnapshot snapshot)
        {
            return _gameMode == FishingGameMode.SingleFishSession &&
                _sessionTracker?.Current.State == FishingSessionState.Playing &&
                snapshot != null &&
                snapshot.State == FishingPlayerState.Fighting &&
                !_paused &&
                LastInputFrame.IsDeviceConnected;
        }

        private void StopResistanceFeedback(FishingSnapshot snapshot)
        {
            _resistanceMapper?.ResetToSafe(snapshot);
            LastResistanceCommand = FishingResistanceCommand.Zero;
            IsResistanceStopped = true;
            _resistanceOutput?.Stop();
        }

        private FishingInputFrame NeutralInputFrame()
        {
            return new FishingInputFrame
            {
                ParticipantId = _launchContext?.LocalParticipantId ?? "local-player",
                TensionNormalized = 0.5f,
                IsDeviceConnected = true
            };
        }
    }
}
