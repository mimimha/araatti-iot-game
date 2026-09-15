using System;
using System.Collections;
using MiniGames.Common.UI;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 포탈, 대기 UI, 네트워크 서비스, 기존 매칭 화면 사이를 잇는 단일 진입점.
    /// Fake와 Fusion 모두 INetworkService의 같은 이벤트를 사용하므로 서버 교체 시 UI는 바뀌지 않는다.
    /// </summary>
    public sealed class MatchingQueueCoordinator : MonoBehaviour
    {
        [SerializeField] private CommonMatchingUI matchingUI;
        [SerializeField] private QueueLoadingPresenter loadingView;
        [SerializeField] private MatchFlowController controller;
        [SerializeField, Min(0f)] private float minimumLoadingSeconds = 5f;

        private INetworkService service;
        private MiniGameConfig pendingConfig;
        private string pendingQueueKey;
        private Coroutine revealRoutine;
        private bool queueActive;
        private bool matchVisible;
        private bool startBuffered;

        public static MatchingQueueCoordinator Current { get; private set; }
        public bool IsQueueActive => queueActive;
        public bool IsLoadingVisible => loadingView != null && loadingView.IsShown;
        public float MinimumLoadingSeconds => minimumLoadingSeconds;

        private void Awake()
        {
            if (Current != null && Current != this)
                Debug.LogWarning("[MatchingQueue] Coordinator가 두 개 있습니다. 씬에는 하나만 두세요.", this);
            Current = this;
        }

        private void OnEnable()
        {
            BindService();
            if (controller != null) controller.MatchCancelled += OnMatchCancelled;
        }

        private void OnDisable()
        {
            if (controller != null) controller.MatchCancelled -= OnMatchCancelled;
            UnbindService();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// 포탈이 호출할 함수. 화면을 먼저 띄운 뒤 서버에 대기열 등록을 요청한다.
        /// </summary>
        public void BeginQueue(MiniGameConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[MatchingQueue] MiniGameConfig가 비어 있습니다.", this);
                return;
            }

            if (queueActive) CancelQueue(hideUi: false);
            BindService();

            pendingConfig = config;
            pendingQueueKey = config.GameId.ToString();
            queueActive = true;
            matchVisible = false;
            startBuffered = false;

            if (matchingUI != null) matchingUI.ShowQueueLoading(config);

            if (service == null)
            {
                Debug.LogError("[MatchingQueue] 등록된 INetworkService가 없어 대기열 요청을 보낼 수 없습니다.", this);
                return;
            }

            service.JoinMiniGameQueue(pendingQueueKey);
        }

        public void CancelQueue() => CancelQueue(hideUi: true);

        private void CancelQueue(bool hideUi)
        {
            if (service != null && queueActive) service.LeaveMiniGameQueue();
            queueActive = false;
            matchVisible = false;
            startBuffered = false;
            pendingConfig = null;
            pendingQueueKey = null;

            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            if (hideUi && matchingUI != null) matchingUI.Hide();
        }

        private void BindService()
        {
            INetworkService next = NetworkServiceLocator.Current;
            if (ReferenceEquals(service, next)) return;

            UnbindService();
            service = next;
            if (service == null) return;

            service.OnQueueUpdated += OnQueueUpdated;
            service.OnMiniGameStarting += OnMiniGameStarting;
            service.OnDisconnected += OnDisconnected;
        }

        private void UnbindService()
        {
            if (service == null) return;
            service.OnQueueUpdated -= OnQueueUpdated;
            service.OnMiniGameStarting -= OnMiniGameStarting;
            service.OnDisconnected -= OnDisconnected;
            service = null;
        }

        private void OnQueueUpdated(string miniGameName, int currentPlayers, int requiredPlayers)
        {
            if (!queueActive || !MatchesPendingGame(miniGameName)) return;

            if (!matchVisible && revealRoutine == null)
                revealRoutine = StartCoroutine(RevealAfterMinimumTime());
        }

        private IEnumerator RevealAfterMinimumTime()
        {
            while (queueActive && loadingView != null &&
                   loadingView.VisibleSeconds < minimumLoadingSeconds)
            {
                yield return null;
            }

            if (!queueActive || pendingConfig == null) yield break;

            if (matchingUI != null) matchingUI.PrepareMatchBehindLoading(pendingConfig);
            if (loadingView != null) yield return loadingView.RevealFromCentre();

            revealRoutine = null;
            if (!queueActive || pendingConfig == null) yield break;
            matchVisible = true;
            if (matchingUI != null) matchingUI.CompleteQueueReveal();

            if (startBuffered) StartPendingGame();
        }

        private void OnMiniGameStarting(string miniGameName)
        {
            if (!queueActive || !MatchesPendingGame(miniGameName)) return;

            if (!matchVisible)
            {
                startBuffered = true;
                // 서버가 꽉 찬 방을 즉시 배정해 QueueUpdated 없이 시작 패킷만 보내도
                // 최소 노출 시간을 지킨 뒤 매칭 화면을 한 프레임 거쳐 정상 시작한다.
                if (revealRoutine == null)
                    revealRoutine = StartCoroutine(RevealAfterMinimumTime());
                return;
            }

            StartPendingGame();
        }

        private void StartPendingGame()
        {
            if (!queueActive || pendingConfig == null) return;

            queueActive = false;
            startBuffered = false;
            controller?.StartGame(pendingConfig);
        }

        private void OnMatchCancelled()
        {
            if (service != null) service.LeaveMiniGameQueue();
            queueActive = false;
            matchVisible = false;
        }

        private void OnDisconnected(string reason)
        {
            if (!queueActive) return;
            Debug.LogWarning($"[MatchingQueue] 서버 연결이 끊겼습니다: {reason}", this);
            CancelQueue(hideUi: true);
        }

        private bool MatchesPendingGame(string miniGameName)
        {
            if (string.IsNullOrEmpty(miniGameName) || pendingConfig == null) return false;

            return string.Equals(miniGameName, pendingQueueKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(miniGameName, pendingConfig.DisplayName, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(pendingConfig.SceneName) &&
                    string.Equals(miniGameName, pendingConfig.SceneName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
