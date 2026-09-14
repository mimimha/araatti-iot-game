using UnityEngine;

/// <summary>
/// 🪣 뱃전. 퍼낸 물을 여기에 버린다. (SHIPCOOP.md 4장)
///
/// 자리가 아닙니다. TaskBase 가 아니므로 여기 서 있어도 자리를 차지하지 않습니다.
/// 포탄 상자와 같은 성격이고, 방향만 반대입니다.
///
///   포탄 상자   여기서 집는다
///   뱃전        여기에 놓는다
///
/// **층마다 하나씩 둡니다.** 갑판이 3층(43m)이 되면서, 배 반대편에 하나만 두면
/// 양동이 왕복이 18초가 됩니다. 4장은 **왕복 4초**를 전제로 침수 속도를 정했으므로
/// 그렇게 두면 구멍 하나가 왕복 한 번에 45% 를 부어 **아무리 퍼내도 안 줄어듭니다.**
///
/// 운반의 대가는 이제 뱃전까지의 거리가 아니라 **배가 넓다는 것 자체**가 만듭니다.
/// 실제로도 물은 제일 가까운 난간에 버리지 배 반대편까지 들고 가지 않습니다.
///
/// 개수는 코드에 묶여 있지 않습니다. `CarryTask` 가 **닿는 것 중 가장 가까운 것**을 찾습니다.
/// </summary>
public class WaterDumpPoint : MonoBehaviour
{
    [Header("버릴 수 있는 거리 (m)")]
    [SerializeField, Min(0.5f)] private float reachRange = 2f;

    [Header("보이는 때")]
    [Tooltip("켜면 배에 물이 찼을 때만 나타난다.\n\n" +
             "양동이 상자와 같이 나타나고 같이 사라져야 한다.\n" +
             "버릴 곳만 먼저 보이면 그것도 '물이 처음부터 있다' 로 읽힌다.")]
    [SerializeField] private bool showOnlyWhenFlooded = true;

    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private ShipFlooding flooding;

    /// <summary>버릴 수 있는 거리</summary>
    public float ReachRange => reachRange;

    private Renderer[] _renderers;

    private void Awake()
    {
        if (flooding == null)
        {
            flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
        }

        if (showOnlyWhenFlooded)
        {
            _renderers = VisibleRenderers();
            ShowRail(false);
        }

        if (flooding == null)
        {
            Debug.LogWarning($"[{name}] ShipFlooding 을 찾지 못했습니다. 물을 버려도 줄지 않습니다.", this);
        }
    }

    private void Update()
    {
        if (_renderers == null)
        {
            return;
        }

        ShowRail(flooding != null && flooding.HasWater);
    }

    private void ShowRail(bool visible)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].enabled = visible;
            }
        }
    }

    /// <summary>
    /// 켜고 끌 렌더러. **씬에서 이미 꺼진 것은 넣지 않는다.**
    ///
    /// 배치 도구가 부모 큐브의 겉을 감추고 자식으로 상자 모델을 얹는다. 큐브 렌더러까지 목록에
    /// 넣으면 물이 찰 때 회색 큐브가 모델과 함께 다시 나타난다. 자식 모델의 렌더러는 켜져 있어 들어간다.
    /// </summary>
    private Renderer[] VisibleRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(includeInactive: true);
        System.Collections.Generic.List<Renderer> kept = new System.Collections.Generic.List<Renderer>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled)
            {
                kept.Add(all[i]);
            }
        }

        return kept.ToArray();
    }

    /// <summary>주어진 위치에서 손이 닿는지</summary>
    public bool IsInReach(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).sqrMagnitude <= reachRange * reachRange;
    }

    /// <summary>
    /// 여기에 물을 버린 횟수. 연출(💦 스플래시)을 네트워크로 옮길 때 쓴다 — 서버가 이 값을 복제하고
    /// 클라이언트는 값이 늘어난 만큼 <see cref="ShowDumped"/> 로 같은 연출을 낸다. (11장)
    /// </summary>
    public int DumpCount { get; private set; }

    private int _shownCount;

    /// <summary>들고 온 물을 버린다. 실제로 줄어들었으면 true 이고, 그때만 물이 튄다.</summary>
    public bool Dump()
    {
        bool dumped = flooding != null && flooding.Dump();

        if (dumped)
        {
            DumpCount++;
            _shownCount = DumpCount;
            Splash();
        }

        return dumped;
    }

    /// <summary>클라이언트: 서버가 버린 횟수를 받아, 늘어난 만큼 튀긴다.</summary>
    public void ShowDumped(int count)
    {
        if (count > _shownCount)
        {
            Splash();
        }

        _shownCount = count;
        DumpCount = count;
    }

    private void Splash()
    {
        WaterDumpSplash fx = GetComponent<WaterDumpSplash>();

        if (fx != null)
        {
            fx.Play();
        }
    }
}
