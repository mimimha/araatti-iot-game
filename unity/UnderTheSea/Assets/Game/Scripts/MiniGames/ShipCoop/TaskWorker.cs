using System;
using UnityEngine;

/// <summary>
/// 플레이어 쪽에 붙는다. 가까운 작업 자리를 찾아 붙고 떨어지는 일을 담당한다.
///
/// 사용법
///   1. 플레이어 오브젝트에 IPlayerController 구현체(KeyboardPlayerController 등)를 붙인다.
///   2. 같은 오브젝트에 이 스크립트를 붙인다.
///   3. 끝.
///
/// 조작
///   자리 근처에서 버튼   → 붙는다
///   자리에서 걸어나감    → 자동으로 떨어진다
///
/// 떨어지는 버튼을 따로 두지 않았다. IoT 컨트롤러의 버튼은 하나뿐이고
/// 그 버튼은 붙어 있는 동안 발사와 집기에 쓰이기 때문이다.
/// 걸어나가면 떨어지는 편이 "빈자리를 메우러 뛰어다니는" 느낌에도 맞는다.
/// </summary>
public class TaskWorker : MonoBehaviour
{
    [Header("컨트롤러")]
    [Tooltip("비워두면 같은 오브젝트에서 IPlayerController 구현체를 찾는다.")]
    [SerializeField] private MonoBehaviour controllerSource;

    /// <summary>이 사람의 컨트롤러</summary>
    public IPlayerController Input { get; private set; }

    /// <summary>지금 붙어 있는 자리. 없으면 null.</summary>
    public TaskBase Current { get; private set; }

    /// <summary>
    /// 양손이 묶여 있어 자리에 붙을 수 없는 상태. 운반(CarryTask)이 켠다.
    ///
    /// 포탄을 양손으로 들면 그동안 다른 일을 못 합니다. 그 제약이 운반을
    /// 이 게임의 접착제로 만듭니다. (SHIPCOOP.md 4장 · 7장)
    ///
    /// 켜져 있는 동안 붙기 버튼을 소비하지 않으므로 운반 쪽이 그 버튼을 받습니다.
    /// </summary>
    public bool HandsBusy { get; set; }

    /// <summary>지금 붙을 수 있는 가장 가까운 자리. 없으면 null. 상호작용 아이콘이 이걸 본다.</summary>
    public TaskBase Nearby { get; private set; }

    /// <summary>
    /// **붙고 떨어지는 것을 이 컴퓨터가 정하는가.**
    ///
    /// 혼자 하는 씬에서는 참이다. 네트워크에서는 <b>서버만</b> 참이고, 클라이언트는
    /// 서버가 정한 결과를 받아 <see cref="Join"/> · <see cref="LeaveCurrent"/> 로 따라간다.
    /// (<c>ShipCoopWorkerSync</c>)
    ///
    /// 거짓이어도 <see cref="Nearby"/> 는 계속 찾는다. "여기서 E" 안내는 화면 쪽 일이라
    /// 서버에 물을 필요가 없고, 물으면 왕복 시간만큼 늦게 뜬다.
    /// </summary>
    public bool DecidesJoin { get; set; } = true;

    /// <summary>붙을 수 있는 자리가 바뀌었다. (없으면 null)</summary>
    public event Action<TaskBase> NearbyChanged;

    /// <summary>붙거나 떨어졌다. (떨어졌으면 null)</summary>
    public event Action<TaskBase> CurrentChanged;

    private void Awake()
    {
        Input = controllerSource as IPlayerController;

        if (Input == null)
        {
            Input = GetComponent<IPlayerController>();
        }

        if (Input == null)
        {
            Debug.LogError(
                $"[{name}] IPlayerController 를 찾을 수 없습니다. " +
                "KeyboardPlayerController 를 같은 오브젝트에 붙이세요.", this);
        }
    }

    private void OnDisable()
    {
        // 죽거나 사라질 때 자리를 물고 있으면 아무도 그 자리를 못 쓴다.
        LeaveCurrent();
    }

