using System.IO;
using UnderTheSea.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lobby.Editor
{
    /// <summary>
    /// 로비 게임 종료 프리팹(<c>Resources/LobbyGameExit.prefab</c>)을 만든다. 여러 번 눌러도 같은 결과다.
    ///
    /// <b>그림을 바꿀 때</b>는 <c>Art/UI/GameExit</c> 의 세 PNG 를 같은 이름으로 덮고 다시 누른다.
    /// 창 그림의 빈 자리가 달라지면 <see cref="ButtonRowY"/> 도 고친다.
    /// </summary>
    public static class LobbyGameExitSetup
    {
        private const string ArtFolder = "Assets/Game/Art/UI/GameExit";
        private const string PanelArt = ArtFolder + "/game_exit_panel.png";
        private const string ConfirmArt = ArtFolder + "/game_exit_confirm.png";
        private const string CancelArt = ArtFolder + "/game_exit_cancel.png";

        private const string Folder = "Assets/Game/Resources";
        private const string Output = Folder + "/LobbyGameExit.prefab";

        /// <summary>제단 봉헌 창(50) 위. 무엇이 떠 있든 종료 창이 맨 위에 온다.</summary>
        private const int SortingOrder = 60;

        /// <summary>창 높이. 가로는 그림 비율을 따른다. 1920×1080 기준.</summary>
        private const float PanelHeight = 600f;

        /// <summary>
        /// 버튼 줄의 높이 · 버튼 크기 · 두 버튼의 가로 가운데. 창 그림 크기에 대한 비율이다 (왼쪽 아래가 0,0).
        /// 그림 1672×941 에서 문구 아래 · 나침반 줄 위의 빈 자리(y 약 540~780)에 놓는다.
        /// 버튼 그림은 위아래에 투명 여백이 있어서 칸을 조금 넉넉히 잡았다.
        /// </summary>
        private const float ButtonRowY = 0.30f;
        private static readonly Vector2 ButtonSize = new Vector2(0.30f, 0.19f);
        private const float ConfirmX = 0.33f;
        private const float CancelX = 0.67f;

        [MenuItem("Tools/아라아띠/로비 게임 종료 프리팹 만들기")]
        public static void Build()
        {
            Sprite panelSprite = ImportAsSprite(PanelArt);
            Sprite confirmSprite = ImportAsSprite(ConfirmArt);
            Sprite cancelSprite = ImportAsSprite(CancelArt);

            if (panelSprite == null || confirmSprite == null || cancelSprite == null)
            {
                Debug.LogError($"[게임 종료] 그림을 찾지 못했습니다: {PanelArt} · {ConfirmArt} · {CancelArt}");
                return;
            }

            GameObject root = new GameObject("LobbyGameExit",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

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
            float aspect = panelSprite.rect.width / panelSprite.rect.height;
            panel.sizeDelta = new Vector2(PanelHeight * aspect, PanelHeight);
            // 그림이 클릭을 받아야 창 안을 눌러도 바닥의 "닫기" 가 먹지 않는다.
            SpriteImage(panel, panelSprite).raycastTarget = true;

            Button confirmButton = SpriteButton("ConfirmButton", panel, confirmSprite, ConfirmX);
            Button cancelButton = SpriteButton("CancelButton", panel, cancelSprite, CancelX);

            LobbyGameExitView view = root.AddComponent<LobbyGameExitView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("window").objectReferenceValue = window.gameObject;
            so.FindProperty("confirmButton").objectReferenceValue = confirmButton;
            so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            so.FindProperty("backdropButton").objectReferenceValue = backdropButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            window.gameObject.SetActive(false);

            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(root, Output);
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            Debug.Log($"[게임 종료] 만들었습니다 — {Output}");
        }

        public static void BuildFromCommandLine() => Build();

        private static Button SpriteButton(string name, RectTransform panel, Sprite sprite, float centerX)
        {
            Vector2 center = new Vector2(centerX, ButtonRowY);

            RectTransform rect = Child(name, panel);
            rect.anchorMin = center - ButtonSize * 0.5f;
            rect.anchorMax = center + ButtonSize * 0.5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            Image image = SpriteImage(rect, sprite);
            // 투명 여백은 누르지 않게 한다. 그림 속 판자만 버튼이다.
            image.alphaHitTestMinimumThreshold = 0.5f;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

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
            // alphaHitTestMinimumThreshold 가 픽셀을 읽는다.
            importer.isReadable = true;
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
