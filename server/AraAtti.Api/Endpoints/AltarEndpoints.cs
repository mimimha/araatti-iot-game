using System.Security.Claims;
using AraAtti.Api.Auth;
using AraAtti.Api.Contracts;
using AraAtti.Api.Data;
using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace AraAtti.Api.Endpoints;

/// <summary>
/// 제단 상태 조회와 봉헌.
///
/// 규칙
///   - 전체 봉헌량은 DB 한 행(altar_state id=1)이 원본이다. 서버 메모리에 두지 않는다.
///   - 활성화 여부와 회복률은 **서버가 계산**해서 내려준다. 클라이언트가 계산하지 않는다.
///   - "나" 는 언제나 JWT 의 sub 다.
///   - 봉헌은 부분 수락하지 않는다. 성공이면 요청한 수량 그대로, 실패면 아무 일도 없다.
///
/// 문서: docs/prd/lobby_altar_inventory_system_design.md 8.6 · 9.2 · 10 · 11
/// </summary>
public static class AltarEndpoints
{
    /// <summary>
    /// altar_state 의 유일한 행 번호.
    ///
    /// ⚠ 이 행은 AddInventoryAndAltar 마이그레이션이 넣어 둔 것이다.
    ///    조회하다가 없다고 해서 여기서 만들지 않는다. 두 요청이 동시에 만들려다 부딪힌다.
    /// </summary>
    internal const int AltarStateId = 1;

    public static void MapAltarEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes
            .MapGroup("/api/altar")
            .WithTags("Altar")
            .RequireAuthorization();

        group.MapGet("/state", GetAltarStateAsync)
            .WithSummary("제단 상태")
            .WithDescription("전체 봉헌량과 내 보유량을 한 번에 돌려준다. 활성화 여부와 회복률은 서버가 계산한다.");

