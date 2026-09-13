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
/// <code>
/// 페이즈      좌상       180×56
/// 배 HP       상단중앙   520×64
/// 시간        상단중앙   184×64
/// 항해        상단중앙   720×68
/// 사건 목록   좌상       410×104 씩, 간격 12
/// 팀원 4명    좌하       570×176
/// 침수        하단중앙   560×94
/// 현재 작업   우하       382×180
/// </code>
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

    // ------------------------------------------------------------------ 색
    // README 가 지정한 값. 바 색은 여기서만 바꾼다.

    private static readonly Color HpGreen = Hex(0x48D889);
    private static readonly Color VoyageBlue = Hex(0x64C9F1);
    private static readonly Color FloodShallow = Hex(0x53BAED);
    private static readonly Color FloodDeep = Hex(0xFF5864);
    private static readonly Color WarnOrange = Hex(0xFFBC55);
    private static readonly Color DangerRed = Hex(0xFF5864);
    private static readonly Color RingGold = Hex(0xFFD05D);
    private static readonly Color Cream = new Color(0.96f, 0.94f, 0.88f);

    /// <summary>밝은 배지 위에 올리는 글자. 흰 글자를 올리면 흰 바탕에 묻혀 안 보인다.</summary>
    private static readonly Color OnBadge = Hex(0x1C2A3A);

    /// <summary>침수가 중간쯤 찼을 때. 하늘색에서 빨강으로 곧장 가면 보라가 된다.</summary>
    private static readonly Color FloodMid = Hex(0xFFBC55);

    private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
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

            BuildPhase(canvas, wired);
            BuildTopBars(canvas, wired);
            BuildEventList(canvas, wired);
            BuildFlood(canvas, wired);
            BuildAction(canvas, wired);
            BuildTeam(canvas, wired);

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
        public TextMeshProUGUI PhaseLabel;
        public Image HpFill;
        public TextMeshProUGUI HpLabel;
        public TextMeshProUGUI TimeLabel;
        public Image ProgressFill;
        public RectTransform ShipMarker;
        public RectTransform ExpectedMarker;
        public RectTransform DelayFill;
        public GameObject BehindWarning;
        public GameObject FloodRoot;
        public Image FloodFill;
        public TextMeshProUGUI FloodLabel;
        public GameObject InteractPanel;
        public TextMeshProUGUI InteractLabel;
        public Image InteractGauge;
        public Image InteractIcon;
        public ShipCoopHud.EventRow[] EventRows;
        public ShipCoopHud.PortraitSlot[] Portraits;
    }

    // ------------------------------------------------------------------ 페이즈

    private static void BuildPhase(RectTransform canvas, Wiring wired)
    {
        RectTransform group = Rect("Phase", canvas);
        Place(group, TopLeft, new Vector2(28f + 90f, -(24f + 28f)), new Vector2(180f, 56f));
        Capsule(group);

        wired.PhaseLabel = Text("Label", group, "출항", 26f, TextAlignmentOptions.Center);
    }

    // ------------------------------------------------------------------ 상단 바

    /// <summary>
    /// HP · 시간 · 항해. README 대로 **폭 720 의 한 그룹** 아래에 둔다.
    /// 그룹 안에서 HP x=0, 시간 x=536, 항해 y=78 이다.
    /// </summary>
    private static void BuildTopBars(RectTransform canvas, Wiring wired)
    {
        RectTransform group = Rect("TopBars", canvas);
        Place(group, TopCenter, new Vector2(0f, -(24f + 34f)), new Vector2(720f, 68f));

        // ── 배 HP ─────────────────────────────
        RectTransform hp = Rect("Hp", group);
        Place(hp, new Vector2(0f, 0.5f), new Vector2(260f, 0f), new Vector2(520f, 64f));
        Capsule(hp);

        RectTransform hpBar = Bar(hp, "HpBar", new Vector2(30f, 0f), new Vector2(440f, 28f),
                                  HpGreen, out Image hpFill);
        wired.HpFill = hpFill;
        wired.HpLabel = Text("HpLabel", hpBar, "100 / 100", 20f, TextAlignmentOptions.Center);

        Icon(hp, "Icon", Sprite("icon-ship"), new Vector2(-228f, 0f), 34f);

        // ── 시간 ─────────────────────────────
        RectTransform time = Rect("Time", group);
        Place(time, new Vector2(0f, 0.5f), new Vector2(536f + 92f, 0f), new Vector2(184f, 64f));
        Capsule(time);

        Icon(time, "Icon", Sprite("icon-clock"), new Vector2(-60f, 0f), 30f);
        wired.TimeLabel = Text("Label", time, "3:00", 28f, TextAlignmentOptions.Center);
        Place((RectTransform)wired.TimeLabel.transform, Half, new Vector2(14f, 0f), new Vector2(120f, 40f));

        // ── 항해 ─────────────────────────────
        RectTransform voyage = Rect("Voyage", canvas);
        Place(voyage, TopCenter, new Vector2(0f, -(102f + 34f)), new Vector2(720f, 68f));
        Capsule(voyage);

        RectTransform track = Bar(voyage, "Track", new Vector2(0f, 0f), new Vector2(640f, 28f),
                                  VoyageBlue, out Image progressFill);
        wired.ProgressFill = progressFill;

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

        wired.ExpectedMarker = Marker(track, "Expected", Sprite("voyage-reference"), new Vector2(8f, 44f), Cream);
        wired.ShipMarker = Marker(track, "Ship", Sprite("icon-ship"), new Vector2(38f, 38f), Color.white);

        // 늦고 있다는 경고. 항해 바 아래.
        RectTransform behind = Rect("BehindWarning", canvas);
        Place(behind, TopCenter, new Vector2(0f, -(176f + 22f)), new Vector2(320f, 40f));
        Img(behind, Sprite("panel-event-background"), new Color(1f, 1f, 1f, 0.9f));
        Text("Label", behind, "⚠ 이 속도면 늦는다!", 22f, TextAlignmentOptions.Center).color = DangerRed;
        behind.gameObject.SetActive(false);
        wired.BehindWarning = behind.gameObject;
    }

    // ------------------------------------------------------------------ 사건 목록

    /// <summary>
    /// 왼쪽 위. **이 게임에서 가장 중요한 UI 다.** (9장)
    ///
    /// 카드 한 장이 410 × 104. README 의 640 × 156 기준 배치를 0.64 로 줄인 값이다.
    /// </summary>
    private static void BuildEventList(RectTransform canvas, Wiring wired)
    {
        RectTransform group = Rect("Events", canvas);
        Place(group, TopLeft, new Vector2(28f + 205f, -(116f + 52f)), new Vector2(410f, 104f));

        wired.EventRows = new ShipCoopHud.EventRow[EventRowCount];

        for (int i = 0; i < EventRowCount; i++)
        {
            wired.EventRows[i] = BuildEventRow(group, i);
        }
    }

    private static ShipCoopHud.EventRow BuildEventRow(RectTransform group, int index)
    {
        RectTransform card = Rect($"Row_{index + 1}", group);
        Place(card, Half, new Vector2(0f, -index * (104f + 12f)), new Vector2(410f, 104f));
        Img(card, Sprite("panel-event-background"), new Color(1f, 1f, 1f, 0.92f));

        RectTransform frame = Rect("Frame", card);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Image frameImage = Img(frame, Sprite("event-frame-warning"), Color.white);

        // 카드 왼쪽 상태선. 예고면 주황, 발생이면 빨강. (9장)
        RectTransform accent = Rect("Accent", card);
        Place(accent, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(10f, 84f));
        Image accentImage = Img(accent, Sprite("event-accent-warning"), WarnOrange);

        // 아래 좌표는 README 의 640 × 156 기준 배치를 410 × 104 로 줄인 값이다.
        //   아이콘 (20,36,80,80) · 제목+설명 (118,20,382,90) · 카운트다운 (118,128,314,8)
        Image icon = Icon(card, "Icon", null, new Vector2(-167f, 3f), 51f);

        TextMeshProUGUI label = Text("Label", card, "⚠ 사건", 21f, TextAlignmentOptions.TopLeft);
        Place((RectTransform)label.transform, Half, new Vector2(28f, 10f), new Vector2(314f, 58f));

        // 남은 시간. 카드 아래쪽 얇은 줄. 프레임 안쪽으로 들어와야 한다.
        RectTransform timerTrack = Rect("Timer", card);
        Place(timerTrack, Half, new Vector2(28f, -33f), new Vector2(314f, 6f));
        Img(timerTrack, Sprite("bar-track"), new Color(1f, 1f, 1f, 0.5f));

        RectTransform timerFill = Rect("Fill", timerTrack);
        Stretch(timerFill, 2f, 2f, 2f, 2f);
        Image timer = Img(timerFill, Sprite("bar-fill-white"), WarnOrange);
        timer.type = Image.Type.Filled;
        timer.fillMethod = Image.FillMethod.Horizontal;
        timer.fillOrigin = (int)Image.OriginHorizontal.Left;
        timer.fillAmount = 1f;

        card.gameObject.SetActive(false);

        return new ShipCoopHud.EventRow
        {
            root = card.gameObject,
            label = label,
            timer = timer,
            icon = icon,
            accent = accentImage,
        };
    }

    // ------------------------------------------------------------------ 침수

    private static void BuildFlood(RectTransform canvas, Wiring wired)
    {
        RectTransform group = Rect("Flood", canvas);
        Place(group, BottomCenter, new Vector2(0f, 1080f - 958f - 47f), new Vector2(560f, 94f));
        Img(group, Sprite("panel-flood-background"), new Color(1f, 1f, 1f, 0.9f));

        RectTransform frame = Rect("Frame", group);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Img(frame, Sprite("panel-flood-frame"), Color.white);

        Icon(group, "Icon", Sprite("icon-water"), new Vector2(-244f, 0f), 34f);

        RectTransform bar = Bar(group, "Bar", new Vector2(18f, 14f), new Vector2(460f, 24f),
                                FloodShallow, out Image fill);
        wired.FloodFill = fill;

        wired.FloodLabel = Text("Label", group, "침수 0%", 20f, TextAlignmentOptions.Center);
        Place((RectTransform)wired.FloodLabel.transform, Half, new Vector2(18f, -20f), new Vector2(480f, 30f));

        // 물이 0 이면 통째로 숨긴다. 늘 떠 있으면 "물은 원래 있는 것" 이 된다. (9장)
        group.gameObject.SetActive(false);
        wired.FloodRoot = group.gameObject;
    }

    // ------------------------------------------------------------------ 현재 작업

    /// <summary>
    /// 오른쪽 아래. 원형 진행 링과 키 표시. (README)
    ///
    /// 하단 중앙에서 **우하단으로 옮겼습니다.** 가운데 아래에 두면 갑판에 내려놓은
    /// 물건을 가립니다. 4장에서 아무 데나 내려놓을 수 있게 되면서 생긴 문제입니다.
    /// </summary>
    private static void BuildAction(RectTransform canvas, Wiring wired)
    {
        // 화면 오른쪽·아래에서 40px 씩 띄운다. README 는 28 인데 그러면 모서리에 붙어 보인다.
        RectTransform group = Rect("InteractPanel", canvas);
        Place(group, BottomRight, new Vector2(-(40f + 191f), 40f + 90f), new Vector2(382f, 180f));
        Img(group, Sprite("panel-action-background"), new Color(1f, 1f, 1f, 0.9f));

        RectTransform frame = Rect("Frame", group);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Img(frame, Sprite("panel-action-frame"), Color.white);

        // ── 원형 진행 링 ─────────────────────
        //
        // 패널 반폭이 191 이다. 링을 -118 에 두면 왼쪽 끝이 -182 라 여백이 9px 뿐이고,
        // **패널 모서리가 둥글어서 링이 밖으로 삐져나온 것처럼 보인다.**
        // 지름을 줄이고 안쪽으로 당겨 여백을 28px 로 둔다.
        RectTransform ring = Rect("Ring", group);
        Place(ring, Half, new Vector2(-107f, 0f), new Vector2(112f, 112f));
        Img(ring, Sprite("ring-background"), Color.white);

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

        wired.InteractIcon = Icon(ring, "Icon", Sprite("icon-helm"), Vector2.zero, 54f);

        // ── 안내 문구와 키 ────────────────────
        //
        // 링 오른쪽 끝이 -51 이고 패널 오른쪽 끝이 191 이다. 그 사이 140 폭을
        // 글자와 키가 나눠 쓴다. 글자는 위, 키는 아래.
        wired.InteractLabel = Text("Label", group, "", 21f, TextAlignmentOptions.TopLeft);
        Place((RectTransform)wired.InteractLabel.transform, Half, new Vector2(56f, 22f), new Vector2(180f, 84f));

        RectTransform key = Rect("Keycap", group);
        Place(key, Half, new Vector2(0f, -54f), new Vector2(72f, 34f));
        Img(key, Sprite("keycap"), Color.white);
        Text("Label", key, "Space", 15f, TextAlignmentOptions.Center).color = OnBadge;

        group.gameObject.SetActive(false);
        wired.InteractPanel = group.gameObject;
    }

    // ------------------------------------------------------------------ 팀원

    private static void BuildTeam(RectTransform canvas, Wiring wired)
    {
        // **왼쪽 끝을 기준으로 둔다.** 사람 수에 따라 폭이 줄어드는데,
        // 가운데 기준이면 줄어들 때 패널이 왼쪽으로 밀려 화면 밖으로 나간다.
        RectTransform row = Rect("TeamRow", canvas);
        row.anchorMin = BottomLeft;
        row.anchorMax = BottomLeft;
        row.pivot = new Vector2(0f, 0.5f);
        row.anchoredPosition = new Vector2(28f, 1080f - 876f - 88f);
        row.sizeDelta = new Vector2(570f, 176f);

        Img(row, Sprite("panel-team-background"), new Color(1f, 1f, 1f, 0.85f));

        RectTransform frame = Rect("Frame", row);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Img(frame, Sprite("panel-team-frame"), Color.white);

        string[] frames = { "portrait-frame-red", "portrait-frame-yellow", "portrait-frame-green", "portrait-frame-purple" };
        string[] faces = { "portrait-captain", "portrait-wig", "portrait-jester", "portrait-diver" };

        wired.Portraits = new ShipCoopHud.PortraitSlot[4];

        for (int i = 0; i < 4; i++)
        {
            wired.Portraits[i] = BuildSlot(row, i, frames[i], faces[i]);
        }
    }

    private static ShipCoopHud.PortraitSlot BuildSlot(RectTransform row, int index, string frameSprite, string faceSprite)
    {
        // 칸도 왼쪽 끝 기준. 패널이 줄어도 있던 자리에 그대로 있어야 한다.
        RectTransform slot = Rect($"Slot_{index + 1}", row);
        slot.anchorMin = new Vector2(0f, 0.5f);
        slot.anchorMax = new Vector2(0f, 0.5f);
        slot.pivot = Half;
        slot.anchoredPosition = new Vector2(13f + 68f + index * 136f, 0f);
        slot.sizeDelta = new Vector2(128f, 168f);

        RectTransform plate = Rect("Backplate", slot);
        Place(plate, Half, new Vector2(0f, 16f), new Vector2(100f, 100f));
        Img(plate, Sprite("portrait-backplate"), Color.white);

        Mask mask = plate.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform face = Rect("Face", plate);
        Stretch(face, 0f, 0f, 0f, 0f);
        Img(face, Sprite(faceSprite), Color.white);

        // 색 테두리는 마스크 밖에 둔다. 안에 두면 같이 잘린다.
        RectTransform ring = Rect("Frame", slot);
        Place(ring, Half, new Vector2(0f, 16f), new Vector2(108f, 108f));
        Image ringImage = Img(ring, Sprite(frameSprite), Color.white);

        // P1 ~ P4 배지는 두지 않는다. 색 테두리 네 가지가 이미 사람을 구분한다.
        // 같은 것을 두 번 말하면 읽을 것만 늘어난다.

        TextMeshProUGUI deck = Text("Deck", slot, "—", 20f, TextAlignmentOptions.Center);
        Place((RectTransform)deck.transform, Half, new Vector2(0f, -56f), new Vector2(128f, 30f));

        // 🆘 — 그림 대신 **빨간 글씨**를 초상화 아래쪽에 겹친다.
        //
        // 경고 아이콘을 초상화 옆에 붙여봤더니 무슨 뜻인지 안 읽혔다.
        // 얼굴 위에 빨갛게 덮이는 편이 훨씬 빨리 눈에 들어온다.
        RectTransform help = Rect("HelpBadge", slot);
        Place(help, Half, new Vector2(0f, -18f), new Vector2(104f, 30f));
        Img(help, Sprite("portrait-backplate"), new Color(0f, 0f, 0f, 0.55f));

        TextMeshProUGUI helpText = Text("Label", help, "도움!", 20f, TextAlignmentOptions.Center);
        helpText.color = DangerRed;
        helpText.fontStyle = FontStyles.Bold;

        help.gameObject.SetActive(false);

        return new ShipCoopHud.PortraitSlot
        {
            root = slot.gameObject,
            frame = ringImage,
            deckLabel = deck,
            helpBadge = help.gameObject,
        };
    }

    // ------------------------------------------------------------------ 연결

    private static void Connect(ShipCoopHud hud, Wiring w)
    {
        var so = new SerializedObject(hud);

        Set(so, "phaseLabel", w.PhaseLabel);
        Set(so, "hpFill", w.HpFill);
        Set(so, "hpLabel", w.HpLabel);
        Set(so, "timeLabel", w.TimeLabel);
        Set(so, "progressFill", w.ProgressFill);
        Set(so, "shipMarker", w.ShipMarker);
        Set(so, "expectedMarker", w.ExpectedMarker);
        Set(so, "delayFill", w.DelayFill);
        Set(so, "behindWarning", w.BehindWarning);
        Set(so, "floodRoot", w.FloodRoot);
        Set(so, "floodFill", w.FloodFill);
        Set(so, "floodLabel", w.FloodLabel);
        Set(so, "interactPanel", w.InteractPanel);
        Set(so, "interactLabel", w.InteractLabel);
        Set(so, "interactGauge", w.InteractGauge);
        Set(so, "interactIcon", w.InteractIcon);

        // 사건 아이콘 다섯 종류. 암초와 파도가 같은 그림이면 목록에서 구분이 안 된다.
        Set(so, "eventIconHull", Sprite("icon-hull-breach"));
        Set(so, "eventIconSail", Sprite("icon-sails"));
        Set(so, "eventIconCannon", Sprite("icon-cannon"));
        Set(so, "eventIconHelm", Sprite("icon-helm"));
        Set(so, "eventIconReef", Sprite("icon-reef"));
        Set(so, "eventIconWave", Sprite("icon-water"));
        Set(so, "eventIconEnemy", Sprite("icon-enemy-ship"));

        Set(so, "taskIconHelm", Sprite("icon-helm"));
        Set(so, "taskIconSails", Sprite("icon-sails"));
        Set(so, "taskIconCannon", Sprite("icon-cannon"));
        Set(so, "taskIconRepair", Sprite("icon-repair"));

        so.FindProperty("floodShallow").colorValue = FloodShallow;
        so.FindProperty("floodDeep").colorValue = FloodDeep;
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
        }

        SerializedProperty slots = so.FindProperty("portraits");
        slots.arraySize = w.Portraits.Length;
        for (int i = 0; i < w.Portraits.Length; i++)
        {
            SerializedProperty item = slots.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("root").objectReferenceValue = w.Portraits[i].root;
            item.FindPropertyRelative("frame").objectReferenceValue = w.Portraits[i].frame;
            item.FindPropertyRelative("deckLabel").objectReferenceValue = w.Portraits[i].deckLabel;
            item.FindPropertyRelative("helpBadge").objectReferenceValue = w.Portraits[i].helpBadge;
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
