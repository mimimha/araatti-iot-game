using UnityEngine;

/// <summary>
/// 아무 일도 하지 않는 임시 작업.
///
/// ⚠ 임시 구현입니다. 진짜 작업(HelmTask, SailTask, CannonTask, RepairTask)이
///    만들어지면 지워도 됩니다.
///
/// 있는 이유
///   TaskBase 는 abstract 라서 그대로는 오브젝트에 붙일 수 없습니다.
///   붙고 떨어지는 것과 선점이 제대로 도는지 먼저 확인하려면 붙일 수 있는 것이 하나 필요합니다.
///
/// 조작
///   W A S D               → 이동
///   자리 근처에서 Space   → 붙는다
///   A / D 를 누르고 있으면 → 게이지가 찬다
///   걸어나가면            → 떨어진다
///
/// 게이지가 다 차면 로그를 찍고 0 으로 돌아갑니다.
/// </summary>
public class DummyTask : TaskBase
{
    [Header("게이지가 다 차는 데 걸리는 시간 (초)")]
    [SerializeField, Range(0.2f, 10f)] private float secondsToFill = 2f;

    [Header("아무도 없을 때 게이지가 빠지는 속도 (초당)")]
    [SerializeField, Range(0f, 1f)] private float decayPerSecond = 0.3f;

    /// <summary>0 ~ 1. 나중에 HUD 의 상호작용 게이지가 이 값을 본다.</summary>
    public float Progress01 { get; private set; }

    protected override void Work(float deltaTime)
    {
        // 붙어 있는 사람들의 입력을 모두 더한다.
        // 협력 작업(Capacity 2)이면 두 명이 함께 밀어야 두 배로 빨리 찬다.
        float power = 0f;
        for (int i = 0; i < Workers.Count; i++)
        {
            power += Mathf.Abs(ShipCoopInput.Steer(Workers[i].Input));
        }

        if (power <= 0f)
        {
            return;
        }

        Progress01 += power / secondsToFill * deltaTime;

        if (Progress01 < 1f)
        {
            return;
        }

        Progress01 = 0f;
        Debug.Log($"[{DisplayName}] 게이지가 찼습니다. (붙어 있는 인원 {Workers.Count}명)", this);

        // 실제 기기가 붙으면 여기서 손에 진동이 온다.
        for (int i = 0; i < Workers.Count; i++)
        {
            Workers[i].Input?.VibrateBoth(0.6f, 0.15f);
        }
    }

    protected override void Idle(float deltaTime)
    {
        Progress01 = Mathf.Max(0f, Progress01 - decayPerSecond * deltaTime);
    }

    protected override void OnWorkerJoined(TaskWorker worker)
    {
        Debug.Log($"[{DisplayName}] {worker.name} 붙음 ({Workers.Count}/{Capacity})", this);
    }

    protected override void OnWorkerLeft(TaskWorker worker)
    {
        Debug.Log($"[{DisplayName}] {worker.name} 떨어짐 ({Workers.Count}/{Capacity})", this);
    }
}
