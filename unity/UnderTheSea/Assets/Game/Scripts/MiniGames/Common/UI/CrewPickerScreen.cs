using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// **몇 명이서 할 것인지 고르는 화면.** 매칭의 첫 단계다.
    ///
    /// <code>
    ///   광산   [2인] [3인] [4인]      [취소]
    ///   검     [1인] [2인]            [취소]
    ///   배     [4인]                  [취소]
    /// </code>
    ///
    /// <b>왜 범위가 아니라 딱 하나인가.</b> "2~4명" 처럼 고르면 2명이 모인 순간 출발해
    /// 버린다. 4명이서 하고 싶던 사람이 원하지 않는 판에 끌려 들어간다. 같은 수를 고른
    /// 사람끼리만 묶이도록 <b>하나만</b> 고르게 한다.
    ///
    /// <b>이 화면은 코드로 그리되, 모양은 매칭 판에서 빌린다.</b> (<see cref="MatchPanelPresenter"/>)
    ///    금테 남색 판 · 금색 버튼 · 제목 글꼴을 따로 정하지 않으므로 매칭 판을 고치면 이 창도 따라간다.
    ///    크기는 매칭 판보다 작다. 버튼이 몇 개 없다.
    /// </summary>
    public sealed class CrewPickerScreen : MonoBehaviour
    {
        private const string HostName = "[인원 선택]";

        /// <summary>
        /// 판의 가로 크기(캔버스 단위). 세로는 판 그림의 비율로 정한다.
        /// 매칭 판(1360)보다 훨씬 작다 — 제목 두 줄과 버튼 몇 개뿐이라 크면 속이 빈다.
        /// </summary>
        private const float PanelWidth = 720f;

        /// <summary>인원 버튼 줄의 폭. 버튼이 많으면(광산 3개) 이 안에 들어가게 좁힌다.</summary>
        private const float ButtonRowWidth = 600f;
        private const float ButtonGap = 20f;

        private static CrewPickerScreen instance;

        private GameObject root;
        private TMP_Text gameTitleText;
        private TMP_Text titleText;
        private RectTransform buttonRow;
        private readonly List<GameObject> crewButtons = new List<GameObject>();

        /// <summary>모양을 빌려 온 매칭 판. 바뀌면(씬이 바뀌어 새 판이 생기면) 다시 그린다.</summary>
        private MatchPanelPresenter styledFrom;

        private Action<int> onPicked;
        private Action onCancelled;

        /// <summary>
        /// **화면을 연다.** 고르면 <paramref name="picked"/>, 그만두면 <paramref name="cancelled"/>.
        /// </summary>
        public static void Open(MiniGameConfig config, Action<int> picked, Action cancelled)
        {
            if (config == null) return;

            if (instance == null)
            {
                var host = new GameObject(HostName);
                DontDestroyOnLoad(host);
                instance = host.AddComponent<CrewPickerScreen>();
            }

            MatchPanelPresenter style = FindStyle();
            if (instance.root == null || instance.styledFrom != style)
                instance.Build(style);

            instance.Show(config, picked, cancelled);
        }

        /// <summary>열려 있으면 닫는다.</summary>
        public static void Close()
        {
            if (instance != null && instance.root != null) instance.root.SetActive(false);
        }

        /// <summary>
        /// 모양을 빌려 올 매칭 판. 로비에는 늘 있다(<see cref="CommonMatchingUI.Current"/>).
        /// 없으면 null — 그때는 단색으로 그린다.
        /// </summary>
        private static MatchPanelPresenter FindStyle()
        {
            return CommonMatchingUI.Current != null
                ? CommonMatchingUI.Current.GetComponentInChildren<MatchPanelPresenter>(includeInactive: true)
                : null;
        }

        private void Show(MiniGameConfig config, Action<int> picked, Action cancelled)
        {
            onPicked = picked;
            onCancelled = cancelled;

            gameTitleText.text = config.DisplayName;
            titleText.text = "참여 인원을 선택하세요";

            foreach (GameObject button in crewButtons) Destroy(button);
            crewButtons.Clear();

            // 버튼이 많아도 판 안에 들어가게 폭을 나눈다. 적으면 190 에서 멈춘다.
            int count = Mathf.Max(1, config.MaxPlayers - config.MinPlayers + 1);
            float buttonWidth = Mathf.Min(190f, (ButtonRowWidth - ButtonGap * (count - 1)) / count);

            for (int crew = config.MinPlayers; crew <= config.MaxPlayers; crew++)
            {
                int chosen = crew;   // ⚠ 람다가 바깥 변수를 붙잡는다. 복사해 두지 않으면 전부 마지막 값이 된다.
                crewButtons.Add(MakeButton(buttonRow, $"{crew}인", primary: true, new Vector2(buttonWidth, 64f), () =>
                {
                    root.SetActive(false);
                    onPicked?.Invoke(chosen);
                }));
            }

            root.SetActive(true);
        }

        // ───────────────────────────── 그리기 ─────────────────────────────
        //
        // 매칭 판과 같은 창으로 보여야 한다. 그래서 색을 따로 정하지 않고 매칭 판의 것을 빌린다.
        //   판        매칭 판 Image 의 그림(금테 + 남색 속)을 비율 그대로 줄여 쓴다
        //   글자      작은 게임 이름(금색) · 큰 제목(흰색)을 복제한다
        //   버튼      [게임 시작](금색 글자) 을 인원 버튼으로, [매칭 취소](흰 글자) 를 취소로 복제한다
        //
        //   ┌───────── ⚓ ─────────┐
        //   │      광산 미니게임      │   작게 · 금색
        //   │    참여 인원을 선택하세요   │   크게 · 흰색
        //   │                       │
        //   │   [2인] [3인] [4인]    │
        //   │      [매칭 취소]       │
        //   └───────────────────────┘

        private void Build(MatchPanelPresenter style)
        {
            if (root != null) Destroy(root);
            crewButtons.Clear();
            styledFrom = style;

            root = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, worldPositionStays: false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 매칭 판보다 위에 뜬다. 뒤에 깔리면 고를 수가 없다.
            canvas.sortingOrder = 500;

            // 매칭 판 · 섬 회복도와 같은 기준. 다르면 화면비가 바뀔 때 크기가 따로 논다.
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            // 뒤를 덮어 로비가 눌리지 않게 한다.
            GameObject shade = MakeImage(root.transform, "Shade", new Color(0f, .02f, .06f, .78f));
            Stretch(shade.GetComponent<RectTransform>());

            GameObject panel = MakeImage(root.transform, "Panel", new Color(.047f, .149f, .286f, 1f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(PanelWidth, 420f);
            panelRect.anchoredPosition = Vector2.zero;

            Image source = style != null ? style.PanelImage : null;
            if (source != null && source.sprite != null)
            {
                Image image = panel.GetComponent<Image>();
                image.sprite = source.sprite;
                image.type = source.type;
                image.color = source.color;
                image.preserveAspect = source.preserveAspect;

                // 판 그림은 늘이면 금테와 닻 장식이 찌그러진다. 그림 비율 그대로 줄인다.
                Rect art = source.sprite.rect;
                panelRect.sizeDelta = new Vector2(PanelWidth, PanelWidth * art.height / art.width);
            }

            // 떠 있는 동안 로비 채팅창 · 섬 회복도 바를 감춘다.
            root.AddComponent<HideLobbyHudWhileShown>();

            float height = panelRect.sizeDelta.y;

            gameTitleText = MakeText(panel.transform, "GameTitle",
                style != null ? style.GameTitleTemplate : null, 22f, new Color(1f, .82f, .35f));
            PlaceTop((RectTransform)gameTitleText.transform, new Vector2(640f, 28f), height * .13f);

            titleText = MakeText(panel.transform, "Title",
                style != null ? style.TitleTemplate : null, 40f, Color.white);
            PlaceTop((RectTransform)titleText.transform, new Vector2(640f, 52f), height * .13f + 30f);

            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(panel.transform, worldPositionStays: false);
            buttonRow = (RectTransform)row.transform;
            buttonRow.sizeDelta = new Vector2(ButtonRowWidth, 72f);
            buttonRow.anchoredPosition = new Vector2(0f, -height * .02f);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = ButtonGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // ⚠ **이 두 줄이 없으면 버튼이 0×0 이 된다.**
            //    childControl* 은 기본값이 true 여서, 레이아웃 그룹이 자식 크기를 자기가
            //    정한다. 우리 버튼에는 LayoutElement 가 없어 "원하는 크기" 가 0 으로 잡히고,
            //    아래에서 준 sizeDelta 가 통째로 덮어써진다.
            //    화면에는 글자만 20px 간격으로 겹쳐 보이고, 누를 면적이 없어 클릭도 안 먹는다.
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            GameObject cancel = MakeButton(panel.transform, "매칭 취소", primary: false, new Vector2(260f, 60f), () =>
            {
                root.SetActive(false);
                onCancelled?.Invoke();
            });
            var cancelRect = (RectTransform)cancel.transform;
            cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(.5f, .5f);
            cancelRect.pivot = new Vector2(.5f, .5f);
            cancelRect.anchoredPosition = new Vector2(0f, -height * .27f);

            root.SetActive(false);
        }

        /// <summary>판 윗단에서 <paramref name="fromTop"/> 만큼 내려온 자리에 가운데 정렬로 둔다.</summary>
        private static void PlaceTop(RectTransform rect, Vector2 size, float fromTop)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, -fromTop);
        }

        private static GameObject MakeImage(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.GetComponent<Image>().color = colour;
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 글자를 만든다. 매칭 판의 글자가 있으면 복제해 글꼴 · 색 · 외곽선을 그대로 쓴다.
        /// </summary>
        private static TMP_Text MakeText(Transform parent, string name, TMP_Text template, float size, Color colour)
        {
            TMP_Text text;
            if (template != null)
            {
                text = Instantiate(template, parent, worldPositionStays: false);
                text.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, worldPositionStays: false);
                text = go.AddComponent<TextMeshProUGUI>();
                text.fontSize = size;
                text.color = colour;
            }

            text.name = name;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// 버튼을 만든다. 매칭 판의 버튼을 복제하므로 그림 · 눌림 색 · 글자 모양이 같다.
        ///   <paramref name="primary"/> 참   [게임 시작] 모양 (금색 글자) — 인원 버튼
        ///   <paramref name="primary"/> 거짓 [매칭 취소] 모양 (흰 글자)   — 취소
        /// </summary>
        private GameObject MakeButton(Transform parent, string label, bool primary, Vector2 size, Action onClick)
        {
            Button template = styledFrom == null ? null
                : primary ? styledFrom.PrimaryButtonTemplate : styledFrom.SecondaryButtonTemplate;

            Button button;
            TMP_Text text;
            if (template != null)
            {
                button = Instantiate(template, parent, worldPositionStays: false);
                button.gameObject.SetActive(true);   // 서버 판에서는 [게임 시작] 이 꺼져 있다
                button.interactable = true;
                // 복제본이 원본의 클릭 연결을 물고 오지 않게 통째로 새로 만든다.
                button.onClick = new Button.ButtonClickedEvent();
                text = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
            }
            else
            {
                GameObject plain = MakeImage(parent, "Button",
                    primary ? new Color(.10f, .32f, .55f) : new Color(.28f, .10f, .14f));
                button = plain.AddComponent<Button>();
                button.targetGraphic = plain.GetComponent<Image>();
                text = MakeText(plain.transform, "Label", null, 32f, Color.white);
                Stretch((RectTransform)text.transform);
            }

            button.name = $"Button {label}";
            ((RectTransform)button.transform).sizeDelta = size;
            if (text != null) text.text = label;
            button.onClick.AddListener(() => onClick?.Invoke());
            return button.gameObject;
        }
    }
}
