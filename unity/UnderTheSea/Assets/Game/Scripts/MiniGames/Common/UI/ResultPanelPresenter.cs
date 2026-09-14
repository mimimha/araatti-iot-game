using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 세 미니게임이 함께 쓰는 결과 화면.
    ///
    /// 게임마다 결과 화면을 새로 만들지 않는다. <see cref="Show"/> 에 결과를 넘기면
    /// 라벨과 값만 바뀐다. 보상 줄은 <see cref="SeaHeartFragments"/> 에 물어보고
    /// 처음 받는 조각일 때만 "획득" 으로, 이미 가진 조각이면 "이미 보유" 로 적는다.
    ///
    /// 버튼은 스스로 씬을 열지 않는다. 누구를 부를지는 이 화면을 띄운 쪽이 정한다.
    /// (GAME_STRUCTURE.md 3장 — 씬 전환은 SceneFlow 한 곳에서)
    /// </summary>
    public sealed class ResultPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text scoreValue;
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private TMP_Text timeValue;
        [SerializeField] private GameObject extraRow;
        [SerializeField] private TMP_Text extraLabel;
        [SerializeField] private TMP_Text extraValue;

        [Header("보상")]
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private TMP_Text rewardName;
        [SerializeField] private TMP_Text rewardState;

        [Header("버튼")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button lobbyButton;

        [Header("색")]
        [SerializeField] private Color clearAccent = new(1f, .82f, .35f, 1f);
        [SerializeField] private Color failAccent = new(1f, .48f, .42f, 1f);
        [SerializeField] private Color freshReward = new(1f, .82f, .35f, 1f);
        [SerializeField] private Color ownedReward = new(.55f, .87f, .98f, 1f);

        /// <summary>[다시 하기] 를 눌렀을 때.</summary>
        public event Action RetryRequested;

        /// <summary>[로비로] 를 눌렀을 때.</summary>
        public event Action LobbyRequested;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (retryButton != null) retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
            if (lobbyButton != null) lobbyButton.onClick.AddListener(() => LobbyRequested?.Invoke());
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>결과를 띄운다. 보상은 처음 클리어했을 때만 새로 들어온다.</summary>
        public void Show(in MiniGameResult result, MiniGameConfig config)
        {
            if (root != null) root.SetActive(true);

            Color accent = result.IsClear ? clearAccent : failAccent;

            if (titleText != null)
            {
                titleText.text = result.IsClear ? "GAME CLEAR" : "GAME OVER";
                titleText.color = accent;
            }

            if (subtitleText != null)
                subtitleText.text = result.IsClear ? "미션 성공" : "다시 도전해 보세요";

            if (scoreLabel != null) scoreLabel.text = "최종 점수";
            if (scoreValue != null) scoreValue.text = result.Score.ToString("N0");

            if (timeLabel != null) timeLabel.text = "플레이 시간";
            if (timeValue != null) timeValue.text = result.PlayTimeText;

            bool hasExtra = !string.IsNullOrEmpty(result.ExtraStatLabel);
            if (extraRow != null) extraRow.SetActive(hasExtra);
            if (hasExtra)
            {
                if (extraLabel != null) extraLabel.text = result.ExtraStatLabel;
                if (extraValue != null) extraValue.text = result.ExtraStatValue ?? string.Empty;
            }

            ShowReward(result, config);
        }

        private void ShowReward(in MiniGameResult result, MiniGameConfig config)
        {
            // 실패한 판은 조각을 주지 않는다.
            bool eligible = result.IsClear && config != null && !string.IsNullOrEmpty(config.FragmentId);
            if (rewardRoot != null) rewardRoot.SetActive(eligible);
            if (!eligible) return;

            // 적립은 MatchFlowController 가 이미 끝냈다. 화면은 그 결과를 읽기만 한다 —
            // 화면이 스스로 Grant 를 부르면 결과 창을 다시 열 때마다 한 번씩 더 들어간다.
            if (rewardName != null) rewardName.text = config.RewardName;

            if (rewardState == null) return;

            if (result.FragmentObtained)
            {
                rewardState.text = "획득!";
                rewardState.color = freshReward;
            }
            else
            {
                rewardState.text = RewardService.Has(result.RewardId) ? "이미 보유 중" : "획득 실패";
                rewardState.color = ownedReward;
            }
        }
    }
}
