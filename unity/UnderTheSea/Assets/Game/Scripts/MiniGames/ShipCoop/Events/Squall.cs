using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 🌪️ 돌풍 · 폭풍. **돛을 접어서** 버틴다. (SHIPCOOP.md 5장)
///
/// <code>
/// 평소     돛을 펴야 빠르다
/// 돌풍     펴 놓으면 배가 **뒤로** 간다  →  접어야 한다  →  접으면 평소 최저 속도로 앞으로
/// 끝나면   바로 다시 펴야 빠르다
/// </code>
///
/// 돌풍이 부는 동안 바람이 돛을 **계속 펴 놓습니다.** 누군가 돛에 붙어 J 로 풀고 있지
/// 않으면 돛이 도로 펴지고, 펴진 만큼 배가 뒤로 밀립니다. (<see cref="ShipVoyage.Speed"/>)
///
/// <b>실패 판정이 없습니다.</b> 벌칙이 속도식 안에 들어 있어서 따로 셀 것이 없습니다.
/// 펴 둔 채로 두면 그 시간만큼 뒤로 간 것이 그대로 대가입니다. 뒤로 밀리면 섬이 멀어지고
/// 항로선이 거꾸로 흐르니 게이지를 안 봐도 보입니다.
///
/// 한때는 "돛을 접지 못하면 3초 뒤 실패, HP 벌칙" 이었는데 값이 안 맞아 **붙어 있어도
/// 실패**했습니다 (돛 100% 에서 30% 까지 초당 0.2 씩이라 3.5초, 허용은 3초). 그리고
/// 그 전에는 반대로 "당겨서 버티라" 였습니다. 실제 배는 돌풍에 돛을 접습니다.
/// </summary>
public class Squall : VoyageEvent
{
    [Header("돌풍의 세기")]
    [Tooltip("돌풍이 부는 동안 바람이 돛을 초당 이만큼 펴 놓는다.\n" +
             "돛의 푸는 속도(기본 0.5)보다 작아야 사람이 붙으면 접을 수 있다.")]
    [FormerlySerializedAs("extraSlackPerSecond")]
    [SerializeField, Min(0f)] private float gustFillPerSecond = 0.3f;

    [Header("안내 기준")]
    [Tooltip("돛 힘이 이 아래면 '접었다' 고 말해 준다. 판정이 아니라 안내 문구용이다.")]
    [FormerlySerializedAs("minSailPower")]
    [SerializeField, Range(0f, 1f)] private float furledBelow = 0.3f;

    /// <summary>지금 돛을 접어 두고 있는지. 안내 문구가 본다.</summary>
    public bool IsFurled { get; private set; }

    private ShipVoyage _voyage;

    protected override void Awake()
    {
        base.Awake();
        _voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 돌풍이 터졌다. **여기서 돛이 바람을 거꾸로 받게 한다.**
    ///
    /// 예고 중에는 켜지 않습니다. 아직 안 터졌기 때문입니다.
    /// </summary>
    protected override void OnBegin()
    {
        IsFurled = false;

        if (_voyage == null)
        {
            Debug.LogWarning($"[{name}] ShipVoyage 를 찾지 못했습니다. 돌풍이 돛을 흔들지 않습니다.", this);
            return;
        }

        _voyage.SquallBlowing = true;
        Debug.Log($"[{name}] 돌풍이 분다. 돛을 펴 두면 뒤로 밀린다.", this);
    }

    /// <summary>
    /// 돌풍이 끝났다. 어떻게 끝났든 바람은 되돌린다.
    ///
    /// Stop() 이 반드시 지나가는 자리라서 여기 둔다.
    /// OnSucceed / OnFail 에 나눠 두면 취소되거나 예고 중에 끊겼을 때
    /// **바람이 거꾸로 부는 채로 남는다.**
    /// </summary>
    protected override void OnHide()
    {
        if (_voyage != null)
        {
            _voyage.SquallBlowing = false;
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (_voyage == null)
        {
            return;
        }

        // 바람이 돛을 계속 펴 놓는다. 사람이 붙어 풀어야 접힌다.
        if (gustFillPerSecond > 0f)
        {
            _voyage.SailPower01 = Mathf.Clamp01(_voyage.SailPower01 + gustFillPerSecond * deltaTime);
        }

        IsFurled = _voyage.SailPower01 <= furledBelow;
    }

    /// <summary>돌풍이 지나갔다. 실패가 없는 사건이라 늘 넘긴 것으로 친다.</summary>
    protected override void OnTimeout()
    {
        Succeed();
    }

    /// <summary>
    /// 사건 알림 아래에 붙는 안내.
    ///
    /// 키는 **J(풀기)** 입니다. 배 협동 키보드는 J 가 풀기, L 이 당기기입니다.
    /// (KeyboardPlayerController — 검 게임만 A/D 를 쓴다)
    /// </summary>
    public override string LiveHint()
    {
        float power = _voyage != null ? _voyage.SailPower01 : 0f;

        if (IsWarning)
        {
            return "돛으로 가라 — 접어야 한다";
        }

        return IsFurled
            ? $"J 로 계속 접어 둬라  (돛 {power:P0})"
            : $"돛이 펴져 있다! 뒤로 밀린다 — J 로 접어라  (돛 {power:P0})";
    }

    /// <summary>발생 중에도 띄운다. "지금 접지 않으면 뒤로 간다" 는 비상 신호다.</summary>
    public override bool HintIsUrgent => !IsFurled;

    /// <summary>예전 이름. 디버그 오버레이가 쓰던 것이다.</summary>
    public string SailHint() => LiveHint();
}
