namespace AraAtti.Api.Entities;

/// <summary>
/// 캐릭터 하나. 계정 하나에 여러 개가 달릴 수 있습니다. (1:N)
/// </summary>
public class Character
{
    public ulong Id { get; set; }

    /// <summary>
    /// 주인 계정.
    ///
    /// ⚠ 이 컬럼에 UNIQUE 를 걸지 않습니다.
    ///    걸어 버리면 계정당 1개가 DB 에 못박히고, 다중 캐릭터를 붙일 때
    ///    인덱스를 떼는 마이그레이션이 필요해집니다.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// 게임에서 보이는 이름. 2~10자.
    ///
    /// Unity 의 검증 규칙과 같게 맞춥니다.
    /// (CharacterCustomizationController.GetNicknameValidationMessage)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>피부색. "#RRGGBB" 형식. 팔레트 인덱스를 저장하지 않습니다.</summary>
    public string SkinColor { get; set; } = string.Empty;

    /// <summary>
    /// 목록에서 몇 번째로 보일지. 지금은 항상 0.
    ///
    /// 나중에 CharacterSelect 화면이 생기면 순서를 정하는 데 씁니다.
    /// </summary>
    public byte SlotIndex { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }

    /// <summary>입고 있는 파츠들. 슬롯마다 하나씩.</summary>
    public List<CharacterPart> Parts { get; set; } = new();
}
