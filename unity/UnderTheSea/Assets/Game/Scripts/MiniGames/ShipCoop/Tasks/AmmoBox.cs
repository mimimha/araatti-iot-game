using UnityEngine;

/// <summary>
/// 📦 보급 상자. 운반하는 사람이 여기서 물건을 집는다. (SHIPCOOP.md 4장)
///
/// 포탄만이 아니라 **수리 자재와 양동이도 같은 상자에서** 나옵니다.
/// 무엇이 나오는지는 kind 로 정합니다. 집고 · 들고 가고 · 놓는 문법이 셋 다 같아서
/// 상자를 따로 만들 이유가 없습니다.
///
/// 자리가 아닙니다. TaskBase 가 아니므로 여기 서 있어도 자리를 차지하지 않습니다.
/// 집는 것은 CarryTask 가 합니다.
/// </summary>
public class AmmoBox : MonoBehaviour
{
    [Header("여기서 무엇이 나오는지")]
    [Tooltip("포탄은 대포로, 자재는 파손 지점으로, 양동이는 뱃전으로 나른다.")]
    [SerializeField] private Cargo kind = Cargo.Ammo;

    [Header("집을 수 있는 거리 (m)")]
    [SerializeField] private float reachRange = 2f;

    [Header("남은 포탄")]
    [Tooltip("-1 이면 무한. 상자가 바닥나는 규칙은 아직 없다.")]
    [SerializeField] private int stock = -1;

    [Header("양동이 상자만")]
    [Tooltip("켜면 배에 물이 찼을 때만 나타난다. (kind 가 물일 때만 쓴다)\n\n" +
             "물은 0 에서 시작하는데 양동이 상자가 처음부터 갑판에 놓여 있으면\n" +
             "'물이 처음부터 차 있다' 로 읽힌다. 도구는 일이 생긴 뒤에 나타나야 한다.")]
    [SerializeField] private bool showOnlyWhenFlooded = true;

    private ShipFlooding _flooding;
    private Renderer[] _renderers;

    /// <summary>여기서 나오는 물건</summary>
    public Cargo Kind => kind;

    /// <summary>집을 수 있는 거리</summary>
    public float ReachRange => reachRange;

    /// <summary>지금 쓸 수 있는 상자인지. 양동이는 퍼낼 물이 있어야 쓸 수 있다.</summary>
    public bool IsUsable =>
        kind != Cargo.Water || !showOnlyWhenFlooded || (_flooding != null && _flooding.HasWater);

    /// <summary>아직 집을 것이 남았는지. HUD 안내와 집기 판정이 함께 이걸 본다.</summary>
    public bool HasStock => stock != 0 && IsUsable;

    private void Awake()
    {
        if (kind != Cargo.Water || !showOnlyWhenFlooded)
        {
            return;
        }

        _flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        ShowBox(false);
    }

    private void Update()
    {
        if (_renderers == null)
        {
            return;
        }

        ShowBox(IsUsable);
    }

    private void ShowBox(bool visible)
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

    /// <summary>포탄 하나를 꺼낸다. 성공하면 true.</summary>
    public bool TryTake()
    {
        if (stock == 0 || !IsUsable)
        {
            return false;
        }

        if (stock > 0)
        {
            stock--;
        }

        return true;
    }
}
