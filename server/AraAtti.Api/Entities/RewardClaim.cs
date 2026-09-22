namespace AraAtti.Api.Entities;

/// <summary>
/// 미니게임 한 판의 보상을 지급한 기록.
///
/// 두 가지 일을 합니다. (설계 문서 11.4.7 · 11.4.15)
///   1. 같은 판으로 두 번 받는 것을 막는다 — UNIQUE (user_id, match_key)
///   2. 60초 쿨다운을 잰다 — SELECT MAX(claimed_at) WHERE user_id = ?
///
/// ⚠ match_key 단독 UNIQUE 로 만들면 안 됩니다. 4인 파티가 한 판을 깨면 네 명의
///    match_key 가 전부 같아서, 가장 먼저 도착한 한 명만 받고 셋은 거절당합니다.
///    지급은 플레이어별이므로 키도 (user_id, match_key) 여야 합니다.
/// </summary>
public class RewardClaim
{
    public ulong Id { get; set; }

    public ulong UserId { get; set; }

    /// <summary>어느 미니게임인지. "sword" / "mining" / "ship".</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>서버가 정한 판 고유값. 같은 판의 네 명은 같은 값을 씁니다.</summary>
    public string MatchKey { get; set; } = string.Empty;

    public DateTime ClaimedAt { get; set; }

    public User? User { get; set; }
}
