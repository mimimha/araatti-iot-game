using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AraAtti.Api.Auth;
using AraAtti.Api.Contracts;
using AraAtti.Api.Data;
using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AraAtti.Api.Endpoints;

/// <summary>
/// 회원가입 · 로그인 엔드포인트.
///
/// 규칙
///   - 실패는 언제나 ErrorResponse(code, message) 로 나간다.
///   - message 는 한국어다. Unity 가 화면에 그대로 띄운다.
///   - 응답에 비밀번호나 해시를 절대 담지 않는다.
///
/// 문서: docs/prd/auth-character-roadmap.md 3장
/// </summary>
public static class AuthEndpoints
{
    /// <summary>비밀번호 최소 길이. Unity 안내 문구도 이 값에 맞춘다.</summary>
    private const int MinimumPasswordLength = 8;

    /// <summary>users.email 컬럼 길이와 같다.</summary>
    private const int MaximumEmailLength = 190;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/signup", SignupAsync)
            .WithSummary("회원가입")
            .WithDescription("이메일과 비밀번호로 계정을 만든다. 토큰은 주지 않는다. 가입 후 로그인을 호출한다.");

        group.MapPost("/login", LoginAsync)
            .WithSummary("로그인")
            .WithDescription("성공하면 JWT 액세스 토큰을 준다.");

        group.MapGet("/me", GetMeAsync)
            .RequireAuthorization()
            .WithSummary("내 정보")
            .WithDescription("Bearer 토큰이 유효한지 확인하는 용도. 토큰이 없거나 만료면 401.");
    }

    // ------------------------------------------------------------
    // 회원가입
    // ------------------------------------------------------------

    private static async Task<IResult> SignupAsync(
        SignupRequest request,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        ErrorResponse? validationError = ValidateSignup(request);
        if (validationError is not null)
        {
            return Results.Json(validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        string email = NormalizeEmail(request.Email!);

        // 먼저 확인해서 흔한 경우를 친절한 메시지로 처리한다.
        // 동시에 두 명이 같은 이메일로 가입하는 경우는 아래 DbUpdateException 이 잡는다.
        bool alreadyUsed = await database.Users
            .AnyAsync(user => user.Email == email, cancellationToken);
        if (alreadyUsed)
        {
            return EmailAlreadyUsed();
        }

        User newUser = new()
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password!),
            CreatedAt = DateTime.UtcNow
        };

        database.Users.Add(newUser);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // users 테이블의 UNIQUE 제약은 email 하나뿐이므로 원인은 이메일 중복이다.
            // (uk_users_email — PRD 03 마이그레이션)
            return EmailAlreadyUsed();
        }

        return Results.Json(ToResponse(newUser), statusCode: StatusCodes.Status201Created);
    }

    private static IResult EmailAlreadyUsed()
    {
        return Results.Json(
            new ErrorResponse("EMAIL_ALREADY_USED", "이미 가입된 이메일입니다."),
            statusCode: StatusCodes.Status409Conflict);
    }

    // ------------------------------------------------------------
    // 로그인
    // ------------------------------------------------------------

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AraAttiDbContext database,
        JwtTokenGenerator tokenGenerator,
        CancellationToken cancellationToken)
    {
        // 로그인에서는 형식·길이 규칙을 다시 검사하지 않는다.
        // 규칙이 나중에 바뀌어도 예전에 가입한 사람이 로그인하지 못하게 되면 안 되기 때문이다.
        // 값이 아예 비어 있는 경우만 막는다.
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Json(
                new ErrorResponse("VALIDATION_FAILED", "이메일과 비밀번호를 입력해 주세요."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        string email = NormalizeEmail(request.Email);

        User? user = await database.Users
            .SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            // 계정이 없어도 대조를 한 번 하고 같은 실패를 돌려준다.
            // 응답 시간으로 가입 여부를 알아내지 못하게 하기 위함이다.
            PasswordHasher.VerifyDummy(request.Password);
            return InvalidCredentials();
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return InvalidCredentials();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        (string accessToken, int expiresIn) = tokenGenerator.Create(user);

        return Results.Ok(new LoginResponse(accessToken, expiresIn, ToResponse(user)));
    }

    /// <summary>
    /// 이메일이 없을 때와 비밀번호가 틀렸을 때가 **완전히 같은 응답**이어야 한다.
    /// 다르게 답하면 어떤 이메일이 가입되어 있는지 알아낼 수 있다.
    /// </summary>
    private static IResult InvalidCredentials()
    {
        return Results.Json(
            new ErrorResponse("INVALID_CREDENTIALS", "이메일 또는 비밀번호가 올바르지 않습니다."),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // ------------------------------------------------------------
    // 내 정보 — JWT 설정이 실제로 동작하는지 확인하는 통로
    // ------------------------------------------------------------

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal principal,
        AraAttiDbContext database,
        CancellationToken cancellationToken)
    {
        string? subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!ulong.TryParse(subject, out ulong userId))
        {
            return TokenInvalid();
        }

        User? user = await database.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        // 토큰은 유효하지만 그 사이 계정이 지워진 경우.
        return user is null ? TokenInvalid() : Results.Ok(ToResponse(user));
    }

    private static IResult TokenInvalid()
    {
        return Results.Json(
            new ErrorResponse("TOKEN_INVALID", "로그인이 필요합니다. 토큰이 없거나 만료되었습니다."),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // ------------------------------------------------------------
    // 공통
    // ------------------------------------------------------------

    private static ErrorResponse? ValidateSignup(SignupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Invalid("이메일을 입력해 주세요.");
        }

        string email = NormalizeEmail(request.Email);

        if (email.Length > MaximumEmailLength)
        {
            return Invalid($"이메일은 {MaximumEmailLength}자 이하여야 합니다.");
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return Invalid("이메일 형식이 올바르지 않습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Invalid("비밀번호를 입력해 주세요.");
        }

        if (request.Password.Length < MinimumPasswordLength)
        {
            return Invalid($"비밀번호는 {MinimumPasswordLength}자 이상이어야 합니다.");
        }

        return null;

        static ErrorResponse Invalid(string message) => new("VALIDATION_FAILED", message);
    }

    /// <summary>
    /// 이메일을 저장·조회에 쓸 형태로 다듬는다.
    ///
    /// 소문자로 통일하는 이유: 사용자가 대문자를 섞어 입력해도 같은 계정으로 찾아야 한다.
    /// 저장할 때와 찾을 때 같은 함수를 쓰므로 둘이 어긋날 일이 없다.
    /// </summary>
    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// 엔티티를 응답 타입으로 옮겨 담는다.
    ///
    /// ⚠ User 엔티티를 그대로 돌려주면 PasswordHash 까지 JSON 에 실린다. 반드시 이걸 거친다.
    /// </summary>
    private static UserResponse ToResponse(User user)
    {
        return new UserResponse(user.Id, user.Email, user.CreatedAt);
    }
}
