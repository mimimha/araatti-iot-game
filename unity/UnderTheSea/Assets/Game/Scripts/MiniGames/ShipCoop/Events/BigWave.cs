using UnityEngine;

/// <summary>
/// 🌊 거대한 파도. 조타로 방향을 맞춰 정면으로 돌파한다. (SHIPCOOP.md 5장)
///
/// 암초와 반대입니다. 암초는 꺾어서 피하고, 파도는 **꺾지 않고 정면으로 받아야** 합니다.
/// 그래서 암초 직후에 파도가 오면 방금 꺾은 것을 되돌려야 하고,
/// 그 사이에 조타를 비우면 둘 다 놓칩니다.
///
/// 버티는 시간 동안 정면을 벗어난 시간이 허용치를 넘으면 옆으로 맞아 배가 깎입니다.
/// </summary>
public class BigWave : VoyageEvent
{
    [Header("정면 판정")]
    [Tooltip("뱃머리가 이 각도 안에 있으면 정면으로 받는 것으로 본다.")]
    [SerializeField, Min(1f)] private float straightTolerance = 12f;

    [Tooltip("정면을 벗어나도 되는 시간 (초). 이만큼 넘게 벗어나면 실패한다.\n" +
             "0 으로 두면 한 순간도 벗어날 수 없어 너무 가혹하다.")]
    [SerializeField, Min(0f)] private float allowedOffTime = 1.5f;

    /// <summary>정면을 벗어나 있던 시간 (초)</summary>
    public float OffTime { get; private set; }

    /// <summary>지금 정면으로 받고 있는지</summary>
    public bool IsStraight { get; private set; }

    /// <summary>벗어난 시간이 허용치의 몇 %인지. HUD 게이지가 이걸 본다.</summary>
    public float OffTime01 =>
        allowedOffTime <= 0f ? (IsStraight ? 0f : 1f) : Mathf.Clamp01(OffTime / allowedOffTime);

    private HelmTask _helm;

    protected override void OnBegin()
    {
        OffTime = 0f;
        IsStraight = true;
        _helm = FindHelm();

        if (_helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 정면 판정을 할 수 없습니다.", this);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (_helm == null)
        {
            return;
        }

        IsStraight = Mathf.Abs(_helm.Heading) <= straightTolerance;

        if (IsStraight)
        {
            return;
        }

        OffTime += deltaTime;

        if (OffTime > allowedOffTime)
        {
            Fail();
        }
    }

    /// <summary>버틴 시간이 다 됐다. 정면을 지켰으면 넘긴 것이다.</summary>
    protected override void OnTimeout()
    {
        Succeed();
    }

    protected override void OnSucceed()
    {
        Game?.ReportObstacleAvoided();
    }

    /// <summary>HUD 문구</summary>
    public string StraightHint()
    {
        float heading = _helm != null ? _helm.Heading : 0f;
        return IsStraight
            ? $"정면 유지 중  ({heading:F0}°)"
            : $"뱃머리를 정면으로!  (지금 {heading:F0}°, 벗어난 시간 {OffTime:F1}/{allowedOffTime:F1}초)";
    }

    private static HelmTask FindHelm()
    {
        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is HelmTask helm)
            {
                return helm;
            }
        }

        return null;
    }
}
