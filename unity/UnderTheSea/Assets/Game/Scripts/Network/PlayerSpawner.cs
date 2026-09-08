using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private NetworkPrefabRef playerPrefab;

    public void PlayerJoined(PlayerRef player)
    {
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
    }
}