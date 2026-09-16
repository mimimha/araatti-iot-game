using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Warriors
{
    /// <summary>
    /// [다시 하기] 버튼. 결과 화면에서 같은 라운드를 처음부터 다시 시작한다.
    ///
    /// 씬 전환 자체는 SceneFlow 가 담당한다. (GAME_STRUCTURE.md 3장)
    /// Warriors 씬에는 EventSystem 이 없어 버튼이 눌리지 않으므로 필요할 때 만들어 둔다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class WarriorsRetryButton : MonoBehaviour
    {
        /// <summary>결과 화면이 뜨고 이만큼 지나야 버튼이 살아난다.</summary>
        private const float ArmDelaySeconds = .8f;

        /// <summary>이 시각이 지나야 클릭을 받는다.</summary>
        private float armedAt;

        private void Awake()
        {
            Button button = GetComponent<Button>();
            button.onClick.RemoveListener(Restart);
            button.onClick.AddListener(Restart);

            // ⚠ **키보드로 눌리면 안 된다.** 아래 OnEnable 의 설명을 볼 것.
            Navigation none = button.navigation;
            none.mode = Navigation.Mode.None;
            button.navigation = none;
        }

        /// <summary>
        /// 결과 화면이 뜬 순간 <b>실수로 눌리는 것</b>을 막는다.
        ///
        /// ⚠ 실제로 일어난 일(2026-09-16 실측 로그).
        ///    클리어 직후 아무도 누르지 않았는데 새 판이 시작됐다.
        ///    서버 로그에는 <c>[Player:2] 가 다시 하기를 눌렀습니다</c> 가 찍혔다.
        ///
        ///    2P 의 조작은 <c>KeyboardPlayerController</c> 기준 <b>방향키 + Space</b> 다.
        ///    그런데 <c>InputSystemUIInputModule</c> 의 기본 바인딩은
        ///      Navigate = 방향키 · <b>Submit = Space/Enter</b>
        ///    라서, 결과 화면이 뜬 뒤 방향키가 이 버튼을 <b>선택</b>하고
        ///    공격키인 Space 가 그것을 <b>눌러 버린다.</b>
        ///    즉 자동 재시작이 아니라 <b>플레이어의 공격 입력이 버튼을 누른 것</b>이었다.
        ///
        /// 그래서 세 겹으로 막는다.
        ///   1. navigation = None  — 방향키로 선택되지 않는다 (Awake)
        ///   2. 선택 해제          — 선택된 것이 없으면 Submit 이 갈 곳이 없다
        ///   3. 짧은 대기          — 화면이 뜨는 프레임에 이미 눌려 있던 입력을 흘려보낸다
        /// </summary>
        private void OnEnable()
        {
            EnsureEventSystem();

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            armedAt = Time.unscaledTime + ArmDelaySeconds;
        }

        /// <summary>
        /// 전투 씬은 키보드만 쓰기 때문에 EventSystem 이 없다.
        /// 없으면 버튼이 화면에 보이기만 하고 클릭이 전달되지 않는다.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("[Warriors] 결과 화면 버튼 입력을 위해 EventSystem 을 생성했습니다.");
        }

        /// <summary>
        /// 다시 시작한다.
        ///
        /// <b>네트워크 판이면 서버에 요청한다.</b> 예전에는 여기서도 <c>SceneFlow.RestartCurrent()</c> 를
        /// 불렀는데, 그것은 <b>내 씬만</b> 다시 여는 것이라 서버의 판은 끝난 상태로 남았다.
        /// 결과 화면을 빠져나와도 다시 시작되지 않고 화면만 어긋났다.
        ///
        /// 판의 상태는 서버가 들고 있으므로 서버에 되돌려 달라고 부탁한다. 그러면 두 사람이
        /// 같은 순간에 새 판 대기로 돌아간다. 혼자 하는 씬에는 매치가 없으니 예전 그대로 씬을 다시 연다.
        /// </summary>
        private void Restart()
        {
            // 화면이 막 뜬 참이면 무시한다. 위 OnEnable 의 3번.
            if (Time.unscaledTime < armedAt)
            {
                Debug.Log("[Warriors] 결과 화면이 막 떠서 [다시 하기] 입력을 무시했습니다.");
                return;
            }

            Warriors.Net.WarriorsMatchState match = Warriors.Net.WarriorsMatchState.Current;

            if (match != null && match.Object != null && match.Object.IsValid)
            {
                match.Rpc_RequestRestart();
                return;
            }

            SceneFlow.RestartCurrent();
        }
    }
}
