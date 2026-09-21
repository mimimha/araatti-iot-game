using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.MiniGames.ShipCoop.EditorTools
{
    /// <summary>
    /// 배 협동 한 벌을 손보는 일회성 도구 모음.
    ///
    /// 메뉴와 커맨드라인 양쪽에서 부를 수 있다. 여러 번 불러도 결과가 같다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod UnderTheSea.MiniGames.ShipCoop.EditorTools.ShipCoopNameplateInstaller.Install
    /// </summary>
    public static class ShipCoopNameplateInstaller
    {
        private const string PrefabPath = "Assets/Game/Prefabs/Characters/ShipCoopPlayer.prefab";

        private static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity",
            "Assets/Game/Scenes/Develop/MinHwa/ShipCoopTest.unity",
        };

        /// <summary>
        /// 이름표를 **떼었다 다시 붙인다.** 저장된 값을 지금 코드 기본값으로 되돌린다.
        ///
        /// ⚠ <b><c>[SerializeField]</c> 는 코드 기본값을 바꿔도 이미 저장된 프리팹 값이
        ///    이긴다.</b> 처음 붙일 때의 <c>height 3.1</c> 이 그대로 남아 머리 위 3.1m 에
        ///    떠 있었고, 그 뒤에 새로 만든 칸(<c>edgeScale</c> 등)만 코드 기본값을 받아
        ///    **어떤 값은 먹고 어떤 값은 안 먹는** 상태였다. 다시 붙이면 한 벌로 맞는다.
        /// </summary>
        [MenuItem("아라아띠/배 협동/머리 위 이름표 값 되돌리기")]
        public static void Reinstall()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            if (root == null)
            {
                Debug.LogError($"[Nameplate] {PrefabPath} 를 열지 못했습니다.");
                return;
            }

            try
            {
                var old = root.GetComponent<ShipCoopNameplate>();

                if (old != null)
                {
                    Object.DestroyImmediate(old, allowDestroyingAssets: false);
                }

                root.AddComponent<ShipCoopNameplate>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Nameplate] 떼었다 다시 붙였습니다. 값이 코드 기본값으로 맞춰졌습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("아라아띠/배 협동/머리 위 이름표 붙이기")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            if (root == null)
            {
                Debug.LogError($"[Nameplate] {PrefabPath} 를 열지 못했습니다.");
                return;
            }

            try
            {
                if (root.GetComponent<ShipCoopNameplate>() != null)
                {
                    Debug.Log("[Nameplate] 이미 붙어 있습니다. 아무것도 하지 않습니다.");
                    return;
                }

                root.AddComponent<ShipCoopNameplate>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Nameplate] ShipCoopPlayer 에 붙였습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 쓰는 데가 없어진 <c>ShipCoopPortrait</c> 를 씬에서 뗀다.
        ///
        /// HUD 팀원 4칸을 빼면서 그 사진을 받아 가던 곳이 사라졌다. 그대로 두면
        /// 아무도 안 보는 얼굴을 매번 RenderTexture 에 찍는다.
        /// </summary>
        [MenuItem("아라아띠/배 협동/안 쓰는 초상화 촬영기 떼기")]
        public static void RemovePortrait()
        {
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                if (!scene.IsValid())
                {
                    Debug.LogError($"[Portrait] {path} 를 열지 못했습니다.");
                    continue;
                }

                int removed = 0;

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    foreach (var shot in go.GetComponentsInChildren<ShipCoopPortrait>(true))
                    {
                        Object.DestroyImmediate(shot, allowDestroyingAssets: false);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                Debug.Log($"[Portrait] {scene.name} — {removed}개 뗐습니다.");
            }
        }
    }
}
