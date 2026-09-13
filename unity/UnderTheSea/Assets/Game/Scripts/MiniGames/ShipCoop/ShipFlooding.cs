using UnityEngine;

/// <summary>
/// 🪣 침수. 배에 찬 물의 양. (SHIPCOOP.md 4장)
///
/// **파손을 막아도 이미 들어온 물은 남습니다.** 남은 물은 배 HP 를 계속 깎습니다.
///
/// <code>
/// 파손 발생 → 수리로 구멍을 막는다 → 그래도 물은 차 있다 → 퍼내야 한다
/// </code>
///
/// **사건 하나가 일 두 개를 만듭니다.** 손님을 받으면 설거지가 생기는 것과 같습니다.
///
/// 침수는 **속도가 아니라 HP 를 깎습니다.**
///
/// 처음에는 물이 찬 배를 느려지게 두어 시간으로 갚게 했는데, 플레이해보니
/// **아무도 못 느꼈습니다.** 속도가 준 것을 알려면 진행도 바를 계속 쳐다봐야 하는데
/// 물이 찼을 때는 그럴 여유가 없습니다. 줄어드는 것이 바로 보이는 값이어야
/// 벌칙이 됩니다. 그래서 속도 감소는 들어냈습니다. (2장)
///
/// 그 결과 다섯 작업이 두 실패 조건에 이렇게 갈립니다.
///
///   침몰 (HP 0)   조타 · 대포 · 수리 · 배수
///   시간 초과      돛
///
/// 스스로 Update 하지 않습니다. ShipCoopGame 이 항해 중일 때만 Tick 을 불러줍니다.
/// 시작 전이나 끝난 뒤에 물이 계속 차면 안 되기 때문입니다. (ShipVoyage 와 같은 이유)
/// </summary>
public class ShipFlooding : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private ShipHealth health;

    [Header("물이 차 있는 동안")]
    [Tooltip("물이 가득 찼을 때 초당 이만큼 배 HP 가 깎인다. 물이 절반이면 절반만 깎인다.\n\n" +
             "속도가 주는 것만으로는 아무도 못 느낀다. 진행도 바를 계속 쳐다봐야 알 수 있는데,\n" +
             "물이 찼을 때는 그럴 여유가 없다. HP 는 줄어드는 것이 바로 보인다.\n\n" +
             "찬 양에 비례시키는 이유는 **양동이 한 번이 바로 보상이 되게** 하기 위해서다.\n" +
             "다 퍼내야만 멈추면 마지막 한 통 전까지는 퍼내도 달라지는 게 없다.")]
    [SerializeField, Min(0f)] private float damagePerSecondWhenFull = 6f;

    [Header("차오르는 속도")]
    [Tooltip("막지 못한 파손 지점 하나당 초당 이만큼 찬다.\n" +
             "파손이 둘이면 두 배로 찬다. 방치할수록 HP 가 빨리 깎인다.")]
    [SerializeField, Min(0f)] private float risePerPointPerSecond = 0.025f;

    [Header("퍼냈을 때")]
    [Tooltip("양동이 하나를 뱃전에 비우면 이만큼 줄어든다.")]
    [SerializeField, Range(0f, 1f)] private float dumpAmount = 0.2f;

    /// <summary>지금 찬 물의 양. 0 ~ 1</summary>
    public float Level01 { get; private set; }

    /// <summary>퍼낼 물이 있는지. 없으면 양동이를 들어도 소용이 없다.</summary>
    public bool HasWater => Level01 > 0.01f;

    /// <summary>지금 초당 깎이는 HP. HUD 가 이 숫자를 그대로 띄운다.</summary>
    public float DamagePerSecond => damagePerSecondWhenFull * Level01;

    /// <summary>
    /// 아직 막지 못한 파손 지점의 수. **0 이 아니면 퍼내도 헛수고다.**
    ///
    /// 구멍 하나가 초당 2.5% 를 붓는데 양동이 왕복이 4초입니다. 그동안 10% 가 다시 찹니다.
    /// 그래서 구멍을 열어둔 채로 퍼내면 물이 0 으로 안 내려가고 제자리를 돕니다.
    ///
    /// 침수 게이지만 보고 있으면 양동이로 손이 가는 것이 당연합니다.
    /// **그래서 이 숫자를 HUD 가 읽어가 "수리가 먼저다" 를 말해줍니다.** (9장)
    /// </summary>
    public int LeakingPoints { get; private set; }

    /// <summary>지금 퍼내는 것이 의미가 있는지. 구멍이 열려 있으면 거짓이다.</summary>
    public bool BailingHelps => HasWater && LeakingPoints == 0;

    private void Awake()
    {
        if (health == null)
        {
            health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        }

        if (health == null)
        {
            Debug.LogWarning($"[{name}] ShipHealth 를 찾지 못했습니다. 물이 차도 HP 가 깎이지 않습니다.", this);
        }
    }

    /// <summary>ShipCoopGame 이 항해 중일 때만 불러준다.</summary>
    public void Tick(float deltaTime)
    {
        LeakingPoints = CountLeakingPoints();

        if (LeakingPoints > 0)
        {
            Level01 = Mathf.Clamp01(Level01 + risePerPointPerSecond * LeakingPoints * deltaTime);
        }

        // 물이 남아 있는 동안 계속 깎인다. 다 퍼내야 멈춘다.
        // reason 을 매 프레임 넘기면 콘솔이 도배된다. 원인은 HUD 의 침수 게이지가 말해준다.
        if (health != null && Level01 > 0f)
        {
            health.TakeDamage(DamagePerSecond * deltaTime);
        }
    }

    /// <summary>
    /// 한 번에 물이 쏟아졌다. 실제로 늘었으면 true.
    ///
    /// **파도를 옆으로 맞으면 여기로 옵니다.** 파도의 실패 대가는 배 HP 가 아니라
    /// 갑판에 쏟아진 물입니다. HP 는 깎이고 끝이지만 물은 **누군가 퍼내야 할 일**로
    /// 남습니다. 실패의 대가는 일거리여야 합니다. (5장)
    ///
    /// 이걸로 침수의 원인이 둘이 됩니다.
    /// <code>
    /// 파손을 방치한다        → 천천히 찬다  (초당 2.5%)
    /// 파도를 옆으로 맞는다   → 한 번에 찬다
    /// </code>
    /// </summary>
    public bool Add(float amount)
    {
        if (amount <= 0f)
        {
            return false;
        }

        float before = Level01;
        Level01 = Mathf.Clamp01(Level01 + amount);

        if (Mathf.Approximately(before, Level01))
        {
            return false;
        }

        Debug.Log($"[침수] 물이 쏟아졌다. {before:P0} → {Level01:P0}", this);
        return true;
    }

    /// <summary>양동이 하나를 뱃전에 비웠다. 실제로 줄어들었으면 true.</summary>
    public bool Dump()
    {
        if (!HasWater)
        {
            return false;
        }

        float before = Level01;
        Level01 = Mathf.Clamp01(Level01 - dumpAmount);

        Debug.Log($"[침수] 물을 버렸다. {before:P0} → {Level01:P0}", this);
        return true;
    }

    /// <summary>처음부터 다시 시작한다.</summary>
    public void ResetFlooding()
    {
        Level01 = 0f;
    }

    /// <summary>아직 막지 못한 파손 지점의 수</summary>
    private static int CountLeakingPoints()
    {
        int leaking = 0;

        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is RepairTask repair && !repair.IsRepaired)
            {
                leaking++;
            }
        }

        return leaking;
    }

    /// <summary>HUD 문구</summary>
    public string FloodHint()
    {
        if (!HasWater)
        {
            return "물 없음";
        }

        if (LeakingPoints > 0)
        {
            return $"물이 {Level01:P0} 찼다 — 초당 -{DamagePerSecond:F1} HP. " +
                   $"구멍 {LeakingPoints}개가 새는 중 — 퍼내도 다시 찬다";
        }

        return $"물이 {Level01:P0} 찼다 — 초당 -{DamagePerSecond:F1} HP. 양동이로 퍼내라";
    }
}
