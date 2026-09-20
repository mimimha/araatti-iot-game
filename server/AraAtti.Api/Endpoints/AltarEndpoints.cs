using System.Security.Claims;
using AraAtti.Api.Auth;
using AraAtti.Api.Contracts;
using AraAtti.Api.Data;
using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AraAtti.Api.Endpoints;

/// <summary>
/// 제단 상태 조회.
///
/// 규칙
///   - 전체 봉헌량은 DB 한 행(altar_state id=1)이 원본이다. 서버 메모리에 두지 않는다.
///   - 활성화 여부와 회복률은 **서버가 계산**해서 내려준다. 클라이언트가 계산하지 않는다.
///   - "나" 는 언제나 JWT 의 sub 다.
///
/// 문서: docs/prd/lobby_altar_inventory_system_design.md 9.2 · 10.7 · 11.1 · 11.3
/// </summary>
public static class AltarEndpoints
{
    /// <summary>
    /// altar_state 의 유일한 행 번호.
    ///
    /// ⚠ 이 행은 AddInventoryAndAltar 마이그레이션이 넣어 둔 것이다.
    ///    조회하다가 없다고 해서 여기서 만들지 않는다. 두 요청이 동시에 만들려다 부딪힌다.
    /// </summary>
    private const int AltarStateId = 1;

    public static void MapAltarEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes
            .MapGroup("/api/altar")
            .WithTags("Altar")
            .RequireAuthorization();

        group.MapGet("/state", GetAltarStateAsync)
            .WithSummary("제단 상태")
            .WithDescription("전체 봉헌량과 내 보유량을 한 번에 돌려준다. 활성화 여부와 회복률은 서버가 계산한다.");
    }

    private static async Task<IResult> GetAltarStateAsync(
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out ulong userId))
        {
            return TokenInvalid();
        }

        AltarState state = await database.AltarStates
            .AsNoTracking()
            .SingleOrDefaultAsync(altar => altar.Id == AltarStateId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"altar_state 에 id={AltarStateId} 행이 없습니다. AddInventoryAndAltar 마이그레이션이 " +
                "넣어 두는 행이므로, 여기서 만들지 않습니다. " +
                "server/README.md 3장의 dotnet ef database update 가 적용되었는지 확인해 주세요.");

        // 내가 지금까지 봉헌한 합계. 전체 TotalOffered 와 다른 값이다.
        // 기여한 적이 없으면 SUM 이 0 으로 떨어진다.
        long myOfferedTotal = await database.AltarContributions
            .AsNoTracking()
            .Where(contribution => contribution.UserId == userId)
            .SumAsync(contribution => (long)contribution.Amount, cancellationToken);

        // 내 조각 보유량. 행이 없으면 0 이다. 조회하면서 행을 만들지 않는다.
        uint myFragments = await database.PlayerInventoryItems
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && item.ItemId == InventoryEndpoints.SeaHeartFragmentItemId)
            .Select(item => item.Quantity)
            .FirstOrDefaultAsync(cancellationToken);

        // 남은 칸. 빼기 전에 대소를 보는 이유는 둘 다 부호 없는 정수라
        // total > target 인 비정상 데이터에서 음수가 아니라 거대한 양수로 돌아가기 때문이다.
        ulong remainingToTarget = state.TotalOffered >= state.TargetOffering
            ? 0UL
            : state.TargetOffering - state.TotalOffered;

        // ⚠ activated_at 의 null 여부로 판단하지 않는다. activated_at 은 감사·기록용이다.
        //    부등호가 == 이 아니라 >= 인 이유는 설계 문서 10.7절에 있다. 시연 중에
        //    target_offering 을 낮추면 total > target 이 되는데, 그때도 섬은 켜져 있어야 한다.
        bool altarActivated = state.TotalOffered >= state.TargetOffering;

        // 정수 나눗셈이 되지 않도록 float 로 올려서 나눈다. 100% 를 넘기지 않는다.
        // target = 0 은 DB 의 ck_altar_target_positive 가 막지만, 0 으로 나누면
        // Infinity/NaN 이 JSON 으로 나가므로 계산 쪽에서도 떨어뜨린다. (설계 문서 11.2)
        float recoveryPercent = state.TargetOffering == 0
            ? 0f
            : Math.Min(state.TotalOffered / (float)state.TargetOffering, 1f) * 100f;

        ulong maxOfferAmount = Math.Min((ulong)myFragments, remainingToTarget);

        return Results.Ok(new AltarStateResponse(
            state.TotalOffered,
            state.TargetOffering,
            remainingToTarget,
            myFragments,
            maxOfferAmount,
            altarActivated,
            recoveryPercent,
            (ulong)myOfferedTotal,
            // DB 에는 UTC 로 넣지만 Pomelo 는 Kind 를 Unspecified 로 돌려준다.
            // 그대로 직렬화하면 끝의 Z 가 빠져 클라이언트가 현지 시각으로 읽는다.
            // 시각 자체는 건드리지 않고 잃어버린 Kind 만 다시 붙인다.
            DateTime.SpecifyKind(state.UpdatedAt, DateTimeKind.Utc)));
    }

    private static IResult TokenInvalid()
    {
        return Results.Json(
            new ErrorResponse("TOKEN_INVALID", "로그인이 필요합니다. 토큰이 없거나 만료되었습니다."),
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
