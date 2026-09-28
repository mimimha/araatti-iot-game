using MiniGames.Common;
using MiniGames.Common.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🐚 무쌍 결과 화면에 **바다의 심장 조각**을 붙인다.
    ///
    /// 클리어하면 <c>MiniGameResultOverlay</c> 가 조각을 적립하고 결과 판에 "획득!" 을 띄운다.
    ///
    /// <b>왜 도구가 필요한가.</b> 오버레이는 <b>프리팹</b>이고 세 미니게임 씬에 모두 들어가 있다.
    /// 프리팹 자산에 설정을 꽂으면 배 협동 · 광산까지 한꺼번에 켜진다 — 그 둘은 아직 지급 단계가
    /// 아니다. 그래서 <b>무쌍 씬 인스턴스에만</b> 오버라이드로 꽂는다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod Warriors.Net.Editor.WarriorsRewardInstaller.Install
    /// </summary>
    public static class WarriorsRewardInstaller
    {
        private const string ScenePath = "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity";
        private const string ConfigPath = "Assets/Game/ScriptableObjects/MiniGames/Common/MiniGame_Sword.asset";
        private const string Field = "rewardConfig";

        [MenuItem("Tools/아라아띠/Warriors 보상 조각 연결")]
        public static void Install()
        {
            // ⚠ 설정은 **씬을 연 뒤에** 불러온다. Single 모드로 씬을 열면 쓰이지 않는 에셋이
            //    언로드되어, 먼저 잡아 둔 참조가 MissingReferenceException 으로 죽는다(실측).
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var config = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(ConfigPath);

            if (config == null)
            {
                Debug.LogError($"[Warriors 보상] {ConfigPath} 를 찾지 못했습니다.");
                return;
            }

            MiniGameResultOverlay overlay = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                overlay = root.GetComponentInChildren<MiniGameResultOverlay>(true);
                if (overlay != null) break;
            }

            if (overlay == null)
            {
                Debug.LogError($"[Warriors 보상] {scene.name} 에 MiniGameResultOverlay 가 없습니다.");
                return;
            }

            var so = new SerializedObject(overlay);
            SerializedProperty slot = so.FindProperty(Field);

            if (slot == null)
            {
                Debug.LogError($"[Warriors 보상] MiniGameResultOverlay 에 {Field} 칸이 없습니다. 이름이 바뀌었나요?");
                return;
            }

            if (slot.objectReferenceValue == config)
            {
                Debug.Log($"[Warriors 보상] 이미 '{config.name}' 이 꽂혀 있습니다. ({config.RewardName} · 조각 {config.FragmentId})");
                return;
            }

            slot.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Warriors 보상] {scene.name} 의 결과 오버레이에 '{config.name}' 을 꽂았습니다 — " +
                $"클리어하면 {config.RewardName}({config.FragmentId})을 줍니다. " +
                "프리팹이 아니라 이 씬에만 걸리므로 배 협동 · 광산은 그대로입니다.");
        }
    }
}
