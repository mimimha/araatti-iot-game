using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// 🔁 <b>미니게임 씬의 Fusion 정보를 다시 굽는다.</b>
    ///
    /// Fusion 은 씬에 놓인 <c>NetworkObject</c> 마다 "이 오브젝트가 네트워크로 주고받을 값이
    /// 몇 칸인가" 를 미리 구워 둔다. <c>[Networked]</c> 필드를 더하거나 빼면 그 칸 수가 달라지는데,
    /// 씬이 다시 구워지지 않으면 <b>코드와 씬이 어긋난 채로 빌드된다.</b>
    ///
    /// <code>
    ///   AssertException: meta.WordCount == NetworkObject.GetWordCount(instance)
    /// </code>
    ///
    /// 그렇게 되면 그 오브젝트가 아예 살아나지 않는다. 실제로 크라켄이 안 나오고 개발자 화면도
    /// 뜨지 않았다 — 둘 다 그 씬의 네트워크 오브젝트에 얹혀 있었기 때문이다.
    ///
    /// 보통은 씬을 저장할 때 알아서 구워진다. 그래도 <c>[Networked]</c> 를 건드린 뒤에는
    /// 이것을 한 번 돌리는 편이 확실하다. 배치 빌드는 씬을 저장하지 않고 읽기만 하므로
    /// 사람이 저장한 적이 없으면 낡은 채로 남는다.
    /// </summary>
    public static class FusionRebake
    {
        private static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/WarriorsBoot.unity",
            "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity",
            "Assets/Game/Scenes/Main/MiniGames/ShipCoopBoot.unity",
            "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity",
            "Assets/Game/Scenes/Main/CoreGames/Lobby.unity",
        };

        [MenuItem("Tools/아라아띠/Fusion 씬 정보 다시 굽기")]
        public static void Rebake()
        {
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                if (!scene.IsValid())
                {
                    Debug.LogError($"[Fusion 재굽기] 씬을 열지 못했습니다: {path}");
                    continue;
                }

                NetworkObjectPostprocessor.BakeScene(scene);

                // 저장할 때도 한 번 더 구워진다. 결과를 파일에 남기려면 저장이 필요하다.
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log($"[Fusion 재굽기] 다시 구웠습니다 — {path}");
            }

            int prefabs = RebakePrefabs();

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fusion 재굽기] 씬 {Scenes.Length}개, 프리팹 {prefabs}개를 마쳤습니다.");
        }

        /// <summary>
        /// <b>네트워크 프리팹도 다시 굽는다.</b> 씬만으로는 모자랐다.
        ///
        /// 스폰되는 것들(플레이어 · 몬스터)은 프리팹이다. 그쪽 정보가 낡으면 클라이언트가
        /// 그 오브젝트를 만들 때 터진다. 서버는 자기가 만든 것이라 멀쩡해서, <b>클라이언트에서만</b>
        /// 나는 것이 특징이다.
        ///
        /// <code>
        ///   AssertException: meta.WordCount == NetworkObject.GetWordCount(instance)
        ///     at Fusion.NetworkRunner.UpdateRemotePrefabs
        /// </code>
        ///
        /// 그러면 그 오브젝트의 네트워크 값이 통째로 어긋난다. 몬스터가 투명하게 나오고,
        /// 처치 수가 안 오르고, 한 명인데 판이 시작하는 식이다 — 값이 전부 엉뚱하게 읽힌다.
        ///
        /// 다시 읽어들이면 Fusion 의 후처리기가 알아서 굽는다.
        /// </summary>
        private static int RebakePrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs" });
            int done = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;
                if (prefab.GetComponentInChildren<Fusion.NetworkObject>(true) == null) continue;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                done++;
            }

            Debug.Log($"[Fusion 재굽기] 네트워크 프리팹 {done}개를 다시 읽었습니다.");
            return done;
        }

        public static void RebakeFromCommandLine() => Rebake();
    }
}
