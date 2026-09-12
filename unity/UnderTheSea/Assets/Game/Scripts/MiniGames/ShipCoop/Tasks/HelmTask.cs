using UnityEngine;

/// <summary>
/// 🛞 조타. 뱃머리 방향을 돌린다.
///
/// 조타 자체로는 점수가 나지 않습니다. 방향을 맞춰서
/// 암초를 피하고 파도를 정면으로 받는 데 씁니다. (SHIPCOOP.md 5장)
/// 그래서 사건이 읽어갈 <see cref="Heading"/> 을 관리하는 것이 이 작업의 전부입니다.
///
/// 비워두면 뱃머리가 꺾인 채로 방치되고, 그 상태로 사건을 만나면 충돌합니다. (4장)
///
/// 입력
///   양손을 같은 방향으로 기울인다 → ShipCoopInput.Steer (-1 좌 ~ +1 우)
///   키보드는 A / D. **회전 속도는 IoT 와 같아야 합니다.** (7장)
/// </summary>
public class HelmTask : TaskBase
{
    [Header("회전")]
    [Tooltip("조타를 끝까지 꺾었을 때 초당 몇 도 돌아가는지")]
    [SerializeField] private float turnSpeed = 35f;

    [Tooltip("정면(0도)에서 좌우로 이만큼까지만 꺾인다.")]
    [SerializeField] private float maxHeading = 60f;

    [Header("자리가 비었을 때")]
    [Tooltip("아무도 없으면 조타륜이 초당 이 각도만큼 정면으로 돌아온다.\n" +
             "0 이면 꺾인 채로 방치된다. 비운 대가를 주려면 0 으로 둔다.")]
    [SerializeField, Min(0f)] private float recenterSpeed = 0f;

    [Header("배 회전 (선택)")]
    [Tooltip("연결하면 이 오브젝트를 뱃머리 각도만큼 실제로 돌린다.\n" +
             "⚠ 지금은 비워두세요. 플레이어가 배의 자식이 아니라서 배를 돌리면 " +
             "자리가 함께 돌아가고 사람이 상호작용 범위에서 튕겨나갑니다.\n" +
             "진짜 캐릭터가 갑판에 올라타면 연결합니다.")]
    [SerializeField] private Transform shipToRotate;

    /// <summary>지금 뱃머리 각도. 0 이 정면, 음수가 좌, 양수가 우.</summary>
    public float Heading { get; private set; }

    /// <summary>뱃머리 각도를 -1 ~ +1 로 바꾼 값. HUD 와 사건 판정이 쓴다.</summary>
    public float Heading01 => maxHeading <= 0f ? 0f : Mathf.Clamp(Heading / maxHeading, -1f, 1f);

    /// <summary>지금 들어오는 조타 입력. -1(좌) ~ +1(우). 아무도 없으면 0.</summary>
    public float Steer { get; private set; }

    /// <summary>
    /// 바깥에서 뱃머리를 미는 힘 (도/초). 양수면 우현으로 밀린다. 0 이면 없다.
    ///
    /// **파도가 이 값을 켭니다.** 파도가 치는 동안 뱃머리가 계속 한쪽으로 밀리고,
    /// 사람이 조타에 붙어 반대로 꺾어야 정면이 유지됩니다.
    ///
    /// 이게 없으면 파도는 아무 일도 아닙니다. 자리가 비면 조타륜이 저절로 정면으로
    /// 돌아오기 때문에(<see cref="recenterSpeed"/>), **아무도 안 가도 파도가 넘어갑니다.**
    /// 돌풍이 돛을 계속 푸는 것과 같은 구조로, 파도는 조타에 사람을 묶습니다. (4장)
    /// </summary>
    public float ExternalPushPerSecond { get; set; }

    /// <summary>지금 바깥에서 밀리고 있는지</summary>
    public bool IsPushed => !Mathf.Approximately(ExternalPushPerSecond, 0f);

    /// <summary>누군가 조타를 잡고 있는지</summary>
    public bool IsManned => !IsEmpty;

    /// <summary>정면을 향하고 있는지. 파도를 정면으로 받았는지 판정할 때 쓴다.</summary>
    public bool IsHeadingStraight(float toleranceDegrees = 10f)
    {
        return Mathf.Abs(Heading) <= toleranceDegrees;
    }

    private float _baseYaw;

    private void Awake()
    {
        if (shipToRotate != null)
        {
            _baseYaw = shipToRotate.eulerAngles.y;
        }
    }

    /// <summary>누군가 붙어 있는 동안. 조타 입력만큼 뱃머리를 돌린다.</summary>
    protected override void Work(float deltaTime)
    {
        // 붙어 있는 사람 전원의 조타를 합친다.
        // 파도에 조타륜을 둘이 붙잡는 협력 작업(6장)이 붙으면 두 명분이 여기로 들어온다.
        float steer = 0f;
        for (int i = 0; i < Workers.Count; i++)
        {
            steer += ShipCoopInput.Steer(Workers[i].Input);
        }

        Steer = Mathf.Clamp(steer, -1f, 1f);
        Turn(Steer * turnSpeed * deltaTime);

        // 밀리는 힘은 사람이 붙어 있어도 계속 작용한다. 그래야 붙잡는 것이 일이 된다.
        // 조타 속도(35)가 미는 속도(20)보다 커서, 붙어 있으면 이기고 놓으면 진다.
        ApplyExternalPush(deltaTime);
    }

    /// <summary>아무도 없는 동안. 방치되거나, 설정해두면 정면으로 돌아온다.</summary>
    protected override void Idle(float deltaTime)
    {
        Steer = 0f;

        ApplyExternalPush(deltaTime);

        // 밀리는 동안에는 저절로 돌아오지 않는다. 자동 복귀가 살아 있으면
        // 미는 힘과 상쇄되어 아무도 안 가도 정면이 유지된다. 그러면 사건이 사라진다.
        if (IsPushed || recenterSpeed <= 0f)
        {
            return;
        }

        float step = recenterSpeed * deltaTime;
        Turn(Mathf.Clamp(-Heading, -step, step));
    }

    private void ApplyExternalPush(float deltaTime)
    {
        if (!IsPushed)
        {
            return;
        }

        Turn(ExternalPushPerSecond * deltaTime);
    }

    private void Turn(float degrees)
    {
        if (Mathf.Approximately(degrees, 0f))
        {
            return;
        }

        Heading = Mathf.Clamp(Heading + degrees, -maxHeading, maxHeading);

        if (shipToRotate != null)
        {
            shipToRotate.rotation = Quaternion.Euler(0f, _baseYaw + Heading, 0f);
        }
    }
}
