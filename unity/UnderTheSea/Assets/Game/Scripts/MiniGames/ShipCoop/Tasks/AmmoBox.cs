using UnityEngine;

/// <summary>
/// ⚫ 포탄 상자. 운반하는 사람이 여기서 포탄을 집는다. (SHIPCOOP.md 4장)
///
/// 자리가 아닙니다. TaskBase 가 아니므로 여기 서 있어도 자리를 차지하지 않습니다.
/// 집는 것은 CarryTask 가 합니다.
/// </summary>
public class AmmoBox : MonoBehaviour
{
    [Header("집을 수 있는 거리 (m)")]
    [SerializeField] private float reachRange = 2f;

    [Header("남은 포탄")]
    [Tooltip("-1 이면 무한. 상자가 바닥나는 규칙은 아직 없다.")]
    [SerializeField] private int stock = -1;

    /// <summary>집을 수 있는 거리</summary>
    public float ReachRange => reachRange;

    /// <summary>아직 집을 포탄이 남았는지</summary>
    public bool HasStock => stock != 0;

    /// <summary>주어진 위치에서 손이 닿는지</summary>
    public bool IsInReach(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).sqrMagnitude <= reachRange * reachRange;
    }

    /// <summary>포탄 하나를 꺼낸다. 성공하면 true.</summary>
    public bool TryTake()
    {
        if (stock == 0)
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
