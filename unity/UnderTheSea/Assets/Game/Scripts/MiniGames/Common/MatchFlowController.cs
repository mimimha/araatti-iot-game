using System;
using System.Collections;
using UnityEngine;
using MiniGames.Common.UI;

namespace MiniGames.Common
{
    /// <summary>
    /// 매칭 · 미니게임 · 결과를 하나로 잇는 조종석.
    ///
    /// <b>바깥에서 볼 곳은 여기 하나다.</b> 미니게임을 만드는 사람도, 네트워크를 붙이는
    /// 사람도 이 파일의 public 함수만 알면 된다. <see cref="MatchFlow"/> 안쪽이나 결과
    /// 화면의 계층 구조를 뒤질 일이 없다.
    ///
    ///     [게임 시작] 버튼  → <see cref="RequestStart"/>  (조건이 맞으면 카운트다운)
    ///     [매칭 취소] 버튼  → <see cref="CancelMatch"/>
    ///     카운트다운 끝    → (자동) <see cref="StartGame"/>  ← 씬 이동의 <b>유일한</b> 진입점
    ///     게임이 끝남      → <see cref="CompleteMiniGame"/>
    ///     다시 하기        → <see cref="ReplayCurrentMiniGame"/>
    ///     로비로           → <see cref="ReturnToLobby"/>
    ///
    /// 보상 적립은 <see cref="RewardService"/> 가, 씬 열기는
    /// <see cref="SceneTransitionService"/> 가 한다. 이 파일은 순서만 정한다.
    ///
    /// ── 서버·네트워크 담당자가 바꿀 곳 ──────────────────────────────
    ///
    /// 씬 이동은 <see cref="StartGame"/> 한 곳에서만 일어난다. Fusion 의 네트워크 씬 로드로
    /// 바꿀 때는 <see cref="SceneTransitionService.LoadMiniGame"/> 안쪽만 갈아 끼우면 되고,
    /// 서버가 "지금 시작" 을 내려 준다면 클라이언트에서 <see cref="StartGame"/> 을 직접 불러도 된다.
    /// 두 번 불려도 한 번만 실행된다.
    /// </summary>
    public sealed class MatchFlowController : MonoBehaviour
    {
        [SerializeField] private MatchFlow flow;
        [SerializeField] private SceneTransitionService sceneTransition;

        [Tooltip("결과 화면. 매칭 전용 프리팹에는 없으므로 비워 둘 수 있다.")]
        [SerializeField] private ResultPanelPresenter resultPanel;

        [SerializeField]
        [Tooltip("켜 두면 매칭이 끝나도 씬을 열지 않는다. 테스트 씬에서 쓴다.")]
        private bool suppressSceneLoad;

        [SerializeField]
        [Tooltip("[매칭 취소] 를 누르면 로비 씬으로 돌아간다. 로비 안에서 매칭 창만 띄우는 구조라면 끈다.")]
        private bool returnToLobbyOnCancel = true;

        /// <summary>매칭이 끝나 이 게임을 시작할 차례. 테스트 씬은 여기서 씬 대신 매칭으로 되돌아간다.</summary>
        public event Action<MiniGameConfig> MiniGameStarting;

        /// <summary>[매칭 취소] 로 파티가 비워졌다.</summary>
        public event Action MatchCancelled;

        /// <summary>결과 화면을 띄운 직후. 적립까지 끝난 결과가 넘어온다.</summary>
        public event Action<MiniGameResult> ResultShown;

        public MatchFlow Flow => flow;
        public MiniGameConfig CurrentConfig => flow != null ? flow.Config : null;

        /// <summary>마지막 판의 결과. 로비가 물어볼 수 있다.</summary>
        public MiniGameResult LastResult { get; private set; }

        /// <summary>
        /// 이 판에서 이미 <see cref="StartGame"/> 이 실행됐는가. 카운트다운이 끝나는 프레임에
        /// 서버 신호까지 같이 들어와도 씬은 한 번만 열린다. 매칭 상태로 돌아오면 풀린다.
        /// </summary>
        public bool HasStarted { get; private set; }

