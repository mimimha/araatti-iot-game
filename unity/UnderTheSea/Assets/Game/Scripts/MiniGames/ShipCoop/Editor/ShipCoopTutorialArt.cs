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
            if (!Dress(root, sprite))
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
