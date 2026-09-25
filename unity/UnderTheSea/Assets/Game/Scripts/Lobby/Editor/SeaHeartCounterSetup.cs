using System.IO;
using UnderTheSea.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lobby.Editor
{
    /// <summary>
    /// 로비 조각 수량 프리팹(<c>Resources/SeaHeartCounter.prefab</c>)을 만든다. 여러 번 눌러도 같은 결과다.
    ///
    /// <b>숫자 그림을 바꿀 때</b>는 <c>Art/UI/SeaHeartCounter</c> 의 PNG 를 같은 이름으로 덮고 다시 누른다.
    /// 그 그림들은 <c>art/ui-assets/sea-heart-counter-v2</c> 원본(1024×1024)을 <b>같은 위아래 띠(y 110~915)</b>로
    /// 잘라낸 것이다. 띠가 같아야 숫자끼리 기준선이 맞는다. 좌우는 글자마다 여백 20px 만 남겼다.
    /// </summary>
    public static class SeaHeartCounterSetup
    {
        private const string ArtFolder = "Assets/Game/Art/UI/SeaHeartCounter";
        private const string MultiplyArt = ArtFolder + "/multiply_x.png";

        /// <summary>제단 창과 같은 조각 그림. 그쪽 임포트 설정은 건드리지 않고 읽기만 한다.</summary>
        private const string IconArt = "Assets/Game/Art/UI/Altar/altar_heart_fragment.png";

        private const string Folder = "Assets/Game/Resources";
        private const string Output = Folder + "/SeaHeartCounter.prefab";

        /// <summary>회복도 HUD(40) 바로 위. 도감(45) · 제단 창(50) · 종료 창(60)이 이것을 덮는다.</summary>
        private const int SortingOrder = 41;

        /// <summary>오른쪽 위 여백. 1920×1080 기준.</summary>
        private static readonly Vector2 Margin = new Vector2(44f, 44f);

        private const float DigitHeight = 56f;

        /// <summary>X 는 숫자보다 작게 둔다. 목업의 "x 1" 처럼 숫자가 주인공이다.</summary>
        private const float MultiplyHeight = DigitHeight * 0.62f;

        /// <summary>조각 그림은 둘레에 빛 번짐 여백이 커서 숫자보다 크게 잡는다.</summary>
        private const float IconSize = 92f;

        [MenuItem("Tools/아라아띠/로비 조각 수량 프리팹 만들기")]
        public static void Build()
        {
            Sprite[] digitSprites = new Sprite[10];
            for (int i = 0; i < 10; i++)
            {
                digitSprites[i] = ImportAsSprite($"{ArtFolder}/digit_{i}.png");
            }

            Sprite multiplySprite = ImportAsSprite(MultiplyArt);
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconArt);

            if (System.Array.Exists(digitSprites, s => s == null) || multiplySprite == null || iconSprite == null)
            {
                Debug.LogError($"[조각 수량] 그림을 찾지 못했습니다: {ArtFolder} · {IconArt}");
                return;
            }

            GameObject root = new GameObject("SeaHeartCounter",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 오른쪽 위 가로줄 — 오른쪽 끝에 붙어서 자릿수가 늘면 왼쪽으로 자란다
            RectTransform row = Child("Row", root.transform);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 1f);
            row.anchoredPosition = new Vector2(-Margin.x, -Margin.y);
            row.sizeDelta = new Vector2(600f, IconSize);

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 4f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            SpriteImage(Child("Icon", row), iconSprite, new Vector2(IconSize, IconSize));

            float multiplyAspect = multiplySprite.rect.width / multiplySprite.rect.height;
            SpriteImage(Child("Multiply", row), multiplySprite,
                new Vector2(MultiplyHeight * multiplyAspect, MultiplyHeight));

            SeaHeartCounterView view = root.AddComponent<SeaHeartCounterView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("row").objectReferenceValue = row;
            so.FindProperty("content").objectReferenceValue = row.gameObject;
            so.FindProperty("digitHeight").floatValue = DigitHeight;
            SerializedProperty sprites = so.FindProperty("digitSprites");
            sprites.arraySize = 10;
            for (int i = 0; i < 10; i++)
            {
                sprites.GetArrayElementAtIndex(i).objectReferenceValue = digitSprites[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 서버 값을 받기 전에는 보이지 않는다. 받으면 View 가 켠다.
            row.gameObject.SetActive(false);

            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(root, Output);
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            Debug.Log($"[조각 수량] 만들었습니다 — {Output}");
        }

        public static void BuildFromCommandLine() => Build();

        private static Sprite ImportAsSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            // 화면에서 56px 로 그린다. 4K 화면에서도 모자라지 않게 여유를 둔다.
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static RectTransform Child(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        private static void SpriteImage(RectTransform rect, Sprite sprite, Vector2 size)
        {
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }
}
