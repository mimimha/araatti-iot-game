using System;
using UnityEngine;

/// <summary>
/// 배의 HP. 씬에 하나만 둔다.
///
/// 개인 HP 가 아니라 **4명이 공유하는 단 하나의 값**이다.
/// 이 값이 0 이 되면 침몰이고, 4명 모두 실패한다.
///
/// 사용법
///   1. 배 오브젝트에 이 스크립트를 붙인다.
///   2. ShipCoopGame 의 Inspector 에 연결한다.
/// </summary>
public class ShipHealth : MonoBehaviour
{
    [Header("배 HP")]
    [SerializeField] private float maxHp = 100f;

    /// <summary>최대 HP</summary>
    public float MaxHp => maxHp;

    /// <summary>현재 HP</summary>
    public float CurrentHp { get; private set; }

    /// <summary>0 ~ 1. HUD 게이지에 그대로 쓴다.</summary>
    public float Ratio01 => maxHp <= 0f ? 0f : Mathf.Clamp01(CurrentHp / maxHp);

    /// <summary>침몰했는지</summary>
    public bool IsSunk => CurrentHp <= 0f;

    /// <summary>HP 가 바뀌었다. (현재, 최대)</summary>
    public event Action<float, float> Changed;

    /// <summary>침몰했다. 한 번만 발생한다.</summary>
    public event Action Sunk;

    private void Awake()
    {
        CurrentHp = maxHp;
    }

    /// <summary>
    /// 피해를 입는다. reason 은 콘솔에서 원인을 추적하기 위한 것이다.
    /// (예: "암초 충돌", "적선 포격", "침수")
    /// </summary>
    public void TakeDamage(float amount, string reason = null)
    {
        if (IsSunk || amount <= 0f)
        {
            return;
        }

        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        Changed?.Invoke(CurrentHp, maxHp);

        if (!string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[ShipHealth] {reason} — {amount:0.#} 피해. 남은 HP {CurrentHp:0.#}", this);
        }

        if (IsSunk)
        {
            Debug.Log("[ShipHealth] 배가 침몰했습니다.", this);
            Sunk?.Invoke();
        }
    }

    /// <summary>수리한다. 최대치를 넘지 않는다.</summary>
    public void Repair(float amount)
    {
        // 이미 가라앉은 배는 고칠 수 없다. 되살아나면 실패가 실패가 아니게 된다.
        if (IsSunk || amount <= 0f)
        {
            return;
        }

        CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
        Changed?.Invoke(CurrentHp, maxHp);
    }
}
