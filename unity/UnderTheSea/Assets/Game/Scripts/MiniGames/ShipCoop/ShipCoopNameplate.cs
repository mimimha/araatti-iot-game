using TMPro;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 머리 위 이름표. 이름 한 줄과 아래를 가리키는 작은 화살표.
///
/// <code>
///      민화        ← 닉네임
///       ▼          ← 화살표. 늘 그 사람 정수리를 가리킨다
///     (머리)
/// </code>
///
/// <b>왜 필요한가.</b> 갑판에 넷이 섞여 있으면 캐릭터 생김새만으로는 누가 누구인지
/// 구분이 안 됩니다. "돛 비었어!" 대신 <b>"민화야 돛!"</b> 이라고 부를 수 있어야
/// 협동이 말로 굴러갑니다.
///
/// <b>내 머리 위에도 뜹니다.</b> 여럿이 겹치면 내가 어디 있는지도 놓치기 때문입니다.
/// 스매시가 1P 를 자기 머리 위에도 띄우는 것과 같은 이유입니다.
///
/// <b>이름은 <see cref="NetworkPlayerIdentity"/> 에서 가져옵니다.</b> 접속할 때 넘긴
/// 닉네임은 세션을 여는 데만 쓰이고 플레이어 오브젝트까지 오지 않습니다. 그래서
/// 캐릭터에 붙은 그 컴포넌트가 [Networked] 로 들고 있는 값을 읽습니다.
/// 아직 안 왔으면 <c>DisplayName</c> 이 대신할 말을 줍니다.
///
/// ⚠ <b>월드에 놓인 판이라 매 프레임 카메라 쪽으로 돌립니다.</b> 안 돌리면 옆에서 볼 때
///    종잇장처럼 보이고, 뒤에서 보면 글자가 좌우로 뒤집혀 읽힙니다.
///    <see cref="LateUpdate"/> 에서 도는 이유는 카메라가 움직인 <b>뒤</b>여야 하기 때문입니다.
///
/// ⚠ <b>화살표 그림을 에셋으로 두지 않고 코드에서 만듭니다.</b> 삼각형 하나 때문에
///    PNG 를 하나 더 들이고 임포트 설정을 맞추는 것보다, 스프라이트를 한 번 구워
///    모두가 나눠 쓰는 편이 간단합니다. (<see cref="ArrowSprite"/>)
/// </summary>
[DisallowMultipleComponent]
public class ShipCoopNameplate : MonoBehaviour
{
    [Header("자리")]
    [Tooltip("머리뼈에서 이만큼 위에 화살표 끝이 온다.\n" +
             "머리뼈를 못 찾으면 발밑 기준으로 재는데, 그때는 여기에 2.9 를 더해 쓴다.")]
    [SerializeField, Min(0f)] private float height = 1.45f;

    [Tooltip("칸 1개가 월드에서 몇 m 인가. 작을수록 이름표가 작아진다.")]
    [SerializeField, Min(0.0005f)] private float scale = 0.006f;

    [Header("모양")]
    [SerializeField, Min(8f)] private float fontSize = 34f;

    /// <summary>
    /// 사람마다 다른 색. **이름과 화살표가 같은 색을 쓴다.**
    ///
    /// 넷이 같은 모습으로 갑판을 돌아다니면 이름을 읽기 전까지 누가 누군지
    /// 모릅니다. 색이 다르면 화면 가장자리에 화살표만 떠 있어도 누구인지 압니다.
    ///
    /// 빨강 · 파랑 · 보라 · 노랑. 갑판(나무색)과 바다(청록) 위에서 넷 다 튑니다.
    /// 어두운 쪽은 피했습니다 — 글자는 검은 테두리를 두르므로 밝아야 읽힙니다.
    /// </summary>
    [Tooltip("사람마다 돌아가며 쓰는 색. 이름과 화살표가 같은 색이 된다.")]
    [SerializeField]
    private Color[] playerColors =
    {
        new Color(1f, 0.33f, 0.33f),   // 빨강
        new Color(0.36f, 0.68f, 1f),   // 파랑
        new Color(0.78f, 0.51f, 1f),   // 보라
        new Color(1f, 0.83f, 0.30f),   // 노랑
    };

