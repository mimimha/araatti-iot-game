using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배 협동 HUD 를 **분리형 에셋(V2) 으로 통째로 다시 짓는다.** (SHIPCOOP.md 9장)
///
/// 배치와 크기는 `art/hud/06-redesign-components/README.md` 의 권장값을 그대로 씁니다.
/// 1920 × 1080 기준이고, 그 숫자에 맞춰 에셋이 만들어졌기 때문입니다.
///
/// 금테 패널 한 벌(`art/hud/07-minigame-mockup/hud-transparent-assets`)로 갈아입혔습니다.
/// 패널에 아이콘과 빈 트랙이 **그려져 있으므로** 글자 · 숫자 · 게이지 채움만 얹습니다.
/// 늘리면 그 아이콘이 뭉개지니 9-slice 대신 **비율을 지켜** 놓습니다.
///
/// <code>
/// 배 HP       좌상                 228×90    hp-panel (하트가 그림에 그려져 있다) + 숫자
/// 시간        상단중앙             228×90    timer-panel (시계가 그림에 그려져 있다) + 숫자
/// 항해        좌하단     620×211    voyage-track-white-from-blue-hq(바탕) +
///                                  voyage-track-filled-blue-v3-hq(Filled, 진행률만큼)
/// 지연 경고   (지금은 안 띄웁니다. 항해 바 안의 붉은 구간이 대신 말해줍니다)
/// 사건 목록   우상단             547×122 씩, 간격 8    event-card-background + 픽토그램 5종
/// (팀원 4칸은 뺐습니다. 머리 위 이름표가 누가 어디 있는지를 대신 말해줍니다)
/// 침수        (바를 뺐습니다. 사건 카드 맨 윗줄로 갑니다)
/// 상호작용    화면 고정 자리 없음 — 사람 옆구리를 따라다니는 원형 버튼
///             (interaction-base + interaction-progress-fill, 키 글자는 TMP)
/// </code>
///
/// 침수 바는 뺐습니다. 게이지 대신 사건 카드 한 장으로 "퍼내라" 만 말합니다.
/// 단계(출항) 패널은 뺐습니다. phase-panel.png 는 지우지 않고 남겨 뒀습니다.
///
/// ⚠ **자식을 전부 새로 만듭니다.** 그래서 씬에 놓인 HUD 인스턴스의 override 가 끊어집니다.
///    확인해보니 지금 override 는 TMP 가 스스로 찍는 값(m_TextStyleHashCode)과
///    루트의 위치 · 크기뿐이라 잃어도 됩니다. 새로 붙인 오브젝트는 0개입니다.
///    **다시 돌리기 전에 그때도 그런지 확인하세요.**
///
/// 옛 <see cref="ShipCoopHudArt"/> 를 대신합니다. 그쪽은 옛 에셋 폴더를 봅니다.
///
/// Tools > 아라아띠 > 배 협동 HUD 를 V2 로 다시 짓기
/// </summary>
public static class ShipCoopHudV2Art
{
    private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHud.prefab";
    private const string ArtRoot = "Assets/Game/Art/UI/ShipCoopHudV2";

    /// <summary>
    /// 금테 패널 한 벌. (`art/hud/07-minigame-mockup/hud-transparent-assets`)
    ///
    /// ⚠ **패널에 아이콘과 빈 트랙이 이미 그려져 있습니다.** 하트 · 시계 · 돛 · 경고 ·
    ///    손 · 조타를 따로 얹으면 두 번 보입니다. 늘리면 그 아이콘과 금테가 뭉개지므로
    ///    9-slice 를 쓰지 않고 **비율을 지켜** 놓습니다. 글자 · 숫자 · 게이지 채움만 얹습니다.
    /// </summary>
    private const string ArtRootV3 = "Assets/Game/Art/UI/ShipCoopHudV3";

    /// <summary>
    /// 사건 카드 한 벌. (`art/hud/09-event-hud-mockup`)
    ///
    /// 배경 · 배지 · 타이머는 9-slice 로 늘려 쓰고, 배지와 타이머 채움은
    /// **흰색이라 런타임이 단계 색으로 물들입니다.** 그림 5종은 512 × 512 입니다.
    /// </summary>
    private const string ArtRootEvent = "Assets/Game/Art/UI/ShipCoopEventV1";

    // ------------------------------------------------------------------ 색
    // README 가 지정한 값. 바 색은 여기서만 바꾼다.

    private static readonly Color HpGreen = Hex(0x48D889);
    private static readonly Color VoyageBlue = Hex(0x64C9F1);
    private static readonly Color WarnOrange = Hex(0xFFBC55);
    private static readonly Color DangerRed = Hex(0xFF5864);
    private static readonly Color Cream = new Color(0.96f, 0.94f, 0.88f);

    /// <summary>밝은 배지 위에 올리는 글자. 흰 글자를 올리면 흰 바탕에 묻혀 안 보인다.</summary>
    private static readonly Color OnBadge = Hex(0x1C2A3A);

    // 사건 카드 글자색. `event-ui-assets/manifest.json` 이 지정한 값.
    private static readonly Color EventTitle = Hex(0xFFF0D0);
    private static readonly Color EventSecondary = Hex(0xB8D8E8);

    // ------------------------------------------------------------------ 배치
    //
    // 1920 × 1080 기준. 패널은 전부 원본 비율을 지키므로 **폭만 정하고 높이는 계산**한다.
    // 화면 가운데는 비워 둔다. 위쪽에 상태, 아래쪽에 사람과 상호작용만 둔다.

    private const float Margin = 28f;
    private const float TopMargin = 24f;

    // 체력 · 시간 — 하트 · 시계가 그림에 그려져 있다. 둘 다 알약 판 하나 + 글자만 얹는다.
    //
    // 두 그림 모두 알약 테두리에 딱 맞게 잘라 뒀다(실측 1250×494, 둘이 정확히 같은 크기 ·
    // 같은 자리라 이번엔 폭도 하나로 같이 쓴다). 두께(PillHeight)를 정하면 폭은 그 비율로 나온다.
    private const float PillHeight = 90f;
    private const float HpPillWidth = PillHeight * 1250f / 494f;     // ≈ 379.6
    private const float TimerPillWidth = HpPillWidth;

    // 왼쪽(하트 · 시계 그림)을 뺀 오른쪽 빈 자리에 글자를 놓는다. 크롭한 알약 자체를
    // 기준으로 잰 값이라(그림에 남는 투명 여백이 없다), 두 그림에 그대로 같이 쓴다.
    private const float PillLabelLeft = 0.40f;   // 판 폭의 이 비율부터 빈 칸이 시작한다.
    private const float PillLabelRight = 0.94f;  // 여기서 끝난다 (오른쪽 알약 테두리 안쪽).

    // 시계 폭. 왼쪽 위에 있던 출항 패널(320 × 86.25)과 **높이를 맞춰** 정한 값이다.
    // 출항 패널은 뺐지만 이 높이가 거리 바 · 체력과도 맞아 그대로 둔다.
    private const float TimerWidth = 301.09f;   // 384 × 110 → 높이 86.25
    private const float VoyageWidth = 620f;
    private const float WarningWidth = 400f;    // 768 × 123

    // ── 항해 트랙 — 흰색/파란색 두 장을 통째로 겹쳐 쓴다 ──
    //
    // voyage-track-white-from-blue-hq.png(항상 보이는 바탕) 과
    // voyage-track-filled-blue-v3-hq.png(진행률만큼 가로로 드러나는 채움)는 **캔버스
    // 전체(2152×731)를 그대로 담은, 픽셀 단위로 이미 정렬된 한 쌍**이다. 리사이즈 ·
    // 크롭 없이 두 장 다 Voyage 전체에 꽉 채운다(Stretch). 체크포인트 5개의 활성 색도
    // 파란색 그림 안에 이미 칠해져 있어 별도 원 오브젝트가 필요 없다 — 가로 Fill 이
    // 그 자리를 지나면 저절로 파랗게 드러난다.
    //
    // 실제 색이 채워지는 구간(항구·섬 배지를 뺀 안쪽)은 그림에서 X 359~1787 이다.
    // 배 마커 · 기준선 · 지연 칸은 예전처럼 이 구간만 감싼 `Track` 자식 위에서 움직이고
    // (ShipCoopHud.PlaceOnBar 가 진행률 0~1 을 그대로 쓴다), 정작 fillAmount 변환은
    // ShipCoopHud.ToVoyageFillAmount 가 맡는다 — 캔버스 전체를 덮는 그림이라 진행률을
    // 그대로 fillAmount 에 넣으면 트랙 앞(0~359)만큼 일찍 끝난 것처럼 보이기 때문이다.
    private const float VoyageImgW = 2152f;
    private const float VoyageImgH = 731f;
    private const float VoyageTrackStartX = 359f;
    private const float VoyageTrackEndX = 1787f;

