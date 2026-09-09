using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace FishingMiniGame.Tests
{
    internal static class FishingPlayModeSceneLoader
    {
        private const string StandaloneSceneName = "FishingStandalone";
        private const string StandaloneScenePath = "Assets/Game/Scenes/Develop/Yongju/FishingScenes/FishingStandalone.unity";
        private const string MiniGameSceneName = "FishingMiniGame";
        private const string MiniGameScenePath = "Assets/Game/Scenes/Develop/Yongju/FishingScenes/FishingMiniGame.unity";

        public static AsyncOperation LoadStandalone(LoadSceneMode mode)
        {
            return Load(StandaloneScenePath, StandaloneSceneName, mode);
        }

        public static AsyncOperation LoadMiniGame(LoadSceneMode mode)
        {
            return Load(MiniGameScenePath, MiniGameSceneName, mode);
        }

        private static AsyncOperation Load(string assetPath, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            return EditorSceneManager.LoadSceneAsyncInPlayMode(assetPath, new LoadSceneParameters(mode));
#else
            return SceneManager.LoadSceneAsync(sceneName, mode);
#endif
        }
    }
}
