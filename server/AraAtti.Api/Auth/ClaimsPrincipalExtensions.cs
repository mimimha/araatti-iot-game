using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AraAtti.Api.Auth;

/// <summary>
/// 토큰에서 "누가 요청했는지" 를 꺼내는 도우미.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// JWT 의 sub 클레임에서 사용자 ID 를 읽는다.
    ///
    /// 인증을 통과했다면 항상 성공한다. 실패하는 경우는 우리가 발급하지 않은 형태의
    /// 토큰이 들어온 때뿐이므로, 부르는 쪽에서 401 로 처리한다.
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out ulong userId)
    {
        string? subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return ulong.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out userId);
    }
}
