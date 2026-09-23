using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배 협동 HUD 프리팹에 **"늦었다" 팻말만** 덧붙인다. (임시 — TMP 글씨 + 삼각형)
///
/// 왜 따로 있는가
///   <see cref="ShipCoopHudV2Art.Apply"/> 는 캔버스 자식을 전부 지우고 새로 짓습니다.
///   그러면 씬에 놓인 HUD 인스턴스의 override 가 통째로 끊어집니다. 팻말 하나
///   붙이자고 치르기엔 큰 값이라, **있던 것은 그대로 두고 이것만** 얹습니다.
///
///   실제로 짓는 일은 <see cref="ShipCoopHudV2Art.BuildHurryCallout"/> 이 합니다.
///   통째로 다시 짓는 길과 여기가 **같은 코드를 부르므로** 둘이 어긋나지 않습니다.
///
/// 여러 번 눌러도 됩니다. 이미 있으면 지우고 다시 짓습니다.
///
/// Tools > 아라아띠 > 배 협동 HUD 에 서두르기 팻말만 붙이기
/// </summary>
public static class ShipCoopHurryCalloutPatch
{
    private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHud.prefab";

    /// <summary>항해 바 안에서 팻말이 붙을 자리. 배 마커 · 붉은 구간과 같은 부모다.</summary>
    private const string TrackPath = "Voyage/Track";

    [MenuItem("Tools/아라아띠/배 협동 HUD 에 서두르기 팻말만 붙이기")]
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

            var track = (RectTransform)root.transform.Find(TrackPath);
            if (track == null)
            {
                throw new MissingReferenceException(
                    $"{PrefabPath} 에 '{TrackPath}' 가 없다. HUD 를 V2 로 먼저 지어야 한다.");
            }

            // 다시 눌렀을 때를 위해. 남겨두면 팻말이 두 장 겹친다.
            Transform old = track.Find("HurryCallout");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            RectTransform hurry = ShipCoopHudV2Art.BuildHurryCallout(
                track, out CanvasGroup group, out TextMeshProUGUI label, out RectTransform arrow);

            var so = new SerializedObject(hud);
            so.FindProperty("hurryCallout").objectReferenceValue = hurry.gameObject;
            so.FindProperty("hurryGroup").objectReferenceValue = group;
            so.FindProperty("hurryLabel").objectReferenceValue = label;
            so.FindProperty("hurryArrow").objectReferenceValue = arrow;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ⚠ **여기서 faceColor 를 건드리지 않는다.** HUD 를 통째로 짓는 쪽은
            //    SyncTextColors 로 맞춰 주지만, 이 글자는 그림자용 **공유 머티리얼**을
            //    쓰기 때문에 faceColor 를 쓰면 그 머티리얼 에셋의 _FaceColor 가 바뀐다.
            //    글자색은 vertex color(label.color) 만으로 나온다.

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[서두르기 팻말] {TrackPath} 아래에 붙였다 → {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
