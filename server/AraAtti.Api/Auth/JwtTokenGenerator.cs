using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AraAtti.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AraAtti.Api.Auth;

/// <summary>
/// 로그인에 성공한 사용자에게 줄 JWT 를 만든다.
///
/// 담기는 내용은 세 가지뿐이다.
///   sub    누구인지 (users.id)
///   email  누구인지 (읽기 편하라고)
///   exp    언제까지 유효한지
///
/// ⚠ 토큰 안에 비밀번호나 해시를 넣지 않는다. JWT 의 본문은 **암호화되지 않고**
///    Base64 로 인코딩되어 있을 뿐이라 누구나 열어볼 수 있다.
///    서명은 "내용이 바뀌지 않았음" 을 보장할 뿐 내용을 가리지 않는다.
/// </summary>
public sealed class JwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>토큰 문자열과 만료까지 남은 시간(초)을 돌려준다.</summary>
    public (string AccessToken, int ExpiresInSeconds) Create(User user)
    {
        Claim[] claims =
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email),

            // 토큰마다 다른 값. 나중에 토큰 하나만 무효화하고 싶어질 때 쓸 수 있다.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes),
            signingCredentials: credentials);

        string encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return (encoded, _options.ExpiresMinutes * 60);
    }
}
