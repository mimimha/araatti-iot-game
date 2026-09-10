using System;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 계정 인증의 경계.
    ///
    /// 규칙
    ///   - 화면은 HTTP 나 토큰을 직접 다루지 않고 이 인터페이스만 쓴다.
    ///   - 결과는 반드시 event 로 돌아온다. 요청 메서드는 값을 돌려주지 않는다.
    ///
    /// 이 모양은 기존 INetworkService 와 같은 방식이다. (요청 메서드 + 결과 event)
    /// 프로젝트에 비동기 관례가 두 가지 생기지 않게 하기 위함이다.
    ///
    /// ⚠ 실시간 접속(INetworkService)과 섞지 않는다. 저쪽은 Photon Fusion 세션이고
    ///    이쪽은 단발성 요청/응답이다. Fake 를 따로 켜고 끌 수 있어야 한다.
    ///
    /// 문서: docs/prd/auth-character-roadmap.md 4장
    /// </summary>
    public interface IAuthService
    {
        // ------------------------------------------------------------
        // 화면 → 서비스 (요청)
        // ------------------------------------------------------------

        /// <summary>회원가입. 결과는 OnSignUpResult 로 온다.</summary>
        void SignUp(string email, string password);

        /// <summary>로그인. 결과는 OnLogInResult 로 온다.</summary>
        void LogIn(string email, string password);

        /// <summary>로그아웃. 토큰과 현재 사용자를 지운다.</summary>
        void LogOut();

        // ------------------------------------------------------------
        // 현재 상태
        // ------------------------------------------------------------

        /// <summary>지금 로그인되어 있는지.</summary>
        bool IsAuthenticated { get; }

        /// <summary>로그인한 계정. 로그인 전이면 null.</summary>
        UserDto CurrentUser { get; }

        /// <summary>
        /// 액세스 토큰. 로그인 전이면 빈 문자열.
        ///
        /// 화면에서 쓸 일은 없다. 캐릭터 서비스가 요청에 실을 때만 쓴다.
        /// </summary>
        string AccessToken { get; }

        // ------------------------------------------------------------
        // 서비스 → 화면 (알림)
        // ------------------------------------------------------------

        /// <summary>회원가입 결과. (성공 여부, 실패했다면 사용자에게 보여줄 이유)</summary>
        event Action<bool, string> OnSignUpResult;

        /// <summary>로그인 결과. (성공 여부, 실패했다면 사용자에게 보여줄 이유)</summary>
        event Action<bool, string> OnLogInResult;
    }
}
