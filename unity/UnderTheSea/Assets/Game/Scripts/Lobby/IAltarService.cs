using System;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// GET /api/altar/state 의 응답. 필드 이름이 서버와 같아서 JsonUtility 가 그대로 채운다.
    ///
    /// (server/AraAtti.Api/Contracts/AltarContracts.cs 의 AltarStateResponse)
    ///
    /// ⚠ 정수는 전부 long 이다. 서버는 BIGINT UNSIGNED · INT UNSIGNED 를 쓰는데,
    ///    JsonUtility 가 확실히 지원하고 그 범위를 담을 수 있는 정수 타입이 long 이다.
    ///    수량을 float 로 담지 않는다. recoveryPercent 만 부동소수 값이다.
    /// </summary>
    // JsonUtility 가 응답을 읽어 채우는 필드들이다. 코드에서 대입하는 곳이 없으므로
    // "값이 대입되지 않았다"(CS0649) 경고가 나는데, 여기서는 정상이라 끈다.
#pragma warning disable 0649
    [Serializable]
    public class AltarStateDto
    {
        public long totalOffered;
        public long targetOffering;
        public long remainingToTarget;
        public long myFragments;
        public long maxOfferAmount;
        public bool altarActivated;
        public float recoveryPercent;
        public long myOfferedTotal;
        public string updatedAt;

        /// <summary>
        /// 봉헌량이 목표에 처음 닿은 시각(UTC 문자열). 목표 아래면 null/빈 문자열.
        /// "이번 완성" 의 번호다 — 완성 영상(<see cref="AltarCompletionVideo"/>)이 한 번씩만 틀려고 본다.
        /// </summary>
        public string activatedAt;
    }
#pragma warning restore 0649

    /// <summary>
    /// 봉헌 한 번의 결과. 성공·실패·중복을 한 타입으로 돌려준다.
    ///
    /// ⚠ <see cref="Duplicate"/> 를 <b>버리지 않는다.</b> 나중에 봉헌 성공 event 와 Blue VFX 를
    ///    붙일 때 "이 논리적 요청이 이미 처리됐는가" 를 판단할 재료다. (결정 #9, 설계 10.4절)
    ///
    /// ⚠ 이 계층은 VFX 를 판단하지 않는다. <c>duplicate == true</c> 라고 해서
    ///    "연출 없음" 으로 단정하면 안 된다 — 첫 응답이 유실된 재시도라면 클라이언트가
    ///    <b>처음</b> 확인하는 성공일 수 있다. 판단은 후속 event 계층의 몫이다.
    /// </summary>
    public sealed class AltarOfferOutcome
    {
        /// <summary>서버가 봉헌을 인정했는지. 409 · 400 · 네트워크 실패면 false.</summary>
        public bool Success { get; }

        /// <summary>
        /// 같은 (사용자, requestId) 를 서버가 이미 처리했는지. 실패면 언제나 false.
        ///
        /// ⚠ <c>true</c> 도 HTTP 성공이다. 오류로 다루지 않는다.
        /// </summary>
        public bool Duplicate { get; }

        /// <summary>실제로 반영된 수량. 중복이면 처음 기록된 수량이다. 실패면 0.</summary>
        public long OfferedAmount { get; }

        /// <summary>이 결과가 어느 요청의 것인지. <b>호출자가 준 값을 그대로 돌려준다.</b></summary>
        public string RequestId { get; }

        /// <summary>
        /// 실패 사유 코드. 성공이면 빈 문자열.
        ///
        /// NOT_ENOUGH_FRAGMENTS · OFFERING_AMOUNT_CHANGED · OFFERING_CLOSED ·
        /// AMOUNT_INVALID · AMOUNT_TOO_LARGE · REQUEST_ID_INVALID · TOKEN_INVALID
        /// </summary>
        public string Code { get; }

        /// <summary>화면에 그대로 보여줄 수 있는 한국어 문구. 성공이면 빈 문자열.</summary>
        public string Message { get; }

        /// <summary>
        /// 서버가 준 최신 상태를 캐시에 반영했는지.
        ///
        /// 409 실패에도 최신 상태가 실려 오므로 보통 실패에서도 true 다.
        /// 연결 실패나 400(입력 오류)처럼 상태가 없는 응답에서만 false 다.
        /// </summary>
        public bool AppliedServerState { get; }

        public AltarOfferOutcome(
            bool success,
            bool duplicate,
            long offeredAmount,
            string requestId,
            string code,
            string message,
            bool appliedServerState)
        {
            Success = success;
            Duplicate = duplicate;
            OfferedAmount = offeredAmount;
            RequestId = requestId;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            AppliedServerState = appliedServerState;
        }
    }

    /// <summary>
    /// 제단 API 의 경계.
    ///
    /// ⚠ 이 인터페이스는 <b>요청을 보내고 결과를 알리는 일만</b> 한다.
    ///    받은 값을 캐시에 넣는 것은 <see cref="AltarState"/> 이고, 화면에 그리는 것은 UI 다.
    ///    구현체가 Particle · Light · GameObject 를 건드리지 않는다. (설계 12.3절)
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 9.2 · 10 · 12.7절
    /// </summary>
    public interface IAltarService
    {
        /// <summary>
        /// GET /api/altar/state. 결과는 <see cref="OnStateResult"/> 로 온다.
        ///
        /// ⚠ 화면은 보통 이것을 직접 부르지 않고 <see cref="AltarState.RequestRefresh"/> 를 부른다.
        ///    그쪽이 중복 요청 합치기와 봉헌 중 차단을 함께 처리한다.
        /// </summary>
        void RequestState();

        /// <summary>
        /// POST /api/altar/offer. 결과는 <see cref="OnOfferResult"/> 로 온다.
        /// </summary>
        /// <param name="amount">봉헌할 수량. 1 이상.</param>
        /// <param name="requestId">
        /// 이 논리적 봉헌의 식별자(Guid 문자열). <b>호출자가 만들어서 넘긴다.</b>
        ///
        /// ⚠ 서비스가 여기서 새로 만들지 않는다. 재전송할 때 같은 값을 보내야
        ///    서버가 중복으로 걸러 주는데, 서비스가 매번 새로 만들면 그 보호가 사라진다.
        ///    버튼을 누른 순간 한 번 만들고, 타임아웃 재시도에도 그 값을 유지한다. (설계 10.4·10.5절)
        /// </param>
        void Offer(long amount, string requestId);

        /// <summary>
        /// 저빈도 상태 동기화를 시작한다. (기본 30초)
        ///
        /// 다른 채널의 봉헌과 유실된 RPC 를 따라잡는 용도다. (설계 12.7절)
        ///
        /// ⚠ <b>자동으로 시작하지 않는다.</b> 로비에 있는 동안만 돌아야 하므로 로비 쪽 컴포넌트가
        ///    켜고 끈다. 미니게임 씬에서는 돌지 않는 것이 설계 전제다.
        /// ⚠ 이 폴링은 <b>상태 전용</b>이다. 폴링 때문에 Blue VFX 가 발생하면 안 된다. (결정 #9)
        /// </summary>
        void StartStatePolling();

        /// <summary>주기 동기화를 멈춘다. 로비를 떠날 때 부른다.</summary>
        void StopStatePolling();

        /// <summary>조회 결과. (성공 여부, 받은 상태 또는 null, 실패했다면 그 이유)</summary>
        event Action<bool, AltarStateDto, string> OnStateResult;

        /// <summary>봉헌 결과. 성공·중복·실패가 모두 이 이벤트로 온다.</summary>
        event Action<AltarOfferOutcome> OnOfferResult;
    }
}
