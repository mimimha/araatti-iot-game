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
///
/// ⚠ **기울기가 뱃머리보다 크면 안 됩니다.**
///
///    한때 뱃머리는 조타 **속도**로, 기울기는 조타 **각도**로 몰았습니다.
///    조타륜을 꺾어 두면 뱃머리는 저절로 풀려서 일자가 되는데 기울기만
///    남아서, **배가 도는 게 아니라 기울기만 하는 것**으로 보였습니다.
///
///    지금은 둘 다 뱃머리 각도(`_yaw`)에서 나옵니다. 기울기는 뱃머리가
///    틀어진 비율만큼만 따라갑니다. 혼자 기울 수 없습니다.
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

    // ------------------------------------------------------------
    // 뱃머리 각도는 **두 가지를 더해서** 냅니다
    //
    //    ① 꺾는 **순간**의 쏠림 — 조타 각도가 바뀌는 **속도**에 비례.
    //       키를 돌리는 동안만 나오고, 손을 멈추면 사라집니다.
    //       뱃머리가 먼저 쏠렸다가 나머지가 따라오는 느낌이 여기서 납니다.
    //
    //    ② 꺾어 **둔 동안**의 각 — 조타 **각도**에 비례. (`heldDegrees`)
    //       조타륜을 되돌려야 사라집니다.
    //
    //    ①만 쓰면 조타륜을 끝까지 꺾고 잡고 있어도 배가 저절로 일자가 됩니다.
    //    ②만 쓰면 꺾는 순간의 쏠림이 없어서 무겁게 안 보입니다.
    //
    // ⚠ **고물은 화면에 안 나옵니다.**
    //
    //    카메라는 갑판 한가운데(z 0)에서 13m 뒤, 즉 z −13 에 있고 고물은
    //    z −19.7 입니다. **카메라 뒤**입니다. 그래서 "고물이 쓸리는 것"으로
    //    도는 것을 보여줄 수는 없습니다. 보이는 것은 뱃머리뿐입니다.
    //    축(`pivotZ`)을 고물로 옮겨봐도 화면상 5.9% → 6.3% 로 거의 안 변합니다.
    //    카메라가 갑판을 0.25초 지연으로 따라가면서 상쇄하기 때문입니다.
    //    그래서 **축이 아니라 각도를 키워야** 도는 것이 읽힙니다.
    // ------------------------------------------------------------

    [Header("얼마나 틀지")]
    [Tooltip("조타가 초당 1도 바뀔 때 배가 몇 도 틀어지는가.\n\n" +
             "조타는 초당 35도로 돈다. 0.35 면 꺾는 동안 약 12도 쏠린다.\n" +
             "손을 멈추면 0 으로 돌아와 배가 일자가 된다.")]
    [SerializeField, Range(0f, 1f)] private float degreesPerTurnRate = 0.35f;

    [Tooltip("아무리 빨리 꺾어도 이 각도를 넘지 않는다.")]
    [SerializeField, Range(0f, 40f)] private float mostDegrees = 14f;

    // ------------------------------------------------------------
    // ⚠ **속도만으로는 부족합니다. 꺾어 둔 각도도 섞어야 합니다.**
    //
    //    속도만 쓰면 조타륜을 끝까지 꺾고 **계속 잡고 있어도 배가 저절로
    //    일자로 돌아옵니다.** 재보니 2초에 11도까지 갔다가 3초에 2.6도로
    //    풀렸습니다. 뱃머리가 좌우로 간다는 느낌이 안 납니다.
    //
    //    원래 주석에는 "카메라가 배를 따라 도니까 각도를 쓰면 안 된다" 고
    //    적혀 있었는데 **틀렸습니다.** `ShipCoopCamera` 는 갑판 자리만 따라가고
    //    방향은 안 따라 돕니다 (Q/E 로 사람이 돌린 각만 씁니다). 그래서
    //    배를 틀어 두면 화면에서 그대로 틀어져 보입니다.
    //
    //    측정값 (조타 끝까지 꺾고 유지, 뱃머리가 화면에서 오간 폭 / 1920px)
    //      섞지 않음   113px  5.9%   ← 잡고 있어도 풀림
    //      12도 섞기   264px 13.8%
    //      18도 섞기   290px 15.1%   ← 이걸로 함. 꺾은 동안 20도 근처 유지
    //      25도 섞기   324px 16.9%   갑판이 너무 비스듬해짐
    // ------------------------------------------------------------

    [Tooltip("조타륜을 끝까지 꺾어 둔 동안 배가 유지하는 각도 (도).\n\n" +
             "0 이면 꺾고 있어도 배가 저절로 일자로 돌아옵니다.\n" +
             "위의 속도 성분은 **꺾는 순간의 쏠림**, 이것은 **꺾어 둔 동안의 각**입니다.")]
    [SerializeField, Range(0f, 35f)] private float heldDegrees = 18f;

    // ------------------------------------------------------------
    // ⚠ **축을 고정해두면 고물이 뱃머리보다 먼저, 더 크게 움직입니다.**
    //
    //    축이 뱃머리 쪽 1/3 지점에 박혀 있으면 고물이 가장 긴 팔로 휘둘립니다.
    //    조타수 시점(뒷갑판)에서 재보니 이랬습니다.
    //
    //      0.3초 —  뱃머리 +10px · 고물 −97px    ← 고물이 10배, 그것도 먼저
    //
    //    강체는 앞뒤가 **동시에** 돕니다. 그래서 "뱃머리가 먼저 가고 고물이
    //    따라온다"를 만들려면 **축 자체가 움직여야** 합니다.
    //
    //    진짜 배가 그렇게 돕니다. 키를 꺾은 직후에는 회전축이 고물 근처에
    //    있어서 뱃머리만 돌아가고, 선회가 자리잡으면서 축이 앞으로 옮겨가
    //    고물이 바깥으로 쓸려 나갑니다.
    //
    //      축 미끄러짐 1.2초
    //        0.3초 —  뱃머리  +23px · 고물   −7px   뱃머리만 먼저
    //        1.0초 —  뱃머리 +145px · 고물 −186px   고물이 따라붙음
    //        2.0초 —  뱃머리 +268px · 고물 −518px   고물이 크게 쓸림
    // ------------------------------------------------------------

    [Header("축 — 고물에서 뱃머리 쪽으로 미끄러진다")]
    [Tooltip("다 돌았을 때의 축 z. **뱃머리에서 1/3 지점.**\n\n" +
             "여기까지 오면 고물이 가장 크게 바깥으로 쓸립니다.")]
    [SerializeField] private float pivotZ = 8.6f;

    [Tooltip("돌기 시작할 때의 축 z. **고물 자리.**\n\n" +
             "여기 있는 동안에는 고물이 제자리에 있고 뱃머리만 돌아갑니다.")]
    [SerializeField] private float sternPivotZ = -19.7f;

    [Tooltip("축이 고물에서 뱃머리 쪽으로 옮겨가는 데 걸리는 시간(초).\n\n" +
             "**고물이 따라오는 속도가 이 값입니다.** 키우면 더 늦게 따라옵니다.\n" +
             "0 이면 축이 안 움직여서 고물이 뱃머리보다 먼저 움직입니다.")]
    [SerializeField, Range(0f, 3f)] private float sternFollowSeconds = 1.2f;

    [Header("얼마나 천천히 따라올지")]
    [Tooltip("목표 각도까지 도는 데 걸리는 시간(초).\n\n" +
             "0 이면 조타와 동시에 **툭 꺾입니다.** 배는 무거워서 천천히 돕니다.\n" +
             "뱃머리가 먼저 가고 나머지가 따라오는 느낌은 여기서 나옵니다.")]
    [SerializeField, Range(0f, 3f)] private float followSeconds = 0.8f;

    [Header("떨림 막기")]
    // ⚠ 이 값이 0 이면 배가 덜덜 떨립니다. 자세한 것은 LateUpdate 주석 참고.
    [Tooltip("조타 속도를 이만큼 다듬어서 쓴다 (초).\n\n" +
             "0 이면 프레임 시간의 흔들림이 그대로 뱃머리로 갑니다.\n" +
             "너무 키우면 꺾는 반응이 굼떠집니다.")]
    [SerializeField, Range(0f, 0.5f)] private float rateSmoothSeconds = 0.15f;

    [Tooltip("이보다 느리게 도는 것은 안 도는 것으로 친다 (도/초).\n" +
             "손을 뗐는데 남은 미세한 값이 배를 계속 흔드는 것을 막는다.")]
    [SerializeField, Min(0f)] private float rateDeadZone = 1.5f;

    // ------------------------------------------------------------
    // ⚠ **기울기는 조타 각도가 아니라 배가 실제로 튼 각도를 따라야 합니다.**
    //
    //    처음에는 조타 각도(`Heading01`)에 비례해서 기울였습니다. 그런데 그때는
    //    뱃머리가 저절로 일자로 풀리고 있어서, **배는 일자인데 기울어만 있는**
    //    상태가 됐습니다. 그래서 도는 게 아니라 기우는 것으로만 보였습니다.
    //
    //    지금은 `_yaw` 를 따라갑니다. 뱃머리가 안 틀어져 있으면 기울지도
    //    않습니다. 기울기가 혼자 나타날 수 없습니다.
    // ------------------------------------------------------------

    [Header("기울기")]
    [Tooltip("배가 최대로 틀어졌을 때 도는 쪽으로 기우는 정도 (도).\n\n" +
             "0 이면 안 기운다. 조금 주면 도는 게 더 잘 읽힌다.\n" +
             "많이 주면 갑판이 눈에 띄게 기울어서 걷기 이상해진다.")]
    [SerializeField, Range(0f, 12f)] private float bankDegrees = 3f;

    // ⚠ **각도가 아니라 행렬로 들고 있어야 합니다.**
    //
    //    축이 움직이므로 "지금까지 돌린 각도"만으로는 되돌릴 수 없습니다.
    //    축이 옮겨간 만큼 **밀린 거리**가 같이 쌓이기 때문입니다. 각도만 되돌리면
    //    밀린 것이 안 풀려서, 조타를 중앙으로 되돌려도 **배가 옆으로 밀린 채**
    //    남습니다. 그것이 매번 쌓이면 배가 바다 밖으로 걸어 나갑니다.
    //
    //    축 P 를 중심으로 R 만큼 도는 것을 통째로 행렬 하나로 둡니다.
    //
    //        M = T(P) · R · T(−P)
    //
    //    R 이 0 이면 축이 어디에 있든 M 은 항상 제자리입니다.
    //    그래서 조타를 중앙으로 되돌리면 **밀린 것까지 정확히 풀립니다.**

    /// <summary>지금까지 적용해 놓은 것. 여기서 얼마나 더 갈지를 뺀다.</summary>
    private Matrix4x4 _applied = Matrix4x4.identity;

    /// <summary>축이 고물(0)에서 뱃머리 쪽(1)으로 얼마나 왔는지.</summary>
    private float _slide;

    private float _slideSpeed;

    private Vector3 _pivot;
    private bool _ready;

    /// <summary>지금 각도와 그 변화 속도. 목표를 향해 천천히 간다.</summary>
    private float _yaw;

    private float _yawSpeed;

    /// <summary>지난 프레임의 조타 각도. 얼마나 빨리 바뀌는지를 여기서 낸다.</summary>
    private float _lastHeading;

    /// <summary>다듬은 조타 속도. 날것을 그대로 쓰면 배가 떨린다.</summary>
    private float _rate;

    private float _rateSpeed;

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

        // z 는 매 프레임 바뀐다. x · y 만 여기서 잡아둔다.
        _pivot = new Vector3(transform.position.x, transform.position.y, pivotZ);
        _applied = Matrix4x4.identity;
        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready || carried == null)
        {
            return;
        }

        // 조타가 **얼마나 빨리 바뀌고 있는지.** 각도 자체가 아니다. (위 주석)
        float raw = Time.deltaTime > 0f
            ? (helm.Heading - _lastHeading) / Time.deltaTime
            : 0f;

        _lastHeading = helm.Heading;

        // ------------------------------------------------------------
        // ⚠ **이 값을 그대로 쓰면 배가 덜덜 떨립니다.**
        //
        //    `변화량 / deltaTime` 은 수치 미분입니다. 조타는 초당 35도로 고르게
        //    도는데, **deltaTime 이 프레임마다 흔들리면** 나눈 결과가 크게
        //    출렁입니다. 60fps 에서 dt 가 15~18ms 사이로만 놀아도
        //    rate 가 32~39 로 튀고, 그게 그대로 뱃머리 각도로 갑니다.
        //
        //    SmoothDamp 는 목표를 **따라가는** 것이라, 목표 자체가 떨리면
        //    떨림이 그대로 남습니다. 그래서 목표를 만들기 전에 먼저 다듬습니다.
        // ------------------------------------------------------------

        _rate = Mathf.SmoothDamp(_rate, raw, ref _rateSpeed, rateSmoothSeconds);

        // 손을 뗐는데 미세하게 남은 값이 계속 배를 흔드는 것을 막는다.
        if (Mathf.Abs(_rate) < rateDeadZone)
        {
            _rate = 0f;
        }

        // 두 가지를 더한다.
        //   꺾는 **순간**의 쏠림 — 손을 멈추면 사라진다
        //   꺾어 **둔 동안**의 각 — 조타륜을 되돌려야 사라진다 (위 주석)
        float wantYaw = Mathf.Clamp(_rate * degreesPerTurnRate, -mostDegrees, mostDegrees)
                        + helm.Heading01 * heldDegrees;

        if (followSeconds > 0f)
        {
            _yaw = Mathf.SmoothDamp(_yaw, wantYaw, ref _yawSpeed, followSeconds);
        }
        else
        {
            _yaw = wantYaw;
        }

        // 기울기는 **배가 실제로 튼 만큼**만. 혼자 기울 수 없다. (위 주석)
        float full = Mathf.Max(mostDegrees + heldDegrees, 0.001f);
        float bank = -Mathf.Clamp(_yaw / full, -1f, 1f) * bankDegrees;

        // ⚠ x(끄덕임)은 건드리지 않는다. 뱃머리가 들리거나 처지면 안 된다.
        Quaternion want = Quaternion.Euler(0f, _yaw, bank);

        // 축을 고물에서 뱃머리 쪽으로 미끄러뜨린다. **고물이 따라오는 것**이 여기서 나온다.
        float turned = Mathf.Clamp01(Mathf.Abs(_yaw) / full);
        _slide = Mathf.SmoothDamp(_slide, turned, ref _slideSpeed, sternFollowSeconds);

        _pivot.z = Mathf.Lerp(sternPivotZ, pivotZ, Mathf.Clamp01(_slide));

        // 축을 옮겨도 밀린 것이 안 쌓이게 행렬로 들고 있는다. (위 주석)
        Matrix4x4 whole = Matrix4x4.Translate(_pivot)
                          * Matrix4x4.Rotate(want)
                          * Matrix4x4.Translate(-_pivot);

        Matrix4x4 step = whole * _applied.inverse;

        Quaternion spin = step.rotation;
        Vector3 shift = step.GetColumn(3);

        if (Quaternion.Angle(spin, Quaternion.identity) < 0.001f && shift.sqrMagnitude < 1e-8f)
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

            t.position = step.MultiplyPoint3x4(t.position);
            t.rotation = spin * t.rotation;
        }

        _applied = whole;

        // 콜라이더를 손으로 옮겼으니 물리에 알려준다.
        // 안 하면 사람이 갑판을 뚫거나 벽에 끼인다.
        Physics.SyncTransforms();
    }
}
