using UnityEngine;
using UnderTheSea.Character;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 서버 응답을 "현재 활성 캐릭터" 로 정하고, 로컬 캐시를 서버 값에 맞춘다.
    ///
    /// ── 누가 원본(authority)인가 ─────────────────────────────────
    ///
    ///   **서버(MySQL)가 원본이다.** 캐릭터가 있는지, 이름이 무엇인지, 외형이 어떤지는
    ///   모두 서버가 정한다. 로그인 후 어느 화면으로 갈지도 서버가 준 목록으로 판단한다.
    ///
    ///   **PlayerPrefs 는 그 응답의 캐시일 뿐이다.** 두 가지 목적으로만 남겨 둔다.
    ///     1. 기존 UI 호환 — SceneFlow.Nickname · ChannelSelectController 가 이 키를 읽는다.
    ///        (그 파일들을 고치지 않기 위해 캐시를 유지한다)
    ///     2. 로그인 없이 CharacterCreate 씬만 단독 실행할 때의 외형 복원 (PRD 01 경로)
    ///
    ///   그래서 **서버 응답이 오면 언제나 서버 값이 이긴다.** 캐시를 근거로 분기하지 않는다.
    ///
    /// ── 실패했을 때 ─────────────────────────────────────────────
    ///
    ///   이 클래스는 **성공한 응답에만** 호출된다. 통신 실패 · 파싱 실패에는 부르지 않는다.
    ///   실패했을 때 캐시를 지우면, 잠깐 서버가 죽은 것 때문에 멀쩡한 로컬 값을 잃는다.
    ///
    /// 문서: docs/prd/auth-character-roadmap.md (PRD 07)
    /// </summary>
    public static class CharacterSessionCache
    {
        /// <summary>
        /// 서버에서 받은 목록에서 현재 활성 캐릭터를 정한다. 없으면 null.
        ///
        ///   0개  → 캐시를 비운다. (이전 계정의 외형이 남아 보이면 안 된다)
        ///   1개  → 그 캐릭터를 활성으로 삼고 캐시를 서버 값으로 갱신한다.
        ///   2개+ → **아무것도 고르지 않는다.** 아래 주석 참고.
        /// </summary>
        public static CharacterDto SelectFromList(CharacterDto[] characters)
        {
            int count = characters != null ? characters.Length : 0;

            if (count == 0)
            {
                // 이 계정에는 캐릭터가 없다.
                // 캐시를 비우지 않으면 CharacterCreate 에서 **이전 계정의 외형과 이름**이
                // 복원되어 버린다. 계정이 바뀌었는데 남의 캐릭터가 보이는 상태가 된다.
                ClearLocalCache("서버에 캐릭터가 없습니다");
                return null;
            }

            if (count > 1)
            {
                // ★ 다중 캐릭터 확장 지점 ★
                //
                // 지금 정책(계정당 1개, 서버의 MaxCharactersPerUser)에서는 올 수 없는 상태다.
                // 그래서 **임의로 첫 번째를 고르지 않는다.** 잘못 고른 캐릭터로 게임에 들어가면
                // 사용자가 되돌릴 방법이 없고, 캐시까지 그 캐릭터로 덮어써 버린다.
                //
                // CharacterSelect 화면을 만들 때 할 일
                //   1. 여기서 고르지 말고 목록을 그대로 그 화면에 넘긴다.
                //   2. 사용자가 고르면 ICharacterService.SetCurrentCharacter() 를 부른다.
                //      그 안에서 이 클래스의 CacheAsCurrent() 가 캐시를 맞춘다.
                //   3. SceneFlow 에 CharacterSelect 상수와 분기를 추가한다.
                //      (SceneFlow.FromLogin(int) 의 "2개+" 자리)
                //
                // 서버 API 와 DB 스키마는 이미 1:N 이라 바꿀 것이 없다.
                Debug.LogWarning(
                    $"[CharacterSessionCache] 캐릭터가 {count}개입니다. " +
                    "지금 정책(계정당 1개)에서는 생기지 않는 상태라 활성 캐릭터를 정하지 않았습니다. " +
                    "CharacterSelect 화면이 필요합니다. 로컬 캐시는 건드리지 않았습니다.");
                return null;
            }

            return CacheAsCurrent(characters[0]);
        }

        /// <summary>
        /// 이 캐릭터를 현재 활성 캐릭터로 삼고 로컬 캐시를 그 값으로 맞춘다.
        ///
        /// 캐릭터를 새로 만들었을 때와, 나중에 선택 화면에서 골랐을 때 쓴다.
        /// 넘기는 값은 **서버가 돌려준 것**이어야 한다. 화면에서 만든 요청값을 넘기면
        /// 서버가 다듬은 결과(공백 제거 등)와 어긋난다.
        /// </summary>
        public static CharacterDto CacheAsCurrent(CharacterDto character)
        {
            if (character == null)
            {
                return null;
            }

            CharacterAppearanceSnapshot snapshot = CharacterAppearanceMapping.ToSnapshot(character);

            CharacterAppearanceStore.Save(snapshot);

            // ⚠ 키 이름을 바꾸지 않는다. SceneFlow.Nickname · ChannelSelectController 가 이 값을 읽는다.
            PlayerPrefs.SetString(SceneFlow.NicknameKey, snapshot.nickname ?? string.Empty);
            PlayerPrefs.Save();

            Debug.Log(
                $"[CharacterSessionCache] 서버 캐릭터를 로컬 캐시에 반영했습니다. " +
                $"이름 \"{snapshot.nickname}\", 파츠 {snapshot.parts.Length}개, 피부색 {snapshot.bodyColorHex}");

            return character;
        }

        /// <summary>
        /// 로컬 캐시만 비운다.
        ///
        /// ⚠ 서버 DB 와 Fake.Account.* 데이터는 건드리지 않는다.
        ///    지우는 것은 PlayerPrefs 의 두 키뿐이다.
        /// </summary>
        public static void ClearLocalCache(string reason)
        {
            CharacterAppearanceStore.Clear();

            PlayerPrefs.DeleteKey(SceneFlow.NicknameKey);
            PlayerPrefs.Save();

            Debug.Log(
                $"[CharacterSessionCache] 로컬 캐시를 비웠습니다. ({reason}) " +
                "서버 데이터는 그대로입니다.");
        }
    }
}