        private void OnEnable()
        {
            // 씬이 막 열린 프레임에는 MatchFlow.Start가 아직 설정을 고르는 중일 수 있다.
            // 한 프레임 뒤 등록하면 보관된 서버 결과가 초기화에 덮이지 않고 결과 화면으로 간다.
            StartCoroutine(RegisterResultReceiverAfterSceneReady());

            if (flow != null)
            {
                flow.LaunchRequested += OnLaunchRequested;
                flow.MatchCancelled += OnMatchCancelled;
                flow.StateChanged += OnFlowStateChanged;
            }

            if (resultPanel != null)
            {
                resultPanel.RetryRequested += ReplayCurrentMiniGame;
                resultPanel.LobbyRequested += ReturnToLobby;
            }
        }

        private void OnDisable()
        {
            MiniGameResultGateway.Unregister(OnResultSubmitted);

            if (flow != null)
            {
                flow.LaunchRequested -= OnLaunchRequested;
                flow.MatchCancelled -= OnMatchCancelled;
                flow.StateChanged -= OnFlowStateChanged;
            }

            if (resultPanel != null)
            {
                resultPanel.RetryRequested -= ReplayCurrentMiniGame;
                resultPanel.LobbyRequested -= ReturnToLobby;
            }
        }

        // ------------------------------------------------------------
        // 매칭 화면의 두 버튼
        // ------------------------------------------------------------

        /// <summary>[게임 시작]. 지금 인원으로 시작할 수 없으면 아무 일도 하지 않는다.</summary>
        public void RequestStart()
        {
            if (flow != null) flow.RequestStart();
        }

        /// <summary>[매칭 취소]. 파티를 비우고, 설정에 따라 로비로 돌아간다.</summary>
        public void CancelMatch()
        {
            if (flow != null) flow.CancelMatch();
        }

        /// <summary>지금 인원으로 시작할 수 있는가. 조건식은 <see cref="MatchFlow.CanStartMatch"/> 한 곳에 있다.</summary>
        public bool CanStartMatch() => flow != null && flow.CanStartMatch();

        // ------------------------------------------------------------
        // 씬 이동 — 유일한 진입점
        // ------------------------------------------------------------

        /// <summary>
        /// 게임을 실제로 시작한다. 카운트다운이 끝나면 자동으로 불리고, 서버가 시작을 내려 줄 때
        /// 직접 불러도 된다. <b>한 판에 한 번만</b> 실행된다 — 두 번째부터는 무시한다.
        ///
        /// 씬을 여는 코드는 <see cref="SceneTransitionService.LoadMiniGame"/> 에만 있다.
        /// 버튼이나 UI 가 <c>SceneManager.LoadScene</c> 을 직접 부르지 않는다.
        /// </summary>
        public void StartGame(MiniGameConfig config)
        {
            if (HasStarted)
            {
                Debug.LogWarning("[MatchFlowController] StartGame 이 두 번 불렸습니다. 두 번째는 무시합니다.", this);
                return;
            }

            HasStarted = true;

            if (flow != null) flow.EnterInGame();
            MiniGameStarting?.Invoke(config);

            if (suppressSceneLoad) return;
            if (sceneTransition != null && config != null)
                sceneTransition.LoadMiniGame(config.SceneName);
        }

        // ------------------------------------------------------------
        // 미니게임이 부를 것 (결과 흐름 — 이 브랜치 범위 밖. 그대로 둔다)
        // ------------------------------------------------------------

        /// <summary>
        /// 미니게임 한 판이 끝났다. 세 게임 모두 <b>이 함수 하나만</b> 부르면 된다.
        ///
        /// 보상 적립 → 결과 화면 표시 순으로 처리한다. 결과 화면의 Text 를 직접 찾아
        /// 고칠 필요가 없고, 보상을 두 번 주는 일도 여기서 막힌다.
        /// </summary>
        public void CompleteMiniGame(MiniGameResult result)
        {
            MiniGameConfig config = CurrentConfig;

            // 보상은 화면보다 먼저 적립한다. 화면이 "획득!" 이라고 말해 놓고
            // 실제로는 안 들어가 있는 상태를 만들지 않기 위해서다.
            string rewardId = !string.IsNullOrEmpty(result.RewardId)
                ? result.RewardId
                : config != null ? config.FragmentId : null;

            bool obtained = result.IsClear && RewardService.Grant(rewardId);
            MiniGameResult settled = result.WithReward(rewardId, obtained);

            LastResult = settled;

            if (flow != null) flow.EnterResult();
            if (resultPanel != null) resultPanel.Show(settled, config);

            ResultShown?.Invoke(settled);
        }

