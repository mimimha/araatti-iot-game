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

        private void Restart() => SceneFlow.RestartCurrent();
    }
}
