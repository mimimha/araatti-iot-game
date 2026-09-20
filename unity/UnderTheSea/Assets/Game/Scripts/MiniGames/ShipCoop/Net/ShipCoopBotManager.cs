using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnderTheSea.MiniGames.ShipCoop.AI;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 서버에서 사람에게 배정되지 않은 승무원 슬롯을 봇으로 채운다.
    ///
    /// 빈 서버에는 봇을 만들지 않는다. 첫 사람이 들어오면 총 승무원이 목표 인원이 되도록
    /// 채우고, 사람이 추가로 들어오면 같은 슬롯의 봇을 내보낸다. 마지막 사람이 나가면
    /// 다음 판을 위해 모든 봇을 정리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopBotManager : SimulationBehaviour, IPlayerJoined, IPlayerLeft
    {
        private readonly Dictionary<int, NetworkObject> botsBySlot = new Dictionary<int, NetworkObject>();

        private ShipCoopPlayerSpawner spawner;
        private int targetCrew = 4;

        public static int ActiveBotCount { get; private set; }

        public int BotCount => botsBySlot.Count;

        public void Configure(ShipCoopPlayerSpawner playerSpawner, int crewSize)
        {
            spawner = playerSpawner;
            targetCrew = Mathf.Max(1, crewSize);
        }

        public void PlayerJoined(PlayerRef player)
        {
            if (Runner.IsServer)
            {
                ReconcileBots();
            }
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (Runner.IsServer)
            {
                // Fusion 버전에 따라 콜백 시점에 떠난 플레이어가 ActivePlayers에 잠깐 남을 수 있어 명시적으로 제외한다.
                ReconcileBots(player);
            }
        }

        private void OnDestroy()
        {
            ActiveBotCount = 0;
        }

        private void ReconcileBots(PlayerRef leaving = default)
        {
            if (spawner == null)
            {
                spawner = GetComponent<ShipCoopPlayerSpawner>();
            }

            if (spawner == null)
            {
                Debug.LogError("[ShipCoopBotManager] ShipCoopPlayerSpawner를 찾지 못했습니다.", this);
                return;
            }

            HashSet<int> humanSlots = new HashSet<int>();

            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (leaving.IsRealPlayer && player == leaving)
                {
                    continue;
                }

                humanSlots.Add(SlotOf(player));
            }

            // 사람 없는 서버에서는 봇끼리 게임하지 않는다.
            if (humanSlots.Count == 0)
            {
                RemoveAllBots();
                return;
            }

            foreach (int occupied in humanSlots)
            {
                RemoveBot(occupied);
            }

            for (int slot = 0; slot < targetCrew; slot++)
            {
                if (humanSlots.Contains(slot) || botsBySlot.ContainsKey(slot))
                {
                    continue;
                }

                NetworkObject bot = spawner.SpawnBot(slot);

                if (bot != null)
                {
                    botsBySlot.Add(slot, bot);
                    ShipCoopBotBrain brain = bot.gameObject.AddComponent<ShipCoopBotBrain>();
                    brain.Configure(slot);
                    bot.gameObject.AddComponent<ShipCoopBotNavigator>();
                    bot.gameObject.AddComponent<ShipCoopBotAction>();
                }
            }

            RefreshCount();
            Debug.Log(
                $"[ShipCoopBotManager] 승무원 구성 — 사람 {humanSlots.Count}명, AI {BotCount}명, 목표 {targetCrew}명",
                this);
        }

        private int SlotOf(PlayerRef player)
        {
            return Mathf.Abs(player.PlayerId) % targetCrew;
        }

        private void RemoveBot(int slot)
        {
            if (!botsBySlot.Remove(slot, out NetworkObject bot))
            {
                return;
            }

            spawner.DespawnBot(bot);
            RefreshCount();
        }

        private void RemoveAllBots()
        {
            NetworkObject[] bots = botsBySlot.Values.Where(bot => bot != null).ToArray();
            botsBySlot.Clear();

            foreach (NetworkObject bot in bots)
            {
                spawner.DespawnBot(bot);
            }

            RefreshCount();
            Debug.Log("[ShipCoopBotManager] 사람이 모두 나가 AI 승무원을 정리했습니다.", this);
        }

        private void RefreshCount()
        {
            ActiveBotCount = botsBySlot.Count;
        }
    }
}
