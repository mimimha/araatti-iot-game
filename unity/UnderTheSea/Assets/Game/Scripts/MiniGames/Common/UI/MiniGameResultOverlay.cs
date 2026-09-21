using System.Collections;
using MiniGames.Common;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 미니게임 <b>안에서</b> 결과를 보여주고 Lobby 로 돌려보내는 오버레이. 세 게임이 같이 쓴다.
    ///
    /// <b>왜 따로 만드는가.</b> 결과 화면(<see cref="ResultPanelPresenter"/>)은 <c>CommonMatchCanvas</c>
    /// 안에 있는데, 그 캔버스에는 <b>매칭 UI 와 그 제어 로직</b>이 함께 들어 있다. 통째로 미니게임
    /// 씬에 넣으면 게임 도중에 매칭 화면이 뜰 수 있고, 그 사고를 세 게임에서 각각 막아야 한다.
    /// 그래서 <b>결과 판만 떼어</b> 이 오버레이로 만들었다.
    ///
    /// <b>ResultPanelPresenter 는 고치지 않았다.</b> 원래부터 "넣어주면 그리고, 눌리면 알린다" 만
    /// 하는 부품이라 주인을 바꿔 끼울 수 있었다. 서연님 파일은 한 줄도 건드리지 않는다.
    ///
    /// <code>
    ///   서버가 결과 확정 → 각 게임이 연출을 보여준 뒤 Gateway 에 제출
    ///   → 이 오버레이가 받아서 결과 판을 연다
    ///   → 확인을 누르거나 시간이 다 되면 MiniGameTransition.ReturnToLobby()
    /// </code>
    ///
    /// ⚠ <b>결과를 여기서 반드시 소비해야 한다.</b> 소비하지 않으면 <c>MiniGameResultGateway</c> 가
    ///    결과를 들고 있다가 Lobby 의 <c>MatchFlowController</c> 에 넘긴다. 그쪽은
    ///    <c>RewardService.Grant</c> 로 <b>보상을 적립한다.</b> 아직 아이템도 인벤토리도 없는 단계라
    ///    그 경로를 타면 안 된다.
    /// </summary>
    public sealed class MiniGameResultOverlay : MonoBehaviour
    {
        [Header("보여줄 결과 판")]
        [SerializeField] private ResultPanelPresenter panel;

        [Header("아직 갈 곳이 없어 숨기는 버튼")]
        [Tooltip("[다시 하기] 는 같은 미니게임을 다시 여는 경로가 아직 없다. " +
                 "보이기만 하고 눌러도 아무 일이 없으면 고장으로 보이므로 통째로 끈다.")]
        [SerializeField] private GameObject retryButtonRoot;

        [Header("아무것도 누르지 않았을 때 (초)")]
        [Tooltip("이 시간이 지나면 스스로 Lobby 로 돌아간다.\n\n" +
                 "여는 시점을 서버가 정하므로 두 사람의 화면이 거의 같은 순간에 닫힌다.")]
        [SerializeField, Min(1f)] private float autoReturnSeconds = 5f;

        /// <summary>
        /// 게임이 스스로 붙이는 <b>[다시 하기] 동작.</b> 붙어 있지 않으면 버튼은 그대로 숨는다.
        ///
        /// <b>왜 정적인가.</b> 이 오버레이는 세 게임이 같이 쓰므로 특정 게임을 알면 안 된다.
        /// 게임 쪽이 "나는 새 판을 시작할 수 있다" 고 등록만 하고, 여기서는 그것이 있는지만 본다.
        ///
        /// ⚠ <b>포탈로 들어온 판에서는 등록돼 있어도 쓰지 않는다.</b> 그때는 로비의 매칭이
        ///    판을 잡고 있어서, 미니게임만 혼자 새 판을 시작하면 로비와 어긋난다.
        ///    단독 실행(개발·QA 빌드)에만 쓴다.
        ///
        /// ⚠ 씬을 벗어날 때 반드시 <c>null</c> 로 되돌려야 한다. 정적이라 씬이 바뀌어도 남는다.
        /// </summary>
        public static System.Action RestartHandler;

        /// <summary>돌아가기를 이미 시작했는가. <b>확인과 시간 만료가 겹쳐도 한 번만 간다.</b></summary>
        private bool returning;

        /// <summary>이번 결과에서 [다시 하기] 를 쓸 수 있는가. <see cref="Receive"/> 가 정한다.</summary>
        private bool canRestart;

        private Coroutine countdown;

        private void Awake()
        {
            // 화면이 없는 Dedicated Server 에서는 아무 일도 하지 않는다.
            // (ShipCoopServerCleanup 이 Canvas 와 EventSystem 을 끄지만, 등록 자체를 막아 둔다)
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                enabled = false;
                return;
            }

            if (panel == null)
            {
                panel = GetComponentInChildren<ResultPanelPresenter>(includeInactive: true);
            }

            if (retryButtonRoot != null)
            {
                // 보이지 않을 뿐 아니라 클릭 판정도 사라진다.
                // 켤지 말지는 결과가 올 때 Receive 가 다시 정한다.
                retryButtonRoot.SetActive(false);
            }

            EnsureEventSystem();
        }

        private void OnEnable()
        {
            if (panel != null)
            {
                panel.Hide();
                panel.LobbyRequested += HandleLobbyButton;
                panel.RetryRequested += HandleRetryButton;
            }

            MiniGameResultGateway.Register(Receive);
        }

        private void OnDisable()
        {
            MiniGameResultGateway.Unregister(Receive);

            if (panel != null)
            {
                panel.LobbyRequested -= HandleLobbyButton;
                panel.RetryRequested -= HandleRetryButton;
            }

            StopCountdown();
        }

        /// <summary>
        /// 결과가 도착했다. 판을 열고 시계를 건다.
        ///
        /// <b>보상 영역은 설정을 넘기지 않아 통째로 숨는다.</b> <c>ResultPanelPresenter</c> 는
        /// 설정이 없으면 보상 줄을 <c>SetActive(false)</c> 한다. 화면 코드를 고칠 필요가 없다.
        /// 지급하지도 않은 보상을 "획득 실패" 처럼 보여주지 않기 위해서다.
        /// </summary>
        private void Receive(MiniGameResult result, MiniGameResultOrigin origin)
        {
            if (panel == null)
            {
                Debug.LogError("[결과 오버레이] 결과 판이 연결되지 않았습니다. 결과를 띄우지 못합니다.", this);
                return;
            }

            Debug.Log(
                $"[결과 오버레이] 결과를 받았습니다 — {(result.IsClear ? "성공" : "실패")}, " +
                $"점수 {result.Score}, {result.PlayTimeText} (출처 {origin})");

            // **단독 실행이고 게임이 새 판을 시작할 수 있을 때만 [다시 하기] 를 살린다.**
            //
            // 포탈로 들어온 판은 로비의 매칭이 쥐고 있어서 미니게임 혼자 새 판을 열 수 없다.
            // 등록된 동작이 없을 때도 마찬가지다 — 눌러도 아무 일이 없으면 고장으로 보인다.
            canRestart = RestartHandler != null && !MiniGameTransition.InMiniGame;

            if (retryButtonRoot != null) retryButtonRoot.SetActive(canRestart);

            panel.Show(result, null);

            StopCountdown();

            // ⚠ **다시 할 수 있으면 시계를 걸지 않는다.** 5초 뒤 혼자 로비로 가 버리면
            //    누를 틈이 없고, 단독 빌드에는 갈 로비도 없다(ChannelSelect 씬이 없다).
            if (!canRestart) countdown = StartCoroutine(ReturnWhenTimeIsUp());
        }

        private IEnumerator ReturnWhenTimeIsUp()
        {
            yield return new WaitForSeconds(autoReturnSeconds);

            Debug.Log($"[결과 오버레이] {autoReturnSeconds:0}초가 지났습니다. Lobby 로 돌아갑니다.");
            GoToLobby();
        }

        /// <summary>
        /// Lobby 로 돌아간다. <b>확인 버튼과 시계가 같은 프레임에 불러도 한 번만 간다.</b>
        ///
        /// 돌아가는 길은 <c>MiniGameTransition.ReturnToLobby()</c> 하나뿐이다. Runner 종료 →
        /// 씬 전환 → Lobby 재접속 순서를 지키는 검증된 경로이고, 실패해도 스스로 복구한다.
        /// </summary>
        /// <summary>
        /// 사람이 확인을 눌렀다. <b>시계가 부르는 길과 나눠 둔 이유는 로그 한 줄 때문이다.</b>
        ///
        /// 돌아가는 동작 자체는 같지만, QA 로그만 보고 "눌러서 나갔는지 5초가 지나 나갔는지"
        /// 를 구별할 수 없으면 확인 버튼이 실제로 동작하는지 확인할 방법이 없다.
        ///
        /// ⚠ <see cref="GoToLobby"/> 안에서 <c>countdown</c> 이 남았는지로 가르려 했더니
        ///    틀렸다. 시계는 알린 뒤 <see cref="GoToLobby"/> 를 부르므로 그때도 코루틴
        ///    핸들이 그대로 살아 있다. 두 길을 입구에서 나누는 편이 확실하다.
        /// </summary>
        /// <summary>
        /// 사람이 [다시 하기] 를 눌렀다. <b>씬을 다시 열지 않는다.</b>
        ///
        /// 등록한 게임이 자기 방식으로 새 판을 시작한다(무쌍은 서버에 RPC 를 보낸다).
        /// 여기서는 결과 판만 닫는다 — 새 판이 시작되면 게임이 알아서 HUD 를 되돌린다.
        /// </summary>
        private void HandleRetryButton()
        {
            if (returning || !canRestart) return;

            Debug.Log("[결과 오버레이] 다시 하기를 눌렀습니다. 새 판을 요청합니다.");

            if (panel != null) panel.Hide();
            StopCountdown();

            RestartHandler?.Invoke();
        }

        /// <summary>
        /// **새 판이 시작됐으니 결과 판을 닫는다.** 게임 쪽이 부른다.
        ///
        /// ⚠ [다시 하기] 를 누른 사람은 <see cref="HandleRetryButton"/> 에서 스스로 닫지만,
        ///    <b>같이 하던 상대의 화면은 아무도 닫아 주지 않는다.</b> 한 사람이 누르면 두
        ///    사람 모두 새 판으로 가므로, 상대 화면에는 새 판 위에 지난 결과가 남는다.
        ///
        /// 돌아가는 중이면 건드리지 않는다 — 그쪽이 이미 닫고 나가는 길이다.
        /// </summary>
        public void CloseForNewMatch()
        {
            if (returning || panel == null || !panel.IsOpen) return;

            Debug.Log("[결과 오버레이] 새 판이 시작되어 결과 판을 닫습니다.");

            StopCountdown();
            panel.Hide();
        }

        private void HandleLobbyButton()
        {
            if (returning) return;

            Debug.Log("[결과 오버레이] 확인을 눌러 Lobby 로 돌아갑니다.");
            GoToLobby();
        }

        private void GoToLobby()
        {
            if (returning)
            {
                return;
            }

            returning = true;
            StopCountdown();

            if (panel != null)
            {
                panel.Hide();
            }

            MiniGameTransition.ReturnToLobby();
        }

        private void StopCountdown()
        {
            if (countdown != null)
            {
                StopCoroutine(countdown);
                countdown = null;
            }
        }

        /// <summary>
        /// 버튼을 누르려면 <c>EventSystem</c> 이 있어야 한다. ShipCoop 씬에는 없다 —
        /// 지금까지 이 게임의 UI 는 보여주기만 했고 누를 것이 없었다.
        ///
        /// ⚠ <b>이미 있으면 만들지 않는다.</b> 둘이 되면 Unity 가 경고를 내고 입력이 한쪽으로만 간다.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject made = new GameObject("EventSystem (결과 오버레이가 만듦)");
            made.AddComponent<EventSystem>();
            made.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            Debug.Log("[결과 오버레이] 이 씬에 EventSystem 이 없어 하나 만들었습니다.");
        }
    }
}
