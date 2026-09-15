using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MiniGames.Common.UI;

namespace MiniGames.Common.DebugTools
{
    /// <summary>
    /// 테스트 씬 전용 조작 줄 (Debug/MatchDebugControls). 매칭 → 카운트다운 → (가짜 게임) → 결과 →
    /// 다시 하기 / 로비까지 네트워크 없이 손으로 돌려 보기 위한 판.
    ///
    ///     [검 1~2인] [광산 1~4인] [배 4인]      ← 지금 씬에 달려 있는 버튼
    ///     [+ Player] [- Player] [준비 토글] [조각 초기화]   ← 코드는 있지만 버튼은 떼어 둠 (필요하면 다시 연결)
    ///
    /// ⚠ 실제 게임 화면에는 올리지 않는다. 유저는 이 버튼으로 게임을 고르지 않는다 — 실제로는
    ///    각 미니게임 포탈이 자기 설정을 <see cref="CommonMatchingUI.Show"/> 에 넘긴다. 이 줄은
    ///    설정 세 개와 명단이 제대로 동작하는지 손으로 확인하는 용도일 뿐이다.
    ///    이 컴포넌트를 지워도 공통 시스템은 그대로 남도록, 공통 쪽에서는 이 파일을 참조하지 않는다.
    ///
    /// 여기서 하는 일은 <b>네트워크가 할 일을 손으로 흉내 내는 것뿐</b>이다. 누르는 버튼은
    /// 전부 <see cref="PlayerRoster"/> 와 <see cref="MatchFlowController"/> 의 public
    /// 함수를 부른다 — 나중에 Photon 콜백이 같은 함수를 부르면 이 판은 그대로 떼어내면 된다.
    /// </summary>
    public sealed class MatchResultTestRig : MonoBehaviour
    {
        [SerializeField] private MatchFlow flow;
        [SerializeField] private MatchFlowController controller;
        [Tooltip("프리팹 루트의 손잡이. 게임을 바꿀 때 포탈이 하는 것과 똑같이 Show(config) 를 부른다.")]
        [SerializeField] private CommonMatchingUI matchingUI;
        [SerializeField] private MatchPanelPresenter matchPanel;
        [SerializeField] private ResultPanelPresenter resultPanel;

        [Header("미니게임 설정 세 개")]
        [SerializeField] private MiniGameConfigProvider configs;

        [Header("디버그 UI")]
        [Tooltip("끄면 조작 줄이 화면에서 사라진다. 실제 게임 연결 시 기본값은 false.")]
        [SerializeField] private bool showDebugControls = true;

        [Tooltip("Play 직후 현재 게임으로 5초 대기열 테스트를 자동 시작한다.")]
        [SerializeField] private bool startQueueOnPlay = true;

        [Tooltip("이 씬에서는 등록된 네트워크가 Fusion 이어도 Fake 로 바꿔 세운다. 서버 없이 대기열 흐름을 보기 위한 것이다.")]
        [SerializeField] private bool useFakeNetworkInTestScene = true;

        [Tooltip("이 키로 조작 줄을 여닫는다.")]
        [SerializeField] private Key toggleKey = Key.F1;

        [SerializeField] private GameObject debugRoot;
        [SerializeField] private Button addPlayerButton;
        [SerializeField] private Button removePlayerButton;
        [SerializeField] private Button toggleReadyButton;
        [SerializeField] private Button swordButton;
        [SerializeField] private Button miningButton;
        [SerializeField] private Button shipButton;
        [SerializeField] private Button resetFragmentsButton;
        [SerializeField] private TMP_Text debugStatus;

        [Header("가짜 게임")]
        [SerializeField] private GameObject playingRoot;
        [SerializeField] private TMP_Text playingText;
        [SerializeField, Min(.2f)] private float fakePlaySeconds = 1.6f;

        private Coroutine fakeGame;

        // 버튼은 한 번만 잇는다. OnEnable 에서 이으면 껐다 켤 때마다 같은 리스너가 쌓여
        // [+ Player] 한 번에 두 명씩 들어오게 된다.
        private void Awake()
        {
            EnsureFakeNetwork();

            Bind(addPlayerButton, AddPlayer);
            Bind(removePlayerButton, RemovePlayer);
            Bind(toggleReadyButton, ToggleReadyOfLast);
            Bind(swordButton, () => SwitchGame(configs != null ? configs.Sword : null));
            Bind(miningButton, () => SwitchGame(configs != null ? configs.Mining : null));
            Bind(shipButton, () => SwitchGame(configs != null ? configs.Ship : null));
            Bind(resetFragmentsButton, () =>
            {
                RewardService.ResetAll();
                RefreshDebugStatus();
            });
        }

