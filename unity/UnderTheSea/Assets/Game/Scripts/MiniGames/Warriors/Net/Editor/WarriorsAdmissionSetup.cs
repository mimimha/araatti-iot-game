using MiniGames.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🚪 <b><c>WarriorsLauncher</c> 에 정원 설정을 물려 준다.</b>
    ///
    /// 서버가 사람을 받을지 말지는 <c>MiniGameAdmission</c> 이 정하고, 그 부품은 정원을
    /// <see cref="MiniGameConfig"/> 에서 읽는다. 비어 있으면 아무나 받는다.
    ///
    /// 씬을 손으로 고치지 않고 메뉴로 붙인다. 무엇을 어디에 연결했는지 코드로 남는다.
    /// </summary>
    public static class WarriorsAdmissionSetup
    {
        private const string BootScenePath = "Assets/Game/Scenes/Main/MiniGames/WarriorsBoot.unity";
        private const string SwordConfigPath = "Assets/Game/ScriptableObjects/MiniGames/Common/MiniGame_Sword.asset";

        [MenuItem("Tools/아라아띠/Warriors 런처에 정원 설정 연결")]
        public static void Wire()
        {
            MiniGameConfig config = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(SwordConfigPath);

            if (config == null)
            {
                Debug.LogError($"[Warriors 입장] 설정을 찾지 못했습니다 — {SwordConfigPath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            WarriorsLauncher launcher = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                launcher = root.GetComponentInChildren<WarriorsLauncher>(true);
                if (launcher != null) break;
            }

            if (launcher == null)
            {
                Debug.LogError($"[Warriors 입장] '{BootScenePath}' 에서 WarriorsLauncher 를 찾지 못했습니다.");
                return;
            }

            SerializedObject data = new SerializedObject(launcher);
            SerializedProperty slot = data.FindProperty("config");

            if (slot == null)
            {
                Debug.LogError(
                    "[Warriors 입장] WarriorsLauncher 에 'config' 칸이 없습니다. " +
                    "필드 이름이 바뀌었는지 확인해 주세요.");
                return;
            }

            slot.objectReferenceValue = config;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(launcher);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ⚠ **넣었다고 믿지 말고 다시 읽어 본다.**
            //
            //    한 번은 이 도구가 "연결했습니다" 를 찍었는데 씬에는 config 가 비어 있었다.
            //    그러면 서버가 "설정 에셋이 없어 정원을 확인하지 못합니다" 로 조용히 아무나
            //    받는다. 거짓으로 성공을 보고하는 도구는 없느니만 못하다.
            SerializedObject check = new SerializedObject(launcher);
            Object wrote = check.FindProperty("config")?.objectReferenceValue;

            if (wrote != config)
            {
                Debug.LogError(
                    "[Warriors 입장] 연결이 저장되지 않았습니다. 씬을 직접 확인해 주세요. " +
                    $"(넣으려 한 것 {config.name}, 실제 {(wrote == null ? "비어 있음" : wrote.name)})");
                return;
            }

            Debug.Log(
                $"[Warriors 입장] 정원 설정을 연결했습니다 — {config.DisplayName}, " +
                $"정원 {config.MaxPlayers}명.");
        }

        public static void WireFromCommandLine() => Wire();
    }
}
