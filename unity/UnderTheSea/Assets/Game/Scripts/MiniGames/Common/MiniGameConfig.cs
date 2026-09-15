using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>어느 미니게임인가.</summary>
    public enum MiniGameId
    {
        Sword = 0,
        Mining = 1,
        Ship = 2,
    }

    /// <summary>
    /// 미니게임 하나의 인원 규칙과 보상.
    ///
    /// 매칭 화면은 게임마다 따로 만들지 않는다. 화면은 하나고, 이 설정을 읽어서
    /// 슬롯 개수와 시작 조건을 바꾼다. "배는 4명이어야 한다" 같은 규칙이 UI·흐름·결과
    /// 세 곳에 if 문으로 흩어지면 한 군데만 고치고 마는 일이 생기므로 여기 한 곳에서만 정한다.
    ///
    /// 세 게임의 규칙 (기획)
    ///
    ///     검   1~2인, 혼자서도 시작
    ///     광산 1~4인, 혼자서도 시작
    ///     배   4인 고정, 다 모여야 시작
    /// </summary>
    [CreateAssetMenu(menuName = "아라아띠/미니게임 설정", fileName = "MiniGameConfig")]
    public sealed class MiniGameConfig : ScriptableObject
    {
        [Header("정체")]
        [SerializeField] private MiniGameId gameId = MiniGameId.Sword;
        [SerializeField] private string displayName = "미니게임";

        /// <summary>
        /// 이동할 씬 이름. Build Profiles 의 Scene List 에 있는 이름과 같아야 한다.
        ///
        /// ⚠ 아직 Scene List 에 없는 게임은 <b>비워 둔다.</b> 비어 있으면 흐름이 씬을 열지 않고
        ///    "여기서 이 게임을 시작한다" 는 신호만 보낸다. 없는 이름을 적어 두는 것보다
        ///    비어 있는 편이 나중에 연결할 때 빠뜨리지 않는다.
        /// </summary>
        [SerializeField] private string sceneName = string.Empty;

        [Header("인원")]
        [SerializeField, Min(1)] private int minPlayers = 1;
        [SerializeField, Min(1)] private int maxPlayers = 4;

        /// <summary>정원이 다 차야만 시작할 수 있는가. 배 협동이 여기에 해당한다.</summary>
        [SerializeField] private bool requireFullParty;

        /// <summary>
        /// 시작할 수 있게 된 뒤 이 시간이 지나면 알아서 시작한다.
        ///
        /// 혼자서도 되는 게임은 "더 올 사람이 있나" 를 잠깐 기다려 주는 시간이고,
        /// 정원이 차야 하는 게임은 다 모인 순간이 곧 시작이므로 이 값을 보지 않는다.
        /// </summary>
        [SerializeField, Min(0f)] private float autoStartSeconds = 5f;

        [Header("결과 · 보상")]
        /// <summary>결과 화면의 미니게임별 기록 라벨. 검이면 "몬스터 처치", 광산이면 "채굴량".</summary>
        [SerializeField] private string extraStatLabel = "기록";
        [SerializeField] private string rewardName = "바다의 심장 조각";

        /// <summary>
        /// 조각을 구분하는 열쇠. 같은 조각을 두 번 주지 않기 위한 것이라
        /// 게임마다 서로 달라야 한다. 예: "sword", "mining", "ship".
        /// </summary>
        [SerializeField] private string fragmentId = "sword";

        public MiniGameId GameId => gameId;
        public string DisplayName => displayName;
        public string SceneName => sceneName;
        public int MinPlayers => minPlayers;
        public int MaxPlayers => Mathf.Max(minPlayers, maxPlayers);
        public bool RequireFullParty => requireFullParty;
        public float AutoStartSeconds => autoStartSeconds;

        /// <summary><see cref="AutoStartSeconds"/> 와 같은 값. 기획 문서의 이름(AutoStartDelay)으로도 읽을 수 있게 둔다.</summary>
        public float AutoStartDelay => autoStartSeconds;
        public string ExtraStatLabel => extraStatLabel;
        public string RewardName => rewardName;
        public string FragmentId => fragmentId;

        /// <summary>지금 인원으로 시작할 수 있는가.</summary>
        public bool CanStart(int players)
        {
            if (players > MaxPlayers) return false;
            return requireFullParty ? players >= MaxPlayers : players >= minPlayers;
        }

        /// <summary>
        /// [게임 시작] 버튼을 보여 주는가.
        ///
        /// 세 게임 모두 같은 자리에 같은 크기로 둔다. 정원이 차야 하는 게임(배)은 인원이
        /// 모자라면 버튼이 <b>잠기고</b>, 다 모이면 버튼을 누르기 전에 자동 카운트다운이
        /// 먼저 시작된다. 버튼을 감추면 게임마다 버튼 줄 폭이 달라져서 화면이 들쭉날쭉했다.
        /// </summary>
        public bool ShowsStartButton => true;

        /// <summary>"1 / 4" 처럼 인원만. 한눈에 들어와야 하는 첫 번째 정보.</summary>
        public string CountText(int players) => $"{players} / {MaxPlayers}명";

        /// <summary>
        /// 언제 시작하는지 한 줄로. 두 번째 정보다.
        ///
        /// 예전에는 "3 / 4명 · 4명의 플레이어가 모두 모이면 자동으로 게임이 시작됩니다." 처럼
        /// 인원과 안내를 한 줄에 붙여 놨는데, 길어서 작게 눌리는 바람에 둘 다 잘 안 읽혔다.
        /// 인원은 <see cref="CountText"/> 가 크게 맡고 여기는 짧게만 적는다.
        /// </summary>
        public string ShortHint(int players, float autoStartRemaining, bool canStart)
        {
            if (requireFullParty)
                return players >= MaxPlayers ? "곧 시작합니다" : $"{MaxPlayers}명이 모두 모이면 시작합니다.";

            if (players < minPlayers)
                return $"최소 {minPlayers}명 필요";

            if (canStart && autoStartRemaining > 0f)
                return $"{Mathf.CeilToInt(autoStartRemaining)}초 후 자동 시작";

            return "시작할 수 있습니다";
        }

        /// <summary>
        /// 아직 시작할 수 없을 때 [게임 시작] 위에 작게 적어 줄 이유. 정원이 차야 하는 게임(배)에서만
        /// 글이 나오고, 혼자서도 되는 게임은 빈 문자열이다 — 인원 숫자는 카드가 이미 보여 주므로 적지 않는다.
        /// </summary>
        public string StartBlockedHint(int players) =>
            requireFullParty && players < MaxPlayers
                ? $"{MaxPlayers}명의 플레이어가 모두 모여야 시작할 수 있습니다."
                : string.Empty;

        /// <summary>
        /// (예전 배치) 인원과 시작 안내를 한 줄로. 지금 매칭 화면은 쓰지 않는다.
        ///
        ///     "1 / 4명 · 5초 후 자동 시작"
        ///     "3 / 4명 · 4명이 모두 모이면 시작합니다."
        /// </summary>
        public string StatusLine(int players, float autoStartRemaining, bool canStart) =>
            $"{CountText(players)} · {ShortHint(players, autoStartRemaining, canStart)}";

        /// <summary>아직 시작할 수 없을 때 화면 아래에 적어 줄 말.</summary>
        public string WaitingLine(int players)
        {
            if (requireFullParty)
                return $"{MaxPlayers}명의 플레이어가 모두 모이면 자동으로 게임이 시작됩니다.";

            if (players < minPlayers)
                return $"최소 {minPlayers}명이 모이면 시작할 수 있습니다.";

            return players >= MaxPlayers
                ? "모두 모였습니다. 곧 시작합니다."
                : "지금 시작하거나, 다른 플레이어를 조금 더 기다릴 수 있습니다.";
        }
    }
}
