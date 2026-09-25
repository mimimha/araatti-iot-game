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
        private const float InnerRadius = 96f;
        private const float OuterRadius = 232f;
        private const float Gap = 14f;
        private const float HubRadius = 82f;
        private const float SlotFontSize = 28f;
        private const float CenterFontSize = 26f;

        private static readonly Color SlotColor = new Color(0.96f, 0.93f, 0.86f, 0.78f);
        private static readonly Color HubColor = new Color(0.96f, 0.93f, 0.86f, 0.55f);
        private static readonly Color TextColor = new Color(0.32f, 0.24f, 0.16f, 1f);

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
            wheel.sizeDelta = Vector2.one * OuterRadius * 2f;

            RingSegmentGraphic hub = Segment("Hub", wheel, HubColor);
            hub.Configure(0.01f, HubRadius, 0f, 360f, 0f);

            float slotSweep = 360f / SlotCount;
            float labelRadius = (InnerRadius + OuterRadius) * 0.5f;

            RingSegmentGraphic[] slots = new RingSegmentGraphic[SlotCount];
            TMP_Text[] labels = new TMP_Text[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                // 칸 0 은 위쪽 가운데. 시계 방향이라 수학 각도로는 줄어든다.
                float center = 90f - i * slotSweep;

                RingSegmentGraphic slot = Segment($"Slot{i + 1}", wheel, SlotColor);
                slot.Configure(InnerRadius, OuterRadius, center - slotSweep * 0.5f, slotSweep, Gap);
                slots[i] = slot;

                // 칸 이름은 칸의 자식으로 둔다 — 칸이 커질 때 같이 바깥으로 밀려난다.
                float rad = center * Mathf.Deg2Rad;
                TMP_Text label = Text("Label", slot.rectTransform, font, SlotFontSize, $"춤 {i + 1}");
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * labelRadius;
                label.rectTransform.sizeDelta = new Vector2(OuterRadius - InnerRadius, 60f);
                labels[i] = label;
            }

            TMP_Text centerLabel = Text("CenterLabel", wheel, font, CenterFontSize, string.Empty);
            centerLabel.rectTransform.anchoredPosition = Vector2.zero;
            centerLabel.rectTransform.sizeDelta = new Vector2(HubRadius * 2f - 12f, 60f);

            DanceWheelView view = root.AddComponent<DanceWheelView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("wheel").objectReferenceValue = wheel;
            so.FindProperty("centerLabel").objectReferenceValue = centerLabel;
            so.FindProperty("normalColor").colorValue = SlotColor;
            so.FindProperty("labelColor").colorValue = TextColor;

            SerializedProperty slotsProp = so.FindProperty("slots");
            SerializedProperty labelsProp = so.FindProperty("slotLabels");
            slotsProp.arraySize = SlotCount;
            labelsProp.arraySize = SlotCount;
            for (int i = 0; i < SlotCount; i++)
            {
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 처음에는 닫혀 있다. 켜고 끄는 것은 Canvas.enabled 다 — 루트는 설치기가 로비에서만 켠다.
            canvas.enabled = false;

            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(root, Output);
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            Debug.Log($"[춤 휠] 만들었습니다 — {Output}");
        }

        public static void BuildFromCommandLine() => Build();

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

        private static TMP_Text Text(string name, RectTransform parent, TMP_FontAsset font, float size, string value)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
