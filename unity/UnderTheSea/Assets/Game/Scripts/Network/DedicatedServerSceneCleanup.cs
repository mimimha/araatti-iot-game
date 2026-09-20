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
    /// <summary>
    /// 창 없는 서버의 프레임률. <b>화면용이 아니라 안정성용이다.</b>
    ///
    /// 서버는 아무것도 그리지 않지만 <c>Update</c> · <c>LateUpdate</c> 는 프레임마다 돈다.
    /// 제한이 없으면 갈 수 있는 만큼 돌아서(실측: Lobby 900fps · ShipCoop 7,000fps)
    /// <b>아무 이득 없이 코어를 태운다.</b> 한 PC 에 서버 둘과 클라이언트 둘을 같이
    /// 띄우면 그 낭비가 그대로 경합이 된다.
    ///
    /// <b>왜 120인가.</b> Fusion 틱이 64Hz 라 틱 하나에 1.875 프레임이 들어간다.
    /// 64 로 딱 맞추면 프레임이 하나만 밀려도 틱을 놓치지만, 120 이면 OS 스케줄링이
    /// 흔들려도 삼킬 여유가 있다. 광산 서버가 같은 이유로 120 을 쓰고 있다.
    /// </summary>
    ///
    /// ⚠ <c>PeerMode.Multiple</c> 이라 이 부품은 서버에서 <b>두 번</b> 깨어난다.
    ///    같은 값을 두 번 넣는 것뿐이라 해가 없다.
    [SerializeField, Min(30)] private int serverFrameRate = 120;

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

        int particles = DedicatedServerParticles.DisableAll();

        // 화면이 없으니 vSync 는 의미가 없다. 끄고 프레임률을 직접 잡는다.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = serverFrameRate;

        Debug.Log($"[DedicatedServerSceneCleanup] 서버이므로 '{name}' 의 렌더링·오디오 컴포넌트 " +
                  $"{disabled}개와 파티클 {particles}개를 껐습니다. 프레임률을 {serverFrameRate}로 맞췄습니다.");
    }


}
