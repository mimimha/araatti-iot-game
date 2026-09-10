using System.Collections.Generic;

/// <summary>
/// 완성된 그림이 목표와 얼마나 닮았는지 재는 방법. (MINE.md 7장)
///
/// 인터페이스로 빼 둔 이유는 **판정 방식을 갈아끼울 수 있게** 하기 위해서다.
/// 첫 버전은 격자 비교(<see cref="MineIoUSimilarity"/>)이고,
/// 나중에 다른 방식(예: 스케치 분류기)으로 바꿀 때 이 파일을 구현하는 클래스만 새로 만들면 된다.
/// </summary>
public interface IMineSimilarity
{
    /// <summary>
    /// 판정한다. 두 배열은 같은 길이여야 하고, 인덱스는 y * size + x 형식이다.
    /// (<see cref="MineGrid.Cells"/> 와 <see cref="MineDrawingTarget.ToCells"/> 가 같은 형식을 쓴다)
    /// </summary>
    MineSimilarityResult Evaluate(IReadOnlyList<bool> dug, IReadOnlyList<bool> target);
}

/// <summary>
/// 판정 결과.
///
/// <see cref="Percent"/> 만 필수다. 나머지는 **진단용**이라 판정 방식에 따라 0 일 수 있다.
/// (분류기 같은 방식에는 교집합·합집합 개념이 없다)
/// </summary>
public struct MineSimilarityResult
{
    /// <summary>유사도. 0 ~ 100. 이 값이 점수가 되고 성공 여부를 가른다.</summary>
    public float Percent;

    /// <summary>목표에도 있고 실제로도 판 칸 수. (진단용)</summary>
    public int Intersection;

    /// <summary>목표이거나 판 칸 수. (진단용)</summary>
    public int Union;

    /// <summary>목표가 파라고 한 칸 수. (진단용)</summary>
    public int TargetCount;

    /// <summary>실제로 판 칸 수. (진단용)</summary>
    public int DugCount;

    public override string ToString()
    {
        return $"{Percent:0.0}% (교집합 {Intersection} / 합집합 {Union} · " +
               $"목표 {TargetCount}칸 · 판 것 {DugCount}칸)";
    }
}