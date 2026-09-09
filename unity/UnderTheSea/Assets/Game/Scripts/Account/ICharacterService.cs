using System;
using System.Collections.Generic;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 캐릭터 저장소의 경계.
    ///
    /// ⚠ 캐릭터는 **언제나 컬렉션**으로 다룬다. 지금은 계정당 1개지만,
    ///    나중에 CharacterSelect 화면과 다중 캐릭터를 붙일 때 이 인터페이스를
    ///    고치지 않기 위해서다. 하나만 돌려주는 메서드를 만들지 않는다.
    ///
    /// 문서: docs/prd/auth-character-roadmap.md 3-3 · 4장
    /// </summary>
    public interface ICharacterService
    {
        // ------------------------------------------------------------
        // 화면 → 서비스 (요청)
        // ------------------------------------------------------------

        /// <summary>내 캐릭터 목록을 요청한다. 결과는 OnMyCharactersResult 로 온다.</summary>
        void RequestMyCharacters();

        /// <summary>캐릭터를 만든다. 결과는 OnCreateResult 로 온다.</summary>
        void CreateCharacter(CharacterCreateRequest request);

        /// <summary>
        /// 지금 로그인 세션에 딸린 **메모리 캐시만** 비운다.
        ///
        /// 로그아웃할 때 부른다. 비우지 않으면 다음 사람이 로그인하기 전까지
        /// Characters 가 이전 계정의 목록을 계속 돌려준다.
        ///
        /// ⚠ 저장된 계정 · 캐릭터 · 외형 데이터는 **절대 지우지 않는다.**
        ///    지우는 것은 Characters 와 HasFetched 뿐이다.
        ///
        /// 직접 부르지 말고 AccountServiceLocator.LogOut() 을 쓴다.
        /// 인증 세션과 캐시가 따로 지워지면 상태가 어긋난다.
        /// </summary>
        void ClearSessionCache();

        // ------------------------------------------------------------
        // 현재 상태
        // ------------------------------------------------------------

        /// <summary>
        /// 마지막으로 받아온 목록. 한 번도 받지 않았으면 비어 있다.
        ///
        /// 캐릭터가 없는 것과 아직 안 받아온 것을 구분하려면 HasFetched 를 함께 본다.
        /// </summary>
        IReadOnlyList<CharacterDto> Characters { get; }

        /// <summary>목록을 한 번이라도 받아왔는지.</summary>
        bool HasFetched { get; }

        /// <summary>
        /// 지금 조작 중인 캐릭터. 고르지 않았으면 null.
        ///
        /// 계정당 1개인 지금은 목록을 받을 때 **1개일 때만** 자동으로 정해진다.
        /// 0개면 null 이고, 2개 이상이면 **자동으로 고르지 않고** null 로 둔다.
        /// (임의로 첫 번째를 고르면 사용자가 되돌릴 수 없다 — CharacterSessionCache 참고)
        ///
        /// ★ CharacterSelect 화면이 생기면 그 화면이 SetCurrentCharacter 로 채운다.
        /// </summary>
        CharacterDto CurrentCharacter { get; }

        /// <summary>
        /// 활성 캐릭터를 직접 정한다. 로컬 캐시도 그 캐릭터의 값으로 맞춰진다.
        ///
        /// 지금은 부르는 곳이 없다. **다중 캐릭터 선택 화면의 연결 지점**이다.
        /// 넘기는 값은 서버가 돌려준 CharacterDto 여야 한다.
        /// </summary>
        void SetCurrentCharacter(CharacterDto character);

        // ------------------------------------------------------------
        // 서비스 → 화면 (알림)
        // ------------------------------------------------------------

        /// <summary>
        /// 목록 조회 결과. (성공 여부, 목록, 실패했다면 그 이유)
        ///
        /// 캐릭터가 0개인 것은 **실패가 아니다.** 성공 + 빈 배열로 온다.
        /// </summary>
        event Action<bool, CharacterDto[], string> OnMyCharactersResult;

        /// <summary>생성 결과. (성공 여부, 만들어진 캐릭터 또는 null, 실패했다면 그 이유)</summary>
        event Action<bool, CharacterDto, string> OnCreateResult;
    }
}