        private void OnEnable()
        {
            if (controller != null) controller.MiniGameStarting += OnMiniGameStarting;

            if (resultPanel != null)
            {
                resultPanel.RetryRequested += OnRetry;
                resultPanel.LobbyRequested += OnLobby;
            }

            PlayerRoster.Changed += RefreshDebugStatus;
            if (flow != null) flow.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (controller != null) controller.MiniGameStarting -= OnMiniGameStarting;
            if (flow != null) flow.StateChanged -= OnStateChanged;

            if (resultPanel != null)
            {
                resultPanel.RetryRequested -= OnRetry;
                resultPanel.LobbyRequested -= OnLobby;
            }

            PlayerRoster.Changed -= RefreshDebugStatus;
        }

        private void Start()
        {
            if (playingRoot != null) playingRoot.SetActive(false);
            if (debugRoot != null) debugRoot.SetActive(showDebugControls);
            if (startQueueOnPlay && matchingUI != null && flow != null && flow.Config != null)
                matchingUI.BeginQueue(flow.Config);
            RefreshDebugStatus();
        }

        private void Update()
        {
            if (debugRoot == null) return;

            bool loading = matchingUI != null && matchingUI.QueueCoordinator != null &&
                           matchingUI.QueueCoordinator.IsLoadingVisible;
            bool shouldShow = showDebugControls && !loading;
            if (debugRoot.activeSelf != shouldShow) debugRoot.SetActive(shouldShow);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[toggleKey].wasPressedThisFrame) return;

            showDebugControls = !showDebugControls;
            debugRoot.SetActive(showDebugControls && !loading);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        /// <summary>
        /// 테스트 씬은 서버 없이 돌아야 한다. 부트스트랩이 Fusion 을 세웠다면(기본값) 그것을 내리고
        /// Fake 를 세운다. 에디터 전체 스위치를 건드리지 않으므로 다른 씬의 Fusion 테스트에는 영향이 없다.
        /// Awake 에서 하는 이유: 프리팹의 MatchingQueueCoordinator 는 BeginQueue 때 다시 서비스를 잡는다.
        /// </summary>
        private void EnsureFakeNetwork()
        {
            if (!useFakeNetworkInTestScene) return;

            INetworkService current = NetworkServiceLocator.Current;
            if (current is FakeNetworkService) return;

            if (current is MonoBehaviour host)
            {
                UnityEngine.Debug.Log($"[TestRig] 테스트 씬이라 {host.GetType().Name} 대신 FakeNetworkService 를 세웁니다.", this);
                DestroyImmediate(host.gameObject); // OnDestroy 가 Locator 등록을 푼다
            }

            new GameObject("NetworkService (Fake, 테스트 씬)").AddComponent<FakeNetworkService>();
        }

        // ------------------------------------------------------------
        // 네트워크가 할 일을 손으로
        // ------------------------------------------------------------

        private void OnStateChanged(MatchState state) => RefreshDebugStatus();

        /// <summary>Fusion 의 "플레이어 입장" 이 할 일과 같다 → RegisterPlayer.</summary>
        private void AddPlayer()
        {
            MiniGameConfig config = flow != null ? flow.Config : null;
            if (config != null && PlayerRoster.ActivePlayerCount >= config.MaxPlayers)
            {
                UnityEngine.Debug.Log($"[TestRig] 정원({config.MaxPlayers}명)이 이미 찼습니다.");
                return;
            }

            PlayerRoster.RegisterNextTestPlayer(isLocal: PlayerRoster.ActivePlayerCount == 0);
        }

        /// <summary>Fusion 의 "플레이어 퇴장" 이 할 일과 같다 → UnregisterPlayer(id).</summary>
        private void RemovePlayer()
        {
            PlayerEntry last = PlayerRoster.AtSlot(PlayerRoster.ActivePlayerCount - 1);
            if (last == null) return;

            PlayerRoster.UnregisterPlayer(last.PlayerId);
        }

        /// <summary>준비 동기화가 할 일과 같다 → SetPlayerReady. 마지막 사람의 준비를 뒤집는다.</summary>
        private void ToggleReadyOfLast()
        {
            PlayerEntry last = PlayerRoster.AtSlot(PlayerRoster.ActivePlayerCount - 1);
            if (last == null) return;

            PlayerRoster.SetPlayerReady(last.PlayerId, !last.IsReady);
        }

