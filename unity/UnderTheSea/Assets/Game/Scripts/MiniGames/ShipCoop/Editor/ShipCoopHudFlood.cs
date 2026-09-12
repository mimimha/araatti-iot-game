using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 에 침수 게이지를 **덧붙인다.** (SHIPCOOP.md 4장 · 9장)
///
/// 침수는 만들어 놓고 화면 어디에도 안 붙어 있었다. 배 HP 를 깎지 않고 속도를 깎기 때문에,
/// 안 보이면 **배가 왜 느려졌는지 알 방법이 없다.**
///
/// ⚠ <see cref="ShipCoopHudArt"/> 와 달리 기존 자식을 지우지 않는다.
///
///    저쪽은 자식을 전부 새로 만들기 때문에 fileID 가 다시 매겨지고,
///    그러면 씬에 놓인 HUD 인스턴스의 override 가 전부 끊어진다.
///    ShipCoopTest 씬에는 지금 그 override 가 30줄 넘게 있다.
///    그래서 여기서는 **내가 만든 것만 지우고 다시 만든다.**
///
/// 되풀이해서 돌려도 된다. FloodCaption · FloodTrack 두 개만 갈아끼운다.
///
/// Tools > 아라아띠 > 배 협동 HUD 에 침수 게이지 붙이기
/// </summary>
public static class ShipCoopHudFlood
{
    private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHud.prefab";
    private const string ArtRoot = "Assets/Game/Art/UI/ShipCoopHud";

    private const string CaptionName = "FloodCaption";
    private const string TrackName = "FloodTrack";

    // HP 바와 같은 축척을 쓴다. 나란히 놓이므로 두께가 달라 보이면 안 된다.
    private const float BarScale = 560f / 1024f;

    /// <summary>진행도 바(-215) 아래. 셋이 같은 세로줄에 선다.</summary>
    private static readonly Vector2 BarPosition = new Vector2(-330f, -285f);
    private static readonly Vector2 CaptionPosition = new Vector2(-694f, -285f);

    private static readonly Color Water = new Color(0.35f, 0.62f, 0.90f, 0.95f);
    private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);

    private static TMP_FontAsset _font;

    [MenuItem("Tools/아라아띠/배 협동 HUD 에 침수 게이지 붙이기")]
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

            // 글꼴은 지금 쓰던 것을 그대로 이어받는다. 한글이 깨지지 않게.
            TextMeshProUGUI sample = root.GetComponentInChildren<TextMeshProUGUI>(true);
            _font = sample != null ? sample.font : null;

            var canvas = (RectTransform)root.transform;

            // 내가 지난번에 만든 것만 지운다. 다른 자식은 건드리지 않는다.
            Remove(canvas, CaptionName);
            Remove(canvas, TrackName);

            Caption(canvas, CaptionName, "침수", CaptionPosition);

            float h = 96f * BarScale;
            float inset = 12f * BarScale;

            RectTransform track = Rect(TrackName, canvas);
            Place(track, TopRight, BarPosition, new Vector2(560f, h));
            Img(track, Sprite("hp-track"), Color.white);

            RectTransform fill = Rect("FloodFill", track);
            Stretch(fill, inset, inset, inset, inset);
            Image floodFill = Img(fill, Sprite("hp-mask"), Water);
            floodFill.type = Image.Type.Filled;
            floodFill.fillMethod = Image.FillMethod.Horizontal;
            floodFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            floodFill.fillAmount = 0f;

            RectTransform frame = Rect("FloodFrame", track);
            Stretch(frame, 0f, 0f, 0f, 0f);
            Img(frame, Sprite("hp-frame"), Color.white);

            TextMeshProUGUI label =
                Text("FloodLabel", track, "침수 0%  ·  속도 100%", 22f, TextAlignmentOptions.Center);

            Connect(hud, track.gameObject, floodFill, label);
            SyncTextColors(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[HUD 침수] 게이지를 붙이고 저장했다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>배치 모드에서 부른다. 실패하면 종료 코드를 남긴다.</summary>
    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
            Debug.Log("[HUD 침수] 끝났다.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HUD 침수] 실패: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static void Connect(ShipCoopHud hud, GameObject root, Image fill, TextMeshProUGUI label)
    {
        var so = new SerializedObject(hud);

        so.FindProperty("floodRoot").objectReferenceValue = root;
        so.FindProperty("floodFill").objectReferenceValue = fill;
        so.FindProperty("floodLabel").objectReferenceValue = label;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Remove(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            Object.DestroyImmediate(found.gameObject);
        }
    }

    // ------------------------------------------------------------------ 손도구
    //
    // ShipCoopHudArt 의 것과 같다. 저쪽 것은 private 이고, 그 파일을 열어
    // 공용으로 바꾸면 저쪽이 다시 구워질 위험이 생겨서 여기 따로 둔다.

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

    private static void Caption(RectTransform canvas, string name, string text, Vector2 position)
    {
        RectTransform plate = Rect(name, canvas);
        Place(plate, TopRight, position, new Vector2(160f, 48f));
        Img(plate, Sprite("heading-label"), Color.white);
        Text("Label", plate, text, 20f, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// TMP 는 글자색을 두 곳에 들고 있다. 코드로 color 만 넣으면 씬에 놓을 때
    /// 쓸데없는 override 가 한 줄 생긴다. (ShipCoopHudArt 의 같은 이름 함수와 같은 이유)
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
}
