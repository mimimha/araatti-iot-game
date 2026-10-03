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

        [Tooltip("이 구간에 들어서는 순간 한꺼번에 터뜨릴 사건 수. 0 이면 간격대로만 뿌린다.\n\n" +
                 "FINAL STORM 은 '한꺼번에 온다' 가 규격이므로(3장) 여기를 채운다.\n" +
                 "간격을 좁히는 것만으로는 하나씩 오는 느낌이 남는다.")]
        [Min(0)] public int openingBurst = 0;

        [Tooltip("이 구간에 들어서는 순간 **반드시** 한 번 띄울 사건. 추첨을 거치지 않는다.\n\n" +
                 "적선이 추첨에만 맡겨져 있어 한 판 내내 안 나오는 일이 있었다. 대포 · 포탄 운반을\n" +
                 "한 번도 안 써 보고 끝나므로, 페이즈 2 에 들어서면 적선을 한 번은 띄운다.\n" +
                 "이미 벌어지고 있는 사건이면 건너뛴다. 그 뒤로는 평소처럼 추첨에도 나온다.")]
        public List<VoyageEvent> guaranteed = new List<VoyageEvent>();
    }

    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ShipCoopGame game;

    [Header("구간별 계획")]
    [SerializeField] private List<PhasePlan> plans = new List<PhasePlan>();

    [Header("출항 직후")]
    [Tooltip("출항하고 이 시간이 지난 뒤부터 사건을 뿌린다.\n" +
             "시작하자마자 터지면 자리를 나누기도 전에 진다.\n\n" +
             "제한시간보다 짧게가 아니라 **출항 구간보다 짧게** 잡아야 한다.\n" +
             "이 값이 출항 구간보다 길면 출항 계획이 한 번도 돌지 않는다.")]
    [SerializeField, Min(0f)] private float graceSeconds = 6f;

    // ------------------------------------------------------------
    // ⚠ **같은 사건이 내리 나오면 고장으로 보입니다.**
    //
    //    후보에서 균등하게 뽑기만 했더니 실제로 같은 것이 세 번, 네 번씩
    //    이어졌습니다. 확률로는 당연한 일입니다 — 페이즈 1 은 후보가 둘뿐이라
    //    세 번 연속이 나올 확률이 1/4 이고, 한 판에 사건이 열 번 넘게 뜨므로
    //    **거의 매 판 한 번씩은 일어납니다.**
    //
    //    문제는 그게 무작위로 안 읽힌다는 것입니다. 사람은 같은 것이 세 번
    //    이어지면 추첨이 아니라 **스크립트이거나 버그**라고 읽습니다.
    //    5장이 "무작위로만 뿌리면 억울하게 지고, 스케줄만 있으면 외워진다" 고
    //    한 것의 반대쪽 실패입니다. 외워지지도 않았는데 짜인 것처럼 보입니다.
    //
    //    그래서 **잇달아 나오는 횟수에만 뚜껑을 씌웁니다.** 그 외에는 그대로
    //    균등 추첨입니다. 가중치를 주거나 순서를 돌리면 외워집니다.
    // ------------------------------------------------------------

    [Header("되풀이 막기")]
    [Tooltip("같은 사건이 잇달아 나올 수 있는 최대 횟수.\n\n" +
             "2 면 두 번까지는 이어져도 세 번째는 다른 것이 나온다.\n" +
             "1 로 두면 같은 것이 연달아 나오는 일이 아예 없어지는데, 후보가\n" +
             "둘뿐인 구간에서는 두 사건이 번갈아 나오는 것이 되어 오히려 외워진다.")]
    [SerializeField, Min(1)] private int mostInARow = 2;

    [Header("무작위 고정 (선택)")]
    [Tooltip("0 이 아니면 이 값으로 무작위를 고정한다. 같은 순서를 다시 보고 싶을 때 쓴다.")]
    [SerializeField] private int randomSeed = 0;

    /// <summary>지금 구간의 계획. 없으면 null.</summary>
    public PhasePlan CurrentPlan { get; private set; }

    /// <summary>
    /// 참이면 새 사건을 뿌리지 않는다. **개발용입니다.**
    ///
    /// 사건 하나를 들여다보는 동안 다른 사건이 끼어들면 무엇 때문에 그렇게 된 것인지
    /// 알 수가 없습니다. 이미 뜬 사건은 그대로 굴러갑니다.
    /// (ShipCoopDevMode 가 켜고 끕니다)
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>다음 사건까지 남은 시간 (초)</summary>
    public float NextEventIn => Mathf.Max(0f, _nextEventTime - _elapsed);

    private float _elapsed;
    private float _nextEventTime;
    private int _lastPhaseIndex = -99;
    private System.Random _random;

    /// <summary>직전에 뽑힌 사건. 몇 번 이어졌는지 세려고 들고 있는다.</summary>
    private VoyageEvent _lastPicked;

    /// <summary>그 사건이 잇달아 뽑힌 횟수.</summary>
    private int _sameInARow;

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
            game.Restarted += ResetSchedule;
        }
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.Finished -= HandleGameFinished;
            game.Restarted -= ResetSchedule;
        }
    }

    private void Start()
    {
        ResetSchedule();
    }

    /// <summary>
    /// 사건 시계를 처음으로 되돌린다. 판이 새로 시작될 때도 부른다.
    ///
    /// 이걸 안 하면 다시 시작한 판에서 출항하자마자 사건이 터진다 — 시계가 아까 그대로라
    /// 다음 사건 시각이 이미 지나 있기 때문이다. 처음 출항할 때처럼 유예 시간을 다시 준다.
    /// </summary>
    private void ResetSchedule()
    {
        _elapsed = 0f;
        _nextEventTime = graceSeconds;

        // 지난 판에서 이어지던 것을 물고 가면 안 된다. 새 판은 아무것도 안 나온 상태다.
        _lastPicked = null;
        _sameInARow = 0;
    }

    private void Update()
    {
        if (game == null || game.State != ShipCoopState.Sailing)
        {
            return;
        }

        // 사건을 고르는 것은 계산하는 쪽만 한다. (SHIPCOOP.md 11장)
        // 4대가 각자 추첨하면 서로 다른 사건이 뜬다. 같은 씨앗을 줘도
        // 프레임 타이밍이 달라 결국 어긋난다.
        if (!game.IsAuthority)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        CurrentPlan = PlanFor(game.CurrentPhaseIndex);

        // 구간이 바뀌었다. 개막 폭발이 있으면 지금 한꺼번에 터뜨린다.
        if (game.CurrentPhaseIndex != _lastPhaseIndex)
        {
            _lastPhaseIndex = game.CurrentPhaseIndex;

            // 멈춰 있으면 개막 폭발도 건너뛴다. 개발 중에 진행도를 건너뛰면
            // 구간이 순식간에 바뀌는데, 그때마다 3개가 터지면 볼 수가 없다.
            if (!Paused)
            {
                BeginGuaranteed(CurrentPlan);
                OpeningBurst(CurrentPlan);
            }
        }

        // 멈춰 있으면 새로 뿌리지 않는다. 이미 뜬 사건은 그대로 굴러간다.
        if (Paused || CurrentPlan == null || _elapsed < _nextEventTime)
        {
            return;
        }

        // 이미 충분히 겹쳐 있으면 더 얹지 않는다. 억울하게 지는 지점이다.
        //
        // ⚠ **침수는 안 셉니다.** 제한 시간이 없어서 한 번 뜨면 수리할 때까지
        //    자리를 물고 있습니다. 출항·페이즈1 은 동시최대가 1 이라, 세면
        //    두 구간이 통째로 조용해집니다. (VoyageEvent.TakesSlot)
        if (BusyCount() >= CurrentPlan.maxConcurrent)
        {
            // 조금 뒤에 다시 본다.
            _nextEventTime = _elapsed + 2f;
            return;
        }

        VoyageEvent picked = PickFrom(CurrentPlan);

        // 고를 것이 없었다. 후보가 전부 벌어지는 중이거나, 되풀이를 막느라 비켰거나.
        //
        // ⚠ 여기서 간격을 통째로 다시 세면 안 됩니다. 아무것도 안 뿌리고 10초를
        //    더 기다리게 되어, 되풀이를 막은 대가로 갑판이 조용해집니다.
        //    위 동시최대에 걸렸을 때와 똑같이 2초 뒤에 다시 봅니다.
        if (picked == null)
        {
            _nextEventTime = _elapsed + 2f;
            return;
        }

        picked.Begin();
        ScheduleNext(CurrentPlan);
    }

    /// <summary>
    /// 구간에 들어서는 순간 여러 사건을 한꺼번에 시작한다.
    ///
    /// FINAL STORM 의 "한꺼번에 온다" 가 이것이다. (3장)
    /// 간격만 좁히면 빠르게 하나씩 오는 느낌이 남고, 자리를 나눌 여유가 생긴다.
    /// 동시에 터져야 "지금까지 쓴 모든 작업을 동시에" 가 성립한다.
    /// </summary>
    private void OpeningBurst(PhasePlan plan)
    {
        if (plan == null || plan.openingBurst <= 0)
        {
            return;
        }

        int fired = 0;

        for (int i = 0; i < plan.openingBurst; i++)
        {
            VoyageEvent picked = PickFrom(plan);
            if (picked == null)
            {
                break;
            }

            picked.Begin();
            fired++;
        }

        Debug.Log($"[스케줄러] {plan.label} 진입 — 사건 {fired}개가 한꺼번에 시작됐다", this);

        ScheduleNext(plan);
    }

    /// <summary>
    /// 구간에 들어서는 순간 반드시 띄울 사건을 띄운다. (<see cref="PhasePlan.guaranteed"/>)
    ///
    /// 추첨도 동시최대도 보지 않는다 — "반드시" 이기 때문이다. 이미 벌어지고 있는 것만 건너뛴다.
    /// 되풀이 막기에는 뽑힌 것으로 적어 둔다. 곧바로 추첨이 같은 것을 또 고르지 않게.
    /// </summary>
    private void BeginGuaranteed(PhasePlan plan)
    {
        if (plan == null || plan.guaranteed == null || plan.guaranteed.Count == 0)
        {
            return;
        }

        int fired = 0;

        for (int i = 0; i < plan.guaranteed.Count; i++)
        {
            VoyageEvent forced = plan.guaranteed[i];

            if (forced == null || forced.IsActive)
            {
                continue;
            }

            Remember(forced);
            forced.Begin();
            fired++;
            Debug.Log($"[스케줄러] {plan.label} 진입 — '{forced.name}' 을(를) 반드시 띄웠다", this);
        }

        // 곧바로 추첨 사건이 얹히지 않게 간격을 새로 잡는다.
        if (fired > 0)
        {
            ScheduleNext(plan);
        }
    }

    /// <summary>지금 자리를 차지하고 있는 사건 수. 침수처럼 시한 없는 것은 안 센다.</summary>
    private static int BusyCount()
    {
        int busy = 0;

        for (int i = 0; i < VoyageEvent.Active.Count; i++)
        {
            if (VoyageEvent.Active[i] != null && VoyageEvent.Active[i].TakesSlot)
            {
                busy++;
            }
        }

        return busy;
    }

    private void ScheduleNext(PhasePlan plan)
    {
        float min = Mathf.Min(plan.minInterval, plan.maxInterval);
        float max = Mathf.Max(plan.minInterval, plan.maxInterval);
        _nextEventTime = _elapsed + min + (float)_random.NextDouble() * (max - min);
    }

    /// <summary>
    /// 이 구간의 사건 후보 중 지금 쓸 수 있는 것을 하나 고른다.
    ///
    /// **잇달아 <see cref="mostInARow"/> 번 나온 사건은 후보에서 뺍니다.** (위 주석)
    ///
    /// ⚠ **뺄 수가 없을 때가 있습니다.** 후보가 그것 하나뿐인 경우입니다.
    ///    출항 구간은 후보가 둘인데 동시최대가 1 이라, 하나가 벌어지고 있으면
    ///    남는 것이 되풀이될 그 사건뿐입니다.
    ///
    ///    그때는 <b>뽑지 않고 null 을 돌려줍니다.</b> 이미 다른 사건이 굴러가고
    ///    있으니 갑판이 조용해지지 않고, 조금 뒤에 다시 보면 그쪽이 끝나 있어
    ///    규칙을 지킨 채로 고를 수 있습니다.
    ///
    ///    ⛔ 다만 **아무것도 안 굴러가는데** 후보가 그것뿐이면 (후보가 사실상
    ///       하나인 구간) 규칙을 지킬 방법이 없습니다. 그대로 두면 사건이 영영
    ///       안 뜨므로, 그때는 되풀이를 허용하고 로그를 남깁니다.
    ///       조용한 갑판이 되풀이보다 나쁩니다. (5장)
    /// </summary>
    private VoyageEvent PickFrom(PhasePlan plan)
    {
        // 이미 벌어지고 있는 사건은 다시 시작할 수 없으므로 후보에서 뺀다.
        var candidates = new List<VoyageEvent>();

        // 잇달아 나온 것을 뺀 나머지. 웬만하면 여기서 고른다.
        var fresh = new List<VoyageEvent>();

        bool worn = _lastPicked != null && _sameInARow >= mostInARow;

        for (int i = 0; i < plan.pool.Count; i++)
        {
            VoyageEvent candidate = plan.pool[i];

            if (candidate == null || candidate.IsActive)
            {
                continue;
            }

            candidates.Add(candidate);

            if (!worn || candidate != _lastPicked)
            {
                fresh.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (fresh.Count > 0)
        {
            return Remember(fresh[_random.Next(fresh.Count)]);
        }

        // 되풀이될 그 사건 하나만 남았다. 다른 것이 굴러가고 있으면 기다린다.
        if (VoyageEvent.Active.Count > 0)
        {
            return null;
        }

        Debug.LogWarning(
            $"[스케줄러] {plan.label} 에서 '{_lastPicked.name}' 이 {_sameInARow + 1}번째 잇달아 나옵니다. " +
            $"고를 수 있는 사건이 이것뿐이라 막지 못했습니다. 이 구간의 후보를 늘리세요.", this);

        return Remember(candidates[_random.Next(candidates.Count)]);
    }

    /// <summary>뽑은 것을 적어 둔다. 몇 번 이어졌는지 세는 것이 전부다.</summary>
    private VoyageEvent Remember(VoyageEvent picked)
    {
        _sameInARow = picked == _lastPicked ? _sameInARow + 1 : 1;
        _lastPicked = picked;

        return picked;
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
