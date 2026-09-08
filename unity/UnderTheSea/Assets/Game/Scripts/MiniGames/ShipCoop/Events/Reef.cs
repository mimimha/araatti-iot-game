using UnityEngine;

/// <summary>
/// 🪨 암초. 조타로 피한다. (SHIPCOOP.md 5장)
///
/// 시작할 때 피해야 할 방향이 정해집니다. 제한 시간 안에 뱃머리를 그쪽으로
/// 충분히 꺾으면 넘어가고, 못 꺾으면 부딪혀서 배가 크게 깎입니다.
///
/// 부딪히면 선체가 파손됩니다. 연쇄 목록에 HullDamage 를 넣어두면
/// "암초에 부딪힘 → 선체 파손 → 수리할 사람이 빠짐 → 대포가 빔" 이 만들어집니다.
/// </summary>
public class Reef : VoyageEvent
{
    [Header("회피")]
    [Tooltip("뱃머리를 이 각도 이상 꺾어야 피한 것으로 본다.")]
    [SerializeField, Min(5f)] private float dodgeAngle = 30f;

    [Tooltip("켜면 피해야 할 방향을 매번 무작위로 정한다.\n" +
             "끄면 아래 고정 방향을 쓴다. 스케줄을 외우게 하고 싶지 않으면 켠다.")]
    [SerializeField] private bool randomSide = true;

    [Tooltip("randomSide 를 끌 때 쓸 방향. 양수면 우현, 음수면 좌현.")]
    [SerializeField] private float fixedSide = 1f;

    /// <summary>피해야 할 방향. +1 이면 우현, -1 이면 좌현.</summary>
    public float DodgeSide { get; private set; } = 1f;

    /// <summary>지금 꺾어야 하는 각도</summary>
    public float TargetAngle => DodgeSide * dodgeAngle;

    private HelmTask _helm;

    protected override void OnBegin()
    {
        _helm = FindHelm();

        DodgeSide = randomSide
            ? (Random.value < 0.5f ? -1f : 1f)
            : Mathf.Sign(fixedSide == 0f ? 1f : fixedSide);

        if (_helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 피할 방법이 없어 그대로 부딪힙니다.", this);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (_helm == null)
        {
            return;
        }

        // 피해야 할 쪽으로 충분히 꺾였는지
        if (_helm.Heading * DodgeSide >= dodgeAngle)
        {
            Succeed();
        }
    }

    protected override void OnSucceed()
    {
        // 피한 것도 점수다. (2장 — 피한 암초 · 파도 × 20)
        Game?.ReportObstacleAvoided();
    }

    /// <summary>HUD 가 "좌현으로 꺾어!" 를 띄우는 데 쓴다.</summary>
    public string DodgeHint()
    {
        string side = DodgeSide < 0f ? "좌현" : "우현";
        float now = _helm != null ? _helm.Heading * DodgeSide : 0f;
        return $"{side}으로 {dodgeAngle:F0}° 꺾어라  (지금 {now:F0}°)";
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
