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
/// 배 반대편에 두는 것이 좋습니다. **가까우면 운반이 아니라 제자리걸음이 됩니다.**
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
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
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

    /// <summary>주어진 위치에서 손이 닿는지</summary>
    public bool IsInReach(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).sqrMagnitude <= reachRange * reachRange;
    }

    /// <summary>들고 온 물을 버린다. 실제로 줄어들었으면 true.</summary>
    public bool Dump()
    {
        return flooding != null && flooding.Dump();
    }
}
