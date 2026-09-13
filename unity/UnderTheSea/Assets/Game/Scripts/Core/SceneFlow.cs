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

    /// <summary>
    /// Login 에서 로그인에 성공하고 **서버에서 캐릭터 개수를 받아왔을 때.**
    ///
    /// 위의 인자 없는 FromLogin() 과 달리 이 PC 의 PlayerPrefs 를 보지 않는다.
    /// 다른 컴퓨터에서 로그인해도 같은 결과가 나오게 하려면 이쪽을 쓴다.
    ///
    ///     0개  → CharacterCreate   아직 캐릭터가 없다
    ///     1개  → ChannelSelect     바로 접속하러 간다
    ///     2개+ → ChannelSelect     CharacterSelect 화면이 생기면 그쪽으로 바꾼다
    /// </summary>
    public static void FromLogin(int characterCount)
    {
        if (characterCount <= 0)
        {
            Load(CharacterCreate);
            return;
        }

        if (characterCount > 1)
        {
            // 지금은 계정당 1개만 만들 수 있으므로 정상 흐름에서는 오지 않는다.
            // 다중 캐릭터를 열었는데 CharacterSelect 씬이 아직 없을 때를 위한 대비다.
            Debug.LogWarning(
                $"[SceneFlow] 캐릭터가 {characterCount}개입니다. " +
                "CharacterSelect 화면이 아직 없어 ChannelSelect 로 보냅니다.");
        }

        Load(ChannelSelect);
    }

    /// <summary>CharacterCreate 에서 [생성 완료] 가 처리된 뒤.</summary>
    public static void FromCharacterCreate() => Load(ChannelSelect);

    /// <summary>
    /// 네트워크가 Lobby 를 직접 로드했는가.
    ///
    /// Fusion 으로 채널에 접속하면 <b>Fusion 이 Lobby 를 네트워크 씬으로 올린다.</b>
    /// 그 상태에서 여기서 또 <c>SceneManager.LoadScene("Lobby")</c> 를 하면
    /// 네트워크 오브젝트가 붙지 않은 Lobby 가 덮어써져 캐릭터가 사라진다.
    ///
    /// 그래서 <b>로드 주체를 하나로 정한다</b> — 이 값이 true 면 Fusion 이 주체다.
    /// 값을 켜고 끄는 것은 네트워크 계층의 일이다. (FusionNetworkService)
    /// 이 파일은 Fusion 을 알지 못한 채 bool 하나만 본다.
    ///
    /// Fake 경로에서는 늘 false 이므로 예전처럼 여기서 Lobby 를 연다.
    /// </summary>
    public static bool LobbyLoadedByNetwork { get; set; }

    /// <summary>ChannelSelect 에서 채널 접속에 성공했을 때.</summary>
    public static void FromChannelSelect()
    {
        if (LobbyLoadedByNetwork)
        {
            Debug.Log("[SceneFlow] Lobby 는 네트워크가 이미 로드했습니다. 씬을 다시 열지 않습니다.");
            return;
        }

        Load(Lobby);
    }

    /// <summary>
    /// 네트워크가 Lobby 를 <b>추가로</b> 올린 뒤, 그때까지 보이던 화면 씬을 내린다.
    ///
    /// ⚠ 왜 필요한가.
    ///    Fusion 은 PeerMode.Multiple 에서 네트워크 씬을 <b>additive 로</b> 올리고
    ///    이전 씬을 내리지 않는다. 그래서 ChannelSelect 가 그대로 남는다.
    ///    카메라는 꺼도 <c>ScreenSpaceOverlay</c> Canvas 는 카메라와 무관하게 계속 그려지므로
    ///    Lobby 가 멀쩡히 로드됐는데도 화면에는 ChannelSelect UI 만 보인다.
    ///    실제로 그 상태였다 — 로그는 전부 정상인데 화면만 안 넘어갔다.
    ///
    ///    <c>SceneManager.LoadScene</c>(Single)과 달리 Unity 가 알아서 치워 주지 않으므로
    ///    씬을 올린 쪽이 아니라 <b>흐름을 아는 이 파일이</b> 내린다.
    /// </summary>
    public static void UnloadScreenScene(Scene screen)
    {
        if (!screen.IsValid() || !screen.isLoaded)
        {
            return;
        }

        if (screen.name == Lobby)
        {
            // 방금 올린 Lobby 를 실수로 내리지 않는다.
            Debug.LogWarning("[SceneFlow] Lobby 를 내리려 했습니다. 건너뜁니다.");
            return;
        }

        if (SceneManager.sceneCount <= 1)
        {
            // 마지막 남은 씬은 내릴 수 없다.
            Debug.LogWarning($"[SceneFlow] '{screen.name}' 이(가) 마지막 씬이라 내리지 않습니다.");
            return;
        }

        Debug.Log($"[SceneFlow] 네트워크가 Lobby 를 올렸으므로 '{screen.name}' 씬을 내립니다.");
        SceneManager.UnloadSceneAsync(screen);
    }

    // ------------------------------------------------------------
    // 뒤로 가는 흐름
    // ------------------------------------------------------------

    /// <summary>
    /// 네트워크가 Lobby 를 쥐고 있다는 표시를 푼다.
    ///
    /// ⚠ 이 값이 true 로 남아 있으면 <see cref="FromChannelSelect"/> 가 Lobby 를 열지 않는다.
    ///    Fake 로 되돌렸을 때 "입장을 눌러도 아무 일도 안 일어나는" 회귀가 바로 이것이다.
    ///    그래서 게임 흐름에서 **물러날 때마다** 반드시 푼다.
    ///    네트워크 쪽에서도 접속 실패 · 연결 끊김 · Runner 종료 때 푼다.
    /// </summary>
    private static void ReleaseNetworkLobby()
    {
        if (LobbyLoadedByNetwork)
        {
            Debug.Log("[SceneFlow] 네트워크 Lobby 표시를 해제합니다.");
            LobbyLoadedByNetwork = false;
        }
    }

    /// <summary>
    /// 플레이 모드에 들어갈 때마다 비운다.
    /// 에디터에서 "Reload Domain" 을 꺼 두면 static 이 이전 플레이의 값을 그대로 들고 있다.
    /// 그 상태로 Fake 로 바꿔서 실행하면 Lobby 가 열리지 않는다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        LobbyLoadedByNetwork = false;
    }

    /// <summary>Login 에서 [뒤로가기].</summary>
    public static void BackToTitle()
    {
        ReleaseNetworkLobby();
        Load(Title);
    }

    /// <summary>
    /// ChannelSelect 에서 [뒤로가기]. 로그인 화면으로 돌아간다.
    ///
    /// ⚠ 로그아웃은 여기서 하지 않는다. 이 파일은 씬 전환만 담당한다.
    ///    (GAME_STRUCTURE.md 3장) 세션을 지우는 것은 부르는 쪽의 일이다.
    /// </summary>
    public static void BackToLogin()
    {
        ReleaseNetworkLobby();
        Load(Login);
    }

    /// <summary>
    /// CharacterCreate 로 되돌아간다.
    ///
    /// ⚠ 계정 흐름이 생긴 뒤로 ChannelSelect 의 [뒤로가기] 는 BackToLogin() 을 쓴다.
    ///    지금 부르는 곳은 없지만, 캐릭터를 다시 만들러 보내야 하는 화면이
    ///    생길 때를 위해 남겨 둔다.
    /// </summary>
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
