namespace AraAtti.Api.Entities;

/// <summary>
/// 한 계정이 가진 아이템 한 종류의 수량.
///
/// 테이블 모양은 Data/AraAttiDbContext.cs 에서 정합니다.
///
/// ⚠ 캐릭터가 아니라 <b>계정</b>에 매답니다. (설계 결정 #6 — 설계 문서 15.4절)
///    캐릭터가 여러 개가 되어도 조각은 계정 하나가 공유합니다.
///
/// ⚠ (user_id, item_id) 가 UNIQUE 입니다. 이 제약이 보상 지급의
///    INSERT ... ON DUPLICATE KEY UPDATE 를 성립시킵니다. (설계 문서 11.4.12)
/// </summary>
public class PlayerInventoryItem
{
    public ulong Id { get; set; }

    /// <summary>주인 계정.</summary>
    public ulong UserId { get; set; }

    /// <summary>아이템 종류. 예: "altar_fragment". 문자열이라 새 아이템이 생겨도 DB 를 바꾸지 않습니다.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>보유 수량. INT UNSIGNED 라 음수가 될 수 없습니다.</summary>
    public uint Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
