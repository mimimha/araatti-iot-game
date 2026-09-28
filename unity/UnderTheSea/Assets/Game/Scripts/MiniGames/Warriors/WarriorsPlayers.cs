using System;
using System.Collections.Generic;

namespace Warriors
{
    /// <summary>
    /// 이 게임의 인원 규격.
    ///
    /// 한때는 4명 고정이었다. 촉수 4개가 상시 떠 있고 협동 마무리가 네 명을 전제했기 때문이다.
    /// 지금은 촉수가 패턴별로 1~2개씩 올라오는 구조라 그 전제가 사라졌고,
    /// 게임은 <b>1~2인</b>이다. 협동은 "없으면 못 깬다"가 아니라
    /// "같이 하면 더 빠르고 시원하다" 쪽이다.
    ///
    /// 인원 상한이 여러 곳(진행·HUD·리듬 레인)에 숫자로 흩어지면 한 군데만 고치고 마는 일이
    /// 생기므로 여기 한 곳에서만 정한다.
    /// </summary>
    public static class WarriorsPlayers
    {
        /// <summary>동시에 참여할 수 있는 최대 인원.</summary>
        public const int Max = 2;

        /// <summary>
        /// Who is actually in the battle right now. The count used to be taken once when the
        /// flow woke up, which is fine for a scene that starts with everyone already in it and
        /// wrong the moment a player arrives or leaves - the round after a join would still be
        /// running on the old number. Players put themselves on this list instead.
        /// </summary>
        public static IReadOnlyList<WarriorsPlayerCombat> Active => Registered;

        public static int Count => Registered.Count;

        /// <summary>Raised whenever someone joins or leaves.</summary>
        public static event Action Changed;

        private static readonly List<WarriorsPlayerCombat> Registered = new();

        public static void Register(WarriorsPlayerCombat combat)
        {
            if (combat == null || Registered.Contains(combat)) return;
            Registered.Add(combat);
            Registered.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
            Changed?.Invoke();
        }

        public static void Unregister(WarriorsPlayerCombat combat)
        {
            if (combat == null || !Registered.Remove(combat)) return;
            Changed?.Invoke();
        }

        /// <summary>The player answering for this id, or null when nobody is.</summary>
        public static WarriorsPlayerCombat ForId(int playerId)
        {
            foreach (WarriorsPlayerCombat combat in Registered)
                if (combat != null && combat.PlayerId == playerId) return combat;
            return null;
        }
    }
}
