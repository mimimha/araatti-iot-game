using System.Linq;
using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private NetworkPrefabRef playerPrefab;

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

        Vector3 spawnPosition = new Vector3(player.PlayerId * 3f, 1f, 0f);

        NetworkObject playerObject = Runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            inputAuthority: player
        );

        Runner.SetPlayerObject(player, playerObject);

        Debug.Log($"[PlayerSpawner] {player} 스폰 완료 — {spawnPosition}, Id {playerObject.Id}");
    }
}