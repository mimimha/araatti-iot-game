using System;
using System.Collections.Generic;

namespace MiniGames.Common
{
    /// <summary>
    /// 보상을 실제로 적립하는 곳.
    ///
    /// 결과 화면은 <b>보여 주기만</b> 한다. 무엇을 얼마나 가졌는지는 여기가 기억한다.
    /// 화면이 보상 상태를 직접 들고 있으면 화면을 닫는 순간 사라지고, 로비가 다시 물어볼
    /// 곳도 없어진다.
    ///
    /// 세 미니게임이 각자 다른 조각을 준다. 이미 가진 조각은 다시 주지 않는다 —
    /// 두 번째 클리어부터는 결과 화면이 "획득" 대신 "이미 보유" 로 바뀐다.
    ///
    /// ── 서버 담당자가 붙일 곳 ─────────────────────────────────────
    ///
    /// 지금은 <b>실행 중에만</b> 기억한다(플레이 모드를 나가면 사라진다). 서버 저장이
    /// 생기면 <see cref="Grant"/> · <see cref="Has"/> · <see cref="LoadFrom"/> 안쪽만
    /// 서버 호출로 갈아끼우면 된다. 부르는 쪽(결과 화면·로비)은 바뀌지 않는다.
    /// </summary>
    public static class RewardService
    {
        private static readonly HashSet<string> OwnedFragments = new();

        /// <summary>조각이 새로 들어왔을 때. 이미 가진 것을 다시 받았을 때는 울리지 않는다.</summary>
        public static event Action<string> FragmentObtained;

        /// <summary>보유 조각 수가 바뀔 때마다. 로비의 회복 게이지가 듣는다.</summary>
        public static event Action Changed;

        public static int OwnedCount => OwnedFragments.Count;

        public static IReadOnlyCollection<string> Owned => OwnedFragments;

        /// <summary>
        /// 바다의 심장이 얼마나 돌아왔는가 (0~1).
        /// 조각 종류 수를 기준으로 센다. 지금은 세 미니게임 = 세 조각.
        /// </summary>
        public static float RecoveryRatio =>
            TotalFragmentCount <= 0 ? 0f : OwnedFragments.Count / (float)TotalFragmentCount;

        /// <summary>모아야 하는 조각의 전체 개수. 미니게임이 늘면 이 값을 올린다.</summary>
        public static int TotalFragmentCount { get; set; } = 3;

        public static bool Has(string fragmentId) =>
            !string.IsNullOrEmpty(fragmentId) && OwnedFragments.Contains(fragmentId);

        /// <summary>처음 받는 조각이면 true. 이미 있으면 아무 일도 하지 않고 false.</summary>
        public static bool Grant(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return false;
            if (!OwnedFragments.Add(fragmentId)) return false;

            FragmentObtained?.Invoke(fragmentId);
            Changed?.Invoke();
            return true;
        }

        /// <summary>서버에서 받은 보유 목록으로 통째로 덮어쓴다. 로그인 직후에 쓸 자리.</summary>
        public static void LoadFrom(IEnumerable<string> fragmentIds)
        {
            OwnedFragments.Clear();
            if (fragmentIds != null)
                foreach (string id in fragmentIds)
                    if (!string.IsNullOrEmpty(id)) OwnedFragments.Add(id);

            Changed?.Invoke();
        }

        /// <summary>테스트에서 처음 클리어 상태를 다시 보고 싶을 때.</summary>
        public static void ResetAll()
        {
            if (OwnedFragments.Count == 0) return;

            OwnedFragments.Clear();
            Changed?.Invoke();
        }
    }
}
