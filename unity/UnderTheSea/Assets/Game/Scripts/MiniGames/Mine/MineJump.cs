using ithappy.Cute_Characters.Controller;
using UnityEngine;

/// <summary>
/// 발밑 돌을 깨면 캐릭터가 폴짝 뛴다.
///
/// **손맛이지 규칙이 아니다.** 파이는 칸도 점수도 바뀌지 않는다.
///
/// **캐릭터 에셋에 기대는 곳을 이 한 파일로 몰아둔다.** <see cref="MineDigger"/> 는
/// 이 컴포넌트만 알면 되고, 나중에 캐릭터가 바뀌면 여기만 고친다.
///
/// ⚠ **실행 순서가 이 기능의 전부다.**
///
/// 점프는 <c>CharacterMover</c> 가 매 프레임 읽는 bool 하나인데, 그 값을
/// <c>MovePlayerInput</c> 이 매 프레임 스페이스바 상태로 덮어쓴다.
/// 그래서 셋의 순서가 반드시 이래야 한다.
///
/// <code>
///   MovePlayerInput (-100)   스페이스바를 읽어 넣는다
///   MineJump        ( -50)   판 직후면 그 위에 덮어쓴다
///   CharacterMover  (   0)   읽어서 실제로 뛴다
/// </code>
///
/// 셋 다 0 이면 순서가 정해지지 않아 **점프가 될 때도 있고 안 될 때도 있다.**
/// MovePlayerInput 의 순서는 그 스크립트의 .meta 에 적어두었다. 남의 에셋이지만
/// 입력을 먼저 모으는 것뿐이라 다른 씬에 해가 없다.
///
/// ⚠ <b><c>m_JumpHeight</c> 는 뛰는 높이가 아니다.</b> 0.2 같은 작은 값은
///   <b>아예 뜨지 못한다.</b> 실측해서 확인했다.
///
///   <c>CharacterMover.CaculateGravity</c> 가 이렇게 더하기 때문이다.
/// <code>
///   땅에 붙어 있을 때   m_GravityAcelleration = Physics.gravity      (-9.81)
///   뛰라고 하면       m_GravityAcelleration += sqrt(h * 6 * 9.81)
/// </code>
///   즉 실제 초기 속도는 <b>sqrt(h*6*g) - g</b> 이다.
///   h = 0.2 면 3.43 - 9.81 = <b>-6.38</b> — 아래로 향해서 아예 안 뜨다.
///   <b>h 가 1.64 를 넘어야</b> 비로소 뜨기 시작한다.
///
///   정점 = (sqrt(h*6*g) - g)² / 2g 이므로,
///   h = 3 이면 약 0.6m(체공 0.71초), <b>h = 2.5 면 약 0.28m(체공 0.47초)</b> 이다.
///   광산은 <b>2.5</b> 를 쓴다 — 파인 칸 깊이(digDepth 0.25m)와 비슷해야
///   "한 칸 깊이만큼 폴짝" 으로 보이고, 더 높으면 느려진다.
/// </summary>
[DefaultExecutionOrder(-50)]
public class MineJump : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 같은 오브젝트와 부모에서 찾는다.")]
    [SerializeField] private CharacterMover mover;

    /// <summary>이번 프레임에 뛰라는 요청이 있었는가.</summary>
    private bool _requested;

    private void Awake()
    {
        if (mover == null) mover = GetComponent<CharacterMover>();
        if (mover == null) mover = GetComponentInParent<CharacterMover>();

        if (mover == null)
            Debug.LogError($"{nameof(MineJump)}: CharacterMover 를 찾지 못했습니다.", this);
    }

    /// <summary>폴짝 뛰게 한다. 다음 프레임에 반영된다.</summary>
    public void Hop()
    {
        _requested = true;
    }

    private void Update()
    {
        if (!_requested || mover == null) return;
        _requested = false;

        // 점프 칸만 덮어쓴다. 이동 방향과 달리기는 MovePlayerInput 이 넣은 그대로 둔다.
        Vector2 axis = mover.Axis;
        Vector3 target = mover.Target;
        bool run = mover.IsRun;

        mover.SetInput(in axis, in target, in run, true);
    }
}
