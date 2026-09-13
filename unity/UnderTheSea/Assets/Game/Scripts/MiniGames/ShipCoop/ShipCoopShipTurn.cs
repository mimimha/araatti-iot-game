using UnityEngine;

/// <summary>
/// 조타한 만큼 **뱃머리를 튼다.** (SHIPCOOP.md 4장)
///
/// 배가 옆으로 평행이동하면 안 됩니다. 진짜 배는 제자리에서 돌아서
/// **뱃머리가 한쪽으로, 고물이 반대쪽으로** 갑니다.
/// 그래서 배 한가운데를 축으로 돌립니다.
///
/// ⚠ **배만 돌리면 안 됩니다.**
///
///    처음에 배 모델만 돌렸더니 **배만 틀어지고 갑판 · 작업 자리 · 사람은
///    제자리에** 있었습니다. 사람이 허공을 걷고 조타륜이 따로 놀았습니다.
///
///    걸어다니는 바닥은 보이지 않는 큐브라 배의 자식이 아닙니다.
///    작업 자리도 사람도 따로 놓여 있습니다. 그래서 **같이 돌 것들을 들고
///    있다가 한꺼번에 돌립니다.** (`carried`)
///
/// ⚠ 사람은 스스로도 움직입니다. 그래서 **절대 각도를 덮어쓰지 않고**
///    지난 프레임과의 **차이만큼만** 돌립니다. 그래야 걷던 것이 안 지워집니다.
///    움직이는 발판을 태우는 것과 같은 방식입니다.
/// </summary>
public class ShipCoopShipTurn : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private HelmTask helm;

    [Header("같이 돌 것들")]
    [Tooltip("배 · 걷는 바닥 · 작업 자리 · 상자 · 사람.\n\n" +
             "여기 없는 것은 제자리에 남아서 배와 따로 놉니다.\n" +
             "배치 도구(ShipCoopDeckLayout)가 채웁니다.")]
    [SerializeField] private Transform[] carried;

    [Header("얼마나 틀지")]
    [Tooltip("뱃머리 1도당 배가 몇 도 도는가.\n\n" +
             "0.18 이면 최대 조타(60도)에서 약 11도 튼다.\n" +
             "같이 도는 것들을 다 들고 있으므로 더 키워도 어긋나지는 않는다.\n" +
             "다만 많이 돌면 카메라가 배를 옆에서 보게 되어 갑판이 좁아 보인다.")]
    [SerializeField, Range(0f, 0.6f)] private float degreesPerHeading = 0.18f;

    [Tooltip("도는 축의 z.\n\n" +
             "진짜 배는 한가운데가 아니라 **뱃머리에서 1/3 지점**을 축으로 돕니다.\n" +
             "그래서 뱃머리는 조금 움직이고 **고물이 크게 바깥으로 쓸립니다.**\n" +
             "한가운데에 두면 앞뒤가 똑같이 벌어져서 제자리에서 도는 것처럼 보입니다.")]
    [SerializeField] private float pivotZ = 8.6f;

    [Header("얼마나 천천히 따라올지")]
    [Tooltip("목표 각도까지 도는 데 걸리는 시간(초).\n\n" +
             "0 이면 조타와 동시에 **툭 꺾입니다.** 배는 무거워서 천천히 돕니다.\n" +
             "뱃머리가 먼저 가고 나머지가 따라오는 느낌은 여기서 나옵니다.")]
    [SerializeField, Range(0f, 3f)] private float followSeconds = 0.8f;

    [Header("기울기")]
    [Tooltip("도는 쪽으로 배가 기우는 정도 (도).\n\n" +
             "0 이면 안 기운다. 조금 주면 **도는 게 훨씬 잘 읽힌다.**\n" +
             "많이 주면 갑판이 눈에 띄게 기울어서 걷기 이상해진다.")]
    [SerializeField, Range(0f, 12f)] private float bankDegrees = 4f;

    /// <summary>지금까지 돌려놓은 각도. 여기서 얼마나 더 돌지를 뺀다.</summary>
    private Quaternion _applied = Quaternion.identity;

    private Vector3 _pivot;
    private bool _ready;

    /// <summary>지금 각도와 그 변화 속도. 목표를 향해 천천히 간다.</summary>
    private float _yaw;

    private float _yawSpeed;
    private float _bank;
    private float _bankSpeed;

    private void Awake()
    {
        if (helm == null)
        {
            helm = FindAnyObjectByType<HelmTask>(FindObjectsInactive.Include);
        }

        if (helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 배가 안 틀어집니다.", this);
            return;
        }

        _pivot = new Vector3(transform.position.x, transform.position.y, pivotZ);
        _applied = Quaternion.identity;
        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready || carried == null)
        {
            return;
        }

        // 배는 무겁다. 조타를 꺾어도 **천천히** 그 각도로 간다.
        // 이 뒤처짐이 "뱃머리가 먼저 가고 나머지가 따라오는" 느낌을 만든다.
        float wantYaw = helm.Heading * degreesPerHeading;
        float wantBank = -helm.Heading01 * bankDegrees;

        if (followSeconds > 0f)
        {
            _yaw = Mathf.SmoothDamp(_yaw, wantYaw, ref _yawSpeed, followSeconds);
            _bank = Mathf.SmoothDamp(_bank, wantBank, ref _bankSpeed, followSeconds);
        }
        else
        {
            _yaw = wantYaw;
            _bank = wantBank;
        }

        Quaternion want = Quaternion.Euler(0f, _yaw, _bank);

        // 지난번에 돌려놓은 것에서 **얼마나 더** 돌지.
        Quaternion step = want * Quaternion.Inverse(_applied);

        if (Quaternion.Angle(step, Quaternion.identity) < 0.001f)
        {
            return;
        }

        for (int i = 0; i < carried.Length; i++)
        {
            Transform t = carried[i];

            if (t == null)
            {
                continue;
            }

            t.position = _pivot + step * (t.position - _pivot);
            t.rotation = step * t.rotation;
        }

        _applied = want;

        // 콜라이더를 손으로 옮겼으니 물리에 알려준다.
        // 안 하면 사람이 갑판을 뚫거나 벽에 끼인다.
        Physics.SyncTransforms();
    }
}
