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
        private GameObject retryButtonRoot;
        private GameObject overlayRoot;
        private TMP_Text overlayDetail;

        private bool shownPaused;

        /// <summary>내 화면에만 있는 메뉴가 열려 있는가. 복제되지 않는다.</summary>
        private bool menuOpen;

        /// <summary>
        /// 결과 화면이 뜨고 이만큼 지나야 [다시 하기] 가 살아난다.
        ///
        /// 2P 의 조작은 방향키 + Space 인데 <c>InputSystemUIInputModule</c> 의 기본 바인딩이
        /// Navigate = 방향키 · Submit = Space/Enter 라, 결과 화면이 뜨는 순간 공격 입력이
        /// 그대로 버튼을 눌러 버렸다(2026-09-16 로그: "[Player:2] 가 다시 하기를 눌렀습니다").
        /// 버튼의 navigation=None 과 이 대기 시간이 한 쌍이다.
        /// </summary>
        private const float RetryArmDelaySeconds = .8f;

        /// <summary>이 시각이 지나야 [다시 하기] 입력을 받는다.</summary>
        private float retryArmedAt;

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

            // 멈춤이 풀렸으면 덮개도 닫는다. 이제 멈춤은 서버가 들고 있으므로(IsPaused)
            // 상대가 풀었을 수도 있다. 그 경우 내 화면만 덮개가 남아 있으면 안 된다.
            if (menuOpen && live && !match.IsPaused) menuOpen = false;

            // 버튼은 **판이 도는 동안에만**, 그리고 메뉴가 닫혀 있을 때만 보인다.
            // 대기 화면과 결과 화면에는 멈출 것이 없고, 메뉴 안에는 [계속] 버튼이 따로 있다.
            bool showButton = canToggle && !menuOpen;

            if (pauseButtonRoot != null && pauseButtonRoot.activeSelf != showButton)
            {
                pauseButtonRoot.SetActive(showButton);
            }

            // 판이 끝났을 때만 [다시 하기] 를 띄운다.
            bool showRetry = live && match.IsOver;

            if (retryButtonRoot != null && retryButtonRoot.activeSelf != showRetry)
            {
                retryButtonRoot.SetActive(showRetry);

                if (showRetry)
                {
                    // 결과 화면이 뜨는 순간에 이미 눌려 있던 입력이 그대로 버튼으로 흘러가지 않게
                    // 선택을 비우고 잠깐 잠가 둔다. 위 MakeButton 의 navigation=None 과 한 쌍이다.
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                    retryArmedAt = Time.unscaledTime + RetryArmDelaySeconds;
                }
            }

            if (overlayRoot != null && overlayRoot.activeSelf != menuOpen)
            {
                overlayRoot.SetActive(menuOpen);
            }

            if (menuOpen && overlayDetail != null)
            {
                overlayDetail.text = "게임이 멈췄습니다\n돌아가려면 [계속] 버튼이나 ESC";
            }

            if (menuOpen != shownPaused)
            {
                shownPaused = menuOpen;
                Debug.Log($"[WarriorsPause] 메뉴 {(menuOpen ? "열림" : "닫힘")} · 게임 {(menuOpen ? "멈춤" : "재개")}");
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
        /// 메뉴를 열고 닫으면서 <b>판도 실제로 멈춘다.</b>
        ///
        /// 멈추는 장치는 <c>WarriorsMatchState.Rpc_TogglePause</c> 가 세우는 <c>IsPaused</c> 다.
        /// 매치가 시계와 진행을 멈추고, <c>WarriorsPhase3Director</c> 는 멈춘 만큼 노트의
        /// <c>DueTick</c> 을 뒤로 미룬다. 그래서 재개하면 같은 자리에서 이어진다.
        /// 네트워크 판이므로 멈춤은 <b>두 사람 모두에게</b> 걸린다 — 한쪽만 멈추면 시뮬레이션이 어긋난다.
        ///
        /// ⚠ 한동안 여기서 <c>menuOpen</c> 만 뒤집고 <c>Rpc_TogglePause</c> 를 부르지 않았다.
        ///    그래서 버튼을 눌러도 게임이 계속 돌았다("일시정지인데 안 멈춘다").
        ///
        /// ⚠ 그렇게 바꿨던 이유도 남겨 둔다. 예전 구현은 멈출 때 <c>Time.timeScale</c> 을 0 으로
        ///    잡았는데, 푸는 길이 ESC 하나뿐이었고 그 ESC 에는 <c>Application.isFocused</c> 조건이
        ///    있었다. 녹화 중처럼 포커스가 다른 창에 있으면 <b>양쪽 다</b> ESC 를 못 받아
        ///    아무도 풀 수 없었다. 실제로 2P 의 ESC 한 번에 판이 통째로 멈춘 채 끝났다.
        ///
        ///    지금은 그 덫이 없다. <c>timeScale</c> 은 건드리지 않으므로 UI 는 계속 살아 있고,
        ///    덮개의 [계속] 버튼이 포커스와 무관하게 <b>클릭으로</b> 풀 수 있는 두 번째 길이다.
        ///    ESC 만 남기지 말 것.
        /// </summary>
        private void RequestToggle()
        {
            if (match == null || match.Object == null || !match.Object.IsValid) return;
            if (!match.CanTogglePause) return;

            menuOpen = !menuOpen;
            match.Rpc_TogglePause();
        }

        /// <summary>
        /// 끝난 판을 서버가 처음부터 다시 시작하게 한다.
        ///
        /// 화면이 막 뜬 참이면 무시한다 — 자세한 이유는 <see cref="RetryArmDelaySeconds"/>.
        /// </summary>
        private void RequestRestart()
        {
            if (match == null || match.Object == null || !match.Object.IsValid) return;

            if (Time.unscaledTime < retryArmedAt)
            {
                Debug.Log("[WarriorsPause] 결과 화면이 막 떠서 [다시 하기] 입력을 무시했습니다.");
                return;
            }

            match.Rpc_RequestRestart();
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

            // **눈에 보이는 버튼이어야 한다.**
            //
            // 반투명(알파 .55) 56x56 기호 버튼으로 뒀더니 우상단 점수 카드 위에서 배경에 묻혀
            // "버튼이 없다" 는 말을 여러 번 들었다. 불투명하게, 조금 크게, 글자를 같이 넣는다.
            // 자리는 점수 카드(우상단 y 384~512) 바로 아래라 HUD 와 겹치지 않는다.
            // **글자가 아니라 그림으로 만든다.**
            //
            // ⚠ 예전에는 라벨이 "❚❚  일시정지" 였다. 그런데 U+275A(❚) 는 Noto Sans KR 에 없는
            //    글리프다(실측: GDI+ MeasureString 폭 0). 이 폰트 에셋은 Dynamic 이고
            //    m_FallbackFontAssetTable 이 비어 있어 대체 폰트도 없다. 그래서 화면에는
            //    **빈 네모 두 개 + "일시정지"** 로 찍혔다. "작은 사각형 두 개" 가 이것이었다.
            //
            // 유니코드에 기대지 않고 Image 두 장으로 막대를 세운다. 폰트가 무엇이든 똑같이 보인다.
            // 자리: 점수 카드 **아래**. 카드는 화면 위에서 28~156px 을 쓰므로(프리팹 실측
            // y 384..512, 화면 중앙 원점) 예전 값 -150 은 카드와 **6px 겹쳤다.**
            // -176 으로 내리면 20px 을 띄우고, 왼쪽 목표 문구(-176)와 윗줄이 맞는다.
            pauseButtonRoot = MakeButton(
                transform, "PauseButton", string.Empty, font, 0f,
                new Vector2(1f, 1f), new Vector2(-40f, -176f), new Vector2(64f, 64f),
                new Color(.05f, .11f, .22f, .96f), RequestToggle);

            AddPauseBars(pauseButtonRoot.transform);

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

            // **결과 화면의 [다시 하기].**
            //
            // HUD 프리팹 안에도 같은 버튼이 있는데 실제 플레이에서 보이지 않는다는 지적을 여러 번 받았다.
            // 이 캔버스는 sortingOrder 100 이라 무엇에 가리든 확실히 맨 위에 그려진다.
            // 누르면 서버가 판을 처음으로 되돌린다 — 내 씬만 다시 여는 것이 아니다.
            retryButtonRoot = MakeButton(
                transform, "RetryButton", "다시 하기", font, 30f,
                new Vector2(.5f, 0f), new Vector2(0f, 120f), new Vector2(320f, 76f),
                new Color(.13f, .55f, .3f, .97f), RequestRestart);

            pauseButtonRoot.SetActive(false);
            retryButtonRoot.SetActive(false);
            overlayRoot.SetActive(false);

            EnsureEventSystem();
            Debug.Log("[WarriorsPause] 일시정지 버튼과 개인 메뉴를 만들었습니다. (오른쪽 위 · ESC)");
        }

        /// <summary>
        /// 일시정지 기호 ❚❚ 를 <b>Image 두 장</b>으로 세운다.
        ///
        /// 버튼(64×64) 한가운데에 8×26 막대 두 개를 7px 간격으로 놓는다.
        /// 폰트 글리프를 쓰지 않으므로 폰트 지원 여부와 무관하다.
        /// </summary>
        private static void AddPauseBars(Transform parent)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject bar = new GameObject($"Bar{i + 1}", typeof(RectTransform));
                bar.transform.SetParent(parent, false);

                RectTransform rt = bar.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(.5f, .5f);
                rt.anchorMax = new Vector2(.5f, .5f);
                rt.pivot = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(i == 0 ? -7.5f : 7.5f, 0f);
                rt.sizeDelta = new Vector2(8f, 26f);

                Image fill = bar.AddComponent<Image>();
                fill.color = new Color(.92f, .96f, 1f, 1f);
                fill.raycastTarget = false;   // 막대가 버튼 클릭을 가로채지 않게
            }
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

            // ⚠ **방향키로 선택되면 안 된다.**
            //    2P 의 조작은 방향키 + Space 인데, InputSystemUIInputModule 의 기본 바인딩이
            //    Navigate = 방향키 · Submit = Space/Enter 다. 그대로 두면 결과 화면이 뜬 순간
            //    방향키가 [다시 하기] 를 선택하고 공격키인 Space 가 그것을 눌러 버린다.
            //    실제로 2026-09-16 로그에 "[Player:2] 가 다시 하기를 눌렀습니다" 로 찍혔다 —
            //    자동 재시작이 아니라 플레이어의 공격 입력이 버튼을 누른 것이었다.
            Navigation none = button.navigation;
            none.mode = Navigation.Mode.None;
            button.navigation = none;

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
