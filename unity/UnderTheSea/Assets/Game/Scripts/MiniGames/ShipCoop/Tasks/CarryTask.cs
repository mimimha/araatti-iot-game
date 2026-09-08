using System;
using UnityEngine;

/// <summary>
/// ⚫ 운반. 포탄 상자에서 포탄을 집어 대포로 나른다. (SHIPCOOP.md 4장)
///
/// **자리를 차지하지 않습니다.** 그래서 TaskBase 가 아니고 플레이어에 붙습니다.
/// 자리로 만들면 포탄을 든 사람이 대포에 붙을 수 없어 운반 자체가 성립하지 않습니다.
///
/// 대신 들고 있는 동안 <see cref="TaskWorker.HandsBusy"/> 를 켭니다.
/// 양손이 묶여서 그동안 다른 일을 못 하는 것이 이 작업의 전부입니다. (7장)
/// 어려워서 의미가 있는 게 아니라, 자리를 비우게 만들어서 의미가 있습니다.
///
/// 입력
///   집기 / 싣기 → ShipCoopInput.ConsumeInteract. 키보드는 Space.
///   유지        → ShipCoopInput.HoldBoth. 키보드는 양쪽 Shift.
///                 한 손이라도 놓으면 떨어뜨립니다.
///
/// ⚠ 포탄 상자를 작업 자리 옆에 두지 마세요.
///    빈손일 때는 붙기 버튼을 자리와 상자가 함께 노리므로 어느 쪽이 먹을지 알 수 없습니다.
/// </summary>
[RequireComponent(typeof(TaskWorker))]
public class CarryTask : MonoBehaviour
{
    [Header("한 번에 나르는 포탄 수")]
    [SerializeField, Min(1)] private int carryAmount = 1;

    [Header("싣을 수 있는 거리 (m)")]
    [Tooltip("대포에 이만큼 가까워야 포탄을 넘길 수 있다.")]
    [SerializeField, Min(0.5f)] private float loadRange = 2f;

    [Header("들고 있는 표시 (선택)")]
    [Tooltip("연결하면 들고 있는 동안만 켜진다. 큐브 하나를 머리 위에 두면 눈에 보인다.")]
    [SerializeField] private GameObject heldVisual;

    /// <summary>지금 포탄을 들고 있는지</summary>
    public bool IsCarrying { get; private set; }

    /// <summary>포탄을 집었다</summary>
    public event Action PickedUp;

    /// <summary>포탄을 대포에 실었다. 인자는 실은 개수.</summary>
    public event Action<int> Loaded;

    /// <summary>손을 놓아 포탄을 떨어뜨렸다</summary>
    public event Action Dropped;

    private TaskWorker _worker;

    private void Awake()
    {
        _worker = GetComponent<TaskWorker>();
        ShowHeld(false);
    }

    private void OnDisable()
    {
        // 사라질 때 손을 풀어준다. 안 그러면 그 사람은 영원히 자리에 못 붙는다.
        if (IsCarrying)
        {
            DropInternal(notify: false);
        }
    }

    private void Update()
    {
        IPlayerController input = _worker.Input;
        if (input == null)
        {
            return;
        }

        if (IsCarrying)
        {
            UpdateCarrying(input);
        }
        else
        {
            UpdateEmptyHanded(input);
        }
    }

    private void UpdateCarrying(IPlayerController input)
    {
        // 한 손이라도 놓으면 떨어뜨린다.
        if (!ShipCoopInput.HoldBoth(input))
        {
            DropInternal(notify: true);
            return;
        }

        // 실을 대포를 먼저 찾는다. 버튼을 먼저 소비하면 범위 밖에서 누른 것이
        // 그냥 사라져서, 눌렀는데 아무 일도 안 일어난 것처럼 보인다.
        CannonTask cannon = FindLoadableCannon();
        if (cannon == null)
        {
            return;
        }

        if (!ShipCoopInput.ConsumeInteract(input))
        {
            return;
        }

        int loaded = cannon.LoadAmmo(carryAmount);
        if (loaded <= 0)
        {
            return;
        }

        IsCarrying = false;
        _worker.HandsBusy = false;
        ShowHeld(false);

        input.VibrateBoth(0.4f, 0.1f);
        Debug.Log($"[{name}] 포탄 {loaded}발 실었다 → {cannon.name} ({cannon.Ammo}/{cannon.MaxAmmo})", this);
        Loaded?.Invoke(loaded);
    }

    private void UpdateEmptyHanded(IPlayerController input)
    {
        // 자리에 붙어 있는 동안에는 그 작업이 버튼을 쓴다. 여기서 가로채지 않는다.
        if (_worker.Current != null)
        {
            return;
        }

        AmmoBox box = FindReachableBox();
        if (box == null || !ShipCoopInput.ConsumeInteract(input))
        {
            return;
        }

        if (!box.TryTake())
        {
            return;
        }

        IsCarrying = true;
        _worker.HandsBusy = true;
        ShowHeld(true);

        input.VibrateBoth(0.3f, 0.1f);
        Debug.Log($"[{name}] 포탄을 집었다. 양손이 묶였다.", this);
        PickedUp?.Invoke();
    }

    /// <summary>손이 닿는 포탄 상자 중 가장 가까운 것</summary>
    private AmmoBox FindReachableBox()
    {
        AmmoBox best = null;
        float bestSqr = float.MaxValue;
        Vector3 position = transform.position;

        foreach (AmmoBox box in FindObjectsByType<AmmoBox>(FindObjectsInactive.Exclude))
        {
            if (!box.HasStock || !box.IsInReach(position))
            {
                continue;
            }

            float sqr = (box.transform.position - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = box;
            }
        }

        return best;
    }

    /// <summary>싣을 수 있는 거리 (m)</summary>
    public float LoadRange => loadRange;

    /// <summary>
    /// 가장 가까운 대포와 그 거리. 대포가 없으면 (null, -1).
    /// 사거리와 무관하게 찾는다. HUD 가 "대포까지 몇 m" 를 띄우는 데 쓴다.
    /// </summary>
    public (CannonTask cannon, float distance) NearestCannon()
    {
        CannonTask best = null;
        float bestDistance = float.MaxValue;
        Vector3 position = transform.position;

        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is not CannonTask cannon)
            {
                continue;
            }

            float d = Vector3.Distance(cannon.transform.position, position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = cannon;
            }
        }

        return best == null ? (null, -1f) : (best, bestDistance);
    }

    /// <summary>포탄을 더 실을 수 있는, 가까운 대포. 없으면 null.</summary>
    public CannonTask FindLoadableCannon()
    {
        CannonTask best = null;
        float bestSqr = loadRange * loadRange;
        Vector3 position = transform.position;

        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is not CannonTask cannon || !cannon.HasRoomForAmmo)
            {
                continue;
            }

            float sqr = (cannon.transform.position - position).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = cannon;
            }
        }

        return best;
    }

    private void DropInternal(bool notify)
    {
        IsCarrying = false;
        _worker.HandsBusy = false;
        ShowHeld(false);

        if (notify)
        {
            Debug.Log($"[{name}] 손을 놓아 포탄을 떨어뜨렸다.", this);
            Dropped?.Invoke();
        }
    }

    private void ShowHeld(bool visible)
    {
        if (heldVisual != null)
        {
            heldVisual.SetActive(visible);
        }
    }
}
