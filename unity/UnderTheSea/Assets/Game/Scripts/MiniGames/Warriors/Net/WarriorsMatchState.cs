using System.Linq;
using System.Text;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>매치가 지금 어느 칸에 있는가. 전투 안의 라운드가 아니라 **판 전체**의 상태다.</summary>
    public enum WarriorsMatchPhase
    {
        /// <summary>사람이 모이기를 기다린다.</summary>
        Waiting = 0,

        /// <summary>다 모였다. 시작까지 센다.</summary>
        Countdown = 1,

        /// <summary>해변 몬스터</summary>
        Phase1 = 2,

        /// <summary>크라켄 촉수</summary>
        Phase2 = 3,

        /// <summary>리듬 전투</summary>
        Phase3 = 4,

        /// <summary>목표 달성</summary>
        Cleared = 5,

        /// <summary>두 명 모두 쓰러짐</summary>
        Failed = 6,
    }

    /// <summary>
    /// 매치 한 판의 **공통 상태**를 서버가 정하고 모두에게 보낸다.
    ///
    /// <code>
    ///   대기        두 명이 모일 때까지
    ///   카운트다운  다 모이면 10초. 중간에 빠지면 취소하고 다시 대기
    ///   Phase 1~3   목표 수치를 채우면 다음 칸으로
    ///   Cleared     3페이즈 목표를 채움
    ///   Failed      두 명 모두 Down
    /// </code>
    ///
    /// <b>왜 <c>WarriorsGameFlow</c> 를 쓰지 않는가.</b>
    /// 그쪽은 라운드 전환 · 연출 문구 · 협동 마무리가 한 덩어리로 얽혀 있고
    /// <b>플레이어 한 명만</b> 참조한다. 2인 서버 권위로 옮기려면 통째로 갈아야 한다.
    /// 그 작업은 3~5단계에서 페이즈별로 나눠서 한다. 지금은 그 위에 얹을
    /// <b>판의 뼈대</b>만 서버 권위로 세운다.
    ///
    /// ⚠ <b>시간 제한으로 지지 않는다.</b> 기존 3분 시계는 <c>WarriorsBattleScore</c> 안에서
    ///    네트워크일 때 멈추도록 막아 두었다. 승패는 목표 수치와 Down 으로만 갈린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsMatchState : NetworkBehaviour
    {
        [Header("시작 대기")]
        [Tooltip("이 인원이 모여야 카운트다운을 시작한다.")]
        [SerializeField, Min(1)] private int crewToStart = 2;

        [Tooltip("인원이 모인 뒤 시작까지 세는 시간(초).")]
        [SerializeField, Min(1f)] private float countdownSeconds = 10f;

        [Header("페이즈 목표 — 내일 같이 조정한다")]
        [Tooltip("1페이즈: 혼자 할 때의 팀 합산 처치 수. 인원이 늘면 아래 값만큼 더한다.")]
        [SerializeField, Min(1)] private int phase1TargetKills = 15;

        /// <summary>
        /// 사람이 한 명 늘 때마다 1페이즈 목표에 더할 처치 수.
        ///
        /// <b>왜 인원에 따라 늘리는가.</b> 예전에는 "한 명이 쓰러지면 같은 목표를 혼자 감당하므로
        /// 인원으로 늘리지 않는다" 고 두었다. 그런데 실제 2인 플레이 영상에서 1페이즈가
        /// <b>약 10초</b> 만에 끝났다 — 목표 10 을 두 사람이 광역 타격으로 나눠 치니
        /// 규칙을 파악하기도 전에 라운드가 지나갔다.
        ///
        /// 한 사람 몫(<see cref="phase1TargetKills"/>)에 인원만큼 더해, 1인과 2인의
        /// <b>플레이 시간</b>이 비슷해지도록 맞춘다. 사람이 늘면 화면의 몬스터도 함께 늘어난다.
        /// </summary>
        [Tooltip("사람이 한 명 늘 때마다 1페이즈 목표에 더할 처치 수. 2인이면 기본값 + 이 값.")]
        [SerializeField, Min(0)] private int phase1KillsPerExtraPlayer = 14;

        [Tooltip("2페이즈: 팀 합산 촉수 성공 횟수.")]
        [SerializeField, Min(1)] private int phase2TargetTentacleHits = 15;

        /// <summary>
        /// 3페이즈 목표. **한 사람 몫이고, 인원만큼 곱한다.**
        ///
        /// <b>왜 곱하는가.</b> <c>WarriorsPhase3Director.SpawnPattern</c> 은 <b>살아 있는 사람마다</b>
        /// 묶음을 하나씩 낸다. 2인이면 길이 3짜리 묶음이 성공 6개를 만든다. 그런데 목표는
        /// 팀 합산이라, 사람이 늘수록 최종 라운드가 그대로 절반이 됐다.
        /// 실측에서 2인 3라운드가 <b>26초</b> 만에 끝난 원인이 이것이다 — 노트가 쉬워서가 아니라
        /// 두 사람 몫이 같은 분모를 나눠 채웠기 때문이다.
        ///
        /// 한 바퀴(묶음 8개)가 약 39초, 레인당 18노트다. 성공률 75% 로 잡으면
        /// 한 사람당 20 이 대략 한 바퀴 반 — 1인도 2인도 55~60초가 된다.
        ///
        /// ⚠ 그 55~60초가 <b>길다</b>는 지적을 받아 20 → 16 으로 내렸다. 목표는
        ///   잘하면 30~40초 · 평균 40~50초다. 같은 계산으로 16 이면 약 44~48초다.
        /// </summary>
        [Tooltip("3페이즈: 한 사람 몫의 리듬 성공 횟수. 실제 목표는 이 값 × 인원이다.")]
        [SerializeField, Min(1)] private int phase3TargetRhythmHits = 16;

        [Header("제한 시간 — 판 전체에 하나. 원본 WarriorsBattleScore 의 3분 시계와 같다")]
        [Tooltip("판 전체 제한 시간(초). 1페이즈 시작에 걸고 세 페이즈를 통틀어 센다. 다 쓰면 TIME OVER 로 실패한다.")]
        [SerializeField, Min(30f)] private float matchSeconds = 240f;

        /// <summary>
        /// 라운드 전환 전체 길이(초). 이 동안 몬스터 · 촉수 · 노트가 나오지 않고 시계도 멈춘다.
        ///
        /// <b>안에서 세 토막으로 나뉜다.</b> 예전에는 종료 문구와 다음 라운드 소개가
        /// 각자 타이머로 돌아 <b>같은 순간에 같이 떴다</b> — "해변 방어 성공!" 과
        /// "크라켄이 모습을 드러냈습니다!" 가 겹쳐 보였다.
        /// 이제 서버가 전환 시작 시각 하나를 들고, 화면은 그 경과로 무엇을 띄울지 정한다.
        ///
        /// <code>
        ///   0.0 ~ 1.4초   <b>앞 라운드 무대</b>에서 종료 문구 ("해변 방어 성공!")
        ///   1.4 ~ 1.9초   빈 화면 — 여운
        ///   1.9초         여기서 <b>무대가 바뀐다</b> (Phase 전환)
        ///   1.9 ~ 4.3초   새 무대에서 다음 라운드 소개
        ///   4.3초         게임 시작
        /// </code>
        ///
        /// ⚠ 순서가 핵심이다. 예전에는 목표를 채운 프레임에 곧바로 다음 페이즈로 넘어가
        ///   <b>이미 바뀐 무대 위에서</b> 종료 문구가 떴다. "클리어 → 다음 장소 → 설명" 이라
        ///   원인과 결과가 뒤집혀 보였다. 지금은 "클리어 → 성공 피드백 → 전환 → 새 무대 → 설명" 이다.
        /// </summary>
        [Tooltip("새 무대에서 라운드 소개를 보여 주는 시간(초). 종료 문구와 여백은 포함하지 않는다.")]
        [SerializeField, Min(0f)] private float roundIntroSeconds = 2.4f;

        [Tooltip("앞 라운드 무대에서 종료 문구('해변 방어 성공!')를 보여 주는 시간(초).")]
        [SerializeField, Min(0f)] private float clearNoticeSeconds = 1.4f;

        [Tooltip("종료 문구가 사라진 뒤 무대가 바뀌기까지의 빈 시간(초). 여운을 준다.")]
        [SerializeField, Min(0f)] private float transitionGapSeconds = .5f;

        [Tooltip("크라켄을 쓰러뜨린 뒤 결과 화면까지 천천히 어두워지는 시간(초). 라운드 사이보다 길다.")]
        [SerializeField, Min(0f)] private float finaleFadeSeconds = 1.6f;

        [Header("점수 — 밸런스 미확정 (WARRIORS.md 4장)")]
        [Tooltip("1페이즈 몬스터 한 마리.")]
        [SerializeField, Min(0)] private int killScore = 100;

        [Tooltip("2페이즈 촉수 하나.")]
        [SerializeField, Min(0)] private int tentacleScore = 200;

        [Tooltip("3페이즈 노트 하나. 콤보 피니시 보너스는 WarriorsPhase3Director 가 따로 더한다.")]
        [SerializeField, Min(0)] private int rhythmScore = 150;

        // ------------------------------------------------------------
        // 복제되는 것 — 두 화면이 같은 값을 본다
        // ------------------------------------------------------------

        /// <summary>지금 어느 칸인가.</summary>
        [Networked] public WarriorsMatchPhase Phase { get; private set; }

        /// <summary>시작까지 남은 초. 0 이면 세는 중이 아니다.</summary>
        [Networked] public float Countdown { get; private set; }

        /// <summary>지금 접속해 있는 인원.</summary>
        [Networked] public int Crew { get; private set; }

        /// <summary>1페이즈 팀 합산 처치 수. 3단계에서 서버가 채운다.</summary>
        [Networked] public int Phase1Kills { get; private set; }

        /// <summary>2페이즈 팀 합산 촉수 성공. 4단계에서 서버가 채운다.</summary>
        [Networked] public int Phase2Hits { get; private set; }

        /// <summary>3페이즈 팀 합산 리듬 성공. 5단계에서 서버가 채운다.</summary>
        [Networked] public int Phase3Hits { get; private set; }

        /// <summary>목표값도 복제한다. 클라이언트가 "7 / 15" 를 그리려면 분모가 필요하다.</summary>
        [Networked] public int Phase1Target { get; private set; }

        [Networked] public int Phase2Target { get; private set; }

        [Networked] public int Phase3Target { get; private set; }

        /// <summary>
        /// 일시정지 중인가. **서버가 정하고 모두가 본다.**
        ///
        /// 누구든 <see cref="Rpc_TogglePause"/> 로 요청할 수 있고, 다시 부르면 풀린다.
        /// 시작 전 · 끝난 뒤에는 걸리지 않는다.
        /// </summary>
        [Networked] public NetworkBool IsPaused { get; private set; }

        /// <summary>마지막으로 멈춘 사람의 번호(1P=1, 2P=2). 모르면 0.</summary>
        [Networked] public int PausedBy { get; private set; }

        /// <summary>
        /// 지금 페이즈의 남은 시간(초). **서버가 세고 모두가 본다.** 0 이 되면 TIME OVER 로 실패한다.
        ///
        /// 원본의 3분 시계(<c>WarriorsBattleScore</c>)는 네트워크에서 꺼져 있어 TIME 칸이 비어 있었다.
        /// 페이즈마다 원본 <c>WarriorsGameFlow</c> 의 라운드 시간을 그대로 준다.
        /// </summary>
        [Networked] public float TimeLeft { get; private set; }

        /// <summary>1페이즈 시작부터 흐른 시간(초). 결과 화면의 "플레이 시간".</summary>
        [Networked] public float Elapsed { get; private set; }

        /// <summary>이 판의 난수 시드. 서버가 정하고 로그로 남겨 재현할 수 있게 한다.</summary>
        [Networked] public int RunSeed { get; private set; }

        /// <summary>팀 점수. 처치 · 촉수 · 리듬 · 콤보 피니시로 오른다.</summary>
        [Networked] public int Score { get; private set; }

        /// <summary>실패 사유. 0 없음 · 1 모두 쓰러짐 · 2 시간 초과.</summary>
        [Networked] public int FailReason { get; private set; }

        /// <summary>도달한 가장 높은 라운드(1~3). 결과 화면의 "도달 라운드".</summary>
        [Networked] public int ReachedRound { get; private set; }

        /// <summary>결과 화면에 쓰는 실패 문구. 원본과 같은 두 가지다.</summary>
        public string FailureLabel => FailReason == 2 ? "TIME OVER" : "GAME OVER";

        /// <summary>
        /// 라운드 소개가 끝나는 틱. 페이즈가 바뀔 때마다 새로 건다.
        ///
        /// 이 타이머가 도는 동안은 **소개 화면 시간**이다. 서버의 몬스터 · 촉수 · 노트 담당은
        /// 이 값을 보고 기다리고, 시계도 멈춘다. 그래서 두 화면이 같은 3초를 보고 같은 순간에 시작한다.
        /// 예전에는 페이즈가 바뀌는 즉시 다음 라운드가 쏟아져 전환이 갑작스러웠다.
        /// </summary>
        [Networked] public TickTimer IntroTimer { get; private set; }

        /// <summary>
        /// **앞 라운드를 깬 직후, 무대를 그대로 둔 채 종료 문구를 보여 주는 구간.**
        ///
        /// 이 구간 동안 <see cref="Phase"/> 는 아직 <b>깬 라운드</b>다. 그래야 카메라도 배경도
        /// 그 자리에 남아, 사람이 "내가 이걸 끝냈다" 를 먼저 본다.
        /// </summary>
        [Networked] public TickTimer ClearHoldTimer { get; private set; }

        /// <summary>종료 문구 구간이 끝나면 넘어갈 페이즈. 0 이면 대기 중인 전환이 없다.</summary>
        [Networked] public int PendingPhase { get; private set; }

        /// <summary>방금 깬 라운드 번호. 종료 문구를 고르는 데 쓴다.</summary>
        [Networked] public int ClearedRound { get; private set; }

        /// <summary>
        /// 이번 종료 문구 구간의 전체 길이(초).
        ///
        /// 라운드 사이와 판 마지막이 서로 다르다 — 마지막은 천천히 어두워지므로 더 길다.
        /// 화면은 이 값을 경계로 "문구 구간" 과 "어두워지는 구간" 을 나눈다.
        /// </summary>
        [Networked] public float ClearHoldSeconds { get; private set; }

        /// <summary>지금 종료 문구 구간인가. 이 동안에는 앞 라운드 무대가 그대로 있다.</summary>
        public bool InClearHold => ClearHoldTimer.IsRunning && !ClearHoldTimer.Expired(Runner);

        /// <summary>지금 라운드 소개 화면 시간인가. 모든 PC 에서 같은 답이 나온다.</summary>
        public bool InIntro => IntroTimer.IsRunning && !IntroTimer.Expired(Runner);

        /// <summary>매치가 끝났는가. 대기로 돌아가지 않는다.</summary>
        public bool IsOver => Phase == WarriorsMatchPhase.Cleared || Phase == WarriorsMatchPhase.Failed;

        /// <summary>이미 시작했는가. 늦게 들어온 사람이 다시 시작시키면 안 된다.</summary>
        public bool HasStarted => Phase >= WarriorsMatchPhase.Phase1;

        /// <summary>
        /// 지금 걸어다닐 수 없는가.
        ///
        /// 2 · 3페이즈는 **정해진 자리**에서 한다. 담당이 좌우로 갈려 있어
        /// 걸어 다닐 수 있으면 한 사람이 반대편까지 가서 둘 몫을 다 해 버린다.
        /// 1페이즈는 해변을 뛰어다니는 것이 내용이라 잠그지 않는다.
        /// </summary>
        public bool MovementLocked =>
            Phase == WarriorsMatchPhase.Phase2 || Phase == WarriorsMatchPhase.Phase3;

        /// <summary>
        /// 지금 판의 매치 상태. 몬스터가 처치를 보고할 때 쓴다.
        ///
        /// 씬에 하나뿐이라 정적으로 들고 있는다. 몬스터마다 <c>FindFirstObjectByType</c> 을
        /// 부르면 한 마리 죽을 때마다 씬 전체를 훑게 된다.
        /// </summary>
        public static WarriorsMatchState Current { get; private set; }

        /// <summary>
        /// 지금 판이 일시정지 중인가. 러너 밖(<c>Current</c> 없음)이나 스폰이 풀린 뒤에는 거짓.
        ///
        /// 서버 권위 부품들이 <c>FixedUpdateNetwork</c> 첫 줄에서 이 값으로 빠진다.
        /// </summary>
        public static bool PausedNow
        {
            get
            {
                WarriorsMatchState current = Current;
                return current != null && current.Object != null && current.Object.IsValid && current.IsPaused;
            }
        }

        /// <summary>
        /// 지금 판의 경과 시간. 매치가 없으면 0.
        ///
        /// 구조 시간처럼 <b>매치 시계를 기준으로 재야 하는</b> 값들이 쓴다.
        /// <c>Time.time</c> 을 쓰면 일시정지 중에도 흘러 구조 시간이 그냥 지나간다.
        /// </summary>
        public static float ElapsedNow
        {
            get
            {
                WarriorsMatchState current = Current;
                return current != null && current.Object != null && current.Object.IsValid ? current.Elapsed : 0f;
            }
        }

        /// <summary>일시정지를 걸거나 풀 수 있는 때인가. 시작 전 · 끝난 뒤는 아니다.</summary>
        public bool CanTogglePause => HasStarted && !IsOver;

        private WarriorsHudPresenter hud;

        /// <summary>서버가 <c>Time.timeScale</c> 을 0 으로 잡고 있는가.</summary>
        private bool holdingTime;

        public override void Spawned()
        {
            Current = this;
            hud = FindFirstObjectByType<WarriorsHudPresenter>(FindObjectsInactive.Include);

            // 화면이 있는 쪽(클라이언트)에만 일시정지 버튼을 만든다. 서버에는 화면이 없다.
            if (!Runner.IsServer) WarriorsPauseControl.Ensure(this);

            if (!HasStateAuthority) return;

            Phase = WarriorsMatchPhase.Waiting;
            Countdown = 0f;
            IsPaused = false;
            PausedBy = 0;
            TimeLeft = 0f;
            Elapsed = 0f;
            Score = 0;
            FailReason = 0;
            ReachedRound = 0;
            Phase1Target = phase1TargetKills;
            Phase2Target = phase2TargetTentacleHits;
            Phase3Target = phase3TargetRhythmHits;

            Debug.Log(
                $"[WarriorsMatch] 매치 준비 — {crewToStart}명 대기, 목표 " +
                $"{Phase1Target}/{Phase2Target}/{Phase3Target}");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Current == this) Current = null;

            // 멈춘 채로 세션이 끝나면 시간을 되돌려 놓는다. 남겨 두면 다음 판이 멈춘 채 시작한다.
            if (holdingTime)
            {
                holdingTime = false;
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// **일시정지를 걸거나 푼다.** 누구든 부를 수 있고 서버가 결정한다.
        ///
        /// 한 사람이 멈추면 두 화면이 함께 멈춘다. 다시 부르면(같은 사람이든 상대든) 풀린다.
        ///
        /// ⚠ 대기 · 카운트다운 · 결과 화면에서는 무시한다. 시작 전에 멈추면
        ///    "두 명이 모였는데 시작하지 않는" 상태와 구별이 안 된다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_TogglePause(RpcInfo info = default)
        {
            if (!CanTogglePause)
            {
                Debug.Log($"[WarriorsMatch] {info.Source} 의 일시정지 요청을 무시합니다. (지금 {Phase})");
                return;
            }

            IsPaused = !IsPaused;
            PausedBy = IndexOf(info.Source);

            string who = PausedBy > 0 ? $"{PausedBy}P" : info.Source.ToString();
            Debug.Log($"[WarriorsMatch] {who} 가 게임을 {(IsPaused ? "일시정지했습니다" : "재개했습니다")}.");
        }

        /// <summary>
        /// **끝난 판을 처음부터 다시 시작한다.** 결과 화면의 [다시 하기] 가 부른다.
        ///
        /// 누구든 부를 수 있고 서버가 실행한다. 한 사람이 누르면 **두 사람 모두** 새 판으로 간다 —
        /// 같은 세션에 함께 있으므로 한쪽만 돌아갈 수는 없다.
        ///
        /// ⚠ 씬을 다시 로드하지 않는다. 예전 <c>WarriorsRetryButton</c> 은 <c>SceneFlow.RestartCurrent()</c>
        ///    로 <b>자기 씬만</b> 다시 열었는데, 서버의 판은 그대로 끝난 상태로 남아 화면만 어긋났다.
        ///    판의 상태는 전부 여기 <c>[Networked]</c> 값이므로, 그것만 처음 값으로 돌리면
        ///    페이즈 담당들도 <see cref="Phase"/> 를 보고 알아서 자기 단계를 닫는다.
        ///
        /// 끝나지 않은 판에는 듣지 않는다. 플레이 도중 눌러 판이 날아가면 안 된다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestRestart(RpcInfo info = default)
        {
            if (!IsOver)
            {
                Debug.Log($"[WarriorsMatch] {info.Source} 의 다시 하기 요청을 무시합니다. (아직 {Phase})");
                return;
            }

            Phase = WarriorsMatchPhase.Waiting;
            Countdown = 0f;
            IntroTimer = TickTimer.None;

            // 전환 중에 판이 끝났을 수 있다. 남겨 두면 다시 시작하자마자 종료 문구가 뜬다.
            ClearHoldTimer = TickTimer.None;
            PendingPhase = 0;
            ClearedRound = 0;
            ClearHoldSeconds = 0f;

            TimeLeft = 0f;
            Elapsed = 0f;
            Score = 0;
            FailReason = 0;
            ReachedRound = 0;
            Phase1Kills = 0;
            Phase2Hits = 0;
            Phase3Hits = 0;
            IsPaused = false;
            PausedBy = 0;

            // 쓰러진 사람을 다시 세운다. 이걸 빼면 새 판이 시작하자마자 둘 다 Down 이라 즉시 실패한다.
            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life != null && life.IsLive) life.ResetForNewMatch();
            }

            Debug.Log($"[WarriorsMatch] {info.Source} 가 다시 하기를 눌렀습니다. 새 판을 준비합니다.");
        }

        /// <summary>이 사람의 번호(1부터). 캐릭터가 없으면 0.</summary>
        private static int IndexOf(PlayerRef player)
        {
            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life == null || !life.IsLive) continue;
                if (life.Object.InputAuthority == player) return life.PlayerIndex + 1;
            }

            return 0;
        }

        /// <summary>
        /// **서버의 시간을 멈춘다.**
        ///
        /// 몬스터 이동 · 공격 · 스포너 코루틴 · 촉수 반격 시간은 서연님 원본 그대로
        /// <c>Time.deltaTime</c> 과 <c>Time.time</c> 으로 돈다. 하나씩 고치지 않고
        /// <c>Time.timeScale</c> 을 0 으로 잡아 한 번에 세운다.
        ///
        /// Fusion 은 <c>unscaledDeltaTime</c> 으로 틱을 돌리므로(<c>SimulationTimeMode</c> 기본값)
        /// 이 동안에도 상태 복제와 RPC 는 계속 흐른다 — 그래서 멈춘 상태에서도 "재개" 가 도착한다.
        /// 틱으로 세는 것들(2·3페이즈 타이머)은 각 부품이 따로 멈추고 되돌린다.
        /// </summary>
        private void HoldServerTime()
        {
            if (IsPaused)
            {
                // 매 틱 다시 잡는다. HUD 의 라운드 소개가 timeScale 을 1 로 되돌리는 일이 있다.
                holdingTime = true;
                if (Time.timeScale != 0f) Time.timeScale = 0f;
                return;
            }

            if (!holdingTime) return;

            holdingTime = false;
            Time.timeScale = 1f;
        }

        /// <summary>
        /// 몬스터 한 마리를 잡았다. **서버만 부른다.**
        ///
        /// 누가 벴는지는 세지 않는다. 팀 합산이다. 목표를 채우면 2페이즈로 넘어간다.
        ///
        /// ⚠ 목표 수치는 인원에 따라 늘리지 않는다. 한 명이 쓰러지면 같은 목표를
        ///    혼자 감당해야 하므로 자연히 어려워진다. 거기에 숫자까지 늘리면 두 번 벌하는 셈이다.
        /// </summary>
        public void ReportPhase1Kill()
        {
            if (!HasStateAuthority || Phase != WarriorsMatchPhase.Phase1) return;

            Phase1Kills++;
            Score += killScore;

            // 처치마다 경과 시각을 남긴다. 1페이즈가 실제로 몇 초짜리 라운드인지는
            // 화면을 보고는 알 수 없고, 이 줄들이 있어야 5·10·마지막 처치 시점을 잴 수 있다.
            Debug.Log($"[WarriorsMatch] 처치 {Phase1Kills}/{Phase1Target} — {Elapsed:F1}초");

            if (Phase1Kills < Phase1Target) return;

            WarriorsTelemetry.RoundEnded(1);
            WarriorsTelemetry.RoundStarted(2);
            EnterPhase(WarriorsMatchPhase.Phase2);
            Debug.Log($"[WarriorsMatch] 1페이즈 목표 달성 ({Phase1Kills}/{Phase1Target}). 2페이즈로 넘어갑니다. (경과 {Elapsed:F1}초)");
        }

        /// <summary>점수를 더한다. **서버만 부른다.** 3페이즈 콤보 피니시 보너스가 여기로 온다.</summary>
        public void AddScore(int points)
        {
            if (!HasStateAuthority || points <= 0) return;
            Score += points;
        }

        // ------------------------------------------------------------
        // 협동 게이지 — 두 사람이 **번갈아** 맞혀야 찬다
        // ------------------------------------------------------------

        // ⚠ **협동 게이지와 팀 강화는 삭제했다.** 번갈아 맞히면 차는 게이지였는데,
        //    규칙에서 빠졌다. 되살리지 마라.

        /// <summary>
        /// 촉수 하나를 잘랐다. **서버만 부른다.**
        ///
        /// 1페이즈와 같이 팀 합산이다. 왼쪽을 맡은 사람이 잘랐든 오른쪽이 잘랐든
        /// 같은 하나로 센다. 한 명이 쓰러지면 남은 사람이 같은 목표를 혼자 채워야 한다.
        /// </summary>
        public void ReportPhase2Hit()
        {
            if (!HasStateAuthority || Phase != WarriorsMatchPhase.Phase2) return;

            Phase2Hits++;
            Score += tentacleScore;

            if (Phase2Hits < Phase2Target) return;

            EnterPhase(WarriorsMatchPhase.Phase3);
            Debug.Log($"[WarriorsMatch] 2페이즈 목표 달성 ({Phase2Hits}/{Phase2Target}). 3페이즈로 넘어갑니다. (경과 {Elapsed:F1}초)");
        }

        /// <summary>
        /// 리듬 노트 하나를 제대로 받았다. **서버만 부른다.**
        ///
        /// 이것이 마지막 관문이다. 목표를 채우면 그대로 전체 클리어다.
        ///
        /// ⚠ 한 명만 살아남아도 클리어다. 기획이 그렇게 정해져 있다 —
        ///    남은 사람이 목표를 채우면 <b>둘 다</b> 클리어로 끝난다.
        /// </summary>
        public void ReportPhase3Hit()
        {
            if (!HasStateAuthority || Phase != WarriorsMatchPhase.Phase3) return;

            Phase3Hits++;
            Score += rhythmScore;

            if (Phase3Hits < Phase3Target) return;

            // ⚠ **합동 결정타는 없다.** 한때 목표를 채운 뒤 "둘이 함께 치는 한 방" 을 더 요구했는데,
            //    크라켄 체력이 이미 0 인데도 판이 끝나지 않고 결정타를 기다리며 멈춰 있었다.
            //    목표를 채우면 그대로 클리어다.
            ClearMatch();
        }

        /// <summary>
        /// 크라켄을 쓰러뜨렸다. **곧바로 결과 화면을 띄우지 않는다.**
        ///
        /// ⚠ 예전에는 여기서 바로 <c>Phase = Cleared</c> 였다. 마지막 타격이 들어간 프레임에
        ///   결과 카드가 튀어나와, 이긴 순간을 볼 틈도 없이 화면이 바뀌었다.
        ///   라운드 전환과 같은 방식으로 <b>크라켄을 앞에 둔 채</b> 승리 문구를 먼저 보여 주고,
        ///   천천히 어두워진 뒤에 결과로 넘어간다.
        ///
        /// 어두워지는 시간은 라운드 사이보다 길다(<see cref="finaleFadeSeconds"/>) — 판이 끝나는
        /// 자리라 다음 라운드로 넘어갈 때처럼 서둘 이유가 없다.
        /// </summary>
        private void ClearMatch()
        {
            if (Phase == WarriorsMatchPhase.Cleared || PendingPhase != 0) return;

            TimeLeft = 0f;

            PendingPhase = (int)WarriorsMatchPhase.Cleared;
            ClearedRound = 3;
            ClearHoldSeconds = clearNoticeSeconds + finaleFadeSeconds;
            ClearHoldTimer = TickTimer.CreateFromSeconds(Runner, ClearHoldSeconds);

            Debug.Log(
                $"[WarriorsMatch] 크라켄을 쓰러뜨렸습니다. {ClearHoldSeconds:F1}초 뒤 결과 화면으로 넘어갑니다.");
        }

        /// <summary>승리 연출이 끝났다. 여기서 실제로 판을 닫는다.</summary>
        private void FinishCleared()
        {
            TimeLeft = 0f;

            WarriorsTelemetry.RoundEnded(3);
            WarriorsTelemetry.MatchEnded(true, Score);

            Debug.Log($"[WarriorsMatch] 전체 클리어입니다. 점수 {Score} (경과 {Elapsed:F1}초)");
        }

        /// <summary>
        /// 페이즈를 넘긴다. 도달 라운드를 기록하고 라운드 소개 시간을 건다.
        /// 시계는 건드리지 않는다 — 판 전체에 하나뿐이고 1페이즈 시작에 한 번만 건다.
        ///
        /// ⚠ **바로 넘기지 않는다.** 앞 라운드를 깬 순간에 <c>Phase</c> 를 바꾸면
        ///   <see cref="MovementLocked"/> 가 같은 프레임에 참이 되고, <c>WarriorsLocalView</c> 가
        ///   그 프레임에 카메라를 아레나 고정 구도로 옮긴다. 그래서 <b>"해변 방어 성공!" 이
        ///   이미 바뀐 크라켄 무대 위에서</b> 떴다 — 사람은 "내가 뭘 끝냈지" 보다
        ///   "왜 갑자기 화면이 바뀌었지" 를 먼저 느낀다.
        ///
        ///   지금은 앞 라운드를 붙잡아 둔 채 종료 문구를 먼저 보여 주고
        ///   (<see cref="ClearHoldTimer"/>), 그 다음에 무대를 옮기며 소개를 띄운다.
        /// </summary>
        private void EnterPhase(WarriorsMatchPhase next)
        {
            // 판이 막 열리는 첫 페이즈는 깬 라운드가 없으므로 붙잡을 것도 없다.
            if (Phase == WarriorsMatchPhase.Waiting || Phase == WarriorsMatchPhase.Countdown)
            {
                OpenPhase(next);
                return;
            }

            PendingPhase = (int)next;
            ClearedRound = RoundOf(Phase);
            ClearHoldSeconds = clearNoticeSeconds + transitionGapSeconds;

            ClearHoldTimer = ClearHoldSeconds > 0f
                ? TickTimer.CreateFromSeconds(Runner, ClearHoldSeconds)
                : TickTimer.None;

            if (ClearHoldTimer.Equals(TickTimer.None)) OpenPhase(next);
        }

        /// <summary>실제로 무대를 바꾼다. 종료 문구가 끝난 뒤에만 불린다.</summary>
        private void OpenPhase(WarriorsMatchPhase next)
        {
            Phase = next;
            PendingPhase = 0;
            ClearHoldTimer = TickTimer.None;

            // 판이 끝나는 자리는 다음 라운드 소개가 없다. 결과 화면이 바로 이어진다.
            if (next == WarriorsMatchPhase.Cleared)
            {
                IntroTimer = TickTimer.None;
                FinishCleared();
                return;
            }

            ReachedRound = Mathf.Max(ReachedRound, RoundOf(next));
            IntroTimer = roundIntroSeconds > 0f
                ? TickTimer.CreateFromSeconds(Runner, roundIntroSeconds)
                : TickTimer.None;
        }

        /// <summary>
        /// 종료 문구 구간이 끝났으면 그때 무대를 옮긴다. 서버 틱에서 부른다.
        /// </summary>
        private void AdvancePendingPhase()
        {
            if (PendingPhase == 0) return;
            if (InClearHold) return;

            OpenPhase((WarriorsMatchPhase)PendingPhase);
        }

        /// <summary>매치 실패. <paramref name="reason"/> 1 모두 쓰러짐 · 2 시간 초과.</summary>
        private void Fail(int reason)
        {
            Phase = WarriorsMatchPhase.Failed;
            FailReason = reason;
            Countdown = 0f;
            TimeLeft = 0f;

            // 진 판에서도 성적은 남긴다. 어디서 무너졌는지는 이긴 판보다 진 판이 더 잘 보여 준다.
            WarriorsTelemetry.RoundEnded(Mathf.Max(1, ReachedRound));
            WarriorsTelemetry.MatchEnded(false, Score);
        }

        /// <summary>페이즈를 사람이 보는 라운드 번호로. 대기 · 결과는 0.</summary>
        public static int RoundOf(WarriorsMatchPhase phase) => phase switch
        {
            WarriorsMatchPhase.Phase1 => 1,
            WarriorsMatchPhase.Phase2 => 2,
            WarriorsMatchPhase.Phase3 => 3,
            _ => 0,
        };

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            Crew = Runner.ActivePlayers.Count();

            if (IsOver)
            {
                // 끝난 판이 멈춘 채로 남지 않게 한다.
                if (IsPaused) IsPaused = false;
                HoldServerTime();
                return;
            }

            HoldServerTime();

            // 멈춘 동안에는 판이 흐르지 않는다. 쓰러짐 판정도 시작 판정도 쉰다.
            if (IsPaused) return;

            // 두 명 모두 쓰러지면 거기서 끝이다. 시작 전이면 아직 아무도 없으므로 지나간다.
            // ⚠ **이미 이긴 판은 뒤집히지 않는다.** 크라켄을 쓰러뜨린 뒤 승리 연출이 도는 동안
            //    남은 촉수 공격에 둘 다 쓰러지면 이겼다가 지는 일이 생긴다.
            if (HasStarted && PendingPhase != (int)WarriorsMatchPhase.Cleared && EveryoneDown())
            {
                Fail(1);
                Debug.Log($"[WarriorsMatch] 두 명 모두 쓰러졌습니다. 매치 실패. (경과 {Elapsed:F1}초)");
                return;
            }

            if (!HasStarted)
            {
                UpdateStartGate();
                return;
            }

            // 종료 문구 구간이 끝났으면 여기서 무대를 옮긴다.
            AdvancePendingPhase();

            RunClock();
        }

        /// <summary>
        /// **페이즈 시계.** 멈춘 동안에는 여기까지 오지 않으므로 그대로 선다.
        ///
        /// 원본은 라운드 시간이 다 되면 TIME OVER 로 실패했다. 네트워크에서 그 시계가 꺼진 채
        /// "시간이 흘러도 아무 일도 없는" 상태였다. 같은 규칙을 서버에서 센다.
        /// </summary>
        private void RunClock()
        {
            // 라운드 소개 화면이 떠 있는 동안은 시계도 쉰다. 3초를 읽는 동안 시간이 새면 억울하다.
            // 종료 문구 구간도 마찬가지다 — 이미 깬 라운드를 보고 있는데 시간이 흐르면 안 된다.
            if (InIntro || InClearHold) return;

            Elapsed += Runner.DeltaTime;
            TimeLeft -= Runner.DeltaTime;

            if (TimeLeft > 0f) return;

            Fail(2);
            Debug.Log($"[WarriorsMatch] 시간이 다 됐습니다. TIME OVER (라운드 {ReachedRound}, 점수 {Score})");
        }

        /// <summary>
        /// **언제 시작할지 정한다.**
        ///
        /// <code>
        ///   인원 부족       기다린다
        ///   인원이 모임     10초를 센다
        ///   세는 중에 이탈  센 것을 버리고 다시 기다린다
        ///   다 셈           딱 한 번 시작한다
        /// </code>
        /// </summary>
        private void UpdateStartGate()
        {
            if (Crew < crewToStart)
            {
                if (Phase == WarriorsMatchPhase.Countdown)
                {
                    Debug.Log($"[WarriorsMatch] 인원이 {Crew}명으로 줄어 카운트다운을 취소합니다.");
                    Phase = WarriorsMatchPhase.Waiting;
                    Countdown = 0f;
                }

                return;
            }

            if (Phase == WarriorsMatchPhase.Waiting)
            {
                Phase = WarriorsMatchPhase.Countdown;
                Countdown = countdownSeconds;
                Debug.Log($"[WarriorsMatch] {Crew}명이 모였습니다. {countdownSeconds:F0}초 뒤 시작합니다.");
                return;
            }

            Countdown -= Runner.DeltaTime;

            if (Countdown > 0f) return;

            Countdown = 0f;
            Elapsed = 0f;
            TimeLeft = matchSeconds;

            // **이 판의 난수 시드를 새로 정한다. 서버가 정한다.**
            //
            // ⚠ 네트워크 판에서는 <c>WarriorsRun.BeginRun</c> 을 아무도 부르지 않았다.
            //    그것을 부르는 <c>WarriorsSceneBootstrap</c> 은 혼자 하는 씬 전용이라
            //    <c>WarriorsNet.unity</c> 에도 아레나 프리팹에도 들어 있지 않다(참조 0).
            //    그래서 난수원이 <c>new System.Random(0)</c> 인 채로 남아,
            //    <b>서버를 새로 켤 때마다 몬스터 순서·촉수 약점·노트 악보가 똑같았다.</b>
            //
            // 판이 시작될 때마다 새 시드를 넣으면 매번 다른 판이 되고,
            // [다시 하기] 도 지난 판의 수열을 이어받지 않고 깨끗한 새 판에서 출발한다.
            // 시드는 복제해 두어 로그로 재현할 수 있게 한다.
            RunSeed = System.Environment.TickCount ^ (Runner.Tick * 397);
            WarriorsRun.BeginRun(RunSeed);

            // 목표는 **시작하는 순간의 인원**으로 정한다. 여기서 한 번만 정하고 판 중간에는 바꾸지 않는다 —
            // 한 명이 빠졌다고 목표가 줄면 남은 사람이 이미 채운 진행도가 뒤로 밀린다.
            Phase1Target = Phase1GoalFor(Crew);
            Phase3Target = Phase3GoalFor(Crew);

            WarriorsMatchPhase opening = RequestedStartPhase();
            EnterPhase(opening);

            // 건너뛴 라운드는 **이미 끝난 것으로 채운다.** 진행도를 0 으로 두면 HUD 가
            // "촉수 0 / 22" 를 보여 주면서 3라운드를 돌리게 되고, 결과 화면의 기록도 어긋난다.
            if (opening >= WarriorsMatchPhase.Phase2) Phase1Kills = Phase1Target;
            if (opening >= WarriorsMatchPhase.Phase3) Phase2Hits = Phase2Target;

            Debug.Log(
                $"[WarriorsMatch] 카운트다운이 끝났습니다. {RoundOf(opening)}페이즈 시작. " +
                $"(인원 {Crew}명, 처치 목표 {Phase1Target}, 전체 제한 {matchSeconds:F0}초, 시드 {RunSeed})");

            WarriorsTelemetry.MatchStarted(Crew, Phase1Target, Phase2Target, Phase3Target, matchSeconds);
            WarriorsTelemetry.RoundStarted(1);
        }

        /// <summary>
        /// **개발용 시작 라운드.** <c>-startphase 2</c> · <c>-startphase 3</c> 로 건너뛴다.
        ///
        /// 3라운드 화면 하나를 확인하려고 매번 1·2라운드를 다 싸우는 비용이 너무 커서 붙였다.
        ///
        /// ⚠ <b>인자가 없으면 1페이즈다.</b> 제품 실행 경로는 이 인자를 넘기지 않으므로
        ///    정상 흐름이 그대로다. 값을 준 경우에만, 그리고 서버에서만 뜻이 있다.
        ///
        /// 건너뛴 라운드의 상태는 부르는 쪽에서 채운다 — 페이즈 담당(<c>WarriorsPhase2Director</c> ·
        /// <c>WarriorsPhase3Director</c>)은 <see cref="Phase"/> 만 보고 자기 단계를 열고 닫으므로,
        /// 여기서 바로 3페이즈로 들어가도 크라켄과 노트가 정상적으로 시작한다.
        /// </summary>
        private WarriorsMatchPhase RequestedStartPhase()
        {
            int round = FusionLaunchArguments.GetInt(FusionLaunchArguments.StartPhaseKey, 1, 1, 3);

            if (round <= 1) return WarriorsMatchPhase.Phase1;

            WarriorsMatchPhase phase = round == 2 ? WarriorsMatchPhase.Phase2 : WarriorsMatchPhase.Phase3;

            Debug.LogWarning(
                $"[WarriorsMatch] 개발용 -startphase {round} 로 {round}라운드부터 시작합니다. " +
                "앞 라운드는 달성한 것으로 채웁니다. 제품 실행에서는 이 인자를 주지 마세요.");

            return phase;
        }

        /// <summary>
        /// 이 인원에서 1페이즈 처치 목표는 몇인가.
        ///
        /// 혼자면 <see cref="phase1TargetKills"/>, 한 명 늘 때마다
        /// <see cref="phase1KillsPerExtraPlayer"/> 만큼 더한다. (기본값이면 1인 15 · 2인 23)
        /// </summary>
        private int Phase1GoalFor(int crew)
        {
            int extra = Mathf.Max(0, crew - 1);
            return phase1TargetKills + extra * phase1KillsPerExtraPlayer;
        }

        /// <summary>
        /// 이 인원에서 3페이즈 목표는 몇인가. **한 사람 몫 × 인원.**
        ///
        /// 1페이즈와 셈법이 다르다. 1페이즈는 몬스터를 <b>같이</b> 잡으므로 사람이 늘 때
        /// 목표를 조금만 더한다. 3페이즈는 <b>사람마다 자기 레인에 자기 묶음</b>이 따로 떨어지므로,
        /// 인원에 비례해 늘려야 1인과 2인의 라운드 길이가 같아진다.
        /// </summary>
        private int Phase3GoalFor(int crew)
        {
            return phase3TargetRhythmHits * Mathf.Max(1, crew);
        }

        /// <summary>
        /// 쓰러진 사람을 알리는 한 문장. 쓰러진 사람이 없으면 빈 문자열이다.
        ///
        /// ⚠ **"1P 를 구하세요" 같은 안내는 넣지 않는다.** 구조·부활이 규칙에서 빠졌으므로
        ///   보는 사람이 할 수 있는 일이 없다. 사실만 짧게 알린다.
        /// </summary>
        private string DownNotice()
        {
            foreach (WarriorsPlayerLife one in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (one == null || !one.IsLive || !one.IsDown) continue;

                return "플레이어가 쓰러졌습니다.";
            }

            return string.Empty;
        }

        /// <summary>
        /// 2라운드 **마무리 창**을 알리는 한 문장. 열려 있지 않으면 빈 문자열이다.
        ///
        /// 창은 서버에만 있고 화면에는 아무 표시도 없었다. 열린 줄 모르면 규칙이 있어도
        /// 두 사람이 서로 신호를 줄 수 없다.
        /// </summary>
        private string FinishWindowNotice()
        {
            int lane = LocalLane();
            if (lane < 0) return string.Empty;

            WarriorsPhase2Director tentacle = WarriorsPhase2Director.Current;
            if (tentacle != null && tentacle.FinishWindowOpenFor(lane))
                return "지금! 이어서 베세요";

            return string.Empty;
        }

        /// <summary>
        /// 라운드 전환이 시작된 뒤 흐른 시간(초). 전환 중이 아니면 -1.
        ///
        /// <c>IntroTimer</c> 하나로 재므로 두 화면이 같은 순간에 같은 구간을 본다.
        /// </summary>
        private float TransitionElapsed()
        {
            // 구간 길이는 라운드 사이와 판 마지막이 다르다. 걸 때 정한 값을 그대로 쓴다.
            float gate = ClearHoldSeconds > 0f
                ? ClearHoldSeconds
                : clearNoticeSeconds + transitionGapSeconds;

            // 1단계 — 앞 라운드 무대에서 종료 문구를 보여 주는 구간.
            if (InClearHold)
            {
                float? left = ClearHoldTimer.RemainingTime(Runner);
                return left.HasValue ? Mathf.Max(0f, gate - left.Value) : 0f;
            }

            // 2단계 — 새 무대에서 소개를 보여 주는 구간. 앞 구간 길이를 더해 이어 붙인다.
            if (InIntro)
            {
                float? left = IntroTimer.RemainingTime(Runner);
                return left.HasValue ? gate + Mathf.Max(0f, roundIntroSeconds - left.Value) : gate;
            }

            return -1f;
        }

        /// <summary>쓰러진 레인을 비트로 모은다. 0번 자리가 1P.</summary>
        private int DownLaneMask()
        {
            int mask = 0;

            foreach (WarriorsPlayerLife one in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (one == null || !one.IsLive || !one.IsDown) continue;
                mask |= 1 << one.PlayerIndex;
            }

            return mask;
        }

        /// <summary>이 화면 주인의 레인. 서버에는 없다(-1).</summary>
        private int LocalLane()
        {
            foreach (WarriorsPlayerLife one in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (one == null || !one.IsLive || !one.HasInputAuthority) continue;
                return one.PlayerIndex;
            }

            return -1;
        }

        private bool EveryoneDown()
        {
            WarriorsPlayerLife[] crew = FindObjectsByType<WarriorsPlayerLife>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (crew.Length == 0) return false;

            foreach (WarriorsPlayerLife one in crew)
                if (one != null && one.IsLive && !one.IsDown) return false;

            // 둘 다 쓰러졌으면 그대로 실패다. 구조를 기다리는 시간 같은 것은 없다.
            return true;
        }

        // ------------------------------------------------------------
        // 표시 — 모든 화면이 같은 복제 값을 본다
        // ------------------------------------------------------------

        public override void Render()
        {
            if (hud == null) return;

            // HUD 는 원래 싱글용 부품(WarriorsBattleScore · WarriorsGameFlow)을 읽는다.
            // 네트워크에서는 그것들이 꺼져 있으므로 서버 복제 값을 대신 넣어 준다.
            hud.NetworkMatchActive = true;
            hud.MatchNotice = NoticeFor(Phase);
            hud.MatchDetail = DetailFor(Phase);
            hud.NetworkRound = RoundOf(Phase);
            hud.NetworkReachedRound = Mathf.Max(1, ReachedRound);
            hud.NetworkObjective = ObjectiveFor(Phase);
            hud.NetworkObjectiveProgress = ProgressFor(Phase);
            hud.NetworkScore = Score;
            hud.NetworkKills = Phase1Kills;
            hud.NetworkElapsedSeconds = Elapsed;
            hud.NetworkFinal = Phase == WarriorsMatchPhase.Cleared ? 1 : Phase == WarriorsMatchPhase.Failed ? 2 : 0;
            hud.NetworkFailureLabel = FailureLabel;

            // 쓰러진 사람이 있으면 알린다. 되살릴 방법은 없고 사실만 전한다.
            hud.NetworkDownNotice = DownNotice();
            hud.NetworkFinishWindowNotice = FinishWindowNotice();

            // **라운드 전환은 서버 시계 하나로 순서를 정한다.**
            // 화면이 각자 타이머를 돌리면 종료 문구와 소개가 겹친다. 경과 초만 넘겨 주고
            // 무엇을 띄울지는 아래 세 구간으로 갈린다. -1 은 전환 중이 아니라는 뜻이다.
            // 쓰러진 사람의 레인은 트랙도 지운다. 노트가 안 나오는 줄이 그대로 그려져 있으면
            // "내 줄이 멈췄다" 가 아니라 "노트가 안 오는 버그" 처럼 보인다.
            hud.NetworkDownLanes = DownLaneMask();

            hud.NetworkTransitionElapsed = TransitionElapsed();
            hud.NetworkClearSeconds = clearNoticeSeconds;
            hud.NetworkIntroStartSeconds = ClearHoldSeconds > 0f
                ? ClearHoldSeconds
                : clearNoticeSeconds + transitionGapSeconds;
            hud.NetworkClearedRound = InClearHold ? ClearedRound : 0;

        }

        /// <summary>상단 가운데 막대의 글. 1페이즈 처치 수 · 2페이즈 촉수 수. 3페이즈는 리듬 화면이 따로 그린다.</summary>
        private string ObjectiveFor(WarriorsMatchPhase phase)
        {
            switch (phase)
            {
                case WarriorsMatchPhase.Waiting:
                case WarriorsMatchPhase.Countdown:
                case WarriorsMatchPhase.Phase1:
                    return $"처치 수   {Phase1Kills} / {Phase1Target}";

                case WarriorsMatchPhase.Phase2:
                    return "크라켄 방어";

                default:
                    return string.Empty;
            }
        }

        private float ProgressFor(WarriorsMatchPhase phase)
        {
            switch (phase)
            {
                case WarriorsMatchPhase.Phase1:
                    return Phase1Target > 0 ? Phase1Kills / (float)Phase1Target : 0f;

                case WarriorsMatchPhase.Phase2:
                    return Phase2Target > 0 ? Phase2Hits / (float)Phase2Target : 0f;

                default:
                    return 0f;
            }
        }

        /// <summary>"01:14" 꼴. 올림해서 0 이 되기 전까지는 1 초로 보인다.</summary>
        private static string Clock(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{whole / 60:00}:{whole % 60:00}";
        }

        private string NoticeFor(WarriorsMatchPhase phase)
        {
            // ⚠ **일시정지로 라운드 제목을 덮지 않는다.**
            //    좌상단 카드는 언제나 "ROUND 3 · 크라켄의 공격 / TIME 01:52" 여야 한다.
            //    멈췄다는 것은 화면을 덮는 어두운 메뉴가 이미 말하고 있다(WarriorsPauseControl).
            //    한 텍스트 칸을 라운드·일시정지·쓰러짐이 나눠 쓰면 의미가 섞인다.
            switch (phase)
            {
                case WarriorsMatchPhase.Waiting:
                    return $"동료를 기다리는 중  ({Crew} / {crewToStart})";

                case WarriorsMatchPhase.Countdown:
                    return $"{Mathf.CeilToInt(Countdown)}초 뒤 시작";

                case WarriorsMatchPhase.Cleared:
                    return "CLEAR";

                case WarriorsMatchPhase.Failed:
                    return "GAME OVER";

                case WarriorsMatchPhase.Phase1:
                    return "ROUND 1  ·  몬스터 습격";

                case WarriorsMatchPhase.Phase2:
                    return "ROUND 2  ·  크라켄의 등장";

                case WarriorsMatchPhase.Phase3:
                    return "ROUND 3  ·  크라켄의 공격";

                default:
                    return null;
            }
        }

        /// <summary>
        /// TIME 칸. **진행 중에는 남은 시간만** 보여 준다 — 처치 수는 상단 가운데 막대의 몫이다.
        /// 대기 중에는 1페이즈 제한 시간을 미리 보여 주고, 끝나면 남은 목숨을 보여 준다.
        /// </summary>
        private string DetailFor(WarriorsMatchPhase phase)
        {
            // ⚠ **TIME 칸에 사람 상태를 넣지 않는다.** 여기는 시계 자리다.
            //    예전에는 멈추면 "1P 가 멈췄습니다" 가 시계를 덮어써서, 정작 남은 시간을
            //    볼 수 없는 데다 ROUND/TIME 카드가 상태 알림판처럼 보였다.
            //    멈춤은 덮개가, 쓰러짐은 캐릭터 자세가 말한다.
            if (phase == WarriorsMatchPhase.Waiting || phase == WarriorsMatchPhase.Countdown)
            {
                return Clock(matchSeconds);
            }

            if (!IsOver) return Clock(TimeLeft);

            return LivesLine();
        }

        /// <summary>
        /// "1P HP 80   2P DOWN" 한 줄. 리듬 화면도 이 줄을 쓴다.
        ///
        /// 예전에는 목숨 개수를 적었다. 목숨이 1개가 되면서(HP 0 이면 곧바로 전투 불능)
        /// "목숨 1" 은 아무것도 알려 주지 않는 줄이 됐다. 대신 두 사람의 HP 를 보여 준다 —
        /// 3페이즈에서 상대가 얼마나 버티고 있는지가 실제로 궁금한 정보다.
        /// </summary>
        public string LivesLine()
        {
            WarriorsPlayerLife[] crew = FindObjectsByType<WarriorsPlayerLife>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(one => one != null && one.IsLive)
                .OrderBy(one => one.PlayerIndex)
                .ToArray();

            if (crew.Length == 0) return string.Empty;

            StringBuilder text = new StringBuilder();

            foreach (WarriorsPlayerLife one in crew)
            {
                if (text.Length > 0) text.Append("   ");
                text.Append($"{one.PlayerIndex + 1}P ").Append(one.IsDown ? "DOWN" : $"HP {one.Hp}");
            }

            return text.ToString();
        }
    }
}
