using UnityEngine;

/// <summary>
/// 사건을 바다 위에 놓아주는 곳. (SHIPCOOP.md 5장)
///
/// **배는 움직이지 않습니다. 바다가 흘러옵니다.**
/// 카메라를 갑판에 고정해 두고 장애물을 배 쪽으로 흘려보냅니다.
/// 화면에 보이는 것은 배가 나아가는 것과 같고, 구현은 훨씬 쌉니다.
///
/// 조타도 배를 옮기지 않습니다. **흘러오는 것들이 반대로 밀립니다.**
/// 배가 우현으로 꺾으면 바다가 좌현으로 밀립니다. 보이는 것은 같습니다.
///
/// 두 가지를 다르게 굴립니다. 섞으면 어긋납니다.
///
///   바다 · 배경   ShipVoyage.Speed 로     → **속도가 눈에 보인다**
///   사건 장애물   그 사건의 남은 시간으로 → **판정과 화면이 안 어긋난다**
///
/// 장애물까지 속도로 굴리면 도중에 돛이 풀렸을 때 바위는 아직 멀리 있는데
/// 타이머가 먼저 끝나 버립니다. 보이는 것과 판정이 다르면 억울합니다. (2장)
/// </summary>
public class VoyageSea : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private ShipVoyage voyage;
    [SerializeField] private HelmTask helm;

    [Tooltip("장애물이 흘러오는 기준점. 비워두면 이 오브젝트. 보통 배(갑판) 위치를 넣는다.")]
    [SerializeField] private Transform origin;

    [Header("거리")]
    [Tooltip("수평선까지의 거리 (m). 장애물이 여기서 나타나 배까지 온다.")]
    [SerializeField, Min(1f)] private float horizonDistance = 60f;

    [Tooltip("지나간 뒤 이만큼 더 가서 사라진다. 뒤로 흘러가는 것이 보여야 한다.")]
    [SerializeField, Min(0f)] private float passDistance = 12f;

    [Header("조타")]
    [Tooltip("조타 1도당 배가 옆으로 비키는 거리 (m).\n" +
             "최대 조타각 60도 × 이 값 = 최대로 비킬 수 있는 거리다.")]
    [SerializeField, Min(0f)] private float lateralPerDegree = 0.08f;

    [Header("목적지 섬")]
    [Tooltip("수평선에 띄울 목적지. 비워두면 회색 큐브를 만들어 쓴다. (프로토타입용)")]
    [SerializeField] private GameObject islandPrefab;

    [Tooltip("출항할 때 섬까지의 거리 (m). 진행도가 오르면 이만큼에서 가까워진다.")]
    [SerializeField, Min(10f)] private float islandFarDistance = 400f;

    [Tooltip("도착했을 때 섬까지의 거리 (m). 0 으로 두면 배를 뚫고 지나간다.")]
    [SerializeField, Min(0f)] private float islandNearDistance = 25f;

    [Header("항로선")]
    [Tooltip("항로 폭의 절반 (m). 이 안에 있으면 목적지를 제대로 향하고 있는 것이다.\n" +
             "0 으로 두면 선을 안 그린다.")]
    [SerializeField, Min(0f)] private float courseHalfWidth = 2f;

    [Tooltip("선의 굵기 (m)")]
    [SerializeField, Min(0.05f)] private float courseLineWidth = 0.35f;

    [Header("바다 흐름 (선택)")]
    [Tooltip("속도에 맞춰 뒤로 흐를 것들. 파도 · 물결 같은 배경.\n" +
             "여기 넣은 것만 ShipVoyage.Speed 로 움직인다.")]
    [SerializeField] private Transform[] scrollWithSpeed;

    [Tooltip("이만큼 흐르면 처음 자리로 되돌린다. 배경이 끝없이 이어지게.")]
    [SerializeField, Min(0.1f)] private float scrollLoopLength = 20f;

    /// <summary>씬에 하나만 둔다. 사건들이 이걸 찾아 쓴다.</summary>
    public static VoyageSea Current { get; private set; }

    /// <summary>장애물이 흘러오는 기준점</summary>
    public Vector3 Origin => origin != null ? origin.position : transform.position;

    /// <summary>수평선까지의 거리 (m)</summary>
    public float HorizonDistance => horizonDistance;

    /// <summary>
    /// 배가 옆으로 얼마나 비켜 있는지 (m). 좌현이 음수, 우현이 양수.
    ///
    /// 조타를 안 잡고 있어도 꺾여 있던 각도는 유지되므로, 이 값도 유지됩니다.
    /// 자리를 비웠다고 배가 저절로 제자리로 오지는 않습니다.
    /// </summary>
    public float ShipLateral => helm == null ? 0f : helm.Heading * lateralPerDegree;

    private Vector3[] _scrollStart;
    private Transform _island;
    private Transform[] _courseLines;

    private void Awake()
    {
        Current = this;

        if (voyage == null)
        {
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        }

        if (helm == null)
        {
            helm = FindAnyObjectByType<HelmTask>(FindObjectsInactive.Include);
        }

        if (helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 배가 좌우로 비키지 않습니다.", this);
        }

        _scrollStart = new Vector3[scrollWithSpeed == null ? 0 : scrollWithSpeed.Length];
        for (int i = 0; i < _scrollStart.Length; i++)
        {
            if (scrollWithSpeed[i] != null)
            {
                _scrollStart[i] = scrollWithSpeed[i].position;
            }
        }
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Start()
    {
        SpawnIsland();
        SpawnCourseLines();
    }

    /// <summary>
    /// 항로선 두 줄. 이 사이에 있으면 목적지를 제대로 향하고 있는 것이다.
    ///
    /// 섬과 역할이 다릅니다. 섬은 수백 미터 밖이라 배가 끝까지 꺾어도 화면에서
    /// 1° 남짓 움직입니다. **지금 얼마나 틀어졌는지는 섬으로 못 읽습니다.**
    /// 진행도 바의 기준선은 "늦고 있다" 는 결과를 알려주고,
    /// 이 선은 **"지금 틀어져 있다" 는 원인**을 알려줍니다.
    /// </summary>
    private void SpawnCourseLines()
    {
        if (courseHalfWidth <= 0f)
        {
            return;
        }

        float length = horizonDistance + passDistance;

        _courseLines = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = i == 0 ? "항로선 좌현" : "항로선 우현";

            // 물 위에 살짝 띄운다. 바다와 z-파이팅이 나지 않게.
            line.transform.localScale = new Vector3(courseLineWidth, 0.05f, length);
            line.transform.SetParent(transform, true);

            // 부딪히면 안 된다. 보여주기만 하는 것이다.
            Collider collider = line.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            _courseLines[i] = line.transform;
        }
    }

    /// <summary>
    /// 목적지 섬을 띄운다. 에셋이 오면 islandPrefab 을 채우면 된다.
    ///
    /// **이 섬 하나가 두 가지를 대신합니다.** (5장)
    ///   어디로 가야 하는가  — 규칙을 가르칠 필요가 없다. 저기로 가면 된다.
    ///   얼마나 왔는가       — 섬이 커지는 것이 진행도다.
    /// </summary>
    private void SpawnIsland()
    {
        if (islandPrefab != null)
        {
            _island = Instantiate(islandPrefab).transform;
        }
        else
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = new Vector3(60f, 25f, 60f);
            _island = cube.transform;
        }

        _island.name = "목적지 섬";
        _island.SetParent(transform, true);
    }

    /// <summary>
    /// 배경을 속도에 맞춰 흘리고, 목적지 섬을 진행도만큼 당기고,
    /// 조타각에서 <see cref="ShipVoyage.CourseFactor"/> 를 뽑는다.
    ///
    /// 장애물은 여기서 안 옮긴다. 사건이 남은 시간을 보고 직접 옮긴다.
    /// </summary>
    private void Update()
    {
        if (voyage == null)
        {
            return;
        }

        // 목적지 쪽으로 향한 만큼만 나아간다. cos 라서 따로 정할 상수가 없다.
        // 뱃머리가 섬을 보고 있으면 1, 옆을 보고 있으면 줄어든다.
        float heading = helm != null ? helm.Heading : 0f;
        voyage.CourseFactor = Mathf.Max(0f, Mathf.Cos(heading * Mathf.Deg2Rad));

        if (_island != null)
        {
            float z = Mathf.Lerp(islandFarDistance, islandNearDistance, voyage.Progress01);
            _island.position = Origin + new Vector3(-ShipLateral, 0f, z);
        }

        // 항로선도 바다의 일부라 배가 꺾이면 같이 밀린다. 그래서 틀어진 것이 보인다.
        if (_courseLines != null)
        {
            float centreZ = (horizonDistance - passDistance) * 0.5f;

            for (int i = 0; i < _courseLines.Length; i++)
            {
                float side = i == 0 ? -courseHalfWidth : courseHalfWidth;
                _courseLines[i].position = Origin + new Vector3(side - ShipLateral, 0.05f, centreZ);
            }
        }

        if (_scrollStart == null)
        {
            return;
        }

        float travelled = voyage.Distance % scrollLoopLength;

        for (int i = 0; i < _scrollStart.Length; i++)
        {
            Transform t = scrollWithSpeed[i];
            if (t != null)
            {
                t.position = _scrollStart[i] + Vector3.back * travelled;
            }
        }
    }

    /// <summary>
    /// 장애물을 제자리에 놓는다. 사건이 매 프레임 부른다.
    ///
    /// approach01 이 1 이면 수평선, 0 이면 배에 닿는다. 0 아래로 내려가면 지나간 것이다.
    /// laneX 는 배가 똑바로 갈 때의 좌우 자리다. 좌현이 음수, 우현이 양수.
    /// </summary>
    public void Place(Transform obstacle, float laneX, float approach01)
    {
        if (obstacle == null)
        {
            return;
        }

        // 0 아래로도 내려가게 둔다. 지나간 바위가 뒤로 흘러가는 것이 보여야
        // "피했다" 가 눈에 남는다.
        float z = Mathf.Lerp(-passDistance, horizonDistance, approach01);

        // 배가 우현으로 꺾으면 세상이 좌현으로 밀린다.
        obstacle.position = Origin + new Vector3(laneX - ShipLateral, 0f, z);
    }

    /// <summary>
    /// 그 자리의 장애물이 배에서 좌우로 얼마나 떨어져 있는지 (m).
    ///
    /// 판정은 이 값 하나로 합니다. 각도가 아니라 거리입니다. (5장)
    /// </summary>
    public float LateralGap(float laneX)
    {
        return Mathf.Abs(laneX - ShipLateral);
    }
}
