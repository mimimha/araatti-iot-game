namespace AraAtti.Api.Contracts;

/// <summary>
/// 요청으로 들어오는 파츠 하나.
///
/// 필드 이름을 Unity 의 저장 형식과 **똑같이** 맞췄다.
/// (Assets/Game/Scripts/Character/CharacterAppearanceSnapshot.cs 의 CharacterPartSnapshot)
/// 그래서 Unity 가 저장해 둔 스냅샷을 이름만 바꾸지 않고 그대로 보낼 수 있다.
///
/// 값이 빠질 수 있으므로 nullable 로 받고 검증은 엔드포인트에서 한다.
/// </summary>
public sealed record CharacterPartRequest(string? Slot, string? PrefabName);

/// <summary>응답으로 나가는 파츠 하나. 검증을 통과한 뒤이므로 값이 비어 있지 않다.</summary>
public sealed record CharacterPartResponse(string Slot, string PrefabName);

/// <summary>
/// 캐릭터 생성 요청.
///
/// ⚠ userId 를 받지 않는다. 주인은 언제나 JWT 의 sub 에서 읽는다.
///    본문으로 받으면 남의 계정에 캐릭터를 만들 수 있다.
/// </summary>
public sealed record CreateCharacterRequest(
    string? Nickname,
    string? SkinColor,
    CharacterPartRequest?[]? Parts);

/// <summary>캐릭터 하나.</summary>
/// <param name="Nickname">엔티티에서는 Character.Name 이지만, Unity 와 맞추려고 nickname 으로 내보낸다.</param>
/// <param name="SlotIndex">목록에서의 순서. 지금은 항상 0.</param>
public sealed record CharacterResponse(
    ulong Id,
    string Nickname,
    string SkinColor,
    int SlotIndex,
    CharacterPartResponse[] Parts,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// 캐릭터 목록.
///
/// ⚠ 지금은 계정당 1개뿐이지만 **반드시 배열**로 내보낸다.
///    나중에 CharacterSelect 화면과 다중 캐릭터를 붙일 때 API 모양을 바꾸지 않기 위해서다.
///    캐릭터가 없으면 404 가 아니라 빈 배열이다.
/// </summary>
public sealed record CharacterListResponse(CharacterResponse[] Characters);