        group.MapPost("/offer", OfferAsync)
            .WithSummary("봉헌")
            .WithDescription("조각을 제단에 바친다. 부분 수락은 없다. 같은 requestId 재요청은 duplicate 로 처리한다.");
    }

    // ------------------------------------------------------------
    // 조회
    // ------------------------------------------------------------

    private static async Task<IResult> GetAltarStateAsync(
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out ulong userId))
        {
            return TokenInvalid();
        }

        AltarSnapshot snapshot = await ReadSnapshotAsync(database, userId, cancellationToken);

        // 내가 지금까지 봉헌한 합계. 전체 TotalOffered 와 다른 값이다.
        // 기여한 적이 없으면 SUM 이 0 으로 떨어진다.
        //
        // altar_contributions.amount 는 int unsigned(uint) 인데 LINQ 에 Sum(uint) 오버로드가
        // 없어서 long 으로 올려 합산한다. 합계는 언제나 0 이상이다.
        long myOfferedTotal = await database.AltarContributions
            .AsNoTracking()
            .Where(contribution => contribution.UserId == userId)
            .SumAsync(contribution => (long)contribution.Amount, cancellationToken);

        return Results.Ok(new AltarStateResponse(
            snapshot.TotalOffered,
            snapshot.TargetOffering,
            snapshot.RemainingToTarget,
            snapshot.MyFragments,
            snapshot.MaxOfferAmount,
            snapshot.AltarActivated,
            snapshot.RecoveryPercent,
            (ulong)myOfferedTotal,
            snapshot.UpdatedAt));
    }

    // ------------------------------------------------------------
    // 봉헌
    // ------------------------------------------------------------

    /// <summary>
    /// 검증 순서는 설계 8.6절 그대로다. 순서를 바꾸면 사용자가 받는 실패 이유가 달라진다.
    ///
    ///   1 로그인했는가            → 401 (그룹의 RequireAuthorization + sub 파싱)
    ///   2 캐릭터가 있는가          → 검사하지 않는다. 인벤토리는 users 에 매달린다 (결정 #6)
    ///   3 amount 가 정수인가       → 역직렬화 단계
    ///   4 amount &gt; 0 인가       → 400 AMOUNT_INVALID
    ///   5 amount 가 상한 이하인가  → 400 AMOUNT_TOO_LARGE
    ///   6 requestId 형식이 맞는가  → 400 REQUEST_ID_INVALID
    ///   7 이미 처리된 요청인가     → 200 duplicate
    ///   8 남은 칸이 amount 이상인가 → 409 (DB 안에서 원자적으로)
    ///   9 보유량이 amount 이상인가  → 409 (DB 안에서 원자적으로)
    ///
    /// 8·9 는 읽어서 판단하지 않는다. 판단을 WHERE 에 넣고 "바뀐 행 0개" 를 실패로 읽는다.
    /// 먼저 SELECT 해서 판단한 뒤 UPDATE 하면 그 사이에 다른 요청이 끼어들어
    /// 1001/1000 이 만들어진다. (설계 10.2 의 두 번째 금지 사례)
    /// </summary>
    private static async Task<IResult> OfferAsync(
        AltarOfferRequest request,
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out ulong userId))
        {
            return TokenInvalid();
        }

        if (request.Amount is not long amount || amount <= 0)
        {
            return AmountInvalid();
        }

        // 상한을 int.MaxValue 로 두는 이유는 오버플로 방어다. 실질적인 상한은
        // 남은 칸과 보유량이고, 그 둘은 아래 조건부 UPDATE 가 본다. (설계 8.6 의 5번)
        if (amount > int.MaxValue)
        {
            return AmountTooLarge();
        }

        if (!Guid.TryParse(request.RequestId, out Guid requestId))
        {
            return RequestIdInvalid();
        }

        uint offerAmount = (uint)amount;

        // ── 7. 멱등성 사전 확인 — 트랜잭션 밖에서 값싸게 거른다 ──────────────
        //    이것만으로는 부족하다. 정말 동시에 온 두 요청은 둘 다 "없음" 을 본다.
        //    최후의 방어선은 uk_contributions_user_request 이고, 아래에서 그 예외를 잡는다.
        AltarContribution? alreadyProcessed =
            await FindContributionAsync(database, userId, requestId, cancellationToken);
        if (alreadyProcessed is not null)
        {
            return await DuplicateAsync(database, userId, alreadyProcessed.Amount, cancellationToken);
        }

        await using IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        // ── ① 제단: 남은 칸을 확인하면서 동시에 더한다 ────────────────────
        //    조건을 뺄셈(target - total >= amount)이 아니라 덧셈으로 쓴 이유:
        //    두 컬럼이 모두 UNSIGNED 라, total > target 인 비정상 데이터에서 뺄셈은
        //    음수가 아니라 MySQL 오류(BIGINT UNSIGNED out of range)가 된다.
        //    덧셈은 같은 판정을 하면서 그 경로가 없다.
        int accepted = await database.AltarStates
            .Where(altar => altar.Id == AltarStateId
                && altar.TotalOffered + offerAmount <= altar.TargetOffering)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(altar => altar.TotalOffered, altar => altar.TotalOffered + offerAmount)
                .SetProperty(altar => altar.UpdatedAt, _ => DateTime.UtcNow), cancellationToken);

        if (accepted == 0)
        {
            // 남은 칸이 모자라다. ⚠ 인벤토리는 아직 건드리지 않았다.
            await transaction.RollbackAsync(cancellationToken);
            return await OfferingBlockedAsync(database, userId, requestId, cancellationToken);
        }

        // ── ② 인벤토리: 보유량을 확인하면서 동시에 뺀다 ────────────────────
        //    행이 아예 없어도 0행이다. 봉헌은 UPSERT 가 아니므로 행을 만들지 않는다.
        int decremented = await database.PlayerInventoryItems
            .Where(item => item.UserId == userId
                && item.ItemId == InventoryEndpoints.SeaHeartFragmentItemId
                && item.Quantity >= offerAmount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Quantity, item => item.Quantity - offerAmount)
                .SetProperty(item => item.UpdatedAt, _ => DateTime.UtcNow), cancellationToken);

        if (decremented == 0)
        {
            // ① 에서 더한 것도 함께 되돌아간다. 인벤토리가 일부만 깎이는 일은 없다.
            await transaction.RollbackAsync(cancellationToken);
            return await NotEnoughFragmentsAsync(database, userId, requestId, offerAmount, cancellationToken);
        }

        // ── ③ 이력 기록 — 중복 방지의 핵심 ────────────────────────────────
        AltarContribution contribution = new()
        {
            UserId = userId,
            RequestId = requestId,
            Amount = offerAmount,
            CreatedAt = DateTime.UtcNow
        };

        database.AltarContributions.Add(contribution);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKeyViolation(exception))
        {
            // 그 사이 같은 (user_id, requestId) 가 먼저 커밋됐다.
            await transaction.RollbackAsync(cancellationToken);

            // 실패한 INSERT 가 Added 로 남아 있으면 다음 SaveChanges 가 같은 것을 또 시도한다.
            database.Entry(contribution).State = EntityState.Detached;

            AltarContribution? winner =
                await FindContributionAsync(database, userId, requestId, cancellationToken);
            if (winner is null)
            {
                // 우리 멱등성 키가 아닌 다른 중복이다. 숨기지 않고 그대로 올린다.
                throw;
            }

            return await DuplicateAsync(database, userId, winner.Amount, cancellationToken);
        }

        // ── ④ 커밋 ────────────────────────────────────────────────────────
        await transaction.CommitAsync(cancellationToken);

        AltarSnapshot snapshot = await ReadSnapshotAsync(database, userId, cancellationToken);

        return Results.Ok(new AltarOfferResponse(
            Success: true,
            OfferedAmount: offerAmount,
            RemainingFragments: snapshot.MyFragments,
            TotalOffered: snapshot.TotalOffered,
            TargetOffering: snapshot.TargetOffering,
            RemainingToTarget: snapshot.RemainingToTarget,
            MaxOfferAmount: snapshot.MaxOfferAmount,
            AltarActivated: snapshot.AltarActivated,
            RecoveryPercent: snapshot.RecoveryPercent,
            Duplicate: false));
    }

    // ------------------------------------------------------------
    // 실패 · 멱등 응답
    // ------------------------------------------------------------

    /// <summary>
    /// 제단 조건부 UPDATE 가 0행이었을 때.
    ///
    /// ⚠ 실패로 답하기 전에 같은 (user_id, requestId) 가 방금 커밋됐는지 다시 본다.
    ///    두 개의 같은 요청이 동시에 오면, 먼저 온 쪽이 마지막 칸을 채운 뒤
    ///    나중 쪽이 여기로 떨어질 수 있다. 그때 OFFERING_CLOSED 로 답하면
    ///    "같은 requestId 는 다시 실행하지 않고 duplicate 로 돌려준다" 는 계약이 깨진다.
    /// </summary>
    private static async Task<IResult> OfferingBlockedAsync(
        AraAttiDbContext database,
        ulong userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        AltarContribution? winner =
            await FindContributionAsync(database, userId, requestId, cancellationToken);
        if (winner is not null)
        {
            return await DuplicateAsync(database, userId, winner.Amount, cancellationToken);
        }

        AltarSnapshot snapshot = await ReadSnapshotAsync(database, userId, cancellationToken);

        // 두 경우를 가른다. 사용자에게 다음 행동이 다르기 때문이다.
        //   끝난 것이다              → OFFERING_CLOSED
        //   수량을 낮추면 된다        → OFFERING_AMOUNT_CHANGED
        // ⚠ 수량이 들어간 문구("N개 남았습니다")를 여기에 붙이지 않는다.
        //    클라이언트가 remainingToTarget 으로 조합한다. (설계 9.2)
        return snapshot.RemainingToTarget == 0
            ? Failure("OFFERING_CLOSED", "섬 회복이 완료되어 더 이상 등록할 수 없습니다.", snapshot)
            : Failure("OFFERING_AMOUNT_CHANGED", "다른 플레이어가 먼저 등록했습니다.", snapshot);
    }

    /// <summary>인벤토리 조건부 UPDATE 가 0행이었을 때. 제단 증가는 이미 되돌아갔다.</summary>
    private static async Task<IResult> NotEnoughFragmentsAsync(
        AraAttiDbContext database,
        ulong userId,
        Guid requestId,
        uint requestedAmount,
        CancellationToken cancellationToken)
    {
        AltarContribution? winner =
            await FindContributionAsync(database, userId, requestId, cancellationToken);
        if (winner is not null)
        {
            return await DuplicateAsync(database, userId, winner.Amount, cancellationToken);
        }

        AltarSnapshot snapshot = await ReadSnapshotAsync(database, userId, cancellationToken);

        return Failure(
            "NOT_ENOUGH_FRAGMENTS",
            $"보유한 조각이 모자랍니다. (보유 {snapshot.MyFragments} / 요청 {requestedAmount})",
            snapshot);
    }

    /// <summary>
    /// 같은 (user_id, requestId) 를 다시 받았다. 봉헌을 다시 하지 않는다.
    ///
    /// offeredAmount 는 <b>이미 기록된 contribution.amount</b> 다. 이번 요청의 amount 가
    /// 다르더라도 그것을 쓰지 않는다. 실제로 반영된 수량은 처음 것 하나뿐이기 때문이다.
    /// 상태값은 그때가 아니라 <b>지금</b> 값이다. (설계 10.4)
    /// </summary>
    private static async Task<IResult> DuplicateAsync(
        AraAttiDbContext database,
        ulong userId,
        uint offeredAmount,
        CancellationToken cancellationToken)
    {
        AltarSnapshot snapshot = await ReadSnapshotAsync(database, userId, cancellationToken);

        return Results.Ok(new AltarOfferResponse(
            Success: true,
            OfferedAmount: offeredAmount,
            RemainingFragments: snapshot.MyFragments,
            TotalOffered: snapshot.TotalOffered,
            TargetOffering: snapshot.TargetOffering,
            RemainingToTarget: snapshot.RemainingToTarget,
            MaxOfferAmount: snapshot.MaxOfferAmount,
            AltarActivated: snapshot.AltarActivated,
            RecoveryPercent: snapshot.RecoveryPercent,
            Duplicate: true));
    }

    private static IResult Failure(string code, string message, AltarSnapshot snapshot)
    {
        return Results.Json(
            new AltarOfferFailureResponse(
                Success: false,
                Code: code,
                Message: message,
                RemainingFragments: snapshot.MyFragments,
                TotalOffered: snapshot.TotalOffered,
                TargetOffering: snapshot.TargetOffering,
                RemainingToTarget: snapshot.RemainingToTarget,
                MaxOfferAmount: snapshot.MaxOfferAmount,
                AltarActivated: snapshot.AltarActivated,
                RecoveryPercent: snapshot.RecoveryPercent),
            statusCode: StatusCodes.Status409Conflict);
    }

    // ------------------------------------------------------------
    // 공통 조회 · 계산
    // ------------------------------------------------------------

    /// <summary>
    /// 응답에 실을 상태값 한 벌.
    ///
    /// 조회와 봉헌이 **같은 공식**을 쓰게 하려고 한 곳에 모았다.
    /// 두 엔드포인트가 각자 계산하면 어느 날 한쪽만 고쳐진다.
    /// </summary>
    private readonly record struct AltarSnapshot(
        ulong TotalOffered,
        uint TargetOffering,
        uint MyFragments,
        ulong RemainingToTarget,
        ulong MaxOfferAmount,
        bool AltarActivated,
        float RecoveryPercent,
        DateTime UpdatedAt);

    private static async Task<AltarSnapshot> ReadSnapshotAsync(
        AraAttiDbContext database,
        ulong userId,
        CancellationToken cancellationToken)
    {
        AltarState state = await database.AltarStates
            .AsNoTracking()
            .SingleOrDefaultAsync(altar => altar.Id == AltarStateId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"altar_state 에 id={AltarStateId} 행이 없습니다. AddInventoryAndAltar 마이그레이션이 " +
                "넣어 두는 행이므로, 여기서 만들지 않습니다. " +
                "server/README.md 3장의 dotnet ef database update 가 적용되었는지 확인해 주세요.");

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

        return new AltarSnapshot(
            state.TotalOffered,
            state.TargetOffering,
            myFragments,
            remainingToTarget,
            Math.Min((ulong)myFragments, remainingToTarget),
            altarActivated,
            recoveryPercent,
            // DB 에는 UTC 로 넣지만 Pomelo 는 Kind 를 Unspecified 로 돌려준다.
            // 그대로 직렬화하면 끝의 Z 가 빠져 클라이언트가 현지 시각으로 읽는다.
            // 시각 자체는 건드리지 않고 잃어버린 Kind 만 다시 붙인다.
            DateTime.SpecifyKind(state.UpdatedAt, DateTimeKind.Utc));
    }

    private static Task<AltarContribution?> FindContributionAsync(
        AraAttiDbContext database,
        ulong userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return database.AltarContributions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                contribution => contribution.UserId == userId && contribution.RequestId == requestId,
                cancellationToken);
    }

    /// <summary>
    /// UNIQUE 위반인지 정확히 가린다.
    ///
    /// ⚠ 모든 DbUpdateException 을 중복으로 보면 안 된다. 연결이 끊긴 것도,
    ///    다른 제약을 위반한 것도 전부 duplicate 성공으로 둔갑한다.
    ///    MySQL 의 1062(ER_DUP_ENTRY)일 때만 참이다.
    /// </summary>
    internal static bool IsDuplicateKeyViolation(DbUpdateException exception)
    {
        return exception.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry };
    }

    // ------------------------------------------------------------
    // 입력 오류
    //
    // 상태를 읽기 전에 걸러지는 400 들이라 프로젝트 공통 ErrorResponse 로 나간다.
    // 최신 상태를 싣는 것은 실제로 경쟁이 일어난 409 쪽이다. (설계 9.2)
    // ------------------------------------------------------------

    private static IResult AmountInvalid()
    {
        return Results.Json(
            new ErrorResponse("AMOUNT_INVALID", "등록할 수량은 1개 이상이어야 합니다."),
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult AmountTooLarge()
    {
        return Results.Json(
            new ErrorResponse("AMOUNT_TOO_LARGE", "등록할 수량이 너무 큽니다."),
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult RequestIdInvalid()
    {
        return Results.Json(
            new ErrorResponse("REQUEST_ID_INVALID", "요청 식별자 형식이 올바르지 않습니다."),
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult TokenInvalid()
    {
        return Results.Json(
            new ErrorResponse("TOKEN_INVALID", "로그인이 필요합니다. 토큰이 없거나 만료되었습니다."),
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