    [Tooltip("색을 못 정했을 때 쓸 색. (혼자 테스트하는 씬처럼 번호가 없을 때)")]
    [SerializeField] private Color fallbackColor = new Color(1f, 0.96f, 0.86f);

    [Tooltip("글자 테두리 두께. 밝은 갑판 위에서도 읽히게 한다. 0 이면 테두리 없음.")]
    [SerializeField, Range(0f, 1f)] private float outline = 0.3f;

    [Tooltip("화살표 크기 (가로, 세로)")]
    [SerializeField] private Vector2 arrowSize = new Vector2(30f, 22f);

    [Header("보이는 거리")]
    [Tooltip("카메라에서 이보다 멀면 감춘다. 0 이면 거리와 상관없이 늘 보인다.\n" +
             "배 협동은 갑판 한 층만 비추므로 기본값이면 사실상 늘 보인다.")]
    [SerializeField, Min(0f)] private float hideBeyond = 0f;

    [Header("화면 밖으로 나갔을 때")]
    [Tooltip("화면 밖에 있는 사람을 가장자리 화살표로 가리킬지.\n\n" +
             "갑판이 3층이라 다른 층에 있는 사람은 아예 안 보인다. 그때 어느 쪽에\n" +
             "있는지라도 알려주면 '어디야?' 를 안 물어도 된다.")]
    [SerializeField] private bool pointFromEdge = true;

    [Tooltip("가장자리에서 이만큼 안쪽에 붙인다. 화면 크기의 비율이다.\n" +
             "0 이면 딱 모서리에 붙어 반이 잘린다.")]
    [SerializeField, Range(0f, 0.3f)] private float edgeMargin = 0.045f;

    /// <summary>
    /// 가장자리 이름표를 카메라 앞 이만큼 되는 곳에 띄운다 (m).
    ///
    /// ⚠ <b>카메라 코앞이어야 한다.</b> 6m 에 뒀더니 거기가 선체·갑판 <b>안쪽</b>이라
    ///    <b>이름이 깊이에 가려 아예 안 보였다.</b> 화살표만 보였는데, 화살표는 깊이 검사를
    ///    끈 UI 머티리얼을 직접 물렸지만 글자(TMP)는 한글 글리프가 런타임에 아틀라스로
    ///    들어갈 때 머티리얼이 다시 만들어지며 그 설정이 날아가기 때문이었다.
    ///    코앞에 두면 무엇보다 앞이라 이 문제가 아예 없다.
    /// </summary>
    [Tooltip("가장자리 이름표를 카메라 앞 이만큼 되는 곳에 띄운다 (m). 코앞이어야 한다.")]
    [SerializeField, Min(0.31f)] private float edgeDistance = 0.8f;

    /// <summary>
    /// 가장자리에 있을 때의 크기 배수.
    ///
    /// <see cref="edgeDistance"/> 와 함께 움직인다. 판이 가까울수록 작게 잡아야
    /// 화면에서 같은 크기가 된다 — 6m 에 1.8 이던 것을 0.8m 로 당기며 0.24 로 줄였고,
    /// 그것도 커 보인다고 해서 60% 인 0.144 로 다시 줄였다.
    /// </summary>
    [Tooltip("가장자리에 있을 때의 크기 배수. edgeDistance 를 바꾸면 같이 바꿔야 한다.")]
    [SerializeField, Min(0.01f)] private float edgeScale = 0.144f;

    [Tooltip("가장자리에서 화살표를 이만큼 키운다. 작으면 시계 · 체력바에 묻힌다.")]
    [SerializeField, Min(1f)] private float edgeArrowScale = 1.6f;

    [Tooltip("가장자리에서 이름을 이만큼 키운다. 갑판 · 선체 위에서도 읽혀야 한다.")]
    [SerializeField, Min(1f)] private float edgeLabelScale = 1f;

    [Tooltip("가장자리에서 이름을 화살표 위로 이만큼 띄운다.")]
    [SerializeField, Min(0f)] private float edgeLabelGap = 40f;

