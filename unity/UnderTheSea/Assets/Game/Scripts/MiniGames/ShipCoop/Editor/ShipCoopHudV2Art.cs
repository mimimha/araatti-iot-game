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
/// 사건 목록   상단중앙(시간 밑) 608×101 씩, 간격 8    event-card-background + 픽토그램 5종
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
    /// </summary>
    private const float EventCardScale = 0.8f;

    private const float EventCardWidth = 760f * EventCardScale;
    private const float EventCardHeight = 126f * EventCardScale;
    private const float EventCardGap = 10f * EventCardScale;

    /// <summary>
    /// 첫 카드가 시작하는 높이. 목록이 상단 가운데로 왔으므로 **바로 위에 있는 것은
    /// 시간 알약**이다. 그 높이(PillHeight) + 한 번 더 마진만큼 내려가 밑에 붙는다.
    /// (왼쪽 위 체력 알약과도 같은 높이라 세 칸이 한 선에서 시작한다)
    /// </summary>
    private const float EventListTop = TopMargin + PillHeight + TopMargin;

    /// <summary>
    /// 상호작용 패널을 그림 원본(424 × 150) 의 몇 배로 띄우는가.
    ///
    /// 원본 크기로는 글자와 키캡이 작아서 눈에 잘 안 들어왔습니다. 1.2 배면
    /// 화면 오른쪽 아래가 답답해지지 않으면서 문구가 편하게 읽힙니다.
    /// 안쪽 배치 값도 전부 이 값을 곱하므로 **여기 하나만 바꾸면 통째로 커집니다.**
    /// </summary>
    private const float ActionScale = 1.2f;

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
        public TextMeshProUGUI InteractHoldHint;
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
        // 왼쪽 위는 체력이, 아래는 항해 바가 맡아 시간은 가장 눈에 잘 들어오는 한가운데
        // 맨 위에 둔다. **사건 목록이 바로 이 밑에 붙으므로**(EventListTop) 이 판 높이를
        // 바꾸면 목록도 같이 내려간다.
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
    /// **상단 가운데.** 이 게임에서 가장 중요한 UI 다. (9장)
    ///
    /// 왼쪽 위 구석에 있던 것을 시간 알약 바로 밑, 화면 한가운데로 옮겼습니다. 구석은
    /// 눈이 잘 안 가는 자리인데 이 카드는 **가장 먼저 읽혀야 하는 글**입니다. 옮기면서
    /// 양옆이 비므로 카드를 가로로 늘렸습니다(<see cref="EventCardWidth"/>).
    /// 카드는 위에서부터 채우고 남는 줄은 꺼지므로(ShipCoopHud.UpdateEvents),
    /// 보통 한두 장이라 갑판을 가리지 않습니다.
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
        Place(group, TopCenter,
              new Vector2(0f, -(EventListTop + EventCardHeight * 0.5f)),
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

        // 제목 · 보조 문구 · 타이머가 **같은 선에서 시작한다.** 셋이 제각각 시작하면
        // 카드가 들쭉날쭉해 보이고, 눈이 줄마다 가로로 다시 자리를 찾아야 한다.
        // 그림 오른쪽 끝(−W*0.40 ± H*0.29) 에서 한 뼘 띄운 자리다.
        const float TextLeft = -W * 0.332f;

        RectTransform card = Rect($"Row_{index + 1}", group);
        Place(card, Half, new Vector2(0f, -index * (H + EventCardGap)), new Vector2(W, H));

        Image back = Img(card, Sprite4("event-card-background"), new Color(1f, 1f, 1f, 0.95f));
        back.type = Image.Type.Sliced;

        // ⚠ **9-slice 테두리를 줄여야 한다.** 원본 테두리가 64px 인데 카드 높이가 126 이라,
        //    그대로 두면 위아래 테두리(64+64=128)가 카드보다 커져서 **알약 모양**이 된다.
        //    `pixelsPerUnitMultiplier` 는 테두리를 그 배수만큼 얇게 그린다. 3 이면 약 21px.
        back.pixelsPerUnitMultiplier = 3f;

        // ── 그림 ─────────────────────────────
        Image icon = Icon(card, "Icon", null, new Vector2(-W * 0.40f, 0f), H * 0.58f);
        icon.color = Color.white;   // 사건 그림은 채색이라 크림색을 곱하면 탁해진다

        // ── 제목 ─────────────────────────────
        //
        // 남은 초를 뺀 자리까지 가져와 **배지 바로 앞까지** 넓게 쓴다. 왼쪽 정렬이라
        // 넓혀도 글자 시작점은 그대로고, 긴 사건 이름이 줄어들지 않고 다 나온다.
        const float TitleWidth = W * 0.63f;
        TextMeshProUGUI title = Text("Title", card, "사건", 27f * EventCardScale, TextAlignmentOptions.Left);
        Place((RectTransform)title.transform, Half,
              new Vector2(TextLeft + TitleWidth * 0.5f, H * 0.23f), new Vector2(TitleWidth, H * 0.30f));
        title.color = EventTitle;
        Shrink(title, 18f * EventCardScale, 27f * EventCardScale);

        // ── 보조 문구 · 갑판 ──────────────────
        const float LabelWidth = W * 0.52f;
        TextMeshProUGUI label = Text("Label", card, "", 19f * EventCardScale, TextAlignmentOptions.Left);
        Place((RectTransform)label.transform, Half,
              new Vector2(TextLeft + LabelWidth * 0.5f, 0f), new Vector2(LabelWidth, H * 0.24f));
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
        Place(badge, Half, new Vector2(W * 0.40f, H * 0.23f), new Vector2(W * 0.135f, H * 0.26f));
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
        // 오른쪽 끝은 그대로 두고 **왼쪽을 글줄에 맞춰** 늘렸다. 글 두 줄과 바가
        // 한 선에서 시작하므로 바가 얼마나 줄었는지도 글 시작점과 견줘 읽힌다.
        const float TrackWidth = W * 0.383f - TextLeft;

        RectTransform track = Rect("Timer", card);
        Place(track, Half, new Vector2(TextLeft + TrackWidth * 0.5f, -H * 0.31f),
              new Vector2(TrackWidth, H * 0.09f));

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
        //
        // ⚠ **바탕(ring-background)이 이제 테두리 · 트랙까지 한 장에 다 그려진 그림이다**
        //    (interaction-base.png). 예전에는 Track · Frame 을 따로 그려 겹쳤지만, 이제
        //    그 두 장은 필요 없다 — 겹치면 옛 그림이 새 그림을 덮어 오히려 어긋난다.
        //    채움(ring-fill-white)도 마찬가지로 이미 초록으로 칠해진 그림
        //    (interaction-progress-fill.png)이라 틴트를 걸지 않고 흰색(원본 그대로) 그린다.

        // ── 원형 버튼(진행 링) ─────────────────────
        RectTransform ring = Rect("Ring", group);
        Place(ring, Half, new Vector2(-120f, 0f) * ActionScale, new Vector2(104f, 104f) * ActionScale);
        Img(ring, Sprite("ring-background"), Color.white);

        RectTransform ringFill = Rect("Fill", ring);
        Stretch(ringFill, 0f, 0f, 0f, 0f);
        Image gauge = Img(ringFill, Sprite("ring-fill-white"), Color.white);
        gauge.type = Image.Type.Filled;
        gauge.fillMethod = Image.FillMethod.Radial360;
        gauge.fillOrigin = (int)Image.Origin360.Top;
        gauge.fillClockwise = true;
        gauge.fillAmount = 0f;
        wired.InteractGauge = gauge;

        // ⚠ **키 글자를 반드시 연결한다.** 여기서 자식을 새로 만들기 때문에
        //    연결하지 않으면 예전처럼 무엇을 하든 빈칸으로 굳는다.
        // ⚠ 색은 흰색이다. OnBadge(짙은 남색)를 썼던 예전 키캡은 밝은 판 위였다.
        //    지금은 이 글자가 interaction-base.png 의 어두운 안쪽 원 위에 바로 앉으므로
        //    흰색이어야 보인다.
        TextMeshProUGUI keyLabel = Text("Key", ring, "Space", 28f * ActionScale, TextAlignmentOptions.Center);
        keyLabel.color = Color.white;
        keyLabel.fontStyle = FontStyles.Bold;
        Shrink(keyLabel, 12f * ActionScale, 12f * ActionScale);
        wired.InteractKeycap = keyLabel;

        // ── 안내 문구 ────────────────────
        //
        // 링 오른쪽 끝이 -68 이다. 그 옆부터 글자(위)와 홀드 안내(아래)가 세로로 나눠 쓴다.
        const float TextLeft = -40f;
        const float TextWidth = 216f;

        // ⚠ 세로는 **TopLeft 가 아니라 Left** 다. 안내 문구는 한 줄일 때도 두 줄일 때도
        //    있는데, 위로 붙여 두면 한 줄일 때 아래에 빈 띠가 크게 남는다.
        //    가운데 정렬로 두면 줄 수가 바뀌어도 덩어리가 제자리에 있다.
        wired.InteractLabel = Text("Label", group, "", 21f * ActionScale, TextAlignmentOptions.Left);
        Place((RectTransform)wired.InteractLabel.transform, Half,
              new Vector2(TextLeft + TextWidth * 0.5f, 19f) * ActionScale,
              new Vector2(TextWidth, 66f) * ActionScale);

        // "길게 누르세요" — 떼면 놓치는 동작(운반 · 집기)에서만 켠다. ShipCoopHud.Show 가 켜고 끈다.
        // 예전에는 이 줄의 왼쪽 칸을 키캡 판이 차지했지만, 키가 링 안으로 들어가 이제 전부 쓴다.
        TextMeshProUGUI holdHint = Text("HoldHint", group, "길게 누르세요", 14f * ActionScale, TextAlignmentOptions.Left);
        holdHint.color = new Color(1f, 1f, 1f, 0.75f);
        Place((RectTransform)holdHint.transform, Half,
              new Vector2(TextLeft + TextWidth * 0.5f, -33f) * ActionScale,
              new Vector2(TextWidth, 30f) * ActionScale);
        wired.InteractHoldHint = holdHint;

        group.gameObject.SetActive(false);
        wired.InteractPanel = group.gameObject;
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
        //    밝은 갑판 위에서 읽기 힘들면 판을 되살리지 말고 글자에 얇은 외곽선을 주세요
        //    (TMP outlineWidth — 머티리얼 인스턴스가 생기므로 프리팹에 굽기 전에 확인).
        label = Text("Label", hurry, "", HurryFontSize, TextAlignmentOptions.Center);
        Place((RectTransform)label.transform, BottomCenter,
              new Vector2(0f, HurryTextBottom), new Vector2(HurryWidth, HurryTextHeight));
        label.color = WarnOrange;
        label.fontStyle = FontStyles.Bold;

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
        Set(so, "interactHoldHint", w.InteractHoldHint);

        Set(so, "courseWarningRoot", w.CourseWarningRoot);
        Set(so, "courseWarningVignette", w.CourseWarningVignette);
        Set(so, "courseWarningLabel", w.CourseWarningLabel);

        // 사건 아이콘 다섯 종류. 암초와 파도가 같은 그림이면 목록에서 구분이 안 된다.
        // 새 픽토그램 5종. 사건마다 그림이 다르지 않으면 목록에서 구분이 안 된다.
        //
        // 돌풍(Sail) 과 조타(Helm) 는 전용 그림이 따로 없다. 돌풍은 그대로 돌풍 그림을 쓰고,
        // 조타로 넘기는 사건은 암초와 파도뿐이라 각자 자기 그림으로 간다.
        Set(so, "eventIconHull", Sprite4("event-hull-damage"));
        Set(so, "eventIconSail", Sprite4("event-squall"));
        Set(so, "eventIconCannon", Sprite4("event-enemy-ship"));
        Set(so, "eventIconHelm", Sprite4("event-reef"));
        Set(so, "eventIconReef", Sprite4("event-reef"));
        Set(so, "eventIconWave", Sprite4("event-big-wave"));
        Set(so, "eventIconEnemy", Sprite4("event-enemy-ship"));
        Set(so, "eventIconFlood", Sprite("icon-water"));

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

