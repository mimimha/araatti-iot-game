namespace AraAtti.Api.Contracts;

/// <summary>
/// 회원가입 요청 본문.
///
/// 값이 없을 수도 있으므로(JSON 에서 통째로 빠질 수 있다) nullable 로 받고
/// 검증은 엔드포인트에서 한다. 그래야 "이메일을 입력해 주세요" 같은
/// 우리 문구로 응답할 수 있다.
/// </summary>
public sealed record SignupRequest(string? Email, string? Password);

/// <summary>로그인 요청 본문.</summary>
public sealed record LoginRequest(string? Email, string? Password);

/// <summary>
/// 클라이언트에게 돌려주는 사용자 정보.
///
/// ⚠ 여기에 PasswordHash 를 절대 넣지 않는다. User 엔티티를 그대로 돌려주지 않고
///    이 타입으로 옮겨 담는 이유가 그것이다.
/// </summary>
public sealed record UserResponse(ulong Id, string Email, DateTime CreatedAt);

/// <summary>
/// 로그인 성공 응답.
/// </summary>
/// <param name="AccessToken">Authorization 헤더에 "Bearer {토큰}" 으로 싣는다.</param>
/// <param name="ExpiresIn">만료까지 남은 시간(초).</param>
public sealed record LoginResponse(string AccessToken, int ExpiresIn, UserResponse User);

/// <summary>
/// 실패 응답. 모든 오류가 이 모양으로 나간다.
/// </summary>
/// <param name="Code">클라이언트가 분기에 쓰는 값. 예: "EMAIL_ALREADY_USED"</param>
/// <param name="Message">사용자에게 그대로 보여줄 한국어 문구.</param>
public sealed record ErrorResponse(string Code, string Message);
