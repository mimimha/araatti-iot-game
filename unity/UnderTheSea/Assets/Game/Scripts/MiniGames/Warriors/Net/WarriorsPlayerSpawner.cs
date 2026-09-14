using System;
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

            Debug.Log($"[WarriorsSpawner] {player} 스폰 완료 - {index + 1}P, {position}, Id {spawned.Id}");
        }

        public void PlayerLeft(PlayerRef player)
        {
            Debug.Log(
                $"[WarriorsSpawner] 플레이어 퇴장 {player} " +
                $"(서버 {Runner.IsServer}, 남은 인원 {Runner.ActivePlayers.Count()})");

            if (!Runner.IsServer) return;

            NetworkObject spawned = Runner.GetPlayerObject(player);
            if (spawned != null) Runner.Despawn(spawned);
        }

        /// <summary>
        /// 이 사람의 번호. 0 = 1P(왼쪽), 1 = 2P(오른쪽).
        ///
        /// 최대 2명이라 <c>PlayerId</c> 를 인원 수로 나눈다. 나갔다 들어와도 같은 번호다.
        /// </summary>
        private static int ResolveIndex(PlayerRef player)
        {
            return Mathf.Abs(player.PlayerId) % WarriorsPlayers.Max;
        }

        private void ResolveSpawn(int index, PlayerRef player, out Vector3 position, out Quaternion rotation)
        {
            Transform[] points = ResolveSpawnPoints();

            if (points.Length > 0)
            {
                Transform point = points[index % points.Length];

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
