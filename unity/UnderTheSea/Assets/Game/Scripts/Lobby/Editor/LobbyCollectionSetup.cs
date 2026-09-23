using System.IO;
using UnderTheSea.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lobby.Editor
{
    /// <summary>
    /// 로비 도감 프리팹(<c>Resources/LobbyCollection.prefab</c>)을 만든다. 여러 번 눌러도 같은 결과다.
    ///
    /// <b>그림을 바꿀 때</b>는 <c>Art/UI/Collection</c> 의 두 PNG 를 같은 이름으로 덮고 다시 누른다.
    /// 창 그림의 X 자리가 달라지면 <see cref="CloseCenter"/> 도 고친다.
    /// </summary>
    public static class LobbyCollectionSetup
    {
        private const string ArtFolder = "Assets/Game/Art/UI/Collection";
        private const string ButtonArt = ArtFolder + "/collection_button.png";
        private const string WindowArt = ArtFolder + "/collection_window.png";

        private const string Folder = "Assets/Game/Resources";
        private const string Output = Folder + "/LobbyCollection.prefab";

        /// <summary>
        /// 회복도 HUD(40) 위, 제단 봉헌 창(50) 아래. 채팅(5)보다는 위라 창이 채팅을 덮는다.
        /// </summary>
        private const int SortingOrder = 45;

        /// <summary>오른쪽 위 버튼. 1920×1080 기준.</summary>
        private static readonly Vector2 ButtonSize = new Vector2(120f, 116f);
        private static readonly Vector2 ButtonMargin = new Vector2(32f, 32f);

        /// <summary>창 높이. 가로는 그림 비율을 따른다. 1080 에서 위아래로 60 씩 남는다.</summary>
        private const float WindowHeight = 960f;

        /// <summary>
        /// 창 그림 속 X 의 가운데와 크기. 그림 크기에 대한 비율이다 (왼쪽 아래가 0,0).
        /// 잘라 낸 그림 918×1194 에서 X 는 (817, 100) 부근, 지름 약 120px 이다.
        /// </summary>
        private static readonly Vector2 CloseCenter = new Vector2(0.890f, 0.916f);
        private static readonly Vector2 CloseSize = new Vector2(0.13f, 0.10f);

        [MenuItem("Tools/아라아띠/로비 도감 프리팹 만들기")]
        public static void Build()
        {
            Sprite buttonSprite = ImportAsSprite(ButtonArt);
            Sprite windowSprite = ImportAsSprite(WindowArt);

            if (buttonSprite == null || windowSprite == null)
            {
                Debug.LogError($"[도감] 그림을 찾지 못했습니다: {ButtonArt} · {WindowArt}");
                return;
            }

            GameObject root = new GameObject("LobbyCollection",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 오른쪽 위 책 버튼
            RectTransform open = Child("OpenButton", root.transform);
            open.anchorMin = open.anchorMax = open.pivot = new Vector2(1f, 1f);
            open.anchoredPosition = -ButtonMargin;
            open.sizeDelta = ButtonSize;
            Image openImage = SpriteImage(open, buttonSprite);
            Button openButton = open.gameObject.AddComponent<Button>();
            openButton.targetGraphic = openImage;

            // 창 — 열렸을 때만 켜진다
            RectTransform window = Child("Window", root.transform);
            Stretch(window);

            RectTransform backdrop = Child("Backdrop", window);
            Stretch(backdrop);
            Image backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.55f);
            Button backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;

            RectTransform panel = Child("Panel", window);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            float aspect = windowSprite.rect.width / windowSprite.rect.height;
            panel.sizeDelta = new Vector2(WindowHeight * aspect, WindowHeight);
            // 그림이 클릭을 받아야 창 안을 눌러도 바닥의 "닫기" 가 먹지 않는다.
            SpriteImage(panel, windowSprite).raycastTarget = true;

            RectTransform close = Child("CloseButton", panel);
            close.anchorMin = CloseCenter - CloseSize * 0.5f;
            close.anchorMax = CloseCenter + CloseSize * 0.5f;
            close.offsetMin = close.offsetMax = Vector2.zero;
            Image closeImage = close.gameObject.AddComponent<Image>();
            closeImage.color = new Color(1f, 1f, 1f, 0f); // 보이지 않고 클릭만 받는다
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;

            LobbyCollectionView view = root.AddComponent<LobbyCollectionView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("openButton").objectReferenceValue = openButton;
            so.FindProperty("window").objectReferenceValue = window.gameObject;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("backdropButton").objectReferenceValue = backdropButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            window.gameObject.SetActive(false);

            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(root, Output);
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            Debug.Log($"[도감] 만들었습니다 — {Output}");
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
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static RectTransform Child(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image SpriteImage(RectTransform rect, Sprite sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }
    }
}
