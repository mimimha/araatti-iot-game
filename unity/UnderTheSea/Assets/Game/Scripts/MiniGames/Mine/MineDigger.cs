using UnityEngine;

/// <summary>
/// 플레이어 한 명의 손. **휘두르면 발밑을 파고, 누른 버튼을 적어둔다.**
///
/// MINE.md 4장 — 한 번 휘두르면 한 칸. 강도도 콤보도 없다.
/// MINE.md 6장 — 조준은 발밑. 걸어다니는 것이 곧 조준이다. 되메우기도 발밑이다.
///
/// **판단은 여기서 하지 않는다.** 복구 블록이 몇 개 남았는지, 지금이 누구 턴인지는
/// <see cref="MineGame"/> 이 안다. 이 컴포넌트는 눌렸다는 사실만 적어두고,
/// MineGame 이 <see cref="ConsumeRestoreRequest"/> 로 가져간다.
/// 그래야 화살표가 MineGame → MineDigger 한 방향으로 유지된다.
///
/// 힌트 사용 여부만 여기 있다. **사람에 붙는 값**이기 때문이다.
/// 복구 개수는 팀 공용이라 MineGame 에 있다. (MINE.md 2·4장)
///
/// 정확히는 **자기 턴에 1회**다. 턴 수 = 사람 수이므로 실제 게임에서는
/// "1인 1회" 와 같은 말이지만, 혼자 테스트할 때는 한 사람이 네 턴을 다 돌기 때문에
/// 사람에만 매어두면 첫 턴에 쓰고 끝난다. 턴이 시작될 때 MineGame 이 되돌려준다.
///
/// ⚠ 키보드의 손 매핑에 주의한다. 같은 버튼이라도 손이 다르다.
///     스윙(F)  → **오른손** 기기
///     버튼1(C) → **왼손** 기기
///     버튼2(V) → **왼손** 기기
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

    [Tooltip("돌을 깼을 때 폴짝 뛰게 한다. 비워두면 찾는다. 없어도 게임은 돈다.")]
    [SerializeField] private MineJump jump;

    private IPlayerController _controller;

    private bool _restoreRequested;
    private bool _hintRequested;

    /// <summary>
    /// 지금 팔 수 있는가. <see cref="MineGame"/> 이 턴에 맞춰 켜고 끈다.
    ///
    /// MineGame 없이 혼자 테스트할 수 있도록 **기본값은 true** 다.
    /// </summary>
    public bool DiggingAllowed { get; set; } = true;

    /// <summary>이번 판에 힌트를 썼는가. **한 사람당 1회.** (MINE.md 2장)</summary>
    public bool HintUsed { get; private set; }

    /// <summary>이번 판에서 이 플레이어가 휘두른 횟수. (자기 턴에 휘두른 것만)</summary>
    public int TotalSwings { get; private set; }

    /// <summary>이번 판에서 이 플레이어가 실제로 판 칸 수. (헛스윙 제외)</summary>
    public int TotalDigs { get; private set; }

    /// <summary>이번 판에서 이 플레이어가 단단한 돌에 금을 낸 횟수.</summary>
    public int TotalCracks { get; private set; }

    private void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<MineGrid>();

        if (jump == null) jump = GetComponent<MineJump>();
        if (jump == null) jump = GetComponentInParent<MineJump>();

        _controller = playerControllerSource as IPlayerController
                      ?? GetComponent<IPlayerController>()
                      ?? GetComponentInParent<IPlayerController>();

        if (grid == null)
            Debug.LogError($"{nameof(MineDigger)}: MineGrid 를 찾지 못했습니다.", this);

        if (_controller == null)
            Debug.LogError($"{nameof(MineDigger)}: IPlayerController 를 찾지 못했습니다. " +
                           $"KeyboardPlayerController 를 붙이거나 참조를 지정하세요.", this);
    }

    /// <summary>판을 새로 시작할 때 MineGame 이 불러준다.</summary>
    public void ResetForNewGame()
    {
        TotalSwings = 0;
        TotalDigs = 0;
        TotalCracks = 0;
        HintUsed = false;
        _restoreRequested = false;
        _hintRequested = false;
    }

    private void Update()
    {
        if (_controller == null || grid == null) return;

        ReadInput();
    }

    private void ReadInput()
    {
        // ⚠ 세 가지를 **먼저 전부 읽는다.** 내 턴이 아니어도 읽는다.
        //   안 읽으면 그 입력이 지워지지 않고 남아 있다가, 내 턴이 시작되는 순간
        //   묵은 입력이 한꺼번에 터진다. 읽어서 버리는 것과 안 읽는 것은 다르다.
        //
        // ⚠ 동작은 `||` 가 아니라 `|` 다. `||` 는 앞이 true 면 뒤를 부르지 않는데,
        //   TryConsumeMotion 은 부를 때 상태를 지우므로 안 부르면 한 번 더 파인다.
        //   기기를 1대만 들면 Right 가 Left 와 같은 객체라 두 번째 호출은 false 다.
        bool swung = ConsumeDigSwing(_controller.Left) | ConsumeDigSwing(_controller.Right);

        // 버튼1·2 는 **왼손만** 읽는다. 오른손 버튼1 은 Space 인데
        // 그건 MovePlayerInput 의 점프와 겹친다. (MINE.md 8장 표)
        bool restore = _controller.Left.ConsumeButton1Press();
        bool hint = _controller.Left.ConsumeButton2Press();

        // ⚠ 여기서 버린다. 스윙만이 아니라 **복구·힌트 요청도 같이 버려진다.**
        //   힌트를 보는 동안 MineGame 이 이 스위치를 내리는데, 그때 되메우기까지
        //   막히는 것은 의도한 것이다. 발밑이 안 보이는 채로 판이 바뀌면 안 된다.
        //   되메우기를 힌트 중에도 받고 싶어지면 이 return **위에서** 적어야 한다.
        if (!DiggingAllowed) return;

        if (swung) Dig();

        // 눌렸다는 것만 적어둔다. 쓸 수 있는지는 MineGame 이 판단한다.
        if (restore) _restoreRequested = true;
        if (hint) _hintRequested = true;
    }

    private static bool ConsumeDigSwing(IHandDevice hand)
    {
        return hand != null
               && hand.TryConsumeMotion(out HandMotion motion)
               && motion.Type == HandMotionType.VerticalSwing;
    }

    private void Dig()
    {
        TotalSwings++;

        if (!grid.WorldToCell(transform.position, out int x, out int y)) return;   // 격자 밖

        // 무른 돌은 한 번에 깨지고, 단단한 돌은 처음엔 금만 간다.
        // 키보드로는 F 를 두 번 누르는 것이 곧 두 번 치는 것이다. (MINE.md 4장)
        switch (grid.Hit(x, y))
        {
            case MineHitResult.Broke:
                TotalDigs++;

                // 발밑이 꺼졌으니 폴짝 뛴다. 손맛일 뿐 규칙은 아니다.
                // 금만 갔을 때는 안 뛴다 — 아직 발밑이 그대로이기 때문이다.
                if (jump != null) jump.Hop();
                break;

            case MineHitResult.Cracked:
                TotalCracks++;
                break;
        }
    }

    // ------------------------------------------------------------
    // MineGame 이 가져가는 것들

    /// <summary>복구 요청이 있었는지 가져가고 비운다.</summary>
    public bool ConsumeRestoreRequest()
    {
        bool requested = _restoreRequested;
        _restoreRequested = false;
        return requested;
    }

    /// <summary>힌트 요청이 있었는지 가져가고 비운다.</summary>
    public bool ConsumeHintRequest()
    {
        bool requested = _hintRequested;
        _hintRequested = false;
        return requested;
    }

    /// <summary>힌트를 썼다고 표시한다. 쓸 수 있는지는 MineGame 이 이미 확인했다.</summary>
    public void MarkHintUsed() => HintUsed = true;

    /// <summary>
    /// 새 턴이 시작될 때 힌트를 되돌려준다. MineGame 이 불러준다.
    ///
    /// 실제 게임에서는 한 사람이 한 턴만 돌므로 아무것도 안 바뀐다.
    /// 혼자 테스트할 때 한 사람이 네 턴을 도는 경우에만 의미가 있다.
    /// </summary>
    public void ResetHintForNewTurn() => HintUsed = false;

    /// <summary>
    /// 발밑 칸을 되메운다. 성공했으면 true.
    ///
    /// **안 파인 칸이면 false 를 돌려준다.** MineGame 이 이 값을 보고 블록을 깎으므로,
    /// 헛으로 누른 것 때문에 블록이 없어지지 않는다.
    /// </summary>
    public bool TryRestoreUnderfoot()
    {
        if (grid == null) return false;
        if (!grid.WorldToCell(transform.position, out int x, out int y)) return false;

        return grid.Restore(x, y);
    }
}
