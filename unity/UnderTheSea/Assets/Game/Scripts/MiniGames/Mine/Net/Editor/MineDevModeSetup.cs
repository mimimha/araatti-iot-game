using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mine.Net.Editor
{
    /// <summary>
    /// 🛠 <b><c>MineNet</c> 씬에 개발자 모드를 놓는다.</b> 검 게임의 <c>WarriorsDevModeSetup</c> 과 같다.
    ///
    /// 손으로 놓지 않고 메뉴로 만든다. 무엇을 어디에 놓았는지 코드로 남는다.
    /// </summary>
    public static class MineDevModeSetup
    {
        private const string HostName = "MineDevMode";

        [MenuItem("Tools/아라아띠/Mine 씬에 개발자 모드 놓기")]
        public static void Place()
        {
            Scene scene = EditorSceneManager.OpenScene(MineNet.ScenePath, OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != HostName) continue;

                Debug.Log($"[Mine 개발자] 씬에 이미 있습니다. 놓지 않습니다. ({MineNet.ScenePath})");
                return;
            }

            GameObject host = new GameObject(HostName);
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<MineDevMode>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Mine 개발자] 씬에 놓았습니다. P 로 켜고 끕니다. ({MineNet.ScenePath})");
        }
    }
}
