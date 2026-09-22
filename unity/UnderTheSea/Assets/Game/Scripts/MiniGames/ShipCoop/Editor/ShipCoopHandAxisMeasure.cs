using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 🖐 손뼈의 **로컬 축이 어디를 보는지 재서**, 드는 자세(<see cref="ShipCoopCarryPose"/>)의 뼈 축 보정값을 낸다.
///
/// <b>왜 필요한가.</b> <c>Animator.SetIKRotation</c> 은 손뼈의 월드 회전을 그대로 정한다. 드는 자세 코드는
/// "손뼈 +Z 가 손가락, +Y 가 손바닥" 이라고 놓고 <c>LookRotation(앞, 손바닥쪽)</c> 을 줬는데, 리그마다 뼈 축이
/// 다르다. ithappy Cute_Characters 는 다른 축이 손가락을 가리켜서 두 손이 수평(손바닥 아래)으로 나왔다.
/// 그래서 리그의 실제 축을 재서 (+Z 손가락, +Y 손바닥) 으로 돌려 놓는 회전(boneFix)을 곱한다.
///
/// <b>어떻게 재나 — 자세와 무관하게 뼈 위치로.</b>
/// <code>
///   손가락 방향   손뼈 → 가운데손가락 첫 마디(Middle1) 방향
///   엄지 방향     손뼈 → 엄지 첫 마디(Thumb1) 방향
///   손바닥 노멀   왼손  cross(엄지, 손가락)
///                 오른손 cross(손가락, 엄지)      ← 미러라 순서가 반대. 손등이 아니라 손바닥이 나오게 부호를 맞췄다
///   boneFix       Inverse(LookRotation(손가락_로컬, 손바닥_로컬))
/// </code>
/// 손가락 · 엄지 뼈가 있는 리그라면 어떤 포즈에서 재도 같은 값이 나온다 — 뼈끼리의 상대 위치는 포즈로 안 바뀐다.
/// (손바닥 노멀은 엄지가 손바닥 평면 안에 있다는 가정이다. 사람 손이면 대개 맞다.)
///
/// <b>쓰는 법.</b> 캐릭터를 바꾸면 다시 잰다.
/// <code>
///   메뉴      Tools/ShipCoop/손뼈 축 재기  — 선택한 오브젝트(Animator 있는 것) 또는 ShipCoopPlayer.prefab
///   배치 모드  Unity.exe -batchmode -quit -projectPath &lt;경로&gt; -executeMethod ShipCoopHandAxisMeasure.MeasureFromCommandLine
/// </code>
/// 나온 Euler 를 <c>ShipCoopCarryPose.leftHandFixEuler / rightHandFixEuler</c> 기본값에 적는다.
/// 눈으로 확인하려면 CarryPose 의 <c>drawHandAxes</c> 를 켜고 들어 본다 (Play 모드, 빨강 X · 초록 Y · 파랑 Z).
/// </summary>
public static class ShipCoopHandAxisMeasure
{
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/ShipCoopPlayer.prefab";

    [MenuItem("Tools/ShipCoop/손뼈 축 재기 (드는 자세 보정값)")]
    public static void Measure()
    {
        Animator animator = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInChildren<Animator>(true)
            : null;

        if (animator != null)
        {
            Debug.Log(Report(animator, Selection.activeGameObject.name));
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"[손뼈 축 재기] 선택된 것도 없고 {PlayerPrefabPath} 도 없습니다.");
            return;
        }

        GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        try
        {
            animator = copy.GetComponentInChildren<Animator>(true);
            Debug.Log(animator != null
                ? Report(animator, PlayerPrefabPath)
                : $"[손뼈 축 재기] {PlayerPrefabPath} 에 Animator 가 없습니다.");
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }
    }

    /// <summary>커맨드라인용. 빈 씬에 프리팹을 세워 재고 로그에 남긴다. 실패하면 종료 코드 1.</summary>
    public static void MeasureFromCommandLine()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"[손뼈 축 재기] {PlayerPrefabPath} 를 못 찾았습니다.");
            EditorApplication.Exit(1);
            return;
        }

        GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Animator animator = copy.GetComponentInChildren<Animator>(true);

        if (animator == null)
        {
            Debug.LogError($"[손뼈 축 재기] {PlayerPrefabPath} 에 Animator 가 없습니다.");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log(Report(animator, PlayerPrefabPath));
        EditorApplication.Exit(0);
    }

    /// <summary>두 손을 재서 사람이 읽을 보고서로 만든다.</summary>
    public static string Report(Animator animator, string label)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine($"[손뼈 축 재기] {label}");

        if (!animator.isHuman)
        {
            text.AppendLine("  ⚠ 휴머노이드 리그가 아닙니다. HumanBodyBones 로 손뼈를 못 찾습니다.");
            return text.ToString();
        }

        Transform body = animator.transform;

        MeasureHand(animator, body, true, text);
        MeasureHand(animator, body, false, text);

        return text.ToString();
    }

    private static void MeasureHand(Animator animator, Transform body, bool left, StringBuilder text)
    {
        string side = left ? "왼손" : "오른손";

        Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        Transform middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal)
                           ?? animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        Transform thumb = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);

        if (hand == null || middle == null || thumb == null)
        {
            text.AppendLine($"  ⚠ {side}: 손뼈 · 손가락 · 엄지 중 없는 것이 있어 못 잽니다. " +
                            $"(손 {(hand != null)}, 손가락 {(middle != null)}, 엄지 {(thumb != null)})");
            return;
        }

        Vector3 fingerWorld = (middle.position - hand.position).normalized;
        Vector3 thumbWorld = (thumb.position - hand.position).normalized;

        // 손바닥이 밖으로 향하는 노멀. 왼손 · 오른손은 미러라 외적 순서가 반대다.
        Vector3 palmWorld = left
            ? Vector3.Cross(thumbWorld, fingerWorld).normalized
            : Vector3.Cross(fingerWorld, thumbWorld).normalized;

        Vector3 fingerLocal = hand.InverseTransformDirection(fingerWorld);
        Vector3 palmLocal = hand.InverseTransformDirection(palmWorld);

        // 이 회전을 곱하면 뼈 로컬 (손가락, 손바닥) 이 (+Z, +Y) 로 온다.
        Quaternion boneFix = Quaternion.Inverse(Quaternion.LookRotation(fingerLocal, palmLocal));

        text.AppendLine($"  {side}  뼈 {hand.name}, 손가락 기준 {middle.name}, 엄지 기준 {thumb.name}");
        text.AppendLine($"    손가락 방향  로컬 {Fmt(fingerLocal)}  ≈ {Nearest(fingerLocal)}   (캐릭터 기준 {Fmt(body.InverseTransformDirection(fingerWorld))})");
        text.AppendLine($"    손바닥 노멀  로컬 {Fmt(palmLocal)}  ≈ {Nearest(palmLocal)}   (캐릭터 기준 {Fmt(body.InverseTransformDirection(palmWorld))})");
        text.AppendLine($"    → boneFix(날것) Euler {Fmt(Round(boneFix.eulerAngles))}   (검산: 손가락→{Fmt(boneFix * fingerLocal)}, 손바닥→{Fmt(boneFix * palmLocal)})");

        // 리그의 뼈 축은 대개 정확히 한 축에 붙어 있다. 엄지뼈는 손바닥 평면에서 떠 있기 마련이라(사람 손도 그렇다)
        // 날것 손바닥 노멀은 손가락 축을 중심으로 몇십 도 기울 수 있다. 가장 가까운 축에 붙인 값도 함께 낸다.
        Vector3 fingerSnapped = Snap(fingerLocal);
        Vector3 palmSnapped = Snap(palmLocal);

        if (Vector3.Dot(fingerSnapped, palmSnapped) != 0f)
        {
            text.AppendLine("    ⚠ 손가락 축과 손바닥 축이 같은 축에 붙었습니다. 날것을 쓰세요.");
            return;
        }

        Quaternion snappedFix = Quaternion.Inverse(Quaternion.LookRotation(fingerSnapped, palmSnapped));
        float tilt = Vector3.Angle(palmLocal, palmSnapped);

        text.AppendLine($"    → boneFix(축에 붙임) Euler {Fmt(Round(snappedFix.eulerAngles))}   손가락 {Nearest(fingerLocal)} → +Z, 손바닥 {Nearest(palmLocal)} → +Y" +
                        $"   (날것과 {tilt:F1}° 차이 — 엄지가 손바닥 평면에서 뜬 만큼. 손이 그만큼 돌아 보이면 날것으로)");
    }

    private static Vector3 Snap(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);

        if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
        if (ay >= az) return new Vector3(0f, Mathf.Sign(v.y), 0f);
        return new Vector3(0f, 0f, Mathf.Sign(v.z));
    }

    private static string Nearest(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);

        if (ax >= ay && ax >= az) return v.x >= 0f ? "+X" : "-X";
        if (ay >= az) return v.y >= 0f ? "+Y" : "-Y";
        return v.z >= 0f ? "+Z" : "-Z";
    }

    private static Vector3 Round(Vector3 e)
    {
        // 0~360 을 -180~180 으로. 인스펙터에 적기 좋다.
        return new Vector3(Wrap(e.x), Wrap(e.y), Wrap(e.z));
    }

    private static float Wrap(float deg)
    {
        deg = Mathf.Round(deg * 10f) / 10f;
        return deg > 180f ? deg - 360f : deg;
    }

    private static string Fmt(Vector3 v)
    {
        return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}
