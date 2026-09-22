using UnderTheSea.Network;
using UnityEditor;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🪪 <b>무쌍 플레이어에게 이름표를 붙인다.</b>
    ///
    /// <b>왜 필요한가.</b> 프로필 칸에 실제 사용자 이름이 떠야 하는데, 지금은 "1P" 밖에
    /// 쓸 것이 없었다. 닉네임은 <see cref="NetworkPlayerIdentity"/> 가 들고 있는데 그것이
    /// <b>로비 아바타(NetworkPlayer.prefab)에만</b> 붙어 있어서, 미니게임에 들어오는 순간
    /// 이름이 사라졌다.
    ///
    /// <code>
    ///   들어온 사람 → NetworkPlayerIdentity.Spawned()
    ///               → (입력 권한자만) AccountServiceLocator.Characters.CurrentCharacter.nickname
    ///               → Rpc_SubmitNickname → 서버가 검사하고 [Networked] 에 쓴다
    ///               → 모두가 DisplayName 으로 읽는다
    /// </code>
    ///
    /// 배 협동도 같은 구멍이 있다(ShipCoopPlayer.prefab 에 없어 "선원 1" 로 나온다).
    /// 여기서는 무쌍 것만 고친다 — 남의 담당 프리팹은 건드리지 않는다.
    ///
    /// ⚠ 서버에는 계정이 없다. 이름을 서버가 만들어 내지 않고 <b>주인 클라이언트가 보낸 것만</b>
    ///    받아 적는다. 그래서 데디케이티드 서버에서도 그대로 동작한다.
    /// </summary>
    public static class WarriorsPlayerIdentity
    {
        private static readonly string[] PlayerPrefabs =
        {
            "Assets/Game/Prefabs/MiniGames/Warriors/Player/WarriorsNetPlayer.prefab",
        };

        [MenuItem("Tools/아라아띠/Warriors 플레이어에 이름표 붙이기")]
        public static void Wire()
        {
            foreach (string path in PlayerPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);

                if (root == null)
                {
                    Debug.LogError($"[이름표] 프리팹을 열지 못했습니다 — {path}");
                    continue;
                }

                try
                {
                    if (root.GetComponent<NetworkPlayerIdentity>() != null)
                    {
                        Debug.Log($"[이름표] 이미 붙어 있습니다 — {root.name}");
                        continue;
                    }

                    root.AddComponent<NetworkPlayerIdentity>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);

                    Debug.Log($"[이름표] 붙였습니다 — {root.name}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // ⚠ 저장을 믿지 않고 디스크에서 다시 읽는다. 같은 배치 안에서는 캐시가 옛 값을 준다.
            AssetDatabase.SaveAssets();

            foreach (string path in PlayerPrefabs)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (saved == null || saved.GetComponent<NetworkPlayerIdentity>() == null)
                {
                    Debug.LogError($"[이름표] 저장되지 않았습니다 — {path}");
                    return;
                }
            }

            Debug.Log("[이름표] ✅ 무쌍 플레이어가 닉네임을 들고 다닙니다.");
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }
    }
}
