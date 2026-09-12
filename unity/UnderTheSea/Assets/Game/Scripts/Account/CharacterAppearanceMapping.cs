using System;
using UnderTheSea.Character;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 로컬 외형 저장 형식과 서비스 요청 형식 사이를 옮겨 담는 곳.
    ///
    /// 두 형식은 사실상 같지만 **색 필드의 이름 하나가 다르다.**
    ///
    ///     로컬 (PRD 01)  CharacterAppearanceSnapshot.bodyColorHex
    ///     서비스 · 서버   CharacterCreateRequest.skinColor
    ///
    /// 담는 값은 양쪽 모두 **"#RRGGBB" 문자열 하나**다. 팔레트 인덱스가 아니다.
    /// 이름이 갈라진 곳은 여기 한 군데뿐이므로, 옮겨 담는 코드도 여기에만 둔다.
    /// 화면 코드가 직접 필드를 베껴 넣기 시작하면 규칙이 여러 곳으로 흩어진다.
    ///
    /// parts 는 양쪽이 같은 타입(CharacterPartSnapshot)을 쓰므로 변환이 없다.
    /// </summary>
    public static class CharacterAppearanceMapping
    {
        /// <summary>지금 화면에서 만든 외형을 캐릭터 생성 요청으로 바꾼다. (Unity → 서버)</summary>
        public static CharacterCreateRequest ToCreateRequest(CharacterAppearanceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new CharacterCreateRequest();
            }

            return new CharacterCreateRequest(
                snapshot.nickname,
                snapshot.bodyColorHex,
                snapshot.parts);
        }

        /// <summary>
        /// 서버가 돌려준 캐릭터를 로컬 외형 저장 형식으로 바꾼다. (서버 → Unity)
        ///
        /// 이 방향이 있어야 **다른 PC 에서 로그인해도 같은 외형**이 나온다.
        /// 로컬에 저장된 것이 없어도 서버 값으로 다시 만들 수 있기 때문이다.
        /// </summary>
        public static CharacterAppearanceSnapshot ToSnapshot(CharacterDto character)
        {
            if (character == null)
            {
                return null;
            }

            return new CharacterAppearanceSnapshot
            {
                nickname = character.nickname,
                bodyColorHex = character.skinColor,
                parts = character.parts ?? Array.Empty<CharacterPartSnapshot>()
            };
        }
    }
}
