namespace UnderTheSea.Account
{
    /// <summary>
    /// 이메일 · 비밀번호 입력 규칙.
    ///
    /// **서버(server/AraAtti.Api, AuthEndpoints.ValidateSignup)와 같은 규칙, 같은 문구다.**
    /// 화면이 요청을 보내기 전에 미리 걸러 주고, <see cref="FakeAuthService"/> 도 이 규칙으로 검증한다.
    /// 최종 판정은 여전히 서버가 한다. 여기서 통과해도 서버가 거절할 수 있고, 그 메시지도 화면에 그대로 띄운다.
    ///
    /// 규칙을 바꾸려면 서버와 이 파일을 **함께** 고친다. 한쪽만 바꾸면 화면 안내와 실제 결과가 어긋난다.
    ///
    /// 이메일 형식 판정은 서버의 <c>EmailAddressAttribute</c> 와 같게 맞췄다.
    ///   - <c>@</c> 가 정확히 하나
    ///   - <c>@</c> 앞뒤가 비어 있지 않음
    ///   - 공백 문자 없음
    /// 도메인에 점(<c>.</c>)이 있어야 한다는 조건은 서버에 없으므로 여기서도 요구하지 않는다.
    /// </summary>
    public static class CredentialRules
    {
        public const int MinimumPasswordLength = 8;
        public const int MaximumEmailLength = 190;

        /// <summary>이메일이 규칙에 맞지 않으면 화면에 띄울 문구를, 맞으면 null 을 돌려준다.</summary>
        public static string ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "이메일을 입력해 주세요.";
            }

            string normalized = Normalize(email);

            if (normalized.Length > MaximumEmailLength)
            {
                return $"이메일은 {MaximumEmailLength}자 이하여야 합니다.";
            }

            if (!LooksLikeEmail(normalized))
            {
                return "이메일 형식이 올바르지 않습니다.";
            }

            return null;
        }

        /// <summary>비밀번호가 규칙에 맞지 않으면 화면에 띄울 문구를, 맞으면 null 을 돌려준다.</summary>
        public static string ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return "비밀번호를 입력해 주세요.";
            }

            if (password.Length < MinimumPasswordLength)
            {
                return $"비밀번호는 {MinimumPasswordLength}자 이상이어야 합니다.";
            }

            return null;
        }

        /// <summary>
        /// 둘을 한 번에 검사한다. 서버와 같은 순서(이메일 → 비밀번호)로 **첫 번째** 문제만 돌려준다.
        /// 칸별로 따로 보여 주려면 <see cref="ValidateEmail"/> · <see cref="ValidatePassword"/> 를 각각 부른다.
        /// </summary>
        public static string Validate(string email, string password)
        {
            return ValidateEmail(email) ?? ValidatePassword(password);
        }

        /// <summary>저장 · 조회에 쓰는 형태. 대소문자를 섞어 입력해도 같은 계정으로 찾는다. (서버도 같다)</summary>
        public static string Normalize(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static bool LooksLikeEmail(string email)
        {
            int at = email.IndexOf('@');

            if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
            {
                return false;
            }

            foreach (char c in email)
            {
                if (char.IsWhiteSpace(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
