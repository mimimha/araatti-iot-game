using System.Security.Claims;
using AraAtti.Api.Auth;
using AraAtti.Api.Contracts;
using AraAtti.Api.Data;
using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

        group.MapPost("/clear-reward", ClearRewardAsync)
            .WithSummary("미니게임 보상")
            .WithDescription("이긴 판 하나에 바다의 심장 조각 1개를 준다. 같은 matchKey 는 두 번 주지 않는다. " +
                             "섬 회복이 끝났으면 주지 않지만 200 이다.");
    }

    // ------------------------------------------------------------
    // 미니게임 보상
    // ------------------------------------------------------------

    /// <summary>보상을 주는 게임. MiniGameConfig.fragmentId 와 같은 문자열이다.</summary>
    private static readonly HashSet<string> RewardGameIds = new(StringComparer.Ordinal) { "sword", "mining", "ship" };

    /// <summary>
    /// 한 사람이 보상을 받는 최소 간격.
    ///
    /// 설계 문서는 60초였다(11.4.15). 시연에서는 개발자 모드로 판을 몇 초 만에 끝내므로
    /// 60초면 두 번째 판부터 보상이 막혀 고장처럼 보인다. 판을 아무리 빨리 끝내도
    /// 10초는 넘으므로 정상 플레이는 막지 않고, 요청을 연달아 쏘는 것만 막는다.
    /// </summary>
    private static readonly TimeSpan RewardCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 이긴 판 하나에 조각 1개. 설계 11.4.3 의 순서를 따른다.
    ///
    /// <code>
    ///   ① 같은 (user_id, matchKey) 가 이미 있나          → 200 duplicate. 다시 주지 않는다
    ///   ② 섬 회복이 끝났나 (제단 행을 잠그고 읽는다)      → 200 granted:false, ALTAR_COMPLETED
    ///   ③ 방금 받았나 (쿨다운)                             → 429 REWARD_COOLDOWN
    ///   ④ 조각 +1 (UPSERT)
    ///   ⑤ 보상 기록                                        ← 중복 방지의 마지막 방어선(uk_claims_user_match)
    /// </code>
    ///
    /// ⚠ <b>클라이언트가 "이겼다" 고 하는 것을 믿는다.</b> 설계 문서가 짚은 대로 요청만 반복해 보내면
    ///    조각을 받을 수 있다(18장). 시연용이라 받아들였고, 쿨다운과 판 하나당 한 번으로만 줄인다.
    ///    근본적으로 막으려면 미니게임 서버가 대신 요청해야 한다(설계 8.3절 B안).
    ///
    /// ⚠ <b>② 에서 제단 행을 잠근다.</b> 마지막 봉헌과 동시에 오면 봉헌이 먼저 끝나 회복이 완료됐는데
    ///    보상이 또 나가는 일이 생긴다. 봉헌도 같은 행을 UPDATE 하므로 잠그면 차례대로 처리된다.
    /// </summary>
    private static async Task<IResult> ClearRewardAsync(
        ClearRewardRequest request,
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out ulong userId))
        {
            return TokenInvalid();
        }

        string gameId = request.GameId?.Trim() ?? string.Empty;
        if (!RewardGameIds.Contains(gameId))
        {
            return Results.Json(
                new ErrorResponse("GAME_ID_INVALID", "보상을 주는 게임이 아닙니다."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        string matchKey = request.MatchKey?.Trim() ?? string.Empty;
        if (matchKey.Length is 0 or > 128)
        {
            return Results.Json(
                new ErrorResponse("MATCH_KEY_INVALID", "판을 가리키는 값이 비어 있거나 너무 깁니다."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // ── ① 이미 받은 판인가 — 트랜잭션 밖에서 값싸게 거른다 ──────────────
        //    이것만으로는 부족하다. 정말 동시에 온 두 요청은 둘 다 "없음" 을 본다.
        //    최후의 방어선은 uk_claims_user_match 이고, ⑤ 에서 그 예외를 잡는다.
        if (await HasClaimAsync(database, userId, matchKey, cancellationToken))
        {
            return await ClaimedAsync(database, userId, duplicate: true, cancellationToken);
        }

        await using IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        // ── ② 섬 회복이 끝났나 — 제단 행을 잠그고 읽는다 ────────────────────
        AltarState? altar = await database.AltarStates
            .FromSqlInterpolated($"SELECT * FROM altar_state WHERE id = {AltarEndpoints.AltarStateId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (altar is not null && altar.TotalOffered >= altar.TargetOffering)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Ok(new ClearRewardResponse(
                Success: true,
                Granted: false,
                Quantity: await ReadFragmentsAsync(database, userId, cancellationToken),
                Duplicate: false,
                Code: "ALTAR_COMPLETED"));
        }

        // ── ③ 방금 받았나 ─────────────────────────────────────────────────
        DateTime now = DateTime.UtcNow;
        DateTime since = now - RewardCooldown;
        bool tooSoon = await database.RewardClaims
            .AsNoTracking()
            .AnyAsync(claim => claim.UserId == userId && claim.ClaimedAt > since, cancellationToken);

        if (tooSoon)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Json(
                new ErrorResponse("REWARD_COOLDOWN", "보상을 방금 받았습니다. 잠시 뒤에 다시 시도해 주세요."),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        // ── ④ 조각 +1 — 행이 없으면 만들고, 있으면 더한다 ────────────────────
        //    읽고 더해서 쓰지 않는다. 같은 사람의 두 요청이 겹치면 하나가 사라진다.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO player_inventories (user_id, item_id, quantity, updated_at)
               VALUES ({userId}, {SeaHeartFragmentItemId}, 1, {now})
               ON DUPLICATE KEY UPDATE quantity = quantity + 1, updated_at = {now}",
            cancellationToken);

        // ── ⑤ 보상 기록 — 같은 판을 두 번 주지 않는 마지막 방어선 ────────────
        RewardClaim claim = new()
        {
            UserId = userId,
            GameId = gameId,
            MatchKey = matchKey,
            ClaimedAt = now
        };

        database.RewardClaims.Add(claim);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (AltarEndpoints.IsDuplicateKeyViolation(exception))
        {
            // 그 사이 같은 (user_id, matchKey) 가 먼저 커밋됐다. ④ 의 +1 도 함께 되돌아간다.
            await transaction.RollbackAsync(cancellationToken);
            database.Entry(claim).State = EntityState.Detached;

            if (!await HasClaimAsync(database, userId, matchKey, cancellationToken))
            {
                // 우리 멱등성 키가 아닌 다른 중복이다. 숨기지 않고 그대로 올린다.
                throw;
            }

            return await ClaimedAsync(database, userId, duplicate: true, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await ClaimedAsync(database, userId, duplicate: false, cancellationToken);
    }

    private static Task<bool> HasClaimAsync(
        AraAttiDbContext database, ulong userId, string matchKey, CancellationToken cancellationToken)
    {
        return database.RewardClaims
            .AsNoTracking()
            .AnyAsync(claim => claim.UserId == userId && claim.MatchKey == matchKey, cancellationToken);
    }

    /// <summary>지급했다(이번에 줬거나, 전에 같은 판으로 이미 줬다). 수량은 <b>지금</b> 값이다.</summary>
    private static async Task<IResult> ClaimedAsync(
        AraAttiDbContext database, ulong userId, bool duplicate, CancellationToken cancellationToken)
    {
        return Results.Ok(new ClearRewardResponse(
            Success: true,
            Granted: true,
            Quantity: await ReadFragmentsAsync(database, userId, cancellationToken),
            Duplicate: duplicate,
            Code: null));
    }

    private static async Task<uint> ReadFragmentsAsync(
        AraAttiDbContext database, ulong userId, CancellationToken cancellationToken)
    {
        return await database.PlayerInventoryItems
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.ItemId == SeaHeartFragmentItemId)
            .Select(item => (uint?)item.Quantity)
            .SingleOrDefaultAsync(cancellationToken) ?? 0u;
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
