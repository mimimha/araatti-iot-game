namespace AraAtti.Api.Auth;

/// <summary>
/// 비밀번호를 BCrypt 로 해싱하고 대조한다.
///
/// **평문 비밀번호는 어디에도 저장하지 않는다.** DB 에는 이 클래스가 만든 해시만 들어간다.
/// 해시는 되돌릴 수 없으므로, 로그인은 "입력값을 같은 방식으로 해싱해서 맞춰보는" 방식으로 한다.
///
/// BCrypt 를 쓰는 이유: SHA-256 같은 범용 해시는 너무 빨라서 무차별 대입에 약하다.
/// BCrypt 는 일부러 느리고, 해시마다 salt 가 자동으로 섞여 들어간다.
/// 그래서 같은 비밀번호라도 해시 결과가 매번 다르다.
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// 계산 비용. 1 올릴 때마다 계산 시간이 두 배가 된다.
    ///
    /// 12 는 최신 PC 에서 한 번에 약 0.2~0.3초 걸리는 값이다.
    /// 사람이 로그인할 때는 느껴지지 않으면서 무차별 대입은 충분히 느려진다.
    /// </summary>
    private const int WorkFactor = 12;

    /// <summary>
    /// 로그인 대상 계정이 없을 때 대신 대조해 볼 가짜 해시.
    ///
    /// 계정이 없다고 해서 곧바로 실패를 돌려주면, 응답이 돌아오는 **시간**만 보고도
    /// "이 이메일은 가입되어 있다/없다" 를 구분할 수 있다.
    /// 계정이 없을 때도 똑같이 한 번 대조해서 걸리는 시간을 비슷하게 만든다.
    /// </summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("araatti-dummy", WorkFactor);

    public static string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <summary>입력한 비밀번호가 저장된 해시와 맞는지.</summary>
    public static bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // 해시 형식이 깨져 있는 경우. 서버를 죽이지 않고 "틀렸다" 로 처리한다.
            return false;
        }
    }

    /// <summary>계정이 없을 때 시간을 맞추기 위해 호출한다. 결과는 항상 false 로 쓴다.</summary>
    public static void VerifyDummy(string password)
    {
        BCrypt.Net.BCrypt.Verify(password, DummyHash);
    }
}
