using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 🪣 양동이 모델의 **단면을 높이별로 재서** 물 원판을 어디에 놓을지 정한다. (SHIPCOOP.md 4장)
///
/// <b>왜 필요한가.</b> 양동이와 손잡이가 **한 메시**라 이름으로 가를 수가 없다. 그래서 물 원판을
/// "전체 경계의 위에서 12% 아래" 같은 식으로 놓았는데, 그 전체 경계의 꼭대기는 양동이 테두리가 아니라
/// <b>손잡이 꼭대기</b>다. 물이 테두리보다 위에 떠 있는 것처럼 보였다.
///
/// 손잡이는 가늘고 몸통은 넓다. 그래서 높이를 잘라 **단면 반지름**을 재면 둘이 갈린다 —
/// 반지름이 갑자기 줄어드는 높이가 테두리다. 그 값을 재서 <c>ShipCoopCargoVisuals</c> 의 기본값에 넣는다.
///
/// <code>
///   메뉴      Tools/ShipCoop/양동이 단면 재기
///   배치 모드  Unity.exe -batchmode -quit -projectPath &lt;경로&gt; -executeMethod ShipCoopBucketMeasure.MeasureFromCommandLine
/// </code>
/// </summary>
public static class ShipCoopBucketMeasure
{
    private const string BucketModelPath = "Assets/QuaterniusPirateKit/Prop_Bucket.fbx";

    /// <summary>높이를 몇 칸으로 자를지. 촘촘할수록 테두리를 정확히 찾는다.</summary>
    private const int Slices = 20;

    /// <summary>몸통으로 치는 반지름 기준. 가장 넓은 곳의 이 비율 이상이면 몸통, 아래면 손잡이.</summary>
    private const float BodyShare = 0.7f;

    [MenuItem("Tools/ShipCoop/양동이 단면 재기")]
    public static void Measure()
    {
        Debug.Log(Report());
    }

    /// <summary>커맨드라인용.</summary>
    public static void MeasureFromCommandLine()
    {
        Debug.Log(Report());
        EditorApplication.Exit(0);
    }

    private static string Report()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine($"[양동이 단면] {BucketModelPath}");

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BucketModelPath);

        if (model == null)
        {
            text.AppendLine("  ⚠ 모델을 못 찾았습니다.");
            return text.ToString();
        }

        // 게임과 같은 방식으로 세워 본다 — FBX 루트의 축 변환 회전 · 배율을 그대로 둔 채 인스턴스로.
        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(model);

        try
        {
            Bounds box = default;
            bool any = false;

            foreach (Renderer draw in made.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { box = draw.bounds; any = true; }
                else { box.Encapsulate(draw.bounds); }
            }

            if (!any)
            {
                text.AppendLine("  ⚠ 렌더러가 없습니다.");
                return text.ToString();
            }

            text.AppendLine($"  전체 경계  가로 {box.size.x:F3} · 높이 {box.size.y:F3} · 세로 {box.size.z:F3} m");

            // 높이 칸마다 중심축에서 가장 먼 정점 거리(=반지름)를 잰다.
            float[] radius = new float[Slices];

            foreach (MeshFilter part in made.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh == null)
                {
                    continue;
                }

                Vector3[] points = part.sharedMesh.vertices;

                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 world = part.transform.TransformPoint(points[i]);
                    float t = Mathf.InverseLerp(box.min.y, box.max.y, world.y);
                    int slot = Mathf.Clamp(Mathf.FloorToInt(t * Slices), 0, Slices - 1);

                    float r = Vector2.Distance(
                        new Vector2(world.x, world.z),
                        new Vector2(box.center.x, box.center.z));

                    radius[slot] = Mathf.Max(radius[slot], r);
                }
            }

            float widest = 0f;

            for (int i = 0; i < Slices; i++)
            {
                widest = Mathf.Max(widest, radius[i]);
            }

            if (widest <= 0.0001f)
            {
                text.AppendLine("  ⚠ 정점을 읽지 못했습니다. (에디터에서는 읽혀야 정상입니다)");
                return text.ToString();
            }

            text.AppendLine("  높이별 반지름 (바닥 0% → 꼭대기 100%)");

            int rim = 0;

            for (int i = 0; i < Slices; i++)
            {
                float low = i / (float)Slices;
                float share = radius[i] / widest;
                bool body = share >= BodyShare;

                if (body)
                {
                    rim = i;
                }

                text.AppendLine($"    {low:P0,5} ~ {(i + 1) / (float)Slices:P0}  반지름 {radius[i]:F3}m ({share:P0}) {(body ? "몸통" : "손잡이")}");
            }

            // 테두리 = 몸통으로 친 가장 높은 칸의 윗면.
            float rimShare = (rim + 1) / (float)Slices;
            float rimY = Mathf.Lerp(box.min.y, box.max.y, rimShare);

            text.AppendLine();
            text.AppendLine($"  → 테두리는 바닥에서 {rimShare:P0} 높이 (y {rimY:F3}, 전체 높이의 {rimShare:P0})");
            text.AppendLine($"  → 몸통 반지름 {widest:F3}m = 지름 {widest * 2f:F3}m (전체 가로 {box.size.x:F3} 의 {widest * 2f / box.size.x:P0})");
            text.AppendLine();
            text.AppendLine("  ShipCoopCargoVisuals 기본값 제안");
            text.AppendLine($"    waterFill  {Mathf.Max(0f, rimShare - 0.06f):F2}   (테두리 조금 아래)");
            text.AppendLine($"    waterWidth {widest * 2f / box.size.x * 0.82f:F2}   (몸통 안쪽)");

            return text.ToString();
        }
        finally
        {
            Object.DestroyImmediate(made);
        }
    }
}
