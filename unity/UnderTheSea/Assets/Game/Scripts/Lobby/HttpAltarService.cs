using System;
using System.Collections;
using UnityEngine;
using UnderTheSea.Account;
using UnderTheSea.Inventory;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 실제 서버와 통신하는 제단 서비스.
    ///
    /// 서버 규격
    ///   GET  /api/altar/state   Bearer  → 200, 상태 9개 필드
    ///   POST /api/altar/offer   Bearer  → 200 성공(duplicate 포함) · 409 실패(+최신 상태) · 400 입력 오류
    ///
    /// ⚠ 이 클래스는 <b>연출을 모른다.</b> Particle · Light · GameObject 를 건드리지 않는다.
    ///    봉헌이 성공해도 여기서 Blue VFX 를 재생하지 않는다. 성공 사건을 세션 전체에 알리고
    ///    pulse 를 재생하는 일은 STEP 8 · 10 의 몫이다. (결정 #9, 설계 12.3·12.4절)
    /// </summary>
    public class HttpAltarService : MonoBehaviour, IAltarService
    {
        [Header("서버")]
        [Tooltip("비워 두면 실행 인자 -api 를 쓰고, 그것도 없으면 http://localhost:5080 을 쓴다.")]
        [SerializeField] private string baseUrl = string.Empty;

        [Header("주기 동기화")]
        [Tooltip("다른 채널의 봉헌과 유실된 RPC 를 따라잡는 간격(초). 설계 권장값은 30초다.")]
        [SerializeField, Range(5f, 300f)] private float statePollIntervalSeconds = 30f;

        [Header("로그")]
        [Tooltip("켜면 요청한 메서드 · 경로 · 상태 코드를 Console 에 남긴다. 토큰은 찍지 않는다.")]
        [SerializeField] private bool verboseLogging = false;

        public event Action<bool, AltarStateDto, string> OnStateResult;
        public event Action<AltarOfferOutcome> OnOfferResult;

        private Coroutine pollingRoutine;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AccountServiceLocator.Register(this);
            Debug.Log($"[HttpAltarService] 실제 서버 제단 상태를 사용합니다. ({ResolvedBaseUrl})", this);
        }

        private void OnDestroy()
        {
            StopStatePolling();
            AccountServiceLocator.Unregister(this);
        }

        private string ResolvedBaseUrl =>
            string.IsNullOrWhiteSpace(baseUrl) ? HttpApiConfig.EffectiveBaseUrl : baseUrl.Trim().TrimEnd('/');

        /// <summary>지금 쓸 수 있는 토큰. 로그인 전이면 빈 문자열.</summary>
        private static string AccessToken
        {
            get
            {
                IAuthService auth = AccountServiceLocator.Auth;
                return auth != null ? auth.AccessToken : string.Empty;
            }
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
            string token = AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                FinishState(false, null, "로그인이 필요합니다.");
                yield break;
            }

            int sequence = AltarState.IssueSequence();

            string url = HttpApiConfig.Combine(ResolvedBaseUrl, HttpApiConfig.AltarStatePath);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "GET", null, token, r => result = r);

            Log("GET", HttpApiConfig.AltarStatePath, result);

            if (!result.IsSuccess)
            {
                // ⚠ 조회 실패 때문에 캐시를 비우지 않는다. 마지막으로 알던 값을 유지한다.
                FinishState(false, null, result.FailureMessage);
                yield break;
            }

            if (!HttpJson.TryParse(result.Body, out AltarStateDto snapshot, out string parseFailure))
            {
                FinishState(false, null, parseFailure);
                yield break;
            }

            AltarState.ApplyStateSnapshot(sequence, snapshot);

            FinishState(true, snapshot, string.Empty);
        }

        /// <summary>
        /// ⚠ <see cref="AltarState.RequestFinished"/> 를 성공·실패 양쪽에서 반드시 부른다.
        ///    빼먹으면 이후 조회가 "이미 묻고 있다" 로 영영 막힌다.
        /// </summary>
        private void FinishState(bool success, AltarStateDto snapshot, string failureMessage)
        {
            AltarState.RequestFinished();
            OnStateResult?.Invoke(success, snapshot, failureMessage);
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
            string token = AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                OnOfferResult?.Invoke(new AltarOfferOutcome(
                    false, false, 0, requestId, "TOKEN_INVALID", "로그인이 필요합니다.", false));
                yield break;
            }

            // 봉헌이 도는 동안에는 새 조회를 내보내지 않는다. 버려질 응답을 만들 뿐이고,
            // 봉헌 응답 자체가 더 새로운 권위 값을 싣고 온다.
            AltarState.BeginMutation();

            int sequence = AltarState.IssueSequence();

            // ⚠ requestId 를 여기서 만들지 않는다. 호출자가 준 값을 그대로 보낸다.
            string body = JsonUtility.ToJson(new OfferRequestBody { amount = amount, requestId = requestId });

            string url = HttpApiConfig.Combine(ResolvedBaseUrl, HttpApiConfig.AltarOfferPath);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "POST", body, token, r => result = r);

            Log("POST", HttpApiConfig.AltarOfferPath, result);

            // 성공이든 409 든 본문에 최신 상태가 실려 온다. 같은 자리에서 읽는다.
            AltarOfferBody parsed = null;
            if (!string.IsNullOrWhiteSpace(result.Body))
            {
                HttpJson.TryParse(result.Body, out parsed, out _);
            }

            bool applied = false;

            // ⚠ 상태가 실제로 실려 있을 때만 적용한다.
            //    400(AMOUNT_INVALID 등)은 code · message 만 오므로 숫자가 전부 0 이다.
            //    그것을 적용하면 캐시가 0 / 0 으로 망가진다.
            //    서버는 target_offering > 0 을 DB CHECK 로 보장하므로 이 값이 판별 기준이 된다.
            if (parsed != null && parsed.targetOffering > 0)
            {
                AltarState.ApplyOfferSnapshot(
                    sequence,
                    parsed.totalOffered,
                    parsed.targetOffering,
                    parsed.remainingToTarget,
                    parsed.remainingFragments,
                    parsed.maxOfferAmount,
                    parsed.altarActivated,
                    parsed.recoveryPercent);

                applied = true;
            }

            AltarOfferOutcome outcome = result.IsSuccess
                ? new AltarOfferOutcome(
                    true,
                    parsed != null && parsed.duplicate,
                    parsed != null ? parsed.offeredAmount : 0,
                    requestId,
                    string.Empty,
                    string.Empty,
                    applied)
                : new AltarOfferOutcome(
                    false,
                    false,
                    0,
                    requestId,
                    parsed != null ? parsed.code : string.Empty,
                    result.FailureMessage,
                    applied);

            // 결과를 먼저 알린다. 화면이 이 안에서 RequestRefresh() 를 불러도
            // 아직 봉헌 중이라 미뤄졌다가 아래 EndMutation 에서 한 번만 나간다.
            OnOfferResult?.Invoke(outcome);

            AltarState.EndMutation(applied);
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

        /// <summary>
        /// 저빈도 상태 동기화. 다른 채널의 봉헌과 유실된 RPC 를 따라잡는다. (설계 12.7절)
        ///
        /// ⚠ 로그인 전에는 요청하지 않는다. 토큰 없이 때리면 401 만 쌓인다.
        /// ⚠ 이 루프는 상태만 갱신한다. 여기서 Blue VFX 가 나가면 실패다. (결정 #9)
        /// </summary>
        private IEnumerator PollStateRoutine()
        {
            WaitForSeconds interval = new WaitForSeconds(statePollIntervalSeconds);

            while (true)
            {
                yield return interval;

                if (string.IsNullOrEmpty(AccessToken))
                {
                    continue;
                }

                // 중복 요청 합치기와 봉헌 중 차단은 AltarState 가 처리한다.
                AltarState.RequestRefresh();
            }
        }

        // ------------------------------------------------------------
        // 본문
        // ------------------------------------------------------------

        // ------------------------------------------------------------
        // 손으로 확인하는 길 (UI 가 아직 없다)
        //
        // 플레이 모드에서 "AccountService (Http)" 오브젝트를 고르고, 이 컴포넌트의
        // 톱니바퀴 메뉴에서 실행한다.
        // ------------------------------------------------------------

        [Header("디버그 봉헌")]
        [Tooltip("아래 \"디버그 — 봉헌\" 메뉴로 보낼 수량. 실제 DB 가 바뀐다.")]
        [SerializeField] private long debugOfferAmount = 1;

        /// <summary>읽기만 한다. DB 를 바꾸지 않는다.</summary>
        [ContextMenu("디버그 — 제단 상태 조회")]
        private void DebugRequestState()
        {
            AltarState.RequestRefresh();
            Debug.Log("[HttpAltarService] 제단 상태 조회를 요청했습니다. " +
                      "결과는 AltarState 에 반영됩니다.", this);
        }

        /// <summary>지금 캐시에 들어 있는 값만 찍는다. 요청하지 않는다.</summary>
        [ContextMenu("디버그 — 캐시 값 보기")]
        private void DebugDumpCache()
        {
            Debug.Log(
                $"[HttpAltarService] 캐시 — {AltarState.TotalOffered}/{AltarState.TargetOffering}, " +
                $"남은 칸 {AltarState.RemainingToTarget}, 내 조각 {AltarState.MyFragments}, " +
                $"최대 봉헌 {AltarState.MaxOfferAmount}, 완료={AltarState.AltarActivated}, " +
                $"회복도 {AltarState.RecoveryPercent}%, 내 기여 {AltarState.MyOfferedTotal}, " +
                $"updatedAt={AltarState.UpdatedAt}", this);
        }

        /// <summary>
        /// ⚠ <b>실제 DB 가 바뀐다.</b> 사람이 메뉴를 눌렀을 때만 실행된다. 자동으로 돌지 않는다.
        ///
        /// 여기서 requestId 를 만드는 것은 이 메서드가 <b>호출자 역할</b>이기 때문이다.
        /// 서비스가 요청을 보내면서 스스로 만드는 것과는 다르다 — 그렇게 하면 재시도 때
        /// 값이 바뀌어 서버의 중복 방지가 무력해진다. (설계 10.4·10.5절)
        /// </summary>
        [ContextMenu("디버그 — 봉헌 (실제 DB 가 바뀝니다)")]
        private void DebugOffer()
        {
            string requestId = Guid.NewGuid().ToString();
            Debug.LogWarning(
                $"[HttpAltarService] 디버그 봉헌 {debugOfferAmount}개를 보냅니다. requestId={requestId}", this);

            if (!verboseLogging)
            {
                Debug.Log(
                    "[HttpAltarService] HTTP 상태 코드까지 보려면 이 컴포넌트의 \"로그\" 칸을 켜세요. " +
                    "켜면 \"POST /api/altar/offer → 실패 (HTTP 409)\" 줄이 함께 찍힙니다.", this);
            }

            OnOfferResult += LogOfferOnce;
            Offer(debugOfferAmount, requestId);
        }

        /// <summary>
        /// 봉헌 결과가 한 번 오면 찍고 스스로 구독을 뗀다.
        ///
        /// ⚠ 여기서 JSON 을 다시 파싱하지 않고 상태를 다시 조회하지도 않는다.
        ///    아래 값은 전부 <b>이미 끝난 처리의 산물</b>이다.
        ///
        /// <code>
        ///   POST /api/altar/offer
        ///     → HttpJson 이 non-2xx 본문을 보존
        ///     → OfferRoutine 이 그 본문을 파싱 (code · 최신 상태)
        ///     → 409 스냅샷을 캐시에 적용
        ///     → 이 콜백
        ///     → 아래 로그
        /// </code>
        ///
        /// 그래서 <c>code</c> 가 찍힌다는 것 자체가 **실패 본문이 보존되고 파싱되었다는 증거**다.
        /// <see cref="HttpJsonResult.FailureMessage"/> 만으로는 <c>message</c> 밖에 알 수 없다.
        /// </summary>
        private void LogOfferOnce(AltarOfferOutcome outcome)
        {
            OnOfferResult -= LogOfferOnce;

            string line =
                $"[HttpAltarService] 봉헌 응답 — success={outcome.Success}, " +
                $"code=\"{outcome.Code}\", message=\"{outcome.Message}\", " +
                $"requestId={outcome.RequestId}, duplicate={outcome.Duplicate}, " +
                $"offeredAmount={outcome.OfferedAmount}, " +
                $"서버 상태 적용={outcome.AppliedServerState}\n" +
                $"  AltarState — TotalOffered={AltarState.TotalOffered}, " +
                $"TargetOffering={AltarState.TargetOffering}, " +
                $"MyFragments={AltarState.MyFragments}, " +
                $"RemainingToTarget={AltarState.RemainingToTarget}, " +
                $"MaxOfferAmount={AltarState.MaxOfferAmount}, " +
                $"AltarActivated={AltarState.AltarActivated}, " +
                $"RecoveryPercent={AltarState.RecoveryPercent}\n" +
                $"  PlayerInventory — SeaHeartFragment={PlayerInventory.SeaHeartFragment}";

            if (outcome.Success)
            {
                Debug.Log(line, this);
            }
            else
            {
                Debug.LogWarning(line, this);
            }
        }

        [Serializable]
        private class OfferRequestBody
        {
            public long amount;
            public string requestId;
        }

        /// <summary>
        /// POST /api/altar/offer 의 응답. <b>성공과 실패를 한 타입으로 읽는다.</b>
        ///
        /// JsonUtility 는 본문에 없는 필드를 기본값으로 둔다. 그래서 성공 응답에는 code 가 비고,
        /// 409 응답에는 offeredAmount · duplicate 가 비어 있게 된다. 둘 다 정상이다.
        /// </summary>
#pragma warning disable 0649
        [Serializable]
        private class AltarOfferBody
        {
            public bool success;
            public bool duplicate;
            public long offeredAmount;
            public long remainingFragments;
            public long totalOffered;
            public long targetOffering;
            public long remainingToTarget;
            public long maxOfferAmount;
            public bool altarActivated;
            public float recoveryPercent;
            public string code;
            public string message;
        }
#pragma warning restore 0649

        /// <summary>⚠ 토큰은 절대 찍지 않는다. 메서드 · 경로 · 상태 코드만 남긴다.</summary>
        private void Log(string method, string path, HttpJsonResult result)
        {
            if (!verboseLogging)
            {
                return;
            }

            Debug.Log(
                $"[HttpAltarService] {method} {path} → " +
                $"{(result.IsSuccess ? "성공" : "실패")} (HTTP {result.StatusCode})", this);
        }
    }
}
