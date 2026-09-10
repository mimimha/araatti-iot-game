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
///   집기        → 쥔 채로 ShipCoopInput.ConsumeInteract. 키보드는 Shift + Space.
///   유지        → ShipCoopInput.HoldBoth. 키보드는 Shift.
///                 한 손이라도 놓으면 떨어뜨립니다.
///   싣기        → 대포 앞에서 쥐던 손을 놓습니다. (loadTrigger 로 바꿀 수 있습니다)
///
/// **쥐지 않은 채 누른 버튼은 가져가지 않습니다.** 쥐지 않으면 집어도 그 프레임에
/// 도로 떨어뜨리는데, 그 사이에 붙기 버튼은 이미 사라집니다. 그래서 상자와 겹친
/// 자리에는 붙을 수 없었습니다. 파손 지점은 아무 데나 생기므로 상자 옆에 생깁니다.
///
/// ⚠ 그래도 포탄 상자를 작업 자리 옆에 두지 마세요.
///    쥐고 있을 때는 붙기 버튼을 자리와 상자가 함께 노립니다.
/// </summary>
[RequireComponent(typeof(TaskWorker))]
public class CarryTask : MonoBehaviour
{
    [Header("한 번에 나르는 포탄 수")]
    [SerializeField, Min(1)] private int carryAmount = 1;

    [Header("싣을 수 있는 거리 (m)")]
    [Tooltip("대포에 이만큼 가까워야 포탄을 넘길 수 있다.")]
    [SerializeField, Min(0.5f)] private float loadRange = 2f;

    /// <summary>포탄을 대포에 넘기는 시점</summary>
    public enum LoadTrigger
    {
        /// <summary>
        /// 대포 앞에서 쥐던 손을 놓으면 넣는다. (기본)
        ///
        /// 내려놓는다는 감각과 맞고, 어차피 놓을 손가락이라 버튼이 늘지 않는다.
        /// 언제 넣을지를 플레이어가 정한다.
        /// </summary>
        OnRelease,

        /// <summary>
        /// 대포에 닿는 순간 저절로 넣는다.
        ///
        /// ⚠ 다른 곳으로 가려고 대포 옆을 지나가기만 해도 들어간다.
        /// </summary>
        OnReach,

        /// <summary>
        /// 대포 앞에서 Space 를 눌러야 넣는다.
        ///
        /// ⚠ Shift 를 쥐고 방향키로 걸어가면서 Space 까지 눌러야 해서 손가락 3개가
        /// 필요하고, 그 순간 키보드가 키 하나를 놓쳐 포탄을 떨어뜨리기 쉽다.
        /// </summary>
        OnButton,
    }

    [Header("싣는 방법")]
    [SerializeField] private LoadTrigger loadTrigger = LoadTrigger.OnRelease;

    [Header("들고 있는 표시 (선택)")]
    [Tooltip("연결하면 들고 있는 동안만 켜진다. 큐브 하나를 머리 위에 두면 눈에 보인다.")]
    [SerializeField] private GameObject heldVisual;

    /// <summary>지금 무엇을 들고 있는지. 빈손이면 None.</summary>
    public Cargo Carrying { get; private set; }

    /// <summary>지금 무언가를 들고 있는지</summary>
    public bool IsCarrying => Carrying != Cargo.None;

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
        // 포탄이 아닌 것은 넘기는 방식이 하나뿐이다. 놓으면 넘어간다.
        // 대포처럼 사거리·정원·발사 타이밍이 얽힌 것이 아니라서 규칙을 늘릴 이유가 없다.
        if (Carrying != Cargo.Ammo)
        {
            if (!ShipCoopInput.HoldBoth(input))
            {
                if (!TryHandOver(input))
                {
                    DropInternal(notify: true);
                }
            }

            return;
        }

        CannonTask cannon = FindLoadableCannon();

        // 손을 놓았다. 대포 앞이면 넣고, 아니면 떨어뜨린다.
        if (!ShipCoopInput.HoldBoth(input))
        {
            if (loadTrigger == LoadTrigger.OnRelease && cannon != null && TryLoad(cannon, input))
            {
                return;
            }

            DropInternal(notify: true);
            return;
        }

        if (cannon == null)
        {
            return;
        }