    // ⚠ **이미지 위쪽 기준(top-down)으로 잰 값이다(그림에서 자로 잰 그대로).** Unity
    //    RectTransform 로컬 Y 는 위가 +, 아래가 − 라서(bottom-up), 그대로 빼면 부호가
    //    뒤집힌다 — `trackCenterY` 를 만들 때 반드시 `VoyageImgH*0.5f - VoyageTrackY`
    //    순서로 뺀다(반대로 하면 위/아래가 바뀐다). Unity 쪽(bottom-up) 값으로 보면
    //    731 − 359 ≈ 372 다.
    private const float VoyageTrackY = 359f;
    private const float VoyageTrackThickness = 56f;

    // ⚠ **트랙 두께(56)와 거의 같은 크기였다.** 화면 크기 비율(voyageScale ≈ 0.29)을 곱하면
    //    실제로는 13px 밖에 안 돼서 — 새 배 마커 그림으로 바꿔도 **거의 안 보였다.** 눈에
    //    띄는 "지금 여기" 배지가 되도록 트랙 두께의 5배 이상으로 키운다(300 ≈ 화면에서 86px).
    private const float VoyageShipMarkerDisplaySize = 300f;

    // ── 늦었다는 알림(임시) ──
    //
    // 트랙 위에 서는 작은 팻말. 그림 없이 TMP 글씨 + 삼각형만으로 세운다.
    // 여기 숫자는 **화면 px 그대로**다 — Track 의 자식이고, Track 은 이미
    // voyageScale 을 곱해 만들어 두었으므로 여기서 또 곱하면 안 된다.
    private const float HurryWidth = 200f;
    private const float HurryHeight = 58f;

    /// <summary>트랙 한가운데에서 위로 이만큼 띄운다. 배 마커와 겹치지 않을 만큼.</summary>
    private const float HurryLift = 12f;

    private const float HurryArrowSize = 20f;

    /// <summary>묶음 아래에서 글자 **가운데**까지. 삼각형(0~20)과 까딱임 폭(±4)을 비운 값.</summary>
    private const float HurryTextBottom = 40f;
    private const float HurryTextHeight = 30f;
    private const float HurryFontSize = 20f;

    // ── 사건 카드 ──
    //
    // 배경이 9-slice(테두리 64) 라서 원본 비율을 지킬 필요가 없다. 읽기 좋은 크기로 정한다.
    //
    // **왼쪽 위에서 상단 가운데로 옮기면서 가로로 늘렸다.** 화면 한복판 위라 양옆이
    // 비어 있어서, 글줄이 줄바꿈 없이 한 줄로 읽힌다.

    /// <summary>
    /// 카드 한 벌을 통째로 줄이고 키우는 값. **여기 하나만 만지면 됩니다.**
    ///
    /// 폭 · 높이 · 줄간격은 물론 글자 크기까지 이 값을 곱합니다. 안쪽 배치(그림 · 제목 ·
    /// 보조 문구 · 배지 · 타이머)는 전부 폭과 높이의 **비율**로 잡혀 있어서, 이것만
    /// 바꾸면 모양이 그대로인 채 크기만 달라집니다.
    ///
    /// 1.0 이 가운데로 옮기면서 정한 크기(760 × 126)이고, 지금은 그 80% 입니다.
    /// 가로 · 세로는 <see cref="EventCardWidthScale"/> · <see cref="EventCardHeightScale"/>
    /// 로 한 번 더 손봅니다 (최종 547 × 122).
    /// </summary>
    private const float EventCardScale = 0.8f;

    /// <summary>
    /// **가로만** 따로 좁히는 값. 세로 · 글자 크기는 안 건드립니다.
    ///
    /// 0.7 까지 좁혔다가 1.0 으로 되돌렸고, 지금은 0.9 입니다. 가로로 너무 길어서
    /// 오른쪽 위 구석에서 화면을 가로로 훑고 지나가는 것처럼 보였습니다.
    ///
    /// ⚠ 다시 좁히려면 <see cref="BuildEventRow"/> 의 안쪽 배치가
    ///    **그림 · 배지는 세로 기준, 글줄은 남는 자리**로 잡혀 있는지 확인하세요.
    ///    예전처럼 전부 폭의 비율로 잡으면 좁힐 때 그림이 글자를 파고듭니다.
    /// </summary>
    private const float EventCardWidthScale = 0.9f;

    /// <summary>
    /// **세로만** 따로 늘리는 값.
    ///
    /// 가로를 줄인 만큼 카드가 납작해 보여서 세로로 폈습니다. 10% 씩 두 번 — 1.21 입니다.
    /// 안쪽에서 세로를 기준으로 재는 것들(<c>Pad</c> · 그림 · <c>Gap</c> · 배지)이
    /// **이 값을 따라 같이 커집니다.**
    /// </summary>
    private const float EventCardHeightScale = 1.21f;

    /// <summary>
    /// 픽토그램이 카드 높이의 몇 할을 쓰는가.
    ///
    /// 0.58 에서 키웠습니다. 0.68 이면 위아래로 딱 <c>Pad</c>(0.16) 만큼만 남으므로
    /// **이 이상 키우면 카드 테두리에 닿습니다.**
    /// </summary>
    private const float EventIconRatio = 0.68f;

    private const float EventCardWidth = 760f * EventCardScale * EventCardWidthScale;
    private const float EventCardHeight = 126f * EventCardScale * EventCardHeightScale;
    private const float EventCardGap = 10f * EventCardScale;

    /// <summary>
    /// 첫 카드가 시작하는 높이. 오른쪽 위 구석은 **위에 아무것도 없다** — 체력은 왼쪽,
    /// 시간은 가운데다. 그래서 그 둘과 같은 선(TopMargin)에서 바로 시작한다.
    /// 세 칸이 화면 맨 윗줄을 왼쪽 · 가운데 · 오른쪽으로 나눠 갖는다.
    /// </summary>
    private const float EventListTop = TopMargin;

    /// <summary>
    /// 상호작용 패널을 그림 원본(424 × 150) 의 몇 배로 띄우는가.
    ///
    /// 원본 크기로는 글자와 키캡이 작아서 눈에 잘 안 들어왔습니다. 1.2 배면
    /// 화면 오른쪽 아래가 답답해지지 않으면서 문구가 편하게 읽힙니다.
    /// 안쪽 배치 값도 전부 이 값을 곱하므로 **여기 하나만 바꾸면 통째로 커집니다.**
    /// </summary>
    private const float ActionScale = 1.2f;

    // ── 상호작용 링(painterly) ──
    //
    // 링 칸의 한 변. 그림 두 장(interaction-ring-painterly · interaction-progress-painterly)이
    // 정사각형 1254×1254 라서 여기도 정사각형이다.
    private const float RingSize = 104f;

    /// <summary>
    /// 키 글자 칸이 링 칸의 몇 할을 쓰는가.
    ///
    /// 도넛(interaction-progress-painterly)의 안쪽 구멍이 그림 칸의 66.5% 다. 글자가 테두리에
    /// 닿지 않도록 그보다 조금 안쪽인 0.60 으로 잡는다.
    /// </summary>
    private const float KeyInset = 0.60f;

    /// <summary>키 글자 칸의 높이 비율. 글자는 한 줄이라 폭만큼 높을 필요가 없다.</summary>
    private const float KeyLineHeight = 0.38f;

    /// <summary>키 글자를 이 배로 줄인다. 1.0 이 도넛 구멍에 꽉 찼을 때의 크기다.</summary>
    private const float KeyFontScale = 0.8f;

    // 자동 크기 범위. "K" 는 위까지 커지고 "Space" · "J · L" 은 칸에 맞춰 줄어든다.
    private const float KeyFontMin = 14f * KeyFontScale;
    private const float KeyFontMax = 30f * KeyFontScale;

    /// <summary>
    /// 링 안 키캡 글꼴. **본문(NotoSansKR)과 일부러 다르게 쓴다.**
    ///
    /// 여기 찍히는 것은 <c>Space</c> · <c>K</c> · <c>J · L</c> 뿐이라 한글이 필요 없습니다.
    /// 키캡은 읽는 "글" 이 아니라 **버튼에 새긴 각인**이라, 본문과 같은 글꼴일 이유가
    /// 없습니다. 둥근 링(interaction-ring-painterly)에 맞춰 **둥근 글꼴(Fredoka)** 을 씁니다.
    ///
    /// 만화체(Bangers)를 먼저 대 봤다가 되돌렸습니다. 모서리가 뾰족하고 세로로 눌린
    /// 글꼴이라 동그란 링과 따로 놀았습니다.
    ///
    /// ⚠ **<see cref="ShipCoopKeyFontInstaller"/> 가 구워 둔 에셋입니다.** 없으면
    ///    경고만 남기고 쓰던 글꼴로 넘어갑니다 — 글자가 안 보이는 것보다 낫습니다.
    ///    그 메뉴(배 협동 키캡 글꼴 굽기)를 한 번 돌리면 만들어집니다.
    /// </summary>
    private const string KeyFontPath = ShipCoopKeyFontInstaller.OutputPath;

