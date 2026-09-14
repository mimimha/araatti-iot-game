using System;
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
    ///     매칭이 다 됨   → (자동) LaunchRequested 를 받아 씬을 연다
    ///     게임이 끝남    → <see cref="CompleteMiniGame"/>
    ///     다시 하기      → <see cref="ReplayCurrentMiniGame"/>
    ///     로비로         → <see cref="ReturnToLobby"/>
    ///
    /// 보상 적립은 <see cref="RewardService"/> 가, 씬 열기는
    /// <see cref="SceneTransitionService"/> 가 한다. 이 파일은 순서만 정한다.
    /// </summary>
    public sealed class MatchFlowController : MonoBehaviour
    {
        [SerializeField] private MatchFlow flow;
        [SerializeField] private SceneTransitionService sceneTransition;
        [SerializeField] private ResultPanelPresenter resultPanel;

        [SerializeField]
        [Tooltip("켜 두면 매칭이 끝나도 씬을 열지 않는다. 테스트 씬에서 가짜 한 판을 돌릴 때 쓴다.")]
        private bool suppressSceneLoad;

        /// <summary>매칭이 끝나 이 게임을 시작할 차례. 테스트 리그가 가짜 한 판을 여기서 돌린다.</summary>
        public event Action<MiniGameConfig> MiniGameStarting;

        /// <summary>결과 화면을 띄운 직후. 적립까지 끝난 결과가 넘어온다.</summary>
        public event Action<MiniGameResult> ResultShown;

        public MatchFlow Flow => flow;
        public MiniGameConfig CurrentConfig => flow != null ? flow.Config : null;

        /// <summary>마지막 판의 결과. 로비가 물어볼 수 있다.</summary>
        public MiniGameResult LastResult { get; private set; }

        private void OnEnable()
        {
            if (flow != null) flow.LaunchRequested += OnLaunchRequested;

            if (resultPanel != null)
            {
                resultPanel.RetryRequested += ReplayCurrentMiniGame;
                resultPanel.LobbyRequested += ReturnToLobby;
            }
        }

        private void OnDisable()
        {
            if (flow != null) flow.LaunchRequested -= OnLaunchRequested;

            if (resultPanel != null)
            {
                resultPanel.RetryRequested -= ReplayCurrentMiniGame;
                resultPanel.LobbyRequested -= ReturnToLobby;
            }
        }

        // ------------------------------------------------------------
        // 미니게임이 부를 것
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

        // ------------------------------------------------------------
        // 결과 화면의 두 버튼
        // ------------------------------------------------------------

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

            PlayerRoster.Clear();
            if (flow != null) flow.ReturnToMatching();

            if (sceneTransition != null) sceneTransition.LoadLobby();
        }

        // ------------------------------------------------------------
        // 안쪽
        // ------------------------------------------------------------

        private void OnLaunchRequested(MiniGameConfig config)
        {
            if (flow != null) flow.EnterInGame();
            MiniGameStarting?.Invoke(config);

            if (suppressSceneLoad) return;
            if (sceneTransition != null && config != null)
                sceneTransition.LoadMiniGame(config.SceneName);
        }
    }
}
