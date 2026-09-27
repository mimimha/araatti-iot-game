using System.IO;
using UnderTheSea.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Lobby.Editor
{
    /// <summary>
    /// 제단 완성 영상 프리팹(<c>Resources/AltarCompletionVideo.prefab</c>)을 만든다. 여러 번 눌러도 같은 결과다.
    ///
    /// <code>
    ///   AltarCompletionVideo     Canvas (화면 오버레이, 맨 위) · CanvasGroup · VideoPlayer · AltarCompletionVideo
    ///     Black                  검은 바탕 — 영상 비율이 화면과 달라도 가장자리가 검게
    ///     Screen                 영상 (RawImage, 16:9 를 지키며 화면에 맞춤)
    /// </code>
    ///
    /// 영상 파일: <c>Assets/Game/Art/Video/AltarComplete.mp4</c> (1920×1080, H.264 + AAC). 바꾸려면 같은 이름으로
    /// 덮어쓰고 다시 누른다. mp4 는 git LFS 로 올라간다(.gitattributes).
    ///
    /// 화면 순서는 이정표 창(32000) 위, 페이드 막(32500) 아래다.
    /// </summary>
    public static class AltarCompletionVideoSetup
    {
        private const string VideoPath = "Assets/Game/Art/Video/AltarComplete.mp4";
        private const string Folder = "Assets/Game/Resources";
        private const string Output = Folder + "/AltarCompletionVideo.prefab";
        private const int SortingOrder = 32400;

        [MenuItem("Tools/아라아띠/제단 완성 영상 설치")]
        public static void Build()
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
            if (clip == null)
            {
                Debug.LogError($"[완성 영상] 영상이 없습니다: {VideoPath}");
                return;
            }

            GameObject root = new GameObject("AltarCompletionVideo",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(VideoPlayer));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 영상이 나오는 동안 아래 화면의 클릭을 먹는다.
            root.AddComponent<GraphicRaycaster>();

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            RectTransform black = Stretch("Black", root.transform);
            Image blackImage = black.gameObject.AddComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = true;

            RectTransform screenRect = Stretch("Screen", root.transform);
            RawImage screen = screenRect.gameObject.AddComponent<RawImage>();
            screen.color = Color.white;
            screen.raycastTarget = false;
            AspectRatioFitter fitter = screenRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = clip.height > 0 ? clip.width / (float)clip.height : 16f / 9f;

            VideoPlayer player = root.GetComponent<VideoPlayer>();
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;

            AltarCompletionVideo video = root.AddComponent<AltarCompletionVideo>();
            var so = new SerializedObject(video);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("screen").objectReferenceValue = screen;
            so.ApplyModifiedPropertiesWithoutUndo();

            canvas.enabled = false;

            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(root, Output);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();

            Debug.Log($"[완성 영상] 만들었습니다 — {Output} ({clip.width}x{clip.height}, {clip.length:F1}초)");
        }

        public static void BuildFromCommandLine() => Build();

        private static RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
