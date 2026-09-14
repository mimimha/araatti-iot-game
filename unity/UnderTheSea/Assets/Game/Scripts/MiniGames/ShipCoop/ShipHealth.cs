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
    /// <summary>
    /// 참이면 아무리 맞아도 HP 가 줄지 않는다. **개발용입니다.**
    ///
    /// 한 가지 장치만 들여다보려면 침몰하지 않아야 합니다. 침수를 보다가 죽으면
    /// 침수를 볼 수가 없습니다. (ShipCoopDevMode 가 켜고 끕니다)
    /// </summary>
    public bool Invincible { get; set; }

    /// <summary>
    /// **남은 체력을 밖에서 정해 준다.** 서버가 정한 값을 화면에 옮길 때만 쓴다.
    ///
    /// <c>TakeDamage</c> 로 맞추지 않는 이유: 그쪽은 피해량을 받아 스스로 빼고 연출까지 낸다.
    /// 복제는 <b>결과만</b> 옮겨야 한다. 안 그러면 같은 피해가 두 번 계산된다.
    /// </summary>
    public void ShowHp(float current)
    {
        float clamped = Mathf.Clamp(current, 0f, maxHp);

        if (Mathf.Approximately(clamped, CurrentHp))
        {
            return;
        }

        bool wasAlive = !IsSunk;
        CurrentHp = clamped;

        Changed?.Invoke(CurrentHp, maxHp);

        if (wasAlive && IsSunk)
        {
            Sunk?.Invoke();
        }
    }

    public void TakeDamage(float amount, string reason = null)
    {
        if (IsSunk || Invincible || amount <= 0f)
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
