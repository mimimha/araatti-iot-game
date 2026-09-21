using UnityEngine;

/// <summary>
/// 🌬️ 돛. 배의 속도를 정한다. (SHIPCOOP.md 4장)
///
/// **속도가 곧 진행도이므로 돛이 시간 초과에 직결됩니다.**
/// 비우면 해류 최저 속도로만 나아가고, 그 속도로는 제한시간 안에 도착하지 못합니다.
/// 이것이 "돛 비었어!" 가 나오는 지점입니다.
///
/// 밧줄을 당기는 방식입니다. 당기면 힘이 올라가고, 비우면 서서히 풀립니다.
/// 한 번 올려놓고 떠나면 끝나는 것이 아니라 누군가 계속 봐줘야 합니다.
///
/// 입력
///   당기기 / 풀기 → ShipCoopInput.SailPull. 키보드는 D 로 당기고 A 로 푼다.
///
/// 강풍에 돛이 찢어질 때 2명이 밧줄을 함께 당기는 협력 작업(6장)은
/// MVP 범위가 아닙니다. 붙은 사람 전원의 입력을 합치므로 정원만 늘리면 얹힙니다.
/// </summary>
public class SailTask : TaskBase
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipVoyage voyage;

    [Header("당기기")]
    [Tooltip("끝까지 당겼을 때 돛 힘이 초당 이만큼 오른다. 0 에서 최대까지 약 2초.")]
    [SerializeField, Min(0.05f)] private float pullSpeed = 0.5f;

    [Header("비웠을 때")]
    [Tooltip("아무도 없으면 돛 힘이 초당 이만큼 풀린다. 최대에서 0 까지 약 3초.\n" +
             "0 으로 두면 한 번 올려놓고 떠나도 되므로 돛이 할 일이 없어진다.")]
    [SerializeField, Min(0f)] private float slackSpeed = 0.35f;

    [Header("붙어 있는 동안")]
    [Tooltip("자리에 있지만 입력이 없을 때 초당 풀리는 양.\n" +
             "0 이면 잡고만 있어도 유지된다. 조작 부담을 늘리려면 올린다.")]
    [SerializeField, Min(0f)] private float idleSlackWhileManned = 0f;

    /// <summary>지금 돛 힘. 0 ~ 1. 항해 속도가 이 값을 따른다.</summary>
    public float SailPower01 => voyage != null ? Mathf.Clamp01(voyage.SailPower01) : 0f;

    /// <summary>지금 들어오는 당기기 입력. -1(풀기) ~ +1(당기기). 아무도 없으면 0.</summary>
    public float Pull { get; private set; }

    /// <summary>
    /// **당기기 입력을 밖에서 정해 준다.** 네트워크에서 서버가 정한 값을 화면에 옮길 때 쓴다.
    ///
    /// 판정(<see cref="Work"/>)은 서버에서만 돈다. 클라이언트는 이 값을 복제받아 여기로 넣는다.
    /// 그래야 밧줄을 당기는 손 동작(<c>ShipCoopStationPose</c>)이 남의 화면에서도 같이 움직인다.
    /// (ShipCoopStateSync — 돛 힘 자체는 SailPower01 로 따로 간다)
    /// </summary>
    public void ShowPull(float pull)
    {
        Pull = Mathf.Clamp(pull, -1f, 1f);
    }

    /// <summary>누군가 돛을 잡고 있는지</summary>
    public bool IsManned => !IsEmpty;

    /// <summary>돛이 풀려서 최저 속도로만 가고 있는지. HUD 경고가 이걸 본다.</summary>
    public bool IsSlack => SailPower01 <= 0.01f;

    private void Awake()
    {
        if (voyage == null)
        {
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        }

        if (voyage == null)
        {
            Debug.LogError(
                $"[{name}] ShipVoyage 를 찾지 못했습니다. 돛을 당겨도 속도가 바뀌지 않습니다.", this);
        }
    }

    protected override void Work(float deltaTime)
    {
        if (voyage == null)
        {
            return;
        }

        // 붙어 있는 사람 전원의 당기기를 합친다.
        // 강풍에 둘이 함께 당기는 협력 작업(6장)이 정원만 늘리면 그대로 얹힌다.
        float pull = 0f;
        for (int i = 0; i < Workers.Count; i++)
        {
            pull += ShipCoopInput.SailPull(Workers[i].Input);
        }

        Pull = Mathf.Clamp(pull, -1f, 1f);

        // 당기면 오르고 풀면 내린다.
        float change = Pull * pullSpeed * deltaTime;

        // 당기지 않는 동안에는 조금씩 풀린다. 기본 0 이라 잡고만 있어도 유지된다.
        if (Pull <= 0f)
        {
            change -= idleSlackWhileManned * deltaTime;
        }

        Apply(change);
    }

    protected override void Idle(float deltaTime)
    {
        Pull = 0f;

        if (voyage == null || slackSpeed <= 0f)
        {
            return;
        }

        Apply(-slackSpeed * deltaTime);
    }

    private void Apply(float change)
    {
        if (Mathf.Approximately(change, 0f))
        {
            return;
        }

        voyage.SailPower01 = Mathf.Clamp01(voyage.SailPower01 + change);
    }
}
