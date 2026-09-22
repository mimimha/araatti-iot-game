using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>떨어지는 노트 하나. <c>Serial</c> 이 0 이면 빈 칸이다.</summary>
    public struct WarriorsNoteSlot : INetworkStruct
    {
        /// <summary>올릴 때마다 1씩 오른다. 0 은 빈 칸이라는 뜻이다.</summary>
        public int Serial;

        /// <summary>어느 레인인가. 플레이어 번호와 같다.</summary>
        public int Lane;

        /// <summary>어떤 공격으로 받아야 하는가. <see cref="WarriorsAttackDirection"/> 의 숫자값.</summary>
        public int Type;

        /// <summary>판정선에 닿는 틱.</summary>
        public int DueTick;

        /// <summary>0 떨어지는 중 · 1 성공 · 2 실패.</summary>
        public int State;
    }

    /// <summary>
    /// 3페이즈 — 최후의 리듬. **서버가 노트를 내고 서버가 판정한다.**
    ///
    /// <code>
    ///   자리 고정   2페이즈와 같은 자리에 선 채로 끝까지 간다
    ///   레인 분리   1P 노트는 1P 만, 2P 노트는 2P 만 받는다
    ///   합산 목표   성공한 노트를 팀 합계로 센다. 채우면 전체 클리어
    ///   쓰러진 사람 그 레인에는 노트가 나오지 않는다
    ///   놓치면      그 사람만 맞는다
    /// </code>
    ///
    /// <b>왜 <c>WarriorsRhythmBattle</c> 을 쓰지 않는가.</b>
    /// 그쪽은 <c>Time.time</c> 과 코루틴으로 도는 <b>혼자 플레이용</b> 루프다.
    /// 레인 수를 <c>WarriorsGameFlow</c> 에서 받아 오는데 그 부품이 꺼져 있어 늘 1이 되고,
    /// 반격 피해도 <b>플레이어 한 명</b>에게만 들어간다. 무엇보다 <c>Time.time</c> 은
    /// PC 마다 다르게 흐른다 — 같은 노트가 서로 다른 순간에 판정된다.
    ///
    /// <b>그래서 시간을 틱으로 센다.</b> <c>DueTick</c> 은 모든 PC 에서 같은 숫자이고,
    /// 입력도 틱에 맞춰 들어오므로 "언제 눌렀는가" 가 서버에서 정확히 하나로 정해진다.
    /// 화면은 각자 자기 틱으로 노트를 내려 그리기만 한다.
    ///
    /// ⚠ <b>혼자 플레이는 그대로 둔다.</b> <c>WarriorsRhythmBattle</c> 은 한 줄도 고치지 않았다.
    ///    <c>WarriorsTest.unity</c> 의 리듬 라운드는 예전 규칙(크라켄 HP 0 으로 종료) 그대로다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsPhase3Director : NetworkBehaviour
    {
        /// <summary>한 번에 하늘에 떠 있을 수 있는 노트 수. 레인당 절반씩 쓴다.</summary>
        public const int NoteCapacity = 12;

        [Header("무대")]
        [Tooltip("최종 형태를 보여 줄 크라켄. 생성 도구가 꽂는다.")]
        [SerializeField] private WarriorsKrakenBoss kraken;

        [Tooltip("3페이즈에 두 사람이 설 자리. 비워 두면 2페이즈 자리를 그대로 쓴다.")]
        [SerializeField] private Transform[] stands = new Transform[WarriorsPlayers.Max];

        [Header("리듬 규칙 — 내일 같이 조정한다")]
        [Tooltip("노트가 판정선까지 내려오는 데 걸리는 시간(초).")]
        [SerializeField, Min(.5f)] private float noteTravelSeconds = 1.45f;

        [Tooltip("정타로 인정되는 시간 폭(초, 앞뒤 각각). 노트(지름 64px)가 1.45초에 540px 내려오므로 " +
                 "동그라미가 판정선에 걸치는 구간은 약 ±0.18초다. 그보다 조금 넉넉히 준다.")]
        [SerializeField, Range(.1f, .6f)] private float goodWindow = .3f;

        /// <summary>
        /// **IoT 검 입력이 늦게 도착하는 만큼 판정을 당겨 준다(초).**
        ///
        /// 실제 검은 <c>IMU 센서 → 분류 → Unity 도착</c> 을 거치므로, 사람이 정확한 순간에 휘둘러도
        /// 스윙이 그만큼 늦게 들어온다. 그 지연을 빼지 않으면 잘 쳐도 늘 "조금 늦음" 으로 판정된다.
        ///
        /// ⚠ <b>기본값은 0 이다 — 감으로 넣지 않는다.</b> 실제 기기로 재서, 성공한 스윙이 평균 몇 초
        ///    늦게 도착하는지 로그로 확인한 뒤 그 값을 넣는다. 키보드 입력에는 이 지연이 없다.
        /// </summary>
        [Tooltip("IoT 스윙이 늦게 도착하는 시간(초)만큼 판정을 당긴다. 실측 전에는 0 으로 둔다.")]
        [SerializeField, Range(0f, .4f)] private float inputLatencyOffset;

        [Tooltip("노트를 놓쳤을 때 그 사람이 받는 피해.")]
        [SerializeField, Min(1)] private int missDamage = 6;

        [Tooltip("판정이 끝난 노트를 화면에서 치우기까지의 여유(초).")]
        [SerializeField, Min(0f)] private float clearSeconds = .35f;

        [Header("묶음(패턴) — 원본 WarriorsRhythmBattle 과 같은 규칙")]
        /// <summary>
        /// 한 묶음의 노트 수를 이 순서로 돌려 쓴다. 한 바퀴 안에서 3 → 4 로 올라가
        /// 앞은 익히는 구간, 뒤는 몰아치는 구간이 된다.
        ///
        /// <b>묶음을 길게 잡는 가장 큰 이유는 난이도가 아니라 길이다.</b>
        ///
        /// 한 묶음에는 노트 수와 무관한 <b>고정 비용</b>이 붙는다 —
        /// 마지막 노트가 판정선까지 내려오는 시간(<see cref="noteTravelSeconds"/>) +
        /// 정리 0.3초 + 쉬는 시간(<see cref="patternRestSeconds"/>). 약 3초다.
        /// 묶음이 짧으면 이 3초를 노트 두 개가 나눠 지므로 한 대당 시간이 커진다.
        ///
        /// 실측 80.1초짜리 판을 모델로 재보니(정확도 44%) 78.9초가 나와 계산이 맞았고,
        /// 그 판은 <b>묶음 16개</b>를 도는 동안 고정 비용만 약 50초를 썼다.
        /// 묶음을 3~4개로 올리면 같은 목표를 더 적은 묶음으로 채운다.
        /// </summary>
        [Tooltip("한 묶음의 노트 수를 이 순서로 돌려 쓴다. 묶음마다 붙는 고정 비용(약 3초)을 여러 노트가 나눠 진다.")]
        [SerializeField] private int[] patternLengths = { 3, 3, 4, 4, 3, 4, 4, 4 };

        /// <summary>
        /// 묶음 안 노트 사이 간격(초). 뒤 묶음으로 갈수록 <see cref="GapFloor"/> 배까지 좁아진다.
        ///
        /// <b>이 값의 하한을 정하는 것은 화면이 아니라 사람의 몸이다.</b> 실제 플레이는 IoT 검을
        /// 휘두르는 것이라, 한 번 휘두르고 자세를 되돌려 다음 종류로 바꾸는 시간이 필요하다.
        ///
        /// 게다가 <c>WarriorsIoTInput.swingCooldownSeconds</c> 가 <b>0.45초</b> 다 —
        /// 그보다 빨리 들어온 스윙은 <b>장치 단계에서 버려진다.</b> 간격이 거기에 가까우면
        /// 사람이 제때 휘둘러도 입력이 사라져 억울하게 놓친다.
        ///
        /// 0.80 에서 시작해 0.78 배(= 0.62초)까지만 좁힌다. 장치 한계 0.45초에 0.17초 여유가 남는다.
        /// 이 아래로 더 좁히면 사람이 제때 휘둘러도 장치가 스윙을 버린다.
        /// </summary>
        [Tooltip("묶음 안 노트 사이 간격(초). 뒤로 갈수록 78% 까지 좁아진다. IoT 검 0.45초 한계 위로 유지한다.")]
        [SerializeField, Min(.35f)] private float patternNoteGap = .80f;

        /// <summary>간격이 좁아지는 하한 비율. 0.80 × 0.78 = 0.62초.</summary>
        private const float GapFloor = .78f;

        [Tooltip("묶음이 모두 판정된 뒤 다음 묶음까지 쉬는 시간(초). 콤보 결과를 읽는 시간이다.")]
        [SerializeField, Min(0f)] private float patternRestSeconds = .55f;

        [Tooltip("묶음을 하나도 안 놓치고 다 받으면(콤보 피니시) 팀 목표에 더해 주는 보너스 성공 수.")]
        [SerializeField, Min(0)] private int finisherBonusHits = 1;

        [Tooltip("콤보 피니시 한 번의 점수. 원본 협동 보너스(500)와 같다.")]
        [SerializeField, Min(0)] private int finisherScore = 500;

        // ------------------------------------------------------------
        // 복제되는 것
        // ------------------------------------------------------------

        /// <summary>하늘에 떠 있는 노트들. 모든 화면이 같은 값을 본다.</summary>
        [Networked, Capacity(NoteCapacity)]
        public NetworkArray<WarriorsNoteSlot> Notes { get; }

        /// <summary>지금 3페이즈가 돌고 있는가. HUD 가 이 값으로 리듬 화면을 켠다.</summary>
        [Networked] public NetworkBool Running { get; private set; }

        /// <summary>콤보 피니시가 날 때마다 1씩 오른다. 화면이 이 번호가 바뀌는 것을 보고 문구를 낸다.</summary>
        [Networked] public int FinishSerial { get; private set; }

        /// <summary>마지막 피니시 종류. 1 한 사람 · 2 두 사람 모두.</summary>
        [Networked] public int FinishKind { get; private set; }

        /// <summary>
        /// 정타가 날 때마다 1씩 오른다. <b>크라켄 리액션을 클라이언트에서 보이게 하는 값이다.</b>
        ///
        /// ⚠ 왜 이 값이 있어야 하는가.
        ///    <c>ReportSwing</c> 은 <c>HasStateAuthority</c> 로 막혀 있어 <b>서버에서만</b> 돈다.
        ///    거기서 <c>kraken.PlayRhythmHit</c> 를 불러도 그 움찔거림은 서버 프로세스 안에서만
        ///    일어나고, 데디케이티드 서버는 <c>WarriorsServerCleanup</c> 이 화면 요소를 전부 꺼 두므로
        ///    <b>아무도 그것을 보지 못한다.</b> "판정은 성공인데 크라켄이 가만히 있다" 가 이것 때문이었다.
        ///    호출을 연결하는 것만으로는 고쳐지지 않는다.
        ///    아래 <c>ShowFinalForm</c> 자리에 이미 같은 취지의 주석이 있다 —
        ///    "서버에서만 세우면 서버에서만 보인다".
        ///
        /// 그래서 <b>번호만 복제하고 연출은 각 화면이 스스로 재생한다.</b>
        /// 피니시 문구가 쓰는 <c>FinishSerial</c> 과 같은 방식이다.
        /// </summary>
        [Networked] public int HitSerial { get; private set; }

        /// <summary>마지막 정타의 세기. 1 보통 · 2 강타(묶음을 이어 가는 중).</summary>
        [Networked] public int HitStrength { get; private set; }

        /// <summary>마지막 정타가 난 레인. 판정선을 그 줄만 번쩍이게 하는 데 쓴다.</summary>
        [Networked] public int HitLane { get; private set; }

        /// <summary>
        /// 지금 판의 3페이즈 담당. 공격 입력이 여기로 들어온다.
        ///
        /// <c>WarriorsNetPlayerCombat</c> 이 사람마다 <c>GetInput</c> 으로 버튼을 읽는데,
        /// 이 부품은 씬 오브젝트라 입력 권한이 없어 스스로 읽을 수 없다. 그래서 받는다.
        /// </summary>
        public static WarriorsPhase3Director Current { get; private set; }

        private WarriorsMatchState match;
        private WarriorsPhase2Director phase2;
        private WarriorsHudPresenter hud;

        private bool stageOpen;
        private int nextSerial = 1;

        // 묶음(패턴) 진행. 서버만 안다 — 결과는 Notes 와 FinishSerial 로 나간다.
        //
        // 원본은 "노트 묶음 → 판정 → 크라켄 반격 → 다음 묶음" 을 코루틴으로 돌렸다.
        // 여기서는 같은 흐름을 틱으로 센다. 끝없이 떨어지는 노트는 원본 규칙이 아니다.
        private readonly int[] laneHits = new int[WarriorsPlayers.Max];    // 이 묶음에서 이 사람이 받은 수
        private readonly int[] laneNotes = new int[WarriorsPlayers.Max];   // 이 묶음에서 이 사람 몫으로 낸 수
        private bool patternOpen;
        private int patternIndex;
        private int nextPatternTick;
        private int patternSettledTick = -1;   // 묶음의 모든 노트가 판정된 틱. -1 이면 아직

        // 피니시 문구(화면). 복제되지 않는 각자의 기억이다.
        private int shownFinishSerial;
        private float finishShownUntil;

        // 정타 리액션(화면). -1 은 "아직 한 번도 안 봤다" 는 뜻이다. 접속 직후 서버의 번호를
        // 그대로 받아 오는데, 그것을 정타로 치면 들어오자마자 크라켄이 한 번 튄다.
        private int shownHitSerial = -1;

        // 판정선 펄스(화면). 맞은 줄 하나만, 아주 잠깐.
        private int lanePulseLane = -1;
        private float lanePulseUntil;

        // 내 레인 판정 문구(화면). "좋아요!" · "놓쳤어요" 를 내 노트가 판정된 순간에만 낸다.
        private int localLane = -1;
        private float localLaneCheckAt;
        private readonly int[] shownSerial = new int[NoteCapacity];
        private readonly int[] shownState = new int[NoteCapacity];
        private string laneJudgement = string.Empty;
        private float laneJudgementUntil;

        // 일시정지(서버). 노트의 DueTick 은 절대 틱이라 멈춘 만큼 뒤로 미뤄야 한다.
        private bool paused;
        private int pauseStartTick;

        private readonly List<WarriorsRhythmNoteView> shownNotes = new();
        private bool shownStage;

        /// <summary>이 화면이 크라켄을 켜 두었는가. 새 판으로 돌아갈 때 끄는 데 쓴다.</summary>
        private bool shownKraken;

        /// <summary>이 화면의 카메라. 흔들 때만 쓴다. 서버에는 없다.</summary>
        private WarriorsThirdPersonCamera arenaCamera;

        // 일시정지(화면). 노트가 멈춘 채 보이도록 틱을 붙잡아 둔다.
        private bool renderPaused;
        private int renderHoldTick;

        public override void Spawned()
        {
            Current = this;

            match = WarriorsMatchState.Current;
            if (match == null) match = FindFirstObjectByType<WarriorsMatchState>(FindObjectsInactive.Include);

            phase2 = GetComponent<WarriorsPhase2Director>();
            hud = FindFirstObjectByType<WarriorsHudPresenter>(FindObjectsInactive.Include);

            if (kraken == null) kraken = FindFirstObjectByType<WarriorsKrakenBoss>(FindObjectsInactive.Include);

            // 늦게 들어온 사람이 지나간 피니시 문구를 한 번 띄우지 않게 지금 값에서 시작한다.
            shownFinishSerial = FinishSerial;

            if (!HasStateAuthority) return;

            if (kraken == null)
            {
                Debug.LogError("[WarriorsPhase3] 크라켄을 찾지 못했습니다. 최종 형태가 보이지 않습니다.", this);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Current == this) Current = null;
            if (hud != null) hud.NetworkRhythmActive = false;
        }

        // ------------------------------------------------------------
        // 규칙 — 서버만 돈다
        // ------------------------------------------------------------

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || match == null) return;

            // 라운드 소개 화면(3초)이 끝난 뒤에 무대를 연다. 두 화면이 소개를 읽는 동안 노트가 먼저 떨어지면 안 된다.
            bool wantStage = match.Phase == WarriorsMatchPhase.Phase3 && !match.InIntro && !match.InClearHold;

            if (wantStage != stageOpen)
            {
                stageOpen = wantStage;

                if (wantStage) OpenStage();
                else CloseStage();
            }

            if (!stageOpen) return;

            bool wantPause = match.IsPaused;

            if (wantPause != paused)
            {
                paused = wantPause;

                if (paused) pauseStartTick = Runner.Tick;
                else ShiftNotes(Runner.Tick - pauseStartTick);
            }

            if (paused) return;

            RetireFinishedNotes();
            MissOverdueNotes();
            DrivePattern();
        }

        /// <summary>
        /// 멈춘 동안 흐른 틱만큼 노트와 다음 노트 시각을 뒤로 미룬다.
        ///
        /// 그러지 않으면 재개 순간 하늘의 노트가 한꺼번에 판정선을 지나 전부 실패가 된다.
        /// </summary>
        private void ShiftNotes(int ticks)
        {
            if (ticks <= 0) return;

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);
                if (note.Serial == 0) continue;

                note.DueTick += ticks;
                Notes.Set(i, note);
            }

            nextPatternTick += ticks;
            if (patternSettledTick >= 0) patternSettledTick += ticks;

            Debug.Log($"[WarriorsPhase3] 일시정지 {ticks}틱만큼 노트를 뒤로 미뤘습니다.");
        }

        private void OpenStage()
        {
            // 촉수 무대를 걷고 최종 형태를 세운다. 이 함수는 원래 공개돼 있어 그대로 쓴다.
            if (kraken != null)
            {
                kraken.gameObject.SetActive(true);
                kraken.ShowFinalForm();
            }

            for (int i = 0; i < NoteCapacity; i++) Notes.Set(i, default);

            for (int lane = 0; lane < laneHits.Length; lane++)
            {
                laneHits[lane] = 0;
                laneNotes[lane] = 0;
            }

            patternOpen = false;
            patternIndex = 0;
            patternSettledTick = -1;
            // 무대가 선 뒤 한 박자 숨 고르고 첫 묶음을 낸다.
            nextPatternTick = Runner.Tick + TicksFor(.6f);
            FinishSerial = 0;
            FinishKind = 0;

            // ⚠ **새 판에서 반드시 지워야 하는 값들.**
            //    [다시 하기] 는 <c>WarriorsMatchState</c> 의 값만 되돌린다. 이 부품이 들고 있는
            //    복제 값은 그대로 남으므로, 여기서 지우지 않으면 다음 판이 지난 판의 상태를 물려받는다.
            //
            //    <see cref="HitSerial"/> 이 남으면 첫 정타에서 번호가 되돌아가 크라켄이 한 번 헛뛴다.
            HitSerial = 0;
            HitStrength = 0;
            HitLane = -1;

            Running = true;
            PlaceEveryone();

            Debug.Log(
                "[WarriorsPhase3] 최후의 리듬을 시작합니다. 레인은 사람마다 따로이고, " +
                $"노트는 {string.Join("·", patternLengths)}개 묶음으로 떨어집니다.");
        }

        private void CloseStage()
        {
            for (int i = 0; i < NoteCapacity; i++) Notes.Set(i, default);

            patternOpen = false;
            Running = false;
            Debug.Log("[WarriorsPhase3] 리듬 단계를 닫았습니다.");
        }

        /// <summary>판정이 끝나고 잠깐 지난 노트를 빈 칸으로 되돌린다.</summary>
        private void RetireFinishedNotes()
        {
            int clearTicks = TicksFor(clearSeconds);

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);

                if (note.Serial == 0 || note.State == 0) continue;
                if (Runner.Tick <= note.DueTick + clearTicks) continue;

                Notes.Set(i, default);
            }
        }

        /// <summary>판정선을 지나쳐 버린 노트. **그 레인의 사람만** 맞는다.</summary>
        private void MissOverdueNotes()
        {
            int goodTicks = TicksFor(goodWindow);

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);

                if (note.Serial == 0 || note.State != 0) continue;
                if (Runner.Tick <= note.DueTick + goodTicks) continue;

                note.State = 2;
                Notes.Set(i, note);

                // ⚠ 여기서 바로 때리지 않는다. 노트 하나 놓칠 때마다 크라켄이 반격하면
                //    리듬이 매번 끊기고, 실수 몇 번에 그대로 죽는다.
                //    묶음이 끝난 뒤 <see cref="SettlePattern"/> 이 성적을 보고 판단한다.
            }
        }

        /// <summary>
        /// **묶음 하나를 한 틱 진행한다.** 원본 <c>PatternLoop</c> 와 같은 순서다.
        ///
        /// <code>
        ///   묶음이 없다        쉬는 시간이 지났으면 새 묶음을 낸다
        ///   묶음이 떠 있다     모두 판정될 때까지 기다린다
        ///   모두 판정됐다      0.3초 뒤 결과를 정리한다 (콤보 피니시 · 보너스) → 쉬는 시간
        /// </code>
        /// </summary>
        private void DrivePattern()
        {
            if (!patternOpen)
            {
                if (Runner.Tick < nextPatternTick) return;

                SpawnPattern();
                return;
            }

            if (!AllNotesResolved())
            {
                patternSettledTick = -1;
                return;
            }

            if (patternSettledTick < 0)
            {
                patternSettledTick = Runner.Tick;
                return;
            }

            // 마지막 노트의 판정을 눈으로 볼 시간을 조금 준다. 원본의 0.3초와 같다.
            if (Runner.Tick < patternSettledTick + TicksFor(.3f)) return;

            SettlePattern();
        }

        /// <summary>떠 있는 노트 중 아직 판정되지 않은 것이 없는가.</summary>
        private bool AllNotesResolved()
        {
            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);
                if (note.Serial != 0 && note.State == 0) return false;
            }

            return true;
        }

        /// <summary>
        /// **살아 있는 사람마다 묶음 하나씩 낸다.** 길이는 <see cref="patternLengths"/> 를 돌려 쓴다.
        ///
        /// 원본과 같은 규칙: 뒤 묶음일수록 노트 사이가 좁아지되 실제 칼질이 따라갈 수 있는 만큼만,
        /// 같은 공격이 두 번 연달아는 되지만 세 번은 잘 나오지 않는다.
        /// 쓰러진 사람 레인에는 노트가 나오지 않는다 — 남은 사람이 목표를 채운다.
        /// </summary>
        /// <summary>
        // ⚠ **합동 결정타는 삭제했다.** 목표를 다 채웠는데도 "둘이 함께 치는 한 방" 을
        //    더 요구해서, 크라켄 체력이 0 인데 판이 끝나지 않고 멈춰 있었다.
        //    3페이즈는 목표 성공 횟수를 채우면 그대로 끝난다.

        private void SpawnPattern()
        {
            int length = PatternLength(patternIndex);
            float ramp = Mathf.Clamp01(patternIndex / 5f);
            int gapTicks = TicksFor(Mathf.Lerp(patternNoteGap, patternNoteGap * GapFloor, ramp));
            int firstDue = Runner.Tick + TicksFor(noteTravelSeconds) + TicksFor(.35f);
            int lanesUsed = 0;

            for (int lane = 0; lane < laneHits.Length; lane++)
            {
                laneHits[lane] = 0;
                laneNotes[lane] = 0;

                WarriorsPlayerLife life = FindLife(lane);
                if (life == null || !life.IsLive || life.IsDown) continue;

                // 두 레인은 같은 박자로 떨어진다. 예전엔 반 박자씩 어긋나게 했는데 두 사람 노트가
                // 번갈아 떨어져 화면이 어지러웠다. 남의 레인은 화면에서 흐리게 그려 구분한다.
                int laneOffset = 0;
                int previous = -1;

                for (int i = 0; i < length; i++)
                {
                    int slot = FreeSlot();
                    if (slot < 0) break;

                    int type = WarriorsRun.Range(0, 3);
                    if (type == previous && WarriorsRun.Range(0, 100) < 70) type = (type + WarriorsRun.Range(1, 3)) % 3;

                    Notes.Set(slot, new WarriorsNoteSlot
                    {
                        Serial = nextSerial++,
                        Lane = lane,
                        Type = type,
                        DueTick = firstDue + laneOffset + i * gapTicks,
                        State = 0,
                    });

                    laneNotes[lane]++;
                    previous = type;
                }

                if (laneNotes[lane] > 0) lanesUsed++;
            }

            // ⚠ **협동 노트는 삭제했다.** 두 레인에 같은 박자의 노트를 하나씩 더 붙이던
            //    규칙인데, 규칙에서 빠졌다. 되살리지 마라.

            patternIndex++;
            patternSettledTick = -1;

            if (lanesUsed == 0)
            {
                // 살아 있는 사람이 없다. (둘 다 쓰러지면 매치가 끝나므로 잠깐의 틈이다) 한 박자 뒤 다시 본다.
                nextPatternTick = Runner.Tick + TicksFor(patternRestSeconds);
                return;
            }

            patternOpen = true;
        }

        /// <summary>
        /// **묶음 결과를 정리한다.** 원본 <c>ApplyPatternOutcome</c> 에 해당한다.
        ///
        /// 한 사람이 자기 묶음을 하나도 놓치지 않으면 콤보 피니시다 — 팀 목표에 보너스를 더하고
        /// 모든 화면에 문구를 낸다. 놓친 노트는 이미 떨어질 때마다 그 사람이 맞았으므로
        /// 여기서 한 번 더 벌하지 않는다. (원본의 크라켄 반격은 그 자리에서 노트 놓침 피해로 대신한다)
        /// </summary>
        private void SettlePattern()
        {
            int finishers = 0;

            for (int lane = 0; lane < laneHits.Length; lane++)
            {
                if (laneNotes[lane] == 0) continue;

                // **묶음 성적으로 반격을 판단한다.** 절반도 못 받았으면 크라켄이 되받아친다.
                // 노트 하나 놓칠 때마다 때리던 예전 방식은 리듬을 매번 끊고, 몇 번 실수에 바로 죽었다.
                if (laneHits[lane] * 2 < laneNotes[lane]) Punish(lane);

                if (laneHits[lane] < laneNotes[lane]) continue;

                finishers++;

                for (int i = 0; i < finisherBonusHits; i++)
                {
                    if (match != null) match.ReportPhase3Hit();
                }
            }

            if (finishers > 0)
            {
                FinishKind = finishers > 1 ? 2 : 1;
                FinishSerial++;
                if (match != null) match.AddScore(finisherScore * finishers);

                Debug.Log(
                    $"[WarriorsPhase3] {(finishers > 1 ? "TEAM" : "COMBO")} FINISH! " +
                    $"{patternIndex}번째 묶음을 놓치지 않고 다 받았습니다. (보너스 +{finisherBonusHits * finishers})");
            }

            patternOpen = false;
            nextPatternTick = Runner.Tick + TicksFor(patternRestSeconds);
        }

        private int PatternLength(int index)
        {
            if (patternLengths == null || patternLengths.Length == 0) return 3;
            return Mathf.Clamp(patternLengths[index % patternLengths.Length], 1, NoteCapacity / WarriorsPlayers.Max);
        }

        /// <summary>
        /// **이 사람이 지금 칼을 휘둘렀다.** <c>WarriorsNetPlayerCombat</c> 이 서버에서 부른다.
        ///
        /// 자기 레인에서 판정선에 가장 가까운 노트 하나만 본다. 남의 레인은 건드리지 않는다 —
        /// 이것이 "각자 레인" 규칙이 실제로 지켜지는 지점이다.
        ///
        /// <code>
        ///   가까운 노트가 없다   아무 일도 없다. 헛스윙에 벌을 주지 않는다
        ///   모양이 맞다          성공. 팀 합계가 하나 오른다
        ///   모양이 틀리다        실패. 그 사람만 맞는다
        /// </code>
        /// </summary>
        public void ReportSwing(int lane, WarriorsAttackDirection direction)
        {
            if (!HasStateAuthority || !stageOpen || paused) return;

            int goodTicks = TicksFor(goodWindow);
            int best = -1;
            int bestDelta = int.MaxValue;

            // 스윙이 도착한 시각에서 파이프라인 지연을 뺀다. 그래야 "휘두른 순간" 으로 판정된다.
            // 기본값 0 이면 예전과 완전히 같다.
            int swungAt = Runner.Tick - TicksFor(inputLatencyOffset);

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);

                if (note.Serial == 0 || note.State != 0 || note.Lane != lane) continue;

                int delta = Mathf.Abs(swungAt - note.DueTick);
                if (delta >= bestDelta) continue;

                bestDelta = delta;
                best = i;
            }

            if (best < 0 || bestDelta > goodTicks) return;

            WarriorsNoteSlot hit = Notes.Get(best);
            bool correct = hit.Type == (int)direction;

            hit.State = correct ? 1 : 2;
            Notes.Set(best, hit);

            // 틀린 모양으로 휘둘러도 그 자리에서 때리지 않는다. 묶음 성적에 반영될 뿐이다.
            if (!correct) return;

            // **크라켄이 맞는 것이 보여야 한다.** 묶음을 이어 가는 중이면 더 세게 친다.
            //
            // ⚠ 여기서 연출을 직접 재생하지 않는다. 이 메서드는 서버에서만 돌기 때문에
            //    (위 HasStateAuthority) 여기서 PlayRhythmHit 을 불러 봐야 서버 화면에서만
            //    일어나고 클라이언트는 아무것도 못 본다. 서버는 체력만 깎고,
            //    연출은 HitSerial 을 보고 각 화면이 Render 에서 재생한다.
            {
                bool strong = lane >= 0 && lane < laneHits.Length && laneHits[lane] >= 2;

                if (kraken != null) kraken.ApplyRhythmHitSilently(strong);

                HitStrength = strong ? 2 : 1;
                HitLane = lane;
                HitSerial++;
            }

            // 묶음 안에서 몇 개를 받았는지 센다. 다 받으면 SettlePattern 이 콤보 피니시로 본다.
            if (lane >= 0 && lane < laneHits.Length) laneHits[lane]++;

            if (match != null) match.ReportPhase3Hit();
        }

        /// <summary>노트를 놓친 사람에게만 피해를 준다.</summary>
        private void Punish(int lane)
        {
            WarriorsPlayerLife life = FindLife(lane);
            if (life == null || !life.IsLive || life.IsDown) return;

            WarriorsHealth health = life.GetComponent<WarriorsHealth>();
            if (health != null) health.TryApplyDamage(missDamage);
        }

        /// <summary>두 사람을 자리에 세운다. 3페이즈 자리가 비어 있으면 2페이즈 자리를 쓴다.</summary>
        private void PlaceEveryone()
        {
            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life == null || !life.IsLive) continue;

                Transform stand = StandFor(life.PlayerIndex);
                if (stand == null) continue;

                Vector3 where = stand.position;

                // **혼자면 가운데에 선다.** 두 사람 자리는 좌우로 갈라 놓은 것이라, 혼자 그 자리에 서면
                // 화면이 한쪽으로 치우쳐 크라켄이 가운데에서 벗어나 보인다. 실제로 1인 실행에서
                // 시야가 왼쪽으로 쏠렸다. 3라운드는 노트 줄이 화면(HUD) 것이라 자리를 옮겨도 판정은 그대로다.
                //
                // ⚠ 2페이즈는 이렇게 하지 않는다. 그쪽은 담당 촉수가 자기 쪽에만 올라오므로
                //    가운데로 옮기면 오히려 자기 팔이 옆으로 밀려 보인다.
                Transform other = StandFor(life.PlayerIndex == 0 ? 1 : 0);
                if (match != null && match.Crew <= 1 && other != null) where = (stand.position + other.position) * 0.5f;

                WarriorsNetPlayerMover mover = life.GetComponent<WarriorsNetPlayerMover>();
                if (mover != null) mover.PlaceAt(where, stand.rotation);
            }
        }

        private Transform StandFor(int playerIndex)
        {
            if (stands != null && playerIndex >= 0 && playerIndex < stands.Length && stands[playerIndex] != null)
            {
                return stands[playerIndex];
            }

            return phase2 != null ? phase2.StandFor(playerIndex) : null;
        }

        private int FreeSlot()
        {
            for (int i = 0; i < NoteCapacity; i++)
                if (Notes.Get(i).Serial == 0) return i;

            return -1;
        }

        private int TicksFor(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds / Runner.DeltaTime));
        }

        private WarriorsPlayerLife FindLife(int index)
        {
            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life != null && life.IsLive && life.PlayerIndex == index) return life;
            }

            return null;
        }

        // ------------------------------------------------------------
        // 표시 — 모든 화면이 같은 노트를 같은 자리에 그린다
        // ------------------------------------------------------------

        /// <summary>
        /// 복제된 노트를 HUD 가 그릴 모양으로 옮긴다.
        ///
        /// <c>Travel</c> 은 0 에서 1 로 가는 값이고 1 이 판정선이다. 남은 틱을 전체 낙하
        /// 틱으로 나누면 그대로 나온다. 클라이언트의 틱은 서버보다 조금 앞서 있지만
        /// 그 차이는 노트 하나 크기보다 훨씬 작다.
        /// </summary>
        public override void Render()
        {
            if (hud == null) return;

            if (!Running)
            {
                shownStage = false;

                if (hud.NetworkRhythmActive)
                {
                    hud.NetworkRhythmActive = false;
                    hud.NetworkRhythmNotes.Clear();
                    hud.NetworkRhythmJudgement = string.Empty;
                    hud.NetworkLocalLane = -1;
                    hud.NetworkRhythmPulseLane = -1;
                    laneJudgementUntil = 0f;
                }

                // **새 판으로 돌아갔으면 크라켄을 내린다.**
                //
                // ⚠ 실제로 본 증상(2026-09-16 영상). 판이 처음으로 되돌아가 HUD 는
                //    ROUND 1 · 처치 0 · 03:00 인데 크라켄은 화면에 그대로 서 있었다.
                //    3페이즈에서 켜 준 것을 아무도 끄지 않았기 때문이다.
                //    진행 상태는 ROUND 1 인데 보이는 것은 ROUND 3 이라 둘이 섞여 보였다.
                //
                // 단, **결과 화면에서는 내리지 않는다.** 쓰러진 크라켄은 결과 직전까지
                // 보여야 하는 그림이다. 판이 끝난 상태(IsOver)가 아니라 대기/1라운드로
                // 돌아갔을 때만 치운다.
                bool backToNewMatch = match != null && !match.IsOver;

                if (backToNewMatch && shownKraken && !HasStateAuthority && kraken != null)
                {
                    shownKraken = false;
                    kraken.gameObject.SetActive(false);
                }

                return;
            }

            // 콤보 피니시 문구. 번호가 바뀐 순간부터 잠깐 보여 준다 — 원본의 1.1초와 같다.
            if (FinishSerial != shownFinishSerial)
            {
                shownFinishSerial = FinishSerial;
                finishShownUntil = Time.unscaledTime + 1.1f;

                // **피니시는 한 대와 다르게 끝나야 한다.** 묶음을 다 받아 낸 순간이 제일 센 순간인데
                // 지금까지는 글자만 떴다. 두 사람이 동시에 해냈으면(TEAM) 한 번 더 크게 친다.
                if (kraken != null) kraken.PlayRhythmFinish(FinishKind == 2);
                ShakeArenaCamera(FinishKind == 2 ? .42f : .26f);
            }

            // **정타 리액션.** 서버가 올려 준 번호가 바뀌면 이 화면에서 크라켄을 때린다.
            // 첫 프레임에 0 -> 값 으로 튀면서 몰아치지 않도록, 처음 본 번호는 재생 없이 맞춰만 둔다.
            if (HitSerial != shownHitSerial)
            {
                bool first = shownHitSerial < 0;
                shownHitSerial = HitSerial;

                if (!first)
                {
                    bool strong = HitStrength == 2;
                    if (kraken != null) kraken.PlayRhythmHit(strong);

                    // 판정선은 맞은 줄만 반응한다. 두 줄이 같이 번쩍이면 누가 맞췄는지 안 읽힌다.
                    lanePulseLane = HitLane;
                    lanePulseUntil = Time.unscaledTime + (strong ? .12f : .08f);

                    if (strong) ShakeArenaCamera(.14f);
                }
            }

            hud.NetworkRhythmPulseLane = Time.unscaledTime < lanePulseUntil ? lanePulseLane : -1;

            // 피니시가 먼저, 아니면 내 노트의 판정("좋아요!" · "놓쳤어요"). HUD 가 한국어로 옮긴다.
            hud.NetworkRhythmJudgement = Time.unscaledTime < finishShownUntil
                ? (FinishKind == 2 ? "TEAM FINISH!" : "COMBO FINISH!")
                : Time.unscaledTime < laneJudgementUntil ? laneJudgement : string.Empty;

            ResolveLocalLane();
            hud.NetworkLocalLane = localLane;

            // 최종 형태를 딱 한 번 세운다. 서버에서만 세우면 서버에서만 보인다 —
            // 크라켄 프리팹의 루트가 기본 비활성이라 켜 주는 쪽이 있어야 한다.
            if (!shownStage && !HasStateAuthority && kraken != null)
            {
                shownStage = true;
                shownKraken = true;
                kraken.gameObject.SetActive(true);
                kraken.ShowFinalForm();
            }

            int travelTicks = TicksFor(noteTravelSeconds);
            shownNotes.Clear();

            // 멈춘 동안에는 노트도 멈춰 보여야 한다. 서버가 풀릴 때 DueTick 을 미뤄 주므로
            // 붙잡아 둔 틱으로 그리면 재개 뒤에도 같은 자리에서 이어진다.
            bool pausedNow = match != null && match.IsPaused;

            if (pausedNow && !renderPaused)
            {
                renderPaused = true;
                renderHoldTick = Runner.Tick;
            }
            else if (!pausedNow)
            {
                renderPaused = false;
            }

            int now = renderPaused ? renderHoldTick : Runner.Tick;

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);

                if (note.Serial == 0)
                {
                    shownSerial[i] = 0;
                    continue;
                }

                float travel = 1f - (note.DueTick - now) / (float)travelTicks;

                // ⚠ **아직 트랙에 들어오지 않은 노트는 그리지 않는다.**
                //    노트 자리는 <c>LerpUnclamped(spawnY, hitLineY, travel)</c> 로 정해지는데,
                //    <c>travel</c> 이 음수면 스폰 높이보다 <b>위</b>에 그려진다.
                //    서버는 묶음을 미리 깔아 두고(협동 노트는 묶음 끝에서 0.9초 더 뒤) 예약하므로
                //    그 값이 크게 음수가 되어, 노트가 화면 꼭대기 ROUND·보스 카드 위까지 올라갔다.
                //    판정에는 영향이 없고 보이기만 잘못된 것이라 여기서 걸러 낸다.
                //    판정 문구는 아래 TrackJudgement 가 계속 따라가야 하므로 그것은 건너뛰지 않는다.
                if (travel >= 0f)
                {
                    shownNotes.Add(new WarriorsRhythmNoteView(
                        (WarriorsAttackDirection)note.Type, note.Lane, travel, note.State == 1, note.State == 2));
                }

                TrackJudgement(i, note);
            }

            hud.NetworkRhythmActive = true;
            hud.NetworkRhythmNotes.Clear();
            hud.NetworkRhythmNotes.AddRange(shownNotes);

            if (match != null)
            {
                // **보스의 남은 체력으로 보여 준다.**
                //
                // 안에서는 여전히 "성공 몇 번" 으로 센다. 하지만 화면에 "최후의 일격 5 / 30" 이라고
                // 적으면 보스전이 아니라 할당량 채우기처럼 읽힌다. 같은 값을 뒤집어
                // 100% 에서 0% 로 내려가는 체력으로 보여 주면, 한 번 벨 때마다 크라켄이
                // 깎이는 싸움이 된다. 규칙은 그대로고 읽히는 방식만 바뀐다.
                float damageDone = match.Phase3Target <= 0
                    ? 1f
                    : Mathf.Clamp01(match.Phase3Hits / (float)match.Phase3Target);

                // 막대가 주인공이고 숫자는 곁들이다. 큰 글씨로 "크라켄 HP 83%" 를 적으면
                // 그 카드가 크라켄 얼굴보다 먼저 눈에 들어온다.
                hud.NetworkRhythmProgress = 1f - damageDone;
                // 65% 로 줄인 숫자는 실측에서 읽기 어려웠다. 이름과 같은 크기로.
                hud.NetworkRhythmDetail = $"크라켄  {Mathf.CeilToInt((1f - damageDone) * 100f)}%";
                hud.NetworkRhythmLives = match.LivesLine();
            }
        }

        /// <summary>
        /// 내 노트가 판정된 순간을 잡아 문구를 낸다. **화면 쪽이다.**
        ///
        /// 복제된 <c>State</c> 가 0 → 1(성공) · 0 → 2(실패)로 바뀌는 순간만 본다.
        /// 남의 레인은 조용히 지나간다 — 내가 휘두른 결과만 알려 주는 것이 목적이다.
        /// 성공한 노트는 HUD 가 초록으로, 놓친 노트는 어둡게 그려 같은 순간을 눈으로도 보여 준다.
        /// </summary>
        private void TrackJudgement(int slot, WarriorsNoteSlot note)
        {
            if (shownSerial[slot] != note.Serial)
            {
                shownSerial[slot] = note.Serial;
                shownState[slot] = 0;
            }

            if (note.State == shownState[slot]) return;

            shownState[slot] = note.State;

            if (note.State == 0 || note.Lane != localLane) return;

            laneJudgement = note.State == 1 ? "GOOD" : "MISS";
            laneJudgementUntil = Time.unscaledTime + (note.State == 1 ? .45f : .6f);
        }

        /// <summary>
        /// 이 화면의 카메라를 흔든다. R3 는 <c>FocusArena</c> 로 고정 구도라 카메라가 따라다니지
        /// 않으므로, 흔들림이 타격의 무게를 대신 전한다.
        ///
        /// 서버에는 카메라가 없다(<c>WarriorsServerCleanup</c> 이 껐다). 못 찾으면 그냥 넘어간다.
        /// </summary>
        private void ShakeArenaCamera(float amount)
        {
            if (HasStateAuthority) return;

            if (arenaCamera == null)
            {
                Camera main = Camera.main;
                if (main != null) arenaCamera = main.GetComponent<WarriorsThirdPersonCamera>();
                if (arenaCamera == null) return;
            }

            arenaCamera.Shake(amount);
        }

        /// <summary>내 캐릭터의 레인. 입력 권한이 있는 <c>WarriorsPlayerLife</c> 의 번호다. 서버에는 없다(-1).</summary>
        private void ResolveLocalLane()
        {
            if (localLane >= 0 || Time.unscaledTime < localLaneCheckAt) return;

            localLaneCheckAt = Time.unscaledTime + 1f;

            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life == null || !life.IsLive || !life.HasInputAuthority) continue;

                localLane = life.PlayerIndex;
                return;
            }
        }

#if UNITY_EDITOR
        /// <summary>생성 도구가 무대 참조를 넣는다. **에디터 전용이다.**</summary>
        public void EditorSetStage(WarriorsKrakenBoss boss, Transform[] standPoints)
        {
            kraken = boss;
            stands = standPoints;
        }
#endif
    }
}
