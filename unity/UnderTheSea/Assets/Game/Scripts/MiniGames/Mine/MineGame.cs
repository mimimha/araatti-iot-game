using System;
using UnityEngine;

/// <summary>판이 지금 어느 단계인가. (MINE.md 3장)</summary>
public enum MineState
{
    /// <summary>아직 시작 안 함. StartGame() 을 기다린다.</summary>
    Ready,

    /// <summary>3 2 1 카운트다운. 아직 아무것도 안 보여주고 아무도 못 판다.</summary>
    Countdown,

    /// <summary>목표 그림을 보여주는 중.</summary>
    Reveal,

    /// <summary>누군가의 턴. 그 사람만 팔 수 있다.</summary>
    Turn,

    /// <summary>턴 사이의 빈 시간. 앞사람이 뭘 했는지 보는 시간.</summary>
    TurnGap,

    /// <summary>끝. 채점하고 결과를 보여준 상태.</summary>
    Finished,
}

/// <summary>판정 방식. (MINE.md 7장)</summary>
public enum MineJudge
{
    /// <summary>모양이 얼마나 닮았는가. 위치를 맞춘 뒤 거리로 잰다. **기본값.**</summary>
    Shape,

    /// <summary>정확히 같은 칸을 팠는가. 비교용으로 남겨둔 옛 방식.</summary>
    Cells,
}

