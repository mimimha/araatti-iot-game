using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Network
{
    /// <summary>
    /// 캐릭터 머리 위에 잠깐 뜨는 말풍선.
    ///
    /// 채팅을 치면 창에 한 줄 쌓이는 것과 **동시에** 말한 사람 머리 위에도 뜬다.
    /// 창만 있으면 누가 말했는지 화면에서 찾아야 하고, 그러면 옆에 있는 사람과
    /// 이야기하는 느낌이 안 난다.
    ///
    /// <code>
    ///        ┌──────────┐
    ///        │ Player02 │      이름표. 사람마다 색이 다르다
    ///     ┌──┴──────────┴──┐
    ///     │   같이 가요!    │   말풍선. 글자에 맞춰 늘어난다
    ///     └───────┬────────┘
    ///             ▼            꼬리. 늘 가운데 아래를 가리킨다
    ///          (머리)
    /// </code>
    ///
    /// <b>글자에 맞춰 늘어난다.</b> 짧은 말은 작게, 긴 말은 넓게. 다만 무한정 넓어지면
    /// 화면을 가로지르므로 <see cref="maxWidth"/> 에서 줄이 접힌다.
    ///
    /// <b>캐릭터를 따라다닌다.</b> 캐릭터의 자식이라 따로 따라다니게 할 것이 없다.
    /// 다만 월드에 놓인 판이라 그냥 두면 옆에서 볼 때 종잇장처럼 보인다.
    /// 그래서 매 프레임 카메라 쪽으로 돌린다.
    ///
    /// <b>이름표와 꼬리는 레이아웃 밖에 둔다.</b> 둘 다 말풍선의 자식이지만
    /// 앵커로만 붙어 있고 <see cref="VerticalLayoutGroup"/> 에 들어가지 않는다.
    /// 레이아웃 그룹을 겹쳐 쌓으면 한 프레임 늦게 자리를 잡아 깜빡이는데,
    /// 앵커는 부모 크기가 정해지는 즉시 따라온다.
    ///
    /// ⚠ <b>그림을 코드에서 <c>Resources.GetBuiltinResource</c> 로 찾지 않는다.</b>
    ///    유니티 6 빌드에는 그 기본 스프라이트가 들어 있지 않아 null 이 나온다.
    ///    (실제로 <c>Failed to find UI/Skin/Background.psd</c> 가 떴다.)
    ///    아래 세 칸에 프리팹에서 꽂아 준다.
    /// </summary>
    public class PlayerSpeechBubble : MonoBehaviour
    {
        [Header("그림")]
        [Tooltip("말풍선 바탕. 9-slice 로 늘려 쓴다. 비면 각진 네모가 된다.")]
        [SerializeField] private Sprite bodySprite;

        [Tooltip("아래를 가리키는 꼬리. 비면 꼬리 없이 판만 뜬다.")]
        [SerializeField] private Sprite tailSprite;

        [Tooltip("이름표 바탕. 비면 바탕 없이 글자만 나온다.")]
        [SerializeField] private Sprite namePlateSprite;

        [Header("자리")]
        [Tooltip("캐릭터 발밑에서 이만큼 위에 꼬리 끝이 온다.")]
        [SerializeField, Min(0f)] private float height = 2.05f;

        [Header("크기")]
        [Tooltip("칸 1개가 월드에서 몇 m 인가. 작을수록 말풍선이 작아진다.")]
        [SerializeField, Min(0.0005f)] private float scale = 0.004f;

        [Tooltip("이보다 길어지면 줄이 접힌다. 화면을 가로지르지 않게 한다.")]
        [SerializeField, Min(100f)] private float maxWidth = 380f;

        /// <summary>
        /// 9-slice 테두리를 몇 분의 1 로 그릴까.
        ///
        /// ⚠ <b>이것이 없으면 말풍선이 거대해진다.</b> 말풍선 그림의 테두리는 107px 인데
        ///    그대로 그리면 사방 107칸이 최소 크기가 되어, 글자가 한 자여도
        ///    가로세로 214칸(약 2m)짜리 판이 뜬다. 실제로 그렇게 떴다.
        /// </summary>
        [SerializeField, Min(1f)] private float bodyBorderShrink = 4f;

        [SerializeField, Min(1f)] private float plateBorderShrink = 8f;

        [SerializeField, Min(8f)] private float fontSize = 30f;

        [SerializeField, Min(8f)] private float nameFontSize = 22f;

        [Tooltip("꼬리 크기(가로, 세로).")]
        [SerializeField] private Vector2 tailSize = new Vector2(52f, 34f);

        [Header("시간")]
        [Tooltip("이만큼 있다가 사라진다.")]
        [SerializeField, Min(1f)] private float showSeconds = 8f;

        [Tooltip("사라질 때 흐려지는 시간.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;

        private CanvasGroup group;
        private RectTransform bubble;
        private RectTransform namePlate;
        private TMP_Text label;
        private TMP_Text nameLabel;
        private Transform root;

        private float hideAt;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        /// <summary>말풍선을 띄운다. 이미 떠 있으면 내용을 바꾸고 시간을 다시 센다.</summary>
        public void Show(string message)
        {
            Show(null, message);
        }

        /// <summary>이름표까지 같이 띄운다. 이름이 비면 이름표를 감춘다.</summary>
        public void Show(string speaker, string message)
        {
            if (label == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            label.text = message;
            hideAt = Time.time + showSeconds;

            bool named = !string.IsNullOrWhiteSpace(speaker);

            if (namePlate != null)
            {
                namePlate.gameObject.SetActive(named);
            }

            if (named && nameLabel != null)
            {
                nameLabel.text = speaker;

                // 채팅 목록과 **같은 색**을 쓴다. 창에서 본 색과 머리 위 색이 다르면
                // 같은 사람인 줄 모른다.
                var plateImage = namePlate.GetComponent<Image>();

                if (plateImage != null)
                {
                    plateImage.color = LobbyChatLine.ColorFor(speaker);
                }
            }

            SetVisible(true);

            // 글자가 바뀌었으니 칸을 다시 잰다. 한 프레임 늦으면 이전 크기로 한 번 깜빡인다.
            if (bubble != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
            }

            if (namePlate != null && namePlate.gameObject.activeSelf)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(namePlate);
            }
        }

        private void LateUpdate()
        {
            if (root == null || !root.gameObject.activeSelf)
            {
                return;
            }

            // 시간이 다 되면 흐려지다 사라진다.
            float left = hideAt - Time.time;

            if (left <= 0f)
            {
                SetVisible(false);
                return;
            }

            if (group != null)
            {
                group.alpha = fadeSeconds > 0f ? Mathf.Clamp01(left / fadeSeconds) : 1f;
            }

            // ⚠ 월드에 놓인 판이라 카메라를 안 보면 옆에서 볼 때 사라진 것처럼 보인다.
            //    LateUpdate 에서 돌리는 이유는 카메라가 움직인 뒤여야 하기 때문이다.
            Camera eye = Camera.main;

            if (eye != null)
            {
                root.forward = eye.transform.forward;
            }
        }

        private void SetVisible(bool on)
        {
            if (root != null)
            {
                root.gameObject.SetActive(on);
            }

            if (group != null)
            {
                group.alpha = 1f;
            }
        }

        private void Build()
        {
            // 월드에 놓는 캔버스. 100 단위 = 1m 이 되게 줄여 둔다.
            var host = new GameObject("SpeechBubble", typeof(Canvas), typeof(CanvasGroup));
            root = host.transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, height, 0f);
            root.localScale = Vector3.one * scale;

            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            group = host.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var hostRect = (RectTransform)host.transform;
            hostRect.sizeDelta = new Vector2(maxWidth, 100f);

            BuildBubble();
            BuildTail();
            BuildNamePlate();
        }

        private void BuildBubble()
        {
            var box = new GameObject("Bubble", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            bubble = (RectTransform)box.transform;
            bubble.SetParent(root, false);

            // ⚠ 아래쪽을 기준으로 삼는다. 글이 길어지면 **위로** 자라야 꼬리가 머리에 붙어 있다.
            //    가운데를 기준으로 하면 긴 말일수록 꼬리가 머리를 파고든다.
            bubble.anchorMin = new Vector2(0.5f, 0f);
            bubble.anchorMax = new Vector2(0.5f, 0f);
            bubble.pivot = new Vector2(0.5f, 0f);
            bubble.anchoredPosition = Vector2.zero;

            var image = box.GetComponent<Image>();
            image.sprite = bodySprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = box.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(bubble, false);

            label = text.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.color = new Color(0.10f, 0.13f, 0.22f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = string.Empty;

            // ⚠ 최대 너비를 여기서 건다. 이것이 없으면 긴 말이 한 줄로 쭉 늘어나
            //    화면을 가로지른다. 짧은 말은 이 값과 상관없이 글자만큼만 차지한다.
            var element = text.AddComponent<LayoutElement>();
            element.preferredWidth = -1f;
            element.flexibleWidth = 0f;

            var textRect = (RectTransform)text.transform;
            textRect.sizeDelta = new Vector2(maxWidth, 0f);
        }

        private void BuildTail()
        {
            if (tailSprite == null)
            {
                return;
            }

            var tail = new GameObject("Tail", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)tail.transform;
            rect.SetParent(bubble, false);

            // 말풍선 아래 가운데에 매단다. 말풍선이 넓어져도 늘 가운데다.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = tailSize;

            // 2px 겹쳐 올린다. 딱 붙이면 테두리 선이 한 줄 비쳐 보인다.
            rect.anchoredPosition = new Vector2(0f, 2f);

            var image = tail.GetComponent<Image>();
            image.sprite = tailSprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
        }

        private void BuildNamePlate()
        {
            var plate = new GameObject("NamePlate", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            namePlate = (RectTransform)plate.transform;
            namePlate.SetParent(bubble, false);

            // 말풍선 위 가운데. 말풍선이 자라도 늘 그 위에 얹혀 있다.
            namePlate.anchorMin = new Vector2(0.5f, 1f);
            namePlate.anchorMax = new Vector2(0.5f, 1f);
            namePlate.pivot = new Vector2(0.5f, 0f);
            namePlate.anchoredPosition = new Vector2(0f, -6f);

            var image = plate.GetComponent<Image>();
            image.sprite = namePlateSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var layout = plate.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = plate.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(namePlate, false);

            nameLabel = text.GetComponent<TextMeshProUGUI>();
            nameLabel.fontSize = nameFontSize;
            nameLabel.color = Color.white;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.raycastTarget = false;
            nameLabel.text = string.Empty;

            namePlate.gameObject.SetActive(false);
        }
    }
}
