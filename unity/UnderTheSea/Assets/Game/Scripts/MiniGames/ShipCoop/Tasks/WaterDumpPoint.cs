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
    [Tooltip("켜면 **내가 물동이를 들고 있을 때만** 나타난다.\n\n" +
             "침수 여부로 켜면 배에 물이 찬 동안 계속 떠 있어서 거슬린다.\n" +
             "들고 있을 때만 보이면 '지금 이걸 어디에 버리지' 순간에만 나타난다.")]
    [SerializeField] private bool showOnlyWhenFlooded = true;

    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private ShipFlooding flooding;

    [Tooltip("내가 지금 물을 들고 있는지 물어볼 곳. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipCoopHud hud;

    /// <summary>내(로컬 플레이어)가 지금 물을 나르는 중인지. 못 찾으면 null.</summary>
    private CarryTask _localCarry;

    [Header("노란 표식 — 배치 도구가 붙인다")]
    [Tooltip("여기가 버리는 곳이라고 알려주는 노란 오버레이 박스. 물이 찼을 때만 깜박인다.")]
    [SerializeField] private Renderer marker;

    [Tooltip("깜박이는 한 주기(초). 반은 켜지고 반은 꺼진다.")]
    [SerializeField, Min(0.1f)] private float markerBlinkSeconds = 1.3f;

    /// <summary>버릴 수 있는 거리</summary>
    public float ReachRange => reachRange;

    private Renderer[] _renderers;

    private void Awake()
    {
        if (flooding == null)
        {
            flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
        }

        if (hud == null)
        {
            hud = FindAnyObjectByType<ShipCoopHud>(FindObjectsInactive.Include);
        }

        if (showOnlyWhenFlooded)
        {
            _renderers = VisibleRenderers();
            ShowRail(false);
        }

        if (marker != null)
        {
            marker.enabled = false;
        }

        if (flooding == null)
        {
            Debug.LogWarning($"[{name}] ShipFlooding 을 찾지 못했습니다. 물을 버려도 줄지 않습니다.", this);
        }
    }

    private void Update()
    {
        if (_localCarry == null && hud != null && hud.LocalWorker != null)
        {
            _localCarry = hud.LocalWorker.GetComponent<CarryTask>();
        }

        bool showNow = showOnlyWhenFlooded
            ? _localCarry != null && _localCarry.Carrying == Cargo.Water
            : true;

        if (_renderers != null)
        {
            ShowRail(showNow);
        }

        if (marker != null)
        {
            // 반은 켜지고 반은 꺼진다 — "깜박깜박". 안 들고 있으면 아예 끈다.
            bool blinkOn = showNow && Mathf.Repeat(Time.time, markerBlinkSeconds) < markerBlinkSeconds * 0.5f;
            marker.enabled = blinkOn;
        }
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
