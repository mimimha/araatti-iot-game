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
        /// <summary>지금 화면에서 만든 외형을 캐릭터 생성 요청으로 바꾼다.</summary>
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
    }
}
