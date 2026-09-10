using UnityEngine;

/// <summary>
/// 🪨 암초. 조타로 피한다. (SHIPCOOP.md 5장)
///
/// **바다 위에 실제로 떠 있는 바위입니다.** 오른쪽에 보이면 왼쪽으로 꺾습니다.
/// 글자로 "우현으로 30° 꺾어라" 를 읽을 필요가 없습니다. 보고 피하면 됩니다.
///
/// 예고 동안 바위가 수평선에 나타나 다가옵니다. 배에 닿는 순간 판정합니다.
/// **각도가 아니라 거리로 봅니다.** 지나가는 순간 옆으로 충분히 비켜 있으면 넘어갑니다.
///
/// 부딪히면 선체가 파손됩니다. 연쇄 목록에 HullDamage 를 넣어두면
/// "암초에 부딪힘 → 선체 파손 → 수리할 사람이 빠짐 → 대포가 빔" 이 만들어집니다.
///
/// 씬에 <see cref="VoyageSea"/> 가 없으면 예전처럼 각도로 판정합니다.
/// 바다를 아직 안 만든 씬에서도 돌아가야 하기 때문입니다.
/// </summary>
public class Reef : VoyageEvent
{
    [Header("바다 위 바위")]
    [Tooltip("띄울 바위. 비워두면 회색 큐브를 만들어 쓴다. (프로토타입용)")]
    [SerializeField] private GameObject rockPrefab;

    [Tooltip("바위가 뱃길 중심에서 좌우로 얼마나 치우쳐 있는지 (m).\n" +
             "0 이면 정면이라 어느 쪽으로 피할지 알 수 없다. 조금 치우쳐야 읽힌다.")]
    [SerializeField, Min(0f)] private float laneOffset = 1f;

    [Tooltip("지나가는 순간 이만큼 떨어져 있으면 피한 것으로 본다 (m).")]
    [SerializeField, Min(0.5f)] private float safeGap = 2.5f;

    [Header("회피 — 바다가 없을 때만 쓴다")]
    [Tooltip("뱃머리를 이 각도 이상 꺾어야 피한 것으로 본다.")]
    [SerializeField, Min(5f)] private float dodgeAngle = 30f;

    [Header("방향")]
    [Tooltip("켜면 피해야 할 방향을 매번 무작위로 정한다.\n" +
             "끄면 아래 고정 방향을 쓴다. 스케줄을 외우게 하고 싶지 않으면 켠다.")]
    [SerializeField] private bool randomSide = true;

    [Tooltip("randomSide 를 끌 때 쓸 방향. 양수면 우현, 음수면 좌현.")]
    [SerializeField] private float fixedSide = 1f;

    /// <summary>바위가 있는 쪽. +1 이면 우현, -1 이면 좌현. 피할 방향은 그 반대다.</summary>
    public float RockSide { get; private set; } = 1f;

    /// <summary>바위가 놓인 좌우 자리 (m)</summary>
    public float LaneX => RockSide * laneOffset;

    private HelmTask _helm;
    private GameObject _rock;

    /// <summary>
    /// 예고 시작. **여기서 바위를 띄운다.**
    ///
    /// 방향도 여기서 정합니다. 터질 때 정하면 예고 동안 어느 쪽인지 알 수 없어
    /// 뛰어가면서 판단할 수가 없습니다.
    /// </summary>
    protected override void OnWarn()
    {
        _helm = FindHelm();

        RockSide = randomSide
            ? (Random.value < 0.5f ? -1f : 1f)
            : Mathf.Sign(fixedSide == 0f ? 1f : fixedSide);

        if (_helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 피할 방법이 없어 그대로 부딪힙니다.", this);
        }

        if (VoyageSea.Current == null)
        {
            return;
        }

        if (rockPrefab != null)
        {
            _rock = Instantiate(rockPrefab);
        }
        else
        {
            // 프로토타입. 에셋이 오면 rockPrefab 을 채우면 된다.
            _rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _rock.transform.localScale = new Vector3(2f, 1.6f, 2f);
        }

        _rock.name = $"Reef_{Time.frameCount}";
        VoyageSea.Current.Place(_rock.transform, LaneX, Approach01);
    }

    /// <summary>예고 때부터 계속 다가온다. 판정은 안 한다.</summary>
    protected override void OnShow(float deltaTime)
    {
        if (_rock != null && VoyageSea.Current != null)
        {
            VoyageSea.Current.Place(_rock.transform, LaneX, Approach01);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        // 바다가 있으면 지나가는 순간에 판정한다. 미리 넘기지 않는다.
        // 바위가 아직 저 앞에 있는데 "피했다" 가 뜨면 눈과 다르다.
        if (VoyageSea.Current != null)
        {
            return;
        }

        if (_helm != null && _helm.Heading * RockSide <= -dodgeAngle)
        {
            Succeed();
        }
    }

    /// <summary>바위가 배에 닿았다. 여기서 한 번만 판정한다.</summary>
    protected override void OnTimeout()
    {
        if (VoyageSea.Current == null)
        {
            base.OnTimeout();
            return;
        }

        float gap = VoyageSea.Current.LateralGap(LaneX);

        if (gap >= safeGap)
        {
            Debug.Log($"[{name}] 비켜서 지나갔다. 간격 {gap:F1}m (필요 {safeGap:F1}m)", this);
            Succeed();
            return;
        }

        Debug.Log($"[{name}] 부딪혔다. 간격 {gap:F1}m (필요 {safeGap:F1}m)", this);
        Fail();
    }

    protected override void OnSucceed()
    {
        // 피한 것도 점수다. (2장 — 피한 암초 · 파도 × 20)
        Game?.ReportObstacleAvoided();
    }

    /// <summary>끝났으면 바위를 치운다. 성공·실패·취소 모두 여기를 지난다.</summary>
    protected override void OnHide()
    {
        if (_rock == null)
        {
            return;
        }

        // 띄운 것은 프리팹을 복제했든 큐브를 만들었든 전부 이 사건이 만든 것이다.
        Destroy(_rock);
        _rock = null;
    }

    /// <summary>
    /// 디버그 오버레이가 띄우는 한 줄. (F1)
    ///
    /// 바다가 있으면 "어느 쪽 바위인지, 지금 얼마나 비켰는지" 를 보여줍니다.
    /// 화면에 바위가 보이는데 판정이 이상하면 여기 숫자와 눈을 맞춰 보면 됩니다.
    /// </summary>
    public string DodgeHint()
    {
        string side = RockSide < 0f ? "좌현" : "우현";

        if (VoyageSea.Current == null)
        {
            float turned = _helm != null ? _helm.Heading * -RockSide : 0f;
            return $"{side} 암초 — 반대로 {dodgeAngle:F0}° 꺾어라  (지금 {turned:F0}°)";
        }

        float gap = VoyageSea.Current.LateralGap(LaneX);
        string verdict = gap >= safeGap ? "지금이면 피한다" : "지금이면 부딪힌다";

        return $"{side} 암초 — 간격 {gap:F1}m / 필요 {safeGap:F1}m  ({verdict}, 도달까지 {Approach01:P0})";
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
