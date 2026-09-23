using Fusion;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 도감 버튼과 도감 창을 로비에 띄우는 설치기.
    ///
    /// <see cref="IslandRecoveryInstaller"/> 와 같은 이유 · 같은 모양이다. Fusion 이 씬을 인수하고
    /// 큰 <c>Lobby.unity</c> 를 건드리지 않으려고, <see cref="Object.DontDestroyOnLoad"/> 로 올려 두고
    /// <b>로비를 벗어나면 숨긴다.</b>
    ///
    /// <code>
    ///   Assets/Game/Resources/LobbyCollection.prefab   (LobbyCollectionView 가 붙어 있다)
    /// </code>
    /// </summary>
    public static class LobbyCollectionInstaller
    {
        /// <summary>없으면 조용히 아무것도 하지 않는다. 도감이 없다고 게임이 멈추면 안 된다.</summary>
        private const string PrefabPath = "LobbyCollection";

        private static GameObject root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // 화면이 없는 프로세스(Dedicated Server 등)에는 UI 를 만들지 않는다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }

            LocalPlayer.Registered -= CreateWhenNeeded;
            LocalPlayer.Registered += CreateWhenNeeded;

            SceneManager.sceneLoaded += (_, __) => ShowOnlyInLobby();
            SceneManager.sceneUnloaded += _ => ShowOnlyInLobby();
        }

        /// <summary>
        /// ⚠ <b>여기서 예외가 새어 나가면 안 된다.</b> <c>LocalPlayerView.Spawned</c> 한가운데서
        ///    불리고 그 뒤에 카메라를 붙인다. (<see cref="IslandRecoveryInstaller"/> 와 같은 이유)
        /// </summary>
        private static void CreateWhenNeeded(NetworkObject player)
        {
            try
            {
                CreateNow();
                ShowOnlyInLobby();
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    "[LobbyCollectionInstaller] 도감을 띄우지 못했습니다. 게임은 그대로 진행합니다.\n" + e);
            }
        }

        private static void CreateNow()
        {
            if (root != null || !InLobby())
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(PrefabPath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[LobbyCollectionInstaller] Resources/{PrefabPath} 를 찾지 못해 도감을 띄우지 않습니다.");
                return;
            }

            root = Object.Instantiate(prefab);
            root.name = prefab.name;
            Object.DontDestroyOnLoad(root);
        }

        private static void ShowOnlyInLobby()
        {
            if (root == null)
            {
                return;
            }

            bool lobby = InLobby();

            if (root.activeSelf != lobby)
            {
                root.SetActive(lobby);
            }
        }

        /// <summary>
        /// 로비인가.
        ///
        /// ⚠ <see cref="IslandRecoveryInstaller"/> · <c>LobbyChatInstaller</c> 의 것과 같은 내용이다.
        ///    그쪽들이 <c>private</c> 이라 그대로 옮겼다. 한쪽을 고치면 나머지도 같이 고쳐야 한다.
        /// </summary>
        private static bool InLobby()
        {
            Scene lobby = SceneManager.GetSceneByName(SceneFlow.Lobby);

            if (lobby.IsValid() && lobby.isLoaded)
            {
                return true;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (go.name == SceneFlow.Lobby || go.name == "[" + SceneFlow.Lobby + "]")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
