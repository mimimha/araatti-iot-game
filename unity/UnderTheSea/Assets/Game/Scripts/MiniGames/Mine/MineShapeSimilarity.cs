using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **목표와 얼마나 닮았는가**를 재는 판정. (MINE.md 7장)
///
/// 두 단계다.
///   ① 위치 맞추기  — 무게중심을 겹친다. 통째로 밀려 그린 것은 여기서 사라진다.
///   ② 거리로 점수  — 칸이 정확히 겹치는지 세지 않고, 가장 가까운 상대 칸까지
///                    몇 칸 떨어졌는지를 본다. 가까우면 가까운 만큼 점수를 준다.
///
/// **양쪽에서 다 잰다.** 판 것 기준으로만 재면 판을 전부 파버리면 만점이 되고,
/// 목표 기준으로만 재면 덜 판 것이 안 보인다.
///
/// 거리는 체비쇼프(가로세로대각 모두 1칸)로 잰다. 대각으로도 걸어다닐 수 있으니
/// 대각선 이웃을 1칸으로 보는 것이 이 게임의 감각과 맞는다.
///
/// AI 를 쓰지 않는 이유는 필요 없어서가 아니라 **승패를 외부에 맡기지 않으려고** 다.
/// 이 계산은 즉시 끝나고, 같은 그림에는 항상 같은 점수가 나오고, 왜 그 점수인지 설명된다.
/// </summary>
public class MineShapeSimilarity : IMineSimilarity
{
    private readonly float _tolerance;
    private readonly int _maxAlign;

    /// <param name="tolerance">
    /// 몇 칸까지 어긋남을 봐줄 것인가. 0칸이면 만점, 이 값 이상 떨어지면 0점.
    /// </param>
    /// <param name="maxAlign">
    /// 위치를 맞출 때 최대 몇 칸까지 밀 수 있는가.
    /// 크게 잡을수록 "어디에 그렸는가" 를 안 보게 된다.
    /// </param>
    public MineShapeSimilarity(float tolerance = 2f, int maxAlign = 4)
    {
        _tolerance = Mathf.Max(0.5f, tolerance);
        _maxAlign = Mathf.Max(0, maxAlign);
    }

    public MineSimilarityResult Evaluate(IReadOnlyList<bool> dug, IReadOnlyList<bool> target, int size)
    {
        var result = new MineSimilarityResult();

        if (dug == null || target == null)
        {
            Debug.LogError($"{nameof(MineShapeSimilarity)}: 배열이 null 입니다.");
            return result;
        }

        if (dug.Count != target.Count || dug.Count != size * size)
        {
            Debug.LogError($"{nameof(MineShapeSimilarity)}: 칸 수가 맞지 않습니다. " +
                           $"판 격자 {dug.Count} / 도안 {target.Count} / size {size}.");
            return result;
        }

        List<Vector2Int> dugCells = Collect(dug, size);
        List<Vector2Int> targetCells = Collect(target, size);

        result.DugCount = dugCells.Count;
        result.TargetCount = targetCells.Count;

        if (targetCells.Count == 0)
        {
            result.Detail = "도안이 비어 있습니다";
            return result;
        }

        if (dugCells.Count == 0)
        {
            result.Detail = "아무것도 파지 않았습니다";
            return result;
        }

        // ① 무게중심으로 대강 맞춘 뒤, 한 칸씩 흔들어 가장 잘 맞는 자리를 고른다.
        //    중심만 쓰면 엉뚱한 데 판 몇 칸 때문에 중심이 끌려갈 수 있다.
        //
        // ⚠ **이건 부분 탐색이다.** 무게중심 언저리 아홉 자리만 본다.
        //   무게중심이 2칸 넘게 빗나가면 원점(0,0)이 후보에 아예 안 들어가서,
        //   **안 미는 것보다 못한 자리를 고를 수 있다.** 실제로 그런 판이 나왔다 —
        //   보정 없이 71.5% 인 판을 (1,0) 으로 밀어 70.9% 로 재는 경우.
        //
        //   지금은 maxAlign 이 0 이라 ClampShift 가 전부 (0,0) 으로 눌러서
        //   이 문제가 잠들어 있다. **maxAlign 을 다시 켜려면 탐색 범위부터
        //   maxAlign 전체로 넓혀야 한다.** (MINE.md 7장·12장)
        Vector2Int guess = ClampShift(Center(targetCells) - Center(dugCells));

        float best = -1f;
        Vector2Int bestShift = Vector2Int.zero;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Vector2Int shift = ClampShift(guess + new Vector2Int(dx, dy));
                float score = Score(dugCells, targetCells, shift);

                if (score <= best) continue;

                best = score;
                bestShift = shift;
            }
        }

        result.Percent = best;
        result.Alignment = bestShift;
        result.Detail = $"허용 오차 {_tolerance:0.#}칸";

        return result;
    }

    /// <summary>② 두 무리가 서로 얼마나 가까운지. 0 ~ 100.</summary>
    private float Score(List<Vector2Int> dug, List<Vector2Int> target, Vector2Int shift)
    {
        float total = 0f;

        // 판 것 → 목표. 쓸데없이 판 칸이 여기서 벌점을 받는다.
        foreach (Vector2Int d in dug)
        {
            total += Closeness(d + shift, target);
        }

        // 목표 → 판 것. 덜 판 칸이 여기서 벌점을 받는다.
        foreach (Vector2Int t in target)
        {
            total += Closeness(t, dug, shift);
        }

        return 100f * total / (dug.Count + target.Count);
    }

    private float Closeness(Vector2Int from, List<Vector2Int> to, Vector2Int shift = default)
    {
        int nearest = int.MaxValue;

        foreach (Vector2Int p in to)
        {
            Vector2Int q = p + shift;
            int d = Mathf.Max(Mathf.Abs(from.x - q.x), Mathf.Abs(from.y - q.y));

            if (d < nearest) nearest = d;
            if (nearest == 0) break;      // 더 가까울 수 없다
        }

        return Mathf.Max(0f, 1f - nearest / _tolerance);
    }

    private Vector2Int ClampShift(Vector2Int v)
    {
        return new Vector2Int(
            Mathf.Clamp(v.x, -_maxAlign, _maxAlign),
            Mathf.Clamp(v.y, -_maxAlign, _maxAlign));
    }

    private static Vector2Int Center(List<Vector2Int> cells)
    {
        int sx = 0, sy = 0;

        foreach (Vector2Int c in cells)
        {
            sx += c.x;
            sy += c.y;
        }

        return new Vector2Int(
            Mathf.RoundToInt((float)sx / cells.Count),
            Mathf.RoundToInt((float)sy / cells.Count));
    }

    private static List<Vector2Int> Collect(IReadOnlyList<bool> flags, int size)
    {
        var cells = new List<Vector2Int>();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (flags[y * size + x]) cells.Add(new Vector2Int(x, y));
            }
        }

        return cells;
    }
}