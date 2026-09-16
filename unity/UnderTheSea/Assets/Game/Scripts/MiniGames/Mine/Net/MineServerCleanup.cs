using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mine.Net
{
    /// <summary>
    /// Dedicated Server 에서 **화면과 소리에만 쓰이는 것들을 끈다.**
    ///
    /// 서버는 규칙만 돌린다. 창이 없으므로 카메라도 조명도 필요 없고, 400칸짜리
    /// 격자 블록을 그릴 이유는 더더욱 없다.
    ///
    /// <code>
    ///   카메라 · 오디오      화면과 소리
    ///   UI · 이벤트 시스템   서버에 누를 사람이 없다
    ///   MineGridView         칸 400개를 큐브로 그린다. 규칙은 MineGrid 배열만 있으면 된다
    ///   MineVision           조명을 만들고 RenderSettings(안개 · 환경광)를 전역으로 바꾼다
    ///   MineCamera · Cursor  마우스를 읽고 커서를 잠근다
    ///   MineCursor · Debris  발밑 표시와 부스러기. 순수 연출
    /// </code>
    ///
    /// ⚠ 콜라이더는 끄지 않는다. 바닥(<c>GroundCollider</c>)이 없으면 서버에서
    ///    캐릭터가 아래로 떨어진다. 끄는 것은 <b>보이는 것</b>뿐이다.
    ///
    /// ⚠ 이 부품은 **게임 씬**에 놓인다. 시작 씬에는 끌 것이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineServerCleanup : MonoBehaviour
    {
        [Header("서버")]
        [Tooltip("창 없는 서버가 돌 프레임률. 이동이 Update 에서 일어나므로 이 값이 낮으면 " +
                 "캐릭터가 설정 속도만큼 걷지 못한다. 틱(60Hz)보다 촘촘해야 한다.")]
        [SerializeField, Min(30)] private int serverFrameRate = 120;

        private void Start()
        {
            if (!FusionLaunchArguments.IsDedicatedServerProcess()) return;

            int count = 0;

            // 화면
            count += DisableAll<Camera>("카메라");
            count += DisableAll<MineCamera>("광산 카메라");
            count += DisableAll<Light>("조명");

            // 소리
            count += DisableAll<AudioListener>("귀");
            count += DisableAll<AudioSource>("소리");

            // UI
            count += DisableAll<Canvas>("Canvas");
            count += DisableAll<CanvasScaler>("Canvas 크기 맞춤");
            count += DisableAll<GraphicRaycaster>("클릭 판정");
            count += DisableAll<EventSystem>("입력 시스템");
            count += DisableAll<StandaloneInputModule>("입력 모듈");
            count += DisableAll<MineHud>("HUD");
            count += DisableAll<MineDebugHud>("개발용 HUD");

            // 광산 연출
            // ⚠ <b>MineGridView 는 끄지 않는다.</b> 그리기만 하는 부품이 아니다.
            //
            //   칸 400개는 <c>PrimitiveType.Cube</c> 라 콜라이더를 같이 달고 나오고,
            //   칸이 파이면 <c>OnCellChanged</c> 로 그 블록을 <c>digDepth</c> 만큼 내린다.
            //   그 내려간 블록이 곷 캐릭터가 내려설 바닥이다.
            //
            //   이것을 끄면 <c>OnDisable</c> 이 그 구독을 끊어, 서버의 블록은
            //   처음 높이에 그대로 멈춰 선다. 화면에는 구멍이 보이는데
            //   서버 바닥은 평평해서 <b>캐릭터가 구멍으로 내려서지 않는다.</b>
            //   실측해서 확인한 문제다.
            //
            //   서버는 -nographics 라 렌더러가 실제로 그리지 않는다.
            //   남는 비용은 GameObject 와 콜라이더뿐이다.
            count += DisableAll<MineVision>("어둠과 랜턴");
            count += DisableAll<MineCursor>("발밑 표시");
            count += DisableAll<MineDebris>("부스러기");
            count += DisableAll<MineCrystalTint>("수정 색");

            // 커서를 잠글 이유가 없다. 창이 없으므로 잠기면 로그만 지저분해진다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // ⚠ **서버의 프레임률을 올린다.**
            //
            //    이동을 실제로 일으키는 것은 <c>CharacterMover.Update()</c> 이고, 그것은
            //    틱이 아니라 **프레임**마다 돈다. 창 없는 서버는 기본 프레임률이 낮아
            //    같은 입력을 줘도 캐릭터가 설정 속도만큼 못 걷는다.
            //    실측: 걷기 속도 1 m/s 설정에서 실제 0.13 m/s (약 1/7).
            //
            //    vSync 는 화면이 없으니 끄고, 틱(60Hz)보다 촘촘하게 돌린다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = serverFrameRate;

            Debug.Log($"[MineServerCleanup] 서버이므로 화면·소리 컴포넌트 {count}개를 껐습니다. " +
                      $"프레임률을 {serverFrameRate}로 맞췄습니다.");
        }

        private static int DisableAll<T>(string label) where T : Behaviour
        {
            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int turnedOff = 0;

            foreach (T one in found)
            {
                if (one == null || !one.enabled) continue;

                one.enabled = false;
                turnedOff++;
            }

            if (turnedOff > 0) Debug.Log($"[MineServerCleanup] {label} {turnedOff}개를 껐습니다.");

            return turnedOff;
        }
    }
}
