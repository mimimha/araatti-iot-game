using FishingMiniGame.Runtime;
using UnderTheSea.Lobby;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비에 완드 부품을 **코드로** 붙인다. 씬 · 프리팹 파일은 건드리지 않는다.
///
///     IotFishingBridge   PlayerFishingAdapter 가 있는 오브젝트마다 하나 (FishingLobbyIntegration 프리팹)
///     IotLobbyInteract   그 씬에 하나. 씬과 함께 사라진다
///
/// **왜 코드인가.** 붙일 자리가 둘 다 남의 파일입니다. 낚시 프리팹은 낚시 담당 것이고,
/// <c>Lobby.unity</c> 는 여러 사람이 만지는 씬이라 충돌이 잦습니다. 이동 · 카메라가
/// <see cref="IotPlayerController.Persistent"/> 로 씬 작업 없이 붙은 것과 같은 방식입니다.
///
/// **씬 이름이 아니라 내용물로 로비를 알아봅니다.** <see cref="IotLobbyInteract"/> 가 다루는
/// 셋(낚시터 · 포탈 · 제단) 중 하나라도 있으면 붙입니다. 씬 이름이 바뀌어도, 낚시 개발용 씬
/// (<c>FishingV3PlayerIntegration</c>)에서 돌려도 그대로 됩니다.
///
/// ⚠ **<c>IotLobbyInteract</c> 를 Persistent 오브젝트에 같이 두면 안 됩니다.** 미니게임에서도
///   왼손 버튼 1 을 먹어서 배 협동의 도움 요청이 안 눌립니다. 그래서 로비 씬 안에 만듭니다.
///
/// ⚠ **이미 붙어 있으면 또 붙이지 않습니다.** 누가 나중에 씬 · 프리팹에 직접 올려도 겹치지 않습니다.
///
/// ⚠ **순서.** <c>sceneLoaded</c> 는 씬 오브젝트의 Awake · OnEnable 뒤, Start 앞에 옵니다.
///   <c>PlayerFishingAdapter</c> 는 Awake · OnEnable 에서 키보드 입력원을 꽂고,
///   <c>IotFishingBridge</c> 는 Start 에서 그것을 품은 입력원으로 바꿔 꽂으므로 순서가 맞습니다.
///   브리지를 먼저 붙이고 <c>IotLobbyInteract</c> 를 나중에 만드는 것도 같은 이유입니다 —
///   그쪽 Start 가 브리지를 찾습니다.
///
/// 서버 빌드에서는 아무것도 하지 않습니다. 서버에는 완드가 없습니다.
/// </summary>
public static class IotLobbyInstaller
{
#if !UNITY_SERVER
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        // 에디터에서 도메인 리로드를 꺼도 두 번 걸리지 않게 먼저 뗀다.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool fishing = false;

        foreach (PlayerFishingAdapter adapter in Object.FindObjectsByType<PlayerFishingAdapter>(FindObjectsInactive.Include))
        {
            if (adapter.gameObject.scene != scene)
            {
                continue;
            }

            fishing = true;

            if (!adapter.TryGetComponent(out IotFishingBridge _))
            {
                adapter.gameObject.AddComponent<IotFishingBridge>();
            }
        }

        bool lobby = fishing || HasInScene<MiniGamePortal>(scene) || HasInScene<AltarInteraction>(scene);

        if (!lobby || HasInScene<IotLobbyInteract>(scene))
        {
            return;
        }

        GameObject host = new GameObject("[IotLobbyInteract]");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<IotLobbyInteract>();
    }

    private static bool HasInScene<T>(Scene scene) where T : Component
    {
        foreach (T found in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
        {
            if (found.gameObject.scene == scene)
            {
                return true;
            }
        }

        return false;
    }
#endif
}
