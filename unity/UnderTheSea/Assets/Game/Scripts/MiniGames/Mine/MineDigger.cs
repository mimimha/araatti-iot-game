using UnityEngine;

/// <summary>
/// 플레이어가 휘두르면 **발밑 칸**을 판다.
///
/// MINE.md 4장 — 한 번 휘두르면 한 칸. 강도도 콤보도 없다.
/// MINE.md 조준 방식 — 발밑(A안). 걸어다니는 것이 곧 조준이다.
///
/// 입력은 공용 IoT 경계(<see cref="IPlayerController"/>)만 쓴다.
/// 키보드로 테스트할 때는 <c>KeyboardPlayerController</c> 를 붙인다.
///
/// ⚠ 키보드의 손 매핑에 주의한다. 같은 버튼이라도 손이 다르다.
///     스윙(F)  → **오른손** 기기
///     버튼1(C) → **왼손** 기기
///   왼손만 읽거나 오른손만 읽으면 한쪽이 영영 안 들어온다.
///
/// ⚠ Consume 계열은 한 번 읽으면 스스로 지운다. **한 프레임에 두 곳에서 부르면
///   한쪽이 놓친다.** 광산에서 이것들을 읽는 곳은 이 컴포넌트 하나여야 한다.
/// </summary>
public class MineDigger : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 찾는다. 목표 도안도 이 격자에서 읽는다.")]
    [SerializeField] private MineGrid grid;

    [Tooltip("IPlayerController 를 구현한 컴포넌트. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private MonoBehaviour playerControllerSource;

    [Header("임시 확인용 — 2단계 한정")]
    [Tooltip("켜면 왼손 버튼1(키보드 C)로 지금까지 판 것을 채점해 로그로 남긴다.\n" +
             "복구 블록을 만드는 4단계에서 이 키를 돌려주고 이 기능은 지운다.")]
    [SerializeField] private bool scoreOnButton1 = true;

    [Header("측정 (MINE.md 12장 — 격자 크기 확정용)")]
    [Tooltip("켜면 스윙 횟수를 세어 구간마다 로그로 남긴다. 30초에 몇 번 휘두를 수 있는지 잰다.")]
    [SerializeField] private bool measureSwingRate = true;

    [Tooltip("측정 구간(초). MINE.md 의 턴 시간과 맞춘다.")]
    [SerializeField, Min(1f)] private float measureWindow = 30f;

    private IPlayerController _controller;
    private IMineSimilarity _similarity;

    private int _swingsInWindow;
    private int _digsInWindow;
    private float _windowElapsed;
    private bool _measuring;

    /// <summary>이번 판에서 이 플레이어가 휘두른 총 횟수.</summary>
    public int TotalSwings { get; private set; }

    /// <summary>이번 판에서 이 플레이어가 실제로 판 칸 수. (헛스윙 제외)</summary>
    public int TotalDigs { get; private set; }

    private void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<MineGrid>();

        _controller = playerControllerSource as IPlayerController
                      ?? GetComponent<IPlayerController>()
                      ?? GetComponentInParent<IPlayerController>();

        // 판정 방식은 나중에 갈아끼운다. (MINE.md 7장)
        _similarity = new MineIoUSimilarity();

        if (grid == null)
            Debug.LogError($"{nameof(MineDigger)}: MineGrid 를 찾지 못했습니다.", this);

        if (_controller == null)
            Debug.LogError($"{nameof(MineDigger)}: IPlayerController 를 찾지 못했습니다. " +
                           $"KeyboardPlayerController 를 붙이거나 참조를 지정하세요.", this);
    }

    private void Update()
    {
        if (_controller == null || grid == null) return;

        TickMeasure();
        HandleSwing();
        HandleScoreRequest();
    }

    private void HandleSwing()
    {
        // 스윙(F)은 오른손에 매핑돼 있다. 기기를 1대만 들면 Right 가 Left 와 같은 객체라
        // 두 번째 호출은 false 가 되어 중복으로 파이지 않는다.
        //
        // ⚠ `||` 가 아니라 `|` 다. `||` 는 앞이 true 면 뒤를 부르지 않는데,
        //   ConsumeSwing 은 부를 때 상태를 지우므로 안 부르면 다음 프레임에 한 번 더 파인다.
        bool swung = _controller.Left.ConsumeSwing() | _controller.Right.ConsumeSwing();
        if (!swung) return;

        TotalSwings++;
        _swingsInWindow++;
        StartMeasureIfNeeded();

        if (!grid.WorldToCell(transform.position, out int x, out int y)) return;   // 격자 밖에서 휘두름
        if (!grid.Dig(x, y)) return;                                               // 이미 파인 칸

        TotalDigs++;
        _digsInWindow++;
    }

    /// <summary>
    /// 임시 채점. 2단계에서 "점수가 나온다"를 확인하기 위한 것이다.
    /// 3단계에서 MineGame 이 턴을 관리하게 되면 채점은 그쪽으로 옮기고 이 메서드는 지운다.
    /// </summary>
    private void HandleScoreRequest()
    {
        if (!scoreOnButton1) return;

        // 버튼1(C)은 **왼손**에 매핑돼 있다. 오른손 버튼1 은 Space 인데
        // 그건 MovePlayerInput 의 점프와 겹치므로 왼손만 읽는다.
        if (!_controller.Left.ConsumeButton1Press()) return;

        if (grid.TargetCells == null)
        {
            Debug.LogWarning($"{nameof(MineDigger)}: 목표 도안이 없어 채점할 수 없습니다. " +
                             $"MineGrid 의 Target 을 지정하세요.", this);
            return;
        }

        MineSimilarityResult r = _similarity.Evaluate(grid.Cells, grid.TargetCells);
        string label = grid.Target != null ? grid.Target.displayName : "(이름 없음)";

        Debug.Log($"[MINE-SCORE] {label} — {r}", this);
    }

    private void StartMeasureIfNeeded()
    {
        if (!measureSwingRate || _measuring) return;

        _measuring = true;
        _windowElapsed = 0f;
    }

    private void TickMeasure()
    {
        if (!_measuring) return;

        _windowElapsed += Time.deltaTime;
        if (_windowElapsed < measureWindow) return;

        Debug.Log($"[MINE-MEASURE] {measureWindow:0}초 동안 스윙 {_swingsInWindow}회 · " +
                  $"실제로 판 칸 {_digsInWindow}개 " +
                  $"(초당 {_swingsInWindow / measureWindow:0.0}회) — " +
                  $"격자 {grid.Size}×{grid.Size} = {grid.CellCount}칸", this);

        _measuring = false;
        _swingsInWindow = 0;
        _digsInWindow = 0;
        _windowElapsed = 0f;
    }
}