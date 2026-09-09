using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnderTheSea.Character;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 서버 없이 캐릭터 목록·생성을 다루는 가짜 서비스.
    ///
    /// ⚠ 임시 구현입니다. PRD 06 에서 진짜 HTTP 구현으로 바꿉니다.
    /// ⚠ HTTP · UnityWebRequest 를 쓰지 않습니다. 전부 이 PC 안에서 끝납니다.
    ///
    /// 서버와 같은 규칙을 그대로 흉내냅니다.
    ///   - 계정당 캐릭터 1개 (MaxCharactersPerUser)
    ///   - 닉네임은 전역 중복 불가
    ///   - 닉네임 2~10자, 한글·영문·숫자
    ///   - 피부색 "#RRGGBB"
    ///   - 같은 slot 중복 불가
    /// </summary>
    public class FakeCharacterService : MonoBehaviour, ICharacterService
    {
        /// <summary>
        /// 가짜 캐릭터를 담아두는 PlayerPrefs 키.
        ///
        /// ⚠ PRD 01 의 "PlayerNickname" · "CharacterAppearanceSnapshotV1" 과 겹치지 않는다.
        ///    PRD 01 의 로컬 외형 저장은 이 서비스와 **별개로** 계속 동작한다.
        /// </summary>
        public const string CharactersKey = "Fake.Account.Characters";

        /// <summary>
        /// 계정당 만들 수 있는 캐릭터 수. 서버의 같은 이름 상수와 맞춘다.
        ///
        /// ★ 다중 캐릭터를 열려면 서버와 이 값을 함께 바꾼다.
        /// </summary>
        public const int MaxCharactersPerUser = 1;

        [Header("응답 지연 (초)")]
        [SerializeField, Range(0f, 3f)] private float responseDelay = 0.3f;

        [Header("실패 시뮬레이션 (테스트용)")]
        [SerializeField] private bool alwaysFail = false;

        [SerializeField] private string failReason = "서버에 연결할 수 없습니다.";

        public event Action<bool, CharacterDto[], string> OnMyCharactersResult;
        public event Action<bool, CharacterDto, string> OnCreateResult;

        private readonly List<CharacterDto> _characters = new List<CharacterDto>();

        public IReadOnlyList<CharacterDto> Characters => _characters;

        public bool HasFetched { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AccountServiceLocator.Register(this);
            Debug.Log("[FakeCharacterService] 가짜 캐릭터 서비스가 등록되었습니다. (임시 구현)", this);
        }

        private void OnDestroy()
        {
            AccountServiceLocator.Unregister(this);
        }

        // ------------------------------------------------------------
        // 목록
        // ------------------------------------------------------------

        public void RequestMyCharacters()
        {
            StartCoroutine(RespondMyCharacters());
        }

        private IEnumerator RespondMyCharacters()
        {
            yield return WaitUnscaled(responseDelay);

            if (alwaysFail)
            {
                OnMyCharactersResult?.Invoke(false, Array.Empty<CharacterDto>(), failReason);
                yield break;
            }

            if (!TryGetCurrentUserId(out long userId, out string authFailure))
            {
                OnMyCharactersResult?.Invoke(false, Array.Empty<CharacterDto>(), authFailure);
                yield break;
            }

            _characters.Clear();
            _characters.AddRange(LoadTable().OwnedBy(userId));
            HasFetched = true;

            // 캐릭터가 0개인 것은 실패가 아니다. 성공 + 빈 배열로 알린다.
            OnMyCharactersResult?.Invoke(true, _characters.ToArray(), string.Empty);
        }

        // ------------------------------------------------------------
        // 생성
        // ------------------------------------------------------------

        public void CreateCharacter(CharacterCreateRequest request)
        {
            StartCoroutine(RespondCreate(request));
        }

        private IEnumerator RespondCreate(CharacterCreateRequest request)
        {
            yield return WaitUnscaled(responseDelay);

            if (alwaysFail)
            {
                OnCreateResult?.Invoke(false, null, failReason);
                yield break;
            }

            if (!TryGetCurrentUserId(out long userId, out string authFailure))
            {
                OnCreateResult?.Invoke(false, null, authFailure);
                yield break;
            }

            string validationMessage = Validate(request);
            if (validationMessage != null)
            {
                OnCreateResult?.Invoke(false, null, validationMessage);
                yield break;
            }

            CharacterTable table = LoadTable();

            if (table.CountOwnedBy(userId) >= MaxCharactersPerUser)
            {
                OnCreateResult?.Invoke(false, null, "이미 캐릭터를 보유하고 있습니다.");
                yield break;
            }

            string nickname = request.nickname.Trim();
            if (table.IsNicknameTaken(nickname))
            {
                OnCreateResult?.Invoke(false, null, "이미 사용 중인 닉네임입니다.");
                yield break;
            }

            CharacterDto created = new CharacterDto
            {
                id = table.NextId(),
                nickname = nickname,
                skinColor = request.skinColor.Trim().ToUpperInvariant(),
                slotIndex = 0,
                parts = request.parts ?? Array.Empty<CharacterPartSnapshot>()
            };

            table.Add(userId, created);
            SaveTable(table);

            // 방금 만든 것이 목록에 바로 반영되어야 한다.
            _characters.Clear();
            _characters.AddRange(table.OwnedBy(userId));
            HasFetched = true;

            OnCreateResult?.Invoke(true, created, string.Empty);
        }

        // ------------------------------------------------------------
        // 세션 캐시
        // ------------------------------------------------------------

        /// <summary>
        /// 메모리 캐시만 비운다. PlayerPrefs 는 건드리지 않는다.
        ///
        /// 진행 중인 요청을 취소하지는 않는다. 취소할 필요가 없다 —
        /// RespondMyCharacters · RespondCreate 는 **지연이 끝난 뒤에** 로그인 상태를
        /// 확인하므로, 로그아웃 뒤에 도착한 응답은 캐시를 되살리지 못하고 실패로 끝난다.
        /// </summary>
        public void ClearSessionCache()
        {
            _characters.Clear();
            HasFetched = false;
        }

        // ------------------------------------------------------------
        // 검증 — 서버의 규칙과 같게 맞춘다
        // ------------------------------------------------------------

        private static readonly Regex NicknamePattern =
            new Regex("^[가-힣ㄱ-ㅎㅏ-ㅣA-Za-z0-9]+$", RegexOptions.Compiled);

        private static readonly Regex SkinColorPattern =
            new Regex("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        private static string Validate(CharacterCreateRequest request)
        {
            if (request == null)
            {
                return "캐릭터 정보가 없습니다.";
            }

            if (string.IsNullOrWhiteSpace(request.nickname))
            {
                return "닉네임을 입력해 주세요.";
            }

            string nickname = request.nickname.Trim();
            if (nickname.Length < 2 || nickname.Length > 10)
            {
                return "닉네임은 2~10자여야 합니다.";
            }

            if (!NicknamePattern.IsMatch(nickname))
            {
                return "닉네임은 한글·영문·숫자만 쓸 수 있습니다. (공백·특수문자 불가)";
            }

            if (string.IsNullOrWhiteSpace(request.skinColor) || !SkinColorPattern.IsMatch(request.skinColor.Trim()))
            {
                return "피부색은 \"#RRGGBB\" 형식이어야 합니다.";
            }

            if (request.parts == null)
            {
                return "parts 가 없습니다. 입힌 파츠가 없으면 빈 배열을 보내 주세요.";
            }

            HashSet<string> usedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CharacterPartSnapshot part in request.parts)
            {
                if (string.IsNullOrWhiteSpace(part.slot))
                {
                    return "파츠의 slot 이 비어 있습니다.";
                }

                if (string.IsNullOrWhiteSpace(part.prefabName))
                {
                    return "파츠의 prefabName 이 비어 있습니다.";
                }

                if (!usedSlots.Add(part.slot.Trim()))
                {
                    return $"같은 slot 이 두 번 들어 있습니다: \"{part.slot.Trim()}\"";
                }
            }

            return null;
        }

        private static bool TryGetCurrentUserId(out long userId, out string failureReason)
        {
            IAuthService auth = AccountServiceLocator.Auth;

            if (auth == null || !auth.IsAuthenticated || auth.CurrentUser == null)
            {
                userId = 0;
                failureReason = "로그인이 필요합니다.";
                return false;
            }

            userId = auth.CurrentUser.id;
            failureReason = string.Empty;
            return true;
        }

        // ------------------------------------------------------------
        // 저장
        // ------------------------------------------------------------

        /// <summary>누구의 캐릭터인지까지 담는다. 소유자 정보는 서버 응답에 없으므로 DTO 밖에 둔다.</summary>
        [Serializable]
        private class OwnedCharacter
        {
            public long ownerUserId;
            public CharacterDto character;
        }

        [Serializable]
        private class CharacterTable
        {
            public List<OwnedCharacter> owned = new List<OwnedCharacter>();

            public IEnumerable<CharacterDto> OwnedBy(long userId)
            {
                foreach (OwnedCharacter entry in owned)
                {
                    if (entry?.character != null && entry.ownerUserId == userId)
                    {
                        yield return entry.character;
                    }
                }
            }

            public int CountOwnedBy(long userId)
            {
                int count = 0;
                foreach (OwnedCharacter entry in owned)
                {
                    if (entry?.character != null && entry.ownerUserId == userId) count++;
                }

                return count;
            }

            public bool IsNicknameTaken(string nickname)
            {
                foreach (OwnedCharacter entry in owned)
                {
                    if (entry?.character != null
                        && string.Equals(entry.character.nickname, nickname, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Add(long userId, CharacterDto character)
            {
                owned.Add(new OwnedCharacter { ownerUserId = userId, character = character });
            }

            public long NextId()
            {
                long maximum = 0;
                foreach (OwnedCharacter entry in owned)
                {
                    if (entry?.character != null && entry.character.id > maximum) maximum = entry.character.id;
                }

                return maximum + 1;
            }
        }

        private static CharacterTable LoadTable()
        {
            string json = PlayerPrefs.GetString(CharactersKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new CharacterTable();
            }

            try
            {
                return JsonUtility.FromJson<CharacterTable>(json) ?? new CharacterTable();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[FakeCharacterService] 저장된 가짜 캐릭터를 읽지 못했습니다. 새로 시작합니다. " + exception.Message);
                return new CharacterTable();
            }
        }

        private static void SaveTable(CharacterTable table)
        {
            PlayerPrefs.SetString(CharactersKey, JsonUtility.ToJson(table));
            PlayerPrefs.Save();
        }

        /// <summary>개발 중 가짜 캐릭터를 전부 지우고 싶을 때 쓴다.</summary>
        public static void ClearStoredCharacters()
        {
            PlayerPrefs.DeleteKey(CharactersKey);
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
