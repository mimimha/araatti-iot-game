using System.IO;
using TMPro;
using UnderTheSea.Lobby.Dance;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lobby.Editor
{
    /// <summary>
    /// 로비 춤 휠 프리팹(<c>Resources/DanceWheel.prefab</c>)을 만든다. 여러 번 눌러도 같은 결과다.
    ///
    /// <code>
    ///   DanceWheel            Canvas (화면 오버레이) · CanvasScaler · DanceWheelView
    ///     Wheel               화면 가운데. 모든 칸이 이 점을 중심으로 겹친다
    ///       Hub               가운데 원판. 가리킨 춤 이름이 뜨는 자리
    ///       Slot1 ~ Slot5     칸 (RingSegmentGraphic). 위에서 시계 방향
    ///         Label           칸 이름
    ///       CenterLabel       가리킨 춤 이름
    /// </code>
    ///
    /// 크기 · 틈 · 색을 바꾸려면 아래 숫자를 고치고 다시 누르거나, 만든 프리팹을 직접 고친다.
    /// 다시 누르면 프리팹을 새로 쓰므로 직접 고친 것은 사라진다.
    /// </summary>
    public static class DanceWheelSetup
    {
        private const string Folder = "Assets/Game/Resources";
        private const string Output = Folder + "/DanceWheel.prefab";

        /// <summary>로비 HUD 와 같은 폰트.</summary>
        private const string FontPath = "Assets/Game/Fonts/NotoSansKR-Bold SDF.asset";

        /// <summary>회복도(40) · 조각 수량(41) · 도감(45) 위, 제단 창(50) · 종료 창(60) 아래.</summary>
        private const int SortingOrder = 48;

        private const int SlotCount = 5;

        // 1920×1080 기준 px
        private const float InnerRadius = 100f;
        private const float OuterRadius = 240f;
        private const float Gap = 12f;
        private const float BorderWidth = 3f;
        private const float HubRadius = 86f;
        private const float BackdropPadding = 18f;
        private const float BadgeRadius = 15f;
        private const float SlotFontSize = 30f;
        private const float CenterFontSize = 30f;
        private const float HintFontSize = 17f;

        // 바다 테마 — 깊은 바다색 칸에 청록 빛. 로비의 밝은 모래 · 하늘 위에서도 글씨가 읽히게 칸은 어둡게 둔다.
        private static readonly Color BackdropColor = new Color(0.02f, 0.08f, 0.15f, 0.35f);
        private static readonly Color SlotColor = new Color(0.05f, 0.17f, 0.29f, 0.84f);
        private static readonly Color SlotHoverColor = new Color(0.14f, 0.66f, 0.74f, 0.96f);
        private static readonly Color BorderColor = new Color(0.62f, 0.93f, 1f, 0.38f);
        private static readonly Color BorderHoverColor = new Color(0.92f, 1f, 1f, 1f);
        private static readonly Color HubColor = new Color(0.04f, 0.13f, 0.23f, 0.92f);
        private static readonly Color HubRingColor = new Color(0.42f, 0.88f, 0.95f, 0.85f);
        private static readonly Color BadgeColor = new Color(0.42f, 0.88f, 0.95f, 0.9f);
        private static readonly Color BadgeTextColor = new Color(0.03f, 0.12f, 0.2f, 1f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color HintColor = new Color(0.62f, 0.93f, 1f, 0.85f);

        [MenuItem("Tools/아라아띠/로비 춤 휠 프리팹 만들기")]
        public static void Build()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError($"[춤 휠] 폰트를 찾지 못했습니다: {FontPath}");
                return;
            }

            GameObject root = new GameObject("DanceWheel",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 클릭은 휠 중심에서 잰 방향으로 판단한다(DanceWheelView.SlotUnder). GraphicRaycaster 가 없어서
            // 휠이 채팅창 같은 다른 화면의 클릭을 가로채지 않는다.

            RectTransform wheel = Child("Wheel", root.transform);
            wheel.anchorMin = wheel.anchorMax = wheel.pivot = new Vector2(0.5f, 0.5f);
            wheel.anchoredPosition = Vector2.zero;
            wheel.sizeDelta = Vector2.one * (OuterRadius + BackdropPadding) * 2f;
            CanvasGroup group = wheel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // 뒤판 — 휠 전체를 받치는 옅은 원. 밝은 배경 위에서 휠이 떠 보이게 한다.
            Segment("Backdrop", wheel, BackdropColor).Configure(0.01f, OuterRadius + BackdropPadding, 0f, 360f, 0f);

            float slotSweep = 360f / SlotCount;
            float labelRadius = (InnerRadius + OuterRadius) * 0.5f;

            RingSegmentGraphic[] fills = new RingSegmentGraphic[SlotCount];
            RingSegmentGraphic[] borders = new RingSegmentGraphic[SlotCount];
            RectTransform[] roots = new RectTransform[SlotCount];
            TMP_Text[] labels = new TMP_Text[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                // 칸 0 은 위쪽 가운데. 시계 방향이라 수학 각도로는 줄어든다.
                float center = 90f - i * slotSweep;
                float start = center - slotSweep * 0.5f;
                Vector2 dir = new Vector2(Mathf.Cos(center * Mathf.Deg2Rad), Mathf.Sin(center * Mathf.Deg2Rad));

                // 칸 한 덩어리. 가리키면 이것을 키워서 테두리 · 이름 · 번호가 같이 커진다.
                RectTransform slotRoot = Child($"Slot{i + 1}", wheel);
                slotRoot.anchorMin = slotRoot.anchorMax = slotRoot.pivot = new Vector2(0.5f, 0.5f);
                slotRoot.anchoredPosition = Vector2.zero;
                slotRoot.sizeDelta = Vector2.one * OuterRadius * 2f;
                roots[i] = slotRoot;

                // 테두리는 칸보다 한 겹 크게 그려 뒤에 깐다. 틈은 테두리 두께만큼 좁혀야 칸 둘레에 고르게 남는다.
                RingSegmentGraphic border = Segment("Border", slotRoot, BorderColor);
                border.Configure(InnerRadius - BorderWidth, OuterRadius + BorderWidth, start, slotSweep,
                    Mathf.Max(0f, Gap - BorderWidth * 2f));
                borders[i] = border;

                RingSegmentGraphic fill = Segment("Fill", slotRoot, SlotColor);
                fill.Configure(InnerRadius, OuterRadius, start, slotSweep, Gap);
                fills[i] = fill;

                TMP_Text label = Text("Label", slotRoot, font, SlotFontSize, $"춤 {i + 1}", TextColor);
                // 이름과 번호는 칸 가운데에 위아래로 쌓는다. 번호를 바깥 끝에 두면 양옆 칸에서는
                // 가로로 긴 이름과 같은 방향이라 글자를 가린다(첫 캡처에서 확인).
                Vector2 slotCenter = dir * labelRadius;
                label.rectTransform.anchoredPosition = slotCenter + new Vector2(0f, -14f);
                label.rectTransform.sizeDelta = new Vector2((OuterRadius - InnerRadius) * 1.05f, 48f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = SlotFontSize;
                labels[i] = label;

                // 번호 배지 — 숫자키 1 ~ 5 로도 고를 수 있다는 것을 보여 준다.
                RingSegmentGraphic badge = Segment("Badge", slotRoot, BadgeColor);
                badge.Configure(0.01f, BadgeRadius, 0f, 360f, 0f);
                badge.rectTransform.anchoredPosition = slotCenter + new Vector2(0f, 26f);
                badge.rectTransform.sizeDelta = Vector2.one * BadgeRadius * 2f;

                TMP_Text number = Text("Number", badge.rectTransform, font, 18f, (i + 1).ToString(), BadgeTextColor);
                number.rectTransform.sizeDelta = Vector2.one * BadgeRadius * 2f;
            }

            // 가운데 원판 — 청록 테두리 + 가리킨 춤 이름 + 닫는 법.
            Segment("HubRing", wheel, HubRingColor).Configure(0.01f, HubRadius + BorderWidth, 0f, 360f, 0f);
            Segment("Hub", wheel, HubColor).Configure(0.01f, HubRadius, 0f, 360f, 0f);

            TMP_Text centerLabel = Text("CenterLabel", wheel, font, CenterFontSize, "춤 고르기", TextColor);
            centerLabel.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            centerLabel.rectTransform.sizeDelta = new Vector2(HubRadius * 2f - 20f, 44f);
            centerLabel.enableAutoSizing = true;
            centerLabel.fontSizeMin = 16f;
            centerLabel.fontSizeMax = CenterFontSize;

            TMP_Text hint = Text("Hint", wheel, font, HintFontSize, "Q 닫기", HintColor);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            hint.rectTransform.sizeDelta = new Vector2(HubRadius * 2f - 20f, 26f);

            DanceWheelView view = root.AddComponent<DanceWheelView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("wheel").objectReferenceValue = wheel;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("centerLabel").objectReferenceValue = centerLabel;
            so.FindProperty("normalColor").colorValue = SlotColor;
            so.FindProperty("hoverColor").colorValue = SlotHoverColor;
            so.FindProperty("labelColor").colorValue = TextColor;
            so.FindProperty("borderColor").colorValue = BorderColor;
            so.FindProperty("hoverBorderColor").colorValue = BorderHoverColor;
            so.FindProperty("hoverScale").floatValue = 1.08f;

            SetArray(so.FindProperty("slots"), fills);
            SetArray(so.FindProperty("slotBorders"), borders);
            SetArray(so.FindProperty("slotRoots"), roots);
            SetArray(so.FindProperty("slotLabels"), labels);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 처음에는 닫혀 있다. 켜고 끄는 것은 Canvas.enabled 다 — 루트는 설치기가 로비에서만 켠다.
            canvas.enabled = false;

            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(root, Output);
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            Debug.Log($"[춤 휠] 만들었습니다 — {Output}. 칸 이름은 Tools/아라아띠/로비 춤 설치 가 채운다.");
        }

        private static void SetArray(SerializedProperty prop, Object[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        public static void BuildFromCommandLine() => Build();

        // ─────────────────────────────────────────────── 캡처

        /// <summary>
        /// 휠 프리팹을 1920×1080 으로 찍어 PNG 로 남긴다(<c>%TEMP%/DanceWheelCapture</c>).
        /// 닫힌 칸 그대로 한 장, 1번 칸을 가리킨 상태로 한 장. 모양을 고칠 때마다 눌러서 비교한다.
        ///
        /// ⚠ <b>열린 씬을 건드리지 않는다.</b> 따로 만든 미리보기 씬에서 찍는다.
        ///    오버레이 캔버스는 카메라로 찍히지 않아서, 복제본만 카메라 캔버스로 바꿔 찍는다(프리팹은 그대로).
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 춤 휠 캡처")]
        public static void Capture()
        {
            CaptureTo(Path.Combine(Path.GetTempPath(), "DanceWheelCapture"));
        }

        /// <summary>
        /// **에디터에서 휠만 띄워 본다.** 빈 씬에 휠 프리팹을 놓고 Play 한다.
        ///
        /// 평소에는 로비에 내 캐릭터가 생겨야 휠이 만들어지는데(DanceWheelInstaller), 에디터는 계정 API 가
        /// localhost 라 로그인 · 로비 입장이 안 된다. 그래서 휠만 따로 돌린다. Q · 1~5 · Esc · 마우스 · 열리는
        /// 연출을 볼 수 있다. 고르면 "내 캐릭터가 없다" 경고가 뜨는 것은 정상이다 — 춤은 빌드에서 본다.
        ///
        /// ⚠ 새 씬을 연다. 열려 있던 씬에 저장 안 한 변경이 있으면 먼저 물어본다. 새 씬은 저장하지 않는다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 춤 휠 시험 (에디터)")]
        public static void TryInEditor()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[춤 휠 시험] 플레이 중에는 쓸 수 없습니다. 멈추고 다시 눌러 주세요.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output);
            if (prefab == null)
            {
                Debug.LogError($"[춤 휠 시험] {Output} 가 없습니다. 먼저 프리팹을 만드세요.");
                return;
            }

            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            // 휠 뒤가 너무 휑하지 않게 카메라 배경을 로비 바다색 쪽으로 둔다.
            Camera main = Camera.main;
            if (main != null)
            {
                main.clearFlags = CameraClearFlags.SolidColor;
                main.backgroundColor = new Color(0.42f, 0.58f, 0.60f);
            }

            PrefabUtility.InstantiatePrefab(prefab);
            Debug.Log("[춤 휠 시험] Play 합니다. Game 창을 한 번 클릭하고 Q 를 눌러 보세요.");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// 배치 모드용: 프리팹을 새로 만들고 → 칸 이름을 채우고(로비 춤 설치) → 캡처한다.
        /// 모양을 고치며 여러 번 돌려 볼 때 한 번에 끝내려고 둔다.
        /// </summary>
        public static void RebuildAndCaptureFromCommandLine()
        {
            Build();
            LobbyDanceSetup.Install();
            CaptureFromCommandLine();
        }

        public static void CaptureFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-captureOut");
            CaptureTo(at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetTempPath(), "DanceWheelCapture"));
        }

        private static void CaptureTo(string outDir)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output);
            if (prefab == null)
            {
                Debug.LogError($"[춤 휠 캡처] {Output} 가 없습니다. 먼저 프리팹을 만드세요.");
                return;
            }

            Directory.CreateDirectory(outDir);
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                const int W = 1920, H = 1080;
                var rt = new RenderTexture(W, H, 24);

                var camera = new GameObject("CaptureCamera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                // 로비 모래밭 · 바다 사이쯤의 색. 흰 칸이 밝은 바닥 위에서도 읽히는지 보려고 너무 어둡게 두지 않는다.
                camera.backgroundColor = new Color(0.42f, 0.58f, 0.60f);
                camera.orthographic = true;
                camera.targetTexture = rt;

                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.enabled = true;

                DanceWheelView view = root.GetComponent<DanceWheelView>();
                var so = new SerializedObject(view);
                SerializedProperty names = so.FindProperty("danceNames");
                SerializedProperty slots = so.FindProperty("slots");
                SerializedProperty labels = so.FindProperty("slotLabels");
                var center = (TMP_Text)so.FindProperty("centerLabel").objectReferenceValue;
                Color normal = so.FindProperty("normalColor").colorValue;
                Color hover = so.FindProperty("hoverColor").colorValue;
                float hoverScale = so.FindProperty("hoverScale").floatValue;

                // 실행 중에는 Awake 가 칸 이름을 채운다. 편집 중에는 안 불리므로 여기서 같은 일을 한다.
                for (int i = 0; i < labels.arraySize; i++)
                {
                    var label = (TMP_Text)labels.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (label != null && i < names.arraySize) label.text = names.GetArrayElementAtIndex(i).stringValue;
                }

                SerializedProperty roots = so.FindProperty("slotRoots");
                SerializedProperty borders = so.FindProperty("slotBorders");
                Color border = so.FindProperty("borderColor").colorValue;
                Color hoverBorder = so.FindProperty("hoverBorderColor").colorValue;
                string idle = so.FindProperty("idleCenterText").stringValue;

                // DanceWheelView.SetHovered 와 같은 일을 한다. 실행 중 모습과 같아야 캡처가 쓸모 있다.
                void Shot(string file, int hovered)
                {
                    for (int i = 0; i < slots.arraySize; i++)
                    {
                        var slot = (RingSegmentGraphic)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (slot == null) continue;
                        bool on = i == hovered;
                        slot.color = on ? hover : normal;

                        var edge = i < borders.arraySize
                            ? (RingSegmentGraphic)borders.GetArrayElementAtIndex(i).objectReferenceValue
                            : null;
                        if (edge != null) edge.color = on ? hoverBorder : border;

                        var slotRoot = i < roots.arraySize
                            ? (RectTransform)roots.GetArrayElementAtIndex(i).objectReferenceValue
                            : null;
                        (slotRoot != null ? slotRoot : slot.rectTransform).localScale = Vector3.one * (on ? hoverScale : 1f);
                    }

                    if (center != null)
                    {
                        center.text = hovered >= 0 && hovered < names.arraySize
                            ? names.GetArrayElementAtIndex(hovered).stringValue
                            : idle;
                    }

                    Canvas.ForceUpdateCanvases();
                    camera.Render();

                    var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = previous;

                    File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }

                Shot("wheel_idle.png", -1);
                Shot("wheel_hover1.png", 0);

                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Debug.Log($"[춤 휠 캡처] → {outDir}");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static RectTransform Child(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        private static RingSegmentGraphic Segment(string name, RectTransform parent, Color color)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * OuterRadius * 2f;

            RingSegmentGraphic graphic = rect.gameObject.AddComponent<RingSegmentGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static TMP_Text Text(string name, RectTransform parent, TMP_FontAsset font, float size, string value, Color color)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
