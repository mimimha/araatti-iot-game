namespace UnderTheSea.Account
{
    /// <summary>
    /// 서버 주소와 경로를 한 곳에 모아둔 곳.
    ///
    /// 경로 문자열을 서비스 코드 안에 흩어 놓지 않는다. 서버에서 경로가 바뀌면
    /// 이 파일만 보면 된다. 실제 주소는 각 Http 서비스의 Inspector 필드로 덮어쓸 수 있다.
    ///
    /// 서버 규격: server/README.md 6장 · 7장
    /// </summary>
    public static class HttpApiConfig
    {
        /// <summary>
        /// 개발 기본값. server/AraAtti.Api/Properties/launchSettings.json 의 applicationUrl 과 같다.
        ///
        /// 다른 주소를 쓰려면 씬에 올린 Http 서비스 컴포넌트의 "서버 주소" 칸을 채운다.
        /// (비워 두면 이 값을 쓴다)
        /// </summary>
        public const string DefaultBaseUrl = "http://localhost:5080";

        public const string SignUpPath = "/api/auth/signup";
        public const string LogInPath = "/api/auth/login";
        public const string CharactersPath = "/api/characters";

        /// <summary>주소 끝의 "/" 가 겹치지 않게 이어 붙인다.</summary>
        public static string Combine(string baseUrl, string path)
        {
            string trimmed = string.IsNullOrWhiteSpace(baseUrl)
                ? DefaultBaseUrl
                : baseUrl.Trim().TrimEnd('/');

            return trimmed + path;
        }
    }
}
