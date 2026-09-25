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

        /// <summary>
        /// 서버 주소를 실행할 때 바꾸는 인자.
        ///
        ///     AraAtti-Flow.exe -api http://192.168.0.10:5080
        ///
        /// <b>왜 필요한가.</b> 기본값이 <c>localhost</c> 라, 다른 PC 에서 실행하면
        /// <b>자기 PC 를 찾아가 로그인에 실패한다.</b> 팀원이 내 PC 의 계정 서버로 붙어
        /// 같은 DB 를 쓰게 하려면 주소를 바깥에서 줄 수 있어야 한다.
        ///
        /// ⚠ <b>인자가 없으면 아무것도 달라지지 않는다.</b> 기본값 그대로다.
        ///    그래서 각자 로컬 서버를 띄워 쓰던 방식이 그대로 살아 있다.
        ///
        /// ⚠ <b>평문 http 로 다른 PC 를 가리키려면 Player Settings 를 같이 열어야 한다.</b>
        ///    Unity 는 <c>localhost</c> 만 예외로 허용하고, <c>192.168.x.x</c> 같은 주소로 가는
        ///    평문 http 를 <c>InvalidOperationException: Insecure connection not allowed</c> 로 막는다.
        ///    이 인자만 주면 주소는 바뀜지지만 <b>전송 단계에서 거부된다.</b> 실측했다.
        ///    풀려면 <c>Player Settings → Allow downloads over HTTP</c> 를 바꿀야 하는데,
        ///    그것은 <c>ProjectSettings.asset</c> 이라 팀 전체에 영향을 준다.
        ///    한번만 확인할 때는 받는 PC 에서 포트프록시를 쓰는 편이 싸다.
        /// </summary>
        public const string BaseUrlKey = "-api";

        public const string SignUpPath = "/api/auth/signup";
        public const string LogInPath = "/api/auth/login";
        public const string CharactersPath = "/api/characters";

        /// <summary>내 인벤토리. 가진 것이 없으면 200 + 빈 배열이다 (404 아님).</summary>
        public const string InventoryPath = "/api/inventory";

        /// <summary>미니게임을 이긴 보상. 조각 1개. (server InventoryEndpoints.ClearRewardAsync)</summary>
        public const string ClearRewardPath = "/api/inventory/clear-reward";

        /// <summary>제단 상태. 파생값(남은 칸 · 최대 봉헌량 · 회복률)까지 서버가 계산해서 준다.</summary>
        public const string AltarStatePath = "/api/altar/state";

        /// <summary>봉헌. 409 실패에도 최신 상태가 함께 온다.</summary>
        public const string AltarOfferPath = "/api/altar/offer";

        /// <summary>광산 결과 한 줄 평. 서버가 LLM 을 부른다. (MINE.md 7장 — 아직 서버에 없다)</summary>
        public const string MineReviewPath = "/api/mine/review";

        /// <summary>
        /// <b>지금 실제로 쓸 서버 주소.</b> 정하는 순서는 하나뿐이다.
        ///
        /// <code>
        ///   1. 실행 인자 -api        다른 PC 에서 내 서버로 붙을 때
        ///   2. DefaultBaseUrl        인자가 없으면 예전 그대로
        /// </code>
        ///
        /// 인스펙터의 "서버 주소" 칸은 이것보다 우선한다. 각 서비스가 자기 칸을 먼저 본다.
        ///
        /// 한 번 정해지면 바뀌지 않으므로 처음 한 번만 읽고 들고 있는다.
        /// </summary>
        public static string EffectiveBaseUrl
        {
            get
            {
                if (resolved != null)
                {
                    return resolved;
                }

                string fromArgs = FusionLaunchArguments.GetString(BaseUrlKey, null);

                resolved = string.IsNullOrWhiteSpace(fromArgs)
                    ? DefaultBaseUrl
                    : fromArgs.Trim().TrimEnd('/');

                if (resolved != DefaultBaseUrl)
                {
                    UnityEngine.Debug.Log($"[HttpApiConfig] 계정 서버 주소를 실행 인자로 받았습니다 — {resolved}");
                }

                return resolved;
            }
        }

        private static string resolved;

        /// <summary>주소 끝의 "/" 가 겹치지 않게 이어 붙인다.</summary>
        public static string Combine(string baseUrl, string path)
        {
            string trimmed = string.IsNullOrWhiteSpace(baseUrl)
                ? EffectiveBaseUrl
                : baseUrl.Trim().TrimEnd('/');

            return trimmed + path;
        }
    }
}
