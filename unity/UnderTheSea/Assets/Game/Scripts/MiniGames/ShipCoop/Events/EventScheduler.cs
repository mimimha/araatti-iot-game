using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 구간마다 어떤 사건을 언제 뿌릴지 정하는 곳. (SHIPCOOP.md 5장)
///
/// > 사건 발생은 **구간별 스케줄 + 연쇄 규칙** 두 가지로 만듭니다. 무작위로만 뿌리지 않습니다.
/// > 무작위로만 뿌리면 억울하게 지고, 스케줄만 있으면 외워집니다.
///
/// 여기가 스케줄 쪽입니다. 연쇄는 각 사건의 chainOnFail 이 담당합니다.
///
/// 구간은 ShipCoopGame 의 페이즈를 그대로 씁니다.
/// 진행도로 나뉘므로, 늦게 가면 늦게 온다는 것이 자연스럽게 성립합니다.
/// </summary>
public class EventScheduler : MonoBehaviour
{
    /// <summary>한 구간에서 사건을 어떻게 뿌릴지</summary>
    [Serializable]
    public class PhasePlan
    {
        [Tooltip("몇 번째 페이즈인지. ShipCoopGame 의 페이즈 목록 순서와 같다.")]
        public int phaseIndex;

        [Tooltip("보기용 이름. 동작에는 쓰이지 않는다.")]
        public string label = "페이즈";

        [Tooltip("이 구간에서 뿌릴 사건들. 이 안에서 무작위로 하나씩 고른다.")]
        public List<VoyageEvent> pool = new List<VoyageEvent>();

        [Tooltip("사건 사이 최소 간격 (초)")]
        [Min(0.5f)] public float minInterval = 12f;

        [Tooltip("사건 사이 최대 간격 (초)")]
        [Min(0.5f)] public float maxInterval = 20f;

        [Tooltip("이 구간에서 동시에 살아있을 수 있는 사건 수.\n" +
                 "이 게임의 재미는 작업이 겹칠 때 나오므로 2 이상을 권한다. (10장)")]
        [Min(1)] public int maxConcurrent = 2;
    }

    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipCoopGame game;

    [Header("구간별 계획")]
    [SerializeField] private List<PhasePlan> plans = new List<PhasePlan>();

    [Header("출항 직후")]
    [Tooltip("출항하고 이 시간이 지난 뒤부터 사건을 뿌린다.\n" +
             "시작하자마자 터지면 자리를 나누기도 전에 진다.")]
    [SerializeField, Min(0f)] private float graceSeconds = 10f;

    [Header("무작위 고정 (선택)")]
    [Tooltip("0 이 아니면 이 값으로 무작위를 고정한다. 같은 순서를 다시 보고 싶을 때 쓴다.")]
    [SerializeField] private int randomSeed = 0;

    /// <summary>지금 구간의 계획. 없으면 null.</summary>
    public PhasePlan CurrentPlan { get; private set; }

    /// <summary>다음 사건까지 남은 시간 (초)</summary>
    public float NextEventIn => Mathf.Max(0f, _nextEventTime - _elapsed);

    private float _elapsed;
    private float _nextEventTime;
    private System.Random _random;

    private void Awake()
    {
        if (game == null)
        {
            game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        }

        _random = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
    }

    private void OnEnable()
    {
        if (game != null)
        {
            game.Finished += HandleGameFinished;
        }
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.Finished -= HandleGameFinished;
        }
    }

    private void Start()
    {
        _elapsed = 0f;
        _nextEventTime = graceSeconds;
    }

    private void Update()
    {
        if (game == null || game.State != ShipCoopState.Sailing)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        CurrentPlan = PlanFor(game.CurrentPhaseIndex);

        if (CurrentPlan == null || _elapsed < _nextEventTime)
        {
            return;
        }

        // 이미 충분히 겹쳐 있으면 더 얹지 않는다. 억울하게 지는 지점이다.
        if (VoyageEvent.Active.Count >= CurrentPlan.maxConcurrent)
        {
            // 조금 뒤에 다시 본다.
            _nextEventTime = _elapsed + 2f;
            return;
        }

        VoyageEvent picked = PickFrom(CurrentPlan);
        if (picked != null)
        {
            picked.Begin();
        }

        ScheduleNext(CurrentPlan);
    }

    private void ScheduleNext(PhasePlan plan)
    {
        float min = Mathf.Min(plan.minInterval, plan.maxInterval);
        float max = Mathf.Max(plan.minInterval, plan.maxInterval);
        _nextEventTime = _elapsed + min + (float)_random.NextDouble() * (max - min);
    }

    /// <summary>이 구간의 사건 후보 중 지금 쓸 수 있는 것을 하나 고른다.</summary>
    private VoyageEvent PickFrom(PhasePlan plan)
    {
        // 이미 벌어지고 있는 사건은 다시 시작할 수 없으므로 후보에서 뺀다.
        var candidates = new List<VoyageEvent>();

        for (int i = 0; i < plan.pool.Count; i++)
        {
            VoyageEvent candidate = plan.pool[i];
            if (candidate != null && !candidate.IsActive)
            {
                candidates.Add(candidate);
            }
        }

        return candidates.Count == 0 ? null : candidates[_random.Next(candidates.Count)];
    }

    private PhasePlan PlanFor(int phaseIndex)
    {
        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].phaseIndex == phaseIndex)
            {
                return plans[i];
            }
        }

        return null;
    }

    /// <summary>게임이 끝나면 남은 사건을 정리한다. 끝난 뒤에 배가 계속 깎이면 안 된다.</summary>
    private void HandleGameFinished(bool success, int score)
    {
        // 원본을 돌면서 Cancel 하면 목록이 바뀌어 순회가 깨진다.
        var running = new List<VoyageEvent>(VoyageEvent.Active);
        for (int i = 0; i < running.Count; i++)
        {
            running[i].Cancel();
        }
    }
}
