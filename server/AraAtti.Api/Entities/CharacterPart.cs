namespace AraAtti.Api.Entities;

/// <summary>
/// 캐릭터가 한 슬롯에 입고 있는 파츠 하나.
///
/// 파츠를 characters 의 JSON 컬럼으로 넣지 않고 따로 테이블로 둔 이유
///   - 슬롯이 늘어나도(모자 · 안경 · 장갑 …) 서버를 고칠 필요가 없다
///   - (character_id, slot) UNIQUE 로 한 슬롯에 두 개가 들어가는 것을 DB 가 막아준다
///
/// Unity 쪽 저장 형식과 필드 이름이 같습니다.
/// (Assets/Game/Scripts/Character/CharacterAppearanceSnapshot.cs 의 CharacterPartSnapshot)
/// 그래서 서버 연동 단계에서 이름을 바꿔 옮기는 코드가 필요 없습니다.
/// </summary>
public class CharacterPart
{
    public ulong Id { get; set; }

    public ulong CharacterId { get; set; }

    /// <summary>
    /// 어느 칸인지. Unity 의 카테고리 이름을 그대로 씁니다.
    /// "Face" / "Hair" / "Top" / "Bottom" / "Shoes" / "Accessory"
    ///
    /// 문자열이라서 Unity 에 새 카테고리가 생겨도 DB 를 바꾸지 않습니다.
    /// </summary>
    public string Slot { get; set; } = string.Empty;

    /// <summary>
    /// 파츠 프리팹의 이름. 예: "Costume_14_01"
    ///
    /// ⚠ 배열 인덱스를 저장하지 않습니다. 인덱스는 Unity Inspector 의 배열 순서라서
    ///    에셋을 정렬하면 저장된 모든 캐릭터의 외형이 밀립니다.
    /// </summary>
    public string PrefabName { get; set; } = string.Empty;

    public Character? Character { get; set; }
}
