using System;
using System.Linq;
using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 접속한 사람마다 ShipCoop 플레이어를 하나씩 스폰한다.
    ///
    /// <b>봇이 아니다.</b> 스폰한 오브젝트의 <c>InputAuthority</c> 를 그 사람에게 주므로,
    /// 각 클라이언트가 자기 캐릭터만 조작한다. 서버는 아무도 조작하지 않는다.
    ///
    /// ⚠ <b><c>NetworkRunner</c> 와 같은 오브젝트에 있어야 한다.</b>
    ///    Fusion 은 러너와 같은 오브젝트에 붙은 <c>SimulationBehaviour</c> 만 자동으로 등록한다.
    ///    다른 오브젝트(심지어 러너가 연 씬 안이어도)에 두면 <c>PlayerJoined</c> 가 오지 않는다.
    ///    <b>오류도 나지 않는다</b> — 접속은 되고 아무도 스폰되지 않는다. 실측해서 알아냈다.
    ///
    /// <b>Lobby 의 <c>PlayerSpawner</c> 를 쓰지 않는 이유</b>
    ///   · 스폰 자리가 다르다 — Lobby 는 지형 위, 여기는 <b>갑판 위</b>
    ///   · 스폰 프리팹이 다르다 — <c>ShipCoopPlayer</c> 에는 Lobby 전용 부품이 없다
    ///   · Lobby 파일을 건드리지 않기로 했다
    ///
    /// 문서: SHIPCOOP.md 11장 ("접속할 때 생성된다. 갑판에 스폰 자리 4곳이 필요하다")
    /// </summary>
    public sealed class ShipCoopPlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("스폰할 플레이어")]
        [Tooltip("ShipCoopPlayer 프리팹. Lobby 의 NetworkPlayer 와 다른 것이어야 한다.")]
        [SerializeField] private NetworkObject playerPrefab;

        [Tooltip("스폰 자리를 하나도 못 찾았을 때 사람끼리 벌릴 간격(m).")]
        [SerializeField, Min(0.5f)] private float fallbackSpacing = 3f;

        /// <summary>
        /// 갑판 위 스폰 자리. 처음 쓸 때 찾아 둔다.
        ///
        /// 인스펙터로 잇지 못한다 — 러너는 시작 씬에, 자리는 게임 씬에 있고
        /// 유니티는 씬을 넘는 참조를 저장하지 못한다. (<see cref="ShipCoopSpawnPoint"/>)
        /// </summary>
        private Transform[] spawnPoints;

        public void PlayerJoined(PlayerRef player)
        {
            Debug.Log(
                $"[ShipCoopSpawner] 플레이어 입장 {player} " +
                $"(나 {Runner.LocalPlayer}, 서버 {Runner.IsServer}, 현재 인원 {Runner.ActivePlayers.Count()})");

            if (!Runner.IsServer)
            {
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[ShipCoopSpawner] 스폰할 프리팹이 연결되지 않았습니다.", this);
                return;
            }

            ResolveSpawn(player, out Vector3 position, out Quaternion rotation);

            NetworkObject spawned = Runner.Spawn(playerPrefab, position, rotation, inputAuthority: player);
            Runner.SetPlayerObject(player, spawned);

            Debug.Log($"[ShipCoopSpawner] {player} 스폰 완료 — {position}, Id {spawned.Id}");
        }

        public void PlayerLeft(PlayerRef player)
        {
            Debug.Log(
                $"[ShipCoopSpawner] 플레이어 퇴장 {player} " +
                $"(서버 {Runner.IsServer}, 남은 인원 {Runner.ActivePlayers.Count()})");

            if (!Runner.IsServer)
            {
                return;
            }

            NetworkObject spawned = Runner.GetPlayerObject(player);
            if (spawned != null)
            {
                // TaskWorker.OnDisable 이 붙어 있던 자리를 스스로 반납한다.
                Runner.Despawn(spawned);
            }
        }

        /// <summary>
        /// 이 사람이 설 자리.
        ///
        /// 자리를 <c>PlayerId</c> 로 나눈다. 들어온 순서가 아니라 번호를 쓰므로
        /// 누가 나갔다 들어와도 같은 자리에 선다.
        /// </summary>
        private void ResolveSpawn(PlayerRef player, out Vector3 position, out Quaternion rotation)
        {
            Transform[] points = ResolveSpawnPoints();

            if (points.Length > 0)
            {
                int index = Mathf.Abs(player.PlayerId) % points.Length;
                Transform point = points[index];

                if (point != null)
                {
                    position = point.position;
                    rotation = point.rotation;
                    return;
                }
            }

            Debug.LogWarning(
                "[ShipCoopSpawner] 갑판 위 스폰 자리를 찾지 못했습니다. " +
                "게임 씬의 SpawnPoints 에 ShipCoopSpawnPoint 가 붙어 있는지 확인해 주세요.", this);

            position = transform.position + Vector3.right * (player.PlayerId * fallbackSpacing);
            rotation = transform.rotation;
        }

        /// <summary>
        /// 게임 씬에서 스폰 자리를 찾아 이름순으로 줄 세운다.
        ///
        /// 이름순으로 정렬한다. <c>FindObjectsByType</c> 의 순서는 보장되지 않아서,
        /// 정렬하지 않으면 실행할 때마다 같은 <c>PlayerId</c> 가 다른 자리에 설 수 있다.
        /// </summary>
        private Transform[] ResolveSpawnPoints()
        {
            if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[0] != null)
            {
                return spawnPoints;
            }

            spawnPoints = FindObjectsByType<ShipCoopSpawnPoint>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(point => point.name, StringComparer.Ordinal)
                .Select(point => point.transform)
                .ToArray();

            if (spawnPoints.Length > 0)
            {
                Debug.Log(
                    $"[ShipCoopSpawner] 스폰 자리 {spawnPoints.Length}곳을 찾았습니다 — " +
                    string.Join(", ", spawnPoints.Select(point => point.name)));
            }

            return spawnPoints;
        }
    }
}
