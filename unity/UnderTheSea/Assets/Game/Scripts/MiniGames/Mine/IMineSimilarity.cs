using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 완성된 그림이 목표와 얼마나 닮았는지 재는 방법. (MINE.md 7장)
///
/// 인터페이스로 빼 둔 이유는 **판정 방식을 갈아끼울 수 있게** 하기 위해서다.
/// 실제로 한 번 갈아끼웠다 — 처음엔 격자 비교(<see cref="MineIoUSimilarity"/>)였는데
/// 만들어서 재보니 한 칸만 밀려도 0점이라 <see cref="MineShapeSimilarity"/> 로 바꿨다.
/// </summary>
public interface IMineSimilarity
{
    /// <summary>
    /// 판정한다. 두 배열은 길이가 같아야 하고, 인덱스는 y * size + x 형식이다.
    /// (<see cref="MineGrid.Cells"/> 와 <see cref="MineDrawingTarget.ToCells"/> 가 같은 형식)
    /// </summary>
    /// <param name="size">한 변의 칸 수. 인덱스를 좌표로 풀 때 쓴다.</param>
    MineSimilarityResult Evaluate(IReadOnlyList<bool> dug, IReadOnlyList<bool> target, int size);
}

/// <summary>
/// 판정 결과.
///
/// <see cref="Percent"/> 만 필수다. 나머지는 **진단용**이라 판정 방식에 따라 비어 있을 수 있다.
/// </summary>
public struct MineSimilarityResult
{
    /// <summary>유사도. 0 ~ 100. 이 값이 점수가 되고 성공 여부를 가른다.</summary>
    public float Percent;

    /// <summary>목표가 파라고 한 칸 수.</summary>
    public int TargetCount;

    /// <summary>실제로 판 칸 수.</summary>
    public int DugCount;

    /// <summary>
    /// 위치를 맞추려고 판 것을 민 칸 수. 위치를 안 맞추는 방식이면 (0, 0).
    /// 결과 화면에서 "한 칸 밀렸습니다" 같은 말을 만들 때 쓴다.
    /// </summary>
    public Vector2Int Alignment;

    /// <summary>방식마다 다른 설명 한 줄. 로그와 결과 화면에 그대로 쓴다.</summary>
    public string Detail;

    public override string ToString()
    {
        string shift = Alignment == Vector2Int.zero
            ? string.Empty
            : $" · 위치 {Alignment.x},{Alignment.y} 보정";

        return $"{Percent:0.0}% (목표 {TargetCount}칸 · 판 것 {DugCount}칸{shift}" +
               $"{(string.IsNullOrEmpty(Detail) ? string.Empty : " · " + Detail)})";
    }
}