    [Tooltip("위쪽 가장자리에서 이름이 잘리지 않게 비워 두는 높이. 화면 높이의 비율이다.")]
    [SerializeField, Range(0f, 0.3f)] private float edgeLabelHeadroom = 0.15f;

    private NetworkPlayerIdentity identity;
    private Transform root;
    private TMP_Text label;
    private Image arrowImage;
    private RectTransform arrowRect;
    private RectTransform labelRect;
    private string shown;

    /// <summary>색을 이미 정했는가. 번호는 스폰 뒤에야 오므로 한 번만 정하고 만다.</summary>
    private bool colored;

    /// <summary>
    /// 머리뼈. 이름표 **높이**만 여기서 가져온다.
    ///
    /// 가로세로 자리까지 머리뼈에서 가져오면 이름표가 옆으로 치우친다 —
    /// 머리뼈는 목 위 얼굴 쪽에 있어서 고개를 돌리거나 걸을 때 좌우로 흔들린다.
    /// 그래서 가로세로는 캐릭터 루트(몸통 한가운데)를 쓴다. <see cref="Anchor"/> 참고.
    /// </summary>
    private Transform head;

    /// <summary>머리뼈를 못 찾았을 때 발밑에서 더해 줄 키. 캐릭터 키가 2.8575 다.</summary>
    private const float FallbackHeadHeight = 2.9f;

    /// <summary>이 거리보다 카메라에 가까우면 투영을 믿지 않는다 (m).</summary>
    private const float NearGuard = 1f;

    /// <summary>
    /// 화살표 스프라이트. **한 번 만들어 모두가 나눠 쓴다.**
    ///
    /// 사람마다 만들면 4벌이 생기고, 그 텍스처들이 각자 드로우콜을 잡는다.
    /// 모양이 같으므로 하나면 된다.
    /// </summary>
    private static Sprite arrowSprite;

    private static Material seeThroughUI;