        /// <summary>
        /// 다른 미니게임 규칙으로 갈아 끼운다. 실제 게임에서 포탈이 하는 일과 같다 —
        /// 명단을 비우고 <see cref="CommonMatchingUI.Show"/> 에 설정을 넘긴다.
        /// (정원 4명인 광산에서 2명인 검으로 바꾸면 자리가 넘치므로 비우고 시작한다.)
        /// </summary>
        private void SwitchGame(MiniGameConfig config)
        {
            if (config == null || flow == null) return;

            StopFakeGame();
            if (resultPanel != null) resultPanel.Hide();
            if (playingRoot != null) playingRoot.SetActive(false);

            // 결과 화면이나 진행 중 화면에서 게임을 바꾸면 매칭 화면으로 돌아와야 한다.
            // 이 한 줄이 없으면 가짜 한 판이 꺼 둔 매칭 패널이 계속 꺼진 채라, 화면이 비고
            // 패널이 꺼져 있는 동안에는 Redraw 도 돌지 않아 인원이 바뀌어도 반응이 없다.
            if (matchPanel != null) matchPanel.gameObject.SetActive(true);

            PlayerRoster.ClearPlayers();
            // 에디터에서는 FakeNetworkService가 5초 뒤 실제 서버와 같은 이벤트를 보낸다.
            // 따라서 아래 게임 선택 버튼으로 조타 로딩 → 매칭 화면 전체 흐름을 검증한다.
            if (matchingUI != null && matchingUI.QueueCoordinator != null) matchingUI.BeginQueue(config);
            else if (matchingUI != null) matchingUI.Show(config);
            else flow.Configure(config);
            RefreshDebugStatus();
        }

        private void RefreshDebugStatus()
        {
            if (debugStatus == null) return;

            MiniGameConfig config = flow != null ? flow.Config : null;
            string game = config != null ? config.DisplayName : "-";
            string rule = config == null
                ? string.Empty
                : config.RequireFullParty
                    ? $"{config.MaxPlayers}인 고정"
                    : $"{config.MinPlayers}~{config.MaxPlayers}인";

            string state = flow != null ? flow.State.ToString() : "-";

            debugStatus.text =
                $"[DEBUG · F1]  {game} ({rule})   인원 {PlayerRoster.ActivePlayerCount}" +
                $"   준비 {PlayerRoster.ReadyCount}   상태 {state}   조각 {RewardService.OwnedCount}개";
        }

        // ------------------------------------------------------------
        // 가짜 게임 한 판 — 진짜 씬 대신
        // ------------------------------------------------------------

        private void OnMiniGameStarting(MiniGameConfig config)
        {
            StopFakeGame();
            fakeGame = StartCoroutine(FakePlay(config));
        }

        private void StopFakeGame()
        {
            if (fakeGame == null) return;

            StopCoroutine(fakeGame);
            fakeGame = null;
        }

        private IEnumerator FakePlay(MiniGameConfig config)
        {
            if (matchPanel != null) matchPanel.gameObject.SetActive(false);
            if (playingRoot != null) playingRoot.SetActive(true);
            if (playingText != null) playingText.text = $"{config.DisplayName} 진행 중...";

            yield return new WaitForSeconds(fakePlaySeconds);

            if (playingRoot != null) playingRoot.SetActive(false);
            fakeGame = null;

            ShowFakeResult(config);
        }

        /// <summary>
        /// 미니게임이 넘겨줄 법한 값을 흉내 낸다. 진짜 미니게임도 이와 똑같이
        /// <see cref="MatchFlowController.CompleteMiniGame(MiniGameResult)"/> 한 번만 부르면 된다.
        /// </summary>
        private void ShowFakeResult(MiniGameConfig config)
        {
            int players = Mathf.Max(1, PlayerRoster.ActivePlayerCount);
            int score = 1200 * players + Random.Range(0, 400);

            string extraValue = config.GameId switch
            {
                MiniGameId.Sword => (7 * players).ToString(),
                MiniGameId.Mining => (110 * players).ToString(),
                _ => (1300 * players).ToString(),
            };

            var result = new MiniGameResult(
                config.GameId,
                isClear: true,
                score: score,
                playTime: 62f + players * 5f,
                extraStatLabel: config.ExtraStatLabel,
                extraStatValue: extraValue,
                rewardId: config.FragmentId,
                fragmentObtained: false,
                playerCount: players);

            if (controller != null) controller.CompleteMiniGame(result);
            RefreshDebugStatus();
        }

        // ------------------------------------------------------------
        // 결과 화면의 두 버튼 — 실제 처리는 컨트롤러가 한다
        // ------------------------------------------------------------

        private void OnRetry()
        {
            if (matchPanel != null) matchPanel.gameObject.SetActive(true);
            RefreshDebugStatus();
        }

        private void OnLobby()
        {
            if (matchPanel != null) matchPanel.gameObject.SetActive(true);

            // 컨트롤러가 파티를 비웠으니 테스트를 이어 가려면 나를 다시 넣어 준다.
            PlayerRoster.RegisterNextTestPlayer(isLocal: true);
            RefreshDebugStatus();
        }
    }
}
