using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// Dedicated Server 에서는 필요 없는 것을 끈다. **씬 전체를 훑는다.**
    ///
    /// <b>Lobby 의 <c>DedicatedServerSceneCleanup</c> 을 쓰지 않는 이유</b>
    ///   그쪽은 자기 자식만 훑는다. Lobby 는 카메라 오브젝트 하나에 붙여 두면 되는 구조이기
    ///   때문이다. ShipCoop 은 카메라 · HUD Canvas · 결과창 · DevMode 가 씬 여기저기에
    ///   흩어져 있어 한 곳의 자식으로 묶을 수 없다. 묶으려면 민화님의 씬 계층을 바꿔야 한다.
    ///   또 그쪽은 <c>PlayerCamera</c>(Lobby 전용)를 알고, ShipCoop 이 꺼야 할
    ///   <c>ShipCoopCamera</c> · <c>ShipCoopHud</c> 는 모른다.
    ///
    /// <b>컴포넌트만 끈다. GameObject 는 끄지 않는다.</b>
    /// 오브젝트가 통째로 꺼지면 <c>FindAnyObjectByType</c> 로 서로를 찾는 코드가
    /// 서버에서만 null 을 보게 되어, 서버에서만 터지는 오류가 생긴다.
    ///
    /// ⚠ <b>게임 규칙은 여기서 끄지 않는다.</b> 침수 · 체력 · 사건 · 결과 판정은
    ///    서버가 <b>계산해야 하는</b> 쪽이다. 그 구분은 <c>ShipCoopGame.IsAuthority</c> 가 한다.
    ///    여기서 끄는 것은 <b>보는 것과 듣는 것</b>뿐이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopServerCleanup : MonoBehaviour
    {
        private void Awake()
        {
            if (!FusionLaunchArguments.IsDedicatedServerProcess())
            {
                return;
            }

            int count = 0;

            // 보는 것
            count += DisableAll<Camera>("카메라");
            count += DisableAll<ShipCoopCamera>("카메라 추적");

            // 듣는 것
            count += DisableAll<AudioListener>("귀");
            count += DisableAll<AudioSource>("소리");

            // 화면에 그리는 것
            count += DisableAll<Canvas>("Canvas");
            count += DisableAll<CanvasScaler>("Canvas 크기 맞춤");
            count += DisableAll<GraphicRaycaster>("클릭 판정");
            count += DisableAll<EventSystem>("입력 시스템");
            count += DisableAll<StandaloneInputModule>("입력 모듈");

            // 화면 쪽 ShipCoop 컴포넌트 — 규칙이 아니라 표시다
            count += DisableAll<ShipCoopHud>("HUD");
            count += DisableAll<ShipCoopDebugHud>("디버그 HUD");
            count += DisableAll<ShipCoopResultView>("결과창");
            count += DisableAll<ShipCoopPortrait>("프로필 사진");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // ShipCoopDevMode 는 파일 전체가 이 조건으로 감싸여 있다.
            // 일반 Dedicated Server 빌드에는 타입 자체가 없어 이 줄이 컴파일되지 않는다.
            count += DisableAll<ShipCoopDevMode>("DevMode");
#endif
            count += DisableAll<ShipCoopHelp>("도움말");

            // 연출
            count += DisableAll<ShipCoopWake>("물살 자국");
            count += DisableRenderers<ParticleSystemRenderer>("파티클");
            count += DisableRenderers<TrailRenderer>("궤적");

            // 조명 — 서버는 아무것도 그리지 않으므로 그림자 계산이 통째로 낭비다
            count += DisableAll<Light>("조명");

            Debug.Log($"[ShipCoopServerCleanup] 서버이므로 화면·소리 컴포넌트 {count}개를 껐습니다.");
        }

        /// <summary>씬에 있는 이 종류를 전부 끈다. 꺼진 오브젝트 안의 것도 포함한다.</summary>
        private static int DisableAll<T>(string label) where T : Behaviour
        {
            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    found[i].enabled = false;
                }
            }

            if (found.Length > 0)
            {
                Debug.Log($"[ShipCoopServerCleanup] {label} {found.Length}개를 껐습니다.");
            }

            return found.Length;
        }

        /// <summary>
        /// 렌더러를 끈다.
        ///
        /// <c>Renderer</c> 는 <c>Behaviour</c> 가 아니라서 위 함수로는 못 받는다.
        /// (파티클은 <c>ParticleSystem</c> 에 <c>enabled</c> 가 없어 렌더러 쪽을 끈다)
        /// </summary>
        private static int DisableRenderers<T>(string label) where T : Renderer
        {
            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    found[i].enabled = false;
                }
            }

            if (found.Length > 0)
            {
                Debug.Log($"[ShipCoopServerCleanup] {label} {found.Length}개를 껐습니다.");
            }

            return found.Length;
        }
    }
}
