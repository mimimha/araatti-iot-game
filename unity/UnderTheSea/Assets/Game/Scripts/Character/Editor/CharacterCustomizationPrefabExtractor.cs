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
            if (!File.Exists(PrefabPath)) Extract(false);
            else EnsureNicknameInPrefab();
            EnsureTestSceneUsesPrefab();
        }

        private static void EnsureNicknameInPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                CharacterCustomizationController controller = root.GetComponentInChildren<CharacterCustomizationController>(true);
                if (controller == null) return;
                controller.EnsureReusableUiLayout();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
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
