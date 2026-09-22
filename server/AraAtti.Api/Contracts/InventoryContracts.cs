namespace AraAtti.Api.Contracts;

/// <summary>
/// 가지고 있는 아이템 한 종류.
/// </summary>
/// <param name="ItemId">서버 DB 의 item_id 와 같은 문자열. Unity 의 ItemIds 상수와 맞춘다.</param>
/// <param name="DisplayName">화면에 그대로 띄우는 한국어 이름. DB 에 없고 서버가 붙인다.</param>
/// <param name="Quantity">player_inventories.quantity. int unsigned 라 음수가 없다.</param>
public sealed record InventoryItemResponse(string ItemId, string DisplayName, uint Quantity);

/// <summary>
/// 내 인벤토리.
///
/// ⚠ 아이템이 한 종류뿐이어도 **반드시 배열**로 내보낸다.
///    아이템이 늘어날 때 API 모양을 바꾸지 않기 위해서다.
///    가진 것이 없으면 404 가 아니라 빈 배열이다.
///    (CharacterListResponse 가 같은 이유로 같은 모양이다)
/// </summary>
public sealed record InventoryResponse(InventoryItemResponse[] Items);