        switch (loadTrigger)
        {
            case LoadTrigger.OnReach:
                TryLoad(cannon, input);
                break;

            case LoadTrigger.OnButton:
                // 대포를 먼저 찾은 뒤에 버튼을 소비한다. 순서를 바꾸면 사거리 밖에서
                // 누른 것이 그냥 사라져서, 눌렀는데 아무 일도 안 일어난 것처럼 보인다.
                if (ShipCoopInput.ConsumeInteract(input))
                {
                    TryLoad(cannon, input);
                }
                break;
        }
    }

    /// <summary>대포에 포탄을 넘긴다. 성공하면 true.</summary>
    private bool TryLoad(CannonTask cannon, IPlayerController input)
    {
        int loaded = cannon.LoadAmmo(carryAmount);
        if (loaded <= 0)
        {
            return false;
        }

        Carrying = Cargo.None;
        _worker.HandsBusy = false;
        ShowHeld(false);

        input.VibrateBoth(0.4f, 0.1f);
        Debug.Log($"[{name}] 포탄 {loaded}발 실었다 → {cannon.name} ({cannon.Ammo}/{cannon.MaxAmmo})", this);
        Loaded?.Invoke(loaded);
        return true;
    }

    private void UpdateEmptyHanded(IPlayerController input)
    {
        // 자리에 붙어 있는 동안에는 그 작업이 버튼을 쓴다. 여기서 가로채지 않는다.
        if (_worker.Current != null)
        {
            return;
        }

        // 쥐지 않았으면 버튼을 가져가지 않는다.
        //
        // 들자마자 UpdateCarrying 이 "손을 놓았다" 로 보고 떨어뜨리기 때문에,
        // 쥐지 않은 채 집는 것은 어차피 한 프레임도 유지되지 않는다.
        // 그런데 그 한 프레임 사이에 붙기 버튼은 이미 사라진다.
        //
        // 그래서 상자와 자리가 겹쳐 있으면 그 자리에 붙을 수 없었다.
        // 파손 지점은 아무 데나 생기므로 상자 옆에 생길 수 있다.
        // (DamageSpawn_01 은 상자에서 1.5m, 둘 다 반경 2m 다)
        // 수리하려고 Space 를 눌러도 포탄만 집었다 떨어뜨리기를 되풀이했다.
        if (!ShipCoopInput.HoldBoth(input))
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

        Carrying = box.Kind;
        _worker.HandsBusy = true;
        ShowHeld(true);

        input.VibrateBoth(0.3f, 0.1f);
        Debug.Log($"[{name}] {NameOf(Carrying)} 을(를) 집었다. 양손이 묶였다.", this);
        PickedUp?.Invoke();
    }

    /// <summary>손이 닿는 포탄 상자 중 가장 가까운 것. 없으면 null. HUD 안내가 이걸 본다.</summary>
    public AmmoBox FindReachableBox()
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

    /// <summary>
    /// 자재와 물을 넘긴다. 넘겼으면 true, 넘길 곳이 없으면 false 라 떨어뜨린다.
    ///
    /// 포탄은 여기 오지 않습니다. 대포는 사거리 · 정원 · 넣는 시점이 얽혀 있어
    /// TryLoad 가 따로 있습니다.
    /// </summary>
    private bool TryHandOver(IPlayerController input)
    {
        switch (Carrying)
        {
            case Cargo.Plank:
                RepairTask point = FindPointWantingPlank();
                if (point == null || !point.DeliverPlank())
                {
                    return false;
                }

                Finish(input, $"자재를 넘겼다 → {point.name}");
                return true;

            case Cargo.Water:
                WaterDumpPoint rail = FindReachableDump();
                if (rail == null || !rail.Dump())
                {
                    return false;
                }

                Finish(input, "물을 뱃전에 버렸다");
                return true;

            default:
                return false;
        }
    }

    /// <summary>들고 있던 것을 넘기고 손을 푼다.</summary>
    private void Finish(IPlayerController input, string what)
    {
        Carrying = Cargo.None;
        _worker.HandsBusy = false;
        ShowHeld(false);

        input.VibrateBoth(0.4f, 0.1f);
        Debug.Log($"[{name}] {what}", this);
    }

    /// <summary>자재를 기다리는 파손 지점 중 손이 닿는 가장 가까운 것</summary>
    public RepairTask FindPointWantingPlank()
    {
        RepairTask best = null;
        float bestSqr = loadRange * loadRange;
        Vector3 position = transform.position;

        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is not RepairTask repair || !repair.WantsPlank)
            {
                continue;
            }

            float sqr = (repair.transform.position - position).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = repair;
            }
        }

        return best;
    }

    /// <summary>물을 버릴 수 있는 뱃전 중 가장 가까운 것</summary>
    public WaterDumpPoint FindReachableDump()
    {
        WaterDumpPoint best = null;
        float bestSqr = float.MaxValue;
        Vector3 position = transform.position;

        foreach (WaterDumpPoint rail in FindObjectsByType<WaterDumpPoint>(FindObjectsSortMode.None))
        {
            if (!rail.IsInReach(position))
            {
                continue;
            }

            float sqr = (rail.transform.position - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = rail;
            }
        }

        return best;
    }

    private void DropInternal(bool notify)
    {
        Cargo dropped = Carrying;

        Carrying = Cargo.None;
        _worker.HandsBusy = false;
        ShowHeld(false);

        if (notify)
        {
            Debug.Log($"[{name}] 손을 놓아 {NameOf(dropped)} 을(를) 떨어뜨렸다.", this);
            Dropped?.Invoke();
        }
    }

    /// <summary>화면과 로그에 쓰는 이름</summary>
    public static string NameOf(Cargo cargo)
    {
        switch (cargo)
        {
            case Cargo.Ammo: return "포탄";
            case Cargo.Plank: return "수리 자재";
            case Cargo.Water: return "물";
            default: return "빈손";
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
