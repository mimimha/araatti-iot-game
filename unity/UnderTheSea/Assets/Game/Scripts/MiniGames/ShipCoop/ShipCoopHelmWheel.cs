using UnityEngine;

/// <summary>
/// 🛞 배의 조타륜을 **조타한 만큼 돌린다.** (SHIPCOOP.md 4장)
///
/// 조타는 `HelmTask.Heading` 하나로 정해집니다. 그 값을 그대로 각도로 씁니다.
/// 그래서 **저절로 같은 방향, 같은 속도**가 되고,
/// 끝(`maxHeading`)에서 값이 멈추면 **바퀴도 같이 멈춥니다.**
///
/// 따로 속도를 계산하지 않는 것이 핵심입니다. 계산하면 두 값이 언젠가 어긋나고,
/// 화면의 바퀴와 실제 뱃머리가 다른 말을 하게 됩니다.
///
/// ⚠ **바퀴의 한가운데를 축으로 돕니다.**
///    모델의 원점(pivot)이 바퀴 한가운데에 있다는 보장이 없습니다.
///    원점이 비뚤면 바퀴가 도는 대신 **휘둘러집니다.**
///    그래서 켤 때 실제 크기를 재서 한가운데를 찾아 두고, 그 점을 축으로 돌립니다.
/// </summary>
public class ShipCoopHelmWheel : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private HelmTask helm;

    [Tooltip("돌릴 바퀴 모델. 비워두면 이 오브젝트.")]
    [SerializeField] private Transform wheel;

    // ------------------------------------------------------------
    // ⚠ **왜 음수인가 — 화면에서 보는 방향이 뒤집히기 때문입니다.**
    //
    //   D 를 누르면 Heading 이 **양수(우)** 가 됩니다. 배는 오른쪽으로 갑니다.
    //   그런데 +z 축으로 양의 각도를 주면 +x 가 +y 쪽으로 돕니다.
    //   카메라는 배 **뒤(-z)** 에서 +z 를 보고 있어서, 그게 화면에서는
    //   **반시계**로 보입니다. 우로 꺾는데 바퀴가 왼쪽으로 도는 셈입니다.
    //
    //   조타 판정은 맞습니다. 바퀴가 보이는 방향만 뒤집으면 됩니다.
    // ------------------------------------------------------------

    [Header("얼마나 돌지")]
    [Tooltip("뱃머리 1도당 바퀴가 몇 도 도는가.\n\n" +
             "-1 이면 조타한 만큼 그대로 돈다. (음수인 이유는 위 주석)\n" +
             "진짜 배는 여러 바퀴를 돌리므로 -3 ~ -5 로 두면 더 배 같다.\n" +
             "반대로 돌면 부호를 바꾸면 된다.")]
    [SerializeField] private float degreesPerHeading = -1f;

    /// <summary>
    /// 도는 축. 바퀴는 얇은 쪽이 축입니다.
    ///
    /// 이 배의 조타륜은 앞뒤(z)로 얇습니다. (폭 2.05 · 높이 2.08 · 두께 0.45)
    /// 모델을 바꾸면 얇은 쪽을 보고 여기도 바꿉니다.
    /// </summary>
    [SerializeField] private Vector3 spinAxis = Vector3.forward;

    // ⚠ **로컬 기준으로 돌립니다.**
    //
    //    월드 기준으로 돌렸더니 **배가 틀어져도 바퀴만 제자리**에 있었습니다.
    //    바퀴는 배의 자식이라, 월드 자리를 통째로 덮어쓰면 부모가 움직인 것이
    //    지워집니다. 로컬로 두면 배를 따라가고 그 위에서 혼자 돕니다.
    private Quaternion _restLocalRotation;
    private Vector3 _localCenter;
    private Vector3 _localFromCenter;
    private float _radius = 1f;
    private bool _ready;

    // ------------------------------------------------------------
    // 자리 자세(ShipCoopStationPose)가 손을 테에 놓을 때 보는 값들.
    // 바퀴가 도는 규칙은 여기 하나라, 손도 같은 값으로 돌려야 어긋나지 않는다.
    // ------------------------------------------------------------

    /// <summary>바퀴 한가운데 (월드).</summary>
    public Vector3 Center => _ready && wheel.parent != null ? wheel.parent.TransformPoint(_localCenter) : (wheel != null ? wheel.position : transform.position);

    /// <summary>바퀴 반지름 (m). 켤 때 렌더러 경계에서 잰다.</summary>
    public float Radius => _radius;

    /// <summary>도는 축 (월드). 로컬 회전은 부모 축 기준이라 부모로 바꾼다.</summary>
    public Vector3 Axis => wheel != null && wheel.parent != null ? wheel.parent.TransformDirection(spinAxis).normalized : spinAxis.normalized;

    /// <summary>지금 바퀴가 돌아간 각도 (도). 조타 각도 × 배율.</summary>
    public float SpinDegrees => helm != null ? helm.Heading * degreesPerHeading : 0f;

    private void Awake()
    {
        if (helm == null)
        {
            helm = FindAnyObjectByType<HelmTask>(FindObjectsInactive.Include);
        }

        if (wheel == null)
        {
            wheel = transform;
        }

        if (helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 바퀴가 안 돕니다.", this);
            return;
        }

        _restLocalRotation = wheel.localRotation;

        // 바퀴 한가운데를 축으로 삼는다. 원점이 어디에 있든 상관없어진다.
        Renderer draw = wheel.GetComponent<Renderer>();
        Vector3 worldCenter = draw != null ? draw.bounds.center : wheel.position;

        Transform parent = wheel.parent;

        _localCenter = parent != null ? parent.InverseTransformPoint(worldCenter) : worldCenter;
        _localFromCenter = wheel.localPosition - _localCenter;

        // 반지름 — 축에 수직인 두 방향 중 큰 쪽. 축이 z 면 x · y 가 바퀴 면이다.
        if (draw != null)
        {
            Vector3 e = draw.bounds.extents;
            Vector3 a = new Vector3(Mathf.Abs(spinAxis.x), Mathf.Abs(spinAxis.y), Mathf.Abs(spinAxis.z));
            float rx = a.x > 0.5f ? 0f : e.x;
            float ry = a.y > 0.5f ? 0f : e.y;
            float rz = a.z > 0.5f ? 0f : e.z;
            _radius = Mathf.Max(rx, ry, rz, 0.2f);
        }

        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready)
        {
            return;
        }

        Quaternion spin = Quaternion.AngleAxis(helm.Heading * degreesPerHeading, spinAxis.normalized);

        wheel.localRotation = spin * _restLocalRotation;
        wheel.localPosition = _localCenter + spin * _localFromCenter;
    }
}
