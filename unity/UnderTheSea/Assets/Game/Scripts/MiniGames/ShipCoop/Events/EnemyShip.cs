using UnityEngine;

/// <summary>
/// 🏴‍☠️ 오염된 적선. 대포로 제거한다. (SHIPCOOP.md 5장)
///
/// **대포를 쏘려면 포탄이 있어야 하고, 포탄은 누군가 날라야 합니다.**
/// 그래서 이 사건 하나가 대포와 운반 두 자리를 동시에 요구합니다.
/// 운반이 이 게임의 접착제인 이유가 여기서 드러납니다. (4장)
///
/// 제한 시간 안에 정해진 발수를 맞히지 못하면 포격을 맞습니다.
/// 연쇄 목록에 HullDamage 와 Squall 을 넣으면
/// "적선 포격 → 갑판 파손 + 돛 손상" 이 만들어져 두 명이 동시에 필요해집니다.
///
/// ⚠ 지금은 **대포가 쏜 것을 모두 명중으로 봅니다.**
///    조준(오른손 스틱)이 판정에 들어가는 것은 적선을 실제 위치에 띄운 뒤입니다.
///    지금 조준을 판정에 넣으면 큐브를 상대로 각도를 맞추는 이상한 작업이 됩니다.
/// </summary>
public class EnemyShip : VoyageEvent
{
    [Header("격파")]
    [Tooltip("이만큼 맞히면 격파된다. 포탄 운반 횟수를 정하는 숫자다.")]
    [SerializeField, Min(1)] private int hitsToDestroy = 2;

    /// <summary>지금까지 맞힌 발수</summary>
    public int Hits { get; private set; }

    /// <summary>격파까지 필요한 발수</summary>
    public int HitsToDestroy => hitsToDestroy;

    /// <summary>격파 진행도 0 ~ 1</summary>
    public float Progress01 => Mathf.Clamp01((float)Hits / hitsToDestroy);

    private CannonTask _cannon;

    protected override void OnBegin()
    {
        Hits = 0;
        _cannon = FindCannon();

        if (_cannon == null)
        {
            Debug.LogWarning($"[{name}] 대포를 찾지 못했습니다. 적선을 막을 방법이 없습니다.", this);
            return;
        }

        _cannon.Fired += HandleFired;
    }

    private void HandleFired(int ammoLeft)
    {
        if (!IsActive)
        {
            return;
        }

        Hits++;

        if (Hits >= hitsToDestroy)
        {
            Succeed();
        }
    }

    protected override void OnSucceed()
    {
        Unsubscribe();
        Game?.ReportEnemyDestroyed();
    }

    protected override void OnFail()
    {
        Unsubscribe();
    }

    protected override void OnCancel()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_cannon != null)
        {
            _cannon.Fired -= HandleFired;
            _cannon = null;
        }
    }

    /// <summary>HUD 문구. 지금 무엇이 모자란지 알려준다.</summary>
    public string CannonHint()
    {
        if (_cannon == null)
        {
            return "대포가 없다";
        }

        if (_cannon.Ammo <= 0)
        {
            return $"포탄이 없다! 상자에서 날라라  ({Hits}/{hitsToDestroy} 맞힘)";
        }

        return $"대포로 쏴라  ({Hits}/{hitsToDestroy} 맞힘, 포탄 {_cannon.Ammo}/{_cannon.MaxAmmo})";
    }

    private static CannonTask FindCannon()
    {
        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is CannonTask cannon)
            {
                return cannon;
            }
        }

        return null;
    }
}
