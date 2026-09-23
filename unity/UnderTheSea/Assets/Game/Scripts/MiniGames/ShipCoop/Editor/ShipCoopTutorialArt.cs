using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배 협동 **시작 안내 팝업**을 그림 한 장(<c>gadogado-popup.png</c>)으로 바꿔 놓는다.
///
/// 옛 팝업은 짙은 사각형 위에 <see cref="TextMeshProUGUI"/> 본문을 얹은 것이었다.
/// 지금은 제목 · 본문 · 아이콘 다섯 칸 · 테두리 · 오른쪽 "Enter 닫기" 까지
/// **한 장에 그려져 있어서**, UI 는 <see cref="Image"/> 하나면 된다.
/// 그림에 비어 있는 곳은 왼쪽 아래 한 줄뿐이고, 거기에 카운트다운을 올린다.
///
/// <code>
/// Tutorial            (ShipCoopTutorialView)
/// └ Panel             화면 전체, 검정 0.55  ← 딤
///   └ Box             1350×900, 그림 한 장  ← 3:2
///     ├ Body          끈다. 그림에 본문이 들어갔다
///     └ Footer        "10초 후 자동으로 닫힙니다."
/// </code>
///
/// 표시 크기 1350×900 은 1920×1080 기준이다. CanvasScaler 가 ScaleWithScreenSize
/// 라서 1280×720 에서는 900×600 으로 같이 줄어든다. 그림이 3:2 (1536×1024) 이고
/// 칸도 3:2 라서 Preserve Aspect 를 켜도 남는 여백이 없다.
///
/// Footer 의 앵커는 **투명 여백을 포함한 그림 전체** 기준이다. 그림 좌표로 옮기면
/// 가로 200–1075 px, 세로 아래에서 76–136 px — 오른쪽 "Enter 닫기" 앞에서 끊긴다.
///
/// 글꼴 23 은 원본 1536 px 기준 26 px 을 표시 크기로 환산한 것이다 (26 × 1350/1536).
///
/// 여러 번 돌려도 된다. 이미 바뀐 것은 그대로 두고 값만 다시 맞춘다.
/// <b>팝업이 아예 없으면 뼈대부터 짓는다</b> — 그래서 <see cref="ShipCoopHudV2Art"/> 가
/// HUD 를 다시 지은 직후에 이걸 부르면 팝업이 딸려 온다.
///
/// Tools > 아라아띠 > 배 협동 안내 팝업에 그림 입히기
/// </summary>
public static class ShipCoopTutorialArt
{
    private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHud.prefab";
    private const string SpritePath = "Assets/Game/Art/UI/ShipCoopHud/Tutorial/gadogado-popup.png";

    /// <summary>1920×1080 에서의 표시 크기. 그림과 같은 3:2.</summary>
    private static readonly Vector2 BoxSize = new Vector2(1350f, 900f);

    /// <summary>그림 전체(투명 여백 포함) 기준. 왼쪽 아래 빈 줄.</summary>
    private static readonly Vector2 FooterAnchorMin = new Vector2(0.13f, 0.074f);
    private static readonly Vector2 FooterAnchorMax = new Vector2(0.70f, 0.133f);

    /// <summary>#D4BB8C. 그림의 밧줄 · 나무와 같은 계열의 따뜻한 연금색.</summary>
    private static readonly Color FooterGold = new Color32(0xD4, 0xBB, 0x8C, 0xFF);

    private const float FooterFontSize = 23f;
    private const float AutoHideSeconds = 10f;

