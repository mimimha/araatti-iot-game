using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnderTheSea.Character;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 실제 서버(ASP.NET Core + MySQL)와 통신하는 캐릭터 서비스.
    ///
    /// 서버 규격
    ///   GET  /api/characters   Bearer  → 200, { characters: [...] }   (없으면 빈 배열)
    ///   POST /api/characters   Bearer  → 201, 만들어진 캐릭터 하나
    ///
    /// 토큰은 직접 들고 있지 않다. IAuthService 에서 그때그때 꺼내 쓴다.
    /// 그래서 이 클래스는 HttpAuthService 를 알지 못하고, Fake 인증과 섞어 써도 동작한다.
    /// </summary>
    public class HttpCharacterService : MonoBehaviour, ICharacterService
    {
        [Header("서버")]
        [Tooltip("비워 두면 http://localhost:5080 을 쓴다. (HttpApiConfig.DefaultBaseUrl)")]
        [SerializeField] private string baseUrl = string.Empty;

        [Header("로그")]
        [Tooltip("켜면 요청한 메서드 · 경로 · 상태 코드를 Console 에 남긴다. 토큰은 찍지 않는다.")]
        [SerializeField] private bool verboseLogging = false;

        public event Action<bool, CharacterDto[], string> OnMyCharactersResult;
        public event Action<bool, CharacterDto, string> OnCreateResult;

        private readonly List<CharacterDto> _characters = new List<CharacterDto>();

        public IReadOnlyList<CharacterDto> Characters => _characters;

        public bool HasFetched { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AccountServiceLocator.Register(this);
            Debug.Log($"[HttpCharacterService] 실제 서버 캐릭터 저장을 사용합니다. ({ResolvedBaseUrl})", this);
        }

        private void OnDestroy()
        {
            AccountServiceLocator.Unregister(this);
        }

        private string ResolvedBaseUrl =>
            string.IsNullOrWhiteSpace(baseUrl) ? HttpApiConfig.DefaultBaseUrl : baseUrl.Trim().TrimEnd('/');

        /// <summary>지금 쓸 수 있는 토큰. 로그인 전이면 빈 문자열.</summary>
        private static string AccessToken
        {
            get
            {
                IAuthService auth = AccountServiceLocator.Auth;
                return auth != null ? auth.AccessToken : string.Empty;
            }
        }

        // ------------------------------------------------------------
        // 목록
        // ------------------------------------------------------------

        public void RequestMyCharacters()
        {
            StartCoroutine(RequestMyCharactersRoutine());
        }

        private IEnumerator RequestMyCharactersRoutine()
        {
            string token = AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                OnMyCharactersResult?.Invoke(false, Array.Empty<CharacterDto>(), "로그인이 필요합니다.");
                yield break;
            }

            string url = HttpApiConfig.Combine(ResolvedBaseUrl, HttpApiConfig.CharactersPath);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "GET", null, token, r => result = r);

            Log("GET", HttpApiConfig.CharactersPath, result);

            if (!result.IsSuccess)
            {
                OnMyCharactersResult?.Invoke(false, Array.Empty<CharacterDto>(), result.FailureMessage);
                yield break;
            }

            if (!HttpJson.TryParse(result.Body, out CharacterListBody parsed, out string parseFailure))
            {
                OnMyCharactersResult?.Invoke(false, Array.Empty<CharacterDto>(), parseFailure);
                yield break;
            }

            _characters.Clear();
            if (parsed.characters != null)
            {
                foreach (CharacterDto character in parsed.characters)
                {
                    if (character != null)
                    {
                        _characters.Add(Normalize(character));
                    }
                }
            }

            HasFetched = true;

            // 캐릭터가 0개인 것은 실패가 아니다. 성공 + 빈 배열로 알린다.
            OnMyCharactersResult?.Invoke(true, _characters.ToArray(), string.Empty);
        }

        // ------------------------------------------------------------
        // 생성
        // ------------------------------------------------------------

        public void CreateCharacter(CharacterCreateRequest request)
        {
            StartCoroutine(CreateCharacterRoutine(request));
        }

        private IEnumerator CreateCharacterRoutine(CharacterCreateRequest request)
        {
            if (request == null)
            {
                OnCreateResult?.Invoke(false, null, "캐릭터 정보가 없습니다.");
                yield break;
            }

            string token = AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                OnCreateResult?.Invoke(false, null, "로그인이 필요합니다.");
                yield break;
            }

            string url = HttpApiConfig.Combine(ResolvedBaseUrl, HttpApiConfig.CharactersPath);

            // CharacterCreateRequest 의 필드 이름이 서버 본문과 같으므로 그대로 직렬화한다.
            // parts 는 CharacterPartSnapshot(slot · prefabName) 이라 변환이 없다.
            string body = JsonUtility.ToJson(request);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "POST", body, token, r => result = r);

            Log("POST", HttpApiConfig.CharactersPath, result);

            if (!result.IsSuccess)
            {
                // 서버가 준 이유를 그대로 올린다.
                // 예: "이미 캐릭터를 보유하고 있습니다." / "이미 사용 중인 닉네임입니다."
                OnCreateResult?.Invoke(false, null, result.FailureMessage);
                yield break;
            }

            if (!HttpJson.TryParse(result.Body, out CharacterDto created, out string parseFailure))
            {
                OnCreateResult?.Invoke(false, null, parseFailure);
                yield break;
            }

            created = Normalize(created);

            // 방금 만든 것이 목록에 바로 반영되어야 한다.
            _characters.Clear();
            _characters.Add(created);
            HasFetched = true;

            OnCreateResult?.Invoke(true, created, string.Empty);
        }

        // ------------------------------------------------------------
        // 세션 캐시
        // ------------------------------------------------------------

        /// <summary>메모리 캐시만 비운다. 서버 데이터는 건드리지 않는다.</summary>
        public void ClearSessionCache()
        {
            _characters.Clear();
            HasFetched = false;
        }

        // ------------------------------------------------------------
        // 파싱
        // ------------------------------------------------------------

        /// <summary>GET /api/characters 의 응답 껍데기. JsonUtility 는 최상위 배열을 못 읽는다.</summary>
        // JsonUtility 가 응답을 읽어 채우는 필드들이다. 코드에서 대입하는 곳이 없으므로
        // "값이 대입되지 않았다"(CS0649) 경고가 나는데, 여기서는 정상이라 끈다.
#pragma warning disable 0649
        [Serializable]
        private class CharacterListBody
        {
            public CharacterDto[] characters;
        }
#pragma warning restore 0649

        /// <summary>
        /// parts 가 null 로 들어오는 경우를 막는다.
        ///
        /// JsonUtility 는 응답에 parts 가 없으면 필드 초기값을 덮어써 null 로 만들 수 있다.
        /// 이후 코드가 foreach 를 돌기 때문에 빈 배열로 맞춰 둔다.
        /// </summary>
        private static CharacterDto Normalize(CharacterDto character)
        {
            if (character != null && character.parts == null)
            {
                character.parts = Array.Empty<CharacterPartSnapshot>();
            }

            return character;
        }

        /// <summary>⚠ 토큰은 절대 찍지 않는다. 메서드 · 경로 · 상태 코드만 남긴다.</summary>
        private void Log(string method, string path, HttpJsonResult result)
        {
            if (!verboseLogging)
            {
                return;
            }

            Debug.Log(
                $"[HttpCharacterService] {method} {path} → " +
                $"{(result.IsSuccess ? "성공" : "실패")} (HTTP {result.StatusCode})", this);
        }
    }
}
