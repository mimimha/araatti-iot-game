using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 전체의 화면 순서를 한 곳에 모아둔 곳.
///
/// GAME_STRUCTURE.md 3장 — "씬 전환 코드는 민화가 관리합니다.
/// 각자 씬에서 SceneManager.LoadScene 을 직접 호출하지 않습니다."
///
/// 각 화면은 "다음으로 가 줘" 라고만 말하고, 어디가 다음인지는 이 파일이 안다.
///
///     SceneFlow.FromLogin();      // 로그인 화면에서 다음으로
///
/// 순서를 바꾸고 싶으면 아래 흐름 메서드만 고친다. 씬을 하나하나 열어볼 필요가 없다.
///
/// 전체 순서 (GAME_STRUCTURE.md 1장)
///
///     Boot → Title → Login → CharacterCreate → ChannelSelect → Lobby
///                              └ 이름이 이미 있으면 건너뛴다 (6장)
/// </summary>
public static class SceneFlow
{
    // ------------------------------------------------------------
    // 씬 이름
    //
    // Build Profiles 의 Scene List 에 등록된 이름과 똑같아야 한다.
    // ------------------------------------------------------------

    public const string Boot = "Boot";
    public const string Title = "Title";
    public const string Login = "Login";
    public const string CharacterCreate = "CharacterCreate";
    public const string ChannelSelect = "ChannelSelect";

    /// <summary>플레이어들이 모이는 허브 공간.</summary>
    public const string Lobby = "Lobby";

    /// <summary>
    /// 캐릭터 이름을 담아두는 PlayerPrefs 키.
    ///
    /// ⚠ GAME_STRUCTURE.md 6장에서 고정한 이름이다. 바꾸면 캐릭터 생성 화면과 연결이 끊긴다.
    /// </summary>
    public const string NicknameKey = "PlayerNickname";

    // ------------------------------------------------------------
    // 앞으로 가는 흐름
    // ------------------------------------------------------------

    /// <summary>Boot 초기화가 끝났을 때.</summary>
    public static void FromBoot() => Load(Title);

    /// <summary>Title 에서 [시작] 을 눌렀을 때.</summary>
    public static void FromTitle() => Load(Login);

    /// <summary>
    /// Login 에서 [로그인] / [회원가입] 을 눌렀을 때.
    ///
    /// 이 PC 에서 이미 캐릭터를 만들었다면 생성 화면을 건너뛴다. (6장 "두 번째 실행부터")
    /// </summary>
    public static void FromLogin() => Load(HasCharacter ? ChannelSelect : CharacterCreate);

    /// <summary>CharacterCreate 에서 [생성 완료] 가 처리된 뒤.</summary>
    public static void FromCharacterCreate() => Load(ChannelSelect);

    /// <summary>ChannelSelect 에서 채널 접속에 성공했을 때.</summary>
    public static void FromChannelSelect() => Load(Lobby);

    // ------------------------------------------------------------
    // 뒤로 가는 흐름
    // ------------------------------------------------------------

    /// <summary>Login 에서 [뒤로가기].</summary>
    public static void BackToTitle() => Load(Title);

    /// <summary>ChannelSelect 에서 [뒤로가기]. 캐릭터를 다시 만들러 간다.</summary>
    public static void BackToCharacterCreate() => Load(CharacterCreate);

    // ------------------------------------------------------------
    // 캐릭터 정보
    //
    // 이름과 외형은 이 PC 에만 저장한다. 서버에 저장하지 않는다. (11장)
    // ------------------------------------------------------------

    /// <summary>저장된 캐릭터 이름. 없으면 빈 문자열.</summary>
    public static string Nickname => PlayerPrefs.GetString(NicknameKey, string.Empty);

    /// <summary>이 PC 에서 캐릭터를 이미 만들었는지.</summary>
    public static bool HasCharacter => !string.IsNullOrWhiteSpace(Nickname);

    /// <summary>
    /// 접속에 쓸 이름. 비어 있으면 임시 이름을 준다.
    ///
    /// 정상 흐름에서는 CharacterCreate 가 빈 이름을 막으므로 비어 있을 수 없다.
    /// ChannelSelect 씬만 단독 실행했을 때를 위한 대비다.
    /// </summary>
    public static string NicknameOrDefault => HasCharacter ? Nickname : "선원";

    /// <summary>
    /// 저장된 캐릭터 이름을 지운다. 다음 로그인부터 CharacterCreate 가 다시 나온다.
    ///
    /// 개발 중 캐릭터 생성 화면을 다시 보려면 쓴다.
    /// (에디터 메뉴: Tools > 아라아띠 > 캐릭터 이름 지우기)
    /// </summary>
    public static void ClearCharacter()
    {
        PlayerPrefs.DeleteKey(NicknameKey);
        PlayerPrefs.Save();
        Debug.Log("[SceneFlow] 저장된 캐릭터 이름을 지웠습니다.");
    }

    // ------------------------------------------------------------
    // 공통
    // ------------------------------------------------------------

    /// <summary>
    /// 씬을 연다. 없는 씬이면 넘어가지 않고 원인을 알려준다.
    /// </summary>
    private static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneFlow] 씬 이름이 비어 있습니다. SceneFlow 의 씬 이름 상수를 확인해 주세요.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[SceneFlow] \"{sceneName}\" 씬을 찾을 수 없습니다. " +
                "File > Build Profiles 의 Scene List 에 등록되어 있는지 확인해 주세요.");
            return;
        }

        Debug.Log($"[SceneFlow] 씬 이동: {SceneManager.GetActiveScene().name} → {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
