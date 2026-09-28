namespace AraAtti.Api.Auth;

/// <summary>
/// JWT 설정. appsettings 의 "Jwt" 구역을 그대로 받는다.
///
/// ⚠ Key 는 커밋되는 파일에 넣지 않는다.
///    appsettings.json 에는 Issuer · Audience · ExpiresMinutes 만 두고,
///    Key 는 appsettings.Development.json(.gitignore 대상) 이나
///    환경 변수 Jwt__Key 로 넣는다.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HS256 서명 키. **32바이트(영문 32자) 이상**이어야 한다.
    ///
    /// 짧으면 서명이 약해지고, IdentityModel 이 실행 중에 예외를 던진다.
    /// 그래서 서버가 켜질 때 미리 검사한다. (Program.cs)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>토큰을 발급한 주체. 검증할 때 이 값과 같아야 한다.</summary>
    public string Issuer { get; set; } = "araatti-api";

    /// <summary>토큰을 쓸 대상. 검증할 때 이 값과 같아야 한다.</summary>
    public string Audience { get; set; } = "araatti-client";

    /// <summary>만료까지의 시간(분). 기본 2시간.</summary>
    public int ExpiresMinutes { get; set; } = 120;
}
