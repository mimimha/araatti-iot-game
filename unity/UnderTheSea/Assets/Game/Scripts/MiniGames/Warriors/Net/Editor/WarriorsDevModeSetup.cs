using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🛠 <b><c>WarriorsNet</c> 씬에 개발자 모드를 놓는다.</b>
    ///
    /// 손으로 놓지 않고 메뉴로 만든다. 무엇을 어디에 놓았는지 코드로 남는다.
    ///
    /// ⚠ 부품 자체가 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c> 로 감싸여 있어 출시 빌드에는
    ///    들어가지 않는다. 씬에 얹혀 있어도 그때는 사라진 부품 참조로 남을 뿐 도는 것이 없다.
    /// </summary>
    public static class WarriorsDevModeSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity";
        private const string HostName = "WarriorsDevMode";

        [MenuItem("Tools/아라아띠/Warriors 씬에 개발자 모드 놓기")]
        public static void Place()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != HostName) continue;

                Debug.Log($"[Warriors 개발자] 씬에 이미 있습니다. 놓지 않습니다. ({ScenePath})");
                return;
            }

            GameObject host = new GameObject(HostName);
            SceneManager.MoveGameObjectToScene(host, scene);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            host.AddComponent<WarriorsDevMode>();
#endif

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Warriors 개발자] 씬에 놓았습니다. ` 로 켜고 끕니다. ({ScenePath})");
        }

        public static void PlaceFromCommandLine() => Place();
    }
}
