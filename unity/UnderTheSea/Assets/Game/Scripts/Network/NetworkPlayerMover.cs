using Fusion;
using UnityEngine;

/// <summary>
/// Lobby 의 <c>NetworkPlayer</c> 이동. **서버만 위치를 정한다.**
///
/// <b>왜 ithappy 의 CharacterMover 를 쓰지 않는가.</b>
/// CharacterMover 는 <c>Update()</c> 에서 <c>Time.deltaTime</c> 으로 CharacterController 를 움직인다.
/// Fusion 은 <c>FixedUpdateNetwork()</c> tick 에서 서버가 위치를 확정하고 NetworkTransform 이
/// 그 결과를 복제한다. 두 경로가 같은 CharacterController 를 동시에 밀면 서로 덮어써
/// 원격에서 캐릭터가 떨린다. 그래서 이동 로직만 이 스크립트로 옮기고
/// **모델 · Animator · 애니메이션 파라미터 이름은 그대로 재사용**한다.
///
/// 애니메이션은 <see cref="AnimAxis"/> 를 통해 모든 피어가 같은 값을 본다.
/// 로컬에서 입력으로 직접 돌리면 남의 캐릭터는 늘 서 있는 것처럼 보인다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-2)
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerMover : NetworkBehaviour
{
    [Header("이동")]
    [SerializeField] private float walkSpeed = 5.75f;

    [Tooltip("Shift 를 누르고 있는 동안의 속도. 걷기보다 빨라야 의미가 있다.")]
    [SerializeField] private float runSpeed = 9.2f;

    [SerializeField] private float rotateSpeed = 720f;

    [Tooltip("접지 상태를 유지하기 위해 매 tick 아래로 눌러 주는 힘. 경사면에서 튀지 않게 한다.")]
    [SerializeField] private float gravity = -20f;

    [Header("바다 빠짐 구조")]
    [Tooltip("이 높이보다 아래로 내려가면 마지막으로 안전하게 서 있던 자리로 되돌린다.\n\n" +
             "로비 바다는 물이 아니라 아래로 계속 내려가는 지형이라, 한 번 걸어 들어가면 " +
             "넓은 분지와 바위 절벽에 막혀 되돌아 나오지 못한다. 해안선 투명벽이 1차로 막지만 " +
             "그래도 넘어간 경우를 위한 마지막 안전장치다.\n\n" +
             "해수면은 y=0 이고, 물이 어깨까지 차기 전(대략 y=-1)까지는 걸어서 건널 수 있는 " +
             "정상 구간이다. 거기보다 넉넉히 아래여야 개울을 건너다 잘못 끌려오지 않는다.")]
    [SerializeField] private float rescueBelowY = -2.5f;

    [Tooltip("안전한 자리로 기억할 최소 높이. 해수면(0) 보다 조금 위여야 " +
             "물가에서 기억한 자리로 되돌렸다가 다시 빠지는 일이 없다.")]
    [SerializeField] private float safeGroundMinY = 0.5f;

    [Header("헤엄")]
    [Tooltip("해수면 높이. 로비 바다 판(OceanCollider)이 y=0 에 놓여 있다.")]
    [SerializeField] private float seaLevel = 0f;

    [Tooltip("발밑 물 깊이가 이보다 깊으면 헤엄치기 시작한다.\n\n" +
             "해안선 투명벽은 물이 어깨(약 1m)까지 차는 곳에 서 있으므로, " +
             "헤엄칠 수 있는 곳은 이 깊이부터 벽까지의 띠다. 벽 너머로는 여전히 못 나간다.")]
    [SerializeField] private float swimEnterDepth = 0.45f;

    [Tooltip("헤엄치다가 물이 이보다 얕아지면 다시 걷는다. " +
             "들어가는 깊이보다 얕아야 경계에서 헤엄 · 걷기가 번갈아 깜빡이지 않는다.")]
    [SerializeField] private float swimExitDepth = 0.35f;

    [Tooltip("수면에 떠 있을 때 발(피벗)을 수면 아래 얼마에 둘지(m). Space 로 떠올라도 여기서 멈춘다.\n\n" +
             "헤엄 자세에서 몸통은 피벗 위 0~0.35m, 머리는 1m 까지 올라온다. " +
             "0.3m 가라앉히면 몸통은 물에 잠기고 큰 머리만 물 위에 뜬다. " +
             "Tools/아라아띠/로비 헤엄 자세 미리보기 로 보고 정했다.")]
    [SerializeField] private float swimSinkDepth = 0.3f;

    [SerializeField] private float swimSpeed = 3.2f;

    [Tooltip("Shift 를 누르고 헤엄칠 때의 속도.")]
    [SerializeField] private float swimSprintSpeed = 4.6f;

    [Tooltip("Space 를 누르고 있을 때 떠오르는 속도(m/s). 수면 높이(swimSinkDepth)에서 멈춘다.")]
    [SerializeField] private float swimRiseSpeed = 1.8f;

    [Tooltip("Space 를 떼고 있을 때 가라앉는 속도(m/s). 바닥에 닿으면 멈춘다.\n\n" +
             "떠오르는 것보다 느려야 한다. 잠깐 손을 뗀 사이에 훅 가라앉으면 조작이 거칠게 느껴진다.")]
    [SerializeField] private float swimDiveSpeed = 0.8f;

    /// <summary>
    /// 헤엄치며 Space 로 떠오르는 중인가. 서버가 정하고 모든 피어가 읽는다.
    /// 제자리에서 떠오를 때도 팔을 젓게 하려고 둔다(<see cref="AnimAxis"/> 는 수평 입력뿐이다).
    /// </summary>
    [Networked] public NetworkBool Ascending { get; set; }

    /// <summary>
    /// 지금 헤엄치는 중인가. 서버가 정하고 **모든 피어가 읽는다.** <see cref="Running"/> 과 같은 이유다.
    ///
    /// ⚠ <b>[Networked] 여야 한다.</b> 들어가고 나오는 깊이가 달라서(<see cref="swimEnterDepth"/> ·
    ///    <see cref="swimExitDepth"/>) 직전 상태를 보고 정한다. 되돌려 계산하는 틱에서도 같은 값을 봐야 한다.
    /// </summary>
    [Networked] public NetworkBool Swimming { get; set; }

    [Header("점프")]
    [Tooltip("최고점 높이(m). 솟는 속도는 중력에서 거꾸로 계산한다.\n\n" +
             "속도를 직접 두지 않는 이유는 중력을 바꾸면 높이가 같이 변해서다. " +
             "보이는 것은 높이이므로 높이를 적는다.")]
    [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;

    /// <summary>
    /// 직전 틱에 누르고 있던 것들. 여기서 "눌린 순간" 을 만들어 낸다.
    ///
    /// ⚠ <b>[Networked] 여야 한다.</b> Fusion 은 같은 틱을 여러 번 굴리므로
    ///    평범한 필드에 두면 되돌려 계산하는 사이에 값이 어긋난다.
    /// </summary>
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    /// <summary>
    /// 마지막으로 **땅에 발을 붙이고 서 있던 안전한 자리.** 바다로 떨어지면 여기로 되돌린다.
    ///
    /// ⚠ <b>[Networked] 여야 한다.</b> <see cref="PreviousButtons"/> 와 같은 이유다.
    ///    Fusion 은 같은 틱을 여러 번 굴리므로 평범한 필드에 두면 되돌려 계산하는 사이에
    ///    값이 어긋나, 엉뚱한 자리로 되돌리게 된다.
    /// </summary>
    [Networked] private Vector3 SafePosition { get; set; }

    [Header("애니메이터")]
    [Tooltip("CharacterMover 가 쓰던 이름과 같아야 한다. 다르면 애니메이션이 재생되지 않는다.")]
    [SerializeField] private string horizontalId = "Hor";
    [SerializeField] private string verticalId = "Vert";
    [SerializeField] private string stateId = "State";

    [Tooltip("Tools/아라아띠/로비 헤엄 모션 설치 가 애니메이터에 더하는 파라미터와 같아야 한다.")]
    [SerializeField] private string swimmingId = "IsSwimming";
    [SerializeField] private string swimSpeedId = "SwimSpeed";

    [Tooltip("헤엄 모션 재생 속도. 제자리에서는 천천히 저어 떠 있는 것처럼 보이게 한다.")]
    [SerializeField] private float swimIdleAnimSpeed = 0.35f;
    [SerializeField] private float swimSprintAnimSpeed = 1.4f;

    /// <summary>
    /// 애니메이터에 넣을 이동 축. 서버가 쓰고 모든 피어가 읽는다.
    /// </summary>
    [Networked] public Vector2 AnimAxis { get; set; }

    /// <summary>
    /// 지금 달리는 중인가. 서버가 정하고 **모든 피어가 읽는다.**
    ///
    /// ⚠ 로컬 입력으로 그리면 남의 캐릭터는 늘 걷는 자세로 보인다.
    ///    <see cref="AnimAxis"/> 를 네트워크로 보내는 것과 같은 이유다.
    /// </summary>
    [Networked] public bool Running { get; set; }

    private CharacterController controller;
    private Animator animator;

    /// <summary>지면에 붙어 있게 하려고 누적하는 수직 속도.</summary>
    private float verticalVelocity;

    /// <summary>
    /// <c>-logmoves</c> 로 켜는 진단 로그.
    ///
    /// 서버에는 창이 없고 남의 클라이언트 화면도 들여다볼 수 없다.
    /// "A 가 움직이면 B 에도 보이는가" 를 확인할 방법이 로그밖에 없어서 둔다.
    /// 평소에는 꺼져 있어 로그가 늘지 않는다.
    /// </summary>
    private static readonly bool LogMoves = FusionLaunchArguments.HasFlag(FusionLaunchArguments.LogMovesKey);

    private float nextLogTime;
    private Vector3 lastLoggedPosition;

    /// <summary>발밑 물 깊이를 잴 때 쓰는 버퍼. 매 틱 배열을 만들지 않으려고 둔다.</summary>
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    /// <summary>해안선 · 물 투명벽의 루트 이름. 벽 꼭대기를 바닥으로 읽으면 안 된다.</summary>
    private const string BlockerRootName = "WaterBlockers";

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Animator 는 모델 루트가 아니라 자식에 있을 수 있다.
        animator = GetComponentInChildren<Animator>();
    }

    public override void Spawned()
    {
        // ⚠ CharacterController 는 활성화된 순간의 좌표를 내부에 따로 들고 있다.
        //    Runner.Spawn 은 프리팹을 만든 뒤에 transform.position 을 옮기므로,
        //    컨트롤러 내부 좌표는 프리팹 원점(0,0,0) 인 채로 남는다.
        //    그 상태에서 첫 Move() 를 부르면 캐릭터가 스폰 지점이 아니라 원점으로 끌려가
        //    지형 아래로 떨어진다. 실제 로그로 확인했다.
        //      스폰 완료 — (22.09, 1.20, 48.56)  →  다음 tick (0.00, -0.18, 0.00)
        //    껐다 켜면 지금 transform 좌표를 다시 읽는다.
        if (controller != null)
        {
            controller.enabled = false;
            controller.enabled = true;
        }

        // 남의 캐릭터는 NetworkTransform 이 위치를 그린다.
        // 컨트롤러를 켜 둘 이유가 없고, 켜 두면 보간 중인 위치와 충돌 해석이 겹칠 수 있다.
        if (!HasStateAuthority && controller != null)
        {
            controller.enabled = false;
        }

        // 스폰 지점은 언제나 안전한 자리다. 첫 구조 지점으로 삼는다.
        // [Networked] 라 값을 정하는 것은 서버뿐이다.
        if (HasStateAuthority)
        {
            SafePosition = transform.position;
        }

        lastLoggedPosition = transform.position;
    }

    public override void FixedUpdateNetwork()
    {
        // 서버(State Authority)만 위치를 확정한다. 클라이언트는 NetworkTransform 으로 결과만 받는다.
        if (!HasStateAuthority)
        {
            return;
        }

        // 이 오브젝트의 InputAuthority 가 보낸 입력만 가져온다.
        // 입력이 아직 안 왔으면 제자리에서 중력만 적용한다.
        Vector3 move = Vector3.zero;
        Vector2 axis = Vector2.zero;
        bool jumped = false;
        bool running = false;
        bool jumpHeld = false;

        if (GetInput(out NetworkInputData input))
        {
            axis = input.Direction;

            if (axis.sqrMagnitude > 1f)
            {
                axis.Normalize();
            }

            // 카메라가 바라보는 방향을 기준으로 이동시킨다.
            // 서버에는 카메라가 없으므로 클라이언트가 보내 준 각도를 쓴다.
            Quaternion look = Quaternion.Euler(0f, input.LookYaw, 0f);
            move = look * new Vector3(axis.x, 0f, axis.y);

            if (move.sqrMagnitude > 0.0001f)
            {
                // 가는 방향을 바라보게 돌린다.
                Quaternion target = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, rotateSpeed * Runner.DeltaTime);
            }

            // 달리기는 "누르고 있는 상태" 라 되돌려 계산해도 값이 같다.
            // 눌린 순간을 만들 필요가 없으므로 IsForward 안쪽에 두지 않는다.
            running = input.Buttons.IsSet((int)LobbyButton.Sprint);

            // 물속에서 Space 는 "누르고 있는 동안 떠오르기" 다. 달리기와 같은 이유로 IsForward 밖에서 읽는다.
            jumpHeld = input.Buttons.IsSet((int)LobbyButton.Jump);

            // ⚠ 되돌려 다시 계산하는 틱에서는 "눌린 순간" 을 만들지 않는다.
            //    Fusion 은 같은 틱을 여러 번 굴린다. 그대로 두면 한 번 누른 것이
            //    여러 번으로 처리되어 점프가 두 번 튄다.
            if (Runner.IsForward)
            {
                NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
                PreviousButtons = input.Buttons;

                jumped = pressed.IsSet((int)LobbyButton.Jump);
            }
        }

        // 제자리에 서 있으면 달리는 것이 아니다. 가만히 Shift 만 눌러도
        // 달리는 자세가 나오면 어색하다.
        bool moving = move.sqrMagnitude > 0.0001f;
        running = running && moving;

        // 발밑 물이 깊으면 헤엄친다. 들어가는 깊이와 나오는 깊이를 달리 둬 경계에서 깜빡이지 않게 한다.
        float depth = WaterDepth();
        bool swimming = Swimming ? depth >= swimExitDepth : depth >= swimEnterDepth;

        if (swimming)
        {
            SwimStep(move, running, jumpHeld);
        }
        else
        {
            WalkStep(move, running, jumped);
        }

        Swimming = swimming;
        Ascending = swimming && jumpHeld;

        UpdateSafePositionOrRescue();

        AnimAxis = axis;
        Running = running;
    }

    /// <summary>
    /// 헤엄 한 틱. 중력 대신 **Space 로 위아래**를 정한다. 점프는 하지 않는다.
    ///   누르고 있으면  <see cref="swimRiseSpeed"/> 로 떠올라 수면 높이에서 멈춘다
    ///   떼고 있으면    <see cref="swimDiveSpeed"/> 로 가라앉아 바닥에서 멈춘다(컨트롤러가 막는다)
    ///
    /// 벽은 따로 두지 않는다. 해안선 투명벽이 그대로 서 있어서, 헤엄칠 수 있는 곳은
    /// 벽 안쪽 바다뿐이다.
    /// </summary>
    private void SwimStep(Vector3 move, bool running, bool rise)
    {
        float dt = Runner.DeltaTime;

        float y = transform.position.y;
        float surfaceY = seaLevel - swimSinkDepth;

        float dy = rise ? swimRiseSpeed * dt : -swimDiveSpeed * dt;

        // 수면 위로 튀어나가지 않는다. 걸어 들어오며 이미 수면 높이보다 위에 있으면 그 자리에서 내려오기만 한다.
        dy = Mathf.Min(dy, Mathf.Max(0f, surfaceY - y));

        // 물에서 나가 걷기 시작할 때 떨어지며 붙은 속도가 남아 있으면 안 된다.
        verticalVelocity = 0f;

        Vector3 step = move * ((running ? swimSprintSpeed : swimSpeed) * dt);
        step.y = dy;

        controller.Move(step);
    }

    /// <summary>
    /// 발밑 물 깊이(m). 해수면에서 **밟고 설 면**까지의 거리다. 물 위 땅이면 0 이하다.
    ///
    /// 로비 바다에는 물 콜라이더가 없고 해수면 아래로 지형이 이어진다
    /// (<see cref="UpdateSafePositionOrRescue"/> 참고). 그래서 아래로 쏴서 바닥 높이를 잰다.
    /// 다른 캐릭터와 투명벽은 바닥이 아니므로 뺀다. 광선은 캡슐 안에서 쏘므로 자기 자신에는 걸리지 않는다.
    /// </summary>
    private float WaterDepth()
    {
        Vector3 origin = transform.position + Vector3.up;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 30f, ~0, QueryTriggerInteraction.Ignore);

        float ground = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i].collider;

            if (hit is CharacterController || hit.transform.root.name == BlockerRootName)
            {
                continue;
            }

            ground = Mathf.Max(ground, groundHits[i].point.y);
        }

        // 아래에 아무것도 없으면 물이 아니라 허공이다. 헤엄치게 두지 않고 떨어뜨려 구조 장치에 맡긴다.
        return ground > float.MinValue ? seaLevel - ground : 0f;
    }

    /// <summary>걷기 · 달리기 · 점프 한 틱. 헤엄 전의 원래 이동이다.</summary>
    private void WalkStep(Vector3 move, bool running, bool jumped)
    {
        // 접지 중이면 살짝 눌러 두고, 아니면 중력을 누적한다.
        bool grounded = controller.isGrounded;

        verticalVelocity = grounded
            ? -2f
            : verticalVelocity + gravity * Runner.DeltaTime;

        // 땅에 붙어 있을 때만 뛴다. 공중에서 또 누르면 무시한다.
        //
        // 솟는 속도는 v = √(2gh) 다. 높이만 적어두면 중력을 바꿔도 보이는 높이가 그대로다.
        if (jumped && grounded)
        {
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }

        Vector3 velocity = move * (running ? runSpeed : walkSpeed);
        velocity.y = verticalVelocity;

        controller.Move(velocity * Runner.DeltaTime);
    }

    /// <summary>
    /// 안전한 자리를 갱신하거나, 바다에 빠졌으면 거기로 되돌린다.
    /// <b>서버에서만 부른다.</b> 위치를 정하는 것은 서버이기 때문이다.
    ///
    /// 로비 바다에는 물 콜라이더가 없어서 해변에서 걸어 들어가면 y=-9.8 까지 그냥
    /// 걸어 내려간다. 분지가 넓고 바위 절벽에 막혀 되돌아 나오지 못한다.
    /// 해안선 투명벽(<c>Tools/아라아띠/로비 해안선 투명벽 세우기</c>)이 1차로 막지만,
    /// 벽에 틈이 있거나 밀려 넘어가는 경우를 위해 여기서 한 번 더 건진다.
    /// </summary>
    /// <summary>
    /// **이정표에서 다른 이정표로 보내 달라고 서버에 청한다.**
    ///
    /// <b>왜 RPC 인가.</b> 로비 캐릭터의 위치는 서버만 정한다
    /// (<see cref="FixedUpdateNetwork"/> 가 <c>HasStateAuthority</c> 가 아니면 바로 빠져나온다).
    /// 클라이언트가 <c>transform.position</c> 을 바꿔도 다음 틱에 NetworkTransform 이
    /// 서버 값으로 되돌린다. 그래서 "여기로 보내 달라" 고 청하는 수밖에 없다.
    ///
    /// <b>보낸 좌표를 그대로 믿지 않는다.</b> 그러면 누구든 아무 데나 갈 수 있다.
    /// 서버도 같은 Lobby 씬을 들고 있으므로 놓여 있는 이정표를 안다. 받은 좌표가
    /// <b>실제 이정표의 도착 지점과 맞을 때만</b> 옮긴다.
    ///
    /// ⚠ <b>이것으로 충분한 방어는 아니다.</b> 이정표 사이를 얼마나 자주 오갈 수 있는지,
    ///    정말로 그 이정표 앞에 서 있었는지는 보지 않는다. 시연용으로는 과하다고 보고
    ///    "아무 데나 못 간다" 까지만 막았다. 필요해지면 여기에 쿨다운과 출발지 확인을 더한다.
    /// </summary>
    /// <param name="target">가고 싶은 이정표의 도착 지점.</param>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestTeleport(Vector3 target)
    {
        // 받은 좌표가 정말 이정표 앞인가. 1.5m 는 부동소수 오차와 도착 지점을 살짝
        // 옮겨 놓았을 여지를 봐주는 값이다.
        if (!UnderTheSea.Lobby.SignpostTeleport.IsKnownArrival(target, 1.5f, out string who))
        {
            Debug.LogWarning(
                $"[이정표] {Object.InputAuthority} 가 {target.ToString("F2")} 로 보내 달라 했지만 " +
                "그 자리에 이정표가 없습니다. 보내지 않습니다.");
            return;
        }

        Teleport(target);
        Debug.Log($"[이정표] {Object.InputAuthority} 를 \"{who}\" ({target.ToString("F2")}) 로 보냈습니다.");
    }

    /// <summary>
    /// 캐릭터를 그 자리에 놓는다. <b>서버에서만 뜻이 있다.</b>
    ///
    /// ⚠ <c>CharacterController</c> 는 켜져 있는 동안 자기 좌표를 따로 들고 있다.
    ///    <c>transform</c> 만 옮기면 다음 <c>Move()</c> 에서 원래 자리로 끌려 돌아간다.
    ///    껐다 켜야 지금 좌표를 다시 읽는다. 바다 빠짐 구조와 같은 방법이다.
    /// </summary>
    private void Teleport(Vector3 target)
    {
        controller.enabled = false;
        transform.position = target;
        controller.enabled = true;

        // 떨어지며 붙은 속도를 지운다. 안 지우면 도착하자마자 땅으로 처박힌다.
        verticalVelocity = 0f;

        // 도착한 자리를 안전한 자리로 삼는다. 안 하면 바다에 빠졌을 때
        // 떠나온 이정표로 되돌아가 버린다.
        SafePosition = target;
    }

    private void UpdateSafePositionOrRescue()
    {
        Vector3 now = transform.position;

        // 바다 아래로 내려갔으면 마지막 안전한 자리로 되돌린다.
        //
        // ⚠ 헤엄치는 중에는 되돌리지 않는다. 해안선 벽이 15m 바깥으로 물러나 바다 밑이 10m 까지
        //    내려가므로, 잠수만 해도 이 높이 아래로 간다. 헤엄은 발밑에 바닥이 있을 때만 되므로
        //    (WaterDepth) 바닥 없는 허공으로 떨어지는 경우는 여전히 여기서 건진다.
        if (!Swimming && now.y < rescueBelowY)
        {
            // ⚠ CharacterController 는 활성화된 순간의 좌표를 내부에 따로 들고 있다.
            //    transform 만 옮기면 다음 Move() 에서 원래 자리로 끌려 돌아간다.
            //    껐다 켜야 지금 좌표를 다시 읽는다. Spawned() 와 같은 이유다.
            controller.enabled = false;
            transform.position = SafePosition;
            controller.enabled = true;

            // 떨어지며 붙은 속도를 지운다. 안 지우면 도착하자마자 땅으로 처박힌다.
            verticalVelocity = 0f;

            Debug.Log($"[구조] 바다에 빠져 {SafePosition.ToString("F2")} 로 되돌렸다.");
            return;
        }

        // 땅에 발을 붙이고 해수면 위에 있을 때만 안전한 자리로 기억한다.
        // 공중이나 물가에서 기억하면 되돌린 직후 다시 빠진다.
        if (controller.isGrounded && now.y >= safeGroundMinY)
        {
            SafePosition = now;
        }
    }

    /// <summary>
    /// 화면에 그릴 때마다 불린다. 서버(-nographics)에서는 Animator 가 없거나 그릴 것이 없으므로
    /// 여기서만 애니메이터를 만진다. 모든 피어가 <see cref="AnimAxis"/> 라는 같은 값을 본다.
    /// </summary>
    public override void Render()
    {
        if (animator == null)
        {
            return;
        }

        Vector2 axis = AnimAxis;

        animator.SetFloat(horizontalId, axis.x);
        animator.SetFloat(verticalId, axis.y);

        // State 는 걷기(0)~뛰기(1) 블렌드다. 서버가 정한 값을 모든 피어가 같이 본다.
        animator.SetFloat(stateId, Running ? 1f : 0f);

        // 헤엄도 서버가 정한 값을 모두가 같이 본다. 제자리에서는 천천히 저어 떠 있게 한다.
        bool stroking = axis.sqrMagnitude > 0.0001f || Ascending;
        animator.SetBool(swimmingId, Swimming);
        animator.SetFloat(swimSpeedId, !stroking ? swimIdleAnimSpeed : Running ? swimSprintAnimSpeed : 1f);
    }

    private void Update()
    {
        if (!LogMoves || Time.time < nextLogTime)
        {
            return;
        }

        Vector3 now = transform.position;

        // 움직이지 않았으면 로그를 남기지 않는다.
        if ((now - lastLoggedPosition).sqrMagnitude < 0.01f)
        {
            return;
        }

        lastLoggedPosition = now;
        nextLogTime = Time.time + 0.5f;

        // 서버는 "내가 확정한 위치", 클라이언트는 "네트워크로 받은 위치" 를 찍는다.
        // 같은 좌표가 서버와 두 클라이언트 로그에 모두 나오면 복제가 된 것이다.
        string who = Object != null && Object.HasStateAuthority ? "서버확정" : "수신";
        string owner = Object != null ? Object.InputAuthority.ToString() : "?";

        Debug.Log($"[Move] {who} {owner} → {now.ToString("F2")}");
    }
}
