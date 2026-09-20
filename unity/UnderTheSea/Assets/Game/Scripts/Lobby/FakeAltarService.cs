using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnderTheSea.Account;
using UnderTheSea.Inventory;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 서버 없이 제단을 흉내내는 가짜 서비스.
    ///
    /// ⚠ HTTP · UnityWebRequest 를 쓰지 않습니다. 전부 이 PC 안에서 끝납니다.
    ///
    /// 서버와 같은 규칙을 흉내냅니다.
    ///   - totalOffered 는 targetOffering 을 넘지 않는다 (하드캡)
    ///   - 부분 수락 없음. 남은 칸보다 많이 요청하면 전체 실패
    ///   - 같은 requestId 재요청은 duplicate 로 처리하고 수량을 다시 깎지 않는다
    ///
    /// ⚠ 동시성(MySQL 트랜잭션·UNIQUE 경쟁)까지 흉내내지는 않습니다. 화면 흐름 확인용입니다.
    ///
    /// ⚠ 성공했다고 여기서 연출을 재생하지 않습니다. 가짜도 진짜와 똑같이 <b>응답만</b> 돌려줍니다.
    ///    (결정 #9 — 봉헌 성공 event 와 Blue VFX 는 STEP 8 · 10 의 몫)
    /// </summary>
    public class FakeAltarService : MonoBehaviour, IAltarService
    {
        [Header("가짜 서버 초기값")]
        [Tooltip("목표 봉헌량. 서버 altar_state.target_offering 에 해당한다.")]
        [SerializeField] private long targetOffering = 1000;

        [Tooltip("시작 시 이미 봉헌된 양.")]
        [SerializeField] private long startingTotalOffered = 0;

        [Tooltip("시작 시 내가 들고 있는 조각 수.")]
        [SerializeField] private long startingFragments = 100;

        [Header("응답 지연 (초)")]
        [SerializeField, Range(0f, 3f)] private float responseDelay = 0.3f;

        [Header("주기 동기화")]
        [SerializeField, Range(5f, 300f)] private float statePollIntervalSeconds = 30f;

        public event Action<bool, AltarStateDto, string> OnStateResult;
        public event Action<AltarOfferOutcome> OnOfferResult;

        private Coroutine pollingRoutine;

        /// <summary>가짜 "서버" 가 들고 있는 값. 캐시가 아니라 원본 역할이다.</summary>
        private long totalOffered;
        private long fragments;

        /// <summary>이미 처리한 봉헌. requestId → 그때 반영한 수량. 멱등성 흉내용이다.</summary>
        private readonly Dictionary<string, long> processed = new Dictionary<string, long>();

        /// <summary>
        /// 가짜 인벤토리가 읽어 갈 조각 수.
        ///
        /// 두 가짜 서비스가 같은 숫자를 봐야 하므로 여기 하나만 둔다.
        /// 없으면(제단 가짜가 아직 안 생겼으면) null 이다.
        /// </summary>
        internal static FakeAltarService Current { get; private set; }

        internal long Fragments => fragments;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            totalOffered = Math.Max(0, Math.Min(startingTotalOffered, targetOffering));
            fragments = Math.Max(0, startingFragments);

            Current = this;
            AccountServiceLocator.Register(this);
            Debug.Log("[FakeAltarService] 가짜 제단 상태를 사용합니다. 서버에 붙지 않습니다.", this);
        }

        private void OnDestroy()
        {
            StopStatePolling();

            if (Current == this)
            {
                Current = null;
            }

            AccountServiceLocator.Unregister(this);
        }

        // ------------------------------------------------------------
        // 조회
        // ------------------------------------------------------------

        public void RequestState()
        {
            StartCoroutine(RequestStateRoutine());
        }

        private IEnumerator RequestStateRoutine()
        {
            int sequence = AltarState.IssueSequence();

            yield return Wait();

            AltarStateDto snapshot = BuildSnapshot();

            AltarState.ApplyStateSnapshot(sequence, snapshot);

            AltarState.RequestFinished();
            OnStateResult?.Invoke(true, snapshot, string.Empty);
        }

        // ------------------------------------------------------------
        // 봉헌
        // ------------------------------------------------------------

        public void Offer(long amount, string requestId)
        {
            StartCoroutine(OfferRoutine(amount, requestId));
        }

        private IEnumerator OfferRoutine(long amount, string requestId)
        {
            AltarState.BeginMutation();

            int sequence = AltarState.IssueSequence();

            yield return Wait();

            AltarOfferOutcome outcome = Evaluate(amount, requestId, out bool applySnapshot);

            if (applySnapshot)
            {
                AltarState.ApplyOfferSnapshot(
                    sequence,
                    totalOffered,
                    targetOffering,
                    RemainingToTarget,
                    fragments,
                    MaxOfferAmount,
                    AltarActivated,
                    RecoveryPercent);
            }

            OnOfferResult?.Invoke(outcome);

            AltarState.EndMutation(applySnapshot);
        }

        /// <summary>
        /// 가짜 서버의 판정. 서버 검증 순서(설계 8.6절)를 그대로 흉내낸다.
        /// </summary>
        /// <param name="applySnapshot">상태를 캐시에 반영해야 하는 응답인지. 입력 오류(400)는 false.</param>
        private AltarOfferOutcome Evaluate(long amount, string requestId, out bool applySnapshot)
        {
            applySnapshot = false;

            if (amount <= 0)
            {
                return Failure(requestId, "AMOUNT_INVALID", "봉헌할 수량은 1개 이상이어야 합니다.");
            }

            if (amount > int.MaxValue)
            {
                return Failure(requestId, "AMOUNT_TOO_LARGE", "봉헌할 수량이 너무 큽니다.");
            }

            if (!Guid.TryParse(requestId, out _))
            {
                return Failure(requestId, "REQUEST_ID_INVALID", "요청 식별자 형식이 올바르지 않습니다.");
            }

            applySnapshot = true;

            // 같은 요청을 다시 받았다. 봉헌을 다시 하지 않고 처음 반영한 수량을 돌려준다.
            if (processed.TryGetValue(requestId, out long alreadyOffered))
            {
                return new AltarOfferOutcome(
                    true, true, alreadyOffered, requestId, string.Empty, string.Empty, true);
            }

            if (RemainingToTarget <= 0)
            {
                return Failure(requestId, "OFFERING_CLOSED", "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다.");
            }

            if (amount > RemainingToTarget)
            {
                return Failure(requestId, "OFFERING_AMOUNT_CHANGED", "다른 플레이어가 먼저 봉헌했습니다.");
            }

            if (amount > fragments)
            {
                return Failure(
                    requestId,
                    "NOT_ENOUGH_FRAGMENTS",
                    $"보유한 조각이 모자랍니다. (보유 {fragments} / 요청 {amount})");
            }

            totalOffered += amount;
            fragments -= amount;
            processed[requestId] = amount;

            return new AltarOfferOutcome(
                true, false, amount, requestId, string.Empty, string.Empty, true);
        }

        private static AltarOfferOutcome Failure(string requestId, string code, string message)
        {
            return new AltarOfferOutcome(false, false, 0, requestId, code, message, true);
        }

        // ------------------------------------------------------------
        // 주기 동기화
        // ------------------------------------------------------------

        public void StartStatePolling()
        {
            if (pollingRoutine != null)
            {
                return;
            }

            pollingRoutine = StartCoroutine(PollStateRoutine());
        }

        public void StopStatePolling()
        {
            if (pollingRoutine == null)
            {
                return;
            }

            StopCoroutine(pollingRoutine);
            pollingRoutine = null;
        }

        private IEnumerator PollStateRoutine()
        {
            WaitForSeconds interval = new WaitForSeconds(statePollIntervalSeconds);

            while (true)
            {
                yield return interval;
                AltarState.RequestRefresh();
            }
        }

        // ------------------------------------------------------------
        // 가짜 서버의 계산
        //
        // ⚠ 이것은 "서버가 하는 계산" 자리다. 캐시(AltarState)는 여전히 아무것도 계산하지 않고
        //    여기서 만든 값을 그대로 받는다.
        // ------------------------------------------------------------

        private long RemainingToTarget => Math.Max(0, targetOffering - totalOffered);

        private long MaxOfferAmount => Math.Min(fragments, RemainingToTarget);

        private bool AltarActivated => totalOffered >= targetOffering;

        private float RecoveryPercent =>
            targetOffering <= 0 ? 0f : Mathf.Min(totalOffered / (float)targetOffering, 1f) * 100f;

        private AltarStateDto BuildSnapshot()
        {
            long myOfferedTotal = 0;
            foreach (long offered in processed.Values)
            {
                myOfferedTotal += offered;
            }

            return new AltarStateDto
            {
                totalOffered = totalOffered,
                targetOffering = targetOffering,
                remainingToTarget = RemainingToTarget,
                myFragments = fragments,
                maxOfferAmount = MaxOfferAmount,
                altarActivated = AltarActivated,
                recoveryPercent = RecoveryPercent,
                myOfferedTotal = myOfferedTotal,
                updatedAt = DateTime.UtcNow.ToString("o")
            };
        }

        private IEnumerator Wait()
        {
            if (responseDelay > 0f)
            {
                yield return new WaitForSeconds(responseDelay);
            }
        }

        // ------------------------------------------------------------
        // 손으로 확인하는 길 (UI 가 아직 없다)
        //
        // 플레이 모드에서 "AccountService (Fake)" 오브젝트를 고르고,
        // 이 컴포넌트의 ⋮ 메뉴에서 실행한다.
        //
        // ⚠ 여기서 totalOffered · fragments 를 직접 건드리지 않는다. 언제나
        //      ContextMenu → 서비스 public API → 기존 응답·적용 경로 → AltarState
        //    순서로만 값이 움직인다. 그래야 이 메뉴로 확인한 것이 실제 동작과 같다.
        // ------------------------------------------------------------

        /// <summary>
        /// 바로 앞 "디버그 — 1개 봉헌" 이 쓴 requestId.
        ///
        /// 재요청 메뉴가 <b>같은 값</b>을 다시 보내기 위해 들고 있는다.
        /// 한 번도 봉헌하지 않았으면 비어 있다.
        /// </summary>
        private string lastDebugRequestId = string.Empty;

        /// <summary>읽기만 한다. 가짜 서버의 값을 바꾸지 않는다.</summary>
        [ContextMenu("디버그 — 제단 상태 조회")]
        private void DebugRequestState()
        {
            // 진짜와 같은 입구를 쓴다. 중복 요청 합치기와 봉헌 중 차단까지 그대로 탄다.
            OnStateResult += LogStateOnce;
            AltarState.RequestRefresh();
        }

        private void LogStateOnce(bool success, AltarStateDto snapshot, string failureMessage)
        {
            OnStateResult -= LogStateOnce;

            if (!success)
            {
                Debug.LogWarning($"[FakeAltarService] 상태 조회 실패 — {failureMessage}", this);
                return;
            }

            Debug.Log($"[FakeAltarService] 상태 조회 성공 — {DescribeCache()}", this);
        }

        /// <summary>
        /// ⚠ 가짜 서버의 수량이 실제로 바뀐다. 사람이 메뉴를 눌렀을 때만 실행된다.
        ///
        /// 여기서 requestId 를 만드는 것은 이 메서드가 <b>호출자 역할</b>이기 때문이다.
        /// 서비스가 요청을 보내면서 스스로 만드는 것과는 다르다 — 그렇게 하면 재시도 때
        /// 값이 바뀌어 중복 방지가 무력해진다. (설계 10.4·10.5절)
        /// </summary>
        [ContextMenu("디버그 — 1개 봉헌")]
        private void DebugOfferOne()
        {
            lastDebugRequestId = Guid.NewGuid().ToString();

            Debug.Log($"[FakeAltarService] 봉헌 1개를 보냅니다. requestId={lastDebugRequestId}", this);

            OnOfferResult += LogOfferOnce;
            Offer(1, lastDebugRequestId);
        }

        /// <summary>
        /// 바로 앞 봉헌과 <b>같은 requestId</b> 를 다시 보낸다. 새 Guid 를 만들지 않는다.
        ///
        /// 기대: <c>duplicate = true</c> 이고 수량이 더 줄지 않는다.
        /// </summary>
        [ContextMenu("디버그 — 같은 requestId 재요청")]
        private void DebugOfferSameRequestId()
        {
            if (string.IsNullOrEmpty(lastDebugRequestId))
            {
                Debug.LogWarning(
                    "[FakeAltarService] 다시 보낼 requestId 가 없습니다. " +
                    "\"디버그 — 1개 봉헌\" 을 먼저 실행해 주세요.", this);
                return;
            }

            Debug.Log(
                $"[FakeAltarService] 같은 requestId 로 다시 보냅니다. requestId={lastDebugRequestId} " +
                "(duplicate=true 로 와야 하고 수량이 더 줄면 안 됩니다)", this);

            OnOfferResult += LogOfferOnce;
            Offer(1, lastDebugRequestId);
        }

        private void LogOfferOnce(AltarOfferOutcome outcome)
        {
            OnOfferResult -= LogOfferOnce;

            if (!outcome.Success)
            {
                Debug.LogWarning(
                    $"[FakeAltarService] 봉헌 실패 — {outcome.Code} \"{outcome.Message}\" / {DescribeCache()}",
                    this);
                return;
            }

            Debug.Log(
                $"[FakeAltarService] 봉헌 성공 — duplicate={outcome.Duplicate}, " +
                $"offeredAmount={outcome.OfferedAmount}, requestId={outcome.RequestId} / {DescribeCache()}",
                this);
        }

        /// <summary>지금 캐시에 들어 있는 값만 문자열로 만든다. 요청하지 않는다.</summary>
        private static string DescribeCache()
        {
            return
                $"캐시 totalOffered={AltarState.TotalOffered}, " +
                $"targetOffering={AltarState.TargetOffering}, " +
                $"myFragments={AltarState.MyFragments}, " +
                $"remainingToTarget={AltarState.RemainingToTarget}, " +
                $"maxOfferAmount={AltarState.MaxOfferAmount}, " +
                $"altarActivated={AltarState.AltarActivated}, " +
                $"recoveryPercent={AltarState.RecoveryPercent} " +
                $"(PlayerInventory={PlayerInventory.SeaHeartFragment})";
        }
    }
}
