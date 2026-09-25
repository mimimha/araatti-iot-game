namespace AraAtti.Api.Contracts;

/// <summary>
/// 제단의 현재 상태 + 나의 상태.
///
/// 파생값(RemainingToTarget · MaxOfferAmount · AltarActivated · RecoveryPercent)을
/// **서버가 계산해서 넣습니다.** 클라이언트가 같은 공식을 또 쓰면 두 군데가 어긋납니다.
/// (설계 문서 9.2 · 11.1)
///
/// ⚠ 이 응답은 그 순간의 스냅샷입니다. 봉헌 시점에 서버가 다시 검증합니다.
/// </summary>
/// <param name="TotalOffered">altar_state.total_offered. 모두가 지금까지 봉헌한 총량.</param>
/// <param name="TargetOffering">altar_state.target_offering. 1000 을 코드에 박지 않는다.</param>
/// <param name="RemainingToTarget">아직 받을 수 있는 양. max(0, target - total).</param>
/// <param name="MyFragments">내 sea_heart_fragment 보유량. 행이 없으면 0.</param>
/// <param name="MaxOfferAmount">이번에 봉헌할 수 있는 최대. min(MyFragments, RemainingToTarget).</param>
/// <param name="AltarActivated">total &gt;= target. activated_at 으로 판단하지 않는다 (10.7).</param>
/// <param name="RecoveryPercent">min(total / target, 1) * 100. 100 을 넘지 않는다.</param>
/// <param name="MyOfferedTotal">내가 지금까지 봉헌한 합계. 전체 TotalOffered 와 다르다.</param>
/// <param name="UpdatedAt">altar_state.updated_at. UTC.</param>
public sealed record AltarStateResponse(
    ulong TotalOffered,
    uint TargetOffering,
    ulong RemainingToTarget,
    uint MyFragments,
    ulong MaxOfferAmount,
    bool AltarActivated,
    float RecoveryPercent,
    ulong MyOfferedTotal,
    DateTime UpdatedAt);

/// <summary>
/// 봉헌 요청.
///
/// ⚠ userId 를 받지 않는다. 주인은 언제나 JWT 의 sub 다.
///
/// 두 값 모두 nullable 로 받고 검증은 엔드포인트에서 한다. JSON 에서 통째로 빠질 수 있고,
/// 그때 "0 을 보냈다" 와 "안 보냈다" 를 구분해 우리 문구로 답하기 위해서다.
/// (AuthContracts · CharacterContracts 가 같은 이유로 같은 모양이다)
/// </summary>
/// <param name="Amount">
/// 봉헌할 수량. <b>int 가 아니라 long 으로 받는다.</b>
/// int 로 받으면 int.MaxValue 를 넘는 숫자가 핸들러에 닿기도 전에 역직렬화 400 이 되어,
/// 설계 8.6절 5번의 AMOUNT_TOO_LARGE 를 우리가 돌려줄 수 없다.
/// uint 로 받으면 반대로 음수를 잃어 AMOUNT_INVALID 를 구분할 수 없다.
/// </param>
/// <param name="RequestId">
/// 요청마다 새로 만드는 Guid 문자열. 재시도할 때는 같은 값을 보낸다.
/// 문자열로 받아 Guid.TryParse 로 검증한다. Guid 로 직접 받으면 형식 오류가
/// 역직렬화 400 이 되어 REQUEST_ID_INVALID 를 돌려줄 수 없다.
/// </param>
public sealed record AltarOfferRequest(long? Amount, string? RequestId);

/// <summary>
/// 봉헌 성공 응답. 멱등 재수신(duplicate = true)도 이 모양이다.
///
/// ⚠ acceptedAmount · refundedAmount 같은 필드는 없다. 부분 수락을 하지 않으므로
///    성공했다면 반영된 수량은 언제나 요청한 수량이다. (설계 10.3.2)
///
/// 상태값은 전부 <b>지금 시점</b>의 값이다. 재시도 사이에 다른 사람이 봉헌했을 수 있고,
/// 클라이언트가 그려야 하는 것은 과거가 아니라 현재다. (설계 10.4)
/// </summary>
public sealed record AltarOfferResponse(
    bool Success,
    uint OfferedAmount,
    uint RemainingFragments,
    ulong TotalOffered,
    uint TargetOffering,
    ulong RemainingToTarget,
    ulong MaxOfferAmount,
    bool AltarActivated,
    float RecoveryPercent,
    bool Duplicate);

/// <summary>
/// 봉헌 실패 응답. 아무것도 바뀌지 않았다는 뜻이다.
///
/// ⚠ 실패에도 최신 상태를 전부 싣는다. 클라이언트가 실패 직후
///    GET /api/altar/state 를 한 번 더 부르지 않아도 UI 를 맞출 수 있어야 한다. (설계 9.2)
/// </summary>
public sealed record AltarOfferFailureResponse(
    bool Success,
    string Code,
    string Message,
    uint RemainingFragments,
    ulong TotalOffered,
    uint TargetOffering,
    ulong RemainingToTarget,
    ulong MaxOfferAmount,
    bool AltarActivated,
    float RecoveryPercent);

/// <summary>
/// 🛠 개발자 모드의 섬 회복도 조정. 로비 개발자 패널의 <c>]</c>(+1) · <c>[</c>(-1).
///
/// nullable 로 받는 이유는 <see cref="AltarOfferRequest"/> 와 같다. 빠졌을 때 우리 문구로 답한다.
/// </summary>
/// <param name="Delta">+1 또는 -1. 그 밖의 값은 400 DELTA_INVALID.</param>
public sealed record DevRecoveryRequest(int? Delta);
