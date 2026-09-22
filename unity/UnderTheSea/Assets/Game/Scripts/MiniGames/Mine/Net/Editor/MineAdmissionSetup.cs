using MiniGames.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mine.Net.Editor
{
    /// <summary>
    /// 광산 시작 씬의 <c>MineLauncher</c> 에 <b>정원 설정</b>을 꽂는다.
    ///
    /// 서버가 사람을 받을지 말지는 <c>MiniGameAdmission</c> 이 정하고, 그 부품은 정원을
    /// <c>MiniGameConfig</c> 에서 읽는다. 이 칸이 비어 있으면 상태(이미 시작함·끝남)만 보고
    /// <b>정원은 못 막는다.</b> 인원 규칙을 코드에 적지 않고 설정 에셋 하나가 주인이 되도록
    /// 하는 것이 MINE.md 13장의 방침이다.
    /// </summary>
    public static class MineAdmissionSetup
    {
        private const string MiningConfigPath =
            "Assets/Game/ScriptableObjects/MiniGames/Common/MiniGame_Mining.asset";

        [MenuItem("Tools/아라아띠/광산 런처에 정원 설정 연결")]
        public static void Wire()
        {
            // ⚠ **씬을 먼저 열고 에셋을 나중에 읽는다. 순서가 바뀌면 조용히 틀린다.**
            //
            //    OpenScene(Single) 은 이전 씬을 내리면서 <b>참조 없는 에셋도 같이 정리한다.</b>
            //    먼저 읽어 둔 MiniGameConfig 가 그때 파괴되는데, Unity 의 == 는 파괴된 객체를
            //    null 과 같다고 본다. 그래서 "지금 꽂힌 값(null) == 꽂을 값(파괴됨)" 이 참이
            //    되어, 아무것도 안 꽂고 "이미 연결되어 있습니다" 를 찍는다.
            //    실제로 이 도구가 그 거짓 성공을 한 번 냈다.
            Scene scene = EditorSceneManager.OpenScene(MineNet.BootScenePath, OpenSceneMode.Single);

            MiniGameConfig config = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(MiningConfigPath);

            if (config == null)
            {
                Debug.LogError($"[광산 입장] 설정을 찾지 못했습니다 — {MiningConfigPath}");
                return;
            }

            MineLauncher launcher = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                launcher = root.GetComponentInChildren<MineLauncher>(true);
                if (launcher != null) break;
            }

            if (launcher == null)
            {
                Debug.LogError(
                    $"[광산 입장] '{MineNet.BootScenePath}' 에서 MineLauncher 를 찾지 못했습니다.");
                return;
            }

            SerializedObject data = new SerializedObject(launcher);
            SerializedProperty slot = data.FindProperty("config");

            if (slot == null)
            {
                Debug.LogError(
                    "[광산 입장] MineLauncher 에 'config' 칸이 없습니다. " +
                    "필드 이름이 바뀌었는지 확인해 주세요.");
                return;
            }

            Object before = slot.objectReferenceValue;
            Debug.Log($"[광산 입장] 지금 꽂힌 값: {(before == null ? "(비어 있음)" : before.name)} " +
                      $"/ 꽂을 값: {config.name}");

            if (before == config)
            {
                Debug.Log("[광산 입장] 이미 연결되어 있습니다.");
                return;
            }

            slot.objectReferenceValue = config;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(launcher);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ⚠ **넣었다고 믿지 말고 다시 읽어 본다.**
            //
            //    한 번은 같은 종류의 도구가 "연결했습니다" 를 찍었는데 씬에는 config 가
            //    비어 있었다. 그러면 서버가 "설정 에셋이 없어 정원을 확인하지 못합니다" 로
            //    조용히 아무나 받는다. 거짓으로 성공을 보고하는 도구는 없느니만 못하다.
            Scene again = EditorSceneManager.OpenScene(MineNet.BootScenePath, OpenSceneMode.Single);

            // 위와 같은 이유로 여기서도 다시 읽는다. 앞서 들고 있던 참조는 파괴됐다.
            MiniGameConfig expected = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(MiningConfigPath);

            MineLauncher reopened = null;

            foreach (GameObject root in again.GetRootGameObjects())
            {
                reopened = root.GetComponentInChildren<MineLauncher>(true);
                if (reopened != null) break;
            }

            Object wrote = reopened == null
                ? null
                : new SerializedObject(reopened).FindProperty("config")?.objectReferenceValue;

            if (wrote == null || wrote != expected)
            {
                Debug.LogError(
                    "[광산 입장] 저장은 됐다는데 씬에는 안 들어갔습니다. " +
                    "씬이 읽기 전용인지, 다른 곳에서 덮어썼는지 확인해 주세요.");
                return;
            }

            Debug.Log(
                $"[광산 입장] {MineNet.BootScenePath} 의 MineLauncher 에 " +
                $"'{expected.DisplayName}' (정원 {expected.MaxPlayers}명) 을 연결하고 확인했습니다.");
        }
    }
}
