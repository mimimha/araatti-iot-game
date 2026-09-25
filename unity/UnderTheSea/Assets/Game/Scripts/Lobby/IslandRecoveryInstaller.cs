using Fusion;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 섬 회복도 HUD 를 로비에 띄우는 설치기.
    ///
    /// <b>왜 씬에 올려 두지 않는가.</b> <see cref="AltarOfferingInstaller"/> ·
    /// <c>LobbyChatInstaller</c> · <c>LobbyTutorial</c> 과 같은 이유다.
    ///   · Fusion 은 세션을 시작하면서 씬을 러너 전용 씬으로 인수한다. 그때 없어지거나 옮겨진다
    ///   · 22MB 짜리 <c>Lobby.unity</c> 를 건드리지 않아도 된다 (CONVENTION.md 3장)
    ///
    /// 그래서 <see cref="Object.DontDestroyOnLoad"/> 로 올려 두고, <b>로비를 벗어나면 숨긴다.</b>
    /// 지우지 않고 숨기는 것도 채팅과 같다 — 다시 만들 일이 없다.
    ///
    /// <code>
    ///   Assets/Game/Resources/IslandRecoveryHud.prefab
    ///     Panel   PanelBase · RecoveryFill · TitleText · PercentText
    ///             (IslandRecoveryView 가 붙어 있다)
    /// </code>
    ///
    /// 이 설치기가 정하는 것은 <b>언제 보이느냐</b>뿐이고, 숫자와 게이지는
    /// <see cref="IslandRecoveryView"/> 가 맡는다.
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 6.2 · 13.4절 (STEP 9)
    /// </summary>
    public static class IslandRecoveryInstaller
    {
        /// <summary>
        /// 불러올 프리팹. <c>Resources</c> 아래에 있어야 코드가 찾을 수 있다.
        ///
        /// 없으면 조용히 아무것도 하지 않는다. 회복도 HUD 가 없다고 게임이 멈추면 안 된다.
        /// </summary>
        private const string PrefabPath = "IslandRecoveryHud";

        private static GameObject root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // 화면이 없는 프로세스(Dedicated Server 등)에는 UI 를 만들지 않는다.
            // ⚠ 이 한 줄이 없으면 전용 서버 콘솔에 UI 로그와 에러가 쌓인다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }

            // 도메인 리로드를 끈 설정에서도 구독이 쌓이지 않게 한 번 떼고 붙인다.
            LocalPlayer.Registered -= CreateWhenNeeded;
            LocalPlayer.Registered += CreateWhenNeeded;

            // ⚠ 씬이 바뀔 때마다 로비인지 다시 본다. 빼면 미니게임 화면까지 따라간다.
            SceneManager.sceneLoaded += (_, __) => ShowOnlyInLobby();
            SceneManager.sceneUnloaded += _ => ShowOnlyInLobby();
        }

        /// <summary>
        /// 내 캐릭터가 로비에 생긴 순간에 만든다. 채팅·튜토리얼·봉헌 창과 같은 신호를 쓴다.
        ///
        /// ⚠ <b>여기서 예외가 새어 나가면 안 된다.</b> 이 알림은 <c>LocalPlayerView.Spawned</c>
        ///    한가운데서 불리고, 그 <b>뒤에</b> 카메라를 붙이는 코드가 있다. 여기서 터지면
        ///    카메라가 안 붙어 "Lobby에 접속 중..." 에서 멈춘다. 실제로 겪은 사고다.
        ///    (<c>LobbyChatInstaller.CreateWhenNeeded</c> 와 같은 이유, 같은 모양)
        ///
        ///    삼키는 것이 아니라 <b>분명히 남기고</b> 상위 흐름을 깨지 않는 것이다.
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
                    "[IslandRecoveryInstaller] 회복도 HUD 를 띄우지 못했습니다. 게임은 그대로 진행합니다.\n" + e);
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
                    $"[IslandRecoveryInstaller] Resources/{PrefabPath} 를 찾지 못해 회복도 HUD 를 띄우지 않습니다.");
                return;
            }

            root = Object.Instantiate(prefab);
            root.name = prefab.name;
            Object.DontDestroyOnLoad(root);

            if (root.GetComponentInChildren<IslandRecoveryView>(true) == null)
            {
                Debug.LogWarning(
                    $"[IslandRecoveryInstaller] Resources/{PrefabPath} 에 " +
                    $"{nameof(IslandRecoveryView)} 가 없습니다. 회복도가 갱신되지 않습니다.");
            }
        }

        /// <summary>
        /// <b>로비에서만 보이게 한다.</b> 미니게임에서는 숨는다.
        ///
        /// ⚠ 껐다 켜면 <see cref="IslandRecoveryView"/> 의 <c>OnEnable</c> 이 다시 불리면서
        ///    <see cref="AltarState.RequestRefresh"/> 를 한 번 더 낸다. 미니게임을 다녀온 사이에
        ///    남이 봉헌했을 수 있으니 그게 맞다.
        /// </summary>
        /// <summary>
        /// 로비 안이라도 회복도 바를 잠시 감추게 한 것들. (포탈의 인원 선택 · 매칭 화면)
        /// 채팅(<c>LobbyChatInstaller.SetHiddenBy</c>)과 같은 방식이다 — 하나라도 남아 있으면 감춘다.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<Object> hiders =
            new System.Collections.Generic.HashSet<Object>();

        /// <summary><paramref name="owner"/> 가 열려 있는 동안 회복도 바를 감춘다. 닫힐 때 false 로 다시 부른다.</summary>
        public static void SetHiddenBy(Object owner, bool hidden)
        {
            if (owner == null)
            {
                return;
            }

            if (hidden)
            {
                hiders.Add(owner);
            }
            else
            {
                hiders.Remove(owner);
            }

            ShowOnlyInLobby();
        }

        private static void ShowOnlyInLobby()
        {
            if (root == null)
            {
                return;
            }

            // 감춘 쪽이 씬과 함께 사라졌으면 그 이유도 버린다. 남겨 두면 바가 영영 안 나온다.
            hiders.RemoveWhere(owner => owner == null);

            bool show = InLobby() && hiders.Count == 0;

            if (root.activeSelf != show)
            {
                root.SetActive(show);
            }
        }

        /// <summary>
        /// 로비인가.
        ///
        /// ⚠ 씬 이름만 보면 안 된다. Fusion 이 로비를 러너 씬 안의 <c>[Lobby]</c> 오브젝트로
        ///    담아서 이름이 <c>FusionRunner (Client)_[Player:3]</c> 가 된다.
        ///    <c>LobbyTutorial</c> 이 이 함정에 빠져 한 번도 뜨지 않았다.
        ///
        /// ⚠ <c>LobbyChatInstaller.InLobby()</c> · <see cref="AltarOfferingInstaller"/> 의 것과
        ///    같은 내용이다. 그쪽들이 <c>private</c> 이라 부를 수가 없어 그대로 옮겼다.
        ///    한쪽을 고치면 나머지도 같이 고쳐야 한다.
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
