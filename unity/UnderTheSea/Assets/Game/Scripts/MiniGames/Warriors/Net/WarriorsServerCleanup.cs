using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warriors.Net
{
    /// <summary>
    /// Dedicated Server 에서는 필요 없는 것을 끈다. **씬 전체를 훑는다.**
    ///
    /// ⚠ <b>게임 규칙은 여기서 끄지 않는다.</b> 스폰 · 피해 · 페이즈 판정은
    ///    서버가 <b>계산해야 하는</b> 쪽이다. 여기서 끄는 것은 <b>보는 것과 듣는 것</b>뿐이다.
    ///
    /// 컴포넌트만 끄고 GameObject 는 끄지 않는다. 오브젝트가 통째로 꺼지면
    /// <c>FindFirstObjectByType</c> 로 서로를 찾는 코드가 서버에서만 null 을 보게 되어,
    /// 서버에서만 터지는 오류가 생긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsServerCleanup : MonoBehaviour
    {
        private void Awake()
        {
            if (!FusionLaunchArguments.IsDedicatedServerProcess()) return;

            int count = 0;

            // 보는 것
            count += DisableAll<Camera>("카메라");
            count += DisableAll<WarriorsThirdPersonCamera>("카메라 추적");

            // 듣는 것
            count += DisableAll<AudioListener>("귀");
            count += DisableAll<AudioSource>("소리");

            // 화면에 그리는 것
            count += DisableAll<Canvas>("Canvas");
            count += DisableAll<CanvasScaler>("Canvas 크기 맞춤");
            count += DisableAll<GraphicRaycaster>("클릭 판정");
            count += DisableAll<EventSystem>("입력 시스템");
            count += DisableAll<StandaloneInputModule>("입력 모듈");

            // 화면 쪽 Warriors 컴포넌트 - 규칙이 아니라 표시다
            count += DisableAll<WarriorsHudPresenter>("HUD");
            count += DisableAll<WarriorsCombatHud>("전투 HUD");
            count += DisableAll<WarriorsTentacleIndicator>("촉수 표시");
            count += DisableAll<WarriorsMonsterVisualAnimator>("몬스터 애니메이션");
            count += DisableAll<WarriorsKrakenTentacleDeformer>("촉수 변형");

            // 조명 - 서버는 아무것도 그리지 않으므로 그림자 계산이 통째로 낭비다
            count += DisableAll<Light>("조명");

            Debug.Log($"[WarriorsServerCleanup] 서버이므로 화면·소리 컴포넌트 {count}개를 껐습니다.");
        }

        private static int DisableAll<T>(string label) where T : Behaviour
        {
            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) found[i].enabled = false;

            if (found.Length > 0) Debug.Log($"[WarriorsServerCleanup] {label} {found.Length}개를 껐습니다.");

            return found.Length;
        }
    }
}
