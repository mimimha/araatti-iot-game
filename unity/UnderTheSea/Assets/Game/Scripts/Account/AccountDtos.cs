using System;
using UnderTheSea.Character;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 로그인한 계정.
    ///
    /// ⚠ 비밀번호는 담지 않는다. 서버도 응답에 넣지 않는다.
    /// </summary>
    [Serializable]
    public class UserDto
    {
        public long id;
        public string email;
    }

    /// <summary>
    /// 캐릭터 하나.
    ///
    /// 필드 이름을 서버 응답과 **똑같이** 맞췄다. (server/AraAtti.Api — CharacterResponse)
    /// 그래서 PRD 06 에서 실제 HTTP 로 바꿀 때 JsonUtility 로 그대로 받아 쓸 수 있고,
    /// 이름을 바꿔 담는 변환 코드가 필요 없다.
    ///
    /// parts 는 PRD 01 의 CharacterPartSnapshot 을 그대로 쓴다.
    /// 그 타입의 필드가 이미 slot · prefabName 이라 서버 계약과 일치한다.
    /// </summary>
    [Serializable]
    public class CharacterDto
    {
        public long id;
        public string nickname;

        /// <summary>"#RRGGBB". 팔레트 인덱스가 아니다.</summary>
        public string skinColor;

        /// <summary>목록에서의 순서. 지금은 항상 0.</summary>
        public int slotIndex;

        public CharacterPartSnapshot[] parts = Array.Empty<CharacterPartSnapshot>();
    }

    /// <summary>
    /// 캐릭터 생성 요청.
    ///
    /// 서버의 POST /api/characters 본문과 같은 모양이다.
    /// ⚠ 소유자(userId)를 담지 않는다. 주인은 토큰에서 정해진다.
    /// </summary>
    [Serializable]
    public class CharacterCreateRequest
    {
        public string nickname;
        public string skinColor;
        public CharacterPartSnapshot[] parts = Array.Empty<CharacterPartSnapshot>();

        public CharacterCreateRequest()
        {
        }

        public CharacterCreateRequest(string nickname, string skinColor, CharacterPartSnapshot[] parts)
        {
            this.nickname = nickname;
            this.skinColor = skinColor;
            this.parts = parts ?? Array.Empty<CharacterPartSnapshot>();
        }
    }
}