    /// <summary>
    /// 깊이 검사를 끈 UI 머티리얼. **벽 뒤에 있어도 보이게 한다.**
    ///
    /// 월드에 놓인 판이라 그냥 두면 돛대 · 선체에 가립니다. 그런데 이름표는
    /// <b>가려졌을 때가 정확히 필요한 순간</b>입니다 — 안 보이는 사람이 어디 있는지
    /// 알려주는 것이 하는 일이니까요. 화면 밖 화살표는 카메라 앞 몇 m 에 놓이므로
    /// 이게 없으면 선체 안쪽에 박혀 아예 안 보입니다.
    /// </summary>
    private static Material SeeThroughUI
    {
        get
        {
            if (seeThroughUI != null)
            {
                return seeThroughUI;
            }

            Shader uiShader = Shader.Find("UI/Default");
            if (uiShader == null)
            {
                // 셰이더가 빠진 빌드(서버). Awake 에서 이미 막지만 한 번 더 지킨다.
                return null;
            }

            seeThroughUI = new Material(uiShader)
            {
                name = "ShipCoopNameplateSeeThrough",
                hideFlags = HideFlags.HideAndDontSave,
            };

            seeThroughUI.SetInt(
                "unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

            return seeThroughUI;
        }
    }

    private static Sprite ArrowSprite
    {
        get
        {
            if (arrowSprite != null)
            {
                return arrowSprite;
            }

            // 아래를 가리키는 삼각형. 위가 넓고 아래가 뾰족하다.
            const int Size = 64;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "ShipCoopNameplateArrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < Size; y++)
            {
                // y 가 0(아래)일 때 폭 0, y 가 Size-1(위)일 때 폭 전체.
                float t = y / (float)(Size - 1);
                float half = t * Size * 0.5f;
                float mid = Size * 0.5f;

                for (int x = 0; x < Size; x++)
                {
                    // 가장자리 한 칸을 부드럽게 깎아 계단을 없앤다.
                    float d = half - Mathf.Abs(x + 0.5f - mid);
                    float a = Mathf.Clamp01(d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();

            arrowSprite = Sprite.Create(
                tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            arrowSprite.name = "ShipCoopNameplateArrow";
            arrowSprite.hideFlags = HideFlags.HideAndDontSave;

            return arrowSprite;
        }
    }

    private void Awake()
    {
        // ⚠ **서버에서는 만들지 않는다.** Dedicated Server 빌드는 셰이더를 전부 빼서
        //    `Shader.Find("UI/Default")` 가 null 이고 `new Material(null)` 이 터졌다.
        //    이름표는 그림이라 서버가 할 일이 없다. 그래픽 장치가 없으면 그냥 빠진다.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            enabled = false;
            return;
        }

        identity = GetComponent<NetworkPlayerIdentity>();
        head = FindHead();
        Build();
    }

    /// <summary>
    /// 머리뼈를 찾는다. 휴머노이드면 Animator 가 바로 알려준다.
    /// 아니면 뼈 이름으로 찾는다 — 이 모델의 뼈 이름은 `Head` 다.
    /// </summary>
    private Transform FindHead()
    {
        var animator = GetComponentInChildren<Animator>();

        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);

            if (bone != null)
            {
                return bone;
            }
        }

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Head")
            {
                return t;
            }
        }

        Debug.LogWarning(
            "[ShipCoopNameplate] 머리뼈를 못 찾았습니다. 발밑 기준으로 올립니다.", this);
        return null;
    }

    /// <summary>
    /// 이름표가 놓일 월드 자리.
    ///
    /// <b>높이는 머리뼈에서, 가로세로 자리는 캐릭터 루트에서</b> 가져온다.
    ///
    /// ⚠ 머리뼈 자리를 통째로 쓰면 **이름표가 옆으로 치우친다.** 머리뼈는 목 위
    ///    얼굴 쪽에 있어서 고개를 돌리거나 걷는 동작에 따라 좌우로 흔들린다.
    ///    루트는 캡슐 한가운데(x·z 가 0)라 몸통 중심이므로 흔들리지 않는다.
    /// </summary>
    private Vector3 Anchor
    {
        get
        {
            float top = head != null
                ? head.position.y + height
                : transform.position.y + height + FallbackHeadHeight;

            Vector3 middle = transform.position;
            return new Vector3(middle.x, top, middle.z);
        }
    }

    private void Build()
    {
        // 월드에 놓는 캔버스. 100 단위 = 1m 이 되게 줄여 둔다.
        var host = new GameObject("Nameplate", typeof(Canvas));
        host.hideFlags = HideFlags.DontSave;

        root = host.transform;
        root.SetParent(transform, false);
        root.localScale = Vector3.one * scale;

        // 자리는 LateUpdate 가 월드 기준으로 잡는다. 여기서는 한 번만 맞춰 둔다.
        root.position = Anchor;

        var canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // ⚠ **판을 넉넉하게 잡는다.** 예전에 세로 120(가운데 기준 ±60)이었는데,
        //    가장자리 모드의 이름이 y 61.6 이라 판 밖으로 나갔다. 머리 위 모드는 52 라
        //    안쪽이어서, **가장자리에서만 이름이 안 보이는** 증상이 났다.
        //    판은 보이는 것이 아니므로 크게 잡아도 잃을 것이 없다.
        var hostRect = (RectTransform)host.transform;
        hostRect.sizeDelta = new Vector2(600f, 400f);

        // ── 화살표 ──
        //
        // ⚠ **앵커를 캔버스 한가운데(0.5, 0.5)에 둔다.** 아래 모서리에 걸어 두면
        //    캔버스 원점이 화살표가 아니라 판 한가운데가 되어, 화면 밖 모드에서
        //    가장자리에 맞춰 놓아도 화살표는 60칸 아래에 찍힌다. 실제로 그렇게 빗나갔다.
        //    자리는 <see cref="Place"/> 가 모드마다 다시 잡는다.
        var arrowGo = new GameObject("Arrow", typeof(Image));
        arrowRect = (RectTransform)arrowGo.transform;
        arrowRect.SetParent(root, false);
        arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(0f, arrowSize.y * 0.5f);
        arrowRect.sizeDelta = arrowSize;

        var arrow = arrowGo.GetComponent<Image>();
        arrow.sprite = ArrowSprite;
        arrow.color = fallbackColor;
        arrowImage = arrow;
        arrow.raycastTarget = false;
        arrow.material = SeeThroughUI;

        // ── 이름. 화살표 바로 위 ──
        var labelGo = new GameObject("Label", typeof(TextMeshProUGUI));
        labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(root, false);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, arrowSize.y + 30f);
        labelRect.sizeDelta = new Vector2(400f, 60f);

        label = labelGo.GetComponent<TextMeshProUGUI>();
        // ⚠ 가운데 정렬이다. Bottom 으로 두면 글자가 상자 아래쪽에 붙어서,
        //    가장자리 모드에서 화살표 위로 띄운 만큼이 글자에 반영되지 않는다.
        //    실측으로 이름 상자는 y 0.10 인데 글자는 0.065 에 찍혀 화살표와 겹쳤다.
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = fallbackColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        // 글자도 같이 벽을 통과해 보이게 한다. TMP 는 자기 셰이더의 `_ZTestMode` 를 본다.
        // (Always = 8) 그 값이 없는 셰이더면 SetFloat 이 그냥 아무 일도 안 한다.
        label.fontMaterial.SetFloat("_ZTestMode", 8f);

        if (outline > 0f)
        {
            // 밝은 하늘과 흰 돛 위에서도 읽히게 한다.
            //
            // ⚠ `fontMaterial` 은 **이 글자만의 사본**을 돌려준다. (`fontSharedMaterial` 이
            //    원본이다) 그래서 여기서 굵혀도 HUD 의 다른 글자는 그대로다.
            label.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.09f, 0.16f));
            label.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        }

        // 이름이 오기 전에는 빈 채로 둔다. `이름 없음` 이 한 프레임 번쩍이지 않게.
        label.text = string.Empty;
    }

    private void LateUpdate()
    {
        if (root == null)
        {
            return;
        }

        Camera eye = Camera.main;

        if (eye == null)
        {
            return;
        }

        // 너무 멀면 감춘다. 0 이면 이 규칙을 안 쓴다.
        if (hideBeyond > 0f)
        {
            bool near = (eye.transform.position - transform.position).sqrMagnitude
                        <= hideBeyond * hideBeyond;

            if (root.gameObject.activeSelf != near)
            {
                root.gameObject.SetActive(near);
            }

            if (!near)
            {
                return;
            }
        }

        Colorize();
        Place(eye);

        // ⚠ 카메라를 안 보면 옆에서 볼 때 종잇장이 되고 뒤에서는 글자가 뒤집힌다.
        root.forward = eye.transform.forward;

        // 이름은 늦게 온다. 바뀔 때만 글자를 다시 만든다 —
        // 매 프레임 text 에 넣으면 같은 값이어도 메시를 다시 짓는다.
        string want = identity != null ? identity.DisplayName : string.Empty;

        if (want != shown)
        {
            shown = want;
            label.text = want;
        }

    }

    /// <summary>
    /// 이 사람 색을 정한다. **이름과 화살표가 같은 색을 쓴다.**
    ///
    /// 번호는 Fusion 의 <c>InputAuthority.PlayerId</c> 다. 모두가 같은 번호를 보므로
    /// <b>네 화면에서 내가 빨강이면 내 화면에서도 빨강</b>이다. 이게 어긋나면
    /// "빨간 애가 돛 잡아" 가 통하지 않는다.
    ///
    /// ⚠ <b>Awake 가 아니라 여기서 정한다.</b> 스폰되는 순간에는 번호가 아직 없다.
    ///    한 번 정하고 나면 다시 계산하지 않는다.
    /// </summary>
    private void Colorize()
    {
        if (colored || playerColors == null || playerColors.Length == 0)
        {
            return;
        }

        var netObject = GetComponent<Fusion.NetworkObject>();

        if (netObject == null || !netObject.InputAuthority.IsRealPlayer)
        {
            return;
        }

        Color mine = playerColors[
            Mathf.Abs(netObject.InputAuthority.PlayerId) % playerColors.Length];

        label.color = mine;
        arrowImage.color = mine;
        colored = true;
    }

    /// <summary>
    /// 이름표를 어디에 둘지 정한다. 두 가지 모드가 있다.
    ///
    /// <code>
    ///   화면 안   머리 위             화살표는 아래(정수리)를 가리킨다
    ///   화면 밖   화면 가장자리       화살표가 그 사람 있는 쪽을 가리킨다
    /// </code>
    ///
    /// <b>왜 화면 밖 표시가 필요한가.</b> 갑판이 3층이라 다른 층 사람은 화면에
    /// 아예 안 나옵니다. "수리하러 어디로 가?" 를 매번 말로 물어야 했습니다.
    /// 가장자리에 방향이라도 떠 있으면 한 번 보고 움직일 수 있습니다.
    ///
    /// ⚠ <b>화면 밖 모드도 월드 캔버스 그대로 씁니다.</b> 스크린 캔버스를 따로 두면
    ///    HUD 에 칸을 만들고 사람 수만큼 연결해야 합니다. 대신 <b>카메라 앞
    ///    <see cref="edgeDistance"/> m 되는 곳</b>의 해당 화면 좌표에 판을 놓습니다.
    ///    거리가 고정이라 가장자리에서 크기가 흔들리지 않습니다.
    /// </summary>
    private void Place(Camera eye)
    {
        Vector3 anchor = Anchor;

        if (!pointFromEdge)
        {
            root.position = anchor;
            root.localScale = Vector3.one * scale;
            arrowRect.localRotation = Quaternion.identity;
            return;
        }

        // ⚠ **카메라 뒤에서는 투영 좌표를 쓰지 않는다.**
        //
        //    `WorldToViewportPoint` 는 z 로 나누는데 뒤에 있으면 z 가 음수라 x·y 가
        //    통째로 뒤집혀 나온다. 그래서 **뒷갑판(카메라 뒤 · 아래)에 있는 사람을
        //    화면 위쪽으로 가리켰다.** 실제로 그렇게 보였다.
        //
        //    뒤에 있을 때는 카메라 기준 좌표(왼쪽/오른쪽, 위/아래)만 보면 된다.
        //    어차피 화면 밖이므로 정확한 투영 자리는 필요 없고 방향만 맞으면 된다.
        Vector3 local = eye.transform.InverseTransformPoint(anchor);

        // 화면 안인지 아닌지는 투영으로 판단한다. 카메라 앞에 충분히 있을 때만 믿는다.
        Vector3 view = local.z > NearGuard
            ? eye.WorldToViewportPoint(anchor)
            : new Vector3(-1f, -1f, local.z);

        float lo = edgeMargin;
        float hi = 1f - edgeMargin;

        // ⚠ **위 경계만 더 내려 잡는다.** 이름이 늘 화살표 위에 붙으므로, 화살표를
        //    맨 위에 두면 이름이 화면 밖으로 잘린다. 이름 높이만큼 자리를 비워 둔다.
        float hiY = hi - edgeLabelHeadroom;

        bool inside = local.z > NearGuard
                      && view.x >= lo && view.x <= hi && view.y >= lo && view.y <= hi;

        if (inside)
        {
            root.position = anchor;
            root.localScale = Vector3.one * scale;
            arrowRect.localRotation = Quaternion.identity;
            arrowRect.localScale = Vector3.one;

            // 화살표 뾰족한 끝이 캔버스 원점(= 정수리 위)에 오게 반만 올린다.
            arrowRect.anchoredPosition = new Vector2(0f, arrowSize.y * 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, arrowSize.y + 30f);
            labelRect.localScale = Vector3.one;
            return;
        }
        // ── 화면 밖 ──
        //
        // ⚠ **자리를 투영으로 잡지 않는다.**
        //
        //    카메라가 갑판을 내려다보느라 아래로 꺾여 있어서, 카메라 기준 세로(`y`)가
        //    실제 위아래와 다르다. 뒷갑판(선미루)은 **높아서** 화면 위로 투영된다.
        //    실측으로 상대가 카메라보다 +1.87 m, +2.70 m 위였다. 그런데 사람이 원하는 것은
        //    "뒤에 있으면 아래" 다. 높이를 빼고 앞뒤만 봐야 한다.
        //    카메라 평면 근처에서는 투영 자체도 터진다 (`z 0.02` 에서 뷰포트 `-109, 141`).
        //
        // ⚠ **기준점은 카메라 시선이 그 사람 높이의 수평면과 만나는 점이다.**
        //
        //    카메라 자리를 기준으로 하면 안 된다 — 카메라가 배 뒤 높은 데 있어서 갑판 위
        //    모두가 "앞" 이라 전부 위로 간다. 내 캐릭터를 기준으로 해도 안 된다 — 내가
        //    움직이면 가만히 있는 사람의 화살표가 따라 움직이고, 화면 왼쏙에 서 있는
        //    사람이 나보다 살짝 뒤라는 이유로 아래로 갔다. 실제로 둘 다 그랬다.
        //
        //    화면 한가운데 시선을 **그 사람 높이의 수평면**까지 쏘아 만나는 점을 잡으면,
        //      - 카메라에서 나온 기준이라 내가 움직여도 흔들리지 않고
        //      - 좌우는 화면 좌우와 같고
        //      - 앞뒤는 높이가 빠진 채로 재어져 뒷갑판이 아래로 간다.
        //    내 이름표도 같은 식이라 따로 처리할 것이 없다.
        Vector3 ahead = Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up).normalized;

        Ray centerRay = eye.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var levelPlane = new Plane(Vector3.up, anchor);

        // 시선이 그 높이와 안 만나면(위를 보는 카메라) 카메라 앞 적당한 거리로 대신한다.
        Vector3 focus = levelPlane.Raycast(centerRay, out float hit) && hit > 0f
            ? centerRay.GetPoint(hit)
            : eye.transform.position + ahead * 10f;

        Vector3 delta = anchor - focus;

        Vector2 toward = new Vector2(
            Vector3.Dot(delta, eye.transform.right),
            Vector3.Dot(delta, ahead));

        if (toward.sqrMagnitude < 1e-6f)
        {
            toward = Vector2.down;
        }

        // 화면 비율을 고려해 방향을 화면 좌표계로 옮긴다. 뷰포트는 0~1 정사각형이고
        // 실제 화면은 가로로 길어서, 안 나누면 각이 눕는다.
        Vector2 aim = new Vector2(toward.x / eye.aspect, toward.y).normalized;

        var clamped = new Vector2(
            Mathf.Clamp(0.5f + aim.x * 2f, lo, hi),
            Mathf.Clamp(0.5f + aim.y * 2f, lo, hiY));

        root.position = eye.ViewportToWorldPoint(new Vector3(clamped.x, clamped.y, edgeDistance));
        root.localScale = Vector3.one * scale * edgeScale;

        // 화살표를 그 사람 쪽으로 돌린다. 평소 모양이 **아래**를 가리키므로 거기서 잰다.
        Vector2 dir = new Vector2(toward.x, toward.y);

        arrowRect.localScale = Vector3.one * edgeArrowScale;

        // 화살표를 캔버스 원점에 둔다. 그래야 위에서 맞춘 가장자리 자리에 정확히 찍힌다.
        arrowRect.anchoredPosition = Vector2.zero;

        if (dir.sqrMagnitude > 1e-8f)
        {
            dir.Normalize();
            arrowRect.localRotation = Quaternion.Euler(
                0f, 0f, Vector2.SignedAngle(Vector2.down, dir));
        }
        else
        {
            dir = Vector2.down;
        }

        // ⚠ **이름은 언제나 화살표 위다.** 머리 위에 있을 때와 같은 모양이라
        //    눈이 찾는 자리가 안 바뀐다. 화살표만 방향을 가리키고 이름은 안 돈다.
        //
        //    위쪽 모서리에서 이름이 잘리지 않게, 위 경계만 이름 높이만큼 더 내려 잡는다.
        //    (`hiY` 참고)
        labelRect.anchoredPosition =
            new Vector2(0f, arrowSize.y * edgeArrowScale * 0.5f + edgeLabelGap);
        labelRect.localScale = Vector3.one * edgeLabelScale;
    }

}
