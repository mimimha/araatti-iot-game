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
    [SerializeField] private float walkSpeed = 5f;

    [Tooltip("Shift 를 누르고 있는 동안의 속도. 걷기보다 빨라야 의미가 있다.")]
    [SerializeField] private float runSpeed = 8f;

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

        // 제자리에 서 있으면 달리는 것이 아니다. 가만히 Shift 만 눌러도
        // 달리는 자세가 나오면 어색하다.
        bool moving = move.sqrMagnitude > 0.0001f;
        running = running && moving;

        Vector3 velocity = move * (running ? runSpeed : walkSpeed);
        velocity.y = verticalVelocity;

        controller.Move(velocity * Runner.DeltaTime);

        UpdateSafePositionOrRescue();

        AnimAxis = axis;
        Running = running;
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
    private void UpdateSafePositionOrRescue()
    {
        Vector3 now = transform.position;

        // 바다 아래로 내려갔으면 마지막 안전한 자리로 되돌린다.
        if (now.y < rescueBelowY)
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
