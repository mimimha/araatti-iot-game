using System.Collections.Generic;
using System.Linq;
using FishingMiniGame.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingMiniGame.Editor
{
    public static class FishingVerticalSliceSceneBuilder
    {
        public const string LegacyScenePath = "Assets/FishingMiniGame/Scenes/FishingVerticalSlice.unity";
        public const string StandaloneScenePath = "Assets/FishingMiniGame/Scenes/FishingStandalone.unity";
        public const string MiniGameScenePath = "Assets/FishingMiniGame/Scenes/FishingMiniGame.unity";
        public const string ConfigPath = "Assets/FishingMiniGame/Configs/FishingCheckpointBConfig.asset";

        [MenuItem("Tools/Fishing Mini Game/Build Checkpoint B Scenes")]
        public static void BuildCheckpointB()
        {
            FishingGameConfigAsset config = EnsureConfigAsset();
            BuildScene(MiniGameScenePath, config, false);
            BuildScene(StandaloneScenePath, config, true);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Fishing] Checkpoint B scenes built: {StandaloneScenePath}, {MiniGameScenePath}");
        }

        [MenuItem("Tools/Fishing Mini Game/Build Checkpoint A Scene")]
        public static void BuildLegacyCheckpointA()
        {
            FishingGameConfigAsset config = EnsureConfigAsset();
            BuildScene(LegacyScenePath, config, false, true);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Fishing] Legacy vertical slice rebuilt: {LegacyScenePath}");
        }

        private static void BuildScene(string path, FishingGameConfigAsset config, bool standalone, bool autoStart = false)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject(standalone ? "FishingStandaloneRoot" : "FishingMiniGameRoot");
            FishingGameController controller = root.AddComponent<FishingGameController>();
            controller.Configure(config, autoStart);
            root.AddComponent<FishingMiniGameFacade>();
            if (standalone)
            {
                FishingStandaloneBootstrap bootstrap = root.AddComponent<FishingStandaloneBootstrap>();
                bootstrap.Configure(config);
            }
            root.AddComponent<FishingWorldView>();
            FishingDebugUI userInterface = root.AddComponent<FishingDebugUI>();
            userInterface.Configure(controller, standalone);
            CreateCameraAndLight();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new System.InvalidOperationException($"Failed to save fishing scene at {path}");
            }
            Selection.activeGameObject = root;
        }

        private static void CreateCameraAndLight()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.48f, 0.70f, 0.78f);
            camera.fieldOfView = 46f;
            cameraObject.transform.position = new Vector3(9.2f, 6.6f, -10.2f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.2f, 0.85f, 2.7f) - cameraObject.transform.position);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.91f, 0.76f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static FishingGameConfigAsset EnsureConfigAsset()
        {
            FishingGameConfigAsset config = AssetDatabase.LoadAssetAtPath<FishingGameConfigAsset>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FishingGameConfigAsset>();
                config.EnsureCheckpointBDefaults();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            else
            {
                config.EnsureCheckpointBDefaults();
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            AddSceneIfMissing(scenes, StandaloneScenePath);
            AddSceneIfMissing(scenes, MiniGameScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path)
        {
            if (scenes.Any(item => item.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}
