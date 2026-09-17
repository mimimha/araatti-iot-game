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

    [Header("🤝 협력 — 밀리는 동안")]
    [Tooltip("파도가 뱃머리를 미는 동안에는 정원이 이만큼으로 늘어난다.\n\n" +
             "파도가 미는 힘(45°/초)이 한 사람의 회전(35°/초)보다 세서, **혼자서는 집니다.**\n" +
             "둘이 붙어야 70°/초가 되어 이깁니다. 이것이 6장의 협력 작업입니다.\n\n" +
             "1 로 두면 협력이 꺼지고 예전처럼 혼자 버티게 된다.")]
    [SerializeField, Range(1, 4)] private int pushedCapacity = 2;

    /// <summary>
    /// 밀리는 동안에는 정원이 늘어난다. 평소에는 인스펙터 값 그대로다.
    ///
    /// **자리를 늘려주지 않으면 두 번째 사람이 아예 붙을 수가 없습니다.**
    /// 상호작용 아이콘이 회색으로 뜨고, 도와주러 와도 할 수 있는 것이 없습니다.
    /// </summary>
    public override int Capacity => IsPushed ? pushedCapacity : BaseCapacity;

    /// <summary>지금 혼자 버티고 있는지. 한 명 더 와야 한다. HUD 가 이걸 읽는다.</summary>
    public bool NeedsHelp => IsPushed && Workers.Count < pushedCapacity;

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
    /// **뱃머리 각도를 밖에서 정해 준다.** 네트워크에서 서버가 정한 결과를 화면에 옮길 때 쓴다.
    ///
    /// 클라이언트의 <see cref="TaskBase.Update"/> 는 권위 가드로 막혀 <see cref="Work"/> 가 돌지 않는다.
    /// 그대로 두면 조타 게이지 · 조타륜 · 배의 기울기가 클라이언트 화면에서 0 에 멈춰 있다.
    /// <c>ShipCoopStateSync</c> 가 서버 값을 받아 이 함수로 넣는다.
    /// </summary>
    public void ShowHeading(float heading, float steer)
    {
        Heading = Mathf.Clamp(heading, -maxHeading, maxHeading);
        Steer = steer;

        if (shipToRotate != null)
        {
            shipToRotate.rotation = Quaternion.Euler(0f, _baseYaw + Heading, 0f);
        }
    }

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

    /// <summary>
    /// **뱃머리를 똑바로 되돌린다.** 판을 치울 때 부른다.
    ///
    /// <see cref="Heading"/> 은 씬에 고정된 키의 상태라 판이 끝나도 사라지지 않는다. 그래서
    /// 지난 판이 키를 꺾은 채 끝나면 <b>다음 판이 그 각도에서 시작한다.</b> 서버가 그 값을
    /// 그대로 복제하므로 갓 들어온 사람의 화면에서도 배가 시작하자마자 옆으로 쏠린다.
    ///
    /// <c>VoyageSea</c> 가 옆으로 밀리는 양을 <c>SmoothDamp</c> 로 따라가게 해 둔 탓에
    /// 그 쏠림이 한 번에 나타나지 않고 <b>출렁이듯</b> 보인다. 대포(<c>ResetCannon</c>)와
    /// 같은 이유, 같은 처방이다.
    /// </summary>
    public void ResetHelm()
    {
        Heading = 0f;
        Steer = 0f;

        // 사건이 밀어붙이던 힘도 지운다. 사건은 ClearBoard 가 이미 껐지만,
        // 끄는 순서에 기대지 않는 편이 안전하다.
        ExternalPushPerSecond = 0f;

        if (shipToRotate != null)
        {
            shipToRotate.rotation = Quaternion.Euler(0f, _baseYaw, 0f);
        }
    }

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
        // 붙어 있는 사람 전원의 조타를 **합친다.** 여기서 1 로 자르지 않는 것이 핵심이다.
        // 둘이 붙으면 두 배로 돌아가야 파도를 이길 수 있다. (SHIPCOOP.md 6장)
        float steer = 0f;
        for (int i = 0; i < Workers.Count; i++)
        {
            steer += ShipCoopInput.Steer(Workers[i].Input);
        }

        // 정원만큼까지만 인정한다. 한 사람이 두 사람 몫을 낼 수는 없다.
        float force = Mathf.Clamp(steer, -Capacity, Capacity);

        // 표시용 값은 늘 -1 ~ +1 로 맞춘다. 정원이 2 면 둘 다 끝까지 꺾었을 때 1 이다.
        Steer = force / Mathf.Max(1, Capacity);

        Turn(force * turnSpeed * deltaTime);

        // 밀리는 힘은 사람이 붙어 있어도 계속 작용한다. 그래야 붙잡는 것이 일이 된다.
        //
        //   아무도 없음   45°/초 로 밀린다            → 3.9초 뒤 실패
        //   혼자          45 − 35 = 10°/초 로 밀린다  → 5.5초 뒤 실패. 시간은 벌지만 진다
        //   둘이서        70 − 45 = 25°/초 로 되돌린다 → 버틴다
        //
        // 혼자서도 시간을 버는 것이 중요하다. 그 사이에 🆘 를 누르고 둘째가 온다.
        // 혼자가 아무 소용이 없으면 첫 번째 사람이 갈 이유가 없어진다.
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
