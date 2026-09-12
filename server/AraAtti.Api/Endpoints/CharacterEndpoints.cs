using System.Security.Claims;
using System.Text.RegularExpressions;
using AraAtti.Api.Auth;
using AraAtti.Api.Contracts;
using AraAtti.Api.Data;
using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AraAtti.Api.Endpoints;

/// <summary>
/// 내 캐릭터 조회 · 생성.
///
/// 규칙
///   - 주인은 언제나 JWT 의 sub 다. 요청 본문의 userId 같은 것은 받지 않는다.
///   - 남의 캐릭터는 절대 보이지 않는다. 모든 조회에 user_id 조건이 붙는다.
///   - 실패는 ErrorResponse(code, message) 로 나가고 message 는 한국어다.
///
/// 문서: docs/prd/auth-character-roadmap.md 2장 · 3장
/// </summary>
public static class CharacterEndpoints
{
    /// <summary>
    /// 계정당 만들 수 있는 캐릭터 수.
    ///
    /// ★ 다중 캐릭터를 열려면 **이 값만** 바꾸면 된다.
    ///   DB 스키마에는 계정당 1개 제약이 없다. (characters.user_id 에 UNIQUE 를 걸지 않았다)
    ///   그래서 마이그레이션 없이 늘릴 수 있다.
    ///   단, 그때는 CreateCharacterAsync 의 SlotIndex 를 "비어 있는 가장 작은 번호" 로
    ///   골라 주어야 한다. 지금은 1개뿐이라 항상 0 이다.
    /// </summary>
    public const int MaxCharactersPerUser = 1;

    private const int MinNicknameLength = 2;

    /// <summary>characters.name 컬럼 길이와 같다.</summary>
    private const int MaxNicknameLength = 10;

    /// <summary>character_parts.prefab_name 컬럼 길이와 같다.</summary>
    private const int MaxPrefabNameLength = 64;

    /// <summary>
    /// 쓸 수 있는 슬롯 이름.
    ///
    /// Unity 의 캐릭터 생성 화면 카테고리 이름과 같다.
    /// (CharacterCustomizationPersistence.PersistedCategories)
    ///
    /// ⚠ 여기 있는 것은 **슬롯 이름 6개뿐**이다. 프리팹 목록이 아니다.
    ///    어떤 프리팹이 있는지는 서버가 알지 못하고, 알 필요도 없다.
    ///    Unity 에 카테고리가 늘어나면 이 배열에 한 줄 추가하면 된다.
    /// </summary>
    private static readonly string[] AllowedSlots =
    {
        "Face", "Hair", "Shoes", "Top", "Bottom", "Accessory"
    };

    /// <summary>Unity 의 닉네임 검증과 같은 규칙. (GetNicknameValidationMessage)</summary>
    private static readonly Regex NicknamePattern =
        new("^[가-힣ㄱ-ㅎㅏ-ㅣA-Za-z0-9]+$", RegexOptions.Compiled);

    /// <summary>"#RRGGBB". 팔레트 인덱스가 아니라 색 자체를 저장한다.</summary>
    private static readonly Regex SkinColorPattern =
        new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public static void MapCharacterEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes
            .MapGroup("/api/characters")
            .WithTags("Characters")
            .RequireAuthorization();

        group.MapGet("/", GetMyCharactersAsync)
            .WithSummary("내 캐릭터 목록")
            .WithDescription("토큰 주인의 캐릭터를 배열로 돌려준다. 없으면 빈 배열이다. 404 가 아니다.");

