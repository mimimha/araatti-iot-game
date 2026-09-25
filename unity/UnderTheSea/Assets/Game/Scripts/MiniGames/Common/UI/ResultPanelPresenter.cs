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
    /// 라벨과 값만 바뀐다. 보상 줄은 성공한 판마다 바다의 심장 조각 1개를 보여 준다
    /// (설계 문서 결정 #1 — 같은 게임을 다시 깨도 또 1개). 지급 여부의 최종 판단은 서버다.
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

        /// <summary>
        /// 결과를 띄운다.
        ///
        /// <b>보상 칸은 클리어했을 때만, 그리고 클리어하면 무조건 뜬다.</b> (기획 결정)
        /// 서버 지급 결과를 기다리거나 읽지 않는다 — 클리어했는데 "지급되지 않았다" 를 보여 주는
        /// 경우는 없다. 게임 오버면 보상 칸이 통째로 없고, 버튼이 그 빈자리로 올라온다.
        /// </summary>
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

            // 보상 칸: 클리어 = 조각 그림 + 이름. 상태 문구는 쓰지 않는다.
            bool reward = result.IsClear;
            if (rewardRoot != null) rewardRoot.SetActive(reward);
            if (rewardName != null)
                rewardName.text = config != null ? config.RewardName : "바다의 심장 조각";
            if (rewardState != null) rewardState.text = string.Empty;

            PlaceLayout(reward);
        }

        /// <summary>
        /// 게임 오버일 때 기록 칸을 내리는 거리. 보상 칸이 빠진 자리의 절반쯤이다 —
        /// 기록 칸이 부제와 버튼 사이 한가운데에 오게 한다. (판 그림은 늘릴 수 없어 높이가 고정이다)
        /// </summary>
        private const float NoRewardStatDrop = 106f;

        private Vector2 lobbyBase;
        private Vector2 retryBase;
        private Vector2 statBase;
        private bool layoutBaseCaptured;

        /// <summary>
        /// 자리를 정한다.
        ///   · [다시 하기] 가 숨겨져 [로비로] 혼자면 가운데로 (포탈로 들어온 판은 늘 이렇다)
        ///   · 보상 칸이 없으면(게임 오버) 기록 칸을 내려 판 가운데가 텅 비지 않게 한다
        /// </summary>
        private void PlaceLayout(bool hasReward)
        {
            RectTransform statPanel = extraRow != null ? extraRow.transform.parent as RectTransform : null;

            if (!layoutBaseCaptured)
            {
                if (lobbyButton != null) lobbyBase = ((RectTransform)lobbyButton.transform).anchoredPosition;
                if (retryButton != null) retryBase = ((RectTransform)retryButton.transform).anchoredPosition;
                if (statPanel != null) statBase = statPanel.anchoredPosition;
                layoutBaseCaptured = true;
            }

            bool alone = retryButton == null || !retryButton.gameObject.activeSelf;
            if (lobbyButton != null)
                ((RectTransform)lobbyButton.transform).anchoredPosition = new Vector2(alone ? 0f : lobbyBase.x, lobbyBase.y);

            if (statPanel != null)
                statPanel.anchoredPosition = new Vector2(statBase.x, statBase.y - (hasReward ? 0f : NoRewardStatDrop));
        }
    }
}
