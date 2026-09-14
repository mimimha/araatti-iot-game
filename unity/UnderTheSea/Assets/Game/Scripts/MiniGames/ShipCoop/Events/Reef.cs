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

    // ------------------------------------------------------------
    // ⛔ **판정 범위는 보이는 범위여야 합니다.**
    //
    //    전에는 "이만큼 떨어져 있으면 피한 것"(`safeGap`)이라는 **고정값**에
    //    바위 폭을 8%만 얹었습니다. 그래서 크기가 사실상 무시됐습니다.
    //
    //      바위폭    옛 필요간격   진짜 겹침   자리    옛 판정   진짜
    //       9.6m       17.3m        10.4m     12m    부딪힘   안 닿음  ← 거짓
    //      11.8m       17.4m        11.5m     12m    부딪힘   안 닿음  ← 거짓
    //      16.2m       17.8m        13.7m     12m    부딪힘     닿음
    //
    //    **12m 옆을 지나가는데 부딪혔다고 뜨고 바위가 사라졌습니다.**
    //    선체 반폭이 5.6m 인데 판정은 16.5m 였으니 당연합니다.
    //
    //    지금은 **반폭을 더해 진짜 겹치는지**만 봅니다. 바위마다 다릅니다.
    //
    //      필요 간격 = 선체 반폭 + 바위 반폭
    //
    //    자리도 바위 크기를 따릅니다. 그냥 두면 **어느 바위든 같은 깊이로**
    //    스치도록 놓습니다. 그래야 큰 바위는 멀리(잘 보이고), 작은 바위는
    //    가까이 오면서도 난이도가 크기에 비례합니다.
    //
    //      자리 = 선체 반폭 + 바위 반폭 − 스치는 깊이
    // ------------------------------------------------------------

    [Tooltip("가만히 있을 때 바위가 선체를 이만큼 파고들게 놓는다 (바위 폭 대비 비율).\n\n" +
             "0.3 이면 바위 폭의 30% 만큼 겹친다. 그만큼 비켜야 피한다.\n" +
             "**피하는 데 필요한 조타각이 이 값 하나로 정해집니다.**")]
    [SerializeField, Range(0.05f, 0.5f)] private float grazeShare = 0.3f;

    [Tooltip("배를 못 찾을 때 쓸 선체 반폭 (m). 배치 도구가 재어서 넣는다.\n" +
             "돛대·삭구는 빼고 선체와 갑판만 잰다.")]
    [SerializeField, Min(0.5f)] private float fallbackShipHalfWidth = 5.6f;

    [Header("배의 앞뒤 끝")]
    [Tooltip("배를 못 찾을 때 쓸 뱃머리 z (m). 배치 도구가 재어서 넣는다.")]
    [SerializeField] private float fallbackBowZ = 22.8f;

    [Tooltip("배를 못 찾을 때 쓸 배 뒤끝 z (m). 배치 도구가 재어서 넣는다.")]
    [SerializeField] private float fallbackSternZ = -19.7f;

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

    /// <summary>
    /// 이 바위가 **배 한가운데(가장 넓은 곳)** 에 닿는 간격 (m).
    ///
    /// 실제 판정은 바위가 걸쳐 있는 구간의 폭으로 합니다. (<see cref="HullHalfBetween"/>)
    /// 이 값은 바위를 놓는 자리와 안내 문구에만 씁니다.
    /// </summary>
    public float TouchGap => HullWidest + RockWidth * 0.5f;

    /// <summary>
    /// 바위가 놓인 좌우 자리 (m). 배 중심이 x 0 이 아니라 그만큼 밀어준다.
    ///
    /// 가만히 있으면 배 한가운데에서 바위 폭의 <see cref="grazeShare"/> 만큼
    /// 선체를 파고듭니다. 그래서 **바위가 클수록 멀리 놓이고**, 비켜야 하는
    /// 거리도 그만큼 큽니다.
    /// </summary>
    public float LaneX
    {
        get
        {
            float fromCentre = TouchGap - RockWidth * grazeShare;
            float originX = VoyageSea.Current != null ? VoyageSea.Current.Origin.x : 0f;

            return RockSide * fromCentre + (HullCentreX - originX);
        }
    }

    private HelmTask _helm;
    private GameObject _rock;

    /// <summary>띄운 바위의 폭 (m). 클수록 크게 꺾어야 하고 크게 아프다.</summary>
    public float RockWidth { get; private set; } = 2f;

    /// <summary>띄운 바위의 높이 (m). 물에 잠그는 데 쓴다.</summary>
    public float RockHeight { get; private set; } = 1.6f;

    /// <summary>이번 바위를 이미 판정했는가.</summary>
    private bool _judged;

    /// <summary>바위가 배 옆에 들어온 적이 있는가. 다 지나가면 "피했다" 가 된다.</summary>
    private bool _wasAlongside;

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
        _wasAlongside = false;

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

        // ------------------------------------------------------------
        // ⚠ **뱃머리 한 점이 아니라 배 전체로 봅니다.**
        //
        //    전에는 바위 앞면이 뱃머리(z 22.8)를 넘는 **순간 한 번**만 봤습니다.
        //    그래서 뱃머리만 피하고 바로 키를 되돌리면, 바위가 배 옆구리를
        //    그대로 긁고 지나가도 "피했다" 가 떴습니다.
        //
        //    지금은 바위가 **선체를 따라 지나가는 내내** 봅니다.
        //    어디서든 닿으면 그 순간 부딪힌 것이고, 끝까지 다 지나가야 피한 것입니다.
        //
        // ⚠ **그 자리의 진짜 폭**으로 봅니다. 뱃머리는 반폭 1.1m, 한가운데는
        //    5.6m 입니다. 직사각형으로 보면 뱃머리 한참 옆을 지나는 바위도
        //    "닿았다" 가 됩니다. (위 주석)
        // ------------------------------------------------------------

        float rockZ = _rock.transform.position.z;
        float half = RockWidth * 0.5f;

        float nose = rockZ - half;   // 바위 앞면
        float tail = rockZ + half;   // 바위 뒷면

        // 바위가 걸쳐 있는 구간에서 선체가 가장 넓은 곳의 반폭. 0 이면 안 겹친다.
        float shipHalf = HullHalfBetween(nose, tail);

        if (shipHalf > 0f)
        {
            _wasAlongside = true;

            // 배 중심에서 바위 중심까지. 배가 x 0 에 있지 않으므로 실제 자리로 잰다.
            float gap = Mathf.Abs(_rock.transform.position.x - HullCentreX);
            float need = shipHalf + half;

            if (gap < need)
            {
                _judged = true;
                Hit(gap, need, rockZ);
            }

            return;
        }

        // 선체를 다 지나갔다. 한 번도 안 닿았으면 피한 것이다.
        //
        // ⚠ 여기서부터는 안 봅니다. 배 뒤로 다 빠진 바위가 화면 밖에서
        //    조타에 따라 옆으로 움직여도 손실이 나면 안 됩니다.
        if (_wasAlongside && tail < SternZ)
        {
            _judged = true;
            Dodged();
        }
    }

    /// <summary>선체의 앞 끝 z. 삭구는 뺀 진짜 뱃머리다.</summary>
    private float BowZ
    {
        get
        {
            MeasureShip();
            return _hullMaxZ;
        }
    }

    /// <summary>선체의 뒤 끝 z. 여기를 다 지나야 "지나갔다" 가 된다.</summary>
    private float SternZ
    {
        get
        {
            MeasureShip();
            return _hullMinZ;
        }
    }

    // ------------------------------------------------------------
    // ⛔ **배를 직사각형으로 보면 안 됩니다.**
    //
    //    전에는 뱃머리부터 뒤끝까지 **반폭 5.6m 짜리 직사각형**으로 봤습니다.
    //    진짜 선체는 이렇게 생겼습니다.
    //
    //       z   실제 반폭
    //       25   1.11m  ##          ← 뱃머리. 뾰족하다
    //       20   4.15m  ########
    //       15   5.41m  ###########
    //        0   5.61m  ###########
    //      −15   5.46m  ###########
    //      −20   4.49m  #########
    //
    //    게다가 앞뒤 끝을 배 **전체 경계**(z 34.3 ~ −19.7)로 잡았습니다.
    //    34.3 은 선체가 아니라 앞으로 튀어나온 **삭구와 이물장식** 끝입니다.
    //    선체는 z 27 에서 끝납니다.
    //
    //    그래서 바위가 뱃머리 한참 옆을 지나는데도 "닿았다" 가 났습니다.
    //    플레이어 눈에는 분명히 비켰는데 부딪힌 것으로 뜹니다.
    //
    //    지금은 **메시 점을 훑어 z 구간마다 진짜 반폭**을 재둡니다.
    //    바위가 걸쳐 있는 구간의 폭만 씁니다. 보이는 그대로입니다.
    //
    // ⚠ 배 중심 x 는 0 이 아니라 **1.03** 입니다. 모델이 좌우 대칭이 아닙니다.
    //    지금은 바다 기준점도 1.05 라 차이가 0.02m 뿐이지만, 둘이 따로 움직이면
    //    그만큼 한쪽 바위가 가까워집니다. 기준점이 아니라 **배에서** 재도록 둡니다.
    // ------------------------------------------------------------

    /// <summary>선체 폭을 재는 구간 간격 (m). 촘촘할수록 정확하지만 점이 많아진다.</summary>
    private const float SliceSize = 2f;

    /// <summary>구간별 반폭. 배는 안 바뀌므로 한 번만 잰다.</summary>
    private float[] _hullHalves;

    private float _hullMinZ;
    private float _hullMaxZ;
    private float _hullCentreX;
    private float _hullWidest;

    /// <summary>선체에서 가장 넓은 곳의 반폭 (m). 바위를 놓는 기준.</summary>
    private float HullWidest
    {
        get
        {
            MeasureShip();
            return _hullWidest;
        }
    }

    /// <summary>선체 좌우 한가운데의 x. 모델이 대칭이 아니라 0 이 아니다.</summary>
    private float HullCentreX
    {
        get
        {
            MeasureShip();
            return _hullCentreX;
        }
    }

    /// <summary>
    /// z 가 <paramref name="from"/> ~ <paramref name="to"/> 인 구간에서
    /// 선체가 가장 넓은 곳의 반폭 (m). 그 구간에 선체가 없으면 0.
    /// </summary>
    private float HullHalfBetween(float from, float to)
    {
        MeasureShip();

        if (_hullHalves == null || to < _hullMinZ || from > _hullMaxZ)
        {
            return 0f;
        }

        int first = Mathf.Clamp(Mathf.FloorToInt((from - _hullMinZ) / SliceSize), 0, _hullHalves.Length - 1);
        int last = Mathf.Clamp(Mathf.CeilToInt((to - _hullMinZ) / SliceSize), 0, _hullHalves.Length - 1);

        float widest = 0f;

        for (int i = first; i <= last; i++)
        {
            widest = Mathf.Max(widest, _hullHalves[i]);
        }

        return widest;
    }

    /// <summary>
    /// 선체 모양을 한 번 재어 둔다. 메시 점을 z 구간으로 나눠 담는다.
    ///
    /// 배를 재는 이유는 배 모델을 바꿔도 따라가게 하기 위해서입니다.
    /// </summary>
    private void MeasureShip()
    {
        if (_hullHalves != null)
        {
            return;
        }

        GameObject ship = GameObject.Find(ShipName);
        MeshFilter[] parts = ship != null ? ship.GetComponentsInChildren<MeshFilter>() : null;

        if (parts == null || parts.Length == 0)
        {
            UseFallbackShape();
            return;
        }

        // ⚠ **선체와 갑판만.** 돛대·삭구는 물 위 한참 높이라 바위에 안 닿습니다.
        var hulls = new System.Collections.Generic.List<MeshFilter>();
        Bounds box = new Bounds();
        bool any = false;

        for (int i = 0; i < parts.Length; i++)
        {
            string n = parts[i].name;

            if ((!n.StartsWith("Hull") && !n.StartsWith("Deck")) || parts[i].sharedMesh == null)
            {
                continue;
            }

            hulls.Add(parts[i]);

            Renderer draw = parts[i].GetComponent<Renderer>();

            if (draw == null)
            {
                continue;
            }

            if (!any) { box = draw.bounds; any = true; }
            else { box.Encapsulate(draw.bounds); }
        }

        if (!any)
        {
            UseFallbackShape();
            return;
        }

        _hullMinZ = box.min.z;
        _hullMaxZ = box.max.z;
        _hullCentreX = box.center.x;

        int slices = Mathf.Max(Mathf.CeilToInt((_hullMaxZ - _hullMinZ) / SliceSize) + 1, 1);
        _hullHalves = new float[slices];

        for (int p = 0; p < hulls.Count; p++)
        {
            Transform at = hulls[p].transform;

            // ⚠ 빌드에서 정점을 읽으려면 그 FBX 의 Read/Write 가 켜져 있어야 한다. 꺼져 있으면 에디터에서는
            //    멀쩡하다가 빌드에서만 **빈 배열**이 오고 반폭이 전부 0 이 된다 — 그래서 빌드에서 암초가 쉬웠다.
            //    배치 도구(ShipCoopDeckLayout.MakeShipMeshesReadable)가 배 FBX 의 Read/Write 를 켠다.
            Vector3[] points = hulls[p].sharedMesh.vertices;

            for (int v = 0; v < points.Length; v++)
            {
                Vector3 world = at.TransformPoint(points[v]);
                int slot = Mathf.Clamp(Mathf.RoundToInt((world.z - _hullMinZ) / SliceSize), 0, slices - 1);
                float half = Mathf.Abs(world.x - _hullCentreX);

                if (_hullHalves[slot] < half)
                {
                    _hullHalves[slot] = half;
                }
            }
        }

        for (int i = 0; i < slices; i++)
        {
            _hullWidest = Mathf.Max(_hullWidest, _hullHalves[i]);
        }

        // 정점을 하나도 못 읽었으면(Read/Write 꺼진 빌드) 반폭이 0 이다. 그대로 두면 바위가 배를 뚫고 지나가도
        // "안 닿음" 이 되니, 적어둔 직사각형으로라도 판정한다. 조용히 쉬워지는 것보다 소리 내고 폴백하는 게 낫다.
        if (_hullWidest < 0.5f)
        {
            Debug.LogError($"[{name}] 선체 메시의 정점을 읽지 못했습니다 (FBX Read/Write 꺼짐?). " +
                           $"반폭 {fallbackShipHalfWidth:F1}m 직사각형으로 판정합니다. 배치 도구를 한 번 돌리면 켜집니다.");
            UseFallbackShape();
        }
    }

    /// <summary>배를 못 찾았을 때. 적어둔 값으로 직사각형을 쓴다.</summary>
    private void UseFallbackShape()
    {
        _hullMinZ = fallbackSternZ;
        _hullMaxZ = fallbackBowZ;
        _hullCentreX = 0f;
        _hullWidest = fallbackShipHalfWidth;
        _hullHalves = new[] { fallbackShipHalfWidth };
    }

    /// <summary>
    /// 지금 바위 중심이 **배 중심에서** 좌우로 얼마나 떨어져 있는지 (m).
    ///
    /// ⚠ `VoyageSea.LateralGap` 은 바다의 기준점(`Origin`)에서 잽니다. 배 중심은
    ///    모델이 대칭이 아니라 x 1.03 이고, 기준점은 1.05 입니다. 지금은 차이가
    ///    0.02m 뿐이지만 둘이 따로 움직이면 한쪽 바위만 가까워집니다.
    ///    그래서 **배에서** 잽니다.
    /// </summary>
    private float GapFromShip()
    {
        if (_rock != null)
        {
            return Mathf.Abs(_rock.transform.position.x - HullCentreX);
        }

        // 바위가 아직 없으면 놓일 자리로 낸다.
        float originX = VoyageSea.Current != null ? VoyageSea.Current.Origin.x : 0f;
        float lateral = VoyageSea.Current != null ? VoyageSea.Current.ShipLateral : 0f;

        return Mathf.Abs(originX + LaneX - lateral - HullCentreX);
    }

    private void Judge()
    {
        float gap = GapFromShip();

        // ⚠ 여기는 **시간이 다 됐는데 판정이 안 난** 경우만 옵니다. 그때는 바위가
        //    어느 구간에 걸쳐 있는지 알 수 없으니 가장 넓은 곳으로 봅니다.
        //    평소 판정은 `OnShow` 에서 그 자리의 진짜 폭으로 합니다.
        float need = TouchGap;

        if (gap >= need)
        {
            Dodged();
            return;
        }

        Hit(gap, need, _rock != null ? _rock.transform.position.z : 0f);
    }

    /// <summary>
    /// 배 어딘가에 닿았다. **바위는 그 자리에서 사라집니다.**
    ///
    /// <see cref="OnHide"/> 가 지웁니다. 배를 뚫고 지나가는 바위를 보여줄 수는 없습니다.
    /// </summary>
    private void Hit(float gap, float need, float rockZ)
    {
        string where = rockZ > BowZ * 0.5f ? "뱃머리"
            : rockZ > SternZ * 0.5f ? "배 가운데"
            : "배 뒤쪽";

        Debug.Log($"[{name}] {where}에 부딪혔다. 간격 {gap:F1}m (필요 {need:F1}m, " +
                  $"바위 {RockWidth:F1}m, 대가 ×{FailScale:F2})", this);

        Fail();
    }

    /// <summary>
    /// 배 전체를 스치지 않고 지나갔다.
    ///
    /// ⚠ **바위는 지우지 않고 놓아줍니다.** 배 뒤끝을 막 지난 참이라
    ///    아직 화면에 한참 남아 있습니다. 여기서 지우면 눈앞에서 뿅 사라집니다.
    /// </summary>
    private void Dodged()
    {
        Debug.Log($"[{name}] 배 전체를 스치지 않고 지나갔다. (바위 {RockWidth:F1}m)", this);

        LetItDriftBy();
        Succeed();
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

    /// <summary>
    /// 피한 바위를 **사건에서 떼어내** 스스로 흘러가게 놓아준다.
    ///
    /// 떼어낸 뒤에는 <see cref="OnHide"/> 가 지울 것이 없으므로 바위가 살아남습니다.
    /// 화면 뒤로 다 지나가면 <see cref="ReefDrift"/> 가 스스로 지웁니다.
    /// </summary>
    private void LetItDriftBy()
    {
        if (_rock == null || VoyageSea.Current == null)
        {
            return;
        }

        // 여기까지 오던 속도 그대로 계속 간다. 갑자기 느려지면 눈에 띈다.
        float span = VoyageSea.Current.HorizonDistance + VoyageSea.Current.PassDistance;
        float speed = Duration > 0f ? span / Duration : 9f;

        ReefDrift drift = _rock.AddComponent<ReefDrift>();
        drift.Begin(LaneX, speed, -RockHeight * sink);

        // ⚠ 손을 뗀다. 이걸 안 하면 OnHide 가 방금 놓아준 바위를 지운다.
        _rock = null;
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

        float gap = GapFromShip();

        return gap >= TouchGap
            ? $"{side} 암초 — 비켰다  ({gap:F1}m)"
            : $"{side} 암초 — {key} 로 꺾어라  ({gap:F1}/{TouchGap:F1}m)";
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

        float gap = GapFromShip();
        string verdict = gap >= TouchGap ? "지금이면 피한다" : "지금이면 부딪힌다";

        return $"{side} 암초 — 간격 {gap:F1}m / 필요 {TouchGap:F1}m  ({verdict}, 도달까지 {Approach01:P0})";
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
