using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Warriors.Net
{
    /// <summary>
    /// ESC 로 여는 **개인 메뉴**. 내 화면에만 있고 게임을 멈추지 않는다.
    ///
    /// <code>
    ///   ESC                ->  내 화면에만 어두운 덮개와 [계속] 버튼
    ///   [계속] · ESC       ->  덮개를 닫는다
    ///   그동안 게임은       ->  계속 돈다. 상대는 아무것도 눈치채지 못한다
    /// </code>
    ///
    /// <b>왜 멈추지 않는가.</b> 예전에는 서버에 <c>Rpc_TogglePause</c> 를 보내
    /// <c>Time.timeScale</c> 을 0 으로 잡았다. 2인 플레이에서 2P 가 ESC 를 한 번 누르자
    /// 판이 통째로 멈췄고 끝까지 풀리지 않았다. 멀티에서 한 사람의 키 하나가
    /// 상대의 판까지 세우는 구조는 위험하다.
    ///
    /// <b>씬을 고치지 않고 코드로 만든다.</b> <c>WarriorsNet.unity</c> 의 HUD 는 서연님 것이고
    /// 여기 붙이면 혼자 하는 씬에도 따라간다. 그래서 <see cref="WarriorsMatchState"/> 가
    /// 스폰될 때 클라이언트에서만 이 오브젝트를 만든다. 자기 Canvas 를 따로 가져서
    /// HUD 가 <c>standardHud</c> 를 켜고 끄는 것과 얽히지 않는다.
    ///
    /// ⚠ 멈추는 판정은 <b>전부 서버</b>가 한다. 이 부품은 요청을 보내고 복제된 값을 그릴 뿐이다.
    ///    그래서 한 사람이 멈추면 두 화면이 같은 순간에 멈춘다.
    ///
    /// ⚠ Warriors 씬에는 EventSystem 이 없어 버튼이 눌리지 않는다. (WarriorsRetryButton 과 같은 문제)
    ///    필요할 때 만들어 둔다.
    /// </summary>
    public sealed class WarriorsPauseControl : MonoBehaviour
    {
        private const int SortingOrder = 100;

        private WarriorsMatchState match;

        private GameObject pauseButtonRoot;
        private GameObject overlayRoot;
        private TMP_Text overlayDetail;

        private bool shownPaused;

        /// <summary>내 화면에만 있는 메뉴가 열려 있는가. 복제되지 않는다.</summary>
        private bool menuOpen;

        /// <summary>이 판에 하나만 둔다. 이미 있으면 대상만 바꾼다.</summary>
        public static void Ensure(WarriorsMatchState state)
        {
            if (state == null) return;

            WarriorsPauseControl existing = FindFirstObjectByType<WarriorsPauseControl>(FindObjectsInactive.Include);

            if (existing != null)
            {
                existing.match = state;
                return;
            }

            GameObject root = new GameObject("WarriorsPauseControl");

            // 매치와 같은 씬에 둔다. 러너 씬이 내려가면 같이 사라진다.
            if (state.gameObject.scene.IsValid() && state.gameObject.scene != root.scene)
            {
                SceneManager.MoveGameObjectToScene(root, state.gameObject.scene);
            }

            WarriorsPauseControl control = root.AddComponent<WarriorsPauseControl>();
            control.match = state;
            control.Build();
        }

        private void OnEnable() => EnsureEventSystem();

        private void Update()
        {
            bool live = match != null && match.Object != null && match.Object.IsValid;
            bool canToggle = live && match.CanTogglePause;

            // 판이 끝나면 열려 있던 메뉴는 닫는다. 결과 화면 위에 덮개가 남으면 안 된다.
            if (menuOpen && !canToggle) menuOpen = false;

            // 버튼은 **판이 도는 동안에만**, 그리고 메뉴가 닫혀 있을 때만 보인다.
            // 대기 화면과 결과 화면에는 멈출 것이 없고, 메뉴 안에는 [계속] 버튼이 따로 있다.
            bool showButton = canToggle && !menuOpen;

            if (pauseButtonRoot != null && pauseButtonRoot.activeSelf != showButton)
            {
                pauseButtonRoot.SetActive(showButton);
            }

            if (overlayRoot != null && overlayRoot.activeSelf != menuOpen)
            {
                overlayRoot.SetActive(menuOpen);
            }

            if (menuOpen && overlayDetail != null)
            {
                overlayDetail.text = "게임은 계속 진행 중입니다\n돌아가려면 [계속] 버튼이나 ESC";
            }

            if (menuOpen != shownPaused)
            {
                shownPaused = menuOpen;
                Debug.Log($"[WarriorsPause] 내 화면 메뉴: {(menuOpen ? "열림" : "닫힘")} (게임은 계속 진행)");
            }

            // ESC 로 연다. 이 창에 포커스가 있을 때만 — 한 PC 에서 두 클라이언트를 띄워
            // 확인할 때 서로 간섭하지 않는다.
            Keyboard keyboard = Keyboard.current;

            if (canToggle && keyboard != null && Application.isFocused && keyboard.escapeKey.wasPressedThisFrame)
            {
                RequestToggle();
            }
        }

        /// <summary>
        /// **내 화면의 메뉴만 열고 닫는다.**
        ///
        /// 예전에는 <c>Rpc_TogglePause</c> 로 서버에 요청해 <c>Time.timeScale</c> 을 0 으로 잡았다.
        /// 그러면 한 사람의 ESC 가 두 화면을 같이 세웠고, 실제로 2인 플레이 중 2P 의 ESC 한 번에
        /// 판이 통째로 멈춘 뒤 끝까지 풀리지 않았다. (서버 로그에 재개 기록이 없다)
        ///
        /// ESC 토글에는 <c>Application.isFocused</c> 조건이 있어, 녹화 중처럼 포커스가 다른 창에
        /// 있으면 <b>양쪽 클라이언트 모두</b> ESC 를 받지 못해 아무도 풀 수 없었다.
        ///
        /// 그래서 멈춤을 없애고 개인 메뉴로 바꾼다. 시뮬레이션은 계속 돈다 —
        /// 상대는 내가 메뉴를 열었는지 알지 못하고 계속 플레이한다.
        /// </summary>
        private void RequestToggle()
        {
            if (match == null || match.Object == null || !match.Object.IsValid) return;
            if (!match.CanTogglePause) return;

            menuOpen = !menuOpen;
        }

        // ------------------------------------------------------------
        // 화면 만들기
        // ------------------------------------------------------------

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            gameObject.AddComponent<GraphicRaycaster>();

            TMP_FontAsset font = FindFont();

            // 오른쪽 위 구석의 작은 정지 버튼. 예전에는 왼쪽 위에 "일시정지  (ESC)" 라고
            // 글자를 길게 적은 개발용 버튼이 HUD 한가운데 떠 있었다. 게임 화면에서는
            // 기호 하나면 뜻이 통하고, 구석에 있으면 전투 시야를 가리지 않는다.
            pauseButtonRoot = MakeButton(
                transform, "PauseButton", "❚❚", font, 26f,
                new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(56f, 56f),
                new Color(.04f, .09f, .18f, .55f), RequestToggle);

            // 메뉴를 열었을 때의 덮개. **내 화면에만 보인다.**
            overlayRoot = new GameObject("PauseOverlay", typeof(RectTransform));
            overlayRoot.transform.SetParent(transform, false);
            Stretch(overlayRoot.GetComponent<RectTransform>());

            Image dim = overlayRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, .6f);
            dim.raycastTarget = true;   // 덮개 아래 HUD 가 눌리지 않게

            MakeText(overlayRoot.transform, "Title", "메뉴", font, 96f, FontStyles.Bold,
                new Vector2(.5f, .5f), new Vector2(0f, 90f), new Vector2(900f, 130f));

            overlayDetail = MakeText(overlayRoot.transform, "Detail", string.Empty, font, 30f, FontStyles.Normal,
                new Vector2(.5f, .5f), new Vector2(0f, -10f), new Vector2(900f, 100f));

            MakeButton(
                overlayRoot.transform, "ResumeButton", "계속  (ESC)", font, 30f,
                new Vector2(.5f, .5f), new Vector2(0f, -130f), new Vector2(300f, 72f),
                new Color(.13f, .55f, .3f, .95f), RequestToggle);

            pauseButtonRoot.SetActive(false);
            overlayRoot.SetActive(false);

            EnsureEventSystem();
            Debug.Log("[WarriorsPause] 일시정지 버튼과 개인 메뉴를 만들었습니다. (오른쪽 위 · ESC)");
        }

        private static GameObject MakeButton(
            Transform parent, string name, string label, TMP_FontAsset font, float size,
            Vector2 anchor, Vector2 offset, Vector2 rect, Color color, UnityEngine.Events.UnityAction onClick)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = rect;

            Image image = root.AddComponent<Image>();
            image.color = color;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, .9f);
            colors.pressedColor = new Color(.8f, .8f, .8f, .9f);
            button.colors = colors;

            TMP_Text text = MakeText(root.transform, "Label", label, font, size, FontStyles.Bold,
                new Vector2(.5f, .5f), Vector2.zero, rect);
            text.raycastTarget = false;

            return root;
        }

        private static TMP_Text MakeText(
            Transform parent, string name, string content, TMP_FontAsset font, float size, FontStyles style,
            Vector2 anchor, Vector2 offset, Vector2 rect)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = rect;

            TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// HUD 가 쓰는 글꼴을 그대로 쓴다. 한글이 들어 있는 글꼴이라야 "일시정지" 가 네모로 깨지지 않는다.
        /// 없으면 TMP 기본 글꼴(프로젝트에 한글 폴백이 등록돼 있다).
        /// </summary>
        private static TMP_FontAsset FindFont()
        {
            WarriorsHudPresenter hud = FindFirstObjectByType<WarriorsHudPresenter>(FindObjectsInactive.Include);

            if (hud != null)
            {
                TMP_Text sample = hud.GetComponentInChildren<TMP_Text>(true);
                if (sample != null && sample.font != null) return sample.font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("[WarriorsPause] 버튼 입력을 위해 EventSystem 을 생성했습니다.");
        }
    }
}
