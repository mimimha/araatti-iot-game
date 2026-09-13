using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 항해 중에 벌어지는 사건 하나. (SHIPCOOP.md 5장)
///
/// 사건은 스스로 시작하지 않습니다. EventScheduler 가 구간에 맞춰 Begin 을 불러줍니다.
/// 다른 사건의 연쇄로 시작될 수도 있습니다.
///
/// 사건은 세 단계를 거칩니다. (5장)
///
/// <code>
/// [예고] ──warnSeconds── [발생] ────duration──── [실패]
/// </code>
///
/// **예고 동안에는 아무것도 깎지 않습니다.** OnBegin 도 아직 부르지 않습니다.
/// 파손 지점도 안 생기고, 돛도 안 풀리고, 대포 구독도 안 붙습니다. 알림만 뜹니다.
///
/// 예고가 없으면 그 자리에 미리 서 있는 것이 유일한 전략이 되고, 그러면 한 명이
/// 거기 박힙니다. 4명이 돌려 막는 게임이 되려면 **박히는 자리가 0개**여야 합니다. (4장 원칙 1)
///
/// 하는 일
///   - 예고를 띄우고, 끝나면 진짜로 터뜨린다
///   - 제한 시간을 센다
///   - 상속한 쪽이 Succeed / Fail 을 부를 때까지 기다린다
///   - 시간이 다 되면 실패로 본다
///   - 실패하면 배와 돛을 깎고, 연쇄로 다음 사건을 일으킨다
///
/// **왼쪽 사건 알림이 이 게임에서 가장 중요한 UI 입니다.** (9장)
/// 그래서 WarningText 를 사건마다 반드시 채웁니다.
/// </summary>
public abstract class VoyageEvent : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("HUD 왼쪽에 뜨는 알림 문구. 지금 무슨 일인지 한눈에 보여야 한다.")]
    [SerializeField] private string warningText = "⚠ 사건";

    [Header("예고")]
    [Tooltip("터지기 전에 이만큼 미리 알린다. (초) 0 이면 예고 없이 바로 터진다.\n\n" +
             "예고 동안에는 아무것도 깎이지 않는다. 알림만 뜬다.\n" +
             "예고가 없으면 그 자리에 미리 서 있는 것이 유일한 전략이 되고,\n" +
             "그러면 한 명이 거기 박힌다. (4장 원칙 1)")]
    [SerializeField, Min(0f)] private float warnSeconds = 3f;

    [Header("시간")]
    [Tooltip("이 시간 안에 대응하지 못하면 실패로 본다. 0 이면 시간 제한이 없다.")]
    [SerializeField, Min(0f)] private float duration = 8f;

    [Header("실패했을 때")]
    [Tooltip("배가 이만큼 깎인다.")]
    [SerializeField, Min(0f)] private float damageOnFail = 15f;

    [Tooltip("돛 힘이 이만큼 풀린다. (0 ~ 1)\n\n" +
             "암초에 긁히거나 파도에 옆을 맞으면 배가 느려진다. 그것이 이 값이다.\n" +
             "HP 와 달리 이건 시간으로 갚는다. 누군가 돛으로 가서 다시 당겨야 하고, " +
             "그 사람이 가 있는 동안 그 사람의 원래 자리가 빈다.")]
    [SerializeField, Range(0f, 1f)] private float sailLossOnFail = 0.4f;

    [Tooltip("실패하면 여기 넣은 사건들이 함께 시작된다. (5장 — 사건은 연쇄합니다)\n" +
             "예: 암초에 부딪힘 → 선체 파손")]
    [SerializeField] private List<VoyageEvent> chainOnFail = new List<VoyageEvent>();

    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipHealth health;

    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipCoopGame game;

    [Tooltip("비워두면 씬에서 자동으로 찾는다. 실패했을 때 돛을 푸는 데 쓴다.")]
    [SerializeField] private ShipVoyage voyage;

    /// <summary>지금 살아있는 사건들. HUD 가 이걸 그대로 왼쪽에 뿌린다.</summary>
    public static IReadOnlyList<VoyageEvent> Active => ActiveEvents;

    private static readonly List<VoyageEvent> ActiveEvents = new List<VoyageEvent>();

    /// <summary>HUD 알림 문구. 갑판 이름은 안 붙는다. (<see cref="WarningLine"/> 참고)</summary>
    public string WarningText => warningText;

    /// <summary>
    /// 이 사건이 **어느 갑판에서** 벌어지는지. 배 전체에 걸리는 사건이면 null. (SHIPCOOP.md 9장)
    ///
    /// 갑판이 3층이 되면서 **화면 밖에서 벌어지는 일**이 생겼습니다.
    /// 앞갑판에 있는데 뒷갑판에 구멍이 나면 눈에 안 보입니다.
    /// 알림에 층 이름이 없으면 보고도 어디로 뛸지 모릅니다.
    ///
    /// ⚠ **아무 사건에나 붙이지 않습니다.**
    ///
    /// <code>
    /// 💥 선체 파손   매번 다른 층에 생긴다        →  붙인다
    /// 🪨🌊🌪️🏴‍☠️      대응하는 자리가 늘 같은 층이다  →  안 붙인다
    /// </code>
    ///
    /// 조타가 뒷갑판이라는 것은 몇 판 하면 외워집니다. 그걸 매번 알려주면
    /// 읽을 것만 늘고 정작 **변하는 정보(파손이 어디냐)** 가 묻힙니다.
    /// </summary>
    public virtual ShipDeck Where => null;

    /// <summary>
    /// HUD 알림의 첫 줄. 갑판이 정해지는 사건이면 층 이름이 함께 나온다.
    ///
    /// <code>
    /// ⚠ 거대한 파도!
    /// ⚠ 선체 파손!  (뒷갑판)
    /// </code>
    /// </summary>
    public string WarningLine
    {
        get
        {
            ShipDeck deck = Where;
            return deck != null ? $"{warningText}  ({deck.DeckName})" : warningText;
        }
    }

    /// <summary>
    /// 사건 알림 아래에 붙는 **지금 무엇을 해야 하는지**. 없으면 null.
    ///
    /// <see cref="WarningText"/> 는 "⚠ 거대한 파도!" 처럼 무슨 일이 났는지만 말합니다.
    /// 그것만 띄우면 **뭘 해야 하는지는 화면 어디에도 없습니다.**
    /// 실제로 파도가 그랬습니다 — 피하지 말라는 말을 아무도 못 들었습니다.
    ///
    /// 자리 안내(HintOf)와 같은 방식으로 작은 글씨 두 번째 줄이 됩니다. (9장)
    /// 매 프레임 불리므로 문자열을 아껴 만듭니다.
    /// </summary>
    public virtual string LiveHint()
    {
        return null;
    }

    /// <summary>
    /// 이 안내가 **발생 중에도** 떠 있어야 하는지. 보통은 거짓. (SHIPCOOP.md 9장)
    ///
    /// 둘째 줄은 원래 **예고 중에만** 보여줍니다. 예고는 아무것도 안 깎이는 구간이라
    /// 글을 읽을 여유가 있는 유일한 때이고, 발생하면 이미 몸이 움직이고 있습니다.
    /// 거기서까지 글자를 띄우면 우당탕탕하는 중에 읽을 것만 늘어납니다. (5장)
    ///
    /// 다만 **"지금 이걸 안 하면 진다"** 는 신호는 다릅니다. 그건 가르치는 말이 아니라
    /// 비상 신호라서 발생 중에 떠야 의미가 있습니다. 파도의 🆘 가 그렇습니다.
    /// </summary>
    public virtual bool HintIsUrgent => false;

    /// <summary>사건이 지금 어느 단계인지. (5장 — 예고 → 발생 → 실패)</summary>
    public enum Stage
    {
        /// <summary>아직 안 일어났거나 이미 끝났다.</summary>
        None,

        /// <summary>곧 터진다고 알리는 중. **아무것도 깎지 않는다.**</summary>
        Warning,

        /// <summary>터졌다. 제한 시간을 세고, 못 넘기면 실패한다.</summary>
        Running,
    }

    /// <summary>지금 단계</summary>
    public Stage CurrentStage { get; private set; }

    /// <summary>HUD 에 올라가 있는지. 예고 중에도 참이다.</summary>
    public bool IsActive => CurrentStage != Stage.None;

    /// <summary>예고 중인지. HUD 가 이걸로 색을 가른다. (9장)</summary>
    public bool IsWarning => CurrentStage == Stage.Warning;

    /// <summary>이미 터져서 제한 시간을 세는 중인지</summary>
    public bool IsRunning => CurrentStage == Stage.Running;

    /// <summary>지금 단계로 들어온 뒤 지난 시간 (초)</summary>
    public float Elapsed { get; private set; }

    /// <summary>제한 시간 (초). 0 이면 제한이 없다.</summary>
    public float Duration => duration;

    /// <summary>
    /// 줄어드는 게이지를 보여줄 수 있는지.
    ///
    /// 예고 중이면 발생까지, 터진 뒤면 실패까지 셉니다.
    /// 제한이 없는 사건(선체 파손)은 터진 뒤에 셀 것이 없으므로 거짓입니다.
    /// **줄어들지 않는 게이지는 오해를 줍니다.** (9장)
    /// </summary>
    public bool HasCountdown =>
        CurrentStage == Stage.Warning ? warnSeconds > 0f : duration > 0f;

    /// <summary>남은 비율. 1 에서 0 으로 줄어든다. 셀 것이 없으면 항상 1.</summary>
    public float Remaining01
    {
        get
        {
            float span = CurrentStage == Stage.Warning ? warnSeconds : duration;
            return span <= 0f ? 1f : Mathf.Clamp01(1f - Elapsed / span);
        }
    }

    /// <summary>
    /// 예고 시작부터 실패까지를 하나로 이은 값. 1 에서 0 으로 줄어듭니다.
    ///
    /// **바다 위 장애물이 이 값을 보고 다가옵니다.** (5장)
    /// 1 이면 수평선, 0 이면 배에 닿습니다.
    ///
    /// 속도가 아니라 남은 시간에서 위치를 뽑는 이유는, 도중에 돛이 풀려도
    /// **보이는 것과 판정이 어긋나지 않게** 하기 위해서입니다.
    /// 바위는 아직 저기 있는데 타이머가 먼저 끝나면 그 게임은 억울합니다. (2장)
    /// </summary>
    public float Approach01
    {
        get
        {
            float total = warnSeconds + duration;
            if (total <= 0f)
            {
                return 0f;
            }

            float done = CurrentStage == Stage.Warning ? Elapsed : warnSeconds + Elapsed;
            return Mathf.Clamp01(1f - done / total);
        }
    }

    /// <summary>끝났다. (사건, 성공 여부)</summary>
    public event Action<VoyageEvent, bool> Finished;

    protected ShipHealth Health => health;
    protected ShipCoopGame Game => game;
    protected ShipVoyage Voyage => voyage;

    protected virtual void Awake()
    {
        if (health == null)
        {
            health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        }

        if (game == null)
        {
            game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        }

        if (voyage == null)
        {
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        }
    }

    protected virtual void OnDisable()
    {
        if (IsActive)
        {
            Stop();
        }
    }

    /// <summary>사건을 시작한다. 스케줄러나 다른 사건의 연쇄가 부른다.</summary>
    public void Begin()
    {
        if (IsActive)
        {
            return;
        }

        ActiveEvents.Add(this);
        Elapsed = 0f;

        // 예고부터 시작한다. 이 동안에는 아무것도 깎지 않고 알림만 띄운다.
        if (warnSeconds > 0f)
        {
            CurrentStage = Stage.Warning;
            Debug.Log($"[사건] {warningText} 예고 — {warnSeconds:F0}초 뒤", this);
            OnWarn();
            return;
        }

        OnWarn();
        StartRunning();
    }

    /// <summary>예고가 끝났다. 여기서부터 진짜로 터진다.</summary>
    private void StartRunning()
    {
        CurrentStage = Stage.Running;
        Elapsed = 0f;

        Debug.Log($"[사건] {warningText} 시작", this);
        OnBegin();
    }

    /// <summary>판정 없이 그냥 끝낸다. 게임이 끝날 때 쓴다.</summary>
    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }

        // 예고 중에 끊기면 OnBegin 이 아직 안 불렸다. 뒷정리할 것도 없다.
        bool started = IsRunning;

        Stop();

        if (started)
        {
            OnCancel();
        }
    }

    private void Update()
    {
        if (!IsActive)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        Elapsed += deltaTime;

        // 화면에 보여주는 것은 예고 중에도 움직여야 한다. 바위는 예고 때부터 다가온다.
        OnShow(deltaTime);

        if (!IsActive)
        {
            return;
        }

        if (CurrentStage == Stage.Warning)
        {
            if (Elapsed >= warnSeconds)
            {
                StartRunning();
            }

            return;
        }

        OnTick(deltaTime);

        // OnTick 안에서 이미 끝났을 수 있다.
        if (!IsRunning)
        {
            return;
        }

        if (duration > 0f && Elapsed >= duration)
        {
            OnTimeout();
        }
    }

    /// <summary>대응에 성공했다. 상속한 쪽에서 부른다.</summary>
    protected void Succeed()
    {
        // 예고 중에는 넘길 것이 아직 없다.
        if (!IsRunning)
        {
            return;
        }

        Stop();
        Debug.Log($"[사건] {warningText} 넘겼다", this);

        OnSucceed();
        Finished?.Invoke(this, true);
    }

    /// <summary>대응하지 못했다. 배를 깎고 연쇄를 일으킨다.</summary>
    protected void Fail()
    {
        // 예고 중에는 실패할 수 없다. 아직 터지지도 않았다.
        if (!IsRunning)
        {
            return;
        }

        Stop();
        Debug.Log($"[사건] {warningText} 실패", this);

        if (damageOnFail > 0f && health != null)
        {
            health.TakeDamage(damageOnFail, warningText);
        }

        // 배가 느려진다. 돛을 다시 올리려면 누군가 그리로 가야 하고,
        // 그동안 그 사람의 원래 자리가 빈다. 사건이 사람을 움직이게 만드는 쪽이
        // 돛을 계속 눌러야 하게 만드는 것보다 낫다. (4장)
        if (sailLossOnFail > 0f && voyage != null)
        {
            float before = voyage.SailPower01;
            voyage.SailPower01 = Mathf.Clamp01(before - sailLossOnFail);

            if (!Mathf.Approximately(before, voyage.SailPower01))
            {
                Debug.Log($"[사건] {warningText} — 돛이 풀렸다. {before:P0} → {voyage.SailPower01:P0}", this);
            }
        }

        OnFail();

        // 하나의 실수가 다른 문제로 이어진다. (5장)
        for (int i = 0; i < chainOnFail.Count; i++)
        {
            if (chainOnFail[i] != null)
            {
                chainOnFail[i].Begin();
            }
        }

        Finished?.Invoke(this, false);
    }

    private void Stop()
    {
        CurrentStage = Stage.None;
        ActiveEvents.Remove(this);

        // 성공이든 실패든 취소든 여기를 반드시 지난다. 바다에 띄운 것을 여기서 치운다.
        // OnCancel 에 두면 예고 중에 끊겼을 때 바위가 남는다.
        OnHide();
    }

    // ------------------------------------------------------------
    // 상속한 쪽이 채우는 것
    // ------------------------------------------------------------

    /// <summary>
    /// 예고가 시작됐다. **바다 위에 띄울 것을 여기서 띄운다.** (5장)
    ///
    /// 아직 아무것도 깎지 않는 단계입니다. 바위는 수평선에 나타나기만 합니다.
    /// 예고가 0초여도 한 번은 불립니다.
    /// </summary>
    protected virtual void OnWarn()
    {
    }

    /// <summary>
    /// 예고와 발생 내내 매 프레임. **화면에 보여주는 것만** 여기서 합니다.
    ///
    /// 바위를 <see cref="Approach01"/> 에 맞춰 옮기는 자리입니다.
    /// 판정은 하지 않습니다. 판정은 <see cref="OnTick"/> 에서 하고, 그쪽은 터진 뒤에만 돕니다.
    /// </summary>
    protected virtual void OnShow(float deltaTime)
    {
    }

    /// <summary>
    /// 끝났다. 성공이든 실패든 취소든 반드시 지나갑니다.
    /// **바다에 띄운 것을 여기서 치웁니다.**
    /// </summary>
    protected virtual void OnHide()
    {
    }

    /// <summary>시작할 때. 연출을 켜거나 목표값을 정한다.</summary>
    protected virtual void OnBegin()
    {
    }

    /// <summary>
    /// 매 프레임. 대응이 되었는지 여기서 판정하고 Succeed / Fail 을 부른다.
    /// 아무것도 부르지 않으면 제한 시간까지 기다린다.
    /// </summary>
    protected virtual void OnTick(float deltaTime)
    {
    }

    /// <summary>제한 시간이 다 됐다. 기본은 실패다.</summary>
    protected virtual void OnTimeout()
    {
        Fail();
    }

    protected virtual void OnSucceed()
    {
    }

    protected virtual void OnFail()
    {
    }

    /// <summary>판정 없이 끝났을 때</summary>
    protected virtual void OnCancel()
    {
    }
}
