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
    /// ⚠ <b>이 화면은 코드로 그린다.</b> 급한 대로 돌아가게 만든 것이라 프리팹이 없다.
    ///    꾸밀 때가 되면 프리팹으로 옮기고 이 파일은 그것을 열고 닫기만 하면 된다.
    ///    지금 프리팹을 만들어 두면 꾸미는 사람이 버릴 것을 먼저 만드는 셈이다.
    /// </summary>
    public sealed class CrewPickerScreen : MonoBehaviour
    {
        private const string HostName = "[인원 선택]";

        private static CrewPickerScreen instance;

        private GameObject root;
        private TMP_Text titleText;
        private RectTransform buttonRow;
        private readonly List<GameObject> crewButtons = new List<GameObject>();

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
                instance.Build();
            }

            instance.Show(config, picked, cancelled);
        }

        /// <summary>열려 있으면 닫는다.</summary>
        public static void Close()
        {
            if (instance != null && instance.root != null) instance.root.SetActive(false);
        }

        private void Show(MiniGameConfig config, Action<int> picked, Action cancelled)
        {
            onPicked = picked;
            onCancelled = cancelled;

            titleText.text = $"{config.DisplayName} — 몇 명이서 하시겠어요?";

            foreach (GameObject button in crewButtons) Destroy(button);
            crewButtons.Clear();

            for (int crew = config.MinPlayers; crew <= config.MaxPlayers; crew++)
            {
                int chosen = crew;   // ⚠ 람다가 바깥 변수를 붙잡는다. 복사해 두지 않으면 전부 마지막 값이 된다.
                crewButtons.Add(MakeButton(buttonRow, $"{crew}인", new Color(.10f, .32f, .55f), () =>
                {
                    root.SetActive(false);
                    onPicked?.Invoke(chosen);
                }));
            }

            root.SetActive(true);
        }

        // ───────────────────────────── 그리기 ─────────────────────────────

        private void Build()
        {
            root = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, worldPositionStays: false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 매칭 판보다 위에 뜬다. 뒤에 깔리면 고를 수가 없다.
            canvas.sortingOrder = 500;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // 뒤를 덮어 로비가 눌리지 않게 한다.
            GameObject shade = MakeImage(root.transform, "Shade", new Color(0f, .02f, .06f, .78f));
            Stretch(shade.GetComponent<RectTransform>());

            GameObject panel = MakeImage(root.transform, "Panel", new Color(.047f, .149f, .286f, 1f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(760f, 300f);
            panelRect.anchoredPosition = Vector2.zero;

            titleText = MakeText(panel.transform, "Title", 40f);
            var titleRect = (RectTransform)titleText.transform;
            titleRect.sizeDelta = new Vector2(700f, 60f);
            titleRect.anchoredPosition = new Vector2(0f, 90f);

            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(panel.transform, worldPositionStays: false);
            buttonRow = (RectTransform)row.transform;
            buttonRow.sizeDelta = new Vector2(700f, 90f);
            buttonRow.anchoredPosition = new Vector2(0f, -10f);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
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

            GameObject cancel = MakeButton(panel.transform, "매칭 취소", new Color(.28f, .10f, .14f), () =>
            {
                root.SetActive(false);
                onCancelled?.Invoke();
            });
            var cancelRect = (RectTransform)cancel.transform;
            cancelRect.anchoredPosition = new Vector2(0f, -105f);

            root.SetActive(false);
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

        private static TMP_Text MakeText(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject MakeButton(Transform parent, string label, Color colour, Action onClick)
        {
            GameObject go = MakeImage(parent, $"Button {label}", colour);
            ((RectTransform)go.transform).sizeDelta = new Vector2(160f, 76f);

            TMP_Text text = MakeText(go.transform, "Label", 32f);
            Stretch((RectTransform)text.transform);
            text.text = label;

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => onClick?.Invoke());
            return go;
        }
    }
}
