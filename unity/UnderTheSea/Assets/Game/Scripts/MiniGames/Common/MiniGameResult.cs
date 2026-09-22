using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 미니게임 한 판이 끝나고 결과 화면에 넘기는 것.
    ///
    /// 게임마다 결과 화면을 새로 만들지 않는다. 화면은 하나고, 라벨과 값만 바뀐다.
    /// 검이면 "몬스터 처치 10", 광산이면 "채굴량 120", 배면 "항해 점수 5400" 이
    /// 같은 자리에 들어간다.
    ///
    /// ⚠ 미니게임 쪽에서 결과 화면의 Text 를 직접 찾아 고치지 말 것. 이 구조체 하나를
    ///    채워 INetworkService.ReportMiniGameResult에 넘긴다. 서버는 검증한 결과를
    ///    <see cref="MiniGameResultGateway.SubmitAuthoritative"/>로 돌려주며, 결과 화면이
    ///    아직 없는 씬이면 Gateway가 결과를 보관한다.
    /// </summary>
    public readonly struct MiniGameResult
    {
        public MiniGameId GameId { get; }
        public bool IsClear { get; }
        public int Score { get; }

        /// <summary>플레이 시간(초).</summary>
        public float PlayTime { get; }

        /// <summary>미니게임별 기록의 이름. 비어 있으면 그 줄을 감춘다.</summary>
        public string ExtraStatLabel { get; }
        public string ExtraStatValue { get; }

        /// <summary>이 판에 걸린 보상 id. 보통 설정의 FragmentId 를 그대로 쓴다.</summary>
        public string RewardId { get; }

        /// <summary>이 판에서 조각을 <b>새로</b> 받았는가. 적립을 마친 뒤 채워진다.</summary>
        public bool FragmentObtained { get; }

        /// <summary>같이 한 사람 수.</summary>
        public int PlayerCount { get; }

        public MiniGameResult(
            MiniGameId gameId,
            bool isClear,
            int score,
            float playTime,
            string extraStatLabel = null,
            string extraStatValue = null,
            string rewardId = null,
            bool fragmentObtained = false,
            int playerCount = 1)
        {
            GameId = gameId;
            IsClear = isClear;
            Score = score;
            PlayTime = playTime;
            ExtraStatLabel = extraStatLabel;
            ExtraStatValue = extraStatValue;
            RewardId = rewardId;
            FragmentObtained = fragmentObtained;
            PlayerCount = playerCount;
        }

        /// <summary>보상을 적립한 뒤, 그 결과만 갈아끼운 사본을 만든다.</summary>
        public MiniGameResult WithReward(string rewardId, bool fragmentObtained) =>
            new(GameId, IsClear, Score, PlayTime, ExtraStatLabel, ExtraStatValue,
                rewardId, fragmentObtained, PlayerCount);

        /// <summary>00:00 꼴로.</summary>
        public string PlayTimeText
        {
            get
            {
                int seconds = Mathf.Max(0, Mathf.RoundToInt(PlayTime));
                return $"{seconds / 60:00}:{seconds % 60:00}";
            }
        }
    }
}
