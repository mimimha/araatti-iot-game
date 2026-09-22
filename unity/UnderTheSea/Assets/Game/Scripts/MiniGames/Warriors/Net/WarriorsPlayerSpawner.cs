using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 접속한 사람마다 Warriors 플레이어를 하나씩 스폰한다.
    ///
    /// ⚠ <b><c>NetworkRunner</c> 와 같은 오브젝트에 있어야 한다.</b>
    ///    Fusion 은 러너와 같은 오브젝트에 붙은 <c>SimulationBehaviour</c> 만 자동 등록한다.
    ///    다른 오브젝트에 두면 <c>PlayerJoined</c> 가 오지 않는다. <b>오류도 없다</b> -
    ///    접속은 되고 아무도 스폰되지 않는다. ShipCoop 에서 실측한 함정이다.
    ///
    /// 사람 번호(<c>PlayerIndex</c>)는 들어온 순서가 아니라 <c>PlayerId</c> 로 정한다.
    /// 2페이즈 좌/우 담당과 3페이즈 리듬 레인이 이 번호로 갈리므로 흔들리면 안 된다.
    /// </summary>
    public sealed class WarriorsPlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("스폰할 플레이어")]
        [Tooltip("WarriorsNetPlayer 프리팹. 서연님의 StandalonePlayer 와 다른 것이어야 한다.")]
        [SerializeField] private NetworkObject playerPrefab;

        [Tooltip("자리를 하나도 못 찾았을 때 사람끼리 벌릴 간격(m).")]
        [SerializeField, Min(0.5f)] private float fallbackSpacing = 3f;

        private Transform[] spawnPoints;

        /// <summary>지금 자리를 차지한 사람들. 서버만 안다.</summary>
        private readonly Dictionary<PlayerRef, int> seats = new Dictionary<PlayerRef, int>();

        public void PlayerJoined(PlayerRef player)
        {
            Debug.Log(
                $"[WarriorsSpawner] 플레이어 입장 {player} " +
                $"(나 {Runner.LocalPlayer}, 서버 {Runner.IsServer}, 현재 인원 {Runner.ActivePlayers.Count()})");

            if (!Runner.IsServer) return;

            if (playerPrefab == null)
            {
                Debug.LogError("[WarriorsSpawner] 스폰할 프리팹이 연결되지 않았습니다.", this);
                return;
            }

            int index = ResolveIndex(player);
            ResolveSpawn(index, player, out Vector3 position, out Quaternion rotation);

            NetworkObject spawned = Runner.Spawn(playerPrefab, position, rotation, inputAuthority: player);
            Runner.SetPlayerObject(player, spawned);

            spawned.GetComponent<WarriorsPlayerLife>()?.AssignIndex(index);
            // 서버 쪽 등록 목록도 번호를 맞춘다. 클라이언트는 복제된 PlayerIndex 로 각자 맞춘다.
            spawned.GetComponent<WarriorsPlayerCombat>()?.ConfigurePlayerId(index);

            Debug.Log($"[WarriorsSpawner] {player} 스폰 완료 - {index + 1}P, {position}, 회전 {rotation.eulerAngles.y:F0}°, Id {spawned.Id}");
        }

        public void PlayerLeft(PlayerRef player)
        {
            Debug.Log(
                $"[WarriorsSpawner] 플레이어 퇴장 {player} " +
                $"(서버 {Runner.IsServer}, 남은 인원 {Runner.ActivePlayers.Count()})");

            if (!Runner.IsServer) return;

            // 자리를 비운다. 다음 사람이 이 자리를 받는다.
            seats.Remove(player);

            NetworkObject spawned = Runner.GetPlayerObject(player);
            if (spawned != null) Runner.Despawn(spawned);
        }

        /// <summary>
        /// 이 사람의 번호. 0 = 1P(왼쪽), 1 = 2P(오른쪽).
        ///
        /// **비어 있는 가장 낮은 자리**를 준다. 예전에는 <c>PlayerId % 2</c> 였는데,
        /// Fusion 의 PlayerId 는 들어온 순서대로 계속 올라가서 한 사람이 나갔다 들어오면
        /// (예: 2 와 4) 두 사람이 모두 1P 가 됐다 — 같은 자리에 겹쳐 서고 같은 레인을 받는다.
        /// </summary>
        private int ResolveIndex(PlayerRef player)
        {
            if (seats.TryGetValue(player, out int seat)) return seat;

            for (int i = 0; i < WarriorsPlayers.Max; i++)
            {
                if (seats.ContainsValue(i)) continue;

                seats[player] = i;
                return i;
            }

            // 정원을 넘었다. 세션 정원이 막아 주지만, 뚫려도 최소한 번호는 준다.
            int fallback = seats.Count % WarriorsPlayers.Max;
            seats[player] = fallback;
            Debug.LogWarning($"[WarriorsSpawner] 자리가 모두 찼는데 {player} 가 들어왔습니다. {fallback + 1}P 를 겹쳐 씁니다.", this);
            return fallback;
        }

        private void ResolveSpawn(int index, PlayerRef player, out Vector3 position, out Quaternion rotation)
        {
            Transform[] points = ResolveSpawnPoints();

            if (points.Length > 0)
            {
                // **가운데 자리부터 쓴다.**
                //
                // 자리는 x = -3, -1, +1, +3 네 곳인데 예전에는 앞에서부터 나눠 줘서 2인이
                // -3 과 -1 을 받았다. 둘 다 아레나 한가운데(x=0)의 <b>왼쪽</b>에 서고 두 사람의
                // 한가운데가 -2 로 치우쳐, 1P 는 중심에서 3m, 2P 는 1m 떨어진 서로 다른 구도를 봤다.
                // 네 자리 중 가운데 두 곳을 쓰면 -1 과 +1 이 되어 두 화면이 좌우 대칭이 된다.
                int offset = Mathf.Max(0, (points.Length - WarriorsPlayers.Max) / 2);
                Transform point = points[(offset + index) % points.Length];

                if (point != null)
                {
                    position = point.position;
                    rotation = point.rotation;
                    return;
                }
            }

            Debug.LogWarning(
                "[WarriorsSpawner] 플레이어 자리를 찾지 못했습니다. " +
                "게임 씬의 PlayerSpawn_* 에 WarriorsSpawnPoint 가 붙어 있는지 확인해 주세요.", this);

            position = transform.position + Vector3.right * (player.PlayerId * fallbackSpacing);
            rotation = transform.rotation;
        }

        /// <summary>게임 씬에서 자리를 찾아 이름순으로 줄 세운다.</summary>
        private Transform[] ResolveSpawnPoints()
        {
            if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[0] != null) return spawnPoints;

            spawnPoints = FindObjectsByType<WarriorsSpawnPoint>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(point => point.name, StringComparer.Ordinal)
                .Select(point => point.transform)
                .ToArray();

            if (spawnPoints.Length > 0)
                Debug.Log(
                    $"[WarriorsSpawner] 플레이어 자리 {spawnPoints.Length}곳을 찾았습니다 - " +
                    string.Join(", ", spawnPoints.Select(point => point.name)));

            return spawnPoints;
        }
    }
}
