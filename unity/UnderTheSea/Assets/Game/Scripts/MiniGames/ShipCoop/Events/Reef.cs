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
    // ------------------------------------------------------------
    // ⚠ **여러 개를 넣고 매번 다른 것을 띄웁니다.**
    //
    //    하나만 쓰면 두 번째부터는 "아, 그거" 가 되어 긴장이 사라집니다.
    //    로비가 쓰는 Synty 바위 중 큰 것들을 넣어 둡니다.
    //    (배치 도구가 채웁니다. 비워두면 회색 큐브로 돌아갑니다)
    // ------------------------------------------------------------
    [Tooltip("띄울 바위들. 이 중에서 매번 무작위로 하나를 고른다.\n" +
             "비워두면 회색 큐브를 만들어 쓴다. (프로토타입용)")]
    [SerializeField] private GameObject[] rockPrefabs;

    [Tooltip("큐브로 만들 때 입힐 색. 비워두면 회색이라 바다에서 안 보인다.")]
    [SerializeField] private Material rockMaterial;

    [Tooltip("바위를 물에 이만큼 잠근다 (바위 높이 대비 비율).\n" +
             "0 이면 수면 위에 통째로 떠 있어서 바위가 아니라 배처럼 보인다.")]
    [SerializeField, Range(0f, 0.9f)] private float sink = 0.35f;

    // ------------------------------------------------------------
    // 크기에 맞춰 **아픈 정도와 피할 폭**이 같이 커집니다
    //
    // ⚠ 16m 짜리 바위와 9m 짜리 바위가 똑같이 깎으면 **보이는 것과 아픈 정도가
    //    따로 놉니다.** 큰 바위가 무섭게 생겼는데 안 아프면 피할 이유를 눈으로
    //    못 읽습니다. 반대로 작은 바위에 크게 맞으면 억울합니다.
    // ------------------------------------------------------------

    [Tooltip("이 폭(m)인 바위가 인스펙터에 적힌 대가를 그대로 준다.\n" +
             "더 크면 비례해서 더 아프고, 작으면 덜 아프다.")]
    [SerializeField, Min(1f)] private float normalWidth = 10f;

    [Tooltip("아무리 커도 이 배수를 넘지 않는다.")]
    [SerializeField, Min(1f)] private float mostScale = 2f;

    // ⚠ **1m 는 돛 뒤였습니다.** 돛이 정중앙에 있어서 화면 한가운데를 거의 다
    //    가립니다. 1m 옆이면 여전히 돛에 가려 암초가 보이지 않습니다.
    //    3.5m 쯤 빼야 돛 옆으로 나옵니다.
    [Tooltip("바위가 뱃길 중심에서 좌우로 얼마나 치우쳐 있는지 (m).\n\n" +
             "0 이면 정면이라 어느 쪽으로 피할지 알 수 없다.\n" +
             "⚠ 너무 작으면 돛에 가려서 아예 안 보인다.")]
    [SerializeField, Min(0f)] private float laneOffset = 3.5f;

    [Tooltip("이만큼 떨어져 있으면 피한 것으로 본다 (m).")]
    [SerializeField, Min(0.5f)] private float safeGap = 5f;

    // ⚠ 0.5 로 두면 폭의 절반이 더해져서 **어떤 바위도 못 피합니다.**
    //    조타를 끝까지 꺾어도 4.8m 밖에 안 밀리기 때문입니다.
    [Tooltip("바위 폭 1m 당 필요 간격이 이만큼 늘어난다 (m). 큰 바위는 더 꺾어야 한다.")]
    [SerializeField, Range(0f, 0.3f)] private float widthPenalty = 0.08f;

    [Header("뱃머리")]
    [Tooltip("배를 못 찾을 때 쓸 뱃머리 z (m). 배치 도구가 재어서 넣는다.")]
    [SerializeField] private float fallbackBowZ = 22.8f;

    /// <summary>배 오브젝트 이름. 뱃머리를 재려고 찾는다.</summary>
    private const string ShipName = "PirateShip";

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

    /// <summary>띄운 바위의 폭 (m). 클수록 크게 꺾어야 하고 크게 아프다.</summary>
    public float RockWidth { get; private set; } = 2f;

    /// <summary>띄운 바위의 높이 (m). 물에 잠그는 데 쓴다.</summary>
    public float RockHeight { get; private set; } = 1.6f;

    /// <summary>이번 바위를 이미 판정했는가. 뱃머리를 넘는 순간 한 번만 한다.</summary>
    private bool _judged;

    /// <summary>재어둔 뱃머리 z. 배는 안 바뀌므로 한 번만 잰다.</summary>
    private float? _bowZ;

    /// <summary>
    /// 예고 시작. **여기서 바위를 띄운다.**
    ///
    /// 방향도 여기서 정합니다. 터질 때 정하면 예고 동안 어느 쪽인지 알 수 없어
    /// 뛰어가면서 판단할 수가 없습니다.
    /// </summary>
    protected override void OnWarn()
    {
        _helm = FindHelm();

        // 지난 판의 판정이 남아 있으면 이번 바위를 그냥 지나칩니다.
        _judged = false;

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

        GameObject pick = PickRock();

        if (pick != null)
        {
            _rock = Instantiate(pick);
        }
        else
        {
            // 프로토타입. 에셋이 오면 rockPrefabs 를 채우면 된다.
            _rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _rock.transform.localScale = new Vector3(2f, 1.6f, 2f);
            VoyageSea.Paint(_rock, rockMaterial);
        }

        _rock.name = $"Reef_{Time.frameCount}";

        // 같은 바위라도 매번 다르게 보이도록 돌려 놓는다.
        _rock.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        MeasureRock();

        VoyageSea.Current.Place(_rock.transform, LaneX, Approach01);
        SinkRock();
    }

    // 넣어둔 것 중에 하나. 비어 있으면 null.
    private GameObject PickRock()
    {
        if (rockPrefabs == null || rockPrefabs.Length == 0)
        {
            return null;
        }

        // 비어 있는 칸은 건너뛴다. 인스펙터에서 지우다 만 칸이 자주 남는다.
        var ok = new System.Collections.Generic.List<GameObject>();

        for (int i = 0; i < rockPrefabs.Length; i++)
        {
            if (rockPrefabs[i] != null)
            {
                ok.Add(rockPrefabs[i]);
            }
        }

        return ok.Count == 0 ? null : ok[Random.Range(0, ok.Count)];
    }

    // 띄운 바위의 실제 폭과 높이를 재서, 아픈 정도와 피할 폭을 거기에 맞춘다.
    private void MeasureRock()
    {
        Renderer[] draws = _rock.GetComponentsInChildren<Renderer>();

        if (draws.Length == 0)
        {
            RockWidth = 2f;
            RockHeight = 1.6f;
            FailScale = 1f;
            return;
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        RockWidth = Mathf.Max(box.size.x, box.size.z);
        RockHeight = box.size.y;

        // 큰 바위는 더 아프다. 아무리 커도 mostScale 을 넘지 않는다.
        FailScale = Mathf.Clamp(RockWidth / normalWidth, 0.4f, mostScale);
    }

    // 물에 잠근다. 통째로 떠 있으면 바위가 아니라 배처럼 보인다.
    private void SinkRock()
    {
        if (_rock == null || RockHeight <= 0f)
        {
            return;
        }

        _rock.transform.position += new Vector3(0f, -RockHeight * sink, 0f);
    }

    /// <summary>
    /// 예고 때부터 계속 다가온다. **뱃머리에 닿는 순간 판정한다.**
    ///
    /// ⚠ 전에는 `OnTimeout` 에서 판정했는데, 그때 바위는 `z = -passDistance`,
    ///    즉 **배를 다 지나 12m 뒤**에 있었습니다. 뱃머리가 22.8m 앞이니
    ///    바위가 배를 통째로 관통한 뒤에야 "부딪혔다" 가 떴습니다.
    ///    보이는 것과 판정이 35m 어긋나 있었습니다.
    ///
    ///    그래서 바위의 z 를 직접 보고, 뱃머리 선을 넘는 순간 한 번 판정합니다.
    /// </summary>
    protected override void OnShow(float deltaTime)
    {
        if (_rock == null || VoyageSea.Current == null)
        {
            return;
        }

        VoyageSea.Current.Place(_rock.transform, LaneX, Approach01);
        SinkRock();

        if (_judged || !IsRunning)
        {
            return;
        }

        // 바위의 앞면이 뱃머리를 넘었는가. 가운데가 아니라 앞면으로 본다.
        float nose = _rock.transform.position.z - RockWidth * 0.5f;

        if (nose <= BowZ)
        {
            _judged = true;
            Judge();
        }
    }

    /// <summary>
    /// 뱃머리의 z. 배를 재서 찾고, 못 찾으면 적어둔 값을 쓴다.
    ///
    /// 배를 재는 이유는 배 모델을 바꿔도 따라가게 하기 위해서입니다.
    /// </summary>
    private float BowZ
    {
        get
        {
            if (_bowZ.HasValue)
            {
                return _bowZ.Value;
            }

            GameObject ship = GameObject.Find(ShipName);

            if (ship == null)
            {
                _bowZ = fallbackBowZ;
                return _bowZ.Value;
            }

            Renderer[] draws = ship.GetComponentsInChildren<Renderer>();

            if (draws.Length == 0)
            {
                _bowZ = fallbackBowZ;
                return _bowZ.Value;
            }

            Bounds box = draws[0].bounds;

            for (int i = 1; i < draws.Length; i++)
            {
                box.Encapsulate(draws[i].bounds);
            }

            _bowZ = box.max.z;
            return _bowZ.Value;
        }
    }

    private void Judge()
    {
        float gap = VoyageSea.Current.LateralGap(LaneX);

        // ⚠ **바위 폭의 절반을 그대로 더하면 안 됩니다.**
        //
        //    조타를 끝까지 꺾어도 세상은 4.8m 밖에 안 밀립니다.
        //    (`VoyageSea.lateralPerDegree` 0.08 × `HelmTask.maxHeading` 60)
        //    폭의 절반을 더하면 9.6m 짜리도 7.3m 가 필요해서 **절대 못 피합니다.**
        //
        //    이 게임의 회피는 물리가 아니라 **약속**입니다. 배 폭이 11m 라
        //    실제로 비키려면 애초에 조타 범위가 부족합니다. 그러니 크기는
        //    "조금 더 꺾어야 한다" 정도로만 반영합니다.
        float need = safeGap + RockWidth * widthPenalty;

        if (gap >= need)
        {
            Debug.Log($"[{name}] 비켜서 지나갔다. 간격 {gap:F1}m (필요 {need:F1}m, 바위 {RockWidth:F1}m)", this);
            Succeed();
            return;
        }

        Debug.Log($"[{name}] 뱃머리에 부딪혔다. 간격 {gap:F1}m (필요 {need:F1}m, " +
                  $"바위 {RockWidth:F1}m, 대가 ×{FailScale:F2})", this);
        Fail();
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

    /// <summary>
    /// 시간이 다 됐다. **보통은 여기 오기 전에 뱃머리에서 이미 판정됩니다.**
    ///
    /// 여기까지 왔다면 바다가 없거나(옛 씬) 바위가 안 떠 있는 경우입니다.
    /// 그때만 예전처럼 지나가는 순간으로 판정합니다.
    /// </summary>
    protected override void OnTimeout()
    {
        if (_judged)
        {
            return;
        }

        _judged = true;

        if (VoyageSea.Current == null)
        {
            base.OnTimeout();
            return;
        }

        Judge();
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
    /// 사건 알림 아래에 붙는 안내. **어느 쪽 바위이고 어느 키를 누르는지**만 말한다.
    ///
    /// 판정 숫자는 디버그(F1)로 보냅니다. 손이 바쁜 중에 읽을 수 있는 길이여야 합니다. (9장)
    /// </summary>
    public override string LiveHint()
    {
        string side = RockSide < 0f ? "좌현" : "우현";
        string key = RockSide > 0f ? "A" : "D";

        if (VoyageSea.Current == null)
        {
            return $"{side} 암초 — {key} 로 꺾어라";
        }

        float gap = VoyageSea.Current.LateralGap(LaneX);

        return gap >= safeGap
            ? $"{side} 암초 — 비켰다  ({gap:F1}m)"
            : $"{side} 암초 — {key} 로 꺾어라  ({gap:F1}/{safeGap:F1}m)";
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
