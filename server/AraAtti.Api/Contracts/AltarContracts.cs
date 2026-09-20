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
