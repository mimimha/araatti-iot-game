using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnderTheSea.Network;

/// <summary>
/// 로비에 채팅창을 띄우는 설치기. <see cref="LobbyChatView"/> 프리팹을 하나 만들어 둔다.
///
/// <b>왜 씬에 올려 두지 않는가.</b> <see cref="LobbyTutorial"/> 과 같은 이유다.
///   · Fusion 은 세션을 시작하면서 씬을 러너 전용 씬으로 인수한다. 그때 없어지거나 옮겨진다
///   · 큰 Lobby 씬을 건드리지 않아도 된다 (CONVENTION.md 3장 — 같은 Main 씬을 여럿이 고치지 않는다)
///
/// 그래서 <see cref="Object.DontDestroyOnLoad"/> 로 올려 두고, 로비를 벗어나면 스스로 사라진다.
///
/// ⚠ 프리팹은 <c>Resources</c> 에 둬야 코드가 찾을 수 있다.
///    없으면 조용히 아무것도 하지 않는다. 채팅이 없다고 게임이 멈추면 안 된다.
/// </summary>
public static class LobbyChatInstaller
{
    private const string PrefabPath = "LobbyChat";

    private static GameObject instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // 화면이 없는 프로세스(Dedicated Server 등)에는 UI 를 만들지 않는다.
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            return;
        }

        LocalPlayer.Registered += CreateWhenNeeded;
    }

    /// <summary>
    /// 내 캐릭터가 로비에 생긴 순간에 만든다. 튜토리얼과 같은 신호를 쓴다.
    ///
    /// ⚠ <b>여기서 예외가 새어 나가면 안 된다.</b> 이 알림은
    ///    <c>LocalPlayerView.Spawned</c> 한가운데서 불리고, 그 **뒤에** 카메라를 붙이는
    ///    코드가 있다. 여기서 터지면 카메라가 안 붙어 "Lobby에 접속 중..." 에서 멈춘다.
    ///    실제로 한 번 그렇게 멈췄다.
    ///
    ///    채팅이 안 뜨는 것과 게임에 못 들어가는 것 중에는 앞이 훨씬 낫다.
    /// </summary>
    private static void CreateWhenNeeded(NetworkObject player)
    {
        try
        {
            CreateNow();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[LobbyChatInstaller] 채팅을 띄우지 못했습니다. 게임은 그대로 진행합니다.\n" + e);
        }
    }

    private static void CreateNow()
    {
        if (instance != null || !InLobby())
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"[LobbyChatInstaller] Resources/{PrefabPath} 를 찾지 못해 채팅을 띄우지 않습니다.");
            return;
        }

        instance = Object.Instantiate(prefab);
        instance.name = prefab.name;
        Object.DontDestroyOnLoad(instance);

        WarnIfNoEventSystem();
    }

    /// <summary>
    /// 클릭을 받으려면 EventSystem 이 있어야 한다. **없으면 알리기만 한다.**
    ///
    /// ⚠ 여기서 만들지 않는다. 직접 만들었다가 로비가 통째로 멈춘 적이 있다.
    ///    이 프로젝트는 새 Input System 을 쓰는데 옛 <c>StandaloneInputModule</c> 을
    ///    붙이면 그 자리에서 예외가 난다. 그런데 이 함수는
    ///    <c>LocalPlayer.Register</c> 안에서 불리고, 그 뒤에 카메라를 붙이는 코드가 있다.
    ///    (<c>LocalPlayerView.Spawned</c>) 그래서 여기서 예외가 나면 **카메라가 영영
    ///    안 붙고 "Lobby에 접속 중..." 화면에서 멈춘다.**
    ///
    ///    남의 흐름 한가운데서 불리는 코드는 아무것도 만들지 않는 편이 안전하다.
    /// </summary>
    private static void WarnIfNoEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        Debug.LogWarning(
            "[LobbyChatInstaller] 씬에 EventSystem 이 없어 채팅을 클릭할 수 없습니다. " +
            "Lobby 씬에 EventSystem 을 하나 넣어 주세요. (Input System 용 모듈로)");
    }

    /// <summary>
    /// 로비인가.
    ///
    /// ⚠ 씬 이름만 보면 안 된다. Fusion 이 로비를 러너 씬 안의 <c>[Lobby]</c> 오브젝트로
    ///    담기 때문에 이름이 <c>FusionRunner (Client)_[Player:3]</c> 가 된다.
    ///    <see cref="LobbyTutorial"/> 이 같은 함정에 빠져 한 번도 뜨지 않았다.
    /// </summary>
    private static bool InLobby()
    {
        Scene lobby = SceneManager.GetSceneByName(SceneFlow.Lobby);

        if (lobby.IsValid() && lobby.isLoaded)
        {
            return true;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == SceneFlow.Lobby || go.name == "[" + SceneFlow.Lobby + "]")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
