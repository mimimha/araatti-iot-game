using System;
using System.Collections;
using UnityEngine;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 실제 서버(ASP.NET Core)와 통신하는 인증 서비스.
    ///
    /// 화면 코드는 IAuthService 만 쓰기 때문에, 이 클래스로 바꿔도 화면은 고치지 않는다.
    /// 갈아끼우는 지점은 AccountServiceBootstrap 한 곳이다.
    ///
    /// 서버 규격
    ///   POST /api/auth/signup  { email, password }  → 201, 사용자 정보 (토큰 없음)
    ///   POST /api/auth/login   { email, password }  → 200, { accessToken, expiresIn, user }
    ///
    /// ⚠ 토큰은 **메모리에만** 둔다. PlayerPrefs · 씬 · 프리팹에 저장하지 않는다.
    /// ⚠ 비밀번호는 요청을 보낸 뒤 어디에도 남기지 않는다. 로그로도 찍지 않는다.
    /// </summary>
    public class HttpAuthService : MonoBehaviour, IAuthService
    {
        [Header("서버")]
        [Tooltip("비워 두면 http://localhost:5080 을 쓴다. (HttpApiConfig.DefaultBaseUrl)")]
        [SerializeField] private string baseUrl = string.Empty;

        [Header("로그")]
        [Tooltip("켜면 요청한 메서드 · 경로 · 상태 코드를 Console 에 남긴다. 본문과 토큰은 찍지 않는다.")]
        [SerializeField] private bool verboseLogging = false;

        public event Action<bool, string> OnSignUpResult;
        public event Action<bool, string> OnLogInResult;

        private UserDto _currentUser;

        // ⚠ 메모리에만 존재한다. 앱을 끄면 사라지고, 다시 로그인해야 한다.
        private string _accessToken = string.Empty;

        public bool IsAuthenticated => _currentUser != null && !string.IsNullOrEmpty(_accessToken);

        public UserDto CurrentUser => _currentUser;

        public string AccessToken => _accessToken;

        private void Awake()
        {
            // 씬이 바뀌어도 살아남아야 한다. 로그인 상태를 유지해야 하기 때문.
            DontDestroyOnLoad(gameObject);
            AccountServiceLocator.Register(this);
            Debug.Log($"[HttpAuthService] 실제 서버 인증을 사용합니다. ({ResolvedBaseUrl})", this);
        }

        private void OnDestroy()
        {
            AccountServiceLocator.Unregister(this);
        }

        private string ResolvedBaseUrl =>
            string.IsNullOrWhiteSpace(baseUrl) ? HttpApiConfig.DefaultBaseUrl : baseUrl.Trim().TrimEnd('/');

        // ------------------------------------------------------------
        // 회원가입
        // ------------------------------------------------------------

        public void SignUp(string email, string password)
        {
            StartCoroutine(SignUpRoutine(email, password));
        }

        private IEnumerator SignUpRoutine(string email, string password)
        {
            string url = HttpApiConfig.Combine(ResolvedBaseUrl, HttpApiConfig.SignUpPath);
            string body = BuildCredentialsJson(email, password);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "POST", body, null, r => result = r);

            Log("POST", HttpApiConfig.SignUpPath, result);

            if (!result.IsSuccess)
            {
                OnSignUpResult?.Invoke(false, result.FailureMessage);
                yield break;
            }

            // 서버는 가입 응답에 토큰을 주지 않는다. 계정만 만들어진 상태다.
            // 토큰이 필요하면 부르는 쪽에서 이어서 LogIn 을 부른다. (LoginScreenController)
            OnSignUpResult?.Invoke(true, string.Empty);
        }

        // ------------------------------------------------------------
        // 로그인
        // ------------------------------------------------------------

        public void LogIn(string email, string password)
        {
            StartCoroutine(LogInRoutine(email, password));
        }

        private IEnumerator LogInRoutine(string email, string password)
        {
            string url = HttpApiConfig.Combine(ResolvedBaseUrl, HttpApiConfig.LogInPath);
            string body = BuildCredentialsJson(email, password);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "POST", body, null, r => result = r);

            Log("POST", HttpApiConfig.LogInPath, result);

            if (!result.IsSuccess)
            {
                OnLogInResult?.Invoke(false, result.FailureMessage);
                yield break;
            }

            if (!HttpJson.TryParse(result.Body, out LogInResponseBody parsed, out string parseFailure))
            {
                OnLogInResult?.Invoke(false, parseFailure);
                yield break;
            }

            if (string.IsNullOrEmpty(parsed.accessToken) || parsed.user == null)
            {
                OnLogInResult?.Invoke(false, "서버 응답에 로그인 정보가 없습니다.");
                yield break;
            }

            _accessToken = parsed.accessToken;
            _currentUser = parsed.user;

            OnLogInResult?.Invoke(true, string.Empty);
        }

        // ------------------------------------------------------------
        // 로그아웃
        // ------------------------------------------------------------

        /// <summary>
        /// 세션을 지운다.
        ///
        /// 서버에 알리지 않는다. 토큰은 만료 시간이 지나면 스스로 무효가 되고,
        /// 서버에는 토큰을 취소하는 API 가 없다. (Refresh Token 은 범위 밖)
        ///
        /// 캐릭터 서비스의 캐시까지 함께 비우려면 AccountServiceLocator.LogOut() 을 쓴다.
        /// </summary>
        public void LogOut()
        {
            _currentUser = null;
            _accessToken = string.Empty;
        }

        // ------------------------------------------------------------
        // 본문 만들기
        // ------------------------------------------------------------

        /// <summary>서버가 기대하는 { "email": ..., "password": ... } 를 만든다.</summary>
        [Serializable]
        private class CredentialsBody
        {
            public string email;
            public string password;
        }

        /// <summary>서버의 로그인 응답. (server/AraAtti.Api — LoginResponse)</summary>
        // JsonUtility 가 응답을 읽어 채우는 필드들이다. 코드에서 대입하는 곳이 없으므로
        // "값이 대입되지 않았다"(CS0649) 경고가 나는데, 여기서는 정상이라 끈다.
#pragma warning disable 0649
        [Serializable]
        private class LogInResponseBody
        {
            public string accessToken;
            public int expiresIn;
            public UserDto user;
        }
#pragma warning restore 0649

        private static string BuildCredentialsJson(string email, string password)
        {
            // 다듬는 규칙은 서버와 같다. 이메일은 앞뒤 공백을 떼고, 비밀번호는 그대로 보낸다.
            CredentialsBody payload = new CredentialsBody
            {
                email = email != null ? email.Trim() : string.Empty,
                password = password ?? string.Empty
            };

            return JsonUtility.ToJson(payload);
        }

        /// <summary>⚠ 본문과 토큰은 절대 찍지 않는다. 메서드 · 경로 · 상태 코드만 남긴다.</summary>
        private void Log(string method, string path, HttpJsonResult result)
        {
            if (!verboseLogging)
            {
                return;
            }

            Debug.Log(
                $"[HttpAuthService] {method} {path} → " +
                $"{(result.IsSuccess ? "성공" : "실패")} (HTTP {result.StatusCode})", this);
        }
    }
}
