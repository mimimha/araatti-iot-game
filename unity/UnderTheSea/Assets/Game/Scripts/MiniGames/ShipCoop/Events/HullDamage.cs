using System;
using UnityEngine;
using UnderTheSea.MiniGames.ShipCoop.Net;

/// <summary>
/// 💥 선체 파손 · 침수. 직접 수리한다. (SHIPCOOP.md 5장)
///
/// 다른 사건과 성격이 다릅니다. 제한 시간이 없고, **고칠 때까지 계속 새는** 사건입니다.
/// 배를 깎는 것은 이 사건이 아니라 만들어낸 파손 지점(RepairTask)입니다.
/// 그래서 damageOnFail 은 0 으로, duration 도 0(제한 없음)으로 두는 것을 권합니다.
///
/// 이 사건이 다른 사건들의 도착지입니다.
/// 암초에 부딪히거나 적선 포격을 맞으면 여기로 연쇄됩니다.
/// 그때 수리할 사람이 자리를 비우고, 그 자리가 또 비게 됩니다. (5장)
///
/// 파손 지점은 프리팹으로 만들어 둡니다.
/// (Prefabs/MiniGames/ShipCoop/HullDamagePoint.prefab)
/// </summary>
public class HullDamage : VoyageEvent
{
    [Header("파손 지점")]
    [Tooltip("만들어낼 파손 지점 프리팹. RepairTask 가 붙어 있어야 한다.")]
    [SerializeField] private RepairTask damagePointPrefab;

    [Tooltip("파손 지점을 놓을 후보 자리들. 비워두면 이 오브젝트 위치에 놓는다.\n" +
             "갑판 위 여러 곳을 넣어두면 매번 다른 곳이 터진다.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("한 번에 몇 군데가 터지는지")]
    [SerializeField, Min(1)] private int pointCount = 1;

    /// <summary>지금 살아있는 파손 지점</summary>
    public RepairTask SpawnedPoint { get; private set; }

    /// <summary>지난 번에 고른 자리. 바로 다음 번엔 뽑지 않는다 — 후보가 적으면(4곳) 같은 곳이 이어질 확률이 높아 "고정" 처럼 느껴진다.</summary>
    private Transform _lastSpawnPoint;

    // ⚠ **자리를 차지하지 않습니다.** 제한 시간이 없어서, 차지하면 수리할 때까지
    //    다른 사건이 하나도 못 뜹니다. 자세한 이유는 VoyageEvent.TakesSlot 참고.
    /// <inheritdoc />
    public override bool TakesSlot => false;

    private int _remainingToRepair;

    /// <summary>
    /// 파손 지점을 **만드는 방법**. 비어 있으면 예전처럼 <c>Instantiate</c> 한다.
    ///
    /// 네트워크에서는 서버가 <c>Runner.Spawn</c> 으로 만들도록 갈아끼운다.
    /// 그래야 모두가 <b>같은 자리</b>의 같은 구멍을 보고, 늦게 들어온 사람도 받는다.
    /// (<c>ShipCoopHoleSpawner</c> 가 끼우고 뺀다)
    /// </summary>
    public static Func<RepairTask, Vector3, Quaternion, Transform, RepairTask> Factory;

    protected override void OnBegin()
    {
        // ⚠ 구멍은 **계산하는 쪽만** 만든다.
        //    각자 만들면 자리가 제각각이 되고, 남의 화면에는 없는 구멍을 수리하게 된다.
        //    만들어진 구멍은 네트워크가 알아서 모두에게 보낸다.
        if (!ShipCoopNet.IsAuthorityHere)
        {
            return;
        }

        if (damagePointPrefab == null)
        {
            Debug.LogError($"[{name}] 파손 지점 프리팹이 비어 있습니다. 아무 일도 일어나지 않습니다.", this);
            Fail();
            return;
        }

        _remainingToRepair = 0;

        for (int i = 0; i < pointCount; i++)
        {
            Spawn();
        }

        if (_remainingToRepair == 0)
        {
            Fail();
        }
    }

    private void Spawn()
    {
        Transform where = PickSpawnPoint();

        Vector3 position = where != null ? where.position : transform.position;
        Quaternion rotation = where != null ? where.rotation : transform.rotation;
        Transform parent = where != null ? where.parent : transform.parent;

        RepairTask point = Factory != null
            ? Factory(damagePointPrefab, position, rotation, parent)
            : Instantiate(damagePointPrefab, position, rotation, parent);

        if (point == null)
        {
            return;
        }

        point.name = $"{damagePointPrefab.name}_{Time.frameCount}_{_remainingToRepair}";
        point.Repaired += HandleRepaired;

        SpawnedPoint = point;
        _remainingToRepair++;
    }

    private Transform PickSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        // 매번 다른 곳이 터지게 한다. 자리를 외우면 압박이 사라진다.
        // 후보가 4곳뿐이라 그냥 뽑으면 25% 확률로 지난 번과 같은 곳이 이어져 "고정"처럼 느껴진다.
        // 후보가 둘 이상이면 지난 번 자리는 빼고 고른다.
        Transform picked;

        if (spawnPoints.Length > 1 && _lastSpawnPoint != null)
        {
            do
            {
                picked = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            }
            while (picked == _lastSpawnPoint);
        }
        else
        {
            picked = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }

        _lastSpawnPoint = picked;
        return picked;
    }

    private void HandleRepaired(RepairTask point)
    {
        point.Repaired -= HandleRepaired;

        _remainingToRepair--;

        if (_remainingToRepair <= 0)
        {
            Succeed();
        }
    }

    /// <summary>
    /// 구멍이 난 갑판. **파손은 사건 중에 유일하게 매번 다른 층에 생깁니다.** (9장)
    ///
    /// 파손 자리가 세 층에 흩어져 있어서(4장 원칙 1), 앞갑판에 있는 사람은
    /// 뒷갑판 구멍이 안 보입니다. 층 이름이 없으면 알림을 보고도 어디로 뛸지 모릅니다.
    /// </summary>
    public override ShipDeck Where =>
        SpawnedPoint != null ? ShipDeck.At(SpawnedPoint.transform.position) : null;

    /// <summary>HUD 문구</summary>
    public override string LiveHint() => RepairHint();

    /// <summary>HUD 문구</summary>
    public string RepairHint()
    {
        // ⚠ **예고 중에는 아무 말도 하지 않는다.** 구멍은 OnBegin 에서 생기므로 예고 동안 SpawnedPoint 가 비어 있다.
        //    그걸 "수리 완료" 로 읽어서, 파손 예고 카드 밑에 **아직 생기지도 않은 구멍이 다 고쳐졌다**고 떴다.
        if (SpawnedPoint == null)
        {
            return null;
        }

        if (SpawnedPoint.IsRepaired)
        {
            return "수리 완료";
        }

        return SpawnedPoint.IsEmpty
            ? $"물이 들어온다! 수리해라  ({SpawnedPoint.Progress01:P0})"
            : $"수리 중  ({SpawnedPoint.Progress01:P0}, {SpawnedPoint.Hits}회)";
    }
}
