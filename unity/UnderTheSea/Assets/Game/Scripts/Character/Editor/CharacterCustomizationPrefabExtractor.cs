#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Character.Editor
{
    [InitializeOnLoad]
    internal static class CharacterCustomizationPrefabExtractor
    {
        private const string SourceScenePath = "Assets/Game/Scenes/Develop/SeoYeon/CharacterCustomizationTest.unity";
        private const string PrefabFolder = "Assets/Game/Prefabs/Characters";
        private const string PrefabPath = PrefabFolder + "/CharacterCustomization.prefab";

        static CharacterCustomizationPrefabExtractor()
        {
            EditorApplication.delayCall += ExtractIfMissing;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += ExtractIfMissing;
            };
        }

        [MenuItem("Under The Sea/Character Customization/Rebuild Prefab")]
        private static void RebuildFromMenu()
        {
            if (!File.Exists(PrefabPath)) Extract(false);
            EnsureNicknameInPrefab();
            EnsureTestSceneUsesPrefab();
        }

        private static void ExtractIfMissing()
        {
            // ⚠ 가상 플레이어(MPPM 클론)에서는 돌지 않는다.
            //    클론은 Assets 를 심링크로 공유하는 별도 프로젝트이고 AssetDatabase 가
            //    읽기 전용이라, 프리팹을 저장하려다 실패하면서
            //    "Asset Database is set to Read Only" 가 쏟아지고 Fusion 설정까지 못 읽게 된다.
            //    프리팹은 이미 저장소에 있으므로 클론은 읽기만 하면 된다.
            //
            //    ⚠ 메뉴(Rebuild Prefab)는 막지 않는다. 사람이 일부러 누른 것이다.
            if (UnderTheSea.Editor.VirtualPlayer.IsClone) return;

            if (!File.Exists(PrefabPath)) Extract(false);
            else EnsureNicknameInPrefab();
            EnsureTestSceneUsesPrefab();
        }

        /// <summary>
        /// 프리팹이 갖춰야 할 UI 를 보장한다. **실제로 바뀐 것이 있을 때만 저장한다.**
        ///
        /// ⚠ <b>예전에는 조건 없이 저장했다.</b> 이 함수는 에디터를 열 때와 <b>Play 를 멈출
        ///    때마다</b> 불리므로, 내용이 그대로여도 프리팹 파일이 계속 다시 쓰였다.
        ///    그 쓰기가 Multiplayer Play Mode 가상 플레이어에게 에셋 갱신을 일으키고,
        ///    거기 AssetDatabase 는 읽기 전용이라 이렇게 터진다.
        ///
        /// <code>
        ///   Asset Database is set to Read Only, but it has found out-of-date assets.
        ///      → 심하면 클론이 Fusion 설정을 못 읽어 세션 접속까지 실패한다.
        /// </code>
        ///
        /// ⚠ <c>AssetDatabase.SaveAssets()</c> 는 뺐다. <c>SaveAsPrefabAsset</c> 이 이미 이
        ///    프리팹을 디스크에 쓴다. 그 호출은 <b>다른 더러운 에셋까지</b> 함께 쓰기 때문에
        ///    여기서 부를 이유가 없고, 가상 플레이어에게 보낼 갱신만 늘린다.
        /// </summary>
        private static void EnsureNicknameInPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                CharacterCustomizationController controller = root.GetComponentInChildren<CharacterCustomizationController>(true);
                if (controller == null) return;

                if (!controller.EnsureReusableUiLayout()) return;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Nickname input and validation guide added to character customization prefab: " + PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureTestSceneUsesPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(PrefabPath)) return;

            Scene scene = SceneManager.GetSceneByPath(SourceScenePath);
            bool openedForMigration = !scene.IsValid() || !scene.isLoaded;
            if (!openedForMigration && scene.isDirty)
            {
                Debug.LogWarning("Character customization test scene still needs prefab migration. Save the scene, then use Under The Sea/Character Customization/Rebuild Prefab.");
                return;
            }
            if (openedForMigration)
                scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

            GameObject existingPrefabRoot = FindRoot(scene, "CharacterCustomization");
            bool alreadyMigrated = existingPrefabRoot != null
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existingPrefabRoot) == PrefabPath;

            if (!alreadyMigrated)
            {
                GameObject preview = FindRoot(scene, "Character Preview");
                GameObject ui = FindRoot(scene, "Customization UI");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    Debug.LogError("Failed to place the character customization prefab in its test scene.");
                }
                else
                {
                    if (preview != null) Object.DestroyImmediate(preview);
                    if (ui != null) Object.DestroyImmediate(ui);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("Character customization test scene now uses the shared prefab instance.");
                }
            }

            if (openedForMigration)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static void Extract(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!overwrite && File.Exists(PrefabPath)) return;

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene sourceScene = SceneManager.GetSceneByPath(SourceScenePath);
            bool openedForExtraction = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (!openedForExtraction && sourceScene.isDirty)
            {
                Debug.LogWarning("Character customization prefab was not rebuilt because the source scene has unsaved changes. Save it, then use Under The Sea/Character Customization/Rebuild Prefab.");
                return;
            }

            if (openedForExtraction)
                sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

            GameObject preview = FindRoot(sourceScene, "Character Preview");
            GameObject ui = FindRoot(sourceScene, "Customization UI");
            if (preview == null || ui == null)
            {
                if (openedForExtraction) EditorSceneManager.CloseScene(sourceScene, true);
                Debug.LogError("Character customization prefab extraction failed: required scene roots were not found.");
                return;
            }

            GameObject root = new GameObject("CharacterCustomization");
            SceneManager.MoveGameObjectToScene(root, sourceScene);
            preview.transform.SetParent(root.transform, true);
            ui.transform.SetParent(root.transform, true);

            Directory.CreateDirectory(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);

            if (openedForExtraction)
                EditorSceneManager.CloseScene(sourceScene, true);
            else
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);

            AssetDatabase.SaveAssets();
            if (success)
                Debug.Log("Character customization prefab created: " + PrefabPath);
            else
                Debug.LogError("Character customization prefab extraction failed: " + PrefabPath);
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == objectName) return root;
            return null;
        }
    }
}
#endif
