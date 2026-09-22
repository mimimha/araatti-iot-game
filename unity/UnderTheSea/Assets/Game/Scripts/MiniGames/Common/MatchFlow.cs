using System;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>매칭 한 판이 지나가는 단계.</summary>
    public enum MatchState
    {
        /// <summary>아직 아무 게임도 고르지 않았다.</summary>
        Idle,

        /// <summary>사람을 기다리는 중.</summary>
        Matching,

        /// <summary>조건이 다 맞아서 숫자를 세는 중.</summary>
        Countdown,

        /// <summary>다 셌다. 씬을 여는 중 — 이 상태에서는 시작 요청을 전부 무시한다.</summary>
        Starting,

        /// <summary>미니게임이 돌고 있다.</summary>
        InGame,

        /// <summary>결과 화면.</summary>
        Result,
    }

    /// <summary>
    /// 매칭 화면의 규칙.
    ///
    /// 이 파일은 <b>화면을 그리지 않고 씬도 열지 않는다.</b> 지금 시작할 수 있는지만 판단하고
    /// 바뀔 때마다 알린다. 그리는 것은 <see cref="UI.MatchPanelPresenter"/> 가, 씬을 여는 것은
    /// <see cref="MatchFlowController"/> 가 한다. 그래야 테스트 씬에서는 가짜 게임으로,
    /// 실제 포탈에서는 진짜 씬 이동으로 같은 흐름을 쓸 수 있다.
    ///
    /// 인원은 <see cref="PlayerRoster"/> 가 바뀔 때마다 다시 센다. 깨어날 때 한 번 세고 마는
    /// 구조였다면 사람이 들어온 뒤에도 시작 버튼이 잠긴 채였을 것이다.
    ///
    /// 시작은 두 길이다. 사람이 [게임 시작] 을 누르거나(<see cref="RequestStart"/>), 시작할 수 있게 된
    /// 뒤 <see cref="MiniGameConfig.AutoStartSeconds"/>(30초) 가 지나면 알아서 센다. 세 게임 모두
    /// 같은 규칙이다 — 배도 4명이 모인 순간이 아니라 그로부터 30초 뒤에 알아서 시작한다.
    /// 인원이 바뀌면 30초를 다시 잰다. 자동 시작은 <see cref="autoStart"/> 로 끌 수 있다.
    ///
    /// 시작이 두 번 일어나지 않도록 두 겹으로 막는다 — 상태가 <see cref="MatchState.Countdown"/>
    /// 이 아니면 세지 않고, 다 센 뒤에는 <see cref="MatchState.Starting"/> 으로 넘어가
    /// <see cref="LaunchRequested"/> 를 판당 한 번만 올린다. 버튼 연타로 씬이 두 번
    /// 열리는 일은 여기서 끝난다.
    /// </summary>
    public sealed class MatchFlow : MonoBehaviour
    {
        [SerializeField] private MiniGameConfig config;

        [SerializeField, Min(1f)]
        [Tooltip("조건이 맞은 뒤 실제로 게임이 열릴 때까지 세는 시간(초).")]
        private float countdownSeconds = 5f;

        [SerializeField]
        [Tooltip("켜 두면 씬이 시작될 때 이 기기의 플레이어를 자동으로 한 명 넣는다. 네트워크가 붙으면 끈다.")]
        private bool joinLocalPlayerOnStart = true;

        [SerializeField]
        [Tooltip("켜면 시작할 수 있게 된 뒤 Config.AutoStartSeconds 가 지나면 알아서 카운트다운을 시작한다. " +
                 "끄면 [게임 시작] 버튼으로만 시작한다.")]
        private bool autoStart = true;

        /// <summary>상태가 바뀔 때마다.</summary>
        public event Action<MatchState> StateChanged;

        /// <summary>인원이나 시작 조건이 바뀔 때마다. 화면을 다시 그리라는 뜻.</summary>
        public event Action Refreshed;

        /// <summary>카운트다운이 한 칸 줄 때마다. 5, 4, 3, 2, 1 이 넘어온다.</summary>
        public event Action<int> CountdownChanged;

        /// <summary>다 셌다. 이 게임을 열어 달라는 뜻. 한 판에 <b>한 번만</b> 올라온다.</summary>
        public event Action<MiniGameConfig> LaunchRequested;

        /// <summary>[매칭 취소] 를 눌러 파티를 비웠다. 로비로 돌아갈지는 <see cref="MatchFlowController"/> 가 정한다.</summary>
        public event Action MatchCancelled;

        public MiniGameConfig Config => config;
        public MatchState State { get; private set; } = MatchState.Idle;

        /// <summary>카운트다운 길이(초). 화면이 "5초" 같은 안내를 쓸 때 참고한다.</summary>
        public float CountdownSeconds => countdownSeconds;

        public int PlayerCount => PlayerRoster.ActivePlayerCount;

        /// <summary>
        /// 지금 인원으로 시작할 수 있는가.
        ///
        /// 조건식은 여기 한 곳에만 둔다. 화면이 자기 나름의 if 문을 갖게 되면 규칙이 바뀔 때
        /// 한 군데만 고치고 마는 일이 생긴다.
        /// </summary>
        public bool CanStartMatch()
        {
            if (config == null) return false;
            if (State != MatchState.Matching) return false;

            // 서버가 정하는 판에서는 이 화면이 시작시키지 않는다. 시작 버튼도 감춘다.
            if (ServerDriven) return false;

            return config.CanStart(PlayerRoster.ReadyCount);
        }

        /// <summary>카운트다운에 남은 초. 화면에 크게 보여 줄 숫자.</summary>
        public int CountdownRemaining { get; private set; }

        /// <summary>자동 시작까지 남은 초. <see cref="autoStart"/> 가 꺼져 있으면 늘 0.</summary>
        public float AutoStartRemaining { get; private set; }

        /// <summary>자동 시작을 쓰는가. 끄면 버튼으로만 시작한다.</summary>
        public bool AutoStartEnabled => autoStart;

        /// <summary>
        /// **매칭을 로비 서버가 정하는가.**
        ///
        /// 켜지면 이 화면은 <b>아무것도 시작시키지 않는다.</b> 몇 명이 모였는지 보여 주기만
        /// 하고, 출발은 로비 서버가 <see cref="PlayerMatchRelay"/> 로 알려 준다.
        ///
        /// 자동 시작 시계와 [게임 시작] 버튼은 여기서 함께 꺼진다. 둘 다 "이 화면이 판을
        /// 시작시킨다" 는 전제 위에 있어서, 서버가 정하는 판에서는 <b>거짓말</b>이 된다 —
        /// 시계가 0이 되어도 아무 일도 일어나지 않고, 버튼을 눌러도 마찬가지다.
        /// </summary>
        public bool ServerDriven { get; private set; }

        /// <summary>
        /// 이번 판의 인원. 서버가 정해 준 값이다. 0 이면 설정의 정원을 쓴다.
        ///
        /// <see cref="MiniGameConfig.MaxPlayers"/> 와 다를 수 있다 — 광산은 정원이 4명이지만
        /// 3명짜리 판을 고를 수 있다. 판은 늘 정원만큼 그리고, 이 수를 넘는 칸은 ✕ 로 막는다.
        /// </summary>
        public int RoomSize { get; private set; }

        /// <summary>이번 판에 실제로 쓰는 자리 수.</summary>
        public int EffectiveRoomSize =>
            RoomSize > 0 ? RoomSize : (config != null ? config.MaxPlayers : 0);

        /// <summary>
        /// 네트워크가 명단을 채울 때 테스트용 로컬 플레이어 자동 참가만 끈다.
        /// 30초 자동 시작과 [게임 시작] 버튼은 기존 규칙대로 계속 동작한다.
        /// </summary>
        public void SetNetworkManagedRoster(bool enabled)
        {
            joinLocalPlayerOnStart = !enabled;
        }

        /// <summary>화면 전환이 끝난 시점부터 자동 시작 시간을 온전히 다시 센다.</summary>
        public void RestartAutoStartTimer()
        {
            if (State != MatchState.Matching) return;
            RestartAutoStartWindow();
            Refreshed?.Invoke();
        }

        private float countdownTimer;
        private bool waitingForAutoStart;
        private bool launchRaised;

        private void OnEnable() => PlayerRoster.Changed += OnRosterChanged;
        private void OnDisable() => PlayerRoster.Changed -= OnRosterChanged;

        private void Start()
        {
            if (config == null)
            {
                Debug.LogError("[MatchFlow] 미니게임 설정이 비어 있습니다. MiniGameConfig 를 넣어 주세요.", this);
                enabled = false;
                return;
            }

            PlayerRoster.SelectGame(config);

            if (joinLocalPlayerOnStart && PlayerRoster.ActivePlayerCount == 0)
                PlayerRoster.RegisterNextTestPlayer(isLocal: true);

            EnterMatching();
        }

        private void Update()
        {
            switch (State)
            {
                case MatchState.Matching: TickAutoStart(); break;
                case MatchState.Countdown: TickCountdown(); break;
            }
        }

        // ------------------------------------------------------------
        // 바깥에서 부르는 것
        // ------------------------------------------------------------

        /// <summary>[게임 시작] 을 눌렀을 때. 조건이 맞지 않으면 아무 일도 하지 않는다.</summary>
        public void RequestStart()
        {
            if (!CanStartMatch()) return;

            BeginCountdown();
        }

        /// <summary>
        /// 다른 미니게임 규칙으로 갈아 끼운다.
        ///
        /// 매칭 화면 하나로 세 게임을 다 받으려면 규칙만 바뀌어야 한다. 포탈이 어느 게임을
        /// 골랐는지 알려줄 때, 그리고 테스트에서 규칙을 바꿔 볼 때 쓴다.
        /// </summary>
        /// <summary>
        /// **로비 서버가 정해 준 판으로 화면을 맞춘다.**
        ///
        /// <paramref name="roomSize"/> 는 이번 판의 인원이다. 자동 시작과 시작 버튼은
        /// 여기서 꺼진다 — 출발을 정하는 것은 서버다.
        /// </summary>
        public void Configure(MiniGameConfig next, int roomSize)
        {
            ServerDriven = true;
            RoomSize = Mathf.Max(0, roomSize);

            autoStart = false;
            joinLocalPlayerOnStart = false;

            Configure(next);
        }

        public void Configure(MiniGameConfig next)
        {
            if (next == null) return;

            config = next;
            enabled = true; // 설정이 비어 Start 에서 꺼졌더라도 다시 살린다
            PlayerRoster.SelectGame(config);

            // 포탈에서 열었을 때 아직 아무도 없으면 이 기기의 플레이어를 넣는다.
            // 네트워크가 사람을 넣어 주는 씬에서는 joinLocalPlayerOnStart 를 꺼서 막는다.
            if (joinLocalPlayerOnStart && PlayerRoster.ActivePlayerCount == 0)
                PlayerRoster.RegisterNextTestPlayer(isLocal: true);

            EnterMatching();
        }

        /// <summary>
        /// [매칭 취소]. 파티를 비우고 매칭 상태로 되돌린 뒤 <see cref="MatchCancelled"/> 를 올린다.
        /// 어디로 돌아갈지(로비 씬 등)는 여기서 정하지 않는다.
        /// </summary>
        public void CancelMatch()
        {
            PlayerRoster.ClearPlayers();
            EnterMatching();
            MatchCancelled?.Invoke();
        }

        /// <summary>결과 화면에서 [다시 하기] 로 돌아왔을 때. 인원은 그대로 두고 다시 센다.</summary>
        public void ReturnToMatching() => EnterMatching();

        /// <summary>미니게임이 돌기 시작했다고 알린다. <see cref="MatchFlowController"/> 가 부른다.</summary>
        public void EnterInGame() => SetState(MatchState.InGame);

        /// <summary>결과 화면을 띄웠다고 알린다. <see cref="MatchFlowController"/> 가 부른다.</summary>
        public void EnterResult() => SetState(MatchState.Result);

        /// <summary>
        /// 서버가 셈의 주인일 때 남은 시간을 밀어 넣는다.
        ///
        /// 지금은 각 클라이언트가 스스로 세지만, 서버 시간이 들어오면 이 함수로 덮어써서
        /// 화면만 서버를 따라가게 할 수 있다. 화면 코드는 바뀌지 않는다.
        /// </summary>
        public void OverrideCountdown(float remainingSeconds)
        {
            if (State != MatchState.Countdown) return;

            countdownTimer = Mathf.Max(0f, remainingSeconds);
            PushCountdownNumber();
        }

        // ------------------------------------------------------------
        // 안쪽
        // ------------------------------------------------------------

        private void OnRosterChanged()
        {
            // 숫자를 세는 중에 누가 나가서 조건이 깨지면 세는 것을 멈추고 다시 기다린다.
            if (State == MatchState.Countdown &&
                (config == null || !config.CanStart(PlayerRoster.ReadyCount)))
            {
                EnterMatching();
                return;
            }

            // 인원이 바뀌면 자동 시작까지의 시간을 다시 잰다. 배도 다 모인 순간 바로 세지 않고
            // 같은 시간을 기다린다 — 세 게임의 화면 흐름이 같아야 한다.
            if (State == MatchState.Matching) RestartAutoStartWindow();

            Refreshed?.Invoke();
        }

        private void EnterMatching()
        {
            launchRaised = false;
            CountdownRemaining = 0;
            SetState(MatchState.Matching);
            RestartAutoStartWindow();
            Refreshed?.Invoke();

        }

        /// <summary>
        /// 자동 시작까지의 시간을 다시 잰다. 시작할 수 없는 상태(인원 부족)라면 시계를 아예 돌리지 않는다.
        /// </summary>
        private void RestartAutoStartWindow()
        {
            waitingForAutoStart = autoStart && config != null && CanStartMatch();
            AutoStartRemaining = waitingForAutoStart ? config.AutoStartSeconds : 0f;
        }

        private void TickAutoStart()
        {
            if (!waitingForAutoStart) return;

            AutoStartRemaining -= Time.deltaTime;
            if (AutoStartRemaining > 0f) return;

            AutoStartRemaining = 0f;
            waitingForAutoStart = false;
            BeginCountdown();
        }

        private void BeginCountdown()
        {
            if (State == MatchState.Countdown || State == MatchState.Starting) return;

            waitingForAutoStart = false;
            launchRaised = false;
            countdownTimer = countdownSeconds;
            CountdownRemaining = 0;
            SetState(MatchState.Countdown);
            PushCountdownNumber();
            Refreshed?.Invoke();
        }

        private void TickCountdown()
        {
            countdownTimer -= Time.deltaTime;
            PushCountdownNumber();

            if (countdownTimer > 0f) return;
            if (launchRaised) return;

            launchRaised = true;
            SetState(MatchState.Starting);
            LaunchRequested?.Invoke(config);
        }

        private void PushCountdownNumber()
        {
            int remaining = Mathf.Max(0, Mathf.CeilToInt(countdownTimer));
            if (remaining == CountdownRemaining) return;

            CountdownRemaining = remaining;
            CountdownChanged?.Invoke(remaining);
        }

        private void SetState(MatchState next)
        {
            if (State == next) return;

            State = next;
            StateChanged?.Invoke(State);
        }
    }
}
