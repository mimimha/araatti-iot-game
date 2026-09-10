using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자를 그대로 비교하는 판정. (MINE.md 7장)
///
///     유사도 = 교집합 / 합집합 × 100
///            = (목표에도 있고 실제로도 판 칸) / (목표이거나 판 칸)
///
/// **단순 일치율을 쓰지 않는 이유** — 20×20 에서 목표가 20칸뿐이면 빈 칸이 95% 다.
/// 아무것도 안 파도 일치율이 95% 로 나온다. IoU 는 그 문제가 없다.
/// 아무것도 안 파면 교집합이 0 이라 0% 가 된다.
///
/// MonoBehaviour 가 아니라 평범한 클래스다. 씬에 붙일 것이 아니고,
/// 게임 실행 없이도 값을 확인할 수 있어야 한다.
/// </summary>
public class MineIoUSimilarity : IMineSimilarity
{
    public MineSimilarityResult Evaluate(IReadOnlyList<bool> dug, IReadOnlyList<bool> target)
    {
        var result = new MineSimilarityResult();

        if (dug == null || target == null)
        {
            Debug.LogError("MineIoUSimilarity: 배열이 null 입니다.");
            return result;
        }

        // 길이가 다르면 인덱스가 어긋나 엉뚱한 칸을 비교한다.
        // 격자 Size 와 도안 size 가 다를 때 생기는 사고라 조용히 넘기지 않는다.
        if (dug.Count != target.Count)
        {
            Debug.LogError($"MineIoUSimilarity: 칸 수가 다릅니다. " +
                           $"판 격자 {dug.Count} vs 도안 {target.Count}. " +
                           $"MineGrid 의 Size 와 MineDrawingTarget 의 size 를 맞추세요.");
            return result;
        }

        int intersection = 0;
        int union = 0;
        int targetCount = 0;
        int dugCount = 0;

        for (int i = 0; i < dug.Count; i++)
        {
            bool d = dug[i];
            bool t = target[i];

            if (d) dugCount++;
            if (t) targetCount++;
            if (d && t) intersection++;
            if (d || t) union++;
        }

        result.Intersection = intersection;
        result.Union = union;
        result.TargetCount = targetCount;
        result.DugCount = dugCount;

        // 합집합이 0 = 아무것도 안 팠고 목표도 비었다. 0 으로 나누면 NaN 이 된다.
        result.Percent = union == 0 ? 0f : (float)intersection / union * 100f;

        return result;
    }
}