using System;
using UnityEngine;

/// <summary>
/// 🔨 수리. 파손 · 침수된 자리를 망치로 두드려 막는다.
///
/// 다른 작업과 달리 **자리가 고정되어 있지 않습니다.**
/// 파손이 생길 때마다 그 지점에 하나씩 생깁니다. (SHIPCOOP.md 4장)
/// 지점 하나에 붙을 수 있는 사람은 1명이고, 지점은 여러 개가 동시에 생깁니다.
///
/// 방치하면 침수가 쌓여 배 HP 가 계속 깎입니다. 이것이 "수리할 사람이 빠지면
/// 배가 서서히 가라앉는" 압박을 만듭니다.
///
/// 입력
///   내리치기 → ShipCoopInput.ConsumeSwing. 키보드는 F 연타.
///   **횟수는 IoT 와 같아야 합니다.** (7장 — IoT 가 벌칙이 되면 안 됩니다)
///
/// 수리가 끝나면 스스로 꺼집니다. 다시 쓰려면 새로 만드는 쪽(HullDamage)이 만듭니다.
/// </summary>
public class RepairTask : TaskBase
{
    [Header("자재")]
    [Tooltip("켜면 누군가 자재(판자)를 날라 와야 망치질을 시작할 수 있다.\n\n" +
             "수리가 '붙어서 F 연타' 한 단계였던 것이 '집기 → 운반 → 망치질' 세 단계가 된다.\n" +
             "혼자서도 되지만 둘이면 훨씬 빠르다. 운반이 게임의 동사가 되는 자리다. (4장)")]
    [SerializeField] private bool needsPlank = true;

    [Header("수리")]
    [Tooltip("이만큼 내리치면 수리가 끝난다")]
    [SerializeField, Min(1)] private int hitsToRepair = 5;

    [Header("방치했을 때")]
    [Tooltip("아무도 붙어 있지 않은 동안 초당 이만큼 배 HP 가 깎인다. (4장)")]
    [SerializeField, Min(0f)] private float leakDamagePerSecond = 2f;

    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipHealth health;

    [Tooltip("점수 집계용. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipCoopGame game;

    /// <summary>지금까지 내리친 횟수</summary>
    public int Hits { get; private set; }

    /// <summary>수리 진행도 0 ~ 1. HUD 의 게이지가 이 값을 본다.</summary>
    public float Progress01 => Mathf.Clamp01((float)Hits / hitsToRepair);

    /// <summary>수리가 끝났는지</summary>
    public bool IsRepaired { get; private set; }

    /// <summary>이 지점의 수리가 끝났다. 만든 쪽이 이걸 듣고 정리한다.</summary>
    public event Action<RepairTask> Repaired;

    private void Awake()
    {
        if (health == null)
        {
            health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        }

        if (game == null)
        {
            game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        }

        if (health == null)
        {
            Debug.LogWarning(
                $"[{name}] ShipHealth 를 찾지 못했습니다. 방치해도 침수 피해가 들어가지 않습니다.", this);
        }
    }

    /// <summary>자재가 도착했는지. 자재가 필요 없는 지점이면 항상 참.</summary>
    public bool HasPlank { get; private set; }

    /// <summary>지금 망치질이 먹는지. 자재를 기다리는 동안에는 안 먹는다.</summary>
    public bool CanHammer => !needsPlank || HasPlank;

    /// <summary>자재를 날라 왔다. 운반하는 쪽이 부른다. 받았으면 true.</summary>
    public bool DeliverPlank()
    {
        if (IsRepaired || HasPlank)
        {
            return false;
        }

        HasPlank = true;
        Debug.Log($"[{name}] 자재가 도착했다. 이제 망치질이 먹는다.", this);
        return true;
    }

    /// <summary>자재를 기다리고 있는지. 운반하는 쪽이 어디로 갈지 이걸 보고 정한다.</summary>
    public bool WantsPlank => needsPlank && !HasPlank && !IsRepaired;

    /// <summary>누군가 붙어 있는 동안. 내리친 횟수를 센다.</summary>
    protected override void Work(float deltaTime)
    {
        if (IsRepaired)
        {
            return;
        }

        // 자재가 없으면 두드려도 소용이 없다. 누가 날라 와야 한다.
        // 입력은 소비하지 않는다. 여기서 삼키면 자재가 온 뒤에 한 번 놓친다.
        if (!CanHammer)
        {
            return;
        }

        // 정원이 1명이라 보통 한 명이지만, 규격이 바뀌어도 그대로 동작하게 전원을 센다.
        for (int i = 0; i < Workers.Count; i++)
        {
            if (ShipCoopInput.ConsumeSwing(Workers[i].Input))
            {
                Hits++;
            }
        }

        if (Hits >= hitsToRepair)
        {
            Complete();
        }
    }

    /// <summary>아무도 없는 동안. 침수가 쌓여 배가 깎인다.</summary>
    protected override void Idle(float deltaTime)
    {
        if (IsRepaired || leakDamagePerSecond <= 0f || health == null)
        {
            return;
        }

        // reason 을 매 프레임 넘기면 콘솔이 도배된다. 원인은 이름으로 알 수 있다.
        health.TakeDamage(leakDamagePerSecond * deltaTime);
    }

    /// <summary>
    /// 수리가 끝난 구멍을 **치우는 방법**. 비어 있으면 예전처럼 스스로 꺼진다.
    ///
    /// 네트워크에서는 서버가 <c>Runner.Despawn</c> 하도록 갈아끼운다.
    /// 스스로 꺼지면 그 화면에서만 사라지고 남의 화면에는 구멍이 남는다.
    /// </summary>
    public static Action<RepairTask> Remover;

    /// <summary>
    /// **수리 상태를 밖에서 정해 준다.** 서버가 정한 값을 화면에 옮길 때만 쓴다.
    ///
    /// 완료 판정은 하지 않는다. 다 고쳤는지는 서버가 정하고, 다 고쳐진 구멍은
    /// 서버가 치운다. 여기서 <c>Complete</c> 를 부르면 클라이언트가 혼자
    /// "다 고쳤다" 고 판단하게 되어 남의 화면과 어긋난다.
    /// </summary>
    public void ShowRepair(int hits, bool hasPlank, bool repaired)
    {
        Hits = hits;
        HasPlank = hasPlank;
        IsRepaired = repaired;
    }

    private void Complete()
    {
        IsRepaired = true;

        if (game != null)
        {
            game.ReportLeakSealed();
        }

        Debug.Log($"[{name}] 수리 완료 ({Hits}회)", this);

        Repaired?.Invoke(this);

        // 스스로 꺼진다. TaskBase.OnDisable 이 붙어 있던 사람을 놓아주고 목록에서도 빠진다.
        // 네트워크에서는 서버가 치워 모두의 화면에서 함께 사라지게 한다.
        if (Remover != null)
        {
            Remover(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
