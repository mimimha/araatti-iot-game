using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 서버 없이 로그인 흐름을 만들고 테스트하기 위한 가짜 인증 서비스.
    ///
    /// ⚠ 임시 구현입니다. PRD 06 에서 진짜 HTTP 구현이 나오면
    ///    AccountServiceBootstrap 에서 이 컴포넌트만 바꾸면 됩니다.
    ///    화면 코드는 IAuthService 만 쓰기 때문에 고칠 필요가 없습니다.
    ///
    /// ⚠ HTTP · UnityWebRequest · 진짜 JWT 를 쓰지 않습니다. 전부 이 PC 안에서 끝납니다.
    ///
    /// 실패 메시지를 서버(server/AraAtti.Api)와 **똑같은 문구**로 맞춰 두었습니다.
    /// 진짜로 바꿔도 화면에 뜨는 글이 달라지지 않게 하기 위함입니다.
    /// </summary>
    public class FakeAuthService : MonoBehaviour, IAuthService
    {
        /// <summary>
        /// 가짜 계정을 담아두는 PlayerPrefs 키.
        ///
        /// ⚠ PRD 01 이 쓰는 "PlayerNickname" · "CharacterAppearanceSnapshotV1" 과 겹치지 않는다.
        ///    접두사 "Fake.Account." 는 이 가짜 구현 전용이며, 진짜 서버로 바꾸면 쓰이지 않는다.
        /// </summary>
        public const string AccountsKey = "Fake.Account.Users";

        [Header("응답 지연 (초)")]
        [Tooltip("서버가 답할 때까지 걸리는 시간을 흉내낸다. 로딩 표시를 확인할 때 늘린다.")]
        [SerializeField, Range(0f, 3f)] private float responseDelay = 0.4f;

        [Header("실패 시뮬레이션 (테스트용)")]
        [Tooltip("켜면 로그인과 회원가입이 항상 실패한다. 실패 화면을 확인할 때 쓴다.")]
        [SerializeField] private bool alwaysFail = false;

        [SerializeField] private string failReason = "서버에 연결할 수 없습니다.";

        public event Action<bool, string> OnSignUpResult;
        public event Action<bool, string> OnLogInResult;

        private UserDto _currentUser;
        private string _accessToken = string.Empty;

        public bool IsAuthenticated => _currentUser != null;

        public UserDto CurrentUser => _currentUser;

        public string AccessToken => _accessToken;

        private void Awake()
        {
            // 씬이 바뀌어도 살아남아야 한다. 로그인 상태를 유지해야 하기 때문.
            DontDestroyOnLoad(gameObject);
            AccountServiceLocator.Register(this);
            Debug.Log("[FakeAuthService] 가짜 인증 서비스가 등록되었습니다. (임시 구현)", this);
        }

        private void OnDestroy()
        {
            AccountServiceLocator.Unregister(this);
        }

        // ------------------------------------------------------------
        // 요청
        // ------------------------------------------------------------

        public void SignUp(string email, string password)
        {
            StartCoroutine(RespondSignUp(email, password));
        }

        private IEnumerator RespondSignUp(string email, string password)
        {
            yield return WaitUnscaled(responseDelay);

            if (alwaysFail)
            {
                OnSignUpResult?.Invoke(false, failReason);
                yield break;
            }

            string validationMessage = Validate(email, password);
            if (validationMessage != null)
            {
                OnSignUpResult?.Invoke(false, validationMessage);
                yield break;
            }

            string normalized = Normalize(email);
            AccountTable table = LoadAccounts();

            if (table.Find(normalized) != null)
            {
                OnSignUpResult?.Invoke(false, "이미 가입된 이메일입니다.");
                yield break;
            }

            table.Add(new FakeAccount
            {
                id = table.NextId(),
                email = normalized,
                passwordHash = HashPassword(password)
            });
            SaveAccounts(table);

            // 서버와 같다: 가입은 계정만 만들고 토큰을 주지 않는다.
            // 토큰이 필요하면 이어서 LogIn 을 부른다.
            OnSignUpResult?.Invoke(true, string.Empty);
        }

        public void LogIn(string email, string password)
        {
            StartCoroutine(RespondLogIn(email, password));
        }

        private IEnumerator RespondLogIn(string email, string password)
        {
            yield return WaitUnscaled(responseDelay);

            if (alwaysFail)
            {
                OnLogInResult?.Invoke(false, failReason);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                OnLogInResult?.Invoke(false, "이메일과 비밀번호를 입력해 주세요.");
                yield break;
            }

            FakeAccount account = LoadAccounts().Find(Normalize(email));

            // 계정이 없을 때와 비밀번호가 틀렸을 때가 **같은 메시지**여야 한다.
            // 다르게 답하면 어떤 이메일이 가입되어 있는지 알아낼 수 있다. (서버도 같은 규칙)
            if (account == null || account.passwordHash != HashPassword(password))
            {
                OnLogInResult?.Invoke(false, "이메일 또는 비밀번호가 올바르지 않습니다.");
                yield break;
            }

            _currentUser = new UserDto { id = account.id, email = account.email };
            _accessToken = "fake-token-" + account.id + "-" + Guid.NewGuid().ToString("N");

            OnLogInResult?.Invoke(true, string.Empty);
        }

        public void LogOut()
        {
            _currentUser = null;
            _accessToken = string.Empty;
        }

        // ------------------------------------------------------------
        // 검증 — 서버(server/AraAtti.Api)의 규칙과 같게 맞춘다
        // ------------------------------------------------------------

        private static readonly Regex EmailPattern =
            new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private const int MinimumPasswordLength = 8;

        private static string Validate(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "이메일을 입력해 주세요.";
            }

            if (!EmailPattern.IsMatch(email.Trim()))
            {
                return "이메일 형식이 올바르지 않습니다.";
            }

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

        /// <summary>대소문자를 섞어 입력해도 같은 계정으로 찾도록 맞춘다. (서버도 같다)</summary>
        private static string Normalize(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// 비밀번호를 그대로 저장하지 않기 위한 해싱.
        ///
        /// ⚠ 이것은 **보안이 아니다.** 가짜 구현이 개발자 PC 의 PlayerPrefs 에
        ///    비밀번호 원문을 남기지 않게 하려는 것뿐이다.
        ///    진짜 비밀번호 처리는 서버가 BCrypt 로 한다.
        /// </summary>
        private static string HashPassword(string password)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hash);
        }

        // ------------------------------------------------------------
        // 저장
        // ------------------------------------------------------------

        [Serializable]
        private class FakeAccount
        {
            public long id;
            public string email;
            public string passwordHash;
        }

        /// <summary>JsonUtility 는 배열을 통째로 직렬화하지 못한다. 그래서 감싸는 클래스가 필요하다.</summary>
        [Serializable]
        private class AccountTable
        {
            public List<FakeAccount> accounts = new List<FakeAccount>();

            public FakeAccount Find(string normalizedEmail)
            {
                foreach (FakeAccount account in accounts)
                {
                    if (account != null && account.email == normalizedEmail)
                    {
                        return account;
                    }
                }

                return null;
            }

            public void Add(FakeAccount account) => accounts.Add(account);

            public long NextId()
            {
                long maximum = 0;
                foreach (FakeAccount account in accounts)
                {
                    if (account != null && account.id > maximum) maximum = account.id;
                }

                return maximum + 1;
            }
        }

        private static AccountTable LoadAccounts()
        {
            string json = PlayerPrefs.GetString(AccountsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new AccountTable();
            }

            try
            {
                return JsonUtility.FromJson<AccountTable>(json) ?? new AccountTable();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[FakeAuthService] 저장된 가짜 계정을 읽지 못했습니다. 새로 시작합니다. " + exception.Message);
                return new AccountTable();
            }
        }

        private static void SaveAccounts(AccountTable table)
        {
            PlayerPrefs.SetString(AccountsKey, JsonUtility.ToJson(table));
            PlayerPrefs.Save();
        }

        /// <summary>개발 중 가짜 계정을 전부 지우고 싶을 때 쓴다.</summary>
        public static void ClearStoredAccounts()
        {
            PlayerPrefs.DeleteKey(AccountsKey);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------

        private static IEnumerator WaitUnscaled(float seconds)
        {
            if (seconds <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
