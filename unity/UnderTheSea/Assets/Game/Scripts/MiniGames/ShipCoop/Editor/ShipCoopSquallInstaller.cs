using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.MiniGames.ShipCoop.EditorTools
{
    /// <summary>
    /// 돌풍 사건의 씬 저장값을 **"접어서 버틴다"** 규칙에 맞춘다.
    ///
    /// 돌풍을 "당겨서 버틴다" 에서 "접어서 버틴다" 로 뒤집으면서(<c>Squall.cs</c>)
    /// 벌칙을 속도식으로 옮겼다. 펴 두면 배가 뒤로 가는 것이 곧 대가라 **실패 판정이 없다.**
    /// 그래서 실패 시 HP · 돛 풀림은 둘 다 0 이다. 그래도 Fail() 은 지금 안 불리지만,
    /// 씬에 남은 옛 값(HP 0 · 돛 풀림 0.4)이 헷갈리게 두지 않으려고 같이 맞춘다.
    ///
    /// 이 두 값은 VoyageEvent 의 [SerializeField] 라 씬에 저장돼 있다. 코드 기본값을
    /// 바꿔도 씬 값이 이긴다. 그래서 씬을 직접 고친다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod UnderTheSea.MiniGames.ShipCoop.EditorTools.ShipCoopSquallInstaller.Apply
    /// </summary>
    public static class ShipCoopSquallInstaller
    {
        private static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity",
            "Assets/Game/Scenes/Develop/MinHwa/ShipCoopTest.unity",
        };

        private const float DamageOnFail = 0f;
        private const float SailLossOnFail = 0f;

        [MenuItem("아라아띠/배 협동/돌풍 벌칙을 접기 규칙에 맞추기")]
        public static void Apply()
        {
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int touched = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Squall squall in root.GetComponentsInChildren<Squall>(true))
                    {
                        var so = new SerializedObject(squall);
                        SerializedProperty damage = so.FindProperty("damageOnFail");
                        SerializedProperty sailLoss = so.FindProperty("sailLossOnFail");

                        if (damage == null || sailLoss == null)
                        {
                            Debug.LogError($"[Squall] {squall.name} — 필드를 못 찾았습니다. 이름이 바뀌었나요?", squall);
                            continue;
                        }

                        bool changed = !Mathf.Approximately(damage.floatValue, DamageOnFail)
                                    || !Mathf.Approximately(sailLoss.floatValue, SailLossOnFail);

                        Debug.Log(
                            $"[Squall] {scene.name}/{squall.name} — damageOnFail {damage.floatValue} → {DamageOnFail}, " +
                            $"sailLossOnFail {sailLoss.floatValue} → {SailLossOnFail}{(changed ? "" : " (이미 같음)")}");

                        if (!changed)
                        {
                            continue;
                        }

                        damage.floatValue = DamageOnFail;
                        sailLoss.floatValue = SailLossOnFail;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        touched++;
                    }
                }

                if (touched > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
    }
}
