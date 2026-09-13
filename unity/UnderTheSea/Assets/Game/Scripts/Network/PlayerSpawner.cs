using System.Linq;
using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    /// <summary>
    /// 스폰할 캐릭터 프리팹.
    ///
    /// 예전에는 <c>NetworkPrefabRef</c>(에셋 GUID) 였다. 스크립트로 배선하려면
    /// 고정 버퍼(long 2개)에 GUID 를 직접 써야 하는데 그러려면 unsafe 코드가 필요하고,
    /// 값을 넣어 주는 Fusion 쪽 헬퍼(NetworkObjectGuidDrawer)는 internal 이라 부를 수 없다.
    /// <c>NetworkObject</c> 참조로 두면 Inspector 에서도 스크립트에서도 그냥 끌어다 놓으면 된다.
    /// <c>Runner.Spawn</c> 에 NetworkObject 를 그대로 넘길 수 있어 동작도 같다.
    /// </summary>
    [SerializeField] private NetworkObject playerPrefab;

    [Header("스폰 위치")]
    [Tooltip("비워 두면 이 오브젝트 위치를 기준으로 옆으로 늘어놓는다. " +
             "Lobby 처럼 지형이 있는 씬에서는 반드시 채운다.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("스폰 포인트가 없을 때 플레이어끼리 벌릴 간격(m).")]
    [SerializeField] private float fallbackSpacing = 3f;

    public void PlayerJoined(PlayerRef player)
    {
        // 서버·클라이언트 모두 여기를 지난다. 창이 없는 Dedicated Server 를 로그로만 확인해야 하므로,
        // 누가 들어왔고 지금 몇 명인지를 남긴다. (docs/prd/fusion-dedicated-lobby-roadmap.md PRD 08-1)
        Debug.Log(
            $"[PlayerSpawner] 플레이어 입장 {player} " +
            $"(나 {Runner.LocalPlayer}, 서버 {Runner.IsServer}, 현재 인원 {Runner.ActivePlayers.Count()})");

        // Host만 실제 네트워크 오브젝트를 생성한다.
        if (!Runner.IsServer)
            return;

        ResolveSpawn(player, out Vector3 spawnPosition, out Quaternion spawnRotation);

        NetworkObject playerObject = Runner.Spawn(
            playerPrefab,
            spawnPosition,
            spawnRotation,
            inputAuthority: player
        );

        Runner.SetPlayerObject(player, playerObject);

        Debug.Log($"[PlayerSpawner] {player} 스폰 완료 — {spawnPosition}, Id {playerObject.Id}");
    }

    public void PlayerLeft(PlayerRef player)
    {
        Debug.Log(
            $"[PlayerSpawner] 플레이어 퇴장 {player} " +
            $"(나 {Runner.LocalPlayer}, 서버 {Runner.IsServer}, 남은 인원 {Runner.ActivePlayers.Count()})");

        if (!Runner.IsServer)
            return;

        // 나간 사람의 캐릭터만 지운다. 남은 사람들의 캐릭터는 그대로 둔다.
        NetworkObject playerObject = Runner.GetPlayerObject(player);
        if (playerObject != null)
        {
            Runner.Despawn(playerObject);
        }
    }

    /// <summary>
    /// 스폰 위치를 정한다. 스폰 포인트를 채웠으면 순서대로 돌려쓰고,
    /// 없으면 이 오브젝트 옆으로 늘어놓는다.
    /// </summary>
    private void ResolveSpawn(PlayerRef player, out Vector3 position, out Quaternion rotation)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // PlayerId 는 1 부터 늘어난다. 인원이 스폰 포인트보다 많으면 다시 처음으로 돌아간다.
            int index = Mathf.Abs(player.PlayerId) % spawnPoints.Length;
            Transform point = spawnPoints[index];

            if (point != null)
            {
                position = point.position;
                rotation = point.rotation;
                return;
            }

            Debug.LogWarning($"[PlayerSpawner] 스폰 포인트 {index} 가 비어 있습니다. 기본 위치를 씁니다.");
        }

        // PRD 08-1 의 ServerTestScene 이 쓰던 값 그대로다. 그 씬의 QA 결과가 바뀌지 않게 유지한다.
        position = new Vector3(player.PlayerId * fallbackSpacing, 1f, 0f);
        rotation = Quaternion.identity;
    }
}
