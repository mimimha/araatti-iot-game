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
        /// <summary>
        /// 창 없는 서버가 돌 프레임률.
        ///
        /// 서버는 아무것도 그리지 않지만 <c>Update</c> · <c>LateUpdate</c> 는 프레임마다 돈다.
        /// 제한이 없으면 갈 수 있는 만큼 돌아서 <b>아무 이득 없이 코어를 태운다.</b>
        /// 접속자가 한 명도 없는 검 서버가 CPU 를 배 서버의 19배 쓰고 있었다.
        ///
        /// <code>
        ///   Lobby DS      9.1%   (120 제한)
        ///   ShipCoop DS   0.5%   (120 제한)
        ///   Warriors DS   9.4%   (제한 없음)   ← 접속 0명인데 이만큼
        /// </code>
        ///
        /// <b>왜 120인가.</b> Fusion 틱이 64Hz 라 틱 하나에 1.875 프레임이 들어간다.
        /// 64 로 딱 맞추면 프레임이 하나만 밀려도 틱을 놓치지만, 120 이면 OS 스케줄링이
        /// 흔들려도 삼킬 여유가 있다. 배 · 광산 · 로비 서버가 같은 이유로 120 을 쓴다.
        /// </summary>
        [SerializeField, Min(30)] private int serverFrameRate = 120;

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

            // 화면이 없으니 vSync 는 의미가 없다. 끄고 프레임률을 직접 잡는다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = serverFrameRate;

            Debug.Log($"[WarriorsServerCleanup] 서버이므로 화면·소리 컴포넌트 {count}개를 껐습니다. " +
                      $"프레임률을 {serverFrameRate}로 맞췄습니다.");
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
