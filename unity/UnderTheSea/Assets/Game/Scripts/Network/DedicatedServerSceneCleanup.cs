using UnityEngine;
using ithappy.Cute_Characters.Controller;

/// <summary>
/// Dedicated Server 에서는 필요 없는 씬 구성물을 끈다.
///
/// Lobby 는 사람이 보는 씬이라 카메라와 AudioListener 가 씬에 들어 있다.
/// 서버도 같은 씬을 로드하므로 그대로 두면 서버 프로세스에 카메라가 살아 있게 된다.
/// <c>-nographics</c> 로 띄우면 실제로 그리지는 않지만,
/// "서버에는 카메라·오디오·로컬 입력이 없다" 는 것을 코드로 못박아 두는 편이 낫다.
/// 나중에 <c>-nographics</c> 없이 서버를 띄워도 화면이 뜨지 않는다.
///
/// 이 컴포넌트가 붙은 <b>GameObject 자체를 끄지는 않는다.</b>
/// 카메라 오브젝트가 통째로 꺼지면 <see cref="LocalPlayerView"/> 가 클라이언트에서
/// 카메라를 찾지 못하기 때문이다. 컴포넌트만 개별로 끈다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-2)
/// </summary>
public class DedicatedServerSceneCleanup : MonoBehaviour
{
    private void Awake()
    {
        if (!FusionLaunchArguments.IsDedicatedServerProcess())
        {
            return;
        }

        int disabled = 0;

        foreach (Camera camera in GetComponentsInChildren<Camera>(includeInactive: true))
        {
            camera.enabled = false;
            disabled++;
        }

        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(includeInactive: true))
        {
            listener.enabled = false;
            disabled++;
        }

        foreach (PlayerCamera follower in GetComponentsInChildren<PlayerCamera>(includeInactive: true))
        {
            follower.enabled = false;
            disabled++;
        }

        Debug.Log($"[DedicatedServerSceneCleanup] 서버이므로 '{name}' 의 렌더링·오디오 컴포넌트 {disabled}개를 껐습니다.");
    }
}
