using System.Linq;
using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>판이 지금 어느 칸에 있는가. <c>MineState</c> 와 달리 **판 전체**의 상태다.</summary>
    public enum MineMatchPhase
    {
        /// <summary>사람이 모이기를 기다린다.</summary>
        Waiting = 0,

        /// <summary>다 모였다. 시작까지 센다. 중간에 빠지면 취소하고 다시 대기.</summary>
        Countdown = 1,

        /// <summary>목표 그림을 모두에게 보여 준다. 아직 아무도 파지 않는다.</summary>
        Reveal = 2,

        /// <summary>누군가의 턴. 그 사람만 움직이고 나머지는 관전한다.</summary>
        Turn = 3,

        /// <summary>모든 턴이 끝났다.</summary>
        Finished = 4,
    }

    /// <summary>
    /// 판 한 번의 **공통 상태**를 서버가 정하고 모두에게 보낸다.
    ///
    /// <code>
    ///   대기        정해진 인원이 모일 때까지
    ///   카운트다운  다 모이면 3초. 중간에 빠지면 취소하고 다시 대기
    ///   턴          P1 → P2 → P3 → P4 순서로 한 번씩. 30초씩
    ///   끝          모든 턴 소진
    /// </code>
    ///
    /// <b>참가자 목록은 시작하는 순간 굳는다.</b> 그래야 "P3 이 아직 안 왔는데
    /// 3번 턴을 기다리는" 일이 없다. 굳은 뒤에 들어온 사람은 <c>Slot = -1</c> 인
    /// <b>관전 전용</b>이고, 턴도 힌트도 받지 않는다.
    ///
    /// <b>왜 <c>MineGame</c> 을 쓰지 않는가.</b> 그쪽은 격자 채점 · 복구 · 힌트 · 조명이
    /// 한 덩어리로 얽혀 있고 <c>diggers</c> 를 인스펙터 배열로 들고 있다. 접속자가
    /// 그때그때 생기는 네트워크와는 모양이 맞지 않는다. 격자와 채점을 옮기는 것은
    /// 2단계이고, 지금은 그 위에 얹을 <b>판의 뼈대</b>만 서버 권위로 세운다.
    ///
    /// ⚠ 1단계에서는 <b>파지 않는다.</b> 격자 복제가 없는 채로 파게 두면 각자 화면에서만
    ///    파여 네 명이 다른 그림을 보게 된다. 씬 생성 도구가 <c>MineGame</c> 과
    ///    <c>MineDigger</c> 를 꺼 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineMatchState : NetworkBehaviour
    {
        [Header("시작 대기")]
        [Tooltip("이 인원이 모여야 카운트다운을 시작한다. 정식 기본값은 4인 릴레이다.\n" +
                 "⚠ 실행 인자 -crew 2 가 있으면 그쪽이 이긴다. QA 용이다.")]
        [SerializeField, Range(1, MineNet.MaxCrew)] private int crewToStart = MineNet.DefaultCrewToStart;

        [Tooltip("인원이 모인 뒤 시작까지 세는 시간(초).")]
        [SerializeField, Min(1f)] private float countdownSeconds = 3f;

        [Header("턴")]
        [Tooltip("한 턴의 시간(초). MINE.md 2장 기준값은 30초다.")]
        [SerializeField, Min(1f)] private float turnSeconds = 30f;

        [Tooltip("사람 한 명당 복구 블록 몇 개. 시작 인원 × 이 값이 팀 공용 총량이 된다.")]
        [SerializeField, Min(0)] private int restoresPerPlayer = 1;

        [Header("공개와 힌트 (MINE.md 2·3장)")]
        [Tooltip("목표 그림을 보여 주는 시간(초). 여기부터 기억으로 그린다.")]
        [SerializeField, Min(1f)] private float revealSeconds = 7f;

        [Tooltip("힌트로 목표를 다시 보여 주는 시간(초). " +
                 "⚠ 보는 동안에도 턴 시간은 계속 흐른다. 그것이 힌트의 대가다.")]
        [SerializeField, Min(0.5f)] private float hintSeconds = 3f;

        [Header("채점 (MINE.md 7장 — MineGame 과 같은 값)")]
        [Tooltip("이 값 이상이면 성공.")]
        [SerializeField, Range(0f, 100f)] private float successThreshold = 60f;

        // ------------------------------------------------------------
        // 복제되는 것
        // ------------------------------------------------------------

        /// <summary>지금 어느 칸인가.</summary>
        [Networked] public MineMatchPhase Phase { get; private set; }

        /// <summary>시작까지 남은 초. 0 이면 세는 중이 아니다.</summary>
        [Networked] public float Countdown { get; private set; }

        /// <summary>지금 턴이 끝나기까지 남은 초.</summary>
        [Networked] public float TurnTimeLeft { get; private set; }

        /// <summary>지금 접속해 있는 인원. 관전자를 포함한다.</summary>
        [Networked] public int Crew { get; private set; }

        /// <summary>
        /// **시작하는 순간 굳은 참가자 수.** 이 값이 곧 턴 수다.
        ///
        /// 2명으로 시작했으면 2다. 없는 P3 · P4 를 기다리지 않는다.
        /// </summary>
        [Networked] public int RosterSize { get; private set; }

        /// <summary>지금 몇 번 자리의 턴인가. 0부터. 턴이 아니면 -1.</summary>
        [Networked] public int CurrentSlot { get; private set; }

        /// <summary>남은 복구 블록. **팀 공용이고 시작할 때 굳는다.** (2단계에서 쓰인다)</summary>
        [Networked] public int RestoresLeft { get; private set; }

        /// <summary>이번 판에 주어진 복구 블록 전체 수.</summary>
        [Networked] public int TotalRestores { get; private set; }

        /// <summary>돌 배치 시드. 격자가 이 값으로 같은 판을 만든다.</summary>
        [Networked] public int BoardSeed { get; private set; }

        /// <summary>공개가 끝나기까지 남은 초.</summary>
        [Networked] public float RevealLeft { get; private set; }

        /// <summary>힌트가 보이는 동안 남은 초. 0 이면 안 보인다.</summary>
        [Networked] public float HintLeft { get; private set; }

        /// <summary>지금 힌트를 쓴 사람의 자리. 관전자도 같은 화면을 본다.</summary>
        [Networked] public int HintSlot { get; private set; }

        // ------------------------------------------------------------
        // 결과 — 서버가 한 번만 재고 모두가 같은 값을 본다
        // ------------------------------------------------------------

        /// <summary>유사도 0~100. 이 값이 곧 점수다.</summary>
        [Networked] public float ResultPercent { get; private set; }

        /// <summary>성공했는가.</summary>
        [Networked] public NetworkBool ResultSuccess { get; private set; }

        /// <summary>0~100 으로 다듬은 점수.</summary>
        [Networked] public int ResultScore { get; private set; }

        /// <summary>목표가 파라고 한 칸 수.</summary>
        [Networked] public int ResultTargetCount { get; private set; }

        /// <summary>실제로 판 칸 수.</summary>
        [Networked] public int ResultDugCount { get; private set; }

        /// <summary>채점이 민 칸 수. 결과 화면을 같은 기준으로 칠할 때 쓴다.</summary>
        [Networked] public int ResultAlignX { get; private set; }

        [Networked] public int ResultAlignY { get; private set; }

        /// <summary>지금 목표 그림을 보여 줘야 하는가. 공개 시간이거나 힌트 중이다.</summary>
        public bool ShowingTarget => Phase == MineMatchPhase.Reveal || HintLeft > 0f;

        /// <summary>
        /// 목표를 보여 주는 동안 <b>미리 움직여 볼 수 있는 자리.</b> 아니면 -1.
        ///
        /// 공개가 끝나면 <see cref="DriveReveal"/> 가 <c>OpenTurnFrom(0)</c> 로
        /// 첫 턴을 여므로, 미리 움직일 수 있는 사람도 그 0번이다.
        /// 걸어놓은 자리가 그대로 첫 턴의 시작 자리가 된다 — 첫 턴은
        /// 앞사람이 없어 <c>TakeTurnAt</c> 으로 옥기지 않기 때문이다.
        ///
        /// ⚠ <b>힐트는 여기 해당하지 않는다.</b> 힐트 중에도 <c>ShowingTarget</c> 은
        ///   참이지만 그때는 <c>Phase</c> 가 <c>Turn</c> 이라 본인은 이미 움직일 수 있다.
        /// </summary>
        public int WarmupSlot => Phase == MineMatchPhase.Reveal ? 0 : -1;

        /// <summary>이미 시작했는가. 늦게 들어온 사람이 다시 시작시키면 안 된다.</summary>
        public bool HasStarted => Phase >= MineMatchPhase.Reveal;

        /// <summary>판이 끝났는가.</summary>
        public bool IsOver => Phase == MineMatchPhase.Finished;

        /// <summary>
        /// 지금 판의 매치 상태.
        ///
        /// 씬에 하나뿐이라 정적으로 들고 있는다. 플레이어마다
        /// <c>FindFirstObjectByType</c> 을 부르면 매 프레임 씬을 훑게 된다.
        /// </summary>
        public static MineMatchState Current { get; private set; }

        /// <summary>실행 인자까지 반영한 실제 시작 인원.</summary>
        private int RequiredCrew => MineNet.ResolveCrewToStart(crewToStart);

        public override void Spawned()
        {
            Current = this;

            if (!HasStateAuthority) return;

            Phase = MineMatchPhase.Waiting;
            Countdown = 0f;
            CurrentSlot = -1;
            RosterSize = 0;

            Debug.Log($"[MineMatch] 매치 준비 — {RequiredCrew}명 대기, 턴 {turnSeconds:0}초");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Current == this) Current = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            Crew = Runner.ActivePlayers.Count();

            // 판은 기다리는 동안 미리 깔아 둔다. 카운트다운 중에도 모두가 같은 돌 배치를 봐야 한다.
            EnsureBoardOpen();

            if (IsOver) return;

            TickHint();

            if (Phase == MineMatchPhase.Reveal) DriveReveal();
            else if (Phase == MineMatchPhase.Turn) DriveTurns();
            else if (!HasStarted) UpdateStartGate();
        }

        // ------------------------------------------------------------
        // 시작 대기
        // ------------------------------------------------------------

        /// <summary>
        /// **언제 시작할지 정한다.**
        ///
        /// <code>
        ///   인원 부족       기다린다. 들어온 순서대로 자리를 다시 나눈다
        ///   인원이 모임     3초를 센다
        ///   세는 중에 이탈  센 것을 버리고 다시 대기로 돌아간다
        ///   다 셈           참가자 목록을 굳히고 첫 턴을 연다
        /// </code>
        /// </summary>
        private void UpdateStartGate()
        {
            // 시작 전에는 들어온 순서대로 자리를 계속 다시 나눈다.
            // 중간에 누가 빠지면 뒷사람이 앞으로 당겨져 자리에 구멍이 안 생긴다.
            ReseatWaitingCrew();

            int required = RequiredCrew;

            if (Crew < required)
            {
                if (Phase == MineMatchPhase.Countdown)
                {
                    Debug.Log($"[MineMatch] 인원이 {Crew}명으로 줄어 카운트다운을 취소합니다.");
                    Phase = MineMatchPhase.Waiting;
                    Countdown = 0f;
                }

                return;
            }

            if (Phase == MineMatchPhase.Waiting)
            {
                Phase = MineMatchPhase.Countdown;
                Countdown = countdownSeconds;
                Debug.Log($"[MineMatch] {Crew}명이 모였습니다. {countdownSeconds:F0}초 뒤 시작합니다.");
                return;
            }

            Countdown -= Runner.DeltaTime;
            if (Countdown > 0f) return;

            Countdown = 0f;
            BeginMatch();
        }

        /// <summary>
        /// 대기 중인 사람들에게 **들어온 순서대로** 자리를 나눈다.
        ///
        /// <c>JoinTick</c> 이 접속 순서다. 자리를 한 번 주고 마는 대신 매 틱 다시 나누는
        /// 이유는 이탈 때문이다 — P2 가 나가면 P3 이 2번으로 당겨져야 한다.
        /// 굳은 뒤(<c>HasStarted</c>)에는 절대 다시 나누지 않는다.
        /// </summary>
        private void ReseatWaitingCrew()
        {
            MineNetPlayer[] crew = FindObjectsByType<MineNetPlayer>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(one => one != null && one.Object != null && one.Object.IsValid)
                .OrderBy(one => one.JoinTick)
                .ThenBy(one => one.Object.Id.Raw)
                .ToArray();

            for (int i = 0; i < crew.Length; i++)
            {
                crew[i].AssignSlot(i < MineNet.MaxCrew ? i : -1);
            }
        }
        /// <summary>
        /// 판이 아직 안 깔렸으면 깔고 시드를 복제한다. <b>한 번만 한다.</b>
        ///
        /// ⚠ <b>기다리는 동안 미리 깔아야 한다.</b>
        ///
        ///   <see cref="MineGrid"/> 는 Awake 에서 <c>Scatter(Environment.TickCount)</c> 로
        ///   돌을 뿌린다. 그 값은 PC 마다 다르므로 <b>클라이언트마다 배치가 다르다.</b>
        ///   예전에는 시작할 때서야 공통 시드를 보냈기 때문에, 카운트다운 동안
        ///   서로 다른 판을 보다가 시작 순간에 같아졌다. 실측해서 확인한 문제다.
        ///
        /// ⚠ 시드를 한 번 정하면 판이 끝날 때까지 바꾸지 않는다.
        ///   중간에 다시 깔면 플레이어 눈앞에서 판이 통째로 바뀐다.
        ///
        /// 기다리는 동안에는 아무도 파지 못한다 — <c>CurrentSlot</c> 이 -1 이라
        /// <c>MineNetPlayerActions</c> 가 전부 걸러낸다. 그래서 미리 깔아도 안전하다.
        /// </summary>
        private void EnsureBoardOpen()
        {
            if (BoardSeed != 0) return;
            if (MineGridSync.Current == null) return;

            BoardSeed = Runner.Tick == 0 ? 1 : Runner.Tick;
            MineGridSync.Current.ServerOpenBoard(BoardSeed);
        }



        /// <summary>
        /// **참가자를 굳히고 첫 턴을 연다.**
        ///
        /// 여기서 정해진 것은 판이 끝날 때까지 바뀌지 않는다 — 인원, 턴 수, 복구 블록 총량.
        /// 뒤에 들어온 사람은 <c>Slot = -1</c> 인 채로 남아 관전만 한다.
        /// </summary>
        private void BeginMatch()
        {
            MineNetPlayer[] roster = FindObjectsByType<MineNetPlayer>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(one => one != null && one.Object != null && one.Object.IsValid && one.Slot >= 0)
                .OrderBy(one => one.Slot)
                .ToArray();

            RosterSize = Mathf.Min(roster.Length, MineNet.MaxCrew);

            // 복구 블록은 **시작 시점의 참가 인원 × 1회**다. 2명이면 2개다.
            TotalRestores = RosterSize * restoresPerPlayer;
            RestoresLeft = TotalRestores;

            CurrentSlot = -1;
            HintLeft = 0f;
            HintSlot = -1;

            // 판은 기다리는 동안 이미 깔렸다. 여기서는 혹시 못 깔았을 때를 대비한다.
            // 이미 깔렸으면 아무것도 하지 않는다 — 시드가 바뀌면 시작 순간 판이 바뀐다.
            EnsureBoardOpen();

            Phase = MineMatchPhase.Reveal;
            RevealLeft = revealSeconds;

            Debug.Log(
                $"[MineMatch] 참가자를 굳혔습니다 — {RosterSize}명 릴레이 · " +
                $"복구 {TotalRestores}개 · 시드 {BoardSeed}. " +
                $"목표를 {revealSeconds:0}초 동안 공개합니다.");
        }

        // ------------------------------------------------------------
        // 턴 진행
        // ------------------------------------------------------------

        /// <summary>목표 공개. 다 보여 주면 첫 턴을 연다.</summary>
        private void DriveReveal()
        {
            RevealLeft -= Runner.DeltaTime;
            if (RevealLeft > 0f) return;

            RevealLeft = 0f;
            Phase = MineMatchPhase.Turn;

            if (!OpenTurnFrom(0))
            {
                Debug.LogWarning("[MineMatch] 참가자가 하나도 없어 바로 끝냅니다.");
                EnterFinished();
            }
        }

        /// <summary>힌트가 보이는 시간을 센다. 턴 시간은 그동안에도 흐른다.</summary>
        private void TickHint()
        {
            if (HintLeft <= 0f) return;

            HintLeft -= Runner.DeltaTime;

            if (HintLeft > 0f) return;

            HintLeft = 0f;
            HintSlot = -1;
        }

        // ------------------------------------------------------------
        // 턴 주인이 쓰는 것 — 서버만 판정한다
        // ------------------------------------------------------------

        /// <summary>
        /// 복구 블록 하나를 쓴다. 남아 있지 않으면 거짓.
        ///
        /// ⚠ 안 파인 칸에서 눌렀으면 블록을 깎지 않는다. 잘못 누른 것 때문에
        ///    귀한 블록이 날아가면 안 된다. (MINE.md 4장)
        /// </summary>
        public bool ServerUseRestore()
        {
            if (!HasStateAuthority || RestoresLeft <= 0) return false;

            RestoresLeft--;
            Debug.Log($"[MineMatch] 복구 — 남은 블록 {RestoresLeft}/{TotalRestores}개");
            return true;
        }

        /// <summary>힌트를 켠다. 쓸 수 있는지는 부르는 쪽이 이미 확인했다.</summary>
        public void ServerShowHint(int slot)
        {
            if (!HasStateAuthority) return;

            HintSlot = slot;
            HintLeft = hintSeconds;

            Debug.Log($"[MineMatch] P{slot + 1} 힌트 — {hintSeconds:0}초 동안 보여 줍니다.");
        }

        private void DriveTurns()
        {
            // 지금 턴인 사람이 나갔으면 기다리지 않고 바로 넘긴다.
            if (CurrentSlot >= 0 && FindBySlot(CurrentSlot) == null)
            {
                Debug.Log($"[MineMatch] P{CurrentSlot + 1} 이(가) 나가 턴을 건너뜁니다.");
                AdvanceTurn();
                return;
            }

            TurnTimeLeft -= Runner.DeltaTime;
            if (TurnTimeLeft > 0f) return;

            TurnTimeLeft = 0f;
            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            if (!OpenTurnFrom(CurrentSlot + 1)) EnterFinished();
        }

        /// <summary>
        /// <paramref name="from"/> 자리부터 살아 있는 참가자를 찾아 턴을 연다.
        ///
        /// 나간 사람의 자리는 건너뛴다. **격자와 복구 블록은 건드리지 않는다** —
        /// 판은 팀 공용이고 앞사람이 파 둔 것은 그대로 남는다.
        /// </summary>
        private bool OpenTurnFrom(int from)
        {
            for (int slot = Mathf.Max(0, from); slot < RosterSize; slot++)
            {
                MineNetPlayer next = FindBySlot(slot);
                if (next == null) continue;

                // 다음 사람은 **앞사람이 서 있던 자리에서** 이어서 판다.
                // 릴레이라 파던 자리가 곧 이어 그릴 자리다. (MINE.md 3장)
                MineNetPlayer previous = CurrentSlot >= 0 ? FindBySlot(CurrentSlot) : null;

                MineNetPlayerMover mover = next.GetComponent<MineNetPlayerMover>();

                if (mover != null)
                {
                    // ⚠ 컨트롤러를 **먼저 켜고** 옮긴다. 순서를 뒤집으면 켜지는 순간
                    //    CharacterController 가 기억하던 옛 자리로 되돌린다.
                    if (previous != null && previous != next)
                    {
                        mover.TakeTurnAt(previous.transform.position, previous.transform.rotation);
                    }
                    else
                    {
                        mover.SetSimulated(true);
                    }
                }

                CurrentSlot = slot;
                TurnTimeLeft = turnSeconds;

                // 턴이 바뀌면 앞사람이 보던 힌트는 걷는다.
                HintLeft = 0f;
                HintSlot = -1;

                Debug.Log($"[MineMatch] P{slot + 1} 턴 시작 — {turnSeconds:0}초 (참가 {RosterSize}명)");
                return true;
            }

            return false;
        }

        /// <summary>
        /// **판을 닫고 한 번만 채점한다.**
        ///
        /// 규칙은 <c>MineGame.EnterFinished</c> 그대로다 — 완성된 격자와 목표 도안의
        /// 유사도가 곧 점수이고, 기준치를 넘으면 성공이다. 격자는 팀 공용 한 장이라
        /// 인원과 무관하게 같은 계산이 나온다.
        ///
        /// 결과를 <c>[Networked]</c> 로 두는 이유는 두 가지다. 모두가 <b>같은 순간에
        /// 같은 값</b>을 보고, <b>늦게 들어온 사람</b>도 끝난 판이면 결과를 그대로 받는다.
        /// </summary>
        private void EnterFinished()
        {
            Phase = MineMatchPhase.Finished;
            CurrentSlot = -1;
            TurnTimeLeft = 0f;
            HintLeft = 0f;
            HintSlot = -1;

            MineGridSync board = MineGridSync.Current;

            if (board == null)
            {
                Debug.LogWarning("[MineMatch] 격자를 찾지 못해 채점하지 못했습니다.");
                return;
            }

            MineSimilarityResult result = board.ServerScore();

            ResultPercent = result.Percent;
            ResultSuccess = result.Percent >= successThreshold;
            ResultScore = Mathf.Clamp(Mathf.RoundToInt(result.Percent), 0, 100);
            ResultTargetCount = result.TargetCount;
            ResultDugCount = result.DugCount;
            ResultAlignX = result.Alignment.x;
            ResultAlignY = result.Alignment.y;

            Debug.Log(
                $"[MineMatch] 끝 — {(ResultSuccess ? "성공" : "실패")} · {result} " +
                $"(참가 {RosterSize}명 · 복구 {TotalRestores - RestoresLeft}개 씀)");
        }

        // ------------------------------------------------------------
        // 표시 — 모든 화면이 같은 복제 값을 본다
        // ------------------------------------------------------------

        /// <summary>
        /// HUD 를 서버 상태로 몬다.
        ///
        /// 1단계에서는 <c>MineGame</c> 이 꺼져 있어 <c>MineHud</c> 가 시작도 안 한 판의
        /// 기본값을 계속 띄운다. 여기서 실제 상태를 밀어 넣고, 아직 못 잇는 칸은
        /// <c>MineHud</c> 쪽이 감춘다.
        /// </summary>
        public override void Render()
        {
            if (_hud == null)
            {
                _hud = FindFirstObjectByType<MineHud>(FindObjectsInactive.Include);
                if (_hud == null) return;
            }

            _hud.NetworkDriven = true;
            _hud.NetworkResultShow = Phase == MineMatchPhase.Finished;

            if (_hud.NetworkResultShow)
            {
                _hud.NetworkResultText = ResultSuccess ? "성공!" : "실패";
                _hud.NetworkResultDetail =
                    $"{ResultScore}점 · 유사도 {ResultPercent:0.0}%" + System.Environment.NewLine +
                    $"목표 {ResultTargetCount}칸 · 판 것 {ResultDugCount}칸";
            }
            _hud.NetworkPhaseText = PhaseLine();
            _hud.NetworkTurnText = HasStarted && CurrentSlot >= 0 ? $"{CurrentSlot + 1} / {RosterSize}" : string.Empty;

            _hud.NetworkCountdown = Phase == MineMatchPhase.Countdown
                ? Mathf.Max(1, Mathf.CeilToInt(Countdown))
                : 0;

            // 공개 7초도 채굴 30초와 **같은 칸에** 센다. 남은 시간을 읽는 곳이
            // 둘로 나뉘면(위는 --:--, 문구는 "(7초)") 어디를 봐야 하는지 매번 헷갈린다.
            if (Phase == MineMatchPhase.Turn && CurrentSlot >= 0) _hud.NetworkTimeText = Clock(TurnTimeLeft);
            else if (Phase == MineMatchPhase.Reveal) _hud.NetworkTimeText = Clock(RevealLeft);
            else _hud.NetworkTimeText = string.Empty;
        }

        private static string Clock(float secondsLeft)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private string PhaseLine()
        {
            switch (Phase)
            {
                case MineMatchPhase.Waiting:
                    return $"동료를 기다리는 중  ({Crew} / {RequiredCrew})";

                case MineMatchPhase.Countdown:
                    return $"{Mathf.CeilToInt(Countdown)}초 뒤 시작";

                case MineMatchPhase.Reveal:
                    // 초는 위 타이머가 센다. 여기서 또 적으면 두 숫자가 한 프레임씩 어긋난다.
                    return "목표를 외우세요";

                case MineMatchPhase.Turn:
                    if (HintLeft > 0f) return $"P{HintSlot + 1} 힌트 보는 중";
                    return CurrentSlot >= 0 ? $"P{CurrentSlot + 1} 채굴 중" : "턴 준비 중";

                case MineMatchPhase.Finished:
                    return ResultSuccess ? "성공!" : "실패";

                default:
                    return string.Empty;
            }
        }

        private MineHud _hud;

        /// <summary>그 자리의 사람. 나갔으면 null.</summary>
        public MineNetPlayer FindBySlot(int slot)
        {
            if (slot < 0) return null;

            foreach (MineNetPlayer one in FindObjectsByType<MineNetPlayer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (one == null || one.Object == null || !one.Object.IsValid) continue;
                if (one.Slot == slot) return one;
            }

            return null;
        }
    }
}
