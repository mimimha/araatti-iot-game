using System.Collections;
using System.Linq;
using Fusion;
using MiniGames.Common;
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
    public sealed class MineMatchState : NetworkBehaviour, IMiniGameAdmissionSource
    {
        [Header("시작 대기")]
        [Tooltip("이 인원이 모여야 카운트다운을 시작한다. 정식 기본값은 4인 릴레이다.\n" +
                 "⚠ 실행 인자 -crew 2 가 있으면 그쪽이 이긴다. QA 용이다.")]
        [SerializeField, Range(1, MineNet.MaxCrew)] private int crewToStart = MineNet.DefaultCrewToStart;

        [Tooltip("인원이 모인 뒤 시작까지 세는 시간(초).")]
        [SerializeField, Min(1f)] private float countdownSeconds = 3f;

        [Tooltip("카운트다운이 켜질 때 판 위에 흩뿌릴 자리를 고르면서, 가장자리 몇 칸을 비울 것인가.\n" +
                 "0 이면 판 끝 칸까지 나온다. 테두리에 바짝 붙어 떨어지는 것을 막는 값이다.")]
        [SerializeField, Min(0)] private int spawnEdgeMargin = 2;

        [Header("턴")]
        [Tooltip("한 턴의 시간(초). MINE.md 2장 기준값은 30초다.")]
        [SerializeField, Min(1f)] private float turnSeconds = 30f;

        [Tooltip("이번 판에 주어지는 복구 블록 수. 인원과 무관한 고정값이며 팀 공용이다.")]
        [SerializeField, Min(0)] private int restoreBlocks = 5;

        [Header("공개와 힌트 (MINE.md 2·3장)")]
        [Tooltip("목표 그림을 보여 주는 시간(초). 여기부터 기억으로 그린다.")]
        [SerializeField, Min(1f)] private float revealSeconds = 7f;

        [Tooltip("힌트로 목표를 다시 보여 주는 시간(초). " +
                 "⚠ 보는 동안에도 턴 시간은 계속 흐른다. 그것이 힌트의 대가다.")]
        [SerializeField, Min(0.5f)] private float hintSeconds = 3f;

        [Header("결과 화면")]
        [Tooltip("판이 끝나고 \"채굴 종료\" 제목만 보여 주는 시간(초).\n" +
                 "이 동안에는 네 캐릭터가 판 위에 서 있고, 판에는 우리가 판 그림만 보인다. 토글은 아직 안 돈다.")]
        [SerializeField, Min(0f)] private float finishTitleSeconds = 2f;

        [Tooltip("\"채굴 종료\" 가 끝난 뒤 공용 결과 판을 열기까지 기다리는 시간(초).\n" +
                 "이 동안 캐릭터가 사라지고, 정답과 우리가 판 그림을 번갈아 보여 준다.")]
        [SerializeField, Min(0f)] private float resultHoldSeconds = 6f;

        [Header("채점 (MINE.md 7장 — MineGame 과 같은 값)")]
        [Tooltip("이 값 이상이면 성공.")]
        [SerializeField, Range(0f, 100f)] private float successThreshold = 70f;

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

        /// <summary>판이 시작한 뒤 흐른 시간. 결과 화면의 플레이 시간으로 쓴다.</summary>
        [Networked] public float Elapsed { get; private set; }

        /// <summary>
        /// <b>결과가 확정된 틱.</b> 0 이면 아직 안 끝났다.
        ///
        /// 승패·점수·시간은 이미 위에 다 있다. 여기서 새로 정하는 것은 <b>언제 확정됐는가</b>
        /// 하나뿐이다. 그 틱을 찍어 두면 화면이 "이미 보여 준 결과인가" 를 가릴 수 있고,
        /// <b>늦게 들어온 사람</b>도 복제된 이 값을 보고 같은 결과를 받는다.
        /// </summary>
        [Networked] private int ResultTick { get; set; }

        /// <summary>지금 목표 그림을 보여 줘야 하는가. 공개 시간이거나 힌트 중이다.</summary>
        public bool ShowingTarget => Phase == MineMatchPhase.Reveal || HintLeft > 0f;

        /// <summary>
        /// 목표를 보여 주는 동안 <b>미리 움직여 볼 수 있는 자리.</b> 아니면 -1.
        ///
        /// 공개가 끝나면 <see cref="DriveReveal"/> 가 <c>OpenTurnFrom(0)</c> 로
        /// 첫 턴을 여므로, 미리 움직일 수 있는 사람도 그 0번이다.
        /// 걸어놓은 자리가 그대로 첫 턴의 시작 자리가 된다 — 턴이 열릴 때
        /// 아무도 자리를 옮기지 않는다.
        ///
        /// ⚠ <b>힐트는 여기 해당하지 않는다.</b> 힐트 중에도 <c>ShowingTarget</c> 은
        ///   참이지만 그때는 <c>Phase</c> 가 <c>Turn</c> 이라 본인은 이미 움직일 수 있다.
        /// </summary>
        public int WarmupSlot => Phase == MineMatchPhase.Reveal ? 0 : -1;

        /// <summary>
        /// <b>참가자 전원이 제 몸을 쥐는 시간.</b> 카운트다운 3초와 턴 내내다.
        ///
        /// <code>
        ///   카운트다운  넷이 다 움직인다. 아무도 못 판다 (CurrentSlot 이 -1)
        ///   공개 7초    첫 턴 예정자만 움직인다. 나머지 셋은 보이되 그 자리에 굳는다
        ///   턴          넷이 다 움직인다. 파는 것은 턴 주인만
        /// </code>
        ///
        /// <b>넷이 다 걸어다니게 두는 이유.</b> 관전이 "가만히 보고만 있기" 가 되면
        /// 자기 턴 말고는 할 일이 없다. 옆에서 같이 걸어다니며 훈수를 두는 편이
        /// 릴레이와 어울린다. 그래서 <b>이동은 열고 채굴만 잠근다.</b>
        ///
        /// 이 값은 셋을 한꺼번에 정한다 — 누가 보이는가(<c>MineNetPlayer.ApplyPresence</c>),
        /// 누구의 몸을 굴리는가(<c>MineNetPlayerMover.SetSimulated</c>),
        /// 사람끼리 부딪히는가(<c>MineNetPlayerMover.ApplyCrowdCollision</c>).
        /// 셋이 같이 움직여야 한다 — 안 보이는 몸이 길을 막는 것이 제일 나쁘다.
        ///
        /// ⚠ <b>파는 것은 여기서 열리지 않는다.</b> <see cref="MineNetPlayerActions"/> 가
        ///   <c>IsMyTurn</c> 으로 따로 막는다. 카운트다운에는 <see cref="CurrentSlot"/> 이
        ///   -1 이라 아무도 해당되지 않고, 턴에는 그 한 사람만 해당된다.
        ///
        /// ⚠ 공개(<see cref="MineMatchPhase.Reveal"/>)는 <b>일부러 뺐다.</b> 그 7초는
        ///   도안을 외우는 시간이라 <see cref="WarmupSlot"/> 한 명만 미리 자리를 잡는다.
        ///   <b>움직이지 못할 뿐 넷 다 보인다</b> — 보이는 것은 <see cref="CrewOnBoard"/>
        ///   가 따로 정한다.
        ///
        /// ⚠ 대기(<see cref="MineMatchPhase.Waiting"/>)도 뺐다. 사람이 모일 때까지는
        ///   멈춰 있다가 "3" 과 함께 한꺼번에 풀리는 편이 신호로 읽힌다.
        /// </summary>
        public bool FreeRoam => Phase == MineMatchPhase.Countdown || Phase == MineMatchPhase.Turn;

        /// <summary>
        /// <b>참가자의 몸이 격자 위에 보이는 시간.</b> 카운트다운 · 공개 · 턴.
        ///
        /// <b>보이는 것과 움직이는 것은 다른 문이다.</b> 공개 7초에는 넷이 다 서 있되
        /// <see cref="WarmupSlot"/> 한 명만 걷는다 — 나머지 셋은 그 자리에 굳어 있다.
        /// 넷이 같이 도안을 올려다보는 그림이 되고, 누가 첫 턴인지도 그 한 명이
        /// 움직이는 것으로 드러난다.
        ///
        /// <b>부딪히는 것도 이 값을 따른다.</b> (<c>MineNetPlayerMover.ApplyCrowdCollision</c>)
        /// 굳어 있어도 보이면 몸이고, 보이는 몸은 길을 막아도 된다. 막으면 안 되는 것은
        /// <b>보이지 않는 몸</b>이다.
        ///
        /// ⚠ 대기와 결과는 뺐다. 대기 중에는 스폰 높이에 떠 있고(카운트다운에 떨어진다),
        ///   결과 화면은 완성된 그림을 위에서 보여 주는 시간이라 몸이 가리면 안 된다.
        /// </summary>
        public bool CrewOnBoard => Phase == MineMatchPhase.Countdown
                                || Phase == MineMatchPhase.Reveal
                                || Phase == MineMatchPhase.Turn
                                || ShowingFinishTitle;

        /// <summary>
        /// 결과가 확정된 뒤 흐른 시간(초). 아직 안 끝났으면 -1.
        ///
        /// <b>틱으로 재는 이유.</b> <see cref="ResultTick"/> 은 복제되므로 네 화면이
        /// 같은 값을 얻는다. 각자 코루틴이나 <c>Time.time</c> 으로 재면 들어온 시각이
        /// 달라 화면끼리 박자가 어긋난다.
        ///
        /// ⚠ 판이 끝나면 <c>FixedUpdateNetwork</c> 가 <c>IsOver</c> 에서 바로 빠지므로
        ///   여기서는 <b>깎아 내리는 네트워크 값을 쓸 수 없다.</b> (Countdown · RevealLeft
        ///   와 다른 점이다) 그래서 지나간 틱 수로 거꾸로 센다.
        /// </summary>
        public float SinceResult
        {
            get
            {
                if (ResultTick <= 0 || Runner == null) return -1f;
                return Mathf.Max(0f, ((int)Runner.Tick - ResultTick) * Runner.DeltaTime);
            }
        }

        /// <summary>
        /// <b>채굴 종료 — 판이 끝나고 제목만 보여 주는 첫 단계.</b>
        ///
        /// 네 캐릭터가 판 위에 그대로 서 있고(<see cref="CrewOnBoard"/>), 판에는
        /// 우리가 판 그림만 보인다. 정답 토글은 다음 단계부터다.
        /// </summary>
        public bool ShowingFinishTitle =>
            Phase == MineMatchPhase.Finished && SinceResult >= 0f && SinceResult < finishTitleSeconds;

        /// <summary>
        /// <b>채굴 결과 — 성공·실패와 점수를 보여 주는 두 번째 단계.</b>
        ///
        /// 캐릭터가 사라지고 판이 정답과 번갈아 돈다. 이 단계가 끝나면
        /// 공용 결과 화면으로 넘어간다.
        /// </summary>
        public bool ShowingMineResult =>
            Phase == MineMatchPhase.Finished && SinceResult >= finishTitleSeconds;

        /// <summary>
        /// **판이 들려 있는가.** 그동안에는 <b>아무도</b> 몸을 굴리지 않는다.
        ///
        /// 정답 보기는 파인 칸을 <c>digDepth</c>(0.25m)만큼 끌어올린다. 그런데 그 판은
        /// <b>화면마다 따로</b> 그려지고, <b>몸을 굴리는 것은 서버 한 곳</b>이다.
        /// 그래서 서버 화면에 정답이 떠 있는 동안 남들이 그 위를 걸으면
        /// <b>올라온 블록이 캐릭터를 떠민다.</b> 솔로에서 토글마다 점프하던 것과 같다.
        /// (<c>MineGame.SyncFrozen</c>)
        ///
        /// 서버 화면에 정답이 뜨는 경우는 하나다 — <b>호스트를 맡은 사람이 자기 힌트를
        /// 볼 때.</b> <c>MineLocalView</c> 는 입력 권한이 있는 몸 하나에서만 도는데,
        /// 호스트 프로세스에서 그것은 호스트 자신이기 때문이다. 남이 힌트를 보는 것은
        /// 그 사람 화면에서만 일어나므로 서버 물리와 상관이 없다.
        /// (전용 서버로 돌리면 화면 자체가 없어 언제나 거짓이다)
        ///
        /// ⚠ <b><see cref="HintLeft"/> 에서 파생시킨다.</b> 켜는 곳과 끄는 곳을 따로 두면
        ///   턴이 넘어가며 힌트가 걷힐 때 켜진 채로 남아 판이 영영 멈춘다.
        ///   힌트를 0 으로 만드는 곳이 네 군데라 더욱 그렇다.
        /// </summary>
        public bool BoardLifted => HintLeft > 0f && _hintIsOnServerScreen;

        /// <summary>이번 힌트가 서버 화면에 뜨는 것인가. <see cref="ServerShowHint"/> 가 한 번만 정한다.</summary>
        private bool _hintIsOnServerScreen;

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

            // ⚠ **판을 다시 깔기 전에 되돌린다.** 순서가 중요하다. 여기서 BoardSeed 를 0 으로
            //    되돌려야 바로 아래 EnsureBoardOpen 이 다음 팀을 위한 새 판을 깐다.
            ResetWhenEveryoneLeft();

            // 판은 기다리는 동안 미리 깔아 둔다. 카운트다운 중에도 모두가 같은 돌 배치를 봐야 한다.
            EnsureBoardOpen();

            if (IsOver) return;

            // 판이 도는 동안만 시계를 센다. 대기·카운트다운은 플레이 시간이 아니다.
            if (HasStarted) Elapsed += Runner.DeltaTime;

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

                // ⚠ **Phase 를 바꾼 뒤에 흩뿌린다.** 순서를 뒤집으면 아직 Waiting 이라
                //   몸이 숨어 있는 동안 옮기게 되고, 켜지는 순간 이미 흩어져 있는
                //   그림이 된다. 나타나면서 흩어지는 것이 보여야 한다.
                ScatterCrew();

                Debug.Log($"[MineMatch] {Crew}명이 모였습니다. {countdownSeconds:F0}초 뒤 시작합니다.");
                return;
            }

            Countdown -= Runner.DeltaTime;
            if (Countdown > 0f) return;

            Countdown = 0f;
            BeginMatch();
        }

        /// <summary>
        /// 카운트다운이 켜지는 순간 **참가자를 판 위에 흩뿌린다.**
        ///
        /// 자리 표식(<c>MineSpawnPoint</c>) 넷은 판 한가운데 일렬로 서 있어서 매 판 같은
        /// 그림이 나온다. 여기서 <b>칸을 무작위로 골라</b> 흩어 놓으면 판마다 다르게
        /// 시작하고, 넷이 서로를 찾아 움직이는 3초가 된다.
        ///
        /// <b>높이는 건드리지 않는다.</b> 자리 표식이 정한 높이(2m)를 그대로 쓴다 —
        /// x·z 만 바꾸므로 넷이 같은 높이에서 같이 떨어진다.
        ///
        /// <b>칸 단위로 고르는 이유.</b> 칸은 1m 이고 사람의 반지름은 0.35m 라,
        /// <b>서로 다른 칸이면 절대 겹치지 않는다.</b> 거리 계산이 필요 없다.
        /// 이제 사람끼리 부딪히므로 겹쳐 놓으면 그 순간 서로 밀어낸다.
        ///
        /// ⚠ 서버만 뽑는다. 클라이언트는 <c>NetworkTransform</c> 으로 결과만 받으므로
        ///   같은 씨앗을 나눠 가질 필요가 없다. (판의 돌 배치와 다른 점이다)
        /// </summary>
        private void ScatterCrew()
        {
            MineGrid grid = MineGridSync.Current != null ? MineGridSync.Current.Grid : null;

            // 판이 아직 안 깔렸으면 자리 표식 그대로 둔다. 일렬로 서서 시작할 뿐이다.
            if (grid == null)
            {
                Debug.LogWarning("[MineMatch] 판이 없어 흩뿌리지 못했습니다. 자리 표식 그대로 시작합니다.");
                return;
            }

            int low = Mathf.Clamp(spawnEdgeMargin, 0, (grid.Size - 1) / 2);
            int high = grid.Size - 1 - low;

            MineNetPlayer[] crew = FindObjectsByType<MineNetPlayer>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(one => one != null && one.Object != null && one.Object.IsValid && one.Slot >= 0)
                .OrderBy(one => one.Slot)
                .ToArray();

            int[] taken = new int[crew.Length];
            int count = 0;

            foreach (MineNetPlayer one in crew)
            {
                MineNetPlayerMover mover = one.GetComponent<MineNetPlayerMover>();
                if (mover == null) continue;

                int cell = PickFreeCell(grid, low, high, taken, count);

                // 스무 번 뽑고도 남의 칸만 나왔으면 그냥 있던 자리에 둔다.
                // 판이 20×20 이고 사람이 넷이라 실제로는 일어나지 않는다.
                if (cell < 0) continue;

                taken[count++] = cell;

                Vector3 top = grid.CellToWorld(cell % grid.Size, cell / grid.Size);
                Vector3 here = one.transform.position;

                mover.PlaceAt(new Vector3(top.x, here.y, top.z), one.transform.rotation);
            }

            Debug.Log($"[MineMatch] 참가자 {count}명을 판 위에 흩뿌렸습니다. " +
                      $"(가장자리 {low}칸은 비운다)");
        }

        /// <summary>아직 아무도 안 쓴 칸을 하나 고른다. 못 고르면 -1.</summary>
        private int PickFreeCell(MineGrid grid, int low, int high, int[] taken, int count)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int x = UnityEngine.Random.Range(low, high + 1);
                int y = UnityEngine.Random.Range(low, high + 1);
                int candidate = y * grid.Size + x;

                bool used = false;
                for (int i = 0; i < count; i++)
                {
                    if (taken[i] != candidate) continue;
                    used = true;
                    break;
                }

                if (!used) return candidate;
            }

            return -1;
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

            // 복구 블록은 **인원과 무관한 고정값**이다. 2명이든 4명이든 같다.
            TotalRestores = restoreBlocks;
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

            // 이 힌트가 **서버 화면**에 뜨는 것인지 여기서 한 번만 가린다.
            // 매 틱 다시 찾으면 3초 동안 씬을 수백 번 훑게 된다.
            MineNetPlayer viewer = FindBySlot(slot);
            _hintIsOnServerScreen = viewer != null && viewer.Object != null && viewer.Object.HasInputAuthority;

            Debug.Log($"[MineMatch] P{slot + 1} 힌트 — {hintSeconds:0}초 동안 보여 줍니다." +
                      (BoardLifted ? " (서버 화면이라 그동안 모두 멈춥니다)" : string.Empty));
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

                // ⚠ **자리를 옮기지 않는다.** 예전에는 앞사람이 서 있던 자리로 순간이동
                //   시켰지만(릴레이라 파던 자리가 곧 이어 그릴 자리였다), 이제는 넷이
                //   턴 내내 같이 걸어다니므로 <b>서 있던 그 자리에서 바로 판다.</b>
                //   걷고 있던 사람을 끌어오면 그 순간 조작이 끊기고, 앞사람 몸과 겹쳐
                //   서로 밀어낸다. 자기 턴이 된 것은 발밑에 뜨는 조준 표시로 안다.
                //
                //   몸은 이미 굴러가고 있다(FreeRoam 이 턴을 포함한다). 여기서 한 번 더
                //   켜 두는 것은 Phase 가 막 Turn 으로 바뀐 첫 틱의 빈틈을 없애기 위해서다.
                //
                // ⚠ **앞사람의 시야 각도도 물려주지 않는다.** 자리를 물려주던 때에는
                //   "같은 자리에서 시점만 홱 도는" 것을 막으려고 넘겼지만(c1b7d037),
                //   자리 인계가 없어져 카메라가 어차피 판 저쪽으로 크게 움직인다.
                //   게다가 복제된 각도는 입력이 빠진 틱의 대비책으로도 쓰여서
                //   (MineNetPlayerMover), 남겨 두면 다음 턴 주인의 몸이 그 한 틱 동안
                //   앞사람이 보던 쪽을 향한다. 건희님과 빼기로 정했다.
                MineNetPlayerMover mover = next.GetComponent<MineNetPlayerMover>();
                if (mover != null) mover.SetSimulated(true);

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
        // ------------------------------------------------------------
        // 입장 판정 — 받아도 되는 사람만 받는다
        // ------------------------------------------------------------

        /// <summary>
        /// <b>지금 이 판에 사람을 받아도 되는가.</b> <c>MiniGameAdmission</c> 이 접속 요청마다 묻는다.
        ///
        /// 광산은 <b>참가자 목록이 시작하는 순간 굳는다.</b> 그 뒤에 들어온 사람은
        /// <c>Slot = -1</c> 인 관전 전용이라 턴도 힌트도 못 받는다. 판이 끝날 때까지
        /// 아무것도 못 하고 보고만 있게 되므로, 들여보내지 않는 쪽이 낫다.
        ///
        /// ⚠ <b>상태를 캐시하지 않고 그때그때 본다.</b> 캐시하면 "굳었다" 와 "요청이 왔다"
        ///    사이에 틈이 생기고, 그 틈으로 들어온 사람이 정확히 위의 상태가 된다.
        ///
        /// ⚠ 정원은 여기서 보지 않는다. <c>MiniGameAdmission</c> 이 <c>MiniGameConfig</c> 의
        ///    값으로 따로 막는다. 두 곳에서 같은 숫자를 들면 한쪽만 고치는 일이 생긴다.
        /// </summary>
        public bool CanAdmitNow(out string why)
        {
            if (IsOver)
            {
                why = $"이미 끝난 판입니다. ({Phase})";
                return false;
            }

            if (HasStarted)
            {
                why = $"이미 시작한 판입니다. ({Phase}) 참가자는 시작할 때 굳었습니다.";
                return false;
            }

            why = null;
            return true;
        }

        // ------------------------------------------------------------
        // 판 되돌리기 — 같은 서버가 다음 팀을 받는다
        // ------------------------------------------------------------

        /// <summary>
        /// <b>판이 끝나고 아무도 남지 않으면 대기 상태로 되돌린다.</b>
        ///
        /// 이것이 없으면 <b>끝난 판이 그대로 남는다.</b> 다음 사람이 들어오면 지난 판의
        /// 결과 화면부터 보게 된다 — 들어가자마자 "실패 26.7%" 다. 서버를 다시 띄워야만
        /// 풀렸다. 배 게임과 검 게임에서 똑같이 겪었다.
        ///
        /// <b>기준은 "아무도 없을 때" 하나다.</b> 끝난 판만이 아니라 진행 중이던 판도
        /// 사람이 전부 나가면 되돌린다. 남은 사람이 없는 판을 지켜 줄 이유가 없고,
        /// 오히려 남겨 두면 다음 팀이 남의 판 한가운데로 떨어진다.
        ///
        /// ⚠ <b>한 명이라도 남아 있으면 건드리지 않는다.</b> 둘 중 하나가 잠깐 끊겼다고
        ///    판을 날리면 돌아왔을 때 진행이 사라진다.
        /// </summary>
        private void ResetWhenEveryoneLeft()
        {
            if (Crew != 0 || Phase == MineMatchPhase.Waiting) return;

            Debug.Log($"[MineMatch] 판이 끝나고 아무도 남지 않았습니다. ({Phase}) 대기 상태로 되돌립니다.");
            ResetToWaiting();
        }

        /// <summary>
        /// 다음 팀을 받을 수 있는 상태로 모든 값을 되돌린다.
        ///
        /// ⚠ <b>네트워크 값을 하나라도 빠뜨리면 그 값만 지난 판에서 살아 넘어온다.</b>
        ///    복제되는 값이라 새로 들어온 사람이 그것을 그대로 받는다. 특히 두 개가 위험하다.
        ///
        /// <code>
        ///   ResultTick   남으면 들어가자마자 지난 판 결과 화면이 뜬다
        ///   BoardSeed    남으면 EnsureBoardOpen 이 건너뛰어 지난 판의 파인 격자를 그대로 쓴다
        /// </code>
        /// </summary>
        private void ResetToWaiting()
        {
            Phase = MineMatchPhase.Waiting;
            Countdown = 0f;
            TurnTimeLeft = 0f;

            RosterSize = 0;
            CurrentSlot = -1;

            RestoresLeft = 0;
            TotalRestores = 0;

            RevealLeft = 0f;
            HintLeft = 0f;
            HintSlot = -1;

            // 판을 새로 깔게 한다. 바로 다음 줄의 EnsureBoardOpen 이 새 시드로 깐다.
            BoardSeed = 0;

            Elapsed = 0f;

            // 결과 도장을 지운다. 남겨 두면 다음 판이 끝나도 화면이 "이미 보여 준 결과" 로
            // 보고 결과 판을 열지 않는다.
            ResultTick = 0;

            ResultPercent = 0f;
            ResultSuccess = false;
            ResultScore = 0;
            ResultTargetCount = 0;
            ResultDugCount = 0;
            ResultAlignX = 0;
            ResultAlignY = 0;
        }

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

            // ⚠ **결과 도장은 채점이 끝난 뒤에 찍는다.** 먼저 찍으면 화면이 아직 0 인 점수를
            //    읽어 간다. 위에서 격자를 못 찾아 return 한 경우에도 찍히지 않는다 —
            //    채점 못 한 판을 결과로 보여 주느니 안 보여 주는 쪽이 낫다.
            ResultTick = Runner.Tick == 0 ? 1 : Runner.Tick;

            Debug.Log(
                $"[MineMatch] 끝 — {(ResultSuccess ? "성공" : "실패")} · {result} " +
                $"(참가 {RosterSize}명 · 복구 {TotalRestores - RestoresLeft}개 씀, 틱 {ResultTick})");
        }

        // ------------------------------------------------------------
        // 결과를 공용 결과 화면으로 넘기기
        // ------------------------------------------------------------

        /// <summary>이미 띄운 결과인가. 클라이언트마다 따로 센다.</summary>
        private int shownResultTick;

        /// <summary>결과 판을 여는 것을 미루는 중인 코루틴. 없으면 null.</summary>
        private Coroutine resultRelease;

        /// <summary>
        /// 서버가 찍은 틱을 보고 <b>한 번만</b> 공용 결과 화면을 연다.
        ///
        /// <b>왜 0 일 때 기억을 지우는가.</b> 서버가 판을 되돌리면 <c>ResultTick</c> 이 0 이 된다.
        /// 그때 기억도 같이 지워야 다음 판의 결과를 새 것으로 본다. 안 지우면 두 번째 판부터
        /// 결과 화면이 안 열린다.
        /// </summary>
        private void PublishResultWhenReady()
        {
            if (ResultTick == 0)
            {
                shownResultTick = 0;
                return;
            }

            if (ResultTick == shownResultTick) return;

            shownResultTick = ResultTick;

            if (resultRelease != null) StopCoroutine(resultRelease);

            resultRelease = StartCoroutine(ShowResultAfterHold(
                ResultSuccess, ResultScore, Elapsed, ResultDugCount, RosterSize));
        }

        /// <summary>
        /// 잠깐 기다렸다가 결과를 넘긴다.
        ///
        /// ⚠ <b>기다리는 동안 보여 줄 것이 있을 때만 값을 준다.</b> 검 게임에서 3초를 뒀다가
        ///    그 동안 아무것도 없는 검은 화면만 남았다.
        ///
        /// 광산은 판이 끝나고 <b>두 화면</b>을 거친 뒤에야 공용 결과로 넘어간다.
        ///
        ///     0.0s  채굴 종료 — 제목만. 네 캐릭터가 판 위에 서 있고 우리가 판 그림이 보인다
        ///     2.0s  채굴 결과 — 캐릭터가 사라지고 성공·실패가 뜬다. 여기서 토글이 시작된다
        ///           2.0s  우리가 판 그림      5.0s  우리가 판 그림
        ///           3.0s  정답                6.0s  정답
        ///           4.0s  우리가 판 그림      7.0s  우리가 판 그림
        ///     8.0s  ← 공용 결과 판이 열린다
        ///
        /// 교대 주기가 1초(<c>resultSwapSeconds</c>)라서 결과 단계 6초 동안 여섯 번
        /// 바뀐다. 결과 단계를 너무 짧게 두면 <b>몇 번 바뀌지도 못하고 덮인다.</b>
        ///
        /// ⚠ 이 값들은 <c>MineLocalView.resultSwapSeconds</c> 와 짝이다. 한쪽만 바꾸면
        ///    엉뚱한 지점에서 잘린다.
        /// </summary>
        private IEnumerator ShowResultAfterHold(bool clear, int score, float playTime, int dug, int crew)
        {
            // 두 화면을 다 보여 준 뒤에 넘긴다. 제목 단계를 빼먹으면 공용 결과가
            // 그만큼 일찍 덮어 버린다.
            yield return new WaitForSeconds(finishTitleSeconds + resultHoldSeconds);

            MiniGameResultGateway.SubmitAuthoritative(new MiniGameResult(
                MiniGameId.Mining,
                clear,
                score,
                playTime,
                extraStatLabel: "채굴량",
                extraStatValue: dug.ToString(),
                playerCount: crew));

            resultRelease = null;
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
            // ⚠ **HUD 를 찾기 전에 부른다.** 결과 화면은 MineHud 와 아무 상관이 없는데,
            //    아래 탐색 뒤에 두면 HUD 를 못 찾은 화면에서는 결과가 영영 안 열린다.
            PublishResultWhenReady();

            if (_hud == null)
            {
                _hud = FindFirstObjectByType<MineHud>(FindObjectsInactive.Include);
                if (_hud == null) return;
            }

            _hud.NetworkDriven = true;

            // 판이 끝났으면 참가자 줄이 전부 "채굴 완료" 가 된다. CurrentSlot 은 이미
            // -1 로 돌아가 있어서 그것만으로는 가릴 수 없다. (MineHud.NetworkMatchOver)
            _hud.NetworkMatchOver = Phase == MineMatchPhase.Finished;

            // 목표를 보여 주는 7초 동안만 "도안을 기억하세요" 를 띄운다.
            // 힌트로 다시 볼 때는 안 띄운다 — 그건 그 사람만의 화면이고,
            // 이 값은 네 화면에 똑같이 나가기 때문이다.
            _hud.NetworkMemorizeShow = Phase == MineMatchPhase.Reveal;

            // ⚠ 판이 끝나면 화면이 **둘**이다. 제목 먼저, 성적표는 그 다음이다.
            //   둘을 같이 켜면 "채굴 종료" 위에 점수가 겹친다.
            _hud.NetworkFinishTitleShow = ShowingFinishTitle;
            _hud.NetworkResultShow = ShowingMineResult;

            if (_hud.NetworkResultShow)
            {
                // ⚠ 값만 넘긴다. 제목("성공!"/"실패!")도 칸 이름("도안 유사도" · "목표" ·
                //   "채굴")도 **판 그림에 박혀 있다.** 여기서 글자를 만들면 그림 위에 겹친다.
                //
                // ⚠ ResultPercent 는 넘기지 않는다. ResultScore 가 그것을 반올림한
                //   **같은 값**이라, 둘을 같이 적으면 한 정보를 두 번 적는 것이 된다.
                //   (MINE.md 2장)
                _hud.NetworkResultSuccess = ResultSuccess;
                _hud.NetworkResultScore = ResultScore;
                _hud.NetworkResultTargetCount = ResultTargetCount;
                _hud.NetworkResultDugCount = ResultDugCount;
            }
            _hud.NetworkPhaseText = PhaseLine();

            // 차례 칸. 판이 끝나면 CurrentSlot 이 -1 이라 적을 번호가 없는데, 그렇다고
            // 비워 두면 그림틀만 남아 빈 칸처럼 보인다. 그래서 "- / 4" 로 적는다.
            // (채굴 종료 · 채굴 결과 두 화면 내내 이 글자다)
            _hud.NetworkTurnText =
                Phase == MineMatchPhase.Finished && RosterSize > 0 ? $"- / {RosterSize}"
                : HasStarted && CurrentSlot >= 0 ? $"{CurrentSlot + 1} / {RosterSize}"
                : string.Empty;

            _hud.NetworkRosterSize = RosterSize;
            _hud.NetworkCurrentSlot = CurrentSlot;

            // 복구 총량은 BeginMatch 에서야 정해진다. 그 전에 그리면 "0 / 0" 이 뜬다.
            _hud.NetworkRestoreText = TotalRestores > 0
                ? $"{RestoresLeft} / {TotalRestores}"
                : string.Empty;

            // 다 쓰면 복구 판이 흑백이 된다. 힌트와 같은 규칙이다.
            _hud.NetworkRestoreLit = RestoresLeft > 0;

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
