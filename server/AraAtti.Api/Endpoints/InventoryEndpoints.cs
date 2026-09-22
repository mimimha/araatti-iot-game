using System.Security.Claims;
using AraAtti.Api.Auth;
using AraAtti.Api.Contracts;
using AraAtti.Api.Data;
using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AraAtti.Api.Endpoints;

/// <summary>
/// 내 인벤토리 조회.
///
/// 규칙
///   - 주인은 언제나 JWT 의 sub 다. 요청의 userId 같은 것은 받지 않는다.
///   - 남의 인벤토리는 절대 보이지 않는다. 조회에 user_id 조건이 붙는다.
///   - 가진 것이 없으면 빈 배열이다. 404 가 아니다.
///
/// 문서: docs/prd/lobby_altar_inventory_system_design.md 9.2절
/// </summary>
public static class InventoryEndpoints
{
    /// <summary>
    /// 봉헌에 쓰는 재화의 id. DB 의 player_inventories.item_id 에 그대로 들어간다.
    ///
    /// ⚠ Unity 의 ItemIds.SeaHeartFragment 와 **같은 문자열**이어야 한다.
    ///    지금 이 게임의 아이템은 이것 하나뿐이라 아이템 카탈로그를 만들지 않는다.
    ///    3종 이상으로 늘어나는 것이 확정되면 그때 구조를 올린다. (설계 문서 4장)
    /// </summary>
    public const string SeaHeartFragmentItemId = "sea_heart_fragment";

    /// <summary>화면에 그대로 띄우는 이름. DB 에 컬럼을 만들지 않고 서버가 붙인다.</summary>
    public const string SeaHeartFragmentDisplayName = "바다의 심장 조각";

    public static void MapInventoryEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes
            .MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireAuthorization();

        group.MapGet("/", GetMyInventoryAsync)
            .WithSummary("내 인벤토리")
            .WithDescription("토큰 주인이 가진 아이템을 배열로 돌려준다. 없으면 빈 배열이다. 404 가 아니다.");
    }

    private static async Task<IResult> GetMyInventoryAsync(
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out ulong userId))
        {
            return TokenInvalid();
        }

        List<PlayerInventoryItem> items = await database.PlayerInventoryItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            // 저장 순서와 무관하게 언제나 같은 순서로 내보낸다.
            .OrderBy(item => item.ItemId)
            .ToListAsync(cancellationToken);

        // 행이 없으면 빈 배열이 나간다. 여기서 quantity = 0 짜리 가짜 항목을 만들지 않는다.
        InventoryItemResponse[] response = items
            .Select(item => new InventoryItemResponse(item.ItemId, DisplayNameOf(item.ItemId), item.Quantity))
            .ToArray();

        return Results.Ok(new InventoryResponse(response));
    }

    /// <summary>
    /// 화면에 띄울 이름을 붙인다.
    ///
    /// 아는 아이템이 하나뿐이라 분기도 하나다. 모르는 id 가 들어오면 id 를 그대로 돌려준다 —
    /// 화면이 비는 것보다 낫고, 이름표 하나 때문에 조회가 실패하지는 않는다.
    /// </summary>
    private static string DisplayNameOf(string itemId)
    {
        return itemId == SeaHeartFragmentItemId ? SeaHeartFragmentDisplayName : itemId;
    }

    private static IResult TokenInvalid()
    {
        return Results.Json(
            new ErrorResponse("TOKEN_INVALID", "로그인이 필요합니다. 토큰이 없거나 만료되었습니다."),
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