/// <summary>
/// 판의 진행을 맡는다. **초를 세고, 턴을 넘기고, 복구와 힌트를 관리하고,
/// 밝기를 정하고, 끝나면 채점한다.**
///
///     공개 7초 → [턴 30초 → 빈 시간] × 인원 → 채점 → 로비 보고
///
/// 땅(<see cref="MineGrid"/>)과 화면(<see cref="MineGridView"/>)과
/// 어둠(<see cref="MineVision"/>)과 채점(<see cref="IMineSimilarity"/>)은
/// 이미 있는 것을 **쓰기만 한다.** 고치지 않는다.
///
/// 모양은 <c>ShipCoopGame</c> 을 따랐다. 코루틴이 아니라 상태 + Update 인 이유는
/// **밖에서 상태를 읽을 수 있어야** HUD 가 붙고 네트워크 동기화가 되기 때문이다.
///
/// **복구 개수는 여기 있고, 힌트 사용 여부는 <see cref="MineDigger"/> 에 있다.**
/// 복구는 팀 공용이라 판마다 하나, 힌트는 1인 1회라 사람마다 하나이기 때문이다.
/// (MINE.md 2·4장)
///
/// 이 단계에서 하지 않는 것 — 탑뷰 연출(6단계),
/// 진짜 HUD(11장 8단계), 네트워크 턴 소유권(10단계).
/// </summary>
public class MineGame : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 찾는다.")]
    [SerializeField] private MineGrid grid;

    [Tooltip("비워두면 격자에서 찾는다. 공개와 숨김에 쓴다.")]
    [SerializeField] private MineGridView view;

    [Tooltip("비워두면 씬에서 찾는다. 어둠과 랜턴을 맡는다. (MINE.md 6장)")]
    [SerializeField] private MineVision vision;

    [Tooltip("비워두면 씬에서 찾는다. 시점 전환을 맡는다. (MINE.md 6장)")]
    [SerializeField] private MineCamera cameraRig;

    [Tooltip("파는 사람들. 개발 중에는 하나만 넣어도 되고, 그러면 혼자 여러 턴을 돈다.")]
    [SerializeField] private MineDigger[] diggers;

    [Header("판 설정 (MINE.md 2장 — 전부 시작값이고 조정 대상)")]
    [Tooltip("인원 = 턴 수. 1~4명.")]
    [SerializeField, Range(1, 4)] private int playerCount = 4;

    [Tooltip("공개 전 3 2 1 카운트다운(초). 0 이면 건너뛴다. 마음의 준비를 위한 시간이다.")]
    [SerializeField, Min(0f)] private float countdownSeconds = 3f;

    [Tooltip("목표 그림을 보여주는 시간(초).")]
    [SerializeField, Min(0f)] private float revealSeconds = 7f;

    [Tooltip("한 턴의 채굴 시간(초).")]
    [SerializeField, Min(1f)] private float turnSeconds = 30f;

    [Tooltip("턴과 턴 사이의 빈 시간(초). 앞사람 결과를 볼 시간.\n" +
             "0 이면 바로 다음 턴이 시작된다. MINE.md 12장의 조정 항목이다.")]
    [SerializeField, Min(0f)] private float turnGapSeconds = 0f;

    [Tooltip("이 값 이상이면 성공. (MINE.md 7장)")]
    [SerializeField, Range(0f, 100f)] private float successThreshold = 60f;

    [Header("판정 (MINE.md 7장)")]
    [Tooltip("모양 — 위치를 맞춘 뒤 얼마나 닮았는지 잰다. 기본값.\n" +
             "칸  — 정확히 같은 칸을 팠는지 잰다. 비교용.")]
    [SerializeField] private MineJudge judge = MineJudge.Shape;

    [Tooltip("몇 칸까지 어긋남을 봐줄 것인가. 0칸이면 만점, 이 값 이상 떨어지면 0점.")]
    [SerializeField, Min(0.5f)] private float shapeTolerance = 2f;

    [Tooltip("위치를 맞출 때 최대 몇 칸까지 밀 수 있는가.\n" +
             "크게 잡을수록 '어디에 그렸는가' 를 안 보게 된다.")]
    [SerializeField, Min(0)] private int maxAlign = 4;

    [Tooltip("진단용. 점수는 안 바꾸고 원점이 몇 칸 밀렸는지만 따로 재서 로그에 남긴다. 0 이면 재지 않는다. 모양을 못 그린 것과 자리를 잘못 잡은 것을 가르는 데 쓴다.")]
    [SerializeField, Min(0)] private int diagnosticAlign = 4;

    [Header("복구와 힌트 (MINE.md 2·4장)")]
    [Tooltip("이번 판에 주어지는 복구 블록 수. 인원과 무관한 고정값이다.\n" +
             "블록은 팀 공용이다. 7번 밸런싱에서 조정할 값이다.")]
    [SerializeField, Min(0)] private int restoreBlocks = 5;

    [Tooltip("힌트로 목표를 다시 보여주는 시간(초).\n" +
             "⚠ 보는 동안에도 턴 시간은 계속 흐른다. 그것이 힌트의 대가다.")]
    [SerializeField, Min(0.5f)] private float hintSeconds = 3f;

    [Header("개발용")]
    [Tooltip("켜면 재생하자마자 판이 시작된다. 실제로는 로비가 불러준다.")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("돌 배치 시드. 0 이면 매판 다르게. 값을 넣으면 같은 판이 반복돼 비교하기 좋다.")]
    [SerializeField] private int boardSeed = 0;

    private IMineSimilarity _similarity;

    // 턴이 시작할 때의 누적값. 끝날 때 빼면 그 턴에 한 일이 나온다.
    // 혼자 테스트할 때는 한 사람이 네 턴을 다 도므로 누적값만으로는 턴별을 못 본다.
    private int _turnStartDigs;
    private int _turnStartSwings;
    private int _turnStartCracks;
    private int _turnStartRestores;

    /// <summary>턴별 요약. 판이 끝날 때 한꺼번에 다시 찍는다.</summary>
    private readonly System.Collections.Generic.List<string> _turnLog =
        new System.Collections.Generic.List<string>();
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

    /// <summary>이번 판에 주어진 복구 블록 전체 수. **인원과 무관한 고정값이다.**</summary>
    public int TotalRestores => restoreBlocks;

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
        if (vision == null) vision = FindAnyObjectByType<MineVision>();
        if (cameraRig == null) cameraRig = FindAnyObjectByType<MineCamera>();

        // 판정 방식은 인스펙터에서 고른다. (MINE.md 7장)
        _turnLog.Clear();

        _similarity = judge == MineJudge.Shape
            ? new MineShapeSimilarity(shapeTolerance, maxAlign)
            : (IMineSimilarity)new MineIoUSimilarity();

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

        // 돌 배치는 무작위지만 시드로 만든다. 네트워크가 붙으면 이 값을 나눠 갖는다.
        // 그래야 4명이 같은 판을 본다. (MineGrid.Seed)
        grid.ResetAll(boardSeed != 0 ? boardSeed : Environment.TickCount);

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

        EnterCountdown();
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
            case MineState.Countdown:
                // 셋을 다 세면 도안을 보여준다.
                EnterReveal();
                break;

            case MineState.Reveal:
                // 공개가 끝나면 그림을 숨기고 첫 턴을 시작한다.
                EnterTurn(1);
                break;

            case MineState.Turn:
                // 턴이 넘어가기 *전에* 적는다. 넘어간 뒤에는 CurrentDigger 가 바뀐다.
                LogTurnSummary();

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
            Debug.Log("[MINE] 힌트를 이미 썼습니다. 자기 턴에 1회입니다.", this);
            return;
        }

        if (grid.TargetCells == null)
        {
            Debug.LogWarning($"{nameof(MineGame)}: 도안이 없어 힌트를 보여줄 수 없습니다.", this);
            return;
        }

        digger.MarkHintUsed();

        _hintTimer = hintSeconds;

        // ⚠ 보는 동안에는 판을 건드리지 못한다. 카메라가 올라가 발밑이 안 보이는데,
        //   그 상태에서 휘둘러 파이면 어디를 팠는지도 모르고 파인다.
        //   되메우기도 같은 이유로 막힌다. 그림을 보는 동안 판은 멈춰 있는다.
        //   입력은 MineDigger 가 계속 읽어서 버린다. TickHint 가 다시 켜준다.
        digger.DiggingAllowed = false;
        if (view != null) { view.SetTargetOffset(Vector2Int.zero); view.SetOverlay(MineOverlay.Drawing); }

        // 어두우면 색을 바꿔봐야 안 보인다. 힌트 동안에는 판을 밝힌다. (MINE.md 6장)
        if (vision != null) vision.SetLit(true);

        // ⚠ 카메라도 올려야 한다. 낮으면 내 주변 도안만 보인다.
        //   밝히기만 하고 이걸 빠뜨리면 힌트가 반쪽이 된다.
        if (cameraRig != null) cameraRig.ShowBoard();

        Debug.Log($"[MINE] 힌트 — {hintSeconds:0}초 동안 보여줍니다. " +
                  $"그동안에도 턴 시간은 흐릅니다.", this);
    }

    private void TickHint()
    {
        if (_hintTimer <= 0f) return;

        _hintTimer -= Time.deltaTime;
        if (_hintTimer > 0f) return;

        _hintTimer = 0f;

        // 턴 중일 때만 되돌린다. 다른 단계는 각자 알아서 표시를 정한다.
        if (State != MineState.Turn) return;

        if (view != null) view.SetOverlay(MineOverlay.None);
        if (vision != null) vision.SetLit(false);   // 다시 어두워지고 랜턴이 켜진다

        if (CurrentDigger != null)
        {
            // 힌트 동안 막아둔 손을 돌려준다.
            //
            // 턴이 넘어가면서 힌트가 끝난 경우에는 여기까지 오지 않는다.
            // Enter* 가 _hintTimer 를 0 으로 지우고 SetOnlyDiggerActive 로
            // 누가 팔지를 다시 정하기 때문에, 남은 true 가 새는 일은 없다.
            CurrentDigger.DiggingAllowed = true;

            if (cameraRig != null)
                cameraRig.FollowPlayer(CurrentDigger.transform);   // 다시 광산 안으로
        }
    }

    // ------------------------------------------------------------
    // 단계 전환

    /// <summary>
    /// 3 2 1 을 세는 동안. 판은 비추되 **도안은 아직 안 보여준다.**
    ///
    /// 공개 7초는 그림을 외우는 시간이라 1초도 아깝다. 카메라가 올라가고
    /// 화면이 밝아지는 동안 이미 도안이 떠 있으면 그만큼 까먹는다.
    /// 카운트다운에 그 준비를 다 끝내두고, 0 이 되는 순간 도안만 켠다.
    /// </summary>
    private void EnterCountdown()
    {
        // 0 으로 꺼두면 예전처럼 바로 공개로 간다.
        if (countdownSeconds <= 0f) { EnterReveal(); return; }

        SetState(MineState.Countdown);
        _timer = countdownSeconds;
        _hintTimer = 0f;

        TurnNumber = 0;
        SetOnlyDiggerActive(-1);

        // 판은 보이되 도안은 없다. 0 이 되면 EnterReveal 이 Drawing 으로 바꾼다.
        //
        // 돌 종류는 감춘다. 아직 아무도 못 파는 시간인데 단단한 돌이 어두운 얼룩으로
        // 먼저 드러나면 판이 지저분해 보이고 어디가 단단한지도 미리 알려준다.
        if (view != null)
        {
            view.SetTargetOffset(Vector2Int.zero);
            view.SetOverlay(MineOverlay.None);
            view.SetUniformStone(true);
        }
        if (vision != null) { vision.SetLit(true); vision.Follow(null); }
        if (cameraRig != null) cameraRig.ShowBoard();

        Debug.Log($"[MINE] 카운트다운 {countdownSeconds:0}초", this);
    }

    private void EnterReveal()
    {
        SetState(MineState.Reveal);
        _timer = revealSeconds;
        _hintTimer = 0f;

        TurnNumber = 0;
        SetOnlyDiggerActive(-1);          // 보는 시간이지 파는 시간이 아니다
        if (view != null)
        {
            view.SetTargetOffset(Vector2Int.zero);
            view.SetOverlay(MineOverlay.Drawing);
            view.SetUniformStone(false);   // 여기부터는 돌 종류를 다시 보여준다
        }
        // 그림을 봐야 하는 시간이므로 밝게. 랜턴은 필요 없다.
        if (vision != null) { vision.SetLit(true); vision.Follow(null); }

        // 그림 전체를 봐야 외울 수 있다. (MINE.md 6장)
        if (cameraRig != null) cameraRig.ShowBoard();

        Debug.Log($"[MINE] 목표 공개 {revealSeconds:0}초 — " +
                  $"{(string.IsNullOrEmpty(TargetName) ? "(도안 없음)" : TargetName)} · " +
                  $"복구 {RestoresLeft}개 · 돌 시드 {grid.Seed}", this);
    }

    private void EnterTurn(int turnNumber)
    {
        SetState(MineState.Turn);
        _timer = turnSeconds;
        _hintTimer = 0f;

        TurnNumber = turnNumber;

        // 공개가 끝났으니 그림을 감춘다. 여기부터는 기억으로 그린다. (MINE.md 2장)
        if (view != null) view.SetOverlay(MineOverlay.None);

        // 사람이 모자라면 같은 사람이 여러 턴을 돈다. 혼자 테스트할 때가 그렇다.
        int index = (turnNumber - 1) % Mathf.Max(1, diggers.Length);
        SetOnlyDiggerActive(index);

        // 힌트는 **자기 턴에 1회**다. 턴 수 = 사람 수라 실제 게임에서는 1인 1회와
        // 같은 말이지만, 혼자 테스트할 때는 한 사람이 네 턴을 다 돌기 때문에
        // 되돌려주지 않으면 첫 턴에 쓰고 끝난다. (MINE.md 2장)
        if (CurrentDigger != null) CurrentDigger.ResetHintForNewTurn();

        // 어둠 + 지금 턴인 사람을 따라가는 랜턴. (MINE.md 6장)
        // Follow 는 SetOnlyDiggerActive 뒤에 불러야 CurrentDigger 가 정해져 있다.
        if (vision != null)
        {
            vision.SetLit(false);
            vision.Follow(CurrentDigger != null ? CurrentDigger.transform : null);
        }

        // 광산 안으로 내려간다. 여기부터 전체가 안 보인다.
        if (cameraRig != null && CurrentDigger != null)
            cameraRig.FollowPlayer(CurrentDigger.transform);

        // 이 턴에 무엇을 했는지 재려고 눈금을 찍어둔다.
        _turnStartDigs = CurrentDigger != null ? CurrentDigger.TotalDigs : 0;
        _turnStartSwings = CurrentDigger != null ? CurrentDigger.TotalSwings : 0;
        _turnStartCracks = CurrentDigger != null ? CurrentDigger.TotalCracks : 0;
        _turnStartRestores = RestoresLeft;

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

        // 어두운 채로 둔다. 이 틈에 전체를 보여주면 시야 제한이 무의미해진다.
        if (vision != null) { vision.SetLit(false); vision.Follow(null); }
    }

    private void EnterFinished()
    {
        SetState(MineState.Finished);
        _timer = 0f;
        _hintTimer = 0f;

        TurnNumber = 0;
        SetOnlyDiggerActive(-1);

        // 결과는 맞은 칸과 틀린 칸을 색으로 구분해 보여준다. (MINE.md 7장)
        // 도안 보기와 다른 방식으로 칠한다.
        if (view != null) view.SetOverlay(MineOverlay.Result);

        // 판이 끝났으니 전체를 밝힌다. 여기가 이 게임의 하이라이트다. (MINE.md 3장 7번)
        if (vision != null) { vision.SetLit(true); vision.Follow(null); }

        // 카메라가 위로 올라가 완성된 그림을 처음으로 보여준다. (MINE.md 3장 7번)
        if (cameraRig != null) cameraRig.ShowBoard(finale: true);

        if (grid.TargetCells == null)
        {
            Debug.LogWarning($"{nameof(MineGame)}: 도안이 없어 채점하지 못했습니다. " +
                             $"MineGrid 의 Target 을 지정하세요.", this);
            return;
        }

        Result = _similarity.Evaluate(grid.Cells, grid.TargetCells, grid.Size);
        Success = Result.Percent >= successThreshold;

        int score = Mathf.Clamp(Mathf.RoundToInt(Result.Percent), 0, 100);
        string label = string.IsNullOrEmpty(TargetName) ? "(이름 없음)" : TargetName;

        Debug.Log($"[MINE] 끝 — {label} · {(Success ? "성공" : "실패")} · {Result} · " +
                  $"복구 {TotalRestores - RestoresLeft}개 씀", this);

        LogAlignmentDiagnosis();

        if (_turnLog.Count > 0)
        {
            string sep = System.Environment.NewLine + "  ";
            Debug.Log("[MINE] 턴별 정리" + sep + string.Join(sep, _turnLog), this);
        }

        Finished?.Invoke(Success, score);
        // 판정이 위치를 맞춰 채점했으니 화면도 같은 기준으로 칠한다. (MINE.md 7장)
        if (view != null) view.SetTargetOffset(Result.Alignment);
        Report(Success, score);
    }

    /// <summary>
    /// 이 턴에 한 일을 한 줄로 적는다. **턴이 넘어가기 전에** 불러야 한다.
    ///
    /// 누적값의 차이로 잰다. 혼자 테스트할 때는 한 사람이 네 턴을 다 돌기 때문에
    /// <see cref="MineDigger.TotalDigs"/> 같은 누적값만으로는 턴별을 볼 수 없다.
    /// </summary>
    private void LogTurnSummary()
    {
        if (CurrentDigger == null) return;

        int digs = CurrentDigger.TotalDigs - _turnStartDigs;
        int swings = CurrentDigger.TotalSwings - _turnStartSwings;
        int cracks = CurrentDigger.TotalCracks - _turnStartCracks;
        int restores = _turnStartRestores - RestoresLeft;

        // 헛스윙 = 휘둘렀는데 아무 일도 안 일어난 것. 조준이 답답한지를 본다.
        int missed = Mathf.Max(0, swings - digs - cracks);

        string line = $"P{TurnNumber}  판 것 {digs}칸 · 스윙 {swings}회 · 금 {cracks} · " +
                      $"헛스윙 {missed} · 복구 {restores}개";

        _turnLog.Add(line);
        Debug.Log($"[MINE] 턴 끝 — {line}", this);
    }

    /// <summary>
    /// **점수는 그대로 두고** 원점이 몇 칸 밀렸는지만 따로 잰다.
    ///
    /// 낮은 점수가 "모양을 못 그려서" 인지 "자리를 잘못 잡아서" 인지 구분하려는 것이다.
    /// 원인이 다르면 고칠 것도 다르다 — 앞쪽이면 턴 시간이나 도안 복잡도이고,
    /// 뒤쪽이면 공개 시간이나 첫 사람의 역할(3장)이다.
    ///
    /// ⚠ **채점기의 Alignment 를 그대로 쓰면 안 된다.** 그 값은 두 그림의
    ///   *무게중심 차이*지 최고점을 주는 자리가 아니다. 실제로 그 값만큼 밀었더니
    ///   점수가 71.5%에서 70.9%로 **떨어지는** 판이 나왔다.
    ///   여기서는 한도 안의 모든 자리를 직접 밀어보고 제일 높은 곳을 찾는다.
    /// </summary>
    private void LogAlignmentDiagnosis()
    {
        if (diagnosticAlign <= 0 || grid == null || grid.TargetCells == null) return;

        // 보정을 끈 채점기로 잰다. 안 끄면 채점기가 또 제 나름대로 밀어버린다.
        var probe = new MineShapeSimilarity(shapeTolerance, 0);

        int size = grid.Size;
        var dug = grid.Cells;
        var shifted = new bool[size * size];

        float best = -1f;
        Vector2Int bestShift = Vector2Int.zero;

        for (int dy = -diagnosticAlign; dy <= diagnosticAlign; dy++)
        {
            for (int dx = -diagnosticAlign; dx <= diagnosticAlign; dx++)
            {
                System.Array.Clear(shifted, 0, shifted.Length);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!dug[y * size + x]) continue;

                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;

                        shifted[ny * size + nx] = true;
                    }
                }

                float p = probe.Evaluate(shifted, grid.TargetCells, size).Percent;
                if (p <= best) continue;

                best = p;
                bestShift = new Vector2Int(dx, dy);
            }
        }

        if (bestShift == Vector2Int.zero)
        {
            Debug.Log($"[MINE] 진단 — 원점은 맞았다. 어디로 밀어도 더 안 오른다. " +
                      $"점수가 낮다면 모양 문제다. (최대 {diagnosticAlign}칸까지 밀어봄)", this);
            return;
        }

        Debug.Log($"[MINE] 진단 — 원점이 ({bestShift.x}, {bestShift.y}) 밀렸다. " +
                  $"맞추면 {best:0.0}% (지금 {Result.Percent:0.0}% · {best - Result.Percent:+0.0;-0.0}%p)", this);
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