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

    [Header("미니게임에서 돌아올 때")]
    [Tooltip("같은 판을 마친 사람들이 포탈 앞 한 점에 겹쳐 서지 않게 옆으로 벌리는 간격(m).")]
    [SerializeField, Min(0f)] private float returnSpacing = 1.2f;

    /// <summary>돌아온 사람을 옆으로 몇 자리까지 벌리나. 미니게임이 최대 4명이다.</summary>
    private const int ReturnSlots = 4;

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
    /// 스폰 위치를 정한다.
    ///
    /// <code>
    ///   미니게임에서 돌아왔다   그 포탈 앞, 포탈을 등지고           (TryReturnSpawn)
    ///   처음 로그인이다         스폰 포인트를 순서대로 돌려쓴다
    ///   스폰 포인트가 없다      이 오브젝트 옆으로 늘어놓는다
    /// </code>
    /// </summary>
    private void ResolveSpawn(PlayerRef player, out Vector3 position, out Quaternion rotation)
    {
        if (TryReturnSpawn(player, out position, out rotation))
        {
            return;
        }

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

    /// <summary>
    /// **미니게임에서 돌아온 사람이면 그 포탈 앞에 세운다.**
    ///
    /// 미니게임에서 돌아오는 것도 로비에 새로 접속하는 것이라, 예전에는 처음 로그인한 사람과 똑같이
    /// 기본 자리에 섰다. 게임을 마친 사람이 섬 반대편에서 다시 걸어와야 했다. 클라이언트가 접속할 때
    /// "어디서 왔는지" 를 쪽지(<see cref="LobbyReturnToken"/>)로 들고 오므로 그것을 읽는다.
    ///
    /// ⚠ <b>쪽지의 좌표를 믿는 것이 아니다.</b> 쪽지에는 씬 이름만 있고, 자리는 서버 씬에 놓인 포탈의
    ///    돌아오는 자리(<c>MiniGamePortal.returnPoint</c>)에서 온다. 모르는 이름이면 기본 자리다.
    ///
    /// ⚠ <b>같이 끝난 사람들을 옆으로 벌린다.</b> 한 판의 네 명이 거의 동시에 돌아오는데, 한 점에
    ///    세우면 캐릭터가 서로 겹친다. PlayerId 로 자리를 나눠 바라보는 방향의 좌우로 늘어놓는다.
    /// </summary>
    private bool TryReturnSpawn(PlayerRef player, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        // 서버에서만 읽힌다. 클라이언트에서는 늘 null 이다.
        string cameFrom = LobbyReturnToken.Read(Runner.GetPlayerConnectionToken(player));
        if (cameFrom == null)
        {
            return false;
        }

        if (!MiniGamePortal.TryFindReturnPoint(cameFrom, out position, out rotation, out string portalName))
        {
            Debug.LogWarning(
                $"[PlayerSpawner] {player} 는 \"{cameFrom}\" 에서 돌아왔지만 그 포탈에 돌아오는 자리가 없습니다. " +
                "기본 자리에 세웁니다. (MiniGamePortal.returnPoint)");
            return false;
        }

        int slot = Mathf.Abs(player.PlayerId) % ReturnSlots;
        float sideways = (slot - (ReturnSlots - 1) * 0.5f) * returnSpacing;
        position += rotation * Vector3.right * sideways;

        Debug.Log($"[PlayerSpawner] {player} 는 \"{cameFrom}\" 에서 돌아왔습니다. '{portalName}' 앞에 세웁니다.");
        return true;
    }
}