    private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

    private const int EventRowCount = 4;

    private static TMP_FontAsset _font;

    [MenuItem("Tools/아라아띠/배 협동 HUD 를 V2 로 다시 짓기")]
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            var hud = root.GetComponent<ShipCoopHud>();
            if (hud == null)
            {
                throw new MissingComponentException($"{PrefabPath} 에 ShipCoopHud 가 없다.");
            }

            // 글꼴은 쓰던 것을 그대로 이어받는다. 한글이 깨지지 않게.
            TextMeshProUGUI sample = root.GetComponentInChildren<TextMeshProUGUI>(true);
            _font = sample != null ? sample.font : null;

            var canvas = (RectTransform)root.transform;

            for (int i = canvas.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(canvas.GetChild(i).gameObject);
            }

            var wired = new Wiring();

            // ⚠ **맨 먼저 짓습니다.** 화면 전체를 덮는 비네트라, 나중에 지으면
            //    형제 순서상 위로 올라와 HUD 를 붉게 덮습니다.
            BuildCourseWarning(canvas, wired);

            BuildTopBars(canvas, wired);
            BuildEventList(canvas, wired);
            BuildAction(canvas, wired);

            Connect(hud, wired);
            SyncTextColors(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[HUD V2] 전부 다시 짓고 저장했다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HUD V2] 실패: {e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>만들면서 모아둔 연결 대상. 마지막에 한 번에 꽂는다.</summary>
    private class Wiring
    {
        public Image HpFill;
        public TextMeshProUGUI HpLabel;
        public TextMeshProUGUI TimeLabel;
        public Image ProgressFill;
        public RectTransform ExpectedMarker;
        public RectTransform DelayFill;
        public RectTransform ShipMarker;
        public GameObject HurryCallout;
        public CanvasGroup HurryGroup;
        public TextMeshProUGUI HurryLabel;
        public RectTransform HurryArrow;
        public GameObject BehindWarning;
        public GameObject CourseWarningRoot;
        public Image CourseWarningVignette;
        public TextMeshProUGUI CourseWarningLabel;
        public GameObject InteractPanel;
        public TextMeshProUGUI InteractLabel;
        public Image InteractGauge;
        public Image InteractIcon;
        public TextMeshProUGUI InteractKeycap;
        public ShipCoopHud.EventRow[] EventRows;
    }

    // ------------------------------------------------------------------ 항로 이탈 경고

    /// <summary>
    /// 뱃머리가 틀어졌을 때 화면 가장자리를 붉게 물들이는 경고.
    ///
    /// 가운데는 비워 둡니다. 배와 갑판을 가리면 정작 조타를 잡으러 갈 수가 없습니다.
    /// 그래서 테두리만 물들이고, 문구는 아래쪽 침수 게이지 위에 둡니다.
    ///
    /// 진하기와 깜빡임은 <see cref="ShipCoopHud"/> 가 매 프레임 넣습니다.
    /// 여기서는 **꺼진 상태로** 만들어 둡니다.
    /// </summary>
    private static void BuildCourseWarning(RectTransform canvas, Wiring wired)
    {
        RectTransform group = Rect("CourseWarning", canvas);
        Stretch(group, 0f, 0f, 0f, 0f);

        RectTransform edge = Rect("Vignette", group);
        Stretch(edge, 0f, 0f, 0f, 0f);
        wired.CourseWarningVignette = Img(edge, ShipCoopVignetteArt.Ensure(),
                                          new Color(DangerRed.r, DangerRed.g, DangerRed.b, 0f));

        // 문구는 아래쪽. 침수 게이지(높이 28~122) 위에 얹되 겹치지 않게 띄운다.
        // 글자는 **크림색**이다. 붉은 테두리 위에 붉은 글씨를 올리면 서로 묻힌다.
        // 위험하다는 말은 테두리 색이 이미 하고 있으므로, 글자는 읽히기만 하면 된다.
        TextMeshProUGUI label = Text("Label", group, "", 30f, TextAlignmentOptions.Center);
        Place((RectTransform)label.transform, BottomCenter, new Vector2(0f, 196f), new Vector2(760f, 84f));
        label.color = Cream;
        label.fontStyle = FontStyles.Bold;
        wired.CourseWarningLabel = label;

        group.gameObject.SetActive(false);
        wired.CourseWarningRoot = group.gameObject;
    }

    // ------------------------------------------------------------------ 상단 바

    /// <summary>
    /// HP · 시간 · 항해.
    ///
    /// HP는 왼쪽 위(하트 + 숫자, 사건 목록 바로 위), 시간은 위쪽 가운데, 항해는 왼쪽 아래.
    /// </summary>
    private static void BuildTopBars(RectTransform canvas, Wiring wired)
    {
        float voyageHeight = VoyageWidth * VoyageImgH / VoyageImgW;

        // ── 배 HP — **왼쪽 위, 새 알약 판(하트가 그림에 그려져 있다)** ─────────
        //
        // hp-panel.png 가 판과 하트를 한 장으로 그린다. 그래서 그림 하나 + 글자만
        // 얹는다. 사건 목록은 상단 가운데로 갔으므로 이 구석은 이제 체력 혼자 쓴다.
        RectTransform hp = Rect("Hp", canvas);
        Place(hp, TopLeft,
              new Vector2(Margin + HpPillWidth * 0.5f, -(TopMargin + PillHeight * 0.5f)),
              new Vector2(HpPillWidth, PillHeight));
        Panel(hp, "hp-panel");

        wired.HpLabel = Text("Label", hp, "100", 28f, TextAlignmentOptions.Center);
        PlacePillLabel((RectTransform)wired.HpLabel.transform, HpPillWidth);
        Shrink(wired.HpLabel, 20f, 28f);

        // 막대와 함께 있던 채움(hp-fill-green)은 뺐다. `hpFill` 은 비워 둔다.
        // 런타임 UpdateHealth 가 null 이면 그냥 넘어간다.

        // ── 시간 — **위쪽 가운데, 같은 알약 판(시계가 그림에 그려져 있다)** ────
        //
        // timer-panel.png 는 hp-panel.png 와 크롭한 크기까지 똑같아서(실측 1250×494)
        // 폭도 그대로 같이 쓴다(TimerPillWidth = HpPillWidth).
        // 왼쪽 위는 체력이, 오른쪽 위는 사건 목록이, 아래는 항해 바가 맡는다. 시간은
        // 가장 눈에 잘 들어오는 한가운데 맨 위에 홀로 둔다.
        RectTransform time = Rect("Time", canvas);
        Place(time, TopCenter,
              new Vector2(0f, -(TopMargin + PillHeight * 0.5f)),
              new Vector2(TimerPillWidth, PillHeight));
        Panel(time, "timer-panel");

        wired.TimeLabel = Text("Label", time, "3:00", 28f, TextAlignmentOptions.Center);
        PlacePillLabel((RectTransform)wired.TimeLabel.transform, TimerPillWidth);
        Shrink(wired.TimeLabel, 20f, 28f);

        // ── 항해 ─────────────────────────────
        //
        // 흰색(항상 보임) · 파란색(진행률만큼 드러남) 두 장을 Voyage 전체에 통째로 겹친다.
        // 두 장 다 픽셀 단위로 이미 정렬돼 있으므로 **똑같은 RectTransform** (Stretch,
        // anchorMin (0,0) ~ anchorMax (1,1))을 쓴다 — 하나라도 어긋나면 성 · 섬 · 금테가
        // 이중으로 보이거나 흔들린다.
        float voyageScale = VoyageWidth / VoyageImgW;

        // 왼쪽 아래. 위쪽은 체력 · 시간 · 사건 목록이 이미 차 있고, 아래쪽 왼편은
        // 비어 있었다 — 상호작용 배지는 이제 화면 고정 자리가 아니라 사람을 따라다닌다.
        RectTransform voyage = Rect("Voyage", canvas);
        Place(voyage, BottomLeft,
              new Vector2(Margin + VoyageWidth * 0.5f, Margin + voyageHeight * 0.5f),
              new Vector2(VoyageWidth, voyageHeight));

        // 흰색 바탕 — 늘 보인다. preserveAspect 는 켜지 않는다(파란 레이어와 반드시 같아야
        // 하고, 어차피 Voyage 자체가 원본과 같은 비율(voyageHeight)이라 켜도 안 켜도 결과가
        // 같다 — 다만 "같은 설정"이라는 사실 자체가 중요하다).
        RectTransform trackWhite = Rect("TrackWhiteBase", voyage);
        Stretch(trackWhite, 0f, 0f, 0f, 0f);
        Img(trackWhite, Sprite3("voyage-track-white-from-blue-hq"), Color.white);

        // 파란색 채움 — 흰색과 완전히 같은 RectTransform, 그 바로 위(다음 형제라 위에 그려짐).
        RectTransform trackBlue = Rect("TrackBlueFill", voyage);
        Stretch(trackBlue, 0f, 0f, 0f, 0f);
        Image blueFill = Img(trackBlue, Sprite3("voyage-track-filled-blue-v3-hq"), Color.white);
        blueFill.type = Image.Type.Filled;
        blueFill.fillMethod = Image.FillMethod.Horizontal;
        blueFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        blueFill.fillClockwise = true;
        blueFill.fillAmount = 0f;
        wired.ProgressFill = blueFill;

        // 배 마커 · 기준선 · 지연 칸은 그림 전체가 아니라 **실제 트랙 구간(X 359~1787)만
        // 감싼 자리** 위에서 0~1 로 움직인다(ShipCoopHud.PlaceOnBar). 흰색/파란색 레이어와는
        // 달리 화면에 안 보이는 자리 계산용 RectTransform 이라 그림을 얹지 않는다.
        RectTransform track = Rect("Track", voyage);
        float trackCenterX = (VoyageTrackStartX + VoyageTrackEndX) * 0.5f - VoyageImgW * 0.5f;
        // ⚠ Y는 위가 + 인 Unity 로컬 좌표고 VoyageTrackY 는 이미지 위쪽 기준이라 부호가
        //    반대다. VoyageImgH*0.5f 에서 빼야 맞는다(위 상수 주석 참고).
        float trackCenterY = VoyageImgH * 0.5f - VoyageTrackY;
        Place(track, Half,
              new Vector2(trackCenterX, trackCenterY) * voyageScale,
              new Vector2(VoyageTrackEndX - VoyageTrackStartX, VoyageTrackThickness) * voyageScale);

        // 늦은 만큼의 구간. 배 위치에서 기준선까지 폭만 늘어난다. (README)
        RectTransform delay = Rect("Delay", track);
        delay.anchorMin = new Vector2(0f, 0.5f);
        delay.anchorMax = new Vector2(0f, 0.5f);
        delay.pivot = new Vector2(0f, 0.5f);
        delay.anchoredPosition = Vector2.zero;
        delay.sizeDelta = new Vector2(0f, VoyageTrackThickness * voyageScale * 0.4f);

        // **채움처럼 보이면 안 된다.** 이건 "여기부터 저기까지 늦었다" 는 구간 표시다.
        // 진하게 칠하면 진행도 채움과 섞여서 무엇을 보는 건지 알 수 없다.
        Img(delay, Sprite("bar-delay"), new Color(1f, 0.35f, 0.42f, 0.45f));
        wired.DelayFill = delay;

        // 지금 있어야 할 위치(기준선). 늦었는지는 이 선과 배 마커 사이 거리로 보인다.
        wired.ExpectedMarker = Marker(track, "Expected", Sprite("voyage-reference"),
                                      new Vector2(8f, VoyageTrackThickness) * voyageScale, Cream);

        // 배 마커 — 진행도에 따라 트랙 위를 움직인다. (ShipCoopHud.PlaceOnBar)
        wired.ShipMarker = Marker(track, "ShipMarker", Sprite3("voyage-ship-marker"),
                                  Vector2.one * VoyageShipMarkerDisplaySize * voyageScale, Color.white);

        // 늦었다는 알림(임시) — 붉은 구간 위에 떴다 사라지는 팻말.
        // 왜 이게 필요한지와 임시인 이유는 BuildHurryCallout 에 적어 두었습니다.
        wired.HurryCallout = BuildHurryCallout(track, out CanvasGroup hurryGroup,
                                               out TextMeshProUGUI hurryLabel,
                                               out RectTransform hurryArrow).gameObject;
        wired.HurryGroup = hurryGroup;
        wired.HurryLabel = hurryLabel;
        wired.HurryArrow = hurryArrow;

        // ── 늦고 있다는 경고 — 지금은 안 짓습니다 ──
        //
        // `warning-panel` 로 항해 바 아래에 띄웠던 "이 속도면 늦는다!" 를 뺐습니다.
        //
        // ⚠ **늦고 있다는 신호가 사라진 것은 아닙니다.** 항해 바 안의 붉은 구간(`Delay`)이
        //    배와 기준선 사이만큼 벌어지므로 뒤처진 정도는 그대로 보입니다.
        //    다만 글자로 말해주지는 않습니다. 숫자만으로는 늦은 줄 모른다는 것이
        //    원래 이 경고를 만든 이유였으니(9장), 다시 필요해지면 되살리세요.
        //
        // 되살리려면 여기서 `warning-panel` 패널을 짓고 글자를 얹은 뒤
        // `wired.BehindWarning` 에 넣으면 됩니다. 런타임(`behindWarning`)은 그대로 있고,
        // 비어 있어도 그냥 넘어갑니다. 그림도 `ShipCoopHudV3/panels` 에 그대로 둡니다.
    }

    // ------------------------------------------------------------------ 사건 목록

    /// <summary>
    /// **오른쪽 위.** 이 게임에서 가장 중요한 UI 다. (9장)
    ///
    /// 왼쪽 위 → 상단 가운데를 거쳐 오른쪽 위로 왔습니다. 가운데는 눈에는 잘 들어오지만
    /// **갑판 한복판을 가로로 덮어서** 정작 일하러 갈 자리가 안 보였습니다. 오른쪽 위는
    /// 체력(왼쪽) · 시간(가운데) 과 한 줄을 나눠 쓰면서 갑판을 비워 둡니다.
    /// 카드는 위에서부터 채우고 남는 줄은 꺼지므로(ShipCoopHud.UpdateEvents),
    /// 보통 한두 장만 뜹니다.
    ///
    /// 목업(`art/hud/09-event-hud-mockup`)대로 한 장을 이렇게 나눕니다.
    /// <code>
    /// [ 그림 ]  제목                    [예고/발생]
    ///           보조 문구 · 갑판
    ///           ▬▬▬▬▬▬▬▬▬ 타이머
    /// </code>
    ///
    /// 남은 초(숫자)는 뺐습니다. 타이머 바가 같은 말을 하고 있었습니다.
    ///
    /// 카드 배경은 9-slice(테두리 64)라 크기를 자유롭게 정합니다.
    /// 배지와 타이머 채움은 **흰색 에셋**이고 런타임이 단계 색으로 물들입니다.
    /// </summary>
    private static void BuildEventList(RectTransform canvas, Wiring wired)
    {
        RectTransform group = Rect("Events", canvas);
        Place(group, TopRight,
              new Vector2(-(Margin + EventCardWidth * 0.5f), -(EventListTop + EventCardHeight * 0.5f)),
              new Vector2(EventCardWidth, EventCardHeight));

        wired.EventRows = new ShipCoopHud.EventRow[EventRowCount];

        for (int i = 0; i < EventRowCount; i++)
        {
            wired.EventRows[i] = BuildEventRow(group, i);
        }
    }

    private static ShipCoopHud.EventRow BuildEventRow(RectTransform group, int index)
    {
        const float W = EventCardWidth;
        const float H = EventCardHeight;

        // ⚠ **그림과 배지는 세로(H) 기준으로 재고, 글줄이 남는 자리를 쓴다.**
        //
        //    예전에는 셋 다 폭(W)의 비율로 잡혀 있었다. 그러면 카드를 좁힐 때
        //    그림은 (세로 기준이라) 크기가 그대로인데 자리만 안으로 밀려와
        //    **글자를 파고든다.** 실제로 30% 줄였을 때 틈이 0 이 됐다.
        const float Pad = H * 0.16f;        // 카드 테두리 안쪽 여백
        const float IconSize = H * EventIconRatio;
        const float Gap = H * 0.12f;        // 그림 · 글줄 · 배지 사이 틈
        const float BadgeWidth = H * 0.62f;

        const float IconCenter = -W * 0.5f + Pad + IconSize * 0.5f;
        const float BadgeCenter = W * 0.5f - Pad - BadgeWidth * 0.5f;

        // 제목 · 보조 문구 · 타이머가 **같은 선에서 시작해 같은 선에서 끝난다.**
        // 제각각이면 카드가 들쭉날쭉해 보이고, 눈이 줄마다 자리를 다시 찾아야 한다.
        const float TextLeft = -W * 0.5f + Pad + IconSize + Gap;
        const float TextRight = BadgeCenter - BadgeWidth * 0.5f - Gap;
        const float TextWidth = TextRight - TextLeft;

        RectTransform card = Rect($"Row_{index + 1}", group);
        Place(card, Half, new Vector2(0f, -index * (H + EventCardGap)), new Vector2(W, H));

        Image back = Img(card, Sprite4("event-card-background"), new Color(1f, 1f, 1f, 0.95f));
        back.type = Image.Type.Sliced;

        // ⚠ **9-slice 테두리를 줄여야 한다.** 원본 테두리가 64px 인데 카드 높이가 126 이라,
        //    그대로 두면 위아래 테두리(64+64=128)가 카드보다 커져서 **알약 모양**이 된다.
        //    `pixelsPerUnitMultiplier` 는 테두리를 그 배수만큼 얇게 그린다. 3 이면 약 21px.
        back.pixelsPerUnitMultiplier = 3f;

        // ── 그림 ─────────────────────────────
        Image icon = Icon(card, "Icon", null, new Vector2(IconCenter, 0f), IconSize);
        icon.color = Color.white;   // 사건 그림은 채색이라 크림색을 곱하면 탁해진다

        // ── 제목 ─────────────────────────────
        //
        // 남은 초를 뺀 자리까지 가져와 **배지 바로 앞까지** 넓게 쓴다. 왼쪽 정렬이라
        // 넓혀도 글자 시작점은 그대로고, 긴 사건 이름이 줄어들지 않고 다 나온다.
        TextMeshProUGUI title = Text("Title", card, "사건", 27f * EventCardScale, TextAlignmentOptions.Left);
        Place((RectTransform)title.transform, Half,
              new Vector2(TextLeft + TextWidth * 0.5f, H * 0.23f), new Vector2(TextWidth, H * 0.30f));
        title.color = EventTitle;
        Shrink(title, 18f * EventCardScale, 27f * EventCardScale);

        // ── 보조 문구 · 갑판 ──────────────────
        TextMeshProUGUI label = Text("Label", card, "", 19f * EventCardScale, TextAlignmentOptions.Left);
        Place((RectTransform)label.transform, Half,
              new Vector2(TextLeft + TextWidth * 0.5f, 0f), new Vector2(TextWidth, H * 0.24f));
        label.color = EventSecondary;
        Shrink(label, 14f * EventCardScale, 19f * EventCardScale);

        // ── 남은 초는 뺐다 ───────────────────
        //
        // 같은 것을 두 번 말하고 있었다. 아래 타이머 바가 남은 시간을 이미 보여주고,
        // 급한지 아닌지는 바가 줄어드는 속도와 색이 말한다 — 숫자는 읽어야 알지만
        // 바는 흘끗 봐도 안다. 정확한 초가 다시 필요해지면 여기에 되살리면 된다
        // (런타임의 `EventRow.seconds` 는 남겨 뒀고, 비어 있으면 그냥 넘어간다).

        // ── 예고 / 발생 배지 ─────────────────
        //
        // 색만으로 단계를 말하면 색각 이상이 있을 때 안 읽힌다. 글자를 같이 둔다.
        RectTransform badge = Rect("Badge", card);
        Place(badge, Half, new Vector2(BadgeCenter, H * 0.23f), new Vector2(BadgeWidth, H * 0.26f));
        Image badgeImage = Img(badge, Sprite4("event-state-badge"), WarnOrange);
        badgeImage.type = Image.Type.Sliced;
        badgeImage.pixelsPerUnitMultiplier = 3f;

        TextMeshProUGUI badgeLabel = Text("Label", badge, "예고", 15f * EventCardScale, TextAlignmentOptions.Center);
        badgeLabel.color = OnBadge;
        badgeLabel.fontStyle = FontStyles.Bold;
        Shrink(badgeLabel, 10f * EventCardScale, 15f * EventCardScale);

        // ── 타이머 ───────────────────────────
        //
        // 셀 것이 없는 사건(선체 파손)에서는 바탕과 채움을 **둘 다** 끈다.
        // 채움만 끄면 빈 홈이 남아서 "0 초 남았다" 로 읽힌다.
        // 글 두 줄과 **같은 칸**을 쓴다. 한 선에서 시작하고 한 선에서 끝나므로
        // 바가 얼마나 줄었는지가 글 시작점과 견줘 읽힌다.
        RectTransform track = Rect("Timer", card);
        Place(track, Half, new Vector2(TextLeft + TextWidth * 0.5f, -H * 0.31f),
              new Vector2(TextWidth, H * 0.09f));

        Image trackImage = Img(track, Sprite4("event-timer-track"), Color.white);
        trackImage.type = Image.Type.Sliced;
        trackImage.pixelsPerUnitMultiplier = 4f;

        RectTransform fillRect = Rect("Fill", track);
        Stretch(fillRect, 0f, 0f, 0f, 0f);
        Image timer = Img(fillRect, Sprite4("event-timer-fill"), WarnOrange);
        timer.type = Image.Type.Filled;
        timer.fillMethod = Image.FillMethod.Horizontal;
        timer.fillOrigin = (int)Image.OriginHorizontal.Left;
        timer.fillAmount = 1f;

        // 옛 왼쪽 경고선은 배지가 대신한다. 같은 것을 두 번 말하지 않는다.
        card.gameObject.SetActive(false);

        return new ShipCoopHud.EventRow
        {
            root = card.gameObject,
            title = title,
            label = label,
            badge = badgeImage,
            badgeLabel = badgeLabel,
            timer = timer,
            timerTrack = track.gameObject,
            icon = icon,
        };
    }

    // ------------------------------------------------------------------ 현재 작업

    /// <summary>
    /// **원형 진행 링**과 키 표시. 화면 한 자리에 고정되지 않고, 지금 붙어 있거나
    /// 다가간 대상(자리 · 상자 · 짐) 머리 위로 매 프레임 떠서 이동한다.
    ///
    /// 붙박이였던 오른쪽 아래 자리는 뺐다. 사건 알림처럼 "그 일이 벌어지는 자리 옆"에
    /// 뜨는 쪽이, 여러 사람이 동시에 서로 다른 자리에 붙는 이 게임에서 더 잘 읽힌다
    /// (조타에 붙은 사람 화면에 대포 게이지가 뜬 것 같은 혼동이 없다).
    ///
    /// 실제 화면 위치는 <c>ShipCoopHud.LateUpdate</c> 가
    /// <c>Camera.main.WorldToScreenPoint</c> 로 매겨 이 그룹의 <c>anchoredPosition</c> 에
    /// 꽂는다. 그래서 여기서는 **자리만 원점(0,0)에 잡아 둔다** — 피벗을 아래쪽 가운데로
    /// 둬서, 꽂히는 그 점이 배지의 바닥이 되어 대상 위로 떠 보이게 한다.
    ///
    /// 손이 그려진 가로 막대(`interaction-panel`)로 갔다가 링으로 돌아왔다.
    /// 가로 막대는 조타 · 돛처럼 **양쪽으로 차는 게이지를 표현하지 못한다.**
    /// 링은 위(중앙)에서 좌우로 갈라 채울 수 있어 어느 쪽으로 꺾였는지가 그대로 보인다.
    ///
    /// 아래 숫자는 전부 **가로 424 × 150 짜리 보이지 않는 칸을 기준으로 잰 값**이고,
    /// <see cref="ActionScale"/> 만 곱해 한 번에 키운다. 이 칸 자체는 그림이 없다 —
    /// 안에 든 링 · 글자만 보인다. 크기를 다시 바꾸고 싶으면 그 상수 하나만 만지면 된다.
    /// </summary>
    private static void BuildAction(RectTransform canvas, Wiring wired)
    {
        const float W = 424f * ActionScale;
        const float H = 150f * ActionScale;

        RectTransform group = Rect("InteractPanel", canvas);
        Place(group, Half, Vector2.zero, new Vector2(W, H));
        group.pivot = new Vector2(0.5f, 0f);

        // ⚠ **네모 판(panel-action-plaque)도, 링 안의 작업 그림(icon-helm 등)도 없다.**
        //    이 링 자체가 "누를 버튼" 이다. 안에는 지금 누를 키만 적힌다 — K 면 K, Space 면
        //    Space. 무슨 작업인지는 옆 글자(Label)가 말하므로, 그림으로 또 말할 필요가 없다.

        // ── 원형 버튼(진행 링) ─────────────────────
        //
        // 뼈대만 여기서 세우고, 어떤 그림을 쓰고 글자 칸을 어떻게 잡을지는
        // DressInteractRing 이 정한다 — 프리팹을 통째로 다시 짓지 않고 그림만
        // 갈아끼울 수 있게 갈라 두었다. (ShipCoopInteractRingPatch)
        RectTransform ring = Rect("Ring", group);
        Place(ring, Half, new Vector2(-120f, 0f) * ActionScale, new Vector2(RingSize, RingSize) * ActionScale);
        Image badge = Img(ring, null, Color.white);

        RectTransform ringFill = Rect("Fill", ring);
        Stretch(ringFill, 0f, 0f, 0f, 0f);
        Image gauge = Img(ringFill, null, Color.white);

        // 채움 정도는 런타임(ShipCoopHud.UpdateInteract)이 매 프레임 넣는다. 처음엔 비어 있다.
        gauge.fillAmount = 0f;
        wired.InteractGauge = gauge;

        // ⚠ **키 글자를 반드시 연결한다.** 여기서 자식을 새로 만들기 때문에
        //    연결하지 않으면 예전처럼 무엇을 하든 빈칸으로 굳는다.
        TextMeshProUGUI keyLabel = Text("Key", ring, "Space", KeyFontMax * ActionScale,
                                        TextAlignmentOptions.Center);
        wired.InteractKeycap = keyLabel;

        DressInteractRing(badge, gauge, keyLabel);

        // ── 안내 문구 ────────────────────
        //
        // 링 오른쪽 끝이 -68 이다. 그 옆부터 안내 문구가 시작한다.
        const float TextLeft = -40f;
        const float TextWidth = 216f;

        // 링 쪽으로 더 당기는 양. 10 → 20 → 30 으로 두 번 늘렸다.
        // **ActionScale 을 곱하지 않는다** — 화면에서 눈으로 재서 정한 값이다.
        const float LabelPullLeft = 30f;

        // 안내 문구 크기. 원래 21 → 80% 로 줄였다가(16.8) 다시 6pt 올렸다.
        // 아래 보조 문구는 <size=60%> 라 이 값을 따라 같이 움직인다.
        const float LabelFontSize = 22.8f;

        // ⚠ 세로는 **TopLeft 가 아니라 Left** 다. 안내 문구는 한 줄일 때도 세 줄일 때도
        //    있는데, 위로 붙여 두면 한 줄일 때 아래에 빈 띠가 크게 남는다.
        //    가운데 정렬로 두면 줄 수가 바뀌어도 덩어리가 제자리에 있다.
        //
        // y 는 0 — 링과 같은 높이다. 예전 19 는 아래에 "길게 누르세요" 를 두려고
        // 올려놨던 값이라, 그 줄이 빠진 지금 그대로 두면 글자만 붕 떠 보인다.
        //
        // 칸 높이는 66 → 88. 가장 긴 안내(자재를 들고 갈 때)가 **세 줄**이라 66 으로는
        // 마지막 줄이 칸 밖으로 밀렸다. 홀드 안내가 빠지면서 아래가 비었으니 그만큼 쓴다.
        wired.InteractLabel = Text("Label", group, "", LabelFontSize * ActionScale,
                                   TextAlignmentOptions.Left);
        Place((RectTransform)wired.InteractLabel.transform, Half,
              new Vector2(TextLeft + TextWidth * 0.5f, 0f) * ActionScale + Vector2.left * LabelPullLeft,
              new Vector2(TextWidth, 88f) * ActionScale);

        // 링 안 키 글자와 같은 글꼴로. (한글은 대체 글꼴로 빠진다 — ApplyKeyFont 참고)
        ApplyKeyFont(wired.InteractLabel);

        // ── "길게 누르세요" 는 뺐다 ──────
        //
        // 안내 문구가 이미 "길게 눌러서 들고 가기" · "꾹 누른 채로 · 대포로" 라고
        // 말하고 있어서, 같은 말이 한 화면에 두 번 적혀 있었다. 줄이 하나 빠지면서
        // 안내 문구가 링 한가운데 높이로 올라온다.
        //
        // 되살리려면 여기서 글자를 짓고, Wiring 에 칸을 도로 만들어 Connect 에서
        // 꽂으면 된다 (런타임의 interactHoldHint 와 isHold 는 남겨 뒀고,
        //  비어 있으면 그냥 넘어간다).

        group.gameObject.SetActive(false);
        wired.InteractPanel = group.gameObject;
    }


    /// <summary>
    /// 원형 버튼에 **손그림(painterly) 한 쌍**을 입히고 키 글자 칸을 잡는다.
    ///
    /// ⚠ **두 장은 같은 1254×1254 칸에 그려진 한 쌍이다.** 바깥 테두리가 둘 다 x=120 에서
    ///    시작하도록 맞춰져 있어서, 같은 사각형에 꽉 채우면(Stretch) 중심과 크기가 저절로
    ///    맞는다. 자리를 따로 재서 맞추려 들면 안 된다.
    ///
    /// <code>
    /// interaction-ring-painterly      꽉 찬 원판. 가운데가 넓어 키 글자가 그 위에 앉는다.
    /// interaction-progress-painterly  도넛. 테두리 90px, 안쪽 구멍 지름 834px (칸의 66.5%).
    ///                                 Radial360 으로 쓸면 테두리만 12시부터 차오른다.
    /// </code>
    ///
    /// 둘 다 이미 색이 칠해진 그림이라 틴트를 걸지 않고 흰색(원본 그대로) 그린다.
    /// <c>preserveAspect</c> 는 나중에 링 칸이 정사각형이 아니게 되더라도 **두 장이 같이**
    /// 줄어들어 서로 어긋나지 않게 하려고 켠다.
    ///
    /// ⚠ <c>fillAmount</c> 는 **건드리지 않는다.** 진행률은 런타임이 주인이라, 여기서
    ///    손대면 이미 돌고 있는 작업의 게이지가 튄다.
    ///
    /// HUD 를 통째로 다시 짓지 않고 그림만 갈아끼울 수 있도록 갈라 두었다.
    /// (<see cref="ShipCoopInteractRingPatch"/> — 전체 재생성은 씬 override 를 끊는다)
    /// </summary>
    /// <summary>
    /// 키캡 글꼴(<see cref="KeyFontPath"/>)을 입힌다. 링 안 키 글자와 그 옆 안내 문구가
    /// 같이 쓴다 — 둘이 다른 글꼴이면 한 덩어리로 안 읽힌다.
    ///
    /// ⚠ **한글은 이 글꼴에 없다.** Fredoka 는 라틴 전용이라 "돛" 같은 글자는 TMP 의
    ///    기본 대체 글꼴(NotoSansKR-Bold, TMP Settings 에 걸려 있다)로 그려진다.
    ///    그래서 안내 문구는 **한글은 NotoSansKR, 키 글자(J · L · K)는 Fredoka** 로
    ///    섞여 나온다. 노린 것이다 — 문장 속 키 글자가 링 안 키캡과 같은 모양이 된다.
    ///
    /// ⚠ **재질도 같이 바꾼다.** font 만 갈면 프리팹에 구워진 옛 글꼴의 재질이 남아서,
    ///    글자 모양은 바뀌었는데 아틀라스가 안 맞아 뭉개진 채로 나온다.
    /// </summary>
    private static void ApplyKeyFont(TextMeshProUGUI text)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KeyFontPath);

