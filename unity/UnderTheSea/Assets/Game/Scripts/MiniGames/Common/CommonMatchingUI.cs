using UnityEngine;
using MiniGames.Common.UI;

namespace MiniGames.Common
{
    /// <summary>
    /// 공통 매칭 화면의 손잡이. <c>CommonMatchingCanvas.prefab</c> 의 루트에 붙어 있다.
    ///
    /// 각 미니게임 포탈은 자기 설정 하나만 넘긴다. 화면·규칙·슬롯 수는 설정을 읽어 바뀐다.
    ///
    ///     검 포탈    CommonMatchingUI.Current.Show(swordConfig);    // 1~2인
    ///     광산 포탈  CommonMatchingUI.Current.Show(miningConfig);   // 1~4인
    ///     배 포탈    CommonMatchingUI.Current.Show(shipConfig);     // 4인 고정
    ///
    /// 프리팹을 미니게임마다 복사하지 않는다. 같은 프리팹을 놓고 <see cref="Show"/> 에 다른
    /// 설정을 넘기면 끝이다. 프리팹을 씬에 하나만 두고 인스펙터에서 참조를 잡아도 되고,
    /// 참조가 없으면 <see cref="Current"/> 로 찾는다.
    /// </summary>
    public sealed class CommonMatchingUI : MonoBehaviour
    {
        [SerializeField] private MatchFlow flow;
        [SerializeField] private MatchFlowController controller;

        [Tooltip("매칭 화면 Canvas. Show/Hide 가 켜고 끈다.")]
        [SerializeField] private GameObject canvasRoot;

        [Tooltip("매칭 판(MatchPanel). 다른 화면(결과 등)이 꺼 두었더라도 Show 가 다시 켠다.")]
        [SerializeField] private GameObject matchPanel;

        [Tooltip("서버 최초 대기열 패킷을 기다리는 전체 화면.")]
        [SerializeField] private QueueLoadingPresenter queueLoading;

        [Tooltip("포탈과 네트워크 대기열을 잇는 조정자.")]
        [SerializeField] private MatchingQueueCoordinator queueCoordinator;

        [Tooltip("결과 판. 켜져 있으면 Show 가 닫는다. 매칭 전용 프리팹에는 없으므로 비워 둘 수 있다.")]
        [SerializeField] private MiniGames.Common.UI.ResultPanelPresenter resultPanel;

        [Tooltip("켜 두면 씬이 열릴 때 매칭 화면이 바로 보인다(테스트 씬). 포탈에서 Show 로 열 거라면 끈다.")]
        [SerializeField] private bool showOnStart = true;

        /// <summary>씬에 있는 매칭 화면. 프리팹은 씬에 하나만 둔다.</summary>
        public static CommonMatchingUI Current { get; private set; }

        public MatchFlow Flow => flow;
        public MatchFlowController Controller => controller;
        public MatchingQueueCoordinator QueueCoordinator => queueCoordinator;
        public bool IsShown => canvasRoot != null && canvasRoot.activeSelf;

        private void Awake()
        {
            if (Current != null && Current != this)
                Debug.LogWarning("[CommonMatchingUI] 매칭 화면이 씬에 두 개 있습니다. 하나만 두세요.", this);

            Current = this;

            // 매칭 화면(대기 로딩 포함)이 떠 있는 동안 로비 채팅창 · 섬 회복도 바를 감춘다.
            if (canvasRoot != null && canvasRoot.GetComponent<HideLobbyHudWhileShown>() == null)
                canvasRoot.AddComponent<HideLobbyHudWhileShown>();

            if (!showOnStart && canvasRoot != null) canvasRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// 이 게임의 규칙으로 매칭 화면을 연다. 포탈이 부르는 함수는 이것 하나다.
        /// 이미 열려 있으면 규칙만 갈아 끼우고 처음부터 다시 센다.
        /// </summary>
        public void Show(MiniGameConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[CommonMatchingUI] Show 에 넘긴 MiniGameConfig 가 비어 있습니다.", this);
                return;
            }

            if (canvasRoot != null) canvasRoot.SetActive(true);
            if (queueLoading != null) queueLoading.Hide();
            if (resultPanel != null) resultPanel.Hide();   // 결과 판이 남아 있으면 매칭 판 위를 덮는다
            if (matchPanel != null) matchPanel.SetActive(true);
            if (flow != null) flow.Configure(config);
        }

