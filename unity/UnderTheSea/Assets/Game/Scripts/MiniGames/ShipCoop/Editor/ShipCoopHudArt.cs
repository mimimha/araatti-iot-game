using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배 협동 HUD 프리팹을 그림 에셋으로 다시 짠다. (art/hud 02 · 03 · 04 묶음)
///
/// 지금까지 HUD 는 스프라이트 없이 색 사각형만으로 되어 있었다.
/// 이 도구가 art/hud 에서 온 PNG 를 Sprite 로 맞춰 들여오고,
/// 프리팹의 자식을 전부 새로 만들어 각 그림을 제자리에 놓는다.
///
/// 크기는 에셋 묶음의 README 가 준 원본 좌표를 그대로 축소해서 쓴다.
/// 예를 들어 사건 카드는 960 × 240 이 원본이고 여기서는 440 × 110 (× 0.4583) 이다.
/// 원본 비율을 지켜야 둥근 끝과 외곽선이 찌그러지지 않는다.
///
/// 되풀이해서 돌려도 된다. 자식을 지우고 다시 만들기 때문에
/// 에디터에서 손으로 옮긴 위치는 사라진다. 위치를 손보고 굳히려면
/// 이 파일의 숫자를 고치는 편이 낫다.
///
/// Tools > 아라아띠 > 배 협동 HUD 에 그림 입히기
/// </summary>
public static class ShipCoopHudArt
{
    private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHud.prefab";
    private const string ArtRoot = "Assets/Game/Art/UI/ShipCoopHud";

    // 원본 대비 축소 비율. 원본 크기를 그대로 곱해서 쓴다.
    private const float BarScale = 560f / 1024f;   // HP · 항해 바
    private const float CardScale = 440f / 960f;   // 사건 카드

    private static readonly Color White = Color.white;
    private static readonly Color ProgressBlue = new Color(0.45f, 0.72f, 0.95f, 0.9f);
    private static readonly Color WarnOrange = new Color(0.95f, 0.55f, 0.45f, 1f);
    private static readonly Color HpGreen = new Color(0.45f, 0.85f, 0.55f, 1f);

    private static Dictionary<string, Sprite> _sprites;
    private static TMP_FontAsset _font;

    [MenuItem("Tools/아라아띠/배 협동 HUD 에 그림 입히기")]
    public static void Apply()
    {
        ImportSprites();
        BuildPrefab();
    }

    /// <summary>배치 모드에서 부른다. 실패하면 종료 코드를 남긴다.</summary>
    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
            Debug.Log("[HUD 그림] 끝났다.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HUD 그림] 실패: {e}");
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------ 들여오기

