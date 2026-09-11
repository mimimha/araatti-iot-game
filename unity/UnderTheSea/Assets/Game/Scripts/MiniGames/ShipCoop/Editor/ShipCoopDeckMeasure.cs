using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배 모델의 실제 치수를 잰다. (SHIPCOOP.md 4장 · 10장)
///
/// 왜 필요한가
///   4장 원칙 3은 "이동을 포함해 일 하나에 10~15초" 를 전제로 사건 간격을 정했습니다.
///   그 10~15초는 **갑판 크기와 자리 간격에서 나오는 숫자**입니다.
///   갑판이 얼마나 큰지 모르면 사건 간격을 정할 수가 없습니다.
///
///   그래서 에셋을 붙이기 전에 **치수만 먼저** 재서, 그 치수로 회색 큐브 갑판을
///   다시 만듭니다. 모델은 밸런싱이 끝난 뒤에 켭니다. 그래야 자리를 옮길 때마다
///   지형에 맞추느라 느려지는 일이 없고, 나중에 모델을 켜도 자리가 안 움직입니다.
///
/// 쓰는 법
///   메뉴          Tools / ShipCoop / 배 치수 재기
///   커맨드라인    Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt;
///                   -executeMethod ShipCoopDeckMeasure.MeasureFromCommandLine -logFile -
///
/// ⚠ 에디터로 프로젝트를 열어둔 채로는 커맨드라인이 안 됩니다. (프로젝트가 잠깁니다)
/// </summary>
public static class ShipCoopDeckMeasure
{
    /// <summary>재볼 모델들. 없으면 조용히 건너뛴다.</summary>
    private static readonly string[] Targets =
    {
        "Assets/Stylized_Pirate_Ship/StylShip_3dModel/StylShip_Unity.fbx",
        "Assets/QuaterniusPirateKit/Ship_Small.fbx",
        "Assets/QuaterniusPirateKit/Characters_Henry.fbx",
        "Assets/QuaterniusPirateKit/Prop_Barrel.fbx",
        "Assets/QuaterniusPirateKit/Prop_Bucket.fbx",
        "Assets/QuaterniusPirateKit/Environment_Rock_1.fbx",
    };

    [MenuItem("Tools/ShipCoop/배 치수 재기")]
    public static void Measure()
    {
        Debug.Log(Report());
    }

    /// <summary>커맨드라인에서 부르는 입구.</summary>
    public static void MeasureFromCommandLine()
    {
        // Debug.Log 는 배치 모드 로그에서 다른 줄에 섞입니다.
        // 잘라내기 쉽게 앞뒤에 표시를 둡니다.
        System.Console.WriteLine("\n===== SHIPCOOP MEASURE START =====");
        System.Console.WriteLine(Report());
        System.Console.WriteLine("===== SHIPCOOP MEASURE END =====\n");
    }

    private static string Report()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("[배 치수]  단위 m. 씬에 기본 스케일로 놓았을 때의 크기.");

        for (int i = 0; i < Targets.Length; i++)
        {
            AppendOne(text, Targets[i]);
        }

        return text.ToString();
    }

    private static void AppendOne(StringBuilder text, string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        text.AppendLine();
        text.AppendLine("────────────────────────────────────────────");
        text.AppendLine(path);

        if (asset == null)
        {
            text.AppendLine("  (없음)");
            return;
        }

        GameObject instance = Object.Instantiate(asset);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        try
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                text.AppendLine("  (그릴 것이 없음)");
                return;
            }

            // 전체 크기
            Bounds total = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                total.Encapsulate(renderers[i].bounds);
            }

            text.AppendLine($"  전체   가로 {total.size.x:F2}  높이 {total.size.y:F2}  세로 {total.size.z:F2}");
            text.AppendLine($"  바닥   y = {total.min.y:F2}    꼭대기 y = {total.max.y:F2}");

            // 조각별 — 어느 것이 갑판인지 이름으로 찾아야 한다
            if (renderers.Length > 1)
            {
                text.AppendLine($"  조각 {renderers.Length}개:");

                List<Renderer> sorted = new List<Renderer>(renderers);
                sorted.Sort((a, b) => b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));

                int shown = Mathf.Min(sorted.Count, 16);
                for (int i = 0; i < shown; i++)
                {
                    Bounds b = sorted[i].bounds;
                    text.AppendLine(
                        $"    {sorted[i].name,-28} " +
                        $"가로 {b.size.x,6:F2}  높이 {b.size.y,6:F2}  세로 {b.size.z,6:F2}   " +
                        $"바닥 y {b.min.y,6:F2}");
                }

                if (sorted.Count > shown)
                {
                    text.AppendLine($"    … 그 외 {sorted.Count - shown}개");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
