using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배 위의 작업 한 자리. 조타 · 돛 · 대포 · 수리가 모두 이것을 상속한다.
///
/// 자리 규칙
///   조타 · 돛 · 대포   Capacity 1
///   협력 작업          Capacity 2  (두 명이 붙어야 풀린다)
///
/// 붙는 것은 버튼, 떨어지는 것은 **자리에서 걸어나가면 자동**이다.
/// 떨어지는 버튼을 따로 두지 않는 이유는, 버튼이 하나뿐인 IoT 컨트롤러에서
/// 그 버튼이 이미 발사와 집기에 쓰이기 때문이다. (TaskWorker 참고)
///
/// 상속해서 만들 때는 Work 만 채우면 된다.
///
///     protected override void Work(float deltaTime)
///     {
///         float wheel = Workers[0].Input.Rotation;   // -1 ~ +1
///         ...
///     }
/// </summary>
public abstract class TaskBase : MonoBehaviour
{
    [Header("작업 이름 (화면 표시용)")]
    [SerializeField] private string displayName = "작업";

    [Header("동시에 붙을 수 있는 인원")]
    [Tooltip("보통 1. 두 명이 함께 힘을 써야 하는 협력 작업만 2 로 둔다.")]
    [SerializeField, Range(1, 4)] private int capacity = 1;

    [Header("상호작용 범위 (m)")]
    [Tooltip("이 거리 안에 들어오면 붙을 수 있고, 벗어나면 자동으로 떨어진다.")]
    [SerializeField] private float interactRange = 2f;

    private static readonly List<TaskBase> AllTasks = new List<TaskBase>();

    /// <summary>씬에 있는 모든 작업 자리. TaskWorker 가 가까운 자리를 찾을 때 쓴다.</summary>
    public static IReadOnlyList<TaskBase> All => AllTasks;

    private readonly List<TaskWorker> _workers = new List<TaskWorker>();

    /// <summary>지금 이 자리에 붙어 있는 사람들</summary>
    public IReadOnlyList<TaskWorker> Workers => _workers;

    public string DisplayName => displayName;
    public int Capacity => capacity;
    public float InteractRange => interactRange;

    /// <summary>자리가 다 찼는지. 찼으면 상호작용 아이콘을 회색으로 표시한다.</summary>
    public bool IsFull => _workers.Count >= capacity;

    /// <summary>아무도 없는지. 비어 있으면 그 방향의 위협이 쌓인다.</summary>
    public bool IsEmpty => _workers.Count == 0;

    /// <summary>붙어 있는 인원이 바뀌었다. HUD 가 이걸 듣는다.</summary>
    public event Action<TaskBase> OccupancyChanged;

    protected virtual void OnEnable()
    {
        AllTasks.Add(this);
    }

    protected virtual void OnDisable()
    {
        AllTasks.Remove(this);

        // 붙어 있던 사람들을 정리한다. 원본을 그대로 돌면서 지우면 순회가 깨진다.
        TaskWorker[] leaving = _workers.ToArray();
        foreach (TaskWorker worker in leaving)
        {
            Leave(worker);
        }
    }

    // 상속한 쪽에서 Update 를 새로 선언하면 이쪽이 가려져서 Work 가 아예 불리지 않는다.
    // 그런 사고를 막기 위해 virtual 로 열어둔다. 오버라이드할 때는 반드시 base.Update() 를 부른다.
    protected virtual void Update()
    {
        if (_workers.Count > 0)
        {
            Work(Time.deltaTime);
        }
        else
        {
            Idle(Time.deltaTime);
        }
    }

    /// <summary>주어진 위치가 상호작용 범위 안인지</summary>
    public bool IsInRange(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).sqrMagnitude <= interactRange * interactRange;
    }

    /// <summary>이 사람이 지금 붙을 수 있는지</summary>
    public bool CanJoin(TaskWorker worker)
    {
        return worker != null && !IsFull && !_workers.Contains(worker);
    }

    /// <summary>붙는다. 성공하면 true.</summary>
    public bool TryJoin(TaskWorker worker)
    {
        if (!CanJoin(worker))
        {
            return false;
        }

        _workers.Add(worker);
        OnWorkerJoined(worker);
        OccupancyChanged?.Invoke(this);
        return true;
    }

    /// <summary>떨어진다.</summary>
    public void Leave(TaskWorker worker)
    {
        if (worker == null || !_workers.Remove(worker))
        {
            return;
        }

        OnWorkerLeft(worker);
        OccupancyChanged?.Invoke(this);
    }

    /// <summary>누군가 붙어 있는 동안 매 프레임 불린다. 여기에 작업 내용을 쓴다.</summary>
    protected abstract void Work(float deltaTime);

    /// <summary>
    /// 아무도 없는 동안 매 프레임 불린다.
    /// 비웠을 때 벌어지는 일(돛 힘이 빠진다, 침수가 쌓인다)을 여기에 쓴다.
    /// </summary>
    protected virtual void Idle(float deltaTime)
    {
    }

    /// <summary>누가 붙었을 때. 연출이나 LED 를 여기서 켠다.</summary>
    protected virtual void OnWorkerJoined(TaskWorker worker)
    {
    }

    /// <summary>누가 떨어졌을 때</summary>
    protected virtual void OnWorkerLeft(TaskWorker worker)
    {
    }
}
