using Fusion;
using TMPro;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 제단 안내와 봉헌 창을 로비에 띄우는 설치기.
    ///
    /// <b>왜 씬에 올려 두지 않는가.</b> <c>LobbyChatInstaller</c> · <c>LobbyTutorial</c> 과 같은 이유다.
    ///   · Fusion 은 세션을 시작하면서 씬을 러너 전용 씬으로 인수한다. 그때 없어지거나 옮겨진다
    ///   · 22MB 짜리 <c>Lobby.unity</c> 를 건드리지 않아도 된다 (CONVENTION.md 3장)
    ///
    /// 그래서 <see cref="Object.DontDestroyOnLoad"/> 로 올려 두고, <b>로비를 벗어나면 숨긴다.</b>
    /// 숨기기만 하고 지우지 않는 것도 채팅과 같다 — 다시 만들 일이 없다.
    ///
    /// <b>화면은 프리팹 하나에 다 들어 있다.</b> 이 파일은 코드로 Canvas 나 Text 를 조립하지 않는다.
    ///
    /// <code>
    ///   Assets/Game/Resources/AltarOfferingUI.prefab
    ///     Prompt   "[E] 조각 봉헌" 안내 한 줄
    ///     Panel    정식 봉헌 UI  (AltarOfferingUIController 가 붙어 있다)
    /// </code>
    ///
    /// 이 설치기가 정하는 것은 <b>언제 무엇을 보이느냐</b>뿐이고, 창 안에서 벌어지는 일은
    /// <see cref="AltarOfferingUIController"/> 가 맡는다.
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 5.5 · 6.2절
    /// </summary>
    public static class AltarOfferingInstaller
    {
        /// <summary>
        /// 불러올 프리팹. <c>Resources</c> 아래에 있어야 코드가 찾을 수 있다.
        ///
        /// 없으면 조용히 아무것도 하지 않는다. 제단 UI 가 없다고 게임이 멈추면 안 된다.
        /// (<c>LobbyChatInstaller</c> 가 같은 규칙을 쓴다)
        /// </summary>
        private const string PrefabPath = "AltarOfferingUI";

        private static GameObject root;
        private static GameObject promptRoot;
        private static TextMeshProUGUI promptLabel;
        private static AltarOfferingUIController controller;

        private static bool playerIsNear;

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

            AltarInteraction.NearChanged -= OnNearChanged;
            AltarInteraction.NearChanged += OnNearChanged;

            AltarInteraction.InteractPressed -= OnInteractPressed;
            AltarInteraction.InteractPressed += OnInteractPressed;

            AltarInteraction.ClosePressed -= OnClosePressed;
            AltarInteraction.ClosePressed += OnClosePressed;

            // ⚠ 씬이 바뀔 때마다 로비인지 다시 본다. 빼면 미니게임 화면까지 따라간다.
            SceneManager.sceneLoaded += (_, __) => ShowOnlyInLobby();
            SceneManager.sceneUnloaded += _ => ShowOnlyInLobby();
        }

        /// <summary>
        /// 내 캐릭터가 로비에 생긴 순간에 만든다. 채팅·튜토리얼과 같은 신호를 쓴다.
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
                    "[AltarOfferingInstaller] 제단 UI 를 띄우지 못했습니다. 게임은 그대로 진행합니다.\n" + e);
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
                    $"[AltarOfferingInstaller] Resources/{PrefabPath} 를 찾지 못해 제단 UI 를 띄우지 않습니다.");
                return;
            }

            root = Object.Instantiate(prefab);
            root.name = prefab.name;
            Object.DontDestroyOnLoad(root);

            Transform prompt = root.transform.Find("Prompt");
            promptRoot = prompt != null ? prompt.gameObject : null;
            promptLabel = promptRoot != null
                ? promptRoot.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;

            controller = root.GetComponentInChildren<AltarOfferingUIController>(true);

            if (controller == null)
            {
                Debug.LogWarning(
                    $"[AltarOfferingInstaller] Resources/{PrefabPath} 에 " +
                    $"{nameof(AltarOfferingUIController)} 가 없습니다. 봉헌 창이 열리지 않습니다.");
            }
            else
            {
                // 창이 스스로 닫혔을 때(버튼 · Esc) 안내를 다시 띄울지 다시 판단한다.
                controller.Closed += Apply;
            }

            if (promptRoot == null)
            {
                Debug.LogWarning(
                    $"[AltarOfferingInstaller] Resources/{PrefabPath} 에 \"Prompt\" 자식이 없습니다. " +
                    "제단에 다가가도 안내가 뜨지 않습니다.");
            }
        }

        // ------------------------------------------------------------
        // 상호작용 → 화면
        // ------------------------------------------------------------

        private static void OnNearChanged(bool near)
        {
            playerIsNear = near;

            // 범위를 벗어나면 열려 있던 창도 닫는다. (설계 5.5절)
            if (!near && controller != null)
            {
                controller.Close();
            }

            Apply();
        }

        private static void OnInteractPressed()
        {
            if (!InLobby() || controller == null)
            {
                return;
            }

            controller.Open();
            Apply();
        }

        /// <summary>
        /// Esc 를 눌렀다.
        ///
        /// ⚠ 닫을지 말지는 창이 정한다. 채팅칸이 켜져 있으면 그 Esc 는 채팅 몫이다.
        ///    (<see cref="AltarOfferingUIController.CloseFromEscape"/> 가 판단한다)
        /// </summary>
        private static void OnClosePressed()
        {
            if (controller == null)
            {
                return;
            }

            controller.CloseFromEscape();
            Apply();
        }

        /// <summary>
        /// 지금 무엇을 보여야 하는가.
        ///
        /// <code>
        ///   범위 밖            아무것도 없음
        ///   범위 안            안내만
        ///   창이 열림          창만 (안내는 숨긴다)
        ///   로비가 아님        전부 숨김
        /// </code>
        /// </summary>
        private static void Apply()
        {
            if (root == null)
            {
                return;
            }

            bool lobby = InLobby();
            bool panelOpen = controller != null && controller.IsOpen;
            bool showPrompt = lobby && playerIsNear && !panelOpen;

            if (promptRoot != null && promptRoot.activeSelf != showPrompt)
            {
                promptRoot.SetActive(showPrompt);
            }

            // 안내 문구는 인스펙터의 키 설정을 따라간다.
            AltarInteraction active = AltarInteraction.Active;
            if (showPrompt && promptLabel != null && active != null && promptLabel.text != active.PromptText)
            {
                promptLabel.text = active.PromptText;
            }
        }

        /// <summary><b>로비에서만 보이게 한다.</b> 미니게임에서는 숨는다.</summary>
        private static void ShowOnlyInLobby()
        {
            if (root == null)
            {
                return;
            }

            bool lobby = InLobby();

            if (!lobby)
            {
                // 로비를 떠나면 열려 있던 창도 접는다. 창이 닫히면서 입력 잠금도 함께 풀린다.
                playerIsNear = false;

                if (controller != null)
                {
                    controller.Close();
                }
            }

            if (root.activeSelf != lobby)
            {
                root.SetActive(lobby);
            }

            Apply();
        }

        /// <summary>
        /// 로비인가.
        ///
        /// ⚠ 씬 이름만 보면 안 된다. Fusion 이 로비를 러너 씬 안의 <c>[Lobby]</c> 오브젝트로
        ///    담아서 이름이 <c>FusionRunner (Client)_[Player:3]</c> 가 된다.
        ///    <c>LobbyTutorial</c> 이 이 함정에 빠져 한 번도 뜨지 않았다.
        ///
        /// ⚠ <c>LobbyChatInstaller.InLobby()</c> 와 같은 내용이다. 그쪽이 <c>private</c> 이라
        ///    부를 수가 없어 그대로 옮겼다. 한쪽을 고치면 다른 쪽도 같이 고쳐야 한다.
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
