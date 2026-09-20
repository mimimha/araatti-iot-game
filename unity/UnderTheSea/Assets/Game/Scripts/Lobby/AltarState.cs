using System;
using UnderTheSea.Account;
using UnderTheSea.Inventory;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 제단 상태의 <b>클라이언트 캐시</b>.
    ///
    /// ⚠ 원본은 서버(AraAtti.Api + MySQL)다. 여기 있는 값은 전부 서버가 준 것을 그대로 담은 것이다.
    ///
    /// <b>파생값을 여기서 계산하지 않는다.</b> RemainingToTarget · MaxOfferAmount ·
    /// AltarActivated · RecoveryPercent 는 서버가 계산해서 내려준다. 클라이언트가 같은 공식을
    /// 또 쓰면 어느 날 한쪽만 고쳐진다. (설계 9.2 · 11.1절)
    ///
    /// <b>Unity 의 화면을 모른다.</b> Renderer · Light · ParticleSystem · Canvas 를 참조하지 않는다.
    ///
    /// ⚠ <see cref="Changed"/> 는 <b>지속 상태</b>가 바뀌었다는 뜻이다.
    ///    회복도 HUD · 봉헌 UI · 완료 상태 표시가 듣는다.
    ///    <b>Blue VFX 의 트리거가 아니다.</b> Blue VFX 는 "성공한 봉헌 사건" 하나당 한 번이고,
    ///    그 사건 전달은 STEP 8 · 10 에서 만든다. (결정 #9, 설계 12.3절)
    ///
    /// 이 클래스는 상태 캐시이면서 <b>서버 스냅샷 신선도의 기준점</b>이기도 하다.
    /// 조각 수를 싣고 오는 응답이 셋이라 순번을 한 곳에서 세야 하기 때문이다. (아래 "신선도" 영역)
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 9.2 · 12.3 · 12.7절
    /// </summary>
    public static class AltarState
    {
        // ------------------------------------------------------------
        // 서버가 준 값 (전부 읽기 전용)
        // ------------------------------------------------------------

        /// <summary>모두가 지금까지 봉헌한 총량.</summary>
        public static long TotalOffered { get; private set; }

        /// <summary>목표 봉헌량. 서버 DB 값이다. 1000 을 코드에 박지 않는다.</summary>
        public static long TargetOffering { get; private set; }

        /// <summary>아직 받을 수 있는 양. <b>서버 계산값</b>이다.</summary>
        public static long RemainingToTarget { get; private set; }

        /// <summary>내 조각 보유량. <see cref="PlayerInventory.SeaHeartFragment"/> 와 같은 값이다.</summary>
        public static long MyFragments { get; private set; }

        /// <summary>이번에 봉헌할 수 있는 최대. <b>서버 계산값</b>이다.</summary>
        public static long MaxOfferAmount { get; private set; }

        /// <summary>
        /// 섬 회복이 완료됐는지. <b>서버 판단값</b>이다.
        ///
        /// ⚠ 이 값으로 Blue VFX 를 켜지 않는다. 완료 상태와 VFX 는 분리되어 있다 (결정 #9).
        ///    봉헌 종료 · 봉헌 버튼 잠금 · 회복도 100% 표시에 쓴다.
        /// </summary>
        public static bool AltarActivated { get; private set; }

        /// <summary>
        /// 섬 회복도(%). <b>서버 계산값</b>이고 100 을 넘지 않는다.
        ///
        /// ⚠ 서버가 float 로 계산하므로 50.300003 같은 값이 올 수 있다. 여기서는 그대로 담는다.
        ///    자리수를 다듬는 것은 화면의 몫이다.
        /// </summary>
        public static float RecoveryPercent { get; private set; }

        /// <summary>
        /// 내가 지금까지 봉헌한 합계. 전체 <see cref="TotalOffered"/> 와 다른 값이다.
        ///
        /// ⚠ 봉헌 응답에는 이 필드가 없다. 그래서 봉헌 직후에는 한 건만큼 뒤처져 있을 수 있고,
        ///    다음 <see cref="RequestRefresh"/> 때 맞춰진다. 로컬로 더하지 않는다.
        /// </summary>
        public static long MyOfferedTotal { get; private set; }

        /// <summary>
        /// 서버 altar_state 가 마지막으로 바뀐 시각(UTC ISO-8601 문자열).
        ///
        /// ⚠ 로그·디버그용이다. 신선도 판정에도 VFX 판정에도 쓰지 않는다.
        ///    전역 시각이라 내 보유량의 신선도를 말해 주지 못한다. (설계 12.7절)
        /// </summary>
        public static string UpdatedAt { get; private set; }

        /// <summary>서버 값을 한 번이라도 받았는지.</summary>
        public static bool HasValue { get; private set; }

        /// <summary>
        /// 지속 상태가 바뀌었을 때 오른다.
        ///
        /// ⚠ 다시 적는다 — <b>이것은 Blue VFX 트리거가 아니다.</b> (설계 12.3절)
        /// </summary>
        public static event Action Changed;

        // ------------------------------------------------------------
        // 조회
        // ------------------------------------------------------------

        /// <summary>조회가 진행 중인지. 같은 질문을 두 번 하지 않기 위한 값이다.</summary>
        private static bool inFlight;

        /// <summary>
        /// 서버에 지금 상태를 물어본다. (GET /api/altar/state)
        ///
        /// 아무 때나 불러도 안전하다. 세 가지를 여기서 처리한다.
        ///
        /// <code>
        ///   봉헌(POST)이 진행 중이면   → 지금 묻지 않고 "끝나면 한 번" 으로 미룬다
        ///   이미 묻고 있으면           → 합친다 (coalescing)
        ///   그 외                      → 서비스에 요청한다
        /// </code>
        ///
        /// ⚠ 이것을 불렀다고 Blue VFX 가 나가면 안 된다. 조회는 사건이 아니다. (설계 12.5절)
        /// </summary>
        public static void RequestRefresh()
        {
            if (DeferIfMutating(PendingRefresh.Altar))
            {
                return;
            }

            if (inFlight)
            {
                return;
            }

            IAltarService service = AccountServiceLocator.Altar;
            if (service == null)
            {
                return;
            }

            inFlight = true;
            service.RequestState();
        }

        /// <summary>성공이든 실패든 조회가 끝나면 반드시 불린다. 빼먹으면 이후 조회가 영영 막힌다.</summary>
        internal static void RequestFinished()
        {
            inFlight = false;
        }

        // ------------------------------------------------------------
        // 신선도 — 조각 수를 싣고 오는 모든 응답이 공유한다
        //
        // 늦게 도착한 옛 응답이 새 값을 덮어쓰는 것을 막는다.
        //
        //   GET  /api/inventory      quantity
        //   GET  /api/altar/state    myFragments
        //   POST /api/altar/offer    remainingFragments
        //
        // 셋이 같은 숫자를 건드리므로 순번도 하나로 센다. 따로 세면
        // "봉헌으로 76 이 됐는데 먼저 출발한 인벤토리 조회가 77 로 되돌리는" 일이 생긴다.
        // ------------------------------------------------------------

        /// <summary>요청을 낼 때마다 +1.</summary>
        private static int issuedSequence;

        /// <summary>마지막으로 캐시에 반영한 번호.</summary>
        private static int appliedSequence;

        /// <summary>요청 직전에 부른다. 이 응답이 몇 번째인지 표를 받아 간다.</summary>
        internal static int IssueSequence()
        {
            return ++issuedSequence;
        }

        /// <summary>
        /// 이 응답을 캐시에 반영해도 되는지.
        ///
        /// 더 새 것을 이미 반영했으면 false 다. 그때는 <b>캐시만</b> 건드리지 않는다 —
        /// 요청의 완료 통지(성공·실패 이벤트)는 그대로 올려야 화면이 멈추지 않는다.
        /// </summary>
        internal static bool TryAcceptSequence(int sequence)
        {
            if (sequence < appliedSequence)
            {
                return false;
            }

            appliedSequence = sequence;
            return true;
        }

        // ------------------------------------------------------------
        // 봉헌 중 차단 (mutation barrier)
        //
        // 순번만으로는 부족하다. 봉헌이 도는 동안 새 조회를 계속 내보내면
        // 버려질 응답을 만들고 서버만 때린다. 봉헌이 끝날 때까지 미뤘다가 한 번만 낸다.
        // ------------------------------------------------------------

        /// <summary>미뤄 둔 조회의 종류.</summary>
        internal enum PendingRefresh
        {
            Altar,
            Inventory
        }

        private static bool mutating;
        private static bool pendingAltarRefresh;
        private static bool pendingInventoryRefresh;

        /// <summary>봉헌이 진행 중이면 요청을 미루고 true 를 돌려준다.</summary>
        internal static bool DeferIfMutating(PendingRefresh which)
        {
            if (!mutating)
            {
                return false;
            }

            if (which == PendingRefresh.Altar)
            {
                pendingAltarRefresh = true;
            }
            else
            {
                pendingInventoryRefresh = true;
            }

            return true;
        }

        /// <summary>POST /api/altar/offer 를 보내기 직전에 부른다.</summary>
        internal static void BeginMutation()
        {
            mutating = true;
        }

        /// <summary>
        /// 봉헌이 끝나면 부른다. 미뤄 둔 조회를 <b>종류별로 최대 한 번씩</b> 내보낸다.
        /// </summary>
        /// <param name="receivedServerState">
        /// 권위 상태를 실제로 받아 적용했는지. 네트워크 실패 등으로 못 받았으면 false —
        /// 그때는 캐시를 그대로 두고 한 번 다시 물어본다.
        /// </param>
        internal static void EndMutation(bool receivedServerState)
        {
            mutating = false;

            if (!receivedServerState)
            {
                pendingAltarRefresh = true;
            }

            bool refreshAltar = pendingAltarRefresh;
            bool refreshInventory = pendingInventoryRefresh;

            pendingAltarRefresh = false;
            pendingInventoryRefresh = false;

            if (refreshAltar)
            {
                RequestRefresh();
            }

            if (refreshInventory)
            {
                PlayerInventory.RequestRefresh();
            }
        }

        // ------------------------------------------------------------
        // 적용
        //
        // 서버 응답이 캐시로 들어오는 길은 아래 둘뿐이다. 여기가 유일한 입구다.
        // ------------------------------------------------------------

        /// <summary>GET /api/altar/state 의 결과. 아홉 필드가 전부 온다.</summary>
        internal static void ApplyStateSnapshot(int sequence, AltarStateDto snapshot)
        {
            if (snapshot == null || !TryAcceptSequence(sequence))
            {
                return;
            }

            TotalOffered = snapshot.totalOffered;
            TargetOffering = snapshot.targetOffering;
            RemainingToTarget = snapshot.remainingToTarget;
            MyFragments = snapshot.myFragments;
            MaxOfferAmount = snapshot.maxOfferAmount;
            AltarActivated = snapshot.altarActivated;
            RecoveryPercent = snapshot.recoveryPercent;
            MyOfferedTotal = snapshot.myOfferedTotal;
            UpdatedAt = snapshot.updatedAt;
            HasValue = true;

            PlayerInventory.SetFragments(snapshot.myFragments);

            Changed?.Invoke();
        }

        /// <summary>
        /// POST /api/altar/offer 의 결과. <b>성공과 409 실패 모두</b> 이 길로 들어온다.
        ///
        /// ⚠ myOfferedTotal 과 updatedAt 은 봉헌 응답에 없다. 기존 값을 그대로 둔다.
        ///    0 으로 덮으면 화면이 "내가 한 번도 봉헌 안 한" 상태로 보인다.
        /// </summary>
        internal static void ApplyOfferSnapshot(
            int sequence,
            long totalOffered,
            long targetOffering,
            long remainingToTarget,
            long remainingFragments,
            long maxOfferAmount,
            bool altarActivated,
            float recoveryPercent)
        {
            if (!TryAcceptSequence(sequence))
            {
                return;
            }

            TotalOffered = totalOffered;
            TargetOffering = targetOffering;
            RemainingToTarget = remainingToTarget;
            MyFragments = remainingFragments;
            MaxOfferAmount = maxOfferAmount;
            AltarActivated = altarActivated;
            RecoveryPercent = recoveryPercent;
            HasValue = true;

            PlayerInventory.SetFragments(remainingFragments);

            Changed?.Invoke();
        }

        /// <summary>
        /// 로그아웃할 때 메모리 캐시만 비운다. 서버 데이터는 건드리지 않는다.
        /// </summary>
        public static void Clear()
        {
            TotalOffered = 0;
            TargetOffering = 0;
            RemainingToTarget = 0;
            MyFragments = 0;
            MaxOfferAmount = 0;
            AltarActivated = false;
            RecoveryPercent = 0f;
            MyOfferedTotal = 0;
            UpdatedAt = null;
            HasValue = false;

            inFlight = false;
            mutating = false;
            pendingAltarRefresh = false;
            pendingInventoryRefresh = false;

            // ⚠ 아직 돌아오지 않은 요청들을 전부 무효로 만든다.
            //    번호를 하나 태워 두면 그보다 작은 번호(= 로그아웃 전에 출발한 응답)는
            //    돌아와도 캐시에 들어가지 못한다. 이전 계정의 숫자가 다음 사람 화면에 뜨는 것을 막는다.
            appliedSequence = ++issuedSequence;

            Changed?.Invoke();
        }
    }
}