    private void Update()
    {
        if (Input == null)
        {
            return;
        }

        if (HandsBusy)
        {
            // 양손이 묶였다. 자리를 놓고, 붙기 버튼은 운반 쪽이 가져가게 비켜준다.
            if (DecidesJoin)
            {
                LeaveCurrent();
            }

            if (Nearby != null)
            {
                Nearby = null;
                NearbyChanged?.Invoke(null);
            }

            return;
        }

        if (Current != null)
        {
            // 붙어 있는 동안 버튼은 작업이 가져간다. (발사, 집기 / 놓기)
            // 여기서 ConsumeButtonPress 를 부르면 작업이 버튼을 놓친다.
            if (DecidesJoin && !Current.IsInRange(transform.position))
            {
                LeaveCurrent();
            }

            return;
        }

        UpdateNearby();

        // 여기서부터는 판정이다. 정하는 쪽이 아니면 화면 안내까지만 하고 물러난다.
        if (!DecidesJoin)
        {
            return;
        }

        // ⚠ **쥐고 있으면 자리에 붙지 않는다.** 그 버튼은 운반이 가져간다. (SHIPCOOP.md 4장)
        //
        //   쥔 채로 버튼   집는다
        //   그냥 버튼       붙는다
        //
        // 이 줄이 없으면 상자나 쌓인 포탄이 자리와 겹칠 때 **둘이 같은 버튼을 두고 다툽니다.**
        // 누가 먼저 가져갈지는 스크립트 실행 순서에 달려 있어 매번 다를 수 있습니다.
        // 특히 포탄을 대포 옆 바닥에 쌓게 되면서 집는 곳과 붙는 곳이 같은 자리가 됐습니다.
        if (ShipCoopInput.HoldBoth(Input))
        {
            return;
        }

        if (Nearby != null && ShipCoopInput.ConsumeInteract(Input))
        {
            Join(Nearby);
        }
    }

    /// <summary>가장 가까운, 붙을 수 있는 자리를 찾는다.</summary>
    private void UpdateNearby()
    {
        TaskBase best = null;
        float bestSqr = float.MaxValue;
        Vector3 position = transform.position;

        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            TaskBase task = TaskBase.All[i];

            if (task == null || !task.CanJoin(this) || !task.IsInRange(position))
            {
                continue;
            }

            float sqr = (task.transform.position - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = task;
            }
        }

        if (!ReferenceEquals(best, Nearby))
        {
            Nearby = best;
            NearbyChanged?.Invoke(Nearby);
        }
    }

    /// <summary>자리에 붙는다.</summary>
    public bool Join(TaskBase task)
    {
        if (task == null || Current != null || !task.TryJoin(this))
        {
            return false;
        }

        Current = task;
        Nearby = null;
        NearbyChanged?.Invoke(null);
        CurrentChanged?.Invoke(Current);
        return true;
    }

    /// <summary>지금 자리에서 떨어진다.</summary>
    public void LeaveCurrent()
    {
        if (Current == null)
        {
            return;
        }

        TaskBase left = Current;
        Current = null;
        left.Leave(this);
        CurrentChanged?.Invoke(null);
    }

    /// <summary>
    /// 자리 쪽에서 이 사람을 놓아줄 때 부른다. TaskBase.Leave 전용.
    ///
    /// 수리가 끝나 파손 지점이 꺼지는 것처럼 자리가 먼저 사라질 수 있다.
    /// 그때 사람이 죽은 자리를 붙들고 있으면 걸어나가기 전까지 다른 자리에 붙지 못한다.
    ///
    /// LeaveCurrent 는 Current 를 먼저 비우고 Leave 를 부르므로 서로 되부르지 않는다.
    /// </summary>
    internal void ClearCurrent(TaskBase task)
    {
        if (!ReferenceEquals(Current, task))
        {
            return;
        }

        Current = null;
        CurrentChanged?.Invoke(null);
    }
}
