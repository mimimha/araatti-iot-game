using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 접속한 사람마다 캐릭터를 하나 만든다. **서버만 만든다.**
    ///
    /// ⚠ <b>이 부품은 반드시 <c>NetworkRunner</c> 와 같은 오브젝트에 있어야 한다.</b>
    ///    Fusion 은 러너 자신의 오브젝트에 붙은 <c>SimulationBehaviour</c> 만 자동으로
    ///    등록한다. 다른 곳에 두면 <c>PlayerJoined</c> 가 <b>조용히 한 번도 안 불린다.</b>
    ///    ShipCoop 에서 실측한 함정이다.
    ///
    /// <b>자리는 접속 순서로 준다.</b> 실제 번호(P1~P4)는 <c>MineMatchState</c> 가
    /// <c>JoinTick</c> 순서로 다시 나눈다. 여기서는 캐릭터를 만들고 세우기만 한다.
    /// 시작한 뒤에 들어온 사람도 캐릭터는 받는다 — 관전하려면 카메라가 붙을 몸이 필요하다.
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class MinePlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("스폰")]
        [Tooltip("네트워크용 광산 플레이어. MineSceneSetup 이 만든다.")]
        [SerializeField] private NetworkObject playerPrefab;

        [Tooltip("자리를 못 찾았을 때 세울 곳.")]
        [SerializeField] private Vector3 fallbackSpawn = new Vector3(0f, 1f, 0f);

        private readonly Dictionary<PlayerRef, NetworkObject> _spawned = new Dictionary<PlayerRef, NetworkObject>();
        private Transform[] _spots;

        public void PlayerJoined(PlayerRef player)
        {
            Debug.Log($"[MineSpawner] 플레이어 입장 {player} " +
                      $"(나 {Runner.LocalPlayer}, 서버 {Runner.IsServer}, 현재 인원 {Runner.ActivePlayers.Count()})");

            if (!Runner.IsServer) return;

            if (playerPrefab == null)
            {
                Debug.LogError("[MineSpawner] 플레이어 프리팹이 없습니다. MineSceneSetup 을 다시 돌려 주세요.", this);
                return;
            }

            if (_spawned.ContainsKey(player)) return;

            int order = _spawned.Count;
            Transform spot = ResolveSpot(order);

            Vector3 position = spot != null ? spot.position : fallbackSpawn;
            Quaternion rotation = spot != null ? spot.rotation : Quaternion.identity;

            NetworkObject body = Runner.Spawn(playerPrefab, position, rotation, player);

            if (body == null)
            {
                Debug.LogError($"[MineSpawner] {player} 의 캐릭터를 만들지 못했습니다.", this);
                return;
            }

            _spawned[player] = body;

            Debug.Log($"[MineSpawner] {player} 스폰 완료 - {order + 1}번째 접속, {position}, Id {body.Id}");
        }

        public void PlayerLeft(PlayerRef player)
        {
            Debug.Log($"[MineSpawner] 플레이어 퇴장 {player} " +
                      $"(서버 {Runner.IsServer}, 남은 인원 {Runner.ActivePlayers.Count()})");

            if (!Runner.IsServer) return;
            if (!_spawned.TryGetValue(player, out NetworkObject body)) return;

            _spawned.Remove(player);

            // ⚠ 반드시 Despawn 이다. Destroy 로 지우면 이 컴퓨터에서만 사라지고
            //    다른 화면에는 주인 없는 몸이 남는다.
            if (body != null && body.IsValid) Runner.Despawn(body);
        }

        /// <summary>
        /// 몇 번째 자리에 세울 것인가.
        ///
        /// 자리는 게임 씬에 있고 이 부품은 시작 씬에 있어 인스펙터로 이을 수 없다.
        /// 이름순으로 줄을 세워 어느 컴퓨터에서나 같은 순서가 되게 한다.
        /// </summary>
        private Transform ResolveSpot(int order)
        {
            if (_spots == null || _spots.Length == 0 || _spots.Any(one => one == null))
            {
                _spots = FindObjectsByType<MineSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(one => one.transform)
                    .OrderBy(one => one.name, System.StringComparer.Ordinal)
                    .ToArray();

                if (_spots.Length == 0)
                {
                    Debug.LogWarning("[MineSpawner] 자리 표식(MineSpawnPoint)을 찾지 못했습니다. 기본 자리에 세웁니다.");
                    return null;
                }

                Debug.Log($"[MineSpawner] 플레이어 자리 {_spots.Length}곳을 찾았습니다 - " +
                          string.Join(", ", _spots.Select(one => one.name)));
            }

            return _spots[order % _spots.Length];
        }
    }
}
