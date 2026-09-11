using System;
using UnityEngine;

/// <summary>판이 지금 어느 단계인가. (MINE.md 3장)</summary>
public enum MineState
{
    /// <summary>아직 시작 안 함. StartGame() 을 기다린다.</summary>
    Ready,

    /// <summary>목표 그림을 보여주는 중.</summary>
    Reveal,

    /// <summary>누군가의 턴. 그 사람만 팔 수 있다.</summary>
    Turn,

    /// <summary>턴 사이의 빈 시간. 앞사람이 뭘 했는지 보는 시간.</summary>
    TurnGap,

    /// <summary>끝. 채점하고 결과를 보여준 상태.</summary>
    Finished,
}

/// <summary>
/// 판의 진행을 맡는다. **초를 세고, 턴을 넘기고, 복구와 힌트를 관리하고, 끝나면 채점한다.**
///
///     공개 7초 → [턴 30초 → 빈 시간] × 인원 → 채점 → 로비 보고
///
/// 땅(<see cref="MineGrid"/>)과 화면(<see cref="MineGridView"/>)과
/// 채점(<see cref="IMineSimilarity"/>)은 이미 있는 것을 **쓰기만 한다.** 고치지 않는다.
///
/// 모양은 <c>ShipCoopGame</c> 을 따랐다. 코루틴이 아니라 상태 + Update 인 이유는
/// **밖에서 상태를 읽을 수 있어야** HUD 가 붙고 네트워크 동기화가 되기 때문이다.
///
/// **복구 개수는 여기 있고, 힌트 사용 여부는 <see cref="MineDigger"/> 에 있다.**
/// 복구는 팀 공용이라 판마다 하나, 힌트는 1인 1회라 사람마다 하나이기 때문이다.
/// (MINE.md 2·4장)
///
/// 이 단계에서 하지 않는 것 — 시야 제한(5단계), 탑뷰 연출(6단계),
/// 진짜 HUD(11장 8단계), 네트워크 턴 소유권(10단계).
/// </summary>
public class MineGame : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 찾는다.")]
    [SerializeField] private MineGrid grid;

    [Tooltip("비워두면 격자에서 찾는다. 공개와 숨김에 쓴다.")]
    [SerializeField] private MineGridView view;

    [Tooltip("파는 사람들. 개발 중에는 하나만 넣어도 되고, 그러면 혼자 여러 턴을 돈다.")]
    [SerializeField] private MineDigger[] diggers;

    [Header("판 설정 (MINE.md 2장 — 전부 시작값이고 조정 대상)")]
    [Tooltip("인원 = 턴 수. 1~4명.")]
    [SerializeField, Range(1, 4)] private int playerCount = 4;

    [Tooltip("목표 그림을 보여주는 시간(초).")]
    [SerializeField, Min(0f)] private float revealSeconds = 7f;

    [Tooltip("한 턴의 채굴 시간(초).")]
    [SerializeField, Min(1f)] private float turnSeconds = 30f;

    [Tooltip("턴과 턴 사이의 빈 시간(초). 앞사람 결과를 볼 시간.\n" +
             "0 이면 바로 다음 턴이 시작된다. MINE.md 12장의 조정 항목이다.")]
    [SerializeField, Min(0f)] private float turnGapSeconds = 0f;

    [Tooltip("이 값 이상이면 성공. (MINE.md 7장)")]
    [SerializeField, Range(0f, 100f)] private float successThreshold = 60f;

    [Header("복구와 힌트 (MINE.md 2·4장)")]
    [Tooltip("사람 한 명당 복구 블록 몇 개. 1 이면 4명에 4개.\n" +
             "블록은 팀 공용이다. 7번 밸런싱에서 조정할 값이다.")]
    [SerializeField, Min(0)] private int restoresPerPlayer = 1;

    [Tooltip("힌트로 목표를 다시 보여주는 시간(초).\n" +
             "⚠ 보는 동안에도 턴 시간은 계속 흐른다. 그것이 힌트의 대가다.")]
    [SerializeField, Min(0.5f)] private float hintSeconds = 3f;

    [Header("개발용")]
    [Tooltip("켜면 재생하자마자 판이 시작된다. 실제로는 로비가 불러준다.")]
    [SerializeField] private bool autoStart = true;

    private IMineSimilarity _similarity;
    private float _timer;
    private float _hintTimer;

    /// <summary>지금 어느 단계인가.</summary>
    public MineState State { get; private set; } = MineState.Ready;

    /// <summary>지금 몇 번째 턴인가. **1부터** 센다. 턴이 아니면 0.</summary>
    public int TurnNumber { get; private set; }

    /// <summary>이번 판의 전체 턴 수.</summary>
    public int TotalTurns => playerCount;

    /// <summary>지금 단계가 끝날 때까지 남은 초. HUD 가 이걸 읽는다.</summary>
    public float TimeLeft => Mathf.Max(0f, _timer);

    /// <summary>지금 턴인 사람. 턴이 아니면 null.</summary>
    public MineDigger CurrentDigger { get; private set; }

    /// <summary>남은 복구 블록. **팀 공용이다.** (MINE.md 4장)</summary>
    public int RestoresLeft { get; private set; }

    /// <summary>이번 판에 주어진 복구 블록 전체 수.</summary>
    public int TotalRestores => playerCount * restoresPerPlayer;

    /// <summary>지금 힌트로 그림이 보이는 중인가.</summary>
    public bool HintShowing => _hintTimer > 0f;

    /// <summary>지금 턴인 사람이 힌트를 쓸 수 있는가.</summary>
    public bool HintAvailable =>
        State == MineState.Turn && CurrentDigger != null && !CurrentDigger.HintUsed;

    /// <summary>이번 판의 목표 이름. 없으면 빈 문자열.</summary>
    public string TargetName =>
        grid != null && grid.Target != null ? grid.Target.displayName : string.Empty;

    /// <summary>채점 결과. 끝나기 전에는 비어 있다.</summary>
    public MineSimilarityResult Result { get; private set; }

    /// <summary>성공했는가. 끝나기 전에는 false.</summary>
    public bool Success { get; private set; }

    /// <summary>단계가 바뀔 때.</summary>
    public event Action<MineState> StateChanged;

    /// <summary>턴이 시작될 때. (몇 번째 턴인지, 1부터)</summary>
    public event Action<int> TurnStarted;

    /// <summary>복구 블록이 줄어들 때. (남은 수)</summary>
    public event Action<int> RestoresChanged;

    /// <summary>판이 끝났을 때. (성공 여부, 점수)</summary>
    public event Action<bool, int> Finished;

    private void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<MineGrid>();
        if (view == null && grid != null) view = grid.GetComponent<MineGridView>();

        // 판정 방식은 나중에 갈아끼운다. (MINE.md 7장)
        _similarity = new MineIoUSimilarity();

        if (grid == null)
            Debug.LogError($"{nameof(MineGame)}: MineGrid 를 찾지 못했습니다.", this);

        if (diggers == null || diggers.Length == 0)
            Debug.LogError($"{nameof(MineGame)}: 파는 사람이 하나도 없습니다. " +
                           $"Diggers 에 MineDigger 를 넣으세요.", this);
    }

    private void Start()
    {
        // 시작 전에는 아무도 못 판다. 재생하자마자 파이는 것을 막는다.
        SetOnlyDiggerActive(-1);

        if (autoStart) StartGame();
    }

    /// <summary>판을 시작한다. 나중에는 로비가 이걸 불러준다.</summary>
    public void StartGame()
    {
        if (grid == null) return;

        grid.ResetAll();

        foreach (MineDigger d in diggers)
        {
            if (d != null) d.ResetForNewGame();
        }

        TurnNumber = 0;
        Result = default;
        Success = false;

        RestoresLeft = TotalRestores;
        RestoresChanged?.Invoke(RestoresLeft);

        // 난이도별 도안 고르기는 나중에 여기서 grid.SetTarget() 으로 갈아끼운다.
        // (MINE.md 2장 — 인원이 곧 난이도)

        EnterReveal();
    }

    private void Update()
    {
        // Ready 와 Finished 는 시간이 흐르지 않는다.
        if (State == MineState.Ready || State == MineState.Finished) return;

        TickHint();

        // 복구와 힌트 요청은 자기 턴에만 받는다.
        if (State == MineState.Turn) HandleTurnRequests();

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        switch (State)
        {
            case MineState.Reveal:
                // 공개가 끝나면 그림을 숨기고 첫 턴을 시작한다.
                EnterTurn(1);
                break;

            case MineState.Turn:
                // 마지막 턴이었으면 끝, 아니면 빈 시간을 거쳐 다음 턴.
                if (TurnNumber >= TotalTurns) EnterFinished();
                else EnterTurnGap();
                break;

            case MineState.TurnGap:
                EnterTurn(TurnNumber + 1);
                break;
        }
    }

    // ------------------------------------------------------------
    // 복구와 힌트

    /// <summary>
    /// 지금 턴인 사람이 C · V 를 눌렀는지 물어본다.
    ///
    /// **입력은 MineDigger 만 읽고, 판단은 여기서 한다.** 복구가 몇 개 남았는지는
    /// MineGame 만 알기 때문이다. MineDigger 가 여기로 말을 걸게 하면 화살표가
    /// 거꾸로 생기므로, 눌린 것을 적어두게 하고 이쪽에서 가져온다.
    /// </summary>
    private void HandleTurnRequests()
    {
        MineDigger digger = CurrentDigger;
        if (digger == null) return;

        // 둘 다 반드시 부른다. 한쪽만 부르면 나머지 요청이 남아 다음 프레임에 터진다.
        bool wantsRestore = digger.ConsumeRestoreRequest();
        bool wantsHint = digger.ConsumeHintRequest();

        if (wantsRestore) TryRestore(digger);
        if (wantsHint) TryHint(digger);
    }

    private void TryRestore(MineDigger digger)
    {
        if (RestoresLeft <= 0)
        {
            Debug.Log("[MINE] 복구 블록이 남아 있지 않습니다.", this);
            return;
        }

        // ⚠ 안 파인 칸에서 눌렀으면 블록을 깎지 않는다.
        //   잘못 누른 것 때문에 귀한 블록이 날아가면 안 된다.
        if (!digger.TryRestoreUnderfoot())
        {
            Debug.Log("[MINE] 발밑이 파여 있지 않아 되메울 것이 없습니다.", this);
            return;
        }

        RestoresLeft--;
        RestoresChanged?.Invoke(RestoresLeft);

        Debug.Log($"[MINE] 복구 — 남은 블록 {RestoresLeft}/{TotalRestores}개", this);
    }

    private void TryHint(MineDigger digger)
    {
        if (digger.HintUsed)
        {
            Debug.Log("[MINE] 힌트를 이미 썼습니다. 한 사람당 1회입니다.", this);
            return;
        }

        if (grid.TargetCells == null)
        {
            Debug.LogWarning($"{nameof(MineGame)}: 도안이 없어 힌트를 보여줄 수 없습니다.", this);
            return;
        }

        digger.MarkHintUsed();

        _hintTimer = hintSeconds;
        if (view != null) view.SetShowTarget(true);

        Debug.Log($"[MINE] 힌트 — {hintSeconds:0}초 동안 보여줍니다. " +
                  $"그동안에도 턴 시간은 흐릅니다.", this);
    }

    private void TickHint()
    {
        if (_hintTimer <= 0f) return;

        _hintTimer -= Time.deltaTime;
        if (_hintTimer > 0f) return;

        _hintTimer = 0f;

        // 턴 중일 때만 다시 감춘다. 다른 단계는 각자 알아서 표시를 정한다.
        if (State == MineState.Turn && view != null) view.SetShowTarget(false);
    }

    // ------------------------------------------------------------
    // 단계 전환

    private void EnterReveal()
    {
        SetState(MineState.Reveal);
        _timer = revealSeconds;
        _hintTimer = 0f;

        TurnNumber = 0;
        SetOnlyDiggerActive(-1);          // 보는 시간이지 파는 시간이 아니다
        if (view != null) view.SetShowTarget(true);

        Debug.Log($"[MINE] 목표 공개 {revealSeconds:0}초 — " +
                  $"{(string.IsNullOrEmpty(TargetName) ? "(도안 없음)" : TargetName)} · " +
                  $"복구 {RestoresLeft}개", this);
    }

    private void EnterTurn(int turnNumber)
    {
        SetState(MineState.Turn);
        _timer = turnSeconds;
        _hintTimer = 0f;

        TurnNumber = turnNumber;

        // 공개가 끝났으니 그림을 감춘다. 여기부터는 기억으로 그린다. (MINE.md 2장)
        if (view != null) view.SetShowTarget(false);

        // 사람이 모자라면 같은 사람이 여러 턴을 돈다. 혼자 테스트할 때가 그렇다.
        int index = (turnNumber - 1) % Mathf.Max(1, diggers.Length);
        SetOnlyDiggerActive(index);

        Debug.Log($"[MINE] {turnNumber}/{TotalTurns} 턴 시작 — {turnSeconds:0}초 · " +
                  $"복구 {RestoresLeft}개 · 힌트 {(HintAvailable ? "가능" : "사용함")}", this);

        TurnStarted?.Invoke(turnNumber);
    }

    private void EnterTurnGap()
    {
        // 빈 시간이 0 이면 들르지 않고 바로 다음 턴으로 간다.
        if (turnGapSeconds <= 0f)
        {
            EnterTurn(TurnNumber + 1);
            return;
        }

        SetState(MineState.TurnGap);
        _timer = turnGapSeconds;
        _hintTimer = 0f;

        SetOnlyDiggerActive(-1);
    }

    private void EnterFinished()
    {
        SetState(MineState.Finished);
        _timer = 0f;
        _hintTimer = 0f;

        TurnNumber = 0;
        SetOnlyDiggerActive(-1);

        // 결과는 맞은 칸과 틀린 칸을 색으로 구분해 보여준다. (MINE.md 7장)
        // MineGridView 가 이미 4색으로 칠하므로 켜기만 하면 된다.
        if (view != null) view.SetShowTarget(true);

        if (grid.TargetCells == null)
        {
            Debug.LogWarning($"{nameof(MineGame)}: 도안이 없어 채점하지 못했습니다. " +
                             $"MineGrid 의 Target 을 지정하세요.", this);
            return;
        }

        Result = _similarity.Evaluate(grid.Cells, grid.TargetCells);
        Success = Result.Percent >= successThreshold;

        int score = Mathf.Clamp(Mathf.RoundToInt(Result.Percent), 0, 100);
        string label = string.IsNullOrEmpty(TargetName) ? "(이름 없음)" : TargetName;

        Debug.Log($"[MINE] 끝 — {label} · {(Success ? "성공" : "실패")} · {Result} · " +
                  $"복구 {TotalRestores - RestoresLeft}개 씀", this);

        Finished?.Invoke(Success, score);
        Report(Success, score);
    }

    private void SetState(MineState next)
    {
        State = next;
        StateChanged?.Invoke(next);
    }

    /// <summary>
    /// 지정한 사람만 팔 수 있게 한다. -1 이면 아무도 못 판다.
    ///
    /// 컴포넌트를 꺼버리지 않고 <see cref="MineDigger.DiggingAllowed"/> 로 막는 이유는
    /// **꺼진 동안에도 입력을 계속 읽어서 버려야** 하기 때문이다.
    /// 자세한 것은 MineDigger 쪽 주석에 적어두었다.
    /// </summary>
    private void SetOnlyDiggerActive(int index)
    {
        CurrentDigger = null;
        if (diggers == null) return;

        for (int i = 0; i < diggers.Length; i++)
        {
            if (diggers[i] == null) continue;

            bool active = i == index;
            diggers[i].DiggingAllowed = active;
            if (active) CurrentDigger = diggers[i];
        }
    }

    private void Report(bool success, int score)
    {
        // 이 한 줄이 로비와 이어지는 유일한 지점이다. (GAME_STRUCTURE.md 4장)
        if (NetworkServiceLocator.IsReady)
        {
            NetworkServiceLocator.Current.ReportMiniGameResult(success, score);
            return;
        }

        Debug.LogWarning(
            $"{nameof(MineGame)}: 네트워크 서비스가 없어 결과를 보고하지 못했습니다. " +
            $"Develop 씬에서 혼자 테스트하는 중이라면 정상입니다.", this);
    }
}