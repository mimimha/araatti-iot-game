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
/// 배 HP       우하(상호작용 위) 세로 620 · 두께 86   hp-panel-noheart 을 90° 돌림 + hp-heart
/// 시간        우상단     301×86     timer-panel
/// 항해        상단중앙   620×85     voyage-panel  + voyage-fill-cyan
/// 지연 경고   (지금은 안 띄웁니다. 항해 바 안의 붉은 구간이 대신 말해줍니다)
/// 사건 목록   좌상       480×126 씩, 간격 10   event-card-background + 픽토그램 5종
/// (팀원 4칸은 뺐습니다. 머리 위 이름표가 누가 어디 있는지를 대신 말해줍니다)
/// 침수        (바를 뺐습니다. 사건 카드 맨 윗줄로 갑니다)
/// 상호작용    하단우측   424×150    panel-action-plaque + 원형 링 (도넛)
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
    private static readonly Color RingGold = Hex(0xFFD05D);
    private static readonly Color Cream = new Color(0.96f, 0.94f, 0.88f);

    /// <summary>밝은 배지 위에 올리는 글자. 흰 글자를 올리면 흰 바탕에 묻혀 안 보인다.</summary>
    private static readonly Color OnBadge = Hex(0x1C2A3A);

    // 사건 카드 글자색. `event-ui-assets/manifest.json` 이 지정한 값.
    private static readonly Color EventTitle = Hex(0xFFF0D0);
    private static readonly Color EventSecondary = Hex(0xB8D8E8);

    /// <summary>
    /// 링 안쪽 바탕. 원본 그대로 깔면 **포탄이 안 보인다.**
    ///
    /// 포탄은 검은 쇠구슬이라 남색 바탕과 명도가 붙어서 아래쪽 실루엣이 묻혔다.
    /// 곱하기 틴트라 밝게는 못 만들고, 바탕을 반으로 어둡게 깔아 대비를 벌린다.
    /// 0.35 까지 내리면 그냥 검은 구멍이 되고, 0.7 로는 포탄이 여전히 묻는다.
    /// </summary>
    private static readonly Color RingInk = new Color(0.5f, 0.5f, 0.5f);

    // ------------------------------------------------------------------ 배치
    //
    // 1920 × 1080 기준. 패널은 전부 원본 비율을 지키므로 **폭만 정하고 높이는 계산**한다.
    // 화면 가운데는 비워 둔다. 위쪽에 상태, 아래쪽에 사람과 상호작용만 둔다.

    private const float Margin = 28f;
    private const float TopMargin = 24f;

    // 체력은 **거리(항해) 바와 크기를 맞춘다.** 두 그림이 1024 × 142 와 1024 × 140 으로
    // 사실상 같은 비율이라, 길이만 같게 두면 두께도 따라서 같아진다.
    private const float HpWidth = VoyageWidth;  // 1024 × 142 — 세로로 세우므로 이건 "길이" 다
    // 시계 폭. 왼쪽 위에 있던 출항 패널(320 × 86.25)과 **높이를 맞춰** 정한 값이다.
    // 출항 패널은 뺐지만 이 높이가 거리 바 · 체력과도 맞아 그대로 둔다.
    private const float TimerWidth = 301.09f;   // 384 × 110 → 높이 86.25
    private const float VoyageWidth = 620f;     // 1024 × 140
    private const float WarningWidth = 400f;    // 768 × 123

    // ── 사건 카드 ──
    //
    // 배경이 9-slice(테두리 64) 라서 원본 비율을 지킬 필요가 없다. 읽기 좋은 크기로 정한다.
    private const float EventCardWidth = 480f;
    private const float EventCardHeight = 126f;
    private const float EventCardGap = 10f;

    /// <summary>첫 카드가 시작하는 높이. 출항 패널을 뺐으므로 다른 위 칸과 같은 선에서 시작한다.</summary>
    private const float EventListTop = TopMargin;

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
    /// HP · 시간 · 항해. README 대로 **폭 720 의 한 그룹** 아래에 둔다.
    /// 그룹 안에서 HP x=0, 시간 x=536, 항해 y=78 이다.
    /// </summary>
    private static void BuildTopBars(RectTransform canvas, Wiring wired)
    {
        float hpHeight = HpWidth * 142f / 1024f;
        float timerHeight = TimerWidth * 110f / 384f;
        float voyageHeight = VoyageWidth * 140f / 1024f;

        // ── 배 HP — **세로로 세워 오른쪽 끝에** ─────────
        //
        // 화면 오른쪽 가장자리에 세로로 붙입니다. 왼쪽 아래는 프로필 넉 장이 이미
        // 넓게 자리를 쓰고 있어서, HP 까지 거기 두면 왼쪽만 무거워집니다.
        // 아래 끝은 상호작용 패널 바로 위에 맞춥니다.
        // 위쪽 가로줄은 항해 하나만 남아 바다를 덜 가립니다.
        //
        // ⚠ **패널을 90° 돌립니다.** 돌리면 패널 왼쪽(하트)이 화면 아래로 가고,
        //    채움이 **아래에서 위로** 자랍니다. 물이 차오르듯 읽혀서 HP 에 맞습니다.
        //    돌려도 안쪽 배치 값은 그대로 쓸 수 있습니다 — 자식이 같이 돌기 때문입니다.
        const float HpScale = HpWidth / 1024f;

        float hpBottom = Margin + 150f * ActionScale + 14f;  // 상호작용 패널 위

        RectTransform hp = Rect("Hp", canvas);
        Place(hp, BottomRight,
              new Vector2(-(Margin + hpHeight * 0.5f), hpBottom + HpWidth * 0.5f),
              new Vector2(HpWidth, hpHeight));
        hp.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // ⚠ **하트를 떼어낸 패널을 쓴다.** (`hp-panel-noheart`)
        //
        //    하트가 패널에 박혀 있어서 패널을 돌리면 하트도 같이 눕는다. 그래서
        //    하트를 알파로 오려 `hp-heart` 로 빼고, 패널 쪽 하트 자리는 옆 남색으로 메웠다.
        //    가로로 쓸 일이 생기면 원본 `hp-panel` 을 그대로 쓰면 된다.
        Panel(hp, "hp-panel-noheart");

        wired.HpFill = Fill(hp, "hp-fill-green",
                            new Vector2(43.5f, 1f), new Vector2(806f, 45f), HpScale, Color.white);

        // 하트는 **거꾸로 돌려** 세워 둔다. 패널이 +90° 니까 -90° 를 주면 서로 상쇄돼
        // 화면에서는 똑바로 선다. 자리는 원본에서 떼어낸 그 자리(그림 기준 50,35 에 89×83) 에서
        // 화면 왼쪽으로 1px. 패널이 +90° 라 화면 왼쪽은 이 안에서 +y 다.
        RectTransform heart = Rect("Heart", hp);
        Place(heart, Half,
              new Vector2((50f + 89f * 0.5f - 512f) * HpScale, (71f - (35f + 83f * 0.5f)) * HpScale + 1f),
              new Vector2(89f * HpScale, 83f * HpScale));
        heart.localRotation = Quaternion.Euler(0f, 0f, -90f);
        Img(heart, Sprite3("hp-heart"), Color.white).preserveAspect = true;

        // 숫자(`100 / 100`)는 **두지 않는다.**
        //
        // 막대가 이미 얼마나 남았는지 말한다. 세로로 세우면서 숫자를 넣을 자리가
        // 막대 밖뿐이었는데, 거기 두면 왼쪽 세로줄에 읽을 것만 하나 더 늘어난다.
        // 정확한 수치가 필요한 순간이 이 게임에는 없다 — 많이 남았나 적게 남았나뿐이다.
        //
        // `hpLabel` 은 비워 둔다. 런타임이 null 이면 그냥 넘어간다.

        // ── 시간 ─────────────────────────────
        //
        // 시계도 패널에 그려져 있다. 글자는 그 오른쪽 남색 판 안에서만 쓴다.
        // 빈 칸 한가운데는 +40 인데 그대로 두면 오른쪽으로 쏠려 보여 +30 으로 조금 당겼다.
        // 상자 왼쪽 끝이 시계(원본 x 112) 를 넘지 않게 폭은 196.
        const float TimerScale = TimerWidth / 384f;

        // 오른쪽 끝에 붙인다. 앵커가 화면 오른쪽이라 해상도가 바뀌어도 그대로 따라간다.
        // 항해 바와 **위를 맞춘다.**
        RectTransform time = Rect("Time", canvas);
        Place(time, TopRight,
              new Vector2(-(Margin + TimerWidth * 0.5f), -(TopMargin + timerHeight * 0.5f)),
              new Vector2(TimerWidth, timerHeight));
        Panel(time, "timer-panel");

        wired.TimeLabel = Text("Label", time, "3:00", 26f, TextAlignmentOptions.Center);
        Place((RectTransform)wired.TimeLabel.transform, Half,
              new Vector2(30f * TimerScale, 0f), new Vector2(196f * TimerScale, 44f * TimerScale));
        Shrink(wired.TimeLabel, 14f, 26f);

        // ── 항해 ─────────────────────────────
        //
        // 돛도 패널에 그려져 있다. 트랙은 원본에서 x 171~947 · y 52~88 로 쟀다.
        const float VoyageScale = VoyageWidth / 1024f;

        // 상단 한가운데. HP 가 왼쪽 아래로 내려가서 이제 위쪽 가로줄은 이것 하나다.
        float voyageY = -(TopMargin + voyageHeight * 0.5f);

        RectTransform voyage = Rect("Voyage", canvas);
        Place(voyage, TopCenter, new Vector2(0f, voyageY), new Vector2(VoyageWidth, voyageHeight));
        Panel(voyage, "voyage-panel");

        // 표식(지금 위치 · 있어야 할 위치)이 이 안에서 움직이므로 트랙 자리를 실제로 만든다.
        RectTransform track = Rect("Track", voyage);
        Place(track, Half,
              new Vector2(47f * VoyageScale, 0f), new Vector2(777f * VoyageScale, 37f * VoyageScale));

        wired.ProgressFill = Fill(track, "voyage-fill-cyan",
                                  Vector2.zero, new Vector2(777f, 37f), VoyageScale, Color.white);
        Stretch((RectTransform)wired.ProgressFill.transform, 0f, 0f, 0f, 0f);

        // 늦은 만큼의 구간. 배 위치에서 기준선까지 폭만 늘어난다. (README)
        RectTransform delay = Rect("Delay", track);
        delay.anchorMin = new Vector2(0f, 0.5f);
        delay.anchorMax = new Vector2(0f, 0.5f);
        delay.pivot = new Vector2(0f, 0.5f);
        delay.anchoredPosition = Vector2.zero;
        delay.sizeDelta = new Vector2(0f, 14f);

        // **채움처럼 보이면 안 된다.** 이건 "여기부터 저기까지 늦었다" 는 구간 표시다.
        // 진하게 칠하면 진행도 채움과 섞여서 무엇을 보는 건지 알 수 없다.
        Img(delay, Sprite("bar-delay"), new Color(1f, 0.35f, 0.42f, 0.45f));
        wired.DelayFill = delay;

        // ⚠ **배 표식을 두지 않습니다.** 패널 왼쪽에 돛단배가 이미 그려져 있어서
        //    트랙 안에 또 배를 놓으면 배가 두 척으로 보입니다. 지금 어디까지 왔는지는
        //    채움의 오른쪽 끝이 그대로 말해줍니다. 기준선은 남겨야 늦었는지가 보입니다.
        wired.ExpectedMarker = Marker(track, "Expected", Sprite("voyage-reference"), new Vector2(8f, 40f), Cream);

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
    /// 왼쪽 위. **이 게임에서 가장 중요한 UI 다.** (9장)
    ///
    /// 목업(`art/hud/09-event-hud-mockup`)대로 한 장을 이렇게 나눕니다.
    /// <code>
    /// [ 그림 ]  제목            남은초  [예고/발생]
    ///           보조 문구 · 갑판
    ///           ▬▬▬▬▬▬▬▬▬ 타이머
    /// </code>
    ///
    /// 카드 배경은 9-slice(테두리 64)라 크기를 자유롭게 정합니다.
    /// 배지와 타이머 채움은 **흰색 에셋**이고 런타임이 단계 색으로 물들입니다.
    /// </summary>
    private static void BuildEventList(RectTransform canvas, Wiring wired)
    {
        RectTransform group = Rect("Events", canvas);
        Place(group, TopLeft,
              new Vector2(Margin + EventCardWidth * 0.5f, -(EventListTop + EventCardHeight * 0.5f)),
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
        TextMeshProUGUI title = Text("Title", card, "사건", 23f, TextAlignmentOptions.Left);
        Place((RectTransform)title.transform, Half,
              new Vector2(-W * 0.332f + W * 0.20f, H * 0.23f), new Vector2(W * 0.40f, H * 0.28f));
        title.color = EventTitle;
        Shrink(title, 15f, 23f);

        // ── 보조 문구 · 갑판 ──────────────────
        TextMeshProUGUI label = Text("Label", card, "", 15f, TextAlignmentOptions.Left);
        Place((RectTransform)label.transform, Half,
              new Vector2(-W * 0.332f + W * 0.24f, 0f), new Vector2(W * 0.48f, H * 0.22f));
        label.color = EventSecondary;
        Shrink(label, 11f, 15f);

        // ── 남은 초 ──────────────────────────
        // 오른쪽 정렬이라 글자가 배지 쪽으로 자라지 않는다. 배지와 겹치지 않게 왼쪽에 둔다.
        TextMeshProUGUI seconds = Text("Seconds", card, "", 21f, TextAlignmentOptions.Right);
        Place((RectTransform)seconds.transform, Half,
              new Vector2(W * 0.255f, H * 0.23f), new Vector2(W * 0.13f, H * 0.26f));
        seconds.fontStyle = FontStyles.Bold;

        // ── 예고 / 발생 배지 ─────────────────
        //
        // 색만으로 단계를 말하면 색각 이상이 있을 때 안 읽힌다. 글자를 같이 둔다.
        RectTransform badge = Rect("Badge", card);
        Place(badge, Half, new Vector2(W * 0.40f, H * 0.23f), new Vector2(W * 0.135f, H * 0.26f));
        Image badgeImage = Img(badge, Sprite4("event-state-badge"), WarnOrange);
        badgeImage.type = Image.Type.Sliced;
        badgeImage.pixelsPerUnitMultiplier = 3f;

        TextMeshProUGUI badgeLabel = Text("Label", badge, "예고", 15f, TextAlignmentOptions.Center);
        badgeLabel.color = OnBadge;
        badgeLabel.fontStyle = FontStyles.Bold;
        Shrink(badgeLabel, 10f, 15f);

        // ── 타이머 ───────────────────────────
        //
        // 셀 것이 없는 사건(선체 파손)에서는 바탕과 채움을 **둘 다** 끈다.
        // 채움만 끄면 빈 홈이 남아서 "0 초 남았다" 로 읽힌다.
        RectTransform track = Rect("Timer", card);
        Place(track, Half, new Vector2(W * 0.055f, -H * 0.31f), new Vector2(W * 0.656f, H * 0.09f));

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
            seconds = seconds,
            badge = badgeImage,
            badgeLabel = badgeLabel,
            timer = timer,
            timerTrack = track.gameObject,
            icon = icon,
        };
    }

    // ------------------------------------------------------------------ 현재 작업

    /// <summary>
    /// 오른쪽 아래. **원형 진행 링**과 키 표시.
    ///
    /// 손이 그려진 가로 막대(`interaction-panel`)로 갔다가 링으로 돌아왔습니다.
    /// 가로 막대는 조타 · 돛처럼 **양쪽으로 차는 게이지를 표현하지 못합니다.**
    /// 링은 위(중앙)에서 좌우로 갈라 채울 수 있어 어느 쪽으로 꺾였는지가 그대로 보입니다.
    ///
    /// 아래 숫자는 전부 **그림 원본 424 × 150 을 기준으로 잰 값**이고,
    /// <see cref="ActionScale"/> 만 곱해 한 번에 키웁니다. 비율이 그대로라 금테가 안 뭉갭니다.
    /// 크기를 다시 바꾸고 싶으면 그 상수 하나만 만지면 됩니다.
    /// </summary>
    private static void BuildAction(RectTransform canvas, Wiring wired)
    {
        const float W = 424f * ActionScale;
        const float H = 150f * ActionScale;

        RectTransform group = Rect("InteractPanel", canvas);
        Place(group, BottomRight, new Vector2(-(Margin + W * 0.5f), Margin + H * 0.5f), new Vector2(W, H));

        Image plaque = Img(group, Sprite("panel-action-plaque"), Color.white);
        plaque.preserveAspect = true;

        // ── 원형 진행 링 ─────────────────────
        //
        // 원본 기준으로 패널 반폭이 212 이고 금테가 20 쯤이라 안쪽 왼쪽 끝은 -192 다.
        // 링을 -120 에 지름 104 로 두면 왼쪽 끝이 -172 라 금테까지 20 이 남는다.
        // 위아래로도 반높이 75 에서 금테를 빼면 52 인데 링 반지름이 52 라 딱 맞는다.
        RectTransform ring = Rect("Ring", group);
        Place(ring, Half, new Vector2(-120f, 0f) * ActionScale, new Vector2(104f, 104f) * ActionScale);
        Img(ring, Sprite("ring-background"), RingInk);

        RectTransform ringTrack = Rect("Track", ring);
        Stretch(ringTrack, 0f, 0f, 0f, 0f);
        Img(ringTrack, Sprite("ring-track"), new Color(1f, 1f, 1f, 0.6f));

        RectTransform ringFill = Rect("Fill", ring);
        Stretch(ringFill, 0f, 0f, 0f, 0f);
        Image gauge = Img(ringFill, Sprite("ring-fill-white"), RingGold);
        gauge.type = Image.Type.Filled;
        gauge.fillMethod = Image.FillMethod.Radial360;
        gauge.fillOrigin = (int)Image.Origin360.Top;
        gauge.fillClockwise = true;
        gauge.fillAmount = 0f;
        wired.InteractGauge = gauge;

        RectTransform ringFrame = Rect("Frame", ring);
        Stretch(ringFrame, 0f, 0f, 0f, 0f);
        Img(ringFrame, Sprite("ring-frame"), Color.white);

        wired.InteractIcon = Icon(ring, "Icon", Sprite("icon-helm"), Vector2.zero, 50f * ActionScale);

        // ── 안내 문구와 키 ────────────────────
        //
        // 링 오른쪽 끝이 -68 이고 금테 안쪽 오른쪽 끝이 192 다. 위아래로도 금테를 빼면
        // -52 ~ +52 다. 그 칸을 글자(위)와 키캡(아래)이 나눠 쓴다.
        //
        // ⚠ **둘의 왼쪽 선을 맞춘다.** 예전에는 키캡만 52 오른쪽에 떠 있어서 글자에도
        //    링에도 안 붙은 채로 혼자 놓인 것처럼 보였다.
        const float TextLeft = -40f;
        const float TextWidth = 216f;
        const float KeyWidth = 72f;

        // ⚠ 세로는 **TopLeft 가 아니라 Left** 다. 안내 문구는 한 줄일 때도 두 줄일 때도
        //    있는데, 위로 붙여 두면 한 줄일 때 아래에 빈 띠가 크게 남는다.
        //    가운데 정렬로 두면 줄 수가 바뀌어도 덩어리가 제자리에 있다.
        wired.InteractLabel = Text("Label", group, "", 21f * ActionScale, TextAlignmentOptions.Left);
        Place((RectTransform)wired.InteractLabel.transform, Half,
              new Vector2(TextLeft + TextWidth * 0.5f, 19f) * ActionScale,
              new Vector2(TextWidth, 66f) * ActionScale);

        // 키캡은 311 × 141 이라 비율이 72 × 34 와 거의 같아 그대로 들어간다.
        // 9-slice 로 늘리지 않으므로 비율을 지키게 해둔다.
        RectTransform key = Rect("Keycap", group);
        Place(key, Half, new Vector2(TextLeft + KeyWidth * 0.5f, -33f) * ActionScale,
              new Vector2(KeyWidth, 34f) * ActionScale);
        Img(key, Sprite("keycap-plain"), Color.white).preserveAspect = true;

        // ⚠ **키캡 글자를 반드시 연결한다.** 여기서 자식을 새로 만들기 때문에
        //    연결하지 않으면 예전처럼 무엇을 하든 "Space" 로 굳는다.
        TextMeshProUGUI keyLabel = Text("Label", key, "Space", 15f * ActionScale, TextAlignmentOptions.Center);
        keyLabel.color = OnBadge;
        Shrink(keyLabel, 11f * ActionScale, 15f * ActionScale);
        wired.InteractKeycap = keyLabel;

        group.gameObject.SetActive(false);
        wired.InteractPanel = group.gameObject;
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
        Set(so, "behindWarning", w.BehindWarning);
        Set(so, "interactPanel", w.InteractPanel);
        Set(so, "interactLabel", w.InteractLabel);
        Set(so, "interactGauge", w.InteractGauge);
        Set(so, "interactIcon", w.InteractIcon);
        Set(so, "interactKeycap", w.InteractKeycap);

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
    /// 패널의 빈 트랙 안에 딱 맞춰 넣는 게이지 채움.
    ///
    /// `position` 과 `size` 는 **패널 원본 픽셀에서 잰 값**을 그대로 받는다.
    /// 여기서 화면 크기 비율(`scale`)을 곱한다. 그래야 패널 크기를 바꿔도
    /// 채움이 트랙을 벗어나지 않는다.
    /// </summary>
    private static Image Fill(RectTransform parent, string name, Vector2 position, Vector2 size,
                              float scale, Color color)
    {
        RectTransform rt = Rect("Fill", parent);
        Place(rt, Half, position * scale, size * scale);

        Image image = Img(rt, Sprite3(name), color);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
        return image;
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

