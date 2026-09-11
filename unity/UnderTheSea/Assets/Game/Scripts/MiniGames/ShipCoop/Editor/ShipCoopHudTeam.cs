using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 에 팀원 초상화 4칸을 **덧붙인다.** (SHIPCOOP.md 9장)
///
/// 왜 필요한가
///   갑판이 3층이 되면서 **화면에 배 전체가 안 나옵니다.** 앞갑판에 있으면 뒷갑판이
///   안 보입니다. 그래서 여기가 "누가 어디 있나" 를 아는 **유일한 곳**이 됐습니다.
///
/// ⚠ **층까지만 보여줍니다. 어느 자리에 붙었는지는 넣지 않습니다.**
///
///   층    "민화는 뒷갑판"    →  역할을 나눌 재료
///   자리  "돛이 비어 있다"   →  답을 그냥 준다. "돛 비었어!" 가 사라진다
///
/// 하트도 넣지 않습니다. 개인 HP 가 존재하지 않기 때문입니다. (2장)
///
/// ⚠ <see cref="ShipCoopHudArt"/> 와 달리 기존 자식을 지우지 않습니다.
///    저쪽은 자식을 전부 새로 만들어 fileID 가 다시 매겨지고, 그러면 씬에 놓인
///    HUD 인스턴스의 override 가 끊어집니다. 여기서는 **내가 만든 것만** 지우고 다시 만듭니다.
///
/// 되풀이해서 돌려도 됩니다. TeamRow 하나만 갈아끼웁니다.
///
/// Tools > 아라아띠 > 배 협동 HUD 에 팀원 초상화 붙이기
/// </summary>
public static class ShipCoopHudTeam
{
    private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHud.prefab";

    /// <summary>새 에셋 묶음. 기존 HUD 폴더와 섞지 않는다. (art/hud/06 README)</summary>
    private const string ArtRoot = "Assets/Game/Art/UI/ShipCoopHudV2";

    private const string RowName = "TeamRow";

    /// <summary>왼쪽 아래. 06 묶음 README 의 권장 배치(28,876 · 570×176)를 따른다.</summary>
    private static readonly Vector2 RowPosition = new Vector2(28f, 28f);
    private static readonly Vector2 SlotSize = new Vector2(132f, 176f);
    private const float SlotGap = 14f;

    /// <summary>플레이어 구분 색. 직업이 아니라 사람 구분이다.</summary>
    private static readonly string[] FrameSprites =
    {
        "portrait-frame-red",
        "portrait-frame-yellow",
        "portrait-frame-green",
        "portrait-frame-purple",
    };

    /// <summary>초상화 그림. 진짜 캐릭터가 오면 바뀐다.</summary>
    private static readonly string[] FaceSprites =
    {
        "portrait-captain",
        "portrait-wig",
        "portrait-jester",
        "portrait-diver",
    };

    private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 BottomLeft = Vector2.zero;

    private static TMP_FontAsset _font;

    [MenuItem("Tools/아라아띠/배 협동 HUD 에 팀원 초상화 붙이기")]
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

            // 내가 지난번에 만든 것만 지운다.
            Remove(canvas, RowName);

            RectTransform row = Rect(RowName, canvas);
            Place(row, BottomLeft, RowPosition + new Vector2(SlotSize.x * 2f + SlotGap * 1.5f, SlotSize.y * 0.5f),
                  new Vector2(SlotSize.x * 4f + SlotGap * 3f, SlotSize.y));
            Img(row, Sprite("panel-team-background"), new Color(1f, 1f, 1f, 0.85f));

            RectTransform frame = Rect("Frame", row);
            Stretch(frame, 0f, 0f, 0f, 0f);
            Img(frame, Sprite("panel-team-frame"), Color.white);

            var slots = new ShipCoopHud.PortraitSlot[4];

            for (int i = 0; i < 4; i++)
            {
                slots[i] = BuildSlot(row, i);
            }

