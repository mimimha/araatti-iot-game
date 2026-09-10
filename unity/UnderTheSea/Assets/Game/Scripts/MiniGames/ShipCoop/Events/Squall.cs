using UnityEngine;

/// <summary>
/// 🌪️ 돌풍 · 폭풍. 돛을 조절해 속도를 유지한다. (SHIPCOOP.md 5장)
///
/// 돌풍이 부는 동안 돛이 계속 풀립니다. 누군가 돛에 붙어 당기고 있지 않으면
/// 속도가 바닥으로 떨어지고, 그만큼 진행도를 잃습니다.
///
/// **이 사건의 대가는 배 HP 가 아니라 시간입니다.** 잃은 진행도는 돌아오지 않고,
/// 그것이 그대로 시간 초과 위험이 됩니다. 그래서 damageOnFail 은 0 으로 두는 것을 권합니다.
///
/// 실패하면 속도가 죽은 채로 남고, 적선이 따라붙습니다.
/// 연쇄 목록에 EnemyShip 을 넣으면
/// "돛을 놓침 → 속도 급락 → 적선이 따라붙음 → 포격당함" 이 만들어집니다.
/// </summary>
public class Squall : VoyageEvent
{
    [Header("돌풍의 세기")]
    [Tooltip("돌풍이 부는 동안 돛 힘이 초당 이만큼 추가로 풀린다.\n" +
             "돛의 당기는 속도(기본 0.5)보다 작아야 사람이 붙으면 버틸 수 있다.")]
    [SerializeField, Min(0f)] private float extraSlackPerSecond = 0.3f;

    [Header("버티는 기준")]
    [Tooltip("돛 힘이 이 아래로 떨어지면 속도를 놓친 것으로 본다.")]
    [SerializeField, Range(0f, 1f)] private float minSailPower = 0.3f;

    [Tooltip("기준 아래에 머물러도 되는 시간 (초). 이만큼 넘으면 실패한다.")]
    [SerializeField, Min(0f)] private float allowedWeakTime = 3f;

    /// <summary>기준 아래에 머문 시간 (초)</summary>
    public float WeakTime { get; private set; }

    /// <summary>지금 속도를 지키고 있는지</summary>
    public bool IsHolding { get; private set; }

    /// <summary>버틴 정도. HUD 게이지가 이걸 본다.</summary>
    public float WeakTime01 =>
        allowedWeakTime <= 0f ? (IsHolding ? 0f : 1f) : Mathf.Clamp01(WeakTime / allowedWeakTime);

    private ShipVoyage _voyage;

    protected override void Awake()
    {
        base.Awake();
        _voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 돌풍이 터졌다. **여기서 해류를 뒤집는다.**
    ///
    /// 평소에는 아무도 돛을 안 잡아도 해류가 최저 속도만큼 밀어줍니다.
    /// 돌풍 중에는 그 해류가 반대로 붑니다. 돛을 잡고 있으면 느리게라도 앞으로 가지만
    /// **손을 놓는 순간 뒤로 밀립니다.**
    ///
    /// 예고 중에는 뒤집지 않습니다. 아직 안 터졌기 때문입니다.
    /// </summary>
    protected override void OnBegin()
    {
        WeakTime = 0f;
        IsHolding = true;

        if (_voyage == null)
        {
            Debug.LogWarning($"[{name}] ShipVoyage 를 찾지 못했습니다. 돌풍이 돛을 흔들지 않습니다.", this);
            return;
        }

        _voyage.CurrentReversed = true;
        Debug.Log($"[{name}] 해류가 뒤집혔다. 돛을 놓으면 뒤로 밀린다.", this);
    }

    /// <summary>
    /// 돌풍이 끝났다. 넘겼든 못 넘겼든 해류는 되돌린다.
    ///
    /// Stop() 이 반드시 지나가는 자리라서 여기 둔다.
    /// OnSucceed / OnFail 에 나눠 두면 취소되거나 예고 중에 끊겼을 때
    /// **해류가 뒤집힌 채로 남는다.**
    /// </summary>
    protected override void OnHide()
    {
        if (_voyage != null)
        {
            _voyage.CurrentReversed = false;
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (_voyage == null)
        {
            return;
        }

        // 돌풍이 돛을 계속 풀어놓는다. 사람이 붙어 당겨야 버틴다.
        if (extraSlackPerSecond > 0f)
        {
            _voyage.SailPower01 = Mathf.Clamp01(_voyage.SailPower01 - extraSlackPerSecond * deltaTime);
        }

        IsHolding = _voyage.SailPower01 >= minSailPower;

        if (IsHolding)
        {
            return;
        }

        WeakTime += deltaTime;

        if (WeakTime > allowedWeakTime)
        {
            Fail();
        }
    }

    /// <summary>돌풍이 지나갔다. 속도를 지켰으면 넘긴 것이다.</summary>
    protected override void OnTimeout()
    {
        Succeed();
    }

    /// <summary>
    /// 사건 알림 아래에 붙는 안내.
    ///
    /// 예고 중에는 아직 돛이 안 풀리므로 "가라" 고만 합니다.
    /// 터진 뒤에는 해류가 뒤집혀 있어서, 놓으면 앞으로 못 가는 정도가 아니라
    /// **뒤로 밀립니다.** 그 말을 해줘야 왜 급한지 압니다.
    /// </summary>
    public override string LiveHint()
    {
        float power = _voyage != null ? _voyage.SailPower01 : 0f;

        if (IsWarning)
        {
            return "돛으로 가라 — 곧 뒤로 밀린다";
        }

        return IsHolding
            ? $"D 로 계속 당겨라  (돛 {power:P0})"
            : $"돛이 풀렸다! 뒤로 밀린다  ({power:P0} / 기준 {minSailPower:P0}, {WeakTime:F1}/{allowedWeakTime:F1}초)";
    }

    /// <summary>예전 이름. 디버그 오버레이가 쓰던 것이다.</summary>
    public string SailHint() => LiveHint();
}
