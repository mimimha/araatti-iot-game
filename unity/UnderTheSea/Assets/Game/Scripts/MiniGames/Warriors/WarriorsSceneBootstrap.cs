using System.Linq;
using UnityEngine;

namespace Warriors
{
    [DefaultExecutionOrder(-1000)]
    public sealed class WarriorsSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject standalonePlayerPrefab;
        [SerializeField] private bool createStandalonePlayer = true;

        private void Awake()
        {
            // 판을 열기 전에 시드를 먼저 정한다. 스폰·보스 약점·리듬 악보가 모두
            // 이 시드에서 나오므로, 네트워크가 붙으면 서버가 정한 시드를
            // WarriorsRun.BeginRun(seed) 로 넣어 주기만 하면 네 명이 같은 판을 본다.
            WarriorsRun.BeginRun();

            WarriorsLocalPlayerController localPlayer = FindFirstObjectByType<WarriorsLocalPlayerController>(FindObjectsInactive.Include);
            Transform playersRoot = localPlayer != null ? localPlayer.transform.parent : null;

            if (localPlayer == null && createStandalonePlayer && standalonePlayerPrefab != null)
            {
                GameObject root = new("RuntimePlayers");
                playersRoot = root.transform;
                Transform spawn = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(item => item.name == "PlayerSpawn_01");
                Vector3 position = spawn != null ? spawn.position : new Vector3(0f, 0f, -5f);
                Quaternion rotation = spawn != null ? spawn.rotation : Quaternion.identity;
                GameObject instance = Instantiate(standalonePlayerPrefab, position, rotation, playersRoot);
                instance.name = "LocalPlayer";
                localPlayer = instance.GetComponent<WarriorsLocalPlayerController>();
            }

            if (localPlayer == null) return;
            Transform playerTransform = localPlayer.transform;
            WarriorsKeyboardInput keyboardInput = localPlayer.GetComponent<WarriorsKeyboardInput>();
            WarriorsInputRouter inputRouter = FindFirstObjectByType<WarriorsInputRouter>(FindObjectsInactive.Include);
            if (keyboardInput != null)
            {
                inputRouter?.Configure(keyboardInput);
                localPlayer.GetComponent<WarriorsPlayerCombat>()?.BindInputSource(keyboardInput);
            }
            FindFirstObjectByType<WarriorsEnemySpawner>(FindObjectsInactive.Include)?.BindPlayer(playerTransform);
            FindFirstObjectByType<WarriorsThirdPersonCamera>(FindObjectsInactive.Include)?.Configure(playerTransform);
            FindFirstObjectByType<WarriorsGameFlow>(FindObjectsInactive.Include)?.BindPlayersRoot(playersRoot);
        }
    }
}