            Connect(hud, slots);
            SyncTextColors(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[HUD 팀원] 초상화 4칸을 붙이고 저장했다.");
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
            Debug.Log("[HUD 팀원] 끝났다.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HUD 팀원] 실패: {e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 초상화 한 칸.
    ///
    /// 쌓는 순서는 06 묶음 README 를 따른다.
    /// <code>
    /// backplate → (Mask 아래 얼굴) → frame-{색} → 번호 · 갑판 이름 → 🆘
    /// </code>
    /// </summary>
    private static ShipCoopHud.PortraitSlot BuildSlot(RectTransform row, int index)
    {
        float x = (index - 1.5f) * (SlotSize.x + SlotGap);

        RectTransform slot = Rect($"Slot_{index + 1}", row);
        Place(slot, Half, new Vector2(x, 0f), SlotSize);

        // 얼굴 판. 마스크로 써서 초상화가 판 모양대로 잘린다.
        RectTransform plate = Rect("Backplate", slot);
        Place(plate, Half, new Vector2(0f, 18f), new Vector2(104f, 104f));
        Img(plate, Sprite("portrait-backplate"), Color.white);

        Mask mask = plate.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform face = Rect("Face", plate);
        Stretch(face, 0f, 0f, 0f, 0f);
        Img(face, Sprite(FaceSprites[index]), Color.white);

        // 색 테두리는 마스크 **밖**에 둔다. 안에 두면 같이 잘린다.
        RectTransform ring = Rect("Frame", slot);
        Place(ring, Half, new Vector2(0f, 18f), new Vector2(112f, 112f));
        Image ringImage = Img(ring, Sprite(FrameSprites[index]), Color.white);

        // P1 ~ P4
        RectTransform badge = Rect("Number", slot);
        Place(badge, Half, new Vector2(-42f, 62f), new Vector2(40f, 40f));
        Img(badge, Sprite("badge-player"), Color.white);
        TextMeshProUGUI number = Text("Label", badge, $"P{index + 1}", 20f, TextAlignmentOptions.Center);

        // 갑판 이름. **자리 이름은 절대 넣지 않는다.**
        TextMeshProUGUI deck = Text("Deck", slot, "—", 22f, TextAlignmentOptions.Center);
        Place((RectTransform)deck.transform, Half, new Vector2(0f, -58f), new Vector2(SlotSize.x, 32f));

        // 🆘 — 평소에는 꺼져 있다.
        RectTransform help = Rect("HelpBadge", slot);
        Place(help, Half, new Vector2(42f, 62f), new Vector2(44f, 44f));
        Img(help, Sprite("badge-help"), Color.white);

        RectTransform helpIcon = Rect("Icon", help);
        Stretch(helpIcon, 8f, 8f, 8f, 8f);
        Img(helpIcon, Sprite("icon-warning"), Color.white);

        help.gameObject.SetActive(false);

        return new ShipCoopHud.PortraitSlot
        {
            root = slot.gameObject,
            frame = ringImage,
            number = number,
            deckLabel = deck,
            helpBadge = help.gameObject,
        };
    }

    private static void Connect(ShipCoopHud hud, ShipCoopHud.PortraitSlot[] slots)
    {
        var so = new SerializedObject(hud);
        SerializedProperty list = so.FindProperty("portraits");

        list.arraySize = slots.Length;

        for (int i = 0; i < slots.Length; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("root").objectReferenceValue = slots[i].root;
            item.FindPropertyRelative("frame").objectReferenceValue = slots[i].frame;
            item.FindPropertyRelative("number").objectReferenceValue = slots[i].number;
            item.FindPropertyRelative("deckLabel").objectReferenceValue = slots[i].deckLabel;
            item.FindPropertyRelative("helpBadge").objectReferenceValue = slots[i].helpBadge;
        }

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
    // ShipCoopHudFlood 의 것과 같다. 저쪽이 private 이라 여기 따로 둔다.

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

    /// <summary>
    /// TMP 는 글자색을 두 곳에 들고 있다. 코드로 color 만 넣으면 씬에 놓을 때
    /// 쓸데없는 override 가 한 줄 생긴다. (ShipCoopHudFlood 와 같은 이유)
    /// </summary>
    private static void SyncTextColors(GameObject root)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            Color color = text.color;
            text.faceColor = color;
            EditorUtility.SetDirty(text);
        }
    }
}
