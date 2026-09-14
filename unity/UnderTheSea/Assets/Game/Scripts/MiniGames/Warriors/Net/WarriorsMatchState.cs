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
        [Tooltip("1페이즈: 팀 합산 처치 수. 인원에 따라 늘리지 않는다.")]
        [SerializeField, Min(1)] private int phase1TargetKills = 15;

        [Tooltip("2페이즈: 팀 합산 촉수 성공 횟수.")]
        [SerializeField, Min(1)] private int phase2TargetTentacleHits = 15;

        [Tooltip("3페이즈: 팀 합산 리듬 성공 횟수. 채우면 전체 클리어.")]
        [SerializeField, Min(1)] private int phase3TargetRhythmHits = 15;

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

        private WarriorsHudPresenter hud;

        public override void Spawned()
        {
            Current = this;
            hud = FindFirstObjectByType<WarriorsHudPresenter>(FindObjectsInactive.Include);

            if (!HasStateAuthority) return;

            Phase = WarriorsMatchPhase.Waiting;
            Countdown = 0f;
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

            if (Phase1Kills < Phase1Target) return;

            Phase = WarriorsMatchPhase.Phase2;
            Debug.Log($"[WarriorsMatch] 1페이즈 목표 달성 ({Phase1Kills}/{Phase1Target}). 2페이즈로 넘어갑니다.");
        }

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

            if (Phase2Hits < Phase2Target) return;

            Phase = WarriorsMatchPhase.Phase3;
            Debug.Log($"[WarriorsMatch] 2페이즈 목표 달성 ({Phase2Hits}/{Phase2Target}). 3페이즈로 넘어갑니다.");
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

            if (Phase3Hits < Phase3Target) return;

            Phase = WarriorsMatchPhase.Cleared;
            Debug.Log($"[WarriorsMatch] 3페이즈 목표 달성 ({Phase3Hits}/{Phase3Target}). 전체 클리어입니다.");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            Crew = Runner.ActivePlayers.Count();

            if (IsOver) return;

            // 두 명 모두 쓰러지면 거기서 끝이다. 시작 전이면 아직 아무도 없으므로 지나간다.
            if (HasStarted && EveryoneDown())
            {
                Phase = WarriorsMatchPhase.Failed;
                Countdown = 0f;
                Debug.Log("[WarriorsMatch] 두 명 모두 쓰러졌습니다. 매치 실패.");
                return;
            }

            if (!HasStarted)
            {
                UpdateStartGate();
            }
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
            Phase = WarriorsMatchPhase.Phase1;

            Debug.Log($"[WarriorsMatch] 카운트다운이 끝났습니다. 1페이즈 시작. (인원 {Crew}명)");
        }

        /// <summary>살아 있는 사람이 하나도 없는가.</summary>
        private bool EveryoneDown()
        {
            WarriorsPlayerLife[] crew = FindObjectsByType<WarriorsPlayerLife>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (crew.Length == 0) return false;

            foreach (WarriorsPlayerLife one in crew)
                if (one != null && !one.IsDown) return false;

            return true;
        }

        // ------------------------------------------------------------
        // 표시 — 모든 화면이 같은 복제 값을 본다
        // ------------------------------------------------------------

        public override void Render()
        {
            if (hud == null) return;

            hud.MatchNotice = NoticeFor(Phase);
            hud.MatchDetail = DetailFor(Phase);
        }

        private string NoticeFor(WarriorsMatchPhase phase)
        {
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
                    return "ROUND 2  ·  크라켄 촉수";

                case WarriorsMatchPhase.Phase3:
                    return "ROUND 3  ·  최후의 일격";

                default:
                    return null;
            }
        }

        /// <summary>둘째 줄. 대기 중에는 살아 있는 사람의 남은 목숨을 보여 준다.</summary>
        private string DetailFor(WarriorsMatchPhase phase)
        {
            if (phase == WarriorsMatchPhase.Waiting || phase == WarriorsMatchPhase.Countdown)
            {
                return string.Empty;
            }

            if (phase == WarriorsMatchPhase.Phase1) return $"처치   {Phase1Kills} / {Phase1Target}";
            if (phase == WarriorsMatchPhase.Phase2) return $"촉수   {Phase2Hits} / {Phase2Target}";
            if (phase == WarriorsMatchPhase.Phase3) return $"리듬   {Phase3Hits} / {Phase3Target}";

            if (!IsOver) return null;

            return LivesLine();
        }

        /// <summary>"1P 목숨 3   2P DOWN" 한 줄. 리듬 화면도 이 줄을 쓴다.</summary>
        public string LivesLine()
        {
            WarriorsPlayerLife[] crew = FindObjectsByType<WarriorsPlayerLife>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(one => one.PlayerIndex)
                .ToArray();

            if (crew.Length == 0) return string.Empty;

            StringBuilder text = new StringBuilder();

            foreach (WarriorsPlayerLife one in crew)
            {
                if (text.Length > 0) text.Append("   ");
                text.Append($"{one.PlayerIndex + 1}P ").Append(one.IsDown ? "DOWN" : $"목숨 {one.Lives}");
            }

            return text.ToString();
        }
    }
}