        /// <summary>
        /// **로비 서버가 정해 준 판으로 매칭 화면을 연다.**
        ///
        /// <paramref name="roomSize"/> 는 이번 판의 인원이다. 정원보다 작을 수 있고,
        /// 넘는 칸은 판에서 ✕ 로 막힌다. 자동 시작과 [게임 시작] 버튼은 꺼진다 —
        /// 출발을 정하는 것은 서버다.
        /// </summary>
        public void ShowServerMatch(MiniGameConfig config, int roomSize)
        {
            if (config == null)
            {
                Debug.LogError("[CommonMatchingUI] ShowServerMatch 에 넘긴 MiniGameConfig 가 비어 있습니다.", this);
                return;
            }

            if (canvasRoot != null) canvasRoot.SetActive(true);
            if (queueLoading != null) queueLoading.Hide();
            if (resultPanel != null) resultPanel.Hide();
            if (matchPanel != null) matchPanel.SetActive(true);
            if (flow != null) flow.Configure(config, roomSize);
        }

        /// <summary>
        /// 제목 아래 한 줄에 로비 서버가 알려 준 사정을 적는다.
        /// ("빈 서버를 기다리는 중", "3명 중 2명" 처럼)
        /// </summary>
        public void SetServerStatus(string text)
        {
            if (panel == null) panel = GetComponentInChildren<MatchPanelPresenter>(includeInactive: true);
            if (panel != null) panel.SetServerStatus(text);
        }

        private MatchPanelPresenter panel;

        /// <summary>포탈이 서버 대기열에 들어갈 때 호출하는 실제 게임용 진입점.</summary>
        public void BeginQueue(MiniGameConfig config)
        {
            if (queueCoordinator == null)
            {
                Debug.LogError("[CommonMatchingUI] MatchingQueueCoordinator가 연결되지 않았습니다.", this);
                return;
            }

            queueCoordinator.BeginQueue(config);
        }

        /// <summary>서버 응답 전에는 기존 매칭 판을 숨기고 조타 로딩만 보여 준다.</summary>
        public void ShowQueueLoading(MiniGameConfig config)
        {
            if (config == null) return;

            if (flow != null) flow.SetNetworkManagedRoster(true);
            if (canvasRoot != null) canvasRoot.SetActive(true);
            if (resultPanel != null) resultPanel.Hide();
            if (matchPanel != null) matchPanel.SetActive(false);
            if (queueLoading != null) queueLoading.Show();
        }

        /// <summary>원형 전환 동안 대기 화면 뒤에 매칭 화면을 미리 준비한다.</summary>
        public void PrepareMatchBehindLoading(MiniGameConfig config)
        {
            if (config == null) return;

            if (canvasRoot != null) canvasRoot.SetActive(true);
            if (resultPanel != null) resultPanel.Hide();
            if (matchPanel != null) matchPanel.SetActive(true);
            if (flow != null) flow.Configure(config);
        }

        /// <summary>원형 전환이 끝난 순간부터 30초 자동 시작 시간을 다시 센다.</summary>
        public void CompleteQueueReveal()
        {
            if (flow != null) flow.RestartAutoStartTimer();
        }

        /// <summary>화면만 감춘다. 파티는 그대로 둔다.</summary>
        public void Hide()
        {
            if (queueLoading != null) queueLoading.Hide();
            if (canvasRoot != null) canvasRoot.SetActive(false);
        }
    }
}