        /// <summary>
        /// 서버가 성공 여부·점수·보상 지급 여부를 확정한 뒤 부르는 진입점.
        /// 서버 판정을 클라이언트에서 다시 계산하지 않고 그대로 표시한다.
        /// </summary>
        public void CompleteAuthoritativeMiniGame(MiniGameResult result)
        {
            if (result.FragmentObtained && !string.IsNullOrEmpty(result.RewardId))
                RewardService.ApplyAuthoritativeGrant(result.RewardId);

            LastResult = result;

            if (flow != null) flow.EnterResult();
            if (resultPanel != null) resultPanel.Show(result, ResolveConfig(result.GameId));

            ResultShown?.Invoke(result);
        }

        /// <summary>
        /// 결과 구조체를 직접 만들기 번거로울 때 쓰는 짧은 길.
        /// 인원 수와 보상 id 는 지금 설정에서 알아서 채운다.
        /// </summary>
        public void CompleteMiniGame(bool isClear, int score, float playTime, string extraStatValue)
        {
            MiniGameConfig config = CurrentConfig;
            var result = new MiniGameResult(
                config != null ? config.GameId : MiniGameId.Sword,
                isClear,
                score,
                playTime,
                config != null ? config.ExtraStatLabel : null,
                extraStatValue,
                config != null ? config.FragmentId : null,
                fragmentObtained: false,
                playerCount: PlayerRoster.ActivePlayerCount);

            CompleteMiniGame(result);
        }

        /// <summary>인원은 그대로 두고 같은 게임을 다시. 매칭부터 하지 않는다.</summary>
        public void ReplayCurrentMiniGame()
        {
            if (resultPanel != null) resultPanel.Hide();
            if (flow != null) flow.ReturnToMatching();

            if (suppressSceneLoad) return;
            if (sceneTransition != null) sceneTransition.ReloadMiniGame();
        }

        /// <summary>결과·보상 처리를 마치고 로비로.</summary>
        public void ReturnToLobby()
        {
            if (resultPanel != null) resultPanel.Hide();

            PlayerRoster.ClearPlayers();
            if (flow != null) flow.ReturnToMatching();

            if (sceneTransition != null) sceneTransition.LoadLobby();
        }

        // ------------------------------------------------------------
        // 안쪽
        // ------------------------------------------------------------

        private void OnLaunchRequested(MiniGameConfig config) => StartGame(config);

        private IEnumerator RegisterResultReceiverAfterSceneReady()
        {
            yield return null;
            if (isActiveAndEnabled) MiniGameResultGateway.Register(OnResultSubmitted);
        }

        private void OnResultSubmitted(MiniGameResult result, MiniGameResultOrigin origin)
        {
            if (origin == MiniGameResultOrigin.Server) CompleteAuthoritativeMiniGame(result);
            else CompleteMiniGame(result);
        }

        private MiniGameConfig ResolveConfig(MiniGameId gameId)
        {
            MiniGameConfig current = CurrentConfig;
            if (current != null && current.GameId == gameId) return current;

            MiniGameConfig rosterConfig = PlayerRoster.CurrentGame;
            return rosterConfig != null && rosterConfig.GameId == gameId ? rosterConfig : current;
        }

        private void OnMatchCancelled()
        {
            MatchCancelled?.Invoke();

            if (!returnToLobbyOnCancel) return;
            if (sceneTransition != null) sceneTransition.LoadLobby();
        }

        private void OnFlowStateChanged(MatchState state)
        {
            // 매칭으로 돌아오면 다음 판을 열 수 있어야 한다.
            if (state == MatchState.Matching) HasStarted = false;
        }
    }
}
