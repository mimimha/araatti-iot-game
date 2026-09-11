using UnityEngine;

/// <summary>
/// 플레이어가 휘두르면 **발밑 칸**을 판다.
///
/// MINE.md 4장 — 한 번 휘두르면 한 칸. 강도도 콤보도 없다.
/// MINE.md 6장 — 조준은 발밑. 걸어다니는 것이 곧 조준이다.
///
/// 하는 일은 이것 하나다. 언제 팔 수 있는지는 <see cref="MineGame"/> 이 정하고,
/// 채점도 그쪽에서 한다.
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

    private IPlayerController _controller;

    /// <summary>
    /// 지금 팔 수 있는가. <see cref="MineGame"/> 이 턴에 맞춰 켜고 끈다.
    ///
    /// MineGame 없이 혼자 테스트할 수 있도록 **기본값은 true** 다.
    /// </summary>
    public bool DiggingAllowed { get; set; } = true;

    /// <summary>이번 판에서 이 플레이어가 휘두른 횟수. (자기 턴에 휘두른 것만)</summary>
    public int TotalSwings { get; private set; }

    /// <summary>이번 판에서 이 플레이어가 실제로 판 칸 수. (헛스윙 제외)</summary>
    public int TotalDigs { get; private set; }

    private void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<MineGrid>();

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
    public void ResetStats()
    {
        TotalSwings = 0;
        TotalDigs = 0;
    }

    private void Update()
    {
        if (_controller == null || grid == null) return;

        HandleSwing();
    }

    private void HandleSwing()
    {
        // 스윙(F)은 오른손에 매핑돼 있다. 기기를 1대만 들면 Right 가 Left 와 같은 객체라
        // 두 번째 호출은 false 가 되어 중복으로 파이지 않는다.
        //
        // ⚠ `||` 가 아니라 `|` 다. `||` 는 앞이 true 면 뒤를 부르지 않는데,
        //   ConsumeSwing 은 부를 때 상태를 지우므로 안 부르면 다음 프레임에 한 번 더 파인다.
        //
        // ⚠ **내 턴이 아니어도 이 줄은 반드시 지나가야 한다.** 안 읽으면 그 스윙이
        //   지워지지 않고 남아 있다가, 내 턴이 시작되는 순간 묵은 입력이 한 칸을 판다.
        //   그래서 읽기는 하되 아래에서 버린다.
        bool swung = _controller.Left.ConsumeSwing() | _controller.Right.ConsumeSwing();

        if (!DiggingAllowed) return;    // 읽고 버린다
        if (!swung) return;

        TotalSwings++;

        if (!grid.WorldToCell(transform.position, out int x, out int y)) return;   // 격자 밖
        if (!grid.Dig(x, y)) return;                                               // 이미 파인 칸

        TotalDigs++;
    }
}