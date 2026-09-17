using MiniGames.Common.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.Editor
{
    /// <summary>
    /// <c>CommonMatchCanvas</c> 에서 <b>결과 판만 떼어</b> 미니게임용 오버레이 프리팹을 만든다.
    ///
    /// <b>왜 손으로 만들지 않는가.</b> 결과 판은 자식이 스무 개가 넘고, 그 참조가
    /// <c>ResultPanelPresenter</c> 에 하나하나 꽂혀 있다. 손으로 옮기면 하나쯤 빠뜨려도
    /// 눈에 안 띄고, 실행해야 빈 칸이 드러난다. <b>복제한 뒤 필요 없는 가지만 잘라내면</b>
    /// 참조가 저절로 따라온다.
    ///
    /// <b>무엇을 잘라내는가.</b>
    /// <code>
    ///   Systems             매칭 제어 · 로스터 · 씬 전환   → 미니게임 안에서는 돌면 안 된다
    ///   MatchPanel          매칭 화면                      → 게임 도중에 뜨면 사고다
    ///   QueueLoadingPanel   대기 화면                      → 같은 이유
    ///   ResultPanel         결과 화면                      → ★ 이것만 남긴다
    /// </code>
    ///
    /// 세 미니게임이 이 프리팹 하나를 각자 씬에 넣어 쓴다. 매칭 UI 는 따라오지 않는다.
    /// </summary>
    public static class MiniGameResultOverlaySetup
    {
        private const string SourcePath = "Assets/Game/Prefabs/MiniGames/Common/CommonMatchCanvas.prefab";
        private const string OutputPath = "Assets/Game/Prefabs/MiniGames/Common/MiniGameResultOverlay.prefab";

        private static readonly string[] Cut = { "Systems", "MatchPanel", "QueueLoadingPanel" };

        [MenuItem("Tools/MiniGames/결과 오버레이 프리팹 만들기")]
        public static void Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);

            if (source == null)
            {
                Debug.LogError($"[결과 오버레이] 원본을 찾지 못했습니다 — {SourcePath}");
                return;
            }

            GameObject made = Object.Instantiate(source);
            made.name = "MiniGameResultOverlay";

            try
            {
                int removed = CutUnwanted(made);
                ResultPanelPresenter panel = made.GetComponentInChildren<ResultPanelPresenter>(true);

                if (panel == null)
                {
                    Debug.LogError("[결과 오버레이] 잘라낸 뒤 결과 판이 남지 않았습니다. 만들지 않습니다.");
                    return;
                }

                MiniGameResultOverlay overlay = made.AddComponent<MiniGameResultOverlay>();
                Wire(overlay, panel, made);

                PrefabUtility.SaveAsPrefabAsset(made, OutputPath);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[결과 오버레이] 만들었습니다 — {OutputPath}\n" +
                    $"  잘라낸 가지 {removed}개 (매칭 UI 와 제어 로직은 따라오지 않습니다)\n" +
                    $"  남은 것: Canvas + ResultPanel + MiniGameResultOverlay");
            }
            finally
            {
                Object.DestroyImmediate(made);
            }
        }

        /// <summary>결과와 상관없는 가지를 잘라낸다.</summary>
        private static int CutUnwanted(GameObject root)
        {
            int removed = 0;

            foreach (string name in Cut)
            {
                Transform found = FindDeep(root.transform, name);

                if (found == null)
                {
                    Debug.LogWarning($"[결과 오버레이] '{name}' 을 찾지 못했습니다. 원본 구조가 바뀐 것 같습니다.");
                    continue;
                }

                Object.DestroyImmediate(found.gameObject);
                removed++;
            }

            // 매칭 UI 를 들고 있던 부품도 함께 보낸다. 가리킬 곳이 사라져 오류만 낸다.
            foreach (MonoBehaviour left in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (left is CommonMatchingUI or MatchingQueueCoordinator)
                {
                    Object.DestroyImmediate(left);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>결과 판과 [다시 하기] 버튼을 오버레이에 꽂는다.</summary>
        private static void Wire(MiniGameResultOverlay overlay, ResultPanelPresenter panel, GameObject root)
        {
            SerializedObject so = new SerializedObject(overlay);
            so.FindProperty("panel").objectReferenceValue = panel;

            Transform retry = FindDeep(root.transform, "RetryButton");

            if (retry != null)
            {
                so.FindProperty("retryButtonRoot").objectReferenceValue = retry.gameObject;
            }
            else
            {
                Debug.LogWarning("[결과 오버레이] RetryButton 을 찾지 못했습니다. 숨기지 못합니다.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 만든 오버레이를 ShipCoop 씬에 놓는다. <b>이미 있으면 놓지 않는다.</b>
        ///
        /// 씬 파일을 손으로 고치지 않는 이유는 프리팹을 만들 때와 같다 — UI 계층은 참조가 많아
        /// 텍스트로 만지면 조용히 어긋난다. Unity 에게 맡기면 프리팹 인스턴스로 제대로 들어간다.
        /// </summary>
        [MenuItem("Tools/MiniGames/ShipCoop 씬에 결과 오버레이 놓기")]
        public static void PlaceInShipCoop()
        {
            const string scenePath = "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity";

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath);

            if (prefab == null)
            {
                Debug.LogError($"[결과 오버레이] 프리팹이 없습니다. 먼저 만들어 주세요 — {OutputPath}");
                return;
            }

            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<MiniGameResultOverlay>(true) != null)
                {
                    Debug.Log("[결과 오버레이] ShipCoop 씬에 이미 있습니다. 놓지 않습니다.");
                    return;
                }
            }

            GameObject placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.name = "MiniGameResultOverlay";

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            Debug.Log($"[결과 오버레이] ShipCoop 씬에 놓았습니다. ({scenePath})");
        }

        private static Transform FindDeep(Transform where, string name)
        {
            foreach (Transform child in where.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