    [MenuItem("Tools/아라아띠/배 협동 안내 팝업에 그림 입히기")]
    public static void Apply()
    {
        if (!ImportSprite(out Sprite sprite))
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"[안내 팝업] 프리팹을 못 열었다: {PrefabPath}");
            return;
        }

        try
        {
            if (!Rebuild(root, sprite))
            {
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[안내 팝업] 그림 한 장으로 바꿨다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 이미 열어 둔 프리팹 뿌리에 팝업을 **보장한다.** 없으면 짓고, 있으면 값만 다시 맞춘다.
    ///
    /// <see cref="ShipCoopHudV2Art"/> 가 HUD 를 다시 지을 때 마지막에 부른다. 그쪽은 캔버스
    /// 자식을 전부 지우고 네 덩이만 다시 짓기 때문에, 이걸 안 부르면 팝업이 사라진다.
    /// 실제로 그렇게 사라진 적이 있다 — 팝업은 손으로 만든 것이라 빌더가 지을 줄 몰랐다.
    /// </summary>
    internal static bool Rebuild(GameObject root)
    {
        return ImportSprite(out Sprite sprite) && Rebuild(root, sprite);
    }

    private static bool Rebuild(GameObject root, Sprite sprite)
    {
        EnsureBuilt(root);
        return Dress(root, sprite);
    }

    /// <summary>PNG 를 Sprite 로 들여온다. 이미 맞게 돼 있으면 다시 들여오지 않는다.</summary>
    private static bool ImportSprite(out Sprite sprite)
    {
        sprite = null;

        var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError($"[안내 팝업] 그림이 없다: {SpritePath}");
            return false;
        }

        // spriteMeshType 은 TextureImporter 에 없다. TextureImporterSettings 를 거쳐야 읽고 쓴다.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool changed = importer.textureType != TextureImporterType.Sprite
                       || importer.spriteImportMode != SpriteImportMode.Single
                       || settings.spriteMeshType != SpriteMeshType.FullRect
                       || !importer.alphaIsTransparency
                       || importer.mipmapEnabled
                       || importer.maxTextureSize < 2048;

        if (changed)
        {
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            importer.SetTextureSettings(settings);

            importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, 2048);

            importer.SaveAndReimport();
        }

        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);

        if (sprite == null)
        {
            Debug.LogError($"[안내 팝업] Sprite 로 안 들어왔다: {SpritePath}");
            return false;
        }

        return true;
    }

    /// <summary>#000000 55%. 팝업 뒤 갑판을 눌러 글이 읽히게 한다.</summary>
    private static readonly Color DimBlack = new Color(0f, 0f, 0f, 0.55f);

    /// <summary>
    /// 팝업이 없으면 짓는다. 모양을 맞추는 것은 <see cref="Dress"/> 의 일이라 여기서는
    /// **뼈대만** 세운다. 크기 · 스프라이트 · 글자색은 아래에서 어차피 다시 정해진다.
    ///
    /// <code>
    /// Tutorial            (ShipCoopTutorialView)
    /// └ Panel             화면 전체, 검정 0.55  ← 딤
    ///   └ Box             그림 한 장
    ///     ├ Body          옛 글자 본문 (Dress 가 끈다)
    ///     └ Footer        카운트다운
    /// </code>
    /// </summary>
    private static void EnsureBuilt(GameObject root)
    {
        Transform found = root.transform.Find("Tutorial");

        if (found != null && root.transform.Find("Tutorial/Panel/Box") != null)
        {
            found.SetAsLastSibling();
            return;
        }

        // 반쯤 남은 Tutorial 은 통째로 버린다. 어느 칸이 비었는지 따지는 것보다 싸다.
        if (found != null)
        {
            Object.DestroyImmediate(found.gameObject);
        }

        TMP_FontAsset font = FindFont(root);

        RectTransform tutorial = Rect("Tutorial", root.transform);
        Stretch(tutorial);

        RectTransform panel = Rect("Panel", tutorial);
        Stretch(panel);

        // ⚠ raycastTarget 을 켠 채로 둔다. 딤이 뒤를 막아야 팝업을 읽는 동안
        //    갑판 UI 를 실수로 누르지 않는다.
        var dim = panel.gameObject.AddComponent<Image>();
        dim.color = DimBlack;

        RectTransform box = Rect("Box", panel);
        box.gameObject.AddComponent<Image>();

        TextMeshProUGUI body = Label("Body", box, font);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.fontSize = 28f;

        TextMeshProUGUI footer = Label("Footer", box, font);

        var view = tutorial.gameObject.AddComponent<ShipCoopTutorialView>();

        // game 은 비워 둔다 — ShipCoopTutorialView.Awake 가 씬에서 스스로 찾는다.
        var so = new SerializedObject(view);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("bodyLabel").objectReferenceValue = body;
        so.FindProperty("footerLabel").objectReferenceValue = footer;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 팝업은 **맨 나중에** 그려져야 HUD 를 덮는다.
        tutorial.SetAsLastSibling();

        Debug.Log("[안내 팝업] 팝업이 없어서 새로 지었다.");
    }

    /// <summary>HUD 가 쓰던 글꼴을 그대로 받는다. 새로 고르면 한글이 네모로 깨진다.</summary>
    private static TMP_FontAsset FindFont(GameObject root)
    {
        TextMeshProUGUI sample = root.GetComponentInChildren<TextMeshProUGUI>(true);
        return sample != null ? sample.font : null;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font)
    {
        RectTransform rt = Rect(name, parent);
        Stretch(rt);

        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
        {
            tmp.font = font;
        }

        tmp.raycastTarget = false;
        return tmp;
    }

    private static bool Dress(GameObject root, Sprite sprite)
    {
        Transform box = root.transform.Find("Tutorial/Panel/Box");

        if (box == null)
        {
            Debug.LogError("[안내 팝업] Tutorial/Panel/Box 를 못 찾았다. 프리팹 구조가 바뀌었는지 보라.");
            return false;
        }

        // ---------------------------------------------------------- 그림
        var boxRect = (RectTransform)box;
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = BoxSize;

        var boxImage = box.GetComponent<Image>();

        if (boxImage == null)
        {
            Debug.LogError("[안내 팝업] Box 에 Image 가 없다.");
            return false;
        }

        boxImage.sprite = sprite;
        boxImage.type = Image.Type.Simple;   // 9-slice 로 늘리면 테두리 그림이 뭉개진다
        boxImage.preserveAspect = true;
        boxImage.color = Color.white;        // 옛 배경색이 남아 있으면 그림이 물든다
        boxImage.material = null;

        // ---------------------------------------------------------- 옛 본문
        Transform body = box.Find("Body");

        if (body != null)
        {
            // 지우지 않고 끄기만 한다. 그림을 안 쓰기로 하면 도로 켜면 된다.
            body.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------- 카운트다운
        Transform footer = box.Find("Footer");

        if (footer == null)
        {
            Debug.LogError("[안내 팝업] Footer 를 못 찾았다.");
            return false;
        }

        var footerRect = (RectTransform)footer;
        footerRect.anchorMin = FooterAnchorMin;
        footerRect.anchorMax = FooterAnchorMax;
        footerRect.offsetMin = Vector2.zero;
        footerRect.offsetMax = Vector2.zero;

        var footerText = footer.GetComponent<TextMeshProUGUI>();

        if (footerText == null)
        {
            Debug.LogError("[안내 팝업] Footer 에 TextMeshProUGUI 가 없다.");
            return false;
        }

        footerText.alignment = TextAlignmentOptions.Left;   // 왼쪽 · 수직 가운데
        footerText.color = FooterGold;
        footerText.enableAutoSizing = false;
        footerText.fontSize = FooterFontSize;
        footerText.textWrappingMode = TextWrappingModes.NoWrap;
        footerText.overflowMode = TextOverflowModes.Overflow;
        footerText.richText = false;
        footerText.raycastTarget = false;
        footerText.text = "10초 후 자동으로 닫힙니다.";

        // ---------------------------------------------------------- 값
        var view = root.GetComponentInChildren<ShipCoopTutorialView>(true);

        if (view == null)
        {
            Debug.LogError("[안내 팝업] ShipCoopTutorialView 를 못 찾았다.");
            return false;
        }

        var so = new SerializedObject(view);
        so.FindProperty("autoHideSeconds").floatValue = AutoHideSeconds;
        so.FindProperty("footerFormat").stringValue = "{0}초 후 자동으로 닫힙니다.";
        so.ApplyModifiedPropertiesWithoutUndo();

        return true;
    }
}