        if (font == null)
        {
            Debug.LogWarning($"[배 협동 HUD] 키캡 글꼴을 못 찾았다 — {KeyFontPath}. 쓰던 글꼴로 남긴다. " +
                             "Tools > 아라아띠 > 배 협동 키캡 글꼴 굽기 를 한 번 돌리세요.");
            text.fontStyle = FontStyles.Bold;
            return;
        }

        text.font = font;
        text.fontSharedMaterial = font.material;

        // ⚠ **Bold 를 주지 않는다.** 이미 굵기가 Bold 로 박힌 파일(Fredoka-Bold)이라,
        //    여기서 또 Bold 를 주면 TMP 가 그 위에 가짜 굵기를 덧대 뭉개진다.
        text.fontStyle = FontStyles.Normal;
    }

    internal static void DressInteractRing(Image badge, Image gauge, TextMeshProUGUI key)
    {
        // 손그림(painterly) 한 벌로 갈아입혔다. v4 와 **같은 1254×1254** 라 안쪽 배치
        // (KeyInset · KeyLineHeight)를 그대로 쓴다. 옛 v4 두 장은 지우지 않고 남겨 뒀다.
        badge.sprite = Sprite("interaction-ring-painterly");
        badge.color = Color.white;
        badge.preserveAspect = true;

        gauge.sprite = Sprite("interaction-progress-painterly");
        gauge.color = Color.white;
        gauge.preserveAspect = true;

        // 기존 동작 그대로 — 12시에서 시작해 시계 방향으로 도는 원형 게이지.
        gauge.type = Image.Type.Filled;
        gauge.fillMethod = Image.FillMethod.Radial360;
        gauge.fillOrigin = (int)Image.Origin360.Top;
        gauge.fillClockwise = true;

        // 글자 칸은 **도넛 안쪽 구멍에 맞춰** 잡는다. 예전에는 링 전체를 덮고 글씨를
        // 14pt 로 묶어 놨는데, 지금 그림은 가운데가 넓어져 그럴 이유가 없어졌다. 자동 크기를
        // 넓게 열어 두면 "K" 는 크게, "Space" · "J · L" 은 칸에 맞춰 스스로 줄어든다.
        Place(key.rectTransform, Half, Vector2.zero,
              new Vector2(RingSize * KeyInset, RingSize * KeyLineHeight) * ActionScale);

        // ⚠ **Center 가 아니라 Midline 이다.** 둘 다 "가운데" 지만 기준이 다르다.
        //
        //    Center 는 글자 그림이 아니라 **줄 상자(ascender~descender)** 의 한가운데를
        //    맞춘다. 한글 글꼴(NotoSansKR)은 위 여백(ascender 104.4)이 아래(descender
        //    25.92)보다 훨씬 커서, 그대로 두면 글자가 눈에 띄게 아래로 내려앉는다.
        //    30pt 로 재보면 "Space" 가 −5.0px, "K" 가 −2.0px 내려가 있었다.
        //
        //    Midline 은 **실제 글자 덩어리**의 한가운데를 맞춘다. 같은 조건에서
        //    "Space" · "K" · "J · L" 셋 다 0.00px — 도넛 구멍 정중앙에 앉는다.
        key.alignment = TextAlignmentOptions.Midline;

        ApplyKeyFont(key);

        // ⚠ 흰색이다. OnBadge(짙은 남색)를 썼던 예전 키캡은 밝은 판 위였다.
        //    지금은 어두운 원판 위에 바로 앉으므로 흰색이어야 보인다.
        key.color = Color.white;
        Shrink(key, KeyFontMin * ActionScale, KeyFontMax * ActionScale);
    }

    // ------------------------------------------------------------------ 늦었다는 알림

    /// <summary>
    /// 붉은 구간 위에 떴다 사라지는 **"늦었다" 팻말**을 짓는다. 꺼진 채로 나온다.
    ///
    /// 붉은 구간(Delay)만 봐서는 그게 무슨 뜻인지 처음 보는 사람은 모릅니다. 붉은 칸은
    /// "배와 기준선 사이" 라는 뜻인데, 그걸 읽으려면 기준선이 무엇인지부터 알아야 하고
    /// 그건 그림만으로 전해지지 않습니다. 그래서 벌어지는 순간에만 글씨로 한 번
    /// 짚어 줍니다. 뜨고 사라지는 규칙은 <see cref="ShipCoopHud"/> 가 정합니다.
    ///
    /// ⚠ **손글씨 그림이 나오기 전까지 쓰는 임시 모양입니다.** 자리 · 크기 · 타이밍을
    ///    먼저 눈으로 보려고 세운 것이라 글씨는 TMP, 화살표는 코드로 구운 삼각형입니다
    ///    (<see cref="ShipCoopHurryArrowArt"/>). 그림이 나오면 Arrow 와 Label 을
    ///    Image 한 장으로 갈아끼우면 됩니다.
    ///
    /// 배 마커와 같은 자리 계산을 씁니다 — Track 위 t 지점. 다만 pivot 이 아래라
    /// 트랙 **위쪽**에 서고, 런타임이 **기준선 자리**(붉은 구간의 오른쪽 끝, 지금쯤
    /// 있어야 할 곳)로 옮깁니다. 삼각형이 가리키는 곳이 곧 "여기까지 왔어야 한다" 입니다.
    ///
    /// HUD 를 통째로 다시 짓지 않고 **이것만 덧붙일 수 있도록** 따로 빼 두었습니다.
    /// (<see cref="ShipCoopHurryCalloutPatch"/> — 전체 재생성은 씬 override 를 끊습니다)
    /// </summary>
    internal static RectTransform BuildHurryCallout(RectTransform track, out CanvasGroup group,
                                                    out TextMeshProUGUI label, out RectTransform arrow)
    {
        // 통째로 짓는 길로 들어왔으면 이미 채워져 있다. 덧붙이기로 들어왔으면 비어 있어서
        // 한글이 깨지므로, 프리팹이 쓰던 글꼴을 여기서 주워 온다.
        if (_font == null)
        {
            TextMeshProUGUI sample = track.root.GetComponentInChildren<TextMeshProUGUI>(true);
            _font = sample != null ? sample.font : null;
        }

        RectTransform hurry = Rect("HurryCallout", track);
        hurry.anchorMin = new Vector2(0f, 0.5f);
        hurry.anchorMax = new Vector2(0f, 0.5f);
        hurry.pivot = BottomCenter;
        hurry.anchoredPosition = new Vector2(0f, HurryLift);
        hurry.sizeDelta = new Vector2(HurryWidth, HurryHeight);

        // 사라질 때 흐려지라고. 알파만 쓰므로 입력은 전부 막아 둡니다.
        group = hurry.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // 삼각형이 아래 — 기준선(붉은 구간의 오른쪽 끝)을 가리킵니다. 묶음 맨 아래에 붙습니다.
        arrow = Rect("Arrow", hurry);
        Place(arrow, BottomCenter, new Vector2(0f, HurryArrowSize * 0.5f),
              new Vector2(HurryArrowSize, HurryArrowSize));
        Img(arrow, ShipCoopHurryArrowArt.Ensure(), WarnOrange).preserveAspect = true;

        // 글자는 런타임이 채웁니다 — 돛이 문제인지 조타가 문제인지에 따라 달라집니다.
        //
        // ⚠ **판을 깔지 않습니다.** 어두운 판을 뒤에 넣어 봤는데 팻말이 무거워 보였습니다.
        //    대신 글자 자체에 검정 그림자를 씌워서 밝은 갑판 위에서도 읽히게 합니다.
        label = Text("Label", hurry, "", HurryFontSize, TextAlignmentOptions.Center);
        Place((RectTransform)label.transform, BottomCenter,
              new Vector2(0f, HurryTextBottom), new Vector2(HurryWidth, HurryTextHeight));
        label.color = WarnOrange;
        label.fontStyle = FontStyles.Bold;

        // 검정 그림자. TMP 는 UI 의 Shadow 컴포넌트를 무시하므로 제 Underlay 를 켠
        // 머티리얼을 꽂습니다. 글꼴이 없으면 null 이 와서 그림자 없이 그대로 갑니다.
        Material shadow = ShipCoopHurryTextShadow.Ensure(label.font);
        if (shadow != null)
        {
            label.fontSharedMaterial = shadow;
        }

        hurry.gameObject.SetActive(false);
        return hurry;
    }

    // ------------------------------------------------------------------ 연결

    private static void Connect(ShipCoopHud hud, Wiring w)
    {
        var so = new SerializedObject(hud);

        // 출항 패널을 뺐으므로 **비워 둔다.** 런타임에 null 가드가 있다.
        Set(so, "phaseLabel", null);
        Set(so, "hpFill", w.HpFill);
        Set(so, "hpLabel", w.HpLabel);
        Set(so, "timeLabel", w.TimeLabel);
        Set(so, "progressFill", w.ProgressFill);
        Set(so, "expectedMarker", w.ExpectedMarker);
        Set(so, "delayFill", w.DelayFill);
        Set(so, "shipMarker", w.ShipMarker);
        Set(so, "hurryCallout", w.HurryCallout);
        Set(so, "hurryGroup", w.HurryGroup);
        Set(so, "hurryLabel", w.HurryLabel);
        Set(so, "hurryArrow", w.HurryArrow);
        Set(so, "behindWarning", w.BehindWarning);
        Set(so, "interactPanel", w.InteractPanel);
        Set(so, "interactLabel", w.InteractLabel);
        Set(so, "interactGauge", w.InteractGauge);
        Set(so, "interactIcon", w.InteractIcon);
        Set(so, "interactKeycap", w.InteractKeycap);
        // "길게 누르세요" 는 더 이상 짓지 않는다. null 을 넣어 프리팹에 남아 있던
        // 옛 참조를 끊는다 — 안 끊으면 지워진 오브젝트를 가리킨 채로 남는다.
        Set(so, "interactHoldHint", null);

        Set(so, "courseWarningRoot", w.CourseWarningRoot);
        Set(so, "courseWarningVignette", w.CourseWarningVignette);
        Set(so, "courseWarningLabel", w.CourseWarningLabel);

        // 사건 아이콘 다섯 종류. 암초와 파도가 같은 그림이면 목록에서 구분이 안 된다.
        // 새 픽토그램 5종. 사건마다 그림이 다르지 않으면 목록에서 구분이 안 된다.
        //
        // 돌풍(Sail) 과 조타(Helm) 는 전용 그림이 따로 없다. 돌풍은 그대로 돌풍 그림을 쓰고,
        // 조타로 넘기는 사건은 암초와 파도뿐이라 각자 자기 그림으로 간다.
        Set(so, "eventIconHull", Sprite4("event-hull-damage-painterly"));
        Set(so, "eventIconSail", Sprite4("event-squall-painterly"));
        Set(so, "eventIconCannon", Sprite4("event-enemy-ship-painterly"));
        Set(so, "eventIconHelm", Sprite4("event-reef-painterly"));
        Set(so, "eventIconReef", Sprite4("event-reef-painterly"));
        Set(so, "eventIconWave", Sprite4("event-big-wave-painterly"));
        Set(so, "eventIconEnemy", Sprite4("event-enemy-ship-painterly"));
        Set(so, "eventIconFlood", Sprite4("event-flood"));

        Set(so, "taskIconHelm", Sprite("icon-helm"));
        Set(so, "taskIconSails", Sprite("icon-sails"));
        Set(so, "taskIconCannon", Sprite("icon-cannon"));
        Set(so, "taskIconRepair", Sprite("icon-repair"));

        // 운반물 세 가지. 전에는 작업 그림을 돌려써서 자재와 물이 구분되지 않았다.
        Set(so, "taskIconAmmo", Sprite("icon-cannonball"));
        Set(so, "taskIconPlank", Sprite("icon-plank"));
        Set(so, "taskIconWater", Sprite("icon-bucket"));

        so.FindProperty("eventWarning").colorValue = WarnOrange;
        so.FindProperty("eventRunning").colorValue = DangerRed;
        so.FindProperty("hpHealthy").colorValue = HpGreen;

        SerializedProperty rows = so.FindProperty("eventRows");
        rows.arraySize = w.EventRows.Length;
        for (int i = 0; i < w.EventRows.Length; i++)
        {
            SerializedProperty item = rows.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("root").objectReferenceValue = w.EventRows[i].root;
            item.FindPropertyRelative("label").objectReferenceValue = w.EventRows[i].label;
            item.FindPropertyRelative("timer").objectReferenceValue = w.EventRows[i].timer;
            item.FindPropertyRelative("icon").objectReferenceValue = w.EventRows[i].icon;
            item.FindPropertyRelative("accent").objectReferenceValue = w.EventRows[i].accent;
            item.FindPropertyRelative("title").objectReferenceValue = w.EventRows[i].title;
            item.FindPropertyRelative("seconds").objectReferenceValue = w.EventRows[i].seconds;
            item.FindPropertyRelative("badge").objectReferenceValue = w.EventRows[i].badge;
            item.FindPropertyRelative("badgeLabel").objectReferenceValue = w.EventRows[i].badgeLabel;
            item.FindPropertyRelative("timerTrack").objectReferenceValue = w.EventRows[i].timerTrack;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = so.FindProperty(path);

        if (p == null)
        {
            Debug.LogWarning($"[HUD V2] ShipCoopHud 에 '{path}' 가 없다. 건너뛴다.");
            return;
        }

        p.objectReferenceValue = value;
    }

    // ------------------------------------------------------------------ 손도구

    /// <summary>패널 배경 + 가운데가 빈 테두리. README 의 공통 조립 순서다.</summary>
    private static void Capsule(RectTransform parent)
    {
        RectTransform back = Rect("Background", parent);
        Stretch(back, 0f, 0f, 0f, 0f);
        Img(back, Sprite("panel-capsule-background"), new Color(1f, 1f, 1f, 0.9f));

        RectTransform frame = Rect("Frame", parent);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Img(frame, Sprite("panel-capsule-frame"), Color.white);
    }

    /// <summary>
    /// 트랙 + 채움. 채움은 **폭을 줄이지 않고 fillAmount 로** 조절한다. (README)
    /// 채움 PNG 는 전체 길이라 72% 같은 것이 구워져 있지 않다.
    /// </summary>
    private static RectTransform Bar(RectTransform parent, string name, Vector2 position, Vector2 size,
                                     Color color, out Image fill)
    {
        RectTransform track = Rect(name, parent);
        Place(track, Half, position, size);
        Img(track, Sprite("bar-track"), Color.white);

        RectTransform inner = Rect("Fill", track);
        Stretch(inner, 6f, 6f, 6f, 6f);

        fill = Img(inner, Sprite("bar-fill-white"), color);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        return track;
    }

    /// <summary>항해 바 위를 움직이는 표식. 왼쪽 끝을 기준으로 x 만 옮긴다.</summary>
    private static RectTransform Marker(RectTransform track, string name, Sprite sprite, Vector2 size, Color color)
    {
        RectTransform marker = Rect(name, track);
        marker.anchorMin = new Vector2(0f, 0.5f);
        marker.anchorMax = new Vector2(0f, 0.5f);
        marker.pivot = Half;
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = size;

        Image image = Img(marker, sprite, color);
        image.preserveAspect = true;

        return marker;
    }

    private static Image Icon(RectTransform parent, string name, Sprite sprite, Vector2 position, float size)
    {
        RectTransform rt = Rect(name, parent);
        Place(rt, Half, position, Vector2.one * size);

        Image image = Img(rt, sprite, Cream);
        image.preserveAspect = true;
        return image;
    }

    /// <summary>
    /// 금테 패널을 **비율 그대로** 깐다.
    ///
    /// `preserveAspect` 가 핵심이다. 패널 안에 아이콘과 트랙이 그려져 있어서
    /// 가로세로를 따로 늘리면 조타륜이 타원이 되고 금테 굵기가 달라진다.
    /// </summary>
    private static Image Panel(RectTransform rt, string name)
    {
        Image image = Img(rt, Sprite3(name), Color.white);
        image.preserveAspect = true;
        return image;
    }

    /// <summary>
    /// hp-panel · timer-panel 처럼 왼쪽에 그림(하트 · 시계)이 그려진 알약 판의
    /// 오른쪽 빈 자리에 글자를 놓는다. <see cref="PillLabelLeft"/>·<see cref="PillLabelRight"/>
    /// 로 그 빈 자리의 비율을 잡아 두므로, 판 하나만 고치면 둘 다 같이 맞는다.
    /// </summary>
    private static void PlacePillLabel(RectTransform label, float pillWidth)
    {
        float left = pillWidth * (PillLabelLeft - 0.5f);
        float right = pillWidth * (PillLabelRight - 0.5f);

        Place(label, Half, new Vector2((left + right) * 0.5f, 0f),
              new Vector2(right - left, PillHeight * 0.7f));
    }

    /// <summary>
    /// 글자가 자리를 넘으면 스스로 줄어들게 한다.
    ///
    /// 단계 문구는 `출항` 두 글자일 때도 있고 `플레이어 입장중...` 일 때도 있다.
    /// 고정 크기로 두면 짧은 쪽에 맞추면 긴 쪽이 넘치고, 긴 쪽에 맞추면 짧은 쪽이 초라하다.
    /// 한 줄로 묶어 두는 것도 같은 이유다 — 두 줄로 접히면 패널 위아래로 삐져나온다.
    /// </summary>
    private static void Shrink(TextMeshProUGUI text, float min, float max)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    /// <summary>사건 카드 한 벌에서 찾는다.</summary>
    private static Sprite Sprite4(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Sprite", new[] { ArtRootEvent }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        throw new System.IO.FileNotFoundException($"{ArtRootEvent} 아래에 {name} 이 없다.");
    }

    /// <summary>새 패널 한 벌에서 찾는다. 옛 에셋과 이름이 겹쳐도 섞이지 않게 나눠 둔다.</summary>
    private static Sprite Sprite3(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Sprite", new[] { ArtRootV3 }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        throw new System.IO.FileNotFoundException($"{ArtRootV3} 아래에 {name} 이 없다.");
    }

    private static Sprite Sprite(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { ArtRoot });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        throw new System.IO.FileNotFoundException($"{ArtRoot} 아래에 {name} 이 없다.");
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = Half;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = Half;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color color)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string text,
                                        float size, TextAlignmentOptions alignment)
    {
        RectTransform rt = Rect(name, parent);
        Stretch(rt, 0f, 0f, 0f, 0f);

        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null)
        {
            tmp.font = _font;
        }

        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SyncTextColors(GameObject root)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            text.faceColor = text.color;
            EditorUtility.SetDirty(text);
        }
    }

    private static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
