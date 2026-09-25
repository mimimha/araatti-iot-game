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
    /// ⚠ 부품은 Release 빌드에도 들어간다. 도는지는 실행할 때 <c>-devmode</c> 로 정한다
    ///    (<c>UnderTheSea.Core.DevMode</c>). 전에는 <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> 로
    ///    감싸 Release 에서 빠졌는데, 시연을 Release 로 하므로 풀었다.
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

            host.AddComponent<WarriorsDevMode>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Warriors 개발자] 씬에 놓았습니다. -devmode 로 실행한 뒤 " +
                      $"{UnderTheSea.Core.DevMode.PanelKey} 로 켜고 끕니다. ({ScenePath})");
        }

        public static void PlaceFromCommandLine() => Place();
    }
}
