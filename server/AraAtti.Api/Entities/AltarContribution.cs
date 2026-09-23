namespace AraAtti.Api.Entities;

/// <summary>
/// 봉헌 한 건의 기록.
///
/// 통계용이 아니라 <b>멱등성 장치</b>입니다. 같은 (user_id, request_id) 가 두 번 들어오면
/// UNIQUE 가 두 번째를 막아 수량이 두 번 빠지는 일을 없앱니다. (설계 문서 10.4절 · 17.1절)
///
/// ⚠ request_id 단독 UNIQUE 가 아닙니다. 요청 id 는 클라이언트가 만드는 값이라
///    다른 사람이 우연히 같은 값을 쓰면 남의 봉헌이 막힙니다.
/// </summary>
public class AltarContribution
{
    public ulong Id { get; set; }

    /// <summary>
    /// 클라이언트가 요청마다 새로 만드는 값. 재시도할 때는 같은 값을 보냅니다.
    ///
    /// 문자열이 아니라 Guid 인 이유는 두 가지입니다.
    ///   1. 설계의 검증 6 이 이미 "Guid 문자열" 을 요구한다 (설계 문서 8.6절)
    ///   2. Pomelo 는 char(36) 을 Guid 전용 저장 형식으로 잡아 둔다.
    ///      string 으로 두고 char(36) 을 지정하면 모델 생성이 깨진다.
    /// DB 컬럼은 설계대로 char(36) 그대로입니다.
    /// </summary>
    public Guid RequestId { get; set; }

    public ulong UserId { get; set; }

    /// <summary>이 요청으로 봉헌한 양.</summary>
    public uint Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
