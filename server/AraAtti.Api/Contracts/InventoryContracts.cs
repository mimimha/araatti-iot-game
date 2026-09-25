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

/// <summary>
/// 미니게임을 이긴 보상을 달라는 요청.
///
/// ⚠ <b>수량이 없다.</b> 한 판에 언제나 1개이고 서버가 정한다. 클라이언트가 수량을 보낼 여지를 없앤다.
/// </summary>
/// <param name="GameId">어느 게임인가. <c>sword</c> · <c>mining</c> · <c>ship</c> (MiniGameConfig.fragmentId)</param>
/// <param name="MatchKey">판 하나를 가리키는 문자열. 같은 값은 두 번 지급하지 않는다. 128자 이하.</param>
public sealed record ClearRewardRequest(string? GameId, string? MatchKey);

/// <summary>
/// 보상 결과. <b>지급하지 않았을 때도 200</b> 이다 — 게임을 이긴 것 자체는 정상이다.
/// </summary>
/// <param name="Success">요청을 처리했는가. 이 응답에서는 늘 true.</param>
/// <param name="Granted">조각을 줬는가(이번에 줬거나, 전에 같은 판으로 이미 줬거나).</param>
/// <param name="Quantity">지금 가진 조각 수. 화면이 그대로 받아 그린다.</param>
/// <param name="Duplicate">같은 판으로 이미 받은 요청을 다시 받았는가.</param>
/// <param name="Code">지급하지 않은 이유. 줬으면 null. 예: <c>ALTAR_COMPLETED</c></param>
public sealed record ClearRewardResponse(bool Success, bool Granted, uint Quantity, bool Duplicate, string? Code);
