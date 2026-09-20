using MiniGames.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mine.Net.Editor
{
    /// <summary>
    /// 광산 네트워크 씬에 <b>공용 결과 화면</b>을 놓는다.
    ///
    /// 배·검 게임이 쓰는 <c>MiniGameResultOverlay.prefab</c> 을 그대로 넣는다. 성공이든
    /// 실패든 화면은 하나고 문구만 다르다. 보상 칸은 설정을 넘기지 않아 통째로 숨는다 —
    /// 아직 아이템도 인벤토리도 없는 단계라 지급하지도 않은 보상을 보여 주지 않기 위해서다.
    ///
    /// <b>왜 도구로 하는가.</b> 씬 파일을 손으로 고치면 참조 하나를 빠뜨려도 눈에 안 띄고
    /// 실행해야 드러난다. 대신 도구가 <b>썼다고 말하고 실제로는 안 쓴 적</b>이 있어서,
    /// 저장한 뒤 씬을 다시 읽어 확인하고 안 들어갔으면 오류로 알린다.
    /// </summary>
    public static class MineResultOverlaySetup
    {
        private const string PrefabPath =
            "Assets/Game/Prefabs/MiniGames/Common/MiniGameResultOverlay.prefab";

        [MenuItem("Tools/아라아띠/광산 씬에 결과 화면 놓기")]
        public static void Install()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[광산 결과 화면] 프리팹을 찾지 못했습니다 — {PrefabPath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MineNet.ScenePath, OpenSceneMode.Single);

            // 두 가지를 한다. 이미 되어 있는 쪽은 각자 알아서 건너뛴다 —
            // 한쪽만 되어 있는 씬에서 다시 불러도 나머지가 채워져야 하기 때문이다.
            if (Find(scene) == null)
            {
                GameObject placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                placed.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Debug.Log("[광산 결과 화면] 공용 결과 화면을 넣었습니다.");
            }
            else
            {
                Debug.Log("[광산 결과 화면] 공용 결과 화면은 이미 들어 있습니다.");
            }

            HideOldResultCard(scene);

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("[광산 결과 화면] 씬 저장에 실패했습니다.");
                return;
            }

            // ⚠ **저장했다는 말을 믿지 않는다.** 다시 열어서 실제로 들어갔는지 본다.
            Scene again = EditorSceneManager.OpenScene(MineNet.ScenePath, OpenSceneMode.Single);

            if (Find(again) == null)
            {
                Debug.LogError(
                    "[광산 결과 화면] 저장은 됐다는데 씬에 없습니다. " +
                    "씬이 읽기 전용이거나 다른 곳에서 덮어썼는지 확인해 주세요.");
                return;
            }

            Debug.Log($"[광산 결과 화면] {MineNet.ScenePath} 에 넣고 확인했습니다.");
        }

        /// <summary>
        /// 광산 HUD 가 갖고 있던 결과 칸을 감춘다.
        ///
        /// 공용 화면이 붙었는데 그대로 두면 결과가 두 번 뜬다. <c>MineHud.hideOnFinish</c> 가
        /// 이 경우를 위해 이미 있던 값이라 그것만 켠다.
        ///
        /// ⚠ <b>씬에만 켠다.</b> 같은 프리팹을 효진님 개발 씬(<c>Develop/HyoJin/MineTest</c>)도
        ///    쓰는데, 그쪽에는 공용 화면이 없어서 켜면 결과가 아무 데도 안 남는다.
        ///    프리팹이 아니라 이 씬의 인스턴스에만 덮어쓴다.
        /// </summary>
        private static void HideOldResultCard(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MineHud hud = root.GetComponentInChildren<MineHud>(true);
                if (hud == null) continue;

                SerializedObject so = new SerializedObject(hud);
                SerializedProperty flag = so.FindProperty("hideOnFinish");

                if (flag == null)
                {
                    Debug.LogWarning("[광산 결과 화면] MineHud 에 hideOnFinish 가 없습니다. 건너뜁니다.");
                    return;
                }

                if (flag.boolValue)
                {
                    Debug.Log("[광산 결과 화면] 예전 결과 칸은 이미 꺼져 있습니다.");
                    return;
                }

                flag.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log("[광산 결과 화면] 예전 결과 칸을 껐습니다. (hideOnFinish)");
                return;
            }

            Debug.LogWarning("[광산 결과 화면] 씬에서 MineHud 를 찾지 못했습니다.");
        }

        /// <summary>씬에 결과 화면이 이미 있는가.</summary>
        private static MiniGames.Common.UI.MiniGameResultOverlay Find(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<MiniGames.Common.UI.MiniGameResultOverlay>(true);
                if (found != null) return found;
            }

            return null;
        }
    }
}
