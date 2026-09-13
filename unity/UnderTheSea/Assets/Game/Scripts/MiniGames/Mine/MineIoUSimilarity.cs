using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자를 그대로 비교하는 판정 — **정확히 같은 칸을 팠는가.**
///
///     유사도 = 교집합 / 합집합 × 100
///            = (목표에도 있고 실제로도 판 칸) / (목표이거나 판 칸)
///
/// ⚠ **더 이상 기본값이 아니다.** 기본값은 <see cref="MineShapeSimilarity"/> 다.
///
///   한 칸만 어긋나도 **완전히 틀린 것으로** 치는 것이 문제였다. 얇은 선은 한 칸
///   밀리면 겹치는 곳이 거의 없어서 100% 아니면 5% 고, 중간이 없다.
///
///   실측 — 20칸짜리 다이아몬드 윤곽선을 **한 칸도 틀리지 않고** 그렸는데
///   시작점이 한 줄 밀렸던 판에서 **5.3%** 가 나왔다. (교집합 2 / 합집합 38)
///   사람 눈에는 완벽한 다이아몬드였다.
///
///   이 게임은 어두운 곳에서 기억으로 그리는 게임이라 절대 위치를 맞히는 것은
///   실력이 아니라 운에 가깝다. 그래서 판정을 "정확한 칸" 에서 "닮음" 으로 바꿨다.
///   자세한 근거는 MINE.md 7장에 표로 남겨두었다.
///
///   지우지 않고 남겨둔 이유는 **비교용**이다. 두 방식을 오가며 같은 판을
///   채점해봐야 새 판정이 정말 나은지 말할 수 있다. MineGame 의 Judge 로 고른다.
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
    public MineSimilarityResult Evaluate(IReadOnlyList<bool> dug, IReadOnlyList<bool> target, int size)
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

        result.TargetCount = targetCount;
        result.DugCount = dugCount;
        result.Detail = $"교집합 {intersection} / 합집합 {union}";

        // 합집합이 0 = 아무것도 안 팠고 목표도 비었다. 0 으로 나누면 NaN 이 된다.
        result.Percent = union == 0 ? 0f : (float)intersection / union * 100f;

        return result;
    }
}