using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 항해 중에 벌어지는 사건 하나. (SHIPCOOP.md 5장)
///
/// 사건은 스스로 시작하지 않습니다. EventScheduler 가 구간에 맞춰 Begin 을 불러줍니다.
/// 다른 사건의 연쇄로 시작될 수도 있습니다.
///
/// 하는 일
///   - 제한 시간을 센다
///   - 상속한 쪽이 Succeed / Fail 을 부를 때까지 기다린다
///   - 시간이 다 되면 실패로 본다
///   - 실패하면 배를 깎고, 연쇄로 다음 사건을 일으킨다
///
/// **왼쪽 사건 알림이 이 게임에서 가장 중요한 UI 입니다.** (9장)
/// 그래서 WarningText 를 사건마다 반드시 채웁니다.
/// </summary>
public abstract class VoyageEvent : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("HUD 왼쪽에 뜨는 알림 문구. 지금 무슨 일인지 한눈에 보여야 한다.")]
    [SerializeField] private string warningText = "⚠ 사건";

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

    /// <summary>HUD 알림 문구</summary>
    public string WarningText => warningText;

    /// <summary>지금 벌어지고 있는지</summary>
    public bool IsActive { get; private set; }

    /// <summary>시작한 뒤 지난 시간 (초)</summary>
    public float Elapsed { get; private set; }

    /// <summary>제한 시간 (초). 0 이면 제한이 없다.</summary>
    public float Duration => duration;

    /// <summary>남은 시간 비율. 1 에서 0 으로 줄어든다. 제한이 없으면 항상 1.</summary>
    public float Remaining01 =>
        duration <= 0f ? 1f : Mathf.Clamp01(1f - Elapsed / duration);

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

        IsActive = true;
        Elapsed = 0f;
        ActiveEvents.Add(this);

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

        Stop();
        OnCancel();
    }

    private void Update()
    {
        if (!IsActive)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        Elapsed += deltaTime;

        OnTick(deltaTime);

        // OnTick 안에서 이미 끝났을 수 있다.
        if (!IsActive)
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
        if (!IsActive)
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
        if (!IsActive)
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
        IsActive = false;
        ActiveEvents.Remove(this);
    }

    // ------------------------------------------------------------
    // 상속한 쪽이 채우는 것
    // ------------------------------------------------------------

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
