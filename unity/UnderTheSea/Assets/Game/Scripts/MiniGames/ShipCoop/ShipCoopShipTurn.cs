using System.Collections.Generic;
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
    [Tooltip("배가 옆으로 미끄러지는 속도를 여기서 가져온다. 그것으로 뱃머리를 튼다.")]
    [SerializeField] private VoyageSea sea;

    [Header("같이 돌 것들")]
    [Tooltip("배 · 걷는 바닥 · 작업 자리 · 상자 · 사람.\n\n" +
             "여기 없는 것은 제자리에 남아서 배와 따로 놉니다.\n" +
             "배치 도구(ShipCoopDeckLayout)가 채웁니다.")]
    [SerializeField] private Transform[] carried;

    // ------------------------------------------------------------
    // ⚠ **뱃머리는 "배가 옆으로 가는 속도"를 따라 틉니다.**
    //
    //    ⛔ 조타를 **돌리는 속도**로 틀면 안 됩니다. 그렇게 했더니 배가
    //       **어떨 때는 뱃머리부터 가고 어떨 때는 평행으로** 미끄러졌습니다.
    //
    //       뱃머리 회전은 "조타를 돌리는 속도"를, 옆 이동(`VoyageSea.ShipLateral`)은
    //       "조타 각도"를 봤습니다. **서로 다른 것을 봅니다.** 그래서 휠에서
    //       손을 멈추면(끝까지 꺾어 잡고 있어도) 뱃머리는 즉시 펴지는데
    //       배는 3초를 더 미끄러졌습니다. 그 구간이 전부 평행이었습니다.
    //
    //         2.0초  뱃머리 −10.6도 · 옆속도 5.29 m/s   뱃머리가 나간 채
    //         3.6초  뱃머리  −0.8도 · 옆속도 1.10 m/s   ⚠ 평행
    //         4.4초  뱃머리  −0.1도 · 옆속도 0.39 m/s   ⚠ 평행
    //
    //    옆으로 가는 속도를 보면 둘이 같은 것을 봅니다.
    //    **미끄러지는 동안은 늘 그쪽으로 뱃머리가 나가 있고, 다 미끄러져
    //    멈추면 저절로 일자가 됩니다.** 시간이 지나면 일자가 되는 것도 그대로입니다.
    //
    //      휠을 끝까지 꺾고 잡고 있기      평행 구간 14% · 끝 뱃머리 0.00도
    //      살짝만 건드리기                 평행 구간 28% · 끝 뱃머리 0.00도
    //      꺾었다가 중앙으로 되돌리기       평행 구간 13% · 끝 뱃머리 0.03도
    // ------------------------------------------------------------

    [Header("얼마나 틀지")]
    [Tooltip("배가 옆으로 초당 1m 갈 때 뱃머리를 몇 도 트는가.\n\n" +
             "옆으로 가는 최고 속도가 5.5m/s 라 2.2 면 최대 12도쯤 틀어진다.")]
    [SerializeField, Range(0f, 6f)] private float degreesPerSlideSpeed = 2.2f;

    [Tooltip("아무리 빨리 미끄러져도 이 각도를 넘지 않는다.")]
    [SerializeField, Range(0f, 40f)] private float mostDegrees = 14f;

    // ------------------------------------------------------------
    // ⛔ **조타 각도에 비례하는 성분을 넣지 마세요.**
    //
    //    "뱃머리가 더 크게 돌면 좋겠다" 싶어서 조타 각도(`Heading01`)에
    //    비례하는 각을 더해본 적이 있습니다. 뱃머리는 크게 돌았는데,
    //    **조타륜을 잡고 있는 내내 배가 비스듬한 채로 서 있었습니다.**
    //
    //    시간이 지나면 배는 **다시 일자가 되어야 합니다.** 카메라가 배를
    //    따라다니므로, 비스듬한 채로 굳으면 화면이 통째로 기울어 보입니다.
    //    실제로 그렇게 만들었다가 되돌렸습니다.
    //
    //    속도 성분만 쓰면 조타륜을 끝까지 꺾고 잡고 있어도 이렇게 됩니다.
    //
    //      0.5초  배  3.0도   2.0초  배 10.6도   3.0초  배 2.6도   4.5초  배 0도
    //
    //    뱃머리가 크게 안 도는 것은 각도로 해결할 문제가 아니라
    //    **축을 움직여서** 해결합니다. (아래 `sternFollowSeconds`)
    // ------------------------------------------------------------

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
    [SerializeField, Range(0f, 3f)] private float followSeconds = 0.4f;

    [Header("떨림 막기")]
    [Tooltip("이보다 느리게 미끄러지는 것은 안 움직이는 것으로 친다 (m/초).\n" +
             "다 미끄러진 뒤 남은 미세한 값이 배를 계속 흔드는 것을 막는다.")]
    [SerializeField, Min(0f)] private float slideDeadZone = 0.15f;

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

    [Header("충격 흔들림 — 데미지를 받았을 때 한 번")]
    [Tooltip("흔들림이 초당 이만큼(도)씩 잦아든다.")]
    [SerializeField, Min(0.1f)] private float shakeDecayDegreesPerSecond = 40f;

    [Tooltip("좌우로 왔다 갔다 하는 빠르기. 크면 부르르 떨고, 작으면 한 번 크게 기운다.")]
    [SerializeField, Min(0.1f)] private float shakeFrequency = 16f;

    /// <summary>지금 남은 흔들림 세기(도). 0 이면 조용하다.</summary>
    private float _shakeDegrees;

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

    /// <summary>
    /// 게임 도중에 태운 것들. 접속해서 생긴 사람이 여기 들어온다.
    ///
    /// 인스펙터의 <c>carried</c> 는 씬에 처음부터 있는 것만 담을 수 있다.
    /// 스폰되는 플레이어는 씬 파일에 없으므로 실행 중에 등록해야 한다.
    /// </summary>
    private readonly List<Transform> _riders = new List<Transform>();

    /// <summary>
    /// **배가 지금까지 튼 것 전체.** 안 튼 배의 자리 → 지금 자리. 거꾸로 하면 지금 자리 → 안 튼 배의 자리다.
    ///
    /// 네트워크로 온 것을 <b>이 화면의 배</b>에 맞춰 놓을 때 쓴다(<c>ShipCoopHoleSync</c>).
    /// 배는 피어마다 조타 값으로 따로 돌리므로, 서버가 돌린 월드 자리를 그대로 받으면 지연만큼 어긋난다.
    /// </summary>
    public Matrix4x4 Applied => _applied;

    /// <summary>
    /// **배에 태운다.** 이 뒤로 배가 트는 만큼 같이 돈다.
    ///
    /// 네트워크에서는 <b>서버에서만</b> 부른다. 클라이언트의 사람은 자리를
    /// <c>NetworkTransform</c> 으로 받으므로, 거기서 또 돌리면 두 번 돌아간다.
    /// </summary>
    public void Carry(Transform rider)
    {
        if (rider == null || _riders.Contains(rider))
        {
            return;
        }

        _riders.Add(rider);
    }

    /// <summary>배에서 내린다. 사라진 것을 계속 들고 있지 않게 한다.</summary>
    public void StopCarrying(Transform rider)
    {
        if (rider != null)
        {
            _riders.Remove(rider);
        }
    }

    /// <summary>
    /// 배를 한 번 흔든다. 적선을 못 막아 포격을 맞는 등, 데미지를 받았다는 것을
    /// 눈으로도 알려줄 때 부른다.
    ///
    /// 이미 흔들리는 중이면 더 센 쪽만 남긴다(<see cref="Mathf.Max"/>) — 연달아 맞아도
    /// 흔들림이 매번 처음부터 다시 쌓이지 않는다.
    /// </summary>
    public void Shake(float degrees)
    {
        _shakeDegrees = Mathf.Max(_shakeDegrees, degrees);
    }

    private void Awake()
    {
        if (sea == null)
        {
            sea = FindAnyObjectByType<VoyageSea>(FindObjectsInactive.Include);
        }

        if (sea == null)
        {
            Debug.LogWarning($"[{name}] 바다를 찾지 못했습니다. 배가 안 틀어집니다.", this);
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

        // ------------------------------------------------------------
        // 배가 **지금 옆으로 얼마나 빠르게 미끄러지고 있는지.** (위 주석)
        //
        // ⚠ 이 값을 다시 미분하지 마세요. `VoyageSea` 의 SmoothDamp 가 들고 있는
        //    속도라 이미 매끄럽습니다. `변화량 / deltaTime` 으로 다시 뽑으면
        //    프레임 시간의 흔들림이 그대로 뱃머리로 가서 배가 덜덜 떨립니다.
        // ------------------------------------------------------------

        float slide = sea.LateralSpeed;

        // 다 미끄러진 뒤 남은 미세한 값이 계속 배를 흔드는 것을 막는다.
        if (Mathf.Abs(slide) < slideDeadZone)
        {
            slide = 0f;
        }

        // 미끄러지는 쪽으로 뱃머리가 나간다. 멈추면 저절로 일자가 된다.
        float wantYaw = Mathf.Clamp(slide * degreesPerSlideSpeed, -mostDegrees, mostDegrees);

        if (followSeconds > 0f)
        {
            _yaw = Mathf.SmoothDamp(_yaw, wantYaw, ref _yawSpeed, followSeconds);
        }
        else
        {
            _yaw = wantYaw;
        }

        // 기울기는 **배가 실제로 튼 만큼**만. 혼자 기울 수 없다. (위 주석)
        // _yaw 가 0 으로 돌아오면 기울기도 같이 0 이 된다.
        float full = Mathf.Max(mostDegrees, 0.001f);
        float bank = -Mathf.Clamp(_yaw / full, -1f, 1f) * bankDegrees;

        // 충격 흔들림. 감쇠하는 사인파로 좌우 기울기(roll)에 더한다 — 시간이 지나면
        // 저절로 0 이 되어 원래 자세로 돌아온다. (위 Shake 참고)
        float shakeRoll = 0f;

        if (_shakeDegrees > 0.01f)
        {
            shakeRoll = Mathf.Sin(Time.time * shakeFrequency) * _shakeDegrees;
            _shakeDegrees = Mathf.MoveTowards(_shakeDegrees, 0f, shakeDecayDegreesPerSecond * Time.deltaTime);
        }
        else
        {
            _shakeDegrees = 0f;
        }

        // ⚠ x(끄덕임)은 건드리지 않는다. 뱃머리가 들리거나 처지면 안 된다.
        Quaternion want = Quaternion.Euler(0f, _yaw, bank + shakeRoll);

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
            Turn(carried[i], step, spin);
        }

        // 게임 도중에 태운 것들. 사라진 것은 여기서 걸러낸다.
        for (int i = _riders.Count - 1; i >= 0; i--)
        {
            if (_riders[i] == null)
            {
                _riders.RemoveAt(i);
                continue;
            }

            Turn(_riders[i], step, spin);
        }

        _applied = whole;

        // 콜라이더를 손으로 옮겼으니 물리에 알려준다.
        // 안 하면 사람이 갑판을 뚫거나 벽에 끼인다.
        Physics.SyncTransforms();
    }

    /// <summary>한 물건을 배가 튼 만큼 옮기고 돌린다.</summary>
    private static void Turn(Transform t, Matrix4x4 step, Quaternion spin)
    {
        if (t == null)
        {
            return;
        }

        t.position = step.MultiplyPoint3x4(t.position);
        t.rotation = spin * t.rotation;
    }
}
