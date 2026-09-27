using UnityEngine;
using UnityEngine.SceneManagement;
using Warriors;

/// <summary>
/// 무쌍에 완드 다리를 **코드로** 붙인다. 씬 · 프리팹 파일은 건드리지 않는다.
///
///     IotWarriorsBridge   WarriorsIoTInput 이 있는 오브젝트마다 하나
///
/// **왜 코드인가.** 붙일 자리가 <c>WarriorsGameRoot.prefab</c> 이고 무쌍 담당 것입니다.
/// 로비 · 낚시를 <see cref="IotLobbyInstaller"/> 로 붙인 것과 같은 이유입니다.
///
/// **씬 이름이 아니라 내용물로 알아봅니다.** <c>WarriorsIoTInput</c> 이 있으면 무쌍입니다.
/// 씬 이름이 바뀌어도, 검증 씬에서 돌려도 그대로 됩니다.
///
/// ⚠ **로컬 씬에 붙어도 해가 없습니다.** <c>WarriorsGameRoot</c> 는 네트워크 씬과 검증 씬
///   양쪽에 들어가는 프리팹이라 두 곳 다 붙습니다. 하지만 다리가
///   <c>WarriorsInputProvider</c> 가 있을 때만 값을 넘기므로, 검증 씬에서는 조용히 있습니다.
///   가르는 이유는 <see cref="IotWarriorsBridge"/> 주석에 있습니다.
///
/// ⚠ **이미 붙어 있으면 또 붙이지 않습니다.** 누가 나중에 프리팹에 직접 올려도 겹치지 않습니다.
///
/// 서버 빌드에서는 아무것도 하지 않습니다. 서버에는 완드가 없습니다.
/// </summary>
public static class IotWarriorsInstaller
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
        foreach (WarriorsIoTInput device in Object.FindObjectsByType<WarriorsIoTInput>(FindObjectsInactive.Include))
        {
            if (device.gameObject.scene != scene)
            {
                continue;
            }

            if (!device.TryGetComponent(out IotWarriorsBridge _))
            {
                device.gameObject.AddComponent<IotWarriorsBridge>();
            }
        }
    }
#endif
}
