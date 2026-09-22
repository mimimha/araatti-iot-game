using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배 협동 HUD 프리팹의 **상호작용 원형 버튼에 손그림(painterly) 한 쌍만** 갈아끼운다.
///
/// 왜 따로 있는가
///   <see cref="ShipCoopHudV2Art.Apply"/> 는 캔버스 자식을 전부 지우고 새로 짓습니다.
///   그러면 씬에 놓인 HUD 인스턴스의 override 가 통째로 끊어집니다. 그림 두 장
///   바꾸자고 치르기엔 큰 값이라, **있던 것은 그대로 두고 그림만** 바꿉니다.
///
///   실제로 입히는 일은 <see cref="ShipCoopHudV2Art.DressInteractRing"/> 이 합니다.
///   통째로 다시 짓는 길과 여기가 **같은 코드를 부르므로** 둘이 어긋나지 않습니다.
///
/// ⚠ **진행률(fillAmount)은 건드리지 않습니다.** 작업 이름 · 조작 안내 글줄도 그대로
///    둡니다 — 왼쪽으로 10px 민 자리를 포함해서, 이 도구는 링 안쪽만 만집니다.
///
/// 여러 번 눌러도 됩니다.
///
/// Tools > 아라아띠 > 배 협동 상호작용 링을 painterly 로 갈아끼우기
/// </summary>
public static class ShipCoopInteractRingPatch
{
    private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHud.prefab";

    private const string RingPath = "InteractPanel/Ring";
    private const string FillPath = "InteractPanel/Ring/Fill";
    private const string KeyPath = "InteractPanel/Ring/Key";

    [MenuItem("Tools/아라아띠/배 협동 상호작용 링을 painterly 로 갈아끼우기")]
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            Image badge = Find<Image>(root, RingPath);
            Image gauge = Find<Image>(root, FillPath);
            var key = Find<TextMeshProUGUI>(root, KeyPath);

            ShipCoopHudV2Art.DressInteractRing(badge, gauge, key);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[상호작용 링] painterly 로 갈아끼웠다 → {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>프리팹 안의 한 자리를 집는다. 없으면 무엇이 없는지 말하고 멈춘다.</summary>
    private static T Find<T>(GameObject root, string path) where T : Component
    {
        Transform found = root.transform.Find(path);

        if (found == null)
        {
            throw new MissingReferenceException(
                $"{PrefabPath} 에 '{path}' 가 없다. HUD 를 V2 로 먼저 지어야 한다.");
        }

        var component = found.GetComponent<T>();

        if (component == null)
        {
            throw new MissingComponentException($"'{path}' 에 {typeof(T).Name} 이 없다.");
        }

        return component;
    }
}
