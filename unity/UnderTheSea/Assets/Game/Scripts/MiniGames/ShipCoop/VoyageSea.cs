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
    // ⚠ **뱃머리(z 34.3)보다 한참 멀어야 반응할 시간이 생깁니다.**
    //
    //    60m 였을 때는 바위가 **2.3초** 만에 뱃머리에 닿았습니다. 실제로 다가올
    //    거리가 60 − 43.8 = 16m 밖에 없었기 때문입니다. 예고를 보고 조타로
    //    뛰어가는 것은 고사하고, 그 자리에 서서 즉시 꺾어도 못 피했습니다.
    //    100m 면 5.7초가 되어 반응할 시간이 3.75초 생깁니다.
    [Tooltip("수평선까지의 거리 (m). 장애물이 여기서 나타나 배까지 온다.\n" +
             "뱃머리보다 한참 멀어야 반응할 시간이 생긴다.")]
    [SerializeField, Min(1f)] private float horizonDistance = 100f;

    // ⚠ **배 뒤끝(z −19.7)보다 더 뒤까지 가야 합니다.**
    //
    //    12m 였을 때는 바위가 z −12 에서 멈췄습니다. 큰 바위는 뒷면이 z −4 라
    //    **배 옆구리에 걸친 채로 사건이 끝났습니다.** 암초 판정이 배 전체를
    //    훑는 방식(`Reef.OnShow`)이라, 다 지나가질 못하니 판정이 안 끝납니다.
    //    34m 면 가장 큰 바위(19m)도 뒷면이 z −24.5 로 배를 완전히 벗어납니다.
    [Tooltip("지나간 뒤 이만큼 더 가서 사라진다. 뒤로 흘러가는 것이 보여야 한다.\n" +
             "배 뒤끝보다 더 뒤까지 가야 암초가 배를 다 지나간다.")]
    [SerializeField, Min(0f)] private float passDistance = 34f;

    [Header("조타")]
    [Tooltip("조타 1도당 배가 옆으로 비키는 거리 (m).\n" +
             "최대 조타각 60도 × 이 값 = 최대로 비킬 수 있는 거리다.")]
    [SerializeField, Min(0f)] private float lateralPerDegree = 0.08f;

    // ------------------------------------------------------------
    // ⚠ **지연이 0 이면 배가 평행으로 미끄러집니다.**
    //
    //    조타각을 그대로 거리로 바꾸면 휠을 돌리는 즉시 세상이 초당 7m 로
    //    미끄러집니다. 그런데 뱃머리가 도는 것(`ShipCoopShipTurn`)은 0.8초에
    //    걸쳐 겨우 10도입니다. 회전이 묻혀서 **배가 게처럼 옆으로 평행이동**
    //    하는 것으로만 보입니다.
    //
    //    진짜 배는 **먼저 돌고 그 다음에 그쪽으로 밀려납니다.** 그래서 옆으로
    //    가는 것을 뱃머리보다 늦게 따라오게 합니다.
    //
    //      0.0초  뱃머리 0도    옆으로 0.0m
    //      0.5초  뱃머리 3도    옆으로 0.6m    ← 거의 안 밀렸다. 돌기만 한다
    //      2.0초  뱃머리 10.6도 옆으로 7.4m    ← 이제 밀려난다
    //      4.5초  뱃머리 0도    옆으로 12.0m   ← 일자로 펴진 채 옆에 서 있다
    // ------------------------------------------------------------

    [Tooltip("옆으로 밀려나는 것이 뱃머리보다 이만큼 늦게 따라온다 (초).\n\n" +
             "0 이면 휠을 돌리는 즉시 배가 평행으로 미끄러집니다.\n" +
             "뱃머리가 도는 시간(ShipCoopShipTurn.followSeconds)보다 커야 합니다.")]
    [SerializeField, Range(0f, 3f)] private float lateralLagSeconds = 1.2f;

    [Header("목적지 섬")]
    [Tooltip("수평선에 띄울 목적지. 비워두면 회색 큐브를 만들어 쓴다. (프로토타입용)")]
    [SerializeField] private GameObject islandPrefab;

    [Tooltip("큐브로 만들 때 입힐 색. 비워두면 회색이라 바다에서 안 보인다.")]
    [SerializeField] private Material islandMaterial;

    [Tooltip("출항할 때 섬까지의 거리 (m). 진행도가 오르면 이만큼에서 가까워진다.")]
    [SerializeField, Min(10f)] private float islandFarDistance = 400f;

    [Tooltip("도착했을 때 섬까지의 거리 (m). 0 으로 두면 배를 뚫고 지나간다.")]
    [SerializeField, Min(0f)] private float islandNearDistance = 25f;

    // ⚠ **섬이 중반에 이미 코앞으로 보였습니다.**
    //
    //    거리는 진행도와 정확히 맞았다 (재서 확인 — 진행도 46% 일 때 섬 227m = Lerp(400, 25, 0.46)).
    //    문제는 **섬이 크다**는 것이다. 폭 81m · 화산 높이 50m 라, 60° 화각에서 화면을 이만큼 채운다.
    //
    //      400m → 화면 폭의 18%      227m → 31%      100m → 70%      25m → 화면을 넘는다
    //
    //    그래서 절반쯤 왔을 때 이미 "다 온 것" 처럼 보인다. 거리를 선형으로 줄이면 중반이 가장 빨리
    //    커지는 구간이 된다. 진행도를 제곱해서 **앞부분에서는 천천히, 끝에서 확 다가오게** 한다.
    //
    //      진행도 46%  선형 227m  →  제곱 321m (화면의 11%)
    //      진행도 80%  선형 100m  →  제곱 160m (화면의 44%)
    //      진행도 100% 선형  25m  →  제곱  25m (도착은 같다)
    [Tooltip("섬이 다가오는 곡선. 1 이면 진행도에 정비례. 클수록 앞에서는 멀리 있다가 끝에서 확 다가온다.")]
    [SerializeField, Range(1f, 3f)] private float islandApproachCurve = 2f;

    [Tooltip("섬 거리 · 진행도를 2초마다 로그로 남긴다. 값을 맞출 때만 켠다.")]
    [SerializeField] private bool logIsland = true;

    [Header("항로선")]
    [Tooltip("항로 폭의 절반 (m). 이 안에 있으면 목적지를 제대로 향하고 있는 것이다.\n" +
             "0 으로 두면 선을 안 그린다.")]
    [SerializeField, Min(0f)] private float courseHalfWidth = 2f;

    [Tooltip("선의 굵기 (m)")]
    [SerializeField, Min(0.05f)] private float courseLineWidth = 0.35f;

    [Tooltip("항로선 색. 비워두면 회색이라 바다에서 안 보인다.")]
    [SerializeField] private Material courseLineMaterial;

    [Header("바다 흐름 (선택)")]
    [Tooltip("속도에 맞춰 뒤로 흐를 것들. 파도 · 물결 같은 배경.\n" +
             "여기 넣은 것만 ShipVoyage.Speed 로 움직인다.")]
    [SerializeField] private Transform[] scrollWithSpeed;

    [Tooltip("이만큼 흐르면 처음 자리로 되돌린다. 배경이 끝없이 이어지게.")]
    [SerializeField, Min(0.1f)] private float scrollLoopLength = 20f;

    /// <summary>씬에 하나만 둔다. 사건들이 이걸 찾아 쓴다.</summary>
    public static VoyageSea Current { get; private set; }

    /// <summary>프로토타입 큐브에 색을 입힌다. 머티리얼이 없으면 회색 그대로 둔다.</summary>
    public static void Paint(GameObject go, Material material)
    {
        if (go == null || material == null)
        {
            return;
        }

        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    /// <summary>장애물이 흘러오는 기준점</summary>
    public Vector3 Origin => origin != null ? origin.position : transform.position;

    /// <summary>수평선까지의 거리 (m)</summary>
    public float HorizonDistance => horizonDistance;

    /// <summary>배를 지나친 뒤 더 가는 거리 (m). 바위가 다가오는 속도를 내는 데 쓴다.</summary>
    public float PassDistance => passDistance;

    /// <summary>
    /// 배가 옆으로 얼마나 비켜 있는지 (m). 좌현이 음수, 우현이 양수.
    ///
    /// 조타를 안 잡고 있어도 꺾여 있던 각도는 유지되므로, 이 값도 유지됩니다.
    /// 자리를 비웠다고 배가 저절로 제자리로 오지는 않습니다.
    ///
    /// ⚠ **조타각을 그대로 쓰지 않고 늦게 따라옵니다.** (위 주석)
    ///    먼저 뱃머리가 돌고, 그 다음에 그쪽으로 밀려나야 배처럼 보입니다.
    /// </summary>
    public float ShipLateral => _lateral;

    /// <summary>
    /// 배가 지금 옆으로 **얼마나 빠르게** 미끄러지고 있는지 (m/초). 우현이 양수.
    ///
    /// ⚠ **뱃머리를 트는 것이 이 값을 봅니다.** (`ShipCoopShipTurn`)
    ///    옆으로 가는 동안에는 늘 그쪽으로 뱃머리가 나가 있고, 다 미끄러져
    ///    멈추면 저절로 일자가 됩니다.
    ///
    ///    SmoothDamp 가 들고 있는 속도라 이미 매끄럽습니다. 위치를 다시
    ///    미분해서 쓰면 프레임 시간의 흔들림이 그대로 뱃머리로 갑니다.
    /// </summary>
    public float LateralSpeed => _lateralSpeed;

    /// <summary>조타각이 가리키는 자리. <see cref="ShipLateral"/> 이 여기로 따라간다.</summary>
    private float WantLateral => helm == null ? 0f : helm.Heading * lateralPerDegree;

    private float _lateral;
    private float _lateralSpeed;

    private Vector3[] _scrollStart;
    private Transform _island;

    /// <summary>섬 자리 계측을 다음에 찍을 시각. (진행도와 섬 거리를 숫자로 맞춰 보려고)</summary>
    private float _nextIslandLog;
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

            Paint(line, courseLineMaterial);
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
            Paint(cube, islandMaterial);
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

        // ⚠ 옆으로 밀려나는 것은 **뱃머리보다 늦게** 따라온다. (위 주석)
        //    이것을 빼면 배가 게처럼 평행으로 미끄러진다.
        _lateral = lateralLagSeconds > 0f
            ? Mathf.SmoothDamp(_lateral, WantLateral, ref _lateralSpeed, lateralLagSeconds)
            : WantLateral;

        // 목적지 쪽으로 향한 만큼만 나아간다. cos 라서 따로 정할 상수가 없다.
        // 뱃머리가 섬을 보고 있으면 1, 옆을 보고 있으면 줄어든다.
        float heading = helm != null ? helm.Heading : 0f;
        voyage.CourseFactor = Mathf.Max(0f, Mathf.Cos(heading * Mathf.Deg2Rad));

        if (_island != null)
        {
            // 진행도를 그대로 쓰지 않고 곡선을 태운다. (위 주석 — 중반에 이미 코앞으로 보였다)
            float closing = Mathf.Pow(Mathf.Clamp01(voyage.Progress01), islandApproachCurve);
            float z = Mathf.Lerp(islandFarDistance, islandNearDistance, closing);
            _island.position = Origin + new Vector3(-ShipLateral, 0f, z);

            if (logIsland && Time.time >= _nextIslandLog)
            {
                _nextIslandLog = Time.time + 2f;

                Debug.Log(
                    $"[바다] 섬 {z:F0}m 앞 (곡선 {islandApproachCurve:F1}, 정비례였다면 {Mathf.Lerp(islandFarDistance, islandNearDistance, voyage.Progress01):F0}m)   " +
                    $"진행도 {voyage.Progress01:P1} = {voyage.Distance:F0}m / {voyage.TotalDistance:F0}m   " +
                    $"지금 속도 {voyage.Speed:F2}m/s (돛 {voyage.SailPower01:P0}, 항로 {voyage.CourseFactor:F2})", this);
            }
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

    // ⛔ **`LateralGap(laneX)` 은 지웠습니다.**
    //
    //    바다의 기준점(`Origin`)에서 재는 값이었는데, 배 중심은 모델이 대칭이
    //    아니라 거기서 조금 밀려 있습니다. 암초 판정이 그걸 쓰면 한쪽 바위만
    //    가까워집니다. 지금은 `Reef.GapFromShip` 이 **배에서** 잽니다.
}
