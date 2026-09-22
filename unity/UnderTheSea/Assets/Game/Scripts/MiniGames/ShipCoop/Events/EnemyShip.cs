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
///    적선은 이제 우현 바다에 실제 위치가 있습니다 (<c>EnemyShipVisual</c> — 우리 배와 같은 모델, 검은 해골 돛).
///    조준(오른손 스틱)을 판정에 넣는 것은 **다음 작업**입니다. 이 파일의 Hits 로직은 아직 그대로입니다.
///
/// ⚠ <c>Hits</c> 는 서버가 세고 <c>SyncExtra</c> 로 복제됩니다. 클라이언트의 적선 모양이 휘청 · 격파를 값으로 그립니다. (11장)
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

    /// <summary>복제할 값 — 맞힌 발수. 클라이언트의 <c>EnemyShipVisual</c> 이 휘청 · 격파를 이걸로 그린다.</summary>
    public override int SyncExtra => Hits;

    /// <summary>클라이언트 — 서버가 센 발수를 그대로 받는다. 판정은 하지 않는다.</summary>
    public override void ShowExtra(int value)
    {
        Hits = value;
    }

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
        Game?.ReportEnemyBreached();
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

    /// <summary>
    /// HUD 둘째 줄. **적선에는 안 띄운다.**
    ///
    /// 예고 단계에서는 아직 <c>OnBegin</c> 이 돌기 전이라 대포를 잡아 두지 않았고,
    /// 그래서 갑판에 대포가 멀쩡히 있는데도 "대포가 없다" 가 떴다.
    /// 문구 자체를 뺀다 — 적선은 무슨 일인지(WarningLine)만 알려주면 충분하다.
    /// </summary>
    public override string LiveHint() => null;

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
