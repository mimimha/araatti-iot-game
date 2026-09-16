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
        private void Awake()
        {
            Button button = GetComponent<Button>();
            button.onClick.RemoveListener(Restart);
            button.onClick.AddListener(Restart);
        }

        private void OnEnable() => EnsureEventSystem();

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
