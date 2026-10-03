using TMPro;
using UnderTheSea.Character;
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
        [Tooltip("캐릭터 발밑에서 이만큼 위에 꼬리 끝이 온다.\n" +
                 "모자처럼 캐릭터 꼭대기가 이보다 높으면 그 위로 올라간다 (headroom).")]
        [SerializeField, Min(0f)] private float height = 2.05f;

        /// <summary>
        /// 캐릭터 꼭대기와 꼬리 끝 사이의 여유(m).
        ///
        /// <b>모자가 말풍선을 가리지 않게 한다.</b> 말풍선이 뜰 때 캐릭터에 **실제로 그려지는**
        /// 렌더러의 꼭대기를 재서, <see cref="height"/> 보다 높으면 그 꼭대기 + 이 값에 꼬리를 둔다.
        /// 모자가 없으면 꼭대기가 <see cref="height"/> 아래라 예전 자리 그대로다.
        ///
        /// 매 프레임이 아니라 **말이 뜰 때 한 번** 잰다. 걷는 동안 모자가 들썩여도 말풍선은 가만히 있다.
        /// </summary>
        [Tooltip("모자 등 캐릭터 꼭대기와 꼬리 끝 사이 여유(m).")]
        [SerializeField, Min(0f)] private float headroom = 0.1f;

        [Header("크기")]
        [Tooltip("칸 1개가 월드에서 몇 m 인가. 작을수록 말풍선이 작아진다.")]
        [SerializeField, Min(0.0005f)] private float scale = 0.004f;

        [Tooltip("이보다 길어지면 줄이 접힌다. 화면을 가로지르지 않게 한다.")]
        [SerializeField, Min(100f)] private float maxWidth = 380f;

        /// <summary>
        /// 9-slice 테두리를 몇 분의 1 로 그릴까.
        ///
        /// ⚠ <b>말풍선의 최소 크기를 정하는 값이다.</b> 9-slice 는 테두리를 안 늘리므로
        ///    사방 테두리를 더한 만큼이 최소 크기가 된다. 지금 그림(speech-bubble-body-v2)은
        ///    좌우 54px · 위아래 45px 이라, 1 이면 글자가 한 자여도 108 x 90 칸짜리 판이 뜬다.
        ///    1.35 로 나눠 80 x 66 칸으로 맞춰 두었다 — 짧은 말이 딱 붙게 나오는 크기다.
        ///
        ///    너무 키우면 반대로 테두리가 얇아져 모서리가 뭉개진다. 그림을 바꾸면
        ///    <c>.meta</c> 의 <c>spriteBorder</c> 를 보고 이 값을 다시 잡는다.
        /// </summary>
        [SerializeField, Min(1f)] private float bodyBorderShrink = 1.35f;

        [SerializeField, Min(1f)] private float plateBorderShrink = 8f;

        [SerializeField, Min(8f)] private float fontSize = 30f;

        [SerializeField, Min(8f)] private float nameFontSize = 22f;

        [Tooltip("꼬리 크기(가로, 세로).")]
        [SerializeField] private Vector2 tailSize = new Vector2(52f, 34f);

        /// <summary>
        /// 꼬리를 말풍선 안쪽으로 이만큼 밀어 올린다.
        ///
        /// 0 이면 딱 붙는데, 그러면 경계에 테두리가 두 겹으로 겹쳐 선이 한 줄 비쳐 보인다.
        /// 올릴수록 꼬리가 짧아 보이고, 내릴수록 뾰족하게 늘어진다.
        /// </summary>
        [SerializeField] private float tailOverlap = 10f;

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
        private LayoutElement textElement;
        private Transform root;

        /// <summary>캐릭터 외형. 꼭대기 높이를 잴 때 쓴다 (<see cref="FitHeight"/>).</summary>
        private CharacterAppearanceApplier model;

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

            // 띄울 때마다 크기 값을 다시 읽는다. 실행 중에 인스펙터에서 숫자를 바꾸고
            // 한 마디 쳐 보면 바로 반영된다. 안 그러면 껐다 켜기를 반복해야 한다.
            ApplySize();

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
                var plateImage = namePlate.Find("Background").GetComponent<Image>();

                if (plateImage != null)
                {
                    plateImage.color = LobbyChatLine.ColorFor(speaker);
                }
            }

            SetVisible(true);

            // ⚠ 최대 너비를 여기서 건다.
            //
            // 레이아웃 그룹은 글자 칸에게 "한 줄로 쭉 폈을 때 얼마나 필요한가" 를 묻고
            // 그만큼 내준다. 그래서 그냥 두면 긴 말이 2m 넘게 옆으로 퍼진다.
            // 실제로 그렇게 나왔다.
            //
            // 대신 필요한 너비와 <see cref="maxWidth"/> 중 **작은 쪽**을 못 박는다.
            // 짧은 말은 글자만큼만 차지하고, 긴 말은 여기서 줄이 접히며 아래로 자란다.
            if (textElement != null)
            {
                textElement.preferredWidth = Mathf.Min(label.preferredWidth, maxWidth);
            }

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

        /// <summary>
        /// 크기를 맞출 때 쓴다. 부품 오른쪽 위 ⋮ 메뉴에서 고른다.
        ///
        /// <b>Play 를 안 눌러도 된다.</b> 말풍선이 없으면 그 자리에서 만들어 Scene 뷰에
        /// 띄운다. 서버도 남도 필요 없다. 숫자를 돌리면서 짧은 말과 긴 말을 번갈아
        /// 띄워 보면 늘어나는 모양까지 한 번에 확인된다.
        ///
        /// ⚠ 미리보기로 만든 것에는 <see cref="HideFlags.DontSave"/> 를 걸어 둔다.
        ///    안 걸면 프리팹을 저장할 때 말풍선이 통째로 안에 박혀 버린다.
        ///    그러면 게임에서 말풍선이 늘 떠 있게 된다.
        /// </summary>
        [ContextMenu("말풍선 시험 — 짧게")]
        public void TestShort()
        {
            Preview("안녕!");
        }

        [ContextMenu("말풍선 시험 — 길게")]
        public void TestLong()
        {
            Preview("심장 제단 퀘스트 같이 하실 분 구해요. 지금 광장으로 모여 주세요!");
        }

        [ContextMenu("말풍선 시험 — 지우기")]
        public void TestClear()
        {
            if (root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                SetVisible(false);
                return;
            }

            DestroyImmediate(root.gameObject);
            root = null;
            bubble = null;
            namePlate = null;
            label = null;
            nameLabel = null;
            group = null;
        }

        public void Preview(string message)
        {
            if (root == null)
            {
                Build();

                if (!Application.isPlaying)
                {
                    MarkNotSaved(root.gameObject);
                }
            }

            Show("테스트", message);

#if UNITY_EDITOR
            // ⚠ 편집 중에는 LateUpdate 가 안 돈다. 그래서 스스로 카메라를 안 보고,
            //    뒷면이 보이면 **글자가 좌우로 뒤집혀** 읽힌다. 실제로 그렇게 보였다.
            //    버그가 아니라 미리보기의 한계라, 여기서 한 번 돌려 준다.
            UnityEditor.SceneView scene = UnityEditor.SceneView.lastActiveSceneView;

            if (!Application.isPlaying && scene != null && scene.camera != null && root != null)
            {
                root.forward = scene.camera.transform.forward;
            }
#endif
        }

        /// <summary>저장될 것들 틈에 끼어들지 않게 표시한다.</summary>
        private static void MarkNotSaved(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                t.gameObject.hideFlags = HideFlags.DontSave;
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

        /// <summary>
        /// 인스펙터에서 숫자를 바꾸면 바로 반영한다.
        ///
        /// 말풍선은 코드로 만들어지므로 Scene 뷰에는 아무것도 안 보인다. 그래서 크기를
        /// 맞추려면 Play 를 눌러 한 마디 쳐 보는 수밖에 없는데, 그때 값이 안 먹으면
        /// 껐다 켜기를 계속해야 한다. 여기서 받아 주면 실행 중에 숫자만 돌려 가며 맞출 수 있다.
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplySize();
            }
        }

        /// <summary>
        /// 꼬리 끝을 둘 높이. <see cref="height"/> 와 "캐릭터 꼭대기 + <see cref="headroom"/>" 중 높은 쪽.
        ///
        /// 꼭대기는 <see cref="CharacterAppearanceApplier"/> 아래에서 **지금 그려지는** 렌더러로 잰다.
        /// 입은 파츠는 그 아래에 붙고, 파츠에 가려진 기본 몸은 <c>forceRenderingOff</c> 로 꺼진다
        /// (<c>RefreshVisibility</c>). 그래서 모자를 쓰면 모자가, 안 쓰면 머리가 꼭대기가 된다.
        ///
        /// ⚠ 불꽃 · 꼬리 자국 같은 이펙트는 뺀다. 크기가 제멋대로라 말풍선이 튄다.
        /// </summary>
        private float FitHeight()
        {
            if (model == null)
            {
                model = GetComponentInChildren<CharacterAppearanceApplier>(true);
            }

            if (model == null)
            {
                return height;
            }

            float fit = height;

            foreach (Renderer draw in model.GetComponentsInChildren<Renderer>())
            {
                if (!draw.enabled || draw.forceRenderingOff
                    || draw is ParticleSystemRenderer || draw is TrailRenderer || draw is LineRenderer)
                {
                    continue;
                }

                // 캐릭터는 세로축으로만 돈다. 꼭대기 모서리를 캐릭터 기준으로 옮기면 y 가 발밑에서 잰 높이다.
                float top = transform.InverseTransformPoint(draw.bounds.max).y;
                fit = Mathf.Max(fit, top + headroom);
            }

            return fit;
        }

        /// <summary>크기와 관련된 값을 만들어 둔 부품에 다시 먹인다.</summary>
        private void ApplySize()
        {
            if (root == null)
            {
                return;
            }

            root.localScale = Vector3.one * scale;
            root.localPosition = new Vector3(0f, FitHeight(), 0f);

            Transform back = bubble != null ? bubble.Find("Background") : null;

            if (back != null)
            {
                var image = back.GetComponent<Image>();

                if (image != null)
                {
                    image.pixelsPerUnitMultiplier = bodyBorderShrink;
                }
            }

            if (label != null)
            {
                label.fontSize = fontSize;
            }

            if (namePlate != null)
            {
                var plateImage = namePlate.Find("Background").GetComponent<Image>();

                if (plateImage != null)
                {
                    plateImage.pixelsPerUnitMultiplier = plateBorderShrink;
                }
            }

            if (nameLabel != null)
            {
                nameLabel.fontSize = nameFontSize;
            }

            Transform tail = bubble != null ? bubble.Find("Tail") : null;

            if (tail != null)
            {
                var tailRect = (RectTransform)tail;
                tailRect.sizeDelta = tailSize;
                tailRect.anchoredPosition = new Vector2(0f, tailOverlap);
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
            // ⚠ 판 자체에는 Image 를 붙이지 않는다. Image 도 레이아웃 부품이라,
            //    붙여 두면 **그림의 원본 크기**를 "이만큼은 있어야 한다" 고 주장한다.
            //    그러면 글자가 한 자여도 판이 그림만 해진다.
            //    바탕은 따로 깔고 레이아웃에서 빼 둔다.
            var box = new GameObject("Bubble", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            bubble = (RectTransform)box.transform;
            bubble.SetParent(root, false);

            // ⚠ 아래쪽을 기준으로 삼는다. 글이 길어지면 **위로** 자라야 꼬리가 머리에 붙어 있다.
            //    가운데를 기준으로 하면 긴 말일수록 꼬리가 머리를 파고든다.
            bubble.anchorMin = new Vector2(0.5f, 0f);
            bubble.anchorMax = new Vector2(0.5f, 0f);
            bubble.pivot = new Vector2(0.5f, 0f);
            bubble.anchoredPosition = Vector2.zero;

            // 바탕. 판 전체에 깔리되 크기에는 참견하지 않는다.
            var back = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var backRect = (RectTransform)back.transform;
            backRect.SetParent(bubble, false);
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.offsetMin = Vector2.zero;
            backRect.offsetMax = Vector2.zero;

            var image = back.GetComponent<Image>();
            image.sprite = bodySprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = bodyBorderShrink;
            image.raycastTarget = false;

            IgnoreLayout(back);

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

            // 최대 너비는 <see cref="Show"/> 가 띄울 때마다 여기에 건다.
            // 글 길이를 봐야 정할 수 있어서 만들 때는 비워 둔다.
            textElement = text.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 0f;
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

            rect.anchoredPosition = new Vector2(0f, tailOverlap);

            var image = tail.GetComponent<Image>();
            image.sprite = tailSprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            IgnoreLayout(tail);
        }

        private void BuildNamePlate()
        {
            var plate = new GameObject("NamePlate", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            namePlate = (RectTransform)plate.transform;
            namePlate.SetParent(bubble, false);

            // 말풍선 위 가운데. 말풍선이 자라도 늘 그 위에 얹혀 있다.
            namePlate.anchorMin = new Vector2(0.5f, 1f);
            namePlate.anchorMax = new Vector2(0.5f, 1f);
            namePlate.pivot = new Vector2(0.5f, 0f);
            namePlate.anchoredPosition = new Vector2(0f, -6f);

            // ⚠ 말풍선과 같은 이유로 바탕을 자식으로 내린다. 같은 칸에 Image 를 붙이면
            //    그림 원본 크기가 "이만큼은 있어야 한다" 고 주장해서 이름표가 두꺼워진다.
            var back = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var backRect = (RectTransform)back.transform;
            backRect.SetParent(namePlate, false);
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.offsetMin = Vector2.zero;
            backRect.offsetMax = Vector2.zero;

            var image = back.GetComponent<Image>();
            image.sprite = namePlateSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = plateBorderShrink;
            image.raycastTarget = false;

            IgnoreLayout(back);

            var layout = plate.GetComponent<HorizontalLayoutGroup>();

            // 납작한 알약으로. 위아래를 거의 안 띄운다.
            layout.padding = new RectOffset(16, 16, 1, 1);
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

            IgnoreLayout(plate);

            namePlate.gameObject.SetActive(false);
        }

        /// <summary>
        /// 부모의 레이아웃 계산에서 빼 둔다.
        ///
        /// ⚠ <b>앵커로 붙였다고 레이아웃 밖에 있는 것이 아니다.</b> 레이아웃 그룹은
        ///    자식의 앵커를 제 마음대로 덮어쓰고, 자식이 "이만큼은 있어야 한다" 고
        ///    주장하는 크기까지 합산한다.
        ///
        ///    실제로 꼬리 그림의 원본 크기(656x420)가 그대로 더해져서, 글자가 한 자인
        ///    말풍선이 708x570 짜리 판으로 떴다. 이 한 줄이 그것을 막는다.
        /// </summary>
        private static void IgnoreLayout(GameObject go)
        {
            var element = go.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }
    }
}
