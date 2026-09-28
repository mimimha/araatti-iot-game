using ithappy.Cute_Characters.Controller;
using UnityEngine;

/// <summary>
/// 광산에서 **화면 기준으로 걷게** 만드는 입력. (MINE.md 6장)
///
/// 마우스로 시점을 돌릴 수 있게 되면서 필요해졌다. 카메라만 돌리고 이동을 그대로
/// 두면, 뒤를 본 순간 "앞으로" 가 화면 아래로 걸어가 조작이 뒤집힌다.
///
/// 하는 일은 **바라볼 지점을 넘기는 것** 하나다. 나머지는 CharacterMover 가 한다.
/// W 를 누르면 지금 보고 있는 쪽으로 몸을 돌려 직진하고, A · D 는 그 자세로 옆걸음한다.
///
/// ⚠ **씬 설정 두 개에 기대고 있다.** 캐릭터의 <c>CharacterMover</c> 가
///   <c>Space = Self</c> · <c>Rotate Speed &gt; 0</c> 이어야 한다.
///
///     Space = Self       이동과 애니메이션을 '바라보는 쪽' 기준으로 푼다
///     Rotate Speed &gt; 0   그쪽으로 몸을 돌린다. 0 이면 옆걸음으로 미끄러진다
///
///   둘 중 하나만 빠져도 조용히 어긋난다. 그래서 에디터에서는 Awake 가 확인하고
///   경고를 찍는다. 광산 밖(로비 등)의 프리팹은 건드리지 않는다 — 씬의 인스턴스에만
///   걸어둔 값이다.
///
/// ⚠ **축을 직접 돌리지 않는다.** 예전에는 방향키를 카메라 각도만큼 돌려서 넘겼는데,
///   Space = Self 는 이미 바라보는 쪽을 기준으로 축을 푼다. 둘 다 하면 두 번 돌아간다.
///
/// ⚠ **<c>MovePlayerInput</c> 을 끄고 그 자리를 대신한다.** 둘 다 켜두면 같은
///   프레임에 서로 다른 값을 넣어 순서 싸움이 난다. 실행 순서도 그것과 같은
///   -100 이어야 한다. MineJump(-50) → CharacterMover(0) 순서가 유지된다.
///   (자세한 것은 <see cref="MineJump"/> 주석)
///
/// ⚠ **입력은 <c>IPlayerController</c> 에서 온다.** 레거시 <c>Input.GetAxis</c> 를
///   쓰지 않는다. 그래야 키보드든 완드든 같은 길로 들어온다. (MINE.md 8장 표)
///   점프만 예외로 남아 있는데, 씬에서 키를 비워 둬서 실제로는 안 쓰인다.
///
/// 플레이어(캐릭터)에 붙인다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class MineMoveInput : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 같은 오브젝트와 부모에서 찾는다.")]
    [SerializeField] private CharacterMover mover;

    [Tooltip("어느 쪽이 '앞' 인지 정하는 카메라. 비워두면 씬에서 찾는다.\n" +
             "없으면 예전처럼 월드 기준으로 움직인다.")]
    [SerializeField] private MineCamera cam;

    [Header("바라보는 지점")]
    [Tooltip("몇 m 앞을 바라보게 할 것인가. 방향만 쓰이므로 값 자체는 중요하지 않다.\n" +
             "너무 짧으면 캐릭터가 조금 움직일 때마다 방향이 크게 흔들린다.")]
    [SerializeField, Min(1f)] private float lookAhead = 10f;

    [Tooltip("바라보는 지점의 높이(m). 머리 IK 가 이쪽을 본다.")]
    [SerializeField, Min(0f)] private float lookHeight = 1f;

    [Header("입력")]
    [Tooltip("IPlayerController 를 구현한 컴포넌트. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private MonoBehaviour playerControllerSource;

    [Tooltip("비워두면 점프를 받지 않는다. 광산은 Space 를 채굴에 쓰므로 비워 둔다 — " +
             "네트워크(MineInputProvider)에도 점프가 없다.")]
    [SerializeField] private string jumpButton = "Jump";

    private IPlayerController _controller;

    private void Awake()
    {
        if (mover == null) mover = GetComponent<CharacterMover>();
        if (mover == null) mover = GetComponentInParent<CharacterMover>();
        if (cam == null) cam = FindAnyObjectByType<MineCamera>();

        // MineDigger 와 같은 방식으로 찾는다. 한 오브젝트에 구현체가 둘이면
        // GetComponent 가 어느 쪽을 돌려줄지 정해져 있지 않으므로, 그럴 때는
        // 위 칸에 쓸 것을 직접 지정한다.
        _controller = playerControllerSource as IPlayerController
                      ?? GetComponent<IPlayerController>()
                      ?? GetComponentInParent<IPlayerController>();

        if (_controller == null)
        {
            Debug.LogError($"{nameof(MineMoveInput)}: IPlayerController 를 찾지 못했습니다. " +
                           $"KeyboardPlayerController 를 붙이거나 참조를 지정하세요.", this);
        }

        if (mover == null)
        {
            Debug.LogError($"{nameof(MineMoveInput)}: CharacterMover 를 찾지 못했습니다.", this);
            return;
        }

        // 둘이 같이 돌면 한 프레임에 입력을 두 번 넣게 된다. 여기서 확실히 끈다.
        var stock = mover.GetComponent<MovePlayerInput>();
        if (stock != null && stock.enabled)
        {
            stock.enabled = false;
            Debug.Log($"{nameof(MineMoveInput)}: MovePlayerInput 을 껐습니다. " +
                      $"광산에서는 이 컴포넌트가 이동 입력을 넣습니다.", this);
        }

        WarnIfMoverMisconfigured();
    }

    private void Update()
    {
        if (mover == null || _controller == null) return;

        Vector2 axis = _controller.Move;

        // 🏃 달리기는 **오른손 버튼 2** 다. 기기가 2대일 때만 받는다.
        //
        // IOT_INPUT.md 3장 표가 정한 자리다. 왼손 버튼 둘은 복구와 힌트가 이미 쓰고
        // 있어서 달리기가 오른손으로 갔다. 오른손 버튼 1 은 같은 표에서 "안 씁니다" 다.
        //
        // ⚠ **누르고 있기다. 배와 다르다.** 배는 왼손 버튼 2 를 토글로 잠그지만,
        //   광산은 판을 내려다보며 파는 게임이라 카메라를 계속 돌릴 일이 적어서
        //   엄지가 스틱을 떠나도 괜찮다. 그래서 `ConsumeButton2Press` 가 아니라
        //   `Button2` 를 본다. (IOT_INPUT.md 3장 "달리기는 누르고 있기입니다")
        //
        // ⚠ 1대면 Right 가 Left 와 **같은 객체**라, 오른손 버튼 2 가 곧 힌트 버튼이
        //   된다. 힌트를 누를 때마다 달리게 되므로 1대에서는 달리기를 뺀다.
        //   배도 같은 이유로 1대에서 달리기를 뺀다. (ShipCoopInput.Sprint)
        //
        // ⚠ 버튼 1 이 아니라 2 인 이유. KeyboardPlayerController 의 Shared 프로필은
        //   오른손 버튼1 을 **Space** 에 다는데, Space 는 광산에서 땅 파기다.
        //   거기에 달리기를 걸면 파려고 누를 때마다 달린다. 버튼2 는 K 라 안 겹친다.
        bool run = _controller.HasTwoDevices && _controller.Right.Button2;

        // ⚠ 빈 이름으로 GetButton 을 부르면 Unity 가 예외를 던진다. 먼저 걸러야 한다.
        bool jump = !string.IsNullOrEmpty(jumpButton) && Input.GetButton(jumpButton);

        mover.SetInput(axis, LookTarget(), run, jump);
    }

    /// <summary>
    /// 캐릭터가 바라볼 지점. **이 한 점이 '앞' 을 정한다.**
    ///
    /// CharacterMover 는 이 점을 향하는 방향으로 축을 풀고(Space = Self),
    /// 같은 방향으로 몸을 돌린다(Rotate Speed). 그래서 카메라 각도만 넘겨주면
    /// 이동과 회전이 한꺼번에 화면 기준이 된다.
    ///
    /// 카메라가 없으면 월드 정면을 돌려준다 — 마우스가 없던 때와 같은 움직임이다.
    /// </summary>
    private Vector3 LookTarget()
    {
        float yaw = cam != null ? cam.Yaw : 0f;
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        return transform.position + Vector3.up * lookHeight + forward * lookAhead;
    }

    /// <summary>
    /// 씬 설정이 어긋났으면 알려준다. 에디터에서만 돈다.
    ///
    /// Space 와 Rotate Speed 는 남의 에셋의 private 값이라 밖에서 읽을 길이
    /// SerializedObject 뿐이다. 빌드에는 들어가지 않는다.
    /// </summary>
    private void WarnIfMoverMisconfigured()
    {
#if UNITY_EDITOR
        var so = new UnityEditor.SerializedObject(mover);

        var space = so.FindProperty("m_Space");
        if (space != null && space.enumValueIndex != (int)Space.Self)
        {
            Debug.LogWarning($"{nameof(MineMoveInput)}: CharacterMover 의 Space 가 Self 가 아닙니다. " +
                             $"이동이 화면 기준으로 풀리지 않습니다.", this);
        }

        var rotateSpeed = so.FindProperty("m_RotateSpeed");
        if (rotateSpeed != null && rotateSpeed.floatValue <= 0f)
        {
            Debug.LogWarning($"{nameof(MineMoveInput)}: CharacterMover 의 Rotate Speed 가 0 입니다. " +
                             $"보는 쪽으로 몸이 안 돌아 옆걸음으로 미끄러집니다.", this);
        }
#endif
    }
}