    /// <summary>
    /// PNG 를 UI 스프라이트로 맞춘다. 묶음 README 가 지정한 설정이다.
    /// 압축을 끄는 이유는 첫 확인에서 외곽선이 뭉개지는지 보기 위해서다.
    /// </summary>
    private static void ImportSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
        int touched = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
            touched++;
        }

        Debug.Log($"[HUD 그림] 스프라이트 {touched} 장을 맞췄다.");
    }

    private static void LoadSprites()
    {
        _sprites = new Dictionary<string, Sprite>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                _sprites[Path.GetFileNameWithoutExtension(path)] = sprite;
            }
        }
    }

    /// <summary>이름으로 그림을 꺼낸다. 없으면 바로 멈춘다. 조용히 빈 칸으로 두면 나중에 찾기 어렵다.</summary>
    private static Sprite S(string name)
    {
        if (_sprites.TryGetValue(name, out Sprite sprite))
        {
            return sprite;
        }

        throw new FileNotFoundException($"{ArtRoot} 아래에 {name} 이 없다.");
    }

    // ------------------------------------------------------------------ 프리팹

    private static void BuildPrefab()
    {
        LoadSprites();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            var hud = root.GetComponent<ShipCoopHud>();
            if (hud == null)
            {
                throw new MissingComponentException($"{PrefabPath} 에 ShipCoopHud 가 없다.");
            }

            // 글꼴은 지금 쓰던 것을 그대로 이어받는다. 한글이 깨지지 않게.
            var sample = root.GetComponentInChildren<TextMeshProUGUI>(true);
            _font = sample != null ? sample.font : null;

            var canvas = (RectTransform)root.transform;
            for (int i = canvas.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(canvas.GetChild(i).gameObject);
            }

            var wiring = new Wiring();
            BuildTime(canvas, wiring);
            BuildHealth(canvas, wiring);
            BuildVoyage(canvas, wiring);
            BuildAlerts(canvas, wiring);
            BuildInteract(canvas, wiring);

            Connect(hud, wiring);
            SyncTextColors(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[HUD 그림] 프리팹을 다시 저장했다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// TMP 는 글자색을 m_fontColor 와 m_fontColor32 두 곳에 들고 있다.
    /// 코드로 color 만 넣으면 m_fontColor32 는 흰색인 채로 저장된다.
    ///
    /// 그 상태로 프리팹을 씬에 놓으면, TMP 가 실행 중에 둘을 맞추면서 나온 값이
    /// 프리팹과 달라 보여 **씬 파일에 색 override 가 한 줄 적힌다.**
    /// 보이는 색은 똑같은데 씬 diff 만 지저분해진다. 그래서 굽는 김에 맞춰 둔다.
    /// </summary>
    private static void SyncTextColors(GameObject root)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            Color32 c = text.color;
            uint packed = (uint)c.r | ((uint)c.g << 8) | ((uint)c.b << 16) | ((uint)c.a << 24);

            var so = new SerializedObject(text);
            so.FindProperty("m_fontColor32").FindPropertyRelative("rgba").uintValue = packed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>만든 것들을 스크립트 칸에 이어 주려고 들고 다니는 묶음.</summary>
    private class Wiring
    {
        public Image HpFill;
        public TextMeshProUGUI HpLabel;
        public TextMeshProUGUI TimeLabel;
        public Image ProgressFill;
        public RectTransform ShipMarker;
        public RectTransform ExpectedMarker;
        public RectTransform DelayFill;
        public GameObject BehindWarning;

        public readonly List<GameObject> RowRoots = new List<GameObject>();
        public readonly List<TextMeshProUGUI> RowLabels = new List<TextMeshProUGUI>();
        public readonly List<Image> RowTimers = new List<Image>();
        public readonly List<Image> RowIcons = new List<Image>();

        public GameObject InteractPanel;
        public TextMeshProUGUI InteractLabel;
        public Image InteractGauge;
        public Image InteractIcon;
    }

    // ------------------------------------------------------------------ 남은 시간

    private static void BuildTime(RectTransform canvas, Wiring w)
    {
        // timer-label 은 숫자가 들어 있지 않은 빈 판이다. 숫자는 TMP 가 얹는다.
        RectTransform plate = Rect("TimeLabel", canvas);
        Place(plate, TopRight, new Vector2(-150f, -50f), new Vector2(200f, 60f));
        Img(plate, S("timer-label"), White);

        w.TimeLabel = Text("Value", plate, "5:00", 42f, TextAlignmentOptions.Center);
    }

    // ------------------------------------------------------------------ 배 HP

    private static void BuildHealth(RectTransform canvas, Wiring w)
    {
        Caption(canvas, "HpCaption", "배 HP", new Vector2(-694f, -120f));

        // 원본 1024 × 96 을 그대로 줄인다. 안쪽 채움 자리는 사방 12.
        float h = 96f * BarScale;
        float inset = 12f * BarScale;

        RectTransform track = Rect("HpTrack", canvas);
        Place(track, TopRight, new Vector2(-330f, -120f), new Vector2(560f, h));
        Img(track, S("hp-track"), White);

        // 흰 캡슐(hp-mask)에 색을 입힌다. hp-fill-green 은 이미 초록이라
        // 스크립트가 주황·빨강으로 바꿀 때 색이 섞여 탁해진다.
        // 색은 스크립트가 남은 양에 따라 다시 칠한다. 여기 초록은 에디터에서 보라고 넣는다.
        // 흰색으로 두면 그 위의 흰 글자가 묻혀 사람이 잘못 만든 줄 안다.
        RectTransform fill = Rect("HpFill", track);
        Stretch(fill, inset, inset, inset, inset);
        w.HpFill = Img(fill, S("hp-mask"), HpGreen);
        Filled(w.HpFill, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);

        RectTransform frame = Rect("HpFrame", track);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Img(frame, S("hp-frame"), White);

        w.HpLabel = Text("HpLabel", track, "100 / 100", 22f, TextAlignmentOptions.Center);
    }

    // ------------------------------------------------------------------ 항해

    private static void BuildVoyage(RectTransform canvas, Wiring w)
    {
        Caption(canvas, "ProgressCaption", "진행도", new Vector2(-694f, -215f));

        float h = 64f * BarScale;          // 35
        float side = 24f * BarScale;       // 트랙 안쪽이 시작하는 자리

        RectTransform track = Rect("ProgressTrack", canvas);
        Place(track, TopRight, new Vector2(-330f, -215f), new Vector2(560f, h));
        Img(track, S("voyage-track"), White);

        // 트랙 → 채움 → 프레임 → 기준선 → 배. README 가 준 순서다.
        RectTransform inner = Rect("Inner", track);
        Stretch(inner, side, 0f, side, 0f);

        RectTransform fill = Rect("ProgressFill", inner);
        Stretch(fill, 0f, 8f * BarScale, 0f, 8f * BarScale);
        w.ProgressFill = Img(fill, S("hp-mask"), ProgressBlue);
        Filled(w.ProgressFill, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);

        // 배와 기준선 사이. 늦은 만큼만 벌어지므로 처음에는 폭이 0 이다.
        w.DelayFill = Rect("DelayFill", inner);
        w.DelayFill.anchorMin = new Vector2(0f, 0.5f);
        w.DelayFill.anchorMax = new Vector2(0f, 0.5f);
        w.DelayFill.pivot = Half;
        w.DelayFill.anchoredPosition = Vector2.zero;
        w.DelayFill.sizeDelta = new Vector2(0f, 32f * BarScale);
        Img(w.DelayFill, S("voyage-delay-fill"), White);

        RectTransform frame = Rect("VoyageFrame", track);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Img(frame, S("voyage-frame"), White);

        // 기준선과 배는 트랙 밖으로 튀어나온다. 그래서 프레임 위에 따로 둔다.
        RectTransform markers = Rect("Markers", track);
        Stretch(markers, side, 0f, side, 0f);

        w.ExpectedMarker = Rect("ExpectedMarker", markers);
        Place(w.ExpectedMarker, LeftMiddle, Vector2.zero,
              new Vector2(32f * BarScale, 112f * BarScale));
        Img(w.ExpectedMarker, S("voyage-reference-marker"), White);

        w.ShipMarker = Rect("ShipMarker", markers);
        Place(w.ShipMarker, LeftMiddle, new Vector2(0f, 28f * BarScale),
              new Vector2(104f * BarScale, 104f * BarScale));
        Img(w.ShipMarker, S("icon-ship"), White);

        // 출발지(icon-harbour)와 목적지(icon-island)는 넣지 않는다.
        // 바 끝에 붙이면 34px 밖에 안 되어 뭉개지고, 바 자체에 이미 끝이 있어
        // 알려주는 것이 없다. 에셋은 남겨 두었으니 쓸 자리가 생기면 다시 붙인다.

        TextMeshProUGUI expected =
            Text("ExpectedCaption", track, "지금 있어야 할 위치 ┊", 15f, TextAlignmentOptions.Center);
        Place((RectTransform)expected.transform, new Vector2(0.5f, 0f),
              new Vector2(0f, -20f), new Vector2(300f, 22f));

        RectTransform warn = Rect("BehindWarning", canvas);
        Place(warn, TopRight, new Vector2(-330f, -290f), new Vector2(560f, 36f));
        TextMeshProUGUI warnText =
            Text("Label", warn, "⚠ 이 속도면 늦는다!", 26f, TextAlignmentOptions.Right);
        warnText.color = WarnOrange;
        w.BehindWarning = warn.gameObject;
    }

    // ------------------------------------------------------------------ 사건 알림

    /// <summary>
    /// 사건 카드 4장. 원본 960 × 240 을 0.4583 으로 줄여 440 × 110 이다.
    /// 안쪽 좌표는 전부 원본 좌표 × CardScale 이라 README 와 그대로 맞춘다.
    /// </summary>
    private static void BuildAlerts(RectTransform canvas, Wiring w)
    {
        const float cardW = 440f;
        const float cardH = 110f;
        const float pitch = 120f;

        for (int i = 0; i < 4; i++)
        {
            RectTransform row = Rect($"EventRow_{i + 1:00}", canvas);
            Place(row, TopLeft, new Vector2(240f, -(115f + pitch * i)), new Vector2(cardW, cardH));
            Img(row, S("alert-background"), White);

            RectTransform accent = Rect("Accent", row);
            FromTopLeft(accent, 24f, 28f, 24f, 184f);
            Img(accent, S("alert-accent-red"), White);

            RectTransform slot = Rect("IconSlot", row);
            FromTopLeft(slot, 60f, 24f, 192f, 192f);
            Img(slot, S("alert-icon-slot"), White);

            RectTransform icon = Rect("Icon", row);
            FromTopLeft(icon, 76f, 40f, 160f, 160f);
            Image iconImage = Img(icon, S("icon-hull-breach"), White);
            iconImage.preserveAspect = true;

            TextMeshProUGUI label = Text("Label", row, "⚠ 사건", 20f, TextAlignmentOptions.Left);
            FromTopLeft((RectTransform)label.transform, 280f, 62f, 660f, 96f);

            RectTransform timerTrack = Rect("TimerTrack", row);
            FromTopLeft(timerTrack, 280f, 188f, 640f, 16f);
            Img(timerTrack, S("alert-countdown-track"), White);

            RectTransform timerFill = Rect("TimerFill", timerTrack);
            Stretch(timerFill, 0f, 0f, 0f, 0f);
            Image timer = Img(timerFill, S("alert-countdown-fill"), White);
            Filled(timer, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);

            RectTransform frame = Rect("Frame", row);
            Stretch(frame, 0f, 0f, 0f, 0f);
            Img(frame, S("alert-frame"), White);

            w.RowRoots.Add(row.gameObject);
            w.RowLabels.Add(label);
            w.RowTimers.Add(timer);
            w.RowIcons.Add(iconImage);
        }
    }

    /// <summary>원본 카드(960 × 240) 좌상단 기준 좌표를 그대로 받아 놓는다.</summary>
    private static void FromTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = TopLeft;
        rt.anchorMax = TopLeft;
        rt.pivot = Half;
        rt.sizeDelta = new Vector2(w * CardScale, h * CardScale);
        rt.anchoredPosition = new Vector2((x + w * 0.5f) * CardScale, -(y + h * 0.5f) * CardScale);
    }

    // ------------------------------------------------------------------ 상호작용

    /// <summary>
    /// 아래 가운데의 원형 작업 게이지. 묶음 04 의 구성이다.
    /// 아래에서 위로 배경 → 빈 링 → 채운 링 → 테두리 → 작업 그림.
    /// </summary>
    private static void BuildInteract(RectTransform canvas, Wiring w)
    {
        const float ring = 230f;

        RectTransform panel = Rect("InteractPanel", canvas);
        Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(300f, 320f));
        w.InteractPanel = panel.gameObject;

        RectTransform gauge = Rect("Gauge", panel);
        Place(gauge, new Vector2(0.5f, 1f), new Vector2(0f, -ring * 0.5f), new Vector2(ring, ring));

        RectTransform bg = Rect("Background", gauge);
        Stretch(bg, 0f, 0f, 0f, 0f);
        Img(bg, S("task-background"), White);

        RectTransform trackRing = Rect("Track", gauge);
        Stretch(trackRing, 0f, 0f, 0f, 0f);
        Img(trackRing, S("task-progress-track"), White);

        RectTransform fillRing = Rect("Fill", gauge);
        Stretch(fillRing, 0f, 0f, 0f, 0f);
        w.InteractGauge = Img(fillRing, S("task-progress-fill"), White);
        Filled(w.InteractGauge, Image.FillMethod.Radial360, (int)Image.Origin360.Top);
        w.InteractGauge.fillClockwise = true;

        RectTransform frame = Rect("Frame", gauge);
        Stretch(frame, 0f, 0f, 0f, 0f);
        Img(frame, S("task-frame"), White);

        RectTransform icon = Rect("Icon", gauge);
        Place(icon, Half, Vector2.zero, new Vector2(115f, 115f));
        w.InteractIcon = Img(icon, S("icon-helm"), White);
        w.InteractIcon.preserveAspect = true;

        // task-label 도 글자가 들어 있지 않은 빈 판이다. 원본 512 × 128.
        RectTransform plate = Rect("LabelPlate", panel);
        Place(plate, new Vector2(0.5f, 1f), new Vector2(0f, -(ring + 8f + 37.5f)),
              new Vector2(300f, 75f));
        Img(plate, S("task-label"), White);

        w.InteractLabel = Text("Label", plate, "조타  —  Space", 26f, TextAlignmentOptions.Center);
    }

    // ------------------------------------------------------------------ 이어 붙이기

    private static void Connect(ShipCoopHud hud, Wiring w)
    {
        var so = new SerializedObject(hud);

        so.FindProperty("hpFill").objectReferenceValue = w.HpFill;
        so.FindProperty("hpLabel").objectReferenceValue = w.HpLabel;
        so.FindProperty("timeLabel").objectReferenceValue = w.TimeLabel;
        so.FindProperty("progressFill").objectReferenceValue = w.ProgressFill;
        so.FindProperty("shipMarker").objectReferenceValue = w.ShipMarker;
        so.FindProperty("expectedMarker").objectReferenceValue = w.ExpectedMarker;
        so.FindProperty("delayFill").objectReferenceValue = w.DelayFill;
        so.FindProperty("behindWarning").objectReferenceValue = w.BehindWarning;

        SerializedProperty rows = so.FindProperty("eventRows");
        rows.arraySize = w.RowRoots.Count;

        for (int i = 0; i < w.RowRoots.Count; i++)
        {
            SerializedProperty row = rows.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("root").objectReferenceValue = w.RowRoots[i];
            row.FindPropertyRelative("label").objectReferenceValue = w.RowLabels[i];
            row.FindPropertyRelative("timer").objectReferenceValue = w.RowTimers[i];
            row.FindPropertyRelative("icon").objectReferenceValue = w.RowIcons[i];
        }

        // 사건은 "무엇을 해야 넘기는가" 로 그림을 고른다.
        // 암초와 파도는 둘 다 조타라서 조타륜을 함께 쓴다.
        so.FindProperty("eventIconHull").objectReferenceValue = S("icon-hull-breach");
        so.FindProperty("eventIconSail").objectReferenceValue = S("icon-sail-torn");
        so.FindProperty("eventIconCannon").objectReferenceValue = S("icon-cannon");
        so.FindProperty("eventIconHelm").objectReferenceValue = S("icon-helm");

        so.FindProperty("interactPanel").objectReferenceValue = w.InteractPanel;
        so.FindProperty("interactLabel").objectReferenceValue = w.InteractLabel;
        so.FindProperty("interactGauge").objectReferenceValue = w.InteractGauge;
        so.FindProperty("interactIcon").objectReferenceValue = w.InteractIcon;

        so.FindProperty("taskIconHelm").objectReferenceValue = S("icon-helm");
        so.FindProperty("taskIconSails").objectReferenceValue = S("icon-sails");
        so.FindProperty("taskIconCannon").objectReferenceValue = S("icon-cannon");
        so.FindProperty("taskIconRepair").objectReferenceValue = S("icon-repair");

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ 손도구

    private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

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
        image.raycastTarget = false;   // HUD 는 누를 것이 없다. 뒤를 막지 않게 한다.
        return image;
    }

    private static void Filled(Image image, Image.FillMethod method, int origin)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = method;
        image.fillOrigin = origin;
        image.fillAmount = 1f;
    }

    /// <summary>부모를 꽉 채우는 글자. 위치를 따로 잡고 싶으면 뒤에서 Place 로 덮는다.</summary>
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
        tmp.color = White;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>heading-label 판 위에 제목을 얹은 묶음.</summary>
    private static void Caption(RectTransform canvas, string name, string text, Vector2 position)
    {
        RectTransform plate = Rect(name, canvas);
        Place(plate, TopRight, position, new Vector2(160f, 48f));
        Img(plate, S("heading-label"), White);
        Text("Label", plate, text, 20f, TextAlignmentOptions.Center);
    }
}
