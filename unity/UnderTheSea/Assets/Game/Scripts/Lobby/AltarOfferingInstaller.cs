using Fusion;
using TMPro;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    /// ────────────────────────────────────────────────────────────────
    /// ⚠ <b>여기서 만드는 화면은 STEP 5 검증용 임시 placeholder 다.</b>
    ///
    /// 정식 봉헌 UI 는 STEP 6 에서 <c>Assets/Game/Resources/AltarOfferingUI.prefab</c> 으로
    /// 만들고, 그 프리팹은 사용자가 주실 HUD 이미지를 전제로 한다. 아직 그 이미지가 없다.
    /// 그래서 이 파일은 <b>새 에셋을 하나도 만들지 않고</b> 코드로만 최소 화면을 세운다.
    ///
    /// <code>
    ///   지금 (STEP 5)   코드로 만든 안내 한 줄 + 빈 패널.  기능 없음
    ///   STEP 6          Resources/AltarOfferingUI.prefab 을 불러 쓴다.
    ///                   그때 이 placeholder 코드는 지운다
    /// </code>
    ///
    /// 교체 지점은 <see cref="BuildPlaceholder"/> 하나뿐이다. STEP 6 에서 그 메서드를
    /// <c>Resources.Load&lt;GameObject&gt;("AltarOfferingUI")</c> 로 바꾸면 끝난다.
    /// 수량 조절 · 봉헌 버튼 · 보유량 표시 · 서버 호출 · ChatFocus 는 전부 STEP 6 몫이다.
    /// ────────────────────────────────────────────────────────────────
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 5.5 · 6.2절
    /// </summary>
    public static class AltarOfferingInstaller
    {
        private static GameObject root;
        private static GameObject promptRoot;
        private static GameObject panelRoot;
        private static TextMeshProUGUI promptLabel;

        private static bool playerIsNear;
        private static bool panelOpen;

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
        ///    제단 안내가 안 뜨는 것과 로비에 못 들어가는 것 중에는 앞이 훨씬 낫다.
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

            BuildPlaceholder();

            Object.DontDestroyOnLoad(root);
        }

        // ------------------------------------------------------------
        // 상호작용 → 화면
        // ------------------------------------------------------------

        private static void OnNearChanged(bool near)
        {
            playerIsNear = near;

            // 범위를 벗어나면 열려 있던 창도 닫는다. (설계 5.5절)
            if (!near)
            {
                panelOpen = false;
            }

            Apply();
        }

        private static void OnInteractPressed()
        {
            if (!InLobby())
            {
                return;
            }

            panelOpen = true;
            Apply();
        }

        private static void OnClosePressed()
        {
            if (!panelOpen)
            {
                return;
            }

            panelOpen = false;
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
            bool showPanel = lobby && panelOpen;
            bool showPrompt = lobby && playerIsNear && !panelOpen;

            if (promptRoot != null && promptRoot.activeSelf != showPrompt)
            {
                promptRoot.SetActive(showPrompt);
            }

            if (panelRoot != null && panelRoot.activeSelf != showPanel)
            {
                panelRoot.SetActive(showPanel);
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
                // 로비를 떠나면 열려 있던 창 상태도 접는다. 돌아왔을 때 창이 떠 있으면 안 된다.
                panelOpen = false;
                playerIsNear = false;
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

        // ------------------------------------------------------------
        // STEP 5 검증용 placeholder
        //
        // ⚠ 여기 있는 것은 전부 임시다. STEP 6 에서 이 메서드 하나를
        //    Resources.Load<GameObject>("AltarOfferingUI") 로 바꾸고 아래 코드를 지운다.
        //
        // ⚠ 새 Sprite · Texture · 프리팹 · 폰트 에셋을 만들지 않는다.
        //    Image 는 스프라이트 없이도 단색 사각형을 그리고,
        //    한글은 TMP 설정의 fallback(NotoSansKR)이 이미 받아 준다.
        //
        // ⚠ 클릭을 받지 않으므로 GraphicRaycaster 와 EventSystem 이 필요 없다.
        //    (LobbyChatInstaller 가 EventSystem 을 직접 만들었다가 로비를 멈춘 적이 있다)
        // ------------------------------------------------------------

        private static void BuildPlaceholder()
        {
            root = new GameObject("AltarOfferingUI (STEP 5 Placeholder)");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 채팅·튜토리얼보다 뒤에 그려 가리지 않게 낮게 둔다.
            canvas.sortingOrder = 50;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            promptRoot = BuildPrompt(root.transform);
            panelRoot = BuildPanel(root.transform);

            promptRoot.SetActive(false);
            panelRoot.SetActive(false);
        }

        /// <summary>화면 아래쪽 가운데의 "[E] 조각 봉헌" 한 줄.</summary>
        private static GameObject BuildPrompt(Transform parent)
        {
            GameObject go = new GameObject("Prompt", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 180f);
            rect.sizeDelta = new Vector2(600f, 60f);

            promptLabel = go.AddComponent<TextMeshProUGUI>();
            promptLabel.text = "[E] 조각 봉헌";
            promptLabel.fontSize = 36f;
            promptLabel.alignment = TextAlignmentOptions.Center;
            promptLabel.raycastTarget = false;

            return go;
        }

        /// <summary>화면 가운데의 빈 패널. 기능은 없고 "열렸다" 는 것만 보여 준다.</summary>
        private static GameObject BuildPanel(Transform parent)
        {
            GameObject go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(520f, 280f);

            // 스프라이트 없이 단색만 그린다. 새 에셋이 필요 없다.
            Image background = go.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            background.raycastTarget = false;

            GameObject textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);

            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 24f);
            textRect.offsetMax = new Vector2(-24f, -24f);

            TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = "바다의 심장 봉헌\n\nSTEP 5 Placeholder\n\nEsc 로 닫습니다";
            label.fontSize = 32f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            return go;
        }
    }
}
