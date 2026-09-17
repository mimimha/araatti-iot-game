#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// **1인용 씬이 아직 열리는지 확인한다.** 배치로 씬을 열고 오류를 세기만 한다.
    ///
    /// 네트워크 작업이 <c>WarriorsTest.unity</c>(혼자 하는 씬)를 망가뜨리지 않았는지
    /// 사람이 에디터를 켜지 않고도 확인할 수 있어야 해서 만들었다.
    /// 게임을 실행하지는 않는다 — 씬을 여는 것만으로 참조 깨짐이 드러난다.
    /// </summary>
    public static class WarriorsSceneCheck
    {
        private static int errors;

        public static void CheckSoloScene()
        {
            Application.logMessageReceived += Count;

            string path = "Assets/Game/Scenes/Develop/SeoYeon/WarriorsTest.unity";

            Debug.Log($"[씬점검] '{path}' 를 엽니다.");
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            int missing = 0;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) missing++;
                }
            }

            Application.logMessageReceived -= Count;

            Debug.Log($"[씬점검] 끝. 스크립트 깨짐 {missing}개 · 오류 로그 {errors}개");
            EditorApplication.Exit(missing == 0 && errors == 0 ? 0 : 1);
        }

        private static void Count(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) errors++;
        }
    }
}
#endif