        group.MapPost("/", CreateCharacterAsync)
            .WithSummary("캐릭터 생성")
            .WithDescription("계정당 1개. 이미 있으면 409 CHARACTER_LIMIT_REACHED.");
    }

    // ------------------------------------------------------------
    // 조회
    // ------------------------------------------------------------

    private static async Task<IResult> GetMyCharactersAsync(
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out ulong userId))
        {
            return TokenInvalid();
        }

        List<Character> characters = await database.Characters
            .AsNoTracking()
            .Include(character => character.Parts)
            .Where(character => character.UserId == userId)
            .OrderBy(character => character.SlotIndex)
            .ToListAsync(cancellationToken);

        return Results.Ok(new CharacterListResponse(characters.Select(ToResponse).ToArray()));
    }

    // ------------------------------------------------------------
    // 생성
    // ------------------------------------------------------------

    private static async Task<IResult> CreateCharacterAsync(
        CreateCharacterRequest request,
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out ulong userId))
        {
            return TokenInvalid();
        }

        ErrorResponse? validationError = Validate(request);
        if (validationError is not null)
        {
            return Results.Json(validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        string nickname = request.Nickname!.Trim();
        string skinColor = request.SkinColor!.Trim().ToUpperInvariant();

        int owned = await database.Characters
            .CountAsync(character => character.UserId == userId, cancellationToken);
        if (owned >= MaxCharactersPerUser)
        {
            return CharacterLimitReached();
        }

        bool nicknameTaken = await database.Characters
            .AnyAsync(character => character.Name == nickname, cancellationToken);
        if (nicknameTaken)
        {
            return NicknameAlreadyUsed();
        }

        DateTime now = DateTime.UtcNow;

        Character newCharacter = new()
        {
            UserId = userId,
            Name = nickname,
            SkinColor = skinColor,
            SlotIndex = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Parts = request.Parts!
                .Select(part => new CharacterPart
                {
                    Slot = CanonicalSlot(part!.Slot!.Trim()),
                    PrefabName = part.PrefabName!.Trim()
                })
                .ToList()
        };

        database.Characters.Add(newCharacter);

        try
        {
            // 캐릭터와 파츠가 한 트랜잭션으로 함께 저장된다.
            // 중간에 실패하면 둘 다 저장되지 않는다.
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // 같은 요청이 동시에 두 번 들어온 경우다. 위의 검사를 둘 다 통과한 뒤
            // DB 의 UNIQUE 제약이 막아 준 것이므로, 어느 쪽에 걸렸는지 다시 확인한다.
            if (await database.Characters.AnyAsync(c => c.UserId == userId, cancellationToken))
            {
                return CharacterLimitReached();
            }

            if (await database.Characters.AnyAsync(c => c.Name == nickname, cancellationToken))
            {
                return NicknameAlreadyUsed();
            }

            throw;
        }

        return Results.Json(ToResponse(newCharacter), statusCode: StatusCodes.Status201Created);
    }

    // ------------------------------------------------------------
    // 검증
    // ------------------------------------------------------------

    /// <summary>
    /// 구조적으로 말이 되는 요청인지만 본다.
    ///
    /// ⚠ prefabName 이 실제로 있는 프리팹인지는 **검사하지 않는다.**
    ///    서버는 Unity 의 프리팹 목록을 알지 못하고, 알게 만들면 에셋이 바뀔 때마다
    ///    서버를 같이 고쳐야 한다. 빈 값과 길이만 본다.
    ///    없는 이름이 들어오면 Unity 가 그 슬롯만 건너뛰고 경고를 남긴다. (PRD 01)
    /// </summary>
    private static ErrorResponse? Validate(CreateCharacterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nickname))
        {
            return Invalid("닉네임을 입력해 주세요.");
        }

        string nickname = request.Nickname.Trim();

        if (nickname.Length is < MinNicknameLength or > MaxNicknameLength)
        {
            return Invalid($"닉네임은 {MinNicknameLength}~{MaxNicknameLength}자여야 합니다.");
        }

        if (!NicknamePattern.IsMatch(nickname))
        {
            return Invalid("닉네임은 한글·영문·숫자만 쓸 수 있습니다. (공백·특수문자 불가)");
        }

        if (string.IsNullOrWhiteSpace(request.SkinColor))
        {
            return Invalid("피부색을 입력해 주세요.");
        }

        if (!SkinColorPattern.IsMatch(request.SkinColor.Trim()))
        {
            return Invalid("피부색은 \"#RRGGBB\" 형식이어야 합니다. 예: \"#F2C9A0\"");
        }

        if (request.Parts is null)
        {
            return Invalid("parts 가 없습니다. 입힌 파츠가 없으면 빈 배열을 보내 주세요.");
        }

        HashSet<string> usedSlots = new(StringComparer.OrdinalIgnoreCase);

        foreach (CharacterPartRequest? part in request.Parts)
        {
            if (part is null)
            {
                return Invalid("parts 안에 빈 항목이 있습니다.");
            }

            if (string.IsNullOrWhiteSpace(part.Slot))
            {
                return Invalid("파츠의 slot 이 비어 있습니다.");
            }

            if (string.IsNullOrWhiteSpace(part.PrefabName))
            {
                return Invalid("파츠의 prefabName 이 비어 있습니다.");
            }

            string slot = part.Slot.Trim();

            if (!IsAllowedSlot(slot))
            {
                return Invalid(
                    $"알 수 없는 slot 입니다: \"{slot}\". " +
                    $"쓸 수 있는 값: {string.Join(", ", AllowedSlots)}");
            }

            if (part.PrefabName.Trim().Length > MaxPrefabNameLength)
            {
                return Invalid($"prefabName 은 {MaxPrefabNameLength}자 이하여야 합니다.");
            }

            // 한 슬롯에 두 개를 입을 수는 없다. DB 의 uk_parts_character_slot 과 같은 규칙이다.
            if (!usedSlots.Add(slot))
            {
                return Invalid($"같은 slot 이 두 번 들어 있습니다: \"{slot}\"");
            }
        }

        return null;

        static ErrorResponse Invalid(string message) => new("VALIDATION_FAILED", message);
    }

    private static bool IsAllowedSlot(string slot)
    {
        return AllowedSlots.Contains(slot, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>"face" 로 보내도 "Face" 로 저장한다. 저장 형태를 하나로 맞추기 위함이다.</summary>
    private static string CanonicalSlot(string slot)
    {
        return AllowedSlots.First(allowed => string.Equals(allowed, slot, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------
    // 실패 응답
    // ------------------------------------------------------------

    private static IResult CharacterLimitReached()
    {
        return Results.Json(
            new ErrorResponse("CHARACTER_LIMIT_REACHED", "이미 캐릭터를 보유하고 있습니다."),
            statusCode: StatusCodes.Status409Conflict);
    }

    private static IResult NicknameAlreadyUsed()
    {
        return Results.Json(
            new ErrorResponse("NICKNAME_ALREADY_USED", "이미 사용 중인 닉네임입니다."),
            statusCode: StatusCodes.Status409Conflict);
    }

    private static IResult TokenInvalid()
    {
        return Results.Json(
            new ErrorResponse("TOKEN_INVALID", "로그인이 필요합니다. 토큰이 없거나 만료되었습니다."),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // ------------------------------------------------------------
    // 변환
    // ------------------------------------------------------------

    private static CharacterResponse ToResponse(Character character)
    {
        CharacterPartResponse[] parts = character.Parts
            // 저장 순서와 무관하게 언제나 같은 순서로 내보낸다.
            .OrderBy(part => part.Slot, StringComparer.Ordinal)
            .Select(part => new CharacterPartResponse(part.Slot, part.PrefabName))
            .ToArray();

        return new CharacterResponse(
            character.Id,
            character.Name,
            character.SkinColor,
            character.SlotIndex,
            parts,
            character.CreatedAt,
            character.UpdatedAt);
    }
}
