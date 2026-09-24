using Fusion;
using ithappy.Cute_Characters.Controller;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 광산을 걷는 이동. **서버가 틱마다 확정한다.**
    ///
    /// <code>
    ///   서버    지금 턴인 사람의 입력만 받아 틱마다 몸을 옮긴다  -> 자리가 확정된다
    ///   모두    NetworkTransform 이 그 자리를 받아 그린다
    ///   모두    복제된 AnimAxis 로 애니메이터가 돈다
    /// </code>
    ///
    /// <b>왜 <c>CharacterMover</c> 로 움직이지 않는가.</b> 그 부품은 <c>Update()</c> 에서
    /// <c>Time.deltaTime</c> 으로 몸을 옮긴다. 서버 프레임(120fps)과 틱(60Hz)이 딱 맞물리지
    /// 않아서 틱마다 기록되는 이동 거리가 들쭉날쭉했고, 클라이언트가 그걸 보간해 그리니
    /// <b>걸을 때 캐릭터가 덜덜 떨렸다.</b> 실측: 클라이언트 30fps 에서 프레임당 이동이
    /// 67mm 로 일정해야 하는데 3~4프레임마다 50mm, 15프레임쯤마다 117mm 로 튀었다.
    /// 로비의 <c>NetworkPlayerMover</c> 가 같은 이유로 이동을 옮겨 왔다.
    ///
    /// 그래서 <c>CharacterMover</c> 는 네트워크에서 늘 꺼 두고, 그 이동 계산을 여기로 옮겨
    /// <c>FixedUpdateNetwork</c> 에서 <c>Runner.DeltaTime</c> 으로 돌린다. 걷는 느낌이
    /// 혼자 하는 씬과 같도록 **계산은 원본과 한 줄씩 같게** 두었다(<see cref="Step"/>).
    /// 원본은 외부 에셋이라 고치지 않는다. 혼자 하는 씬은 지금도 그 부품으로 걷는다.
    ///
    /// ⚠ <b>차단이 아니라 인정으로 막는다.</b> 관전자의 입력은 "무시" 하는 것이 아니라
    ///    애초에 <c>SetInput</c> 까지 가지 않는다. 자기 화면에서 한 발짝도 움직이지
    ///    않으므로 되돌려 맞출 것도 없다.
    ///
    /// ⚠ <b>화면 기준 이동을 서버가 어떻게 아는가.</b> <c>MineMoveInput.LookTarget()</c> 은
    ///    <c>MineCamera.Yaw</c> 로 '앞' 을 정하는데 <b>서버에는 카메라가 없다.</b>
    ///    그래서 클라이언트가 보내 준 <c>LookYaw</c> 로 바라볼 지점을 직접 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MineNetPlayer))]
    public sealed class MineNetPlayerMover : NetworkBehaviour
    {
        [Header("바라보는 지점 — MineMoveInput 과 같은 값을 쓴다")]
        [Tooltip("몇 m 앞을 바라보게 할 것인가. 방향만 쓰이므로 값 자체는 중요하지 않다.")]
        [SerializeField, Min(1f)] private float lookAhead = 10f;

        [Tooltip("바라보는 지점의 높이(m).")]
        [SerializeField, Min(0f)] private float lookHeight = 1f;

        [Header("이동 — CharacterMover 와 같은 값을 쓴다")]
        [Tooltip("걷는 속도(m/s). 애니메이션도 이 속도로 걸을 때를 1 로 본다. " +
                 "CharacterMover 의 Walk Speed 와 같게 둔다. 그 값은 남의 에셋의 private 이라 여기에 따로 적는다.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2f;

        [Tooltip("Shift 로 달리는 속도(m/s). CharacterMover 의 Run Speed 와 같게 둔다.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 6f;

        [Tooltip("몸을 돌리는 빠르기(도/초). CharacterMover 의 Rotate Speed 와 같게 둔다.")]
        [SerializeField, Min(0f)] private float rotateSpeed = 720f;

        [Tooltip("폴짝 높이. CharacterMover 의 Jump Height 와 같게 둔다. 원본과 같은 식으로 솟는 속도를 낸다.")]
        [SerializeField, Min(0f)] private float jumpHeight = 0.2f;

        [Tooltip("다시 뛸 수 있기까지 기다리는 시간(초). CharacterMover 의 Jump Reload 와 같게 둔다.")]
        [SerializeField, Min(0f)] private float jumpReload = 1f;

        [Header("애니메이션")]
        [Tooltip("애니메이션이 값을 따라가는 빠르기. CharacterMover 안의 값과 같다.")]
        [SerializeField, Min(0.5f)] private float animFlow = 4.5f;

        /// <summary>서버가 확정한 이동 축. 입력 그대로다.</summary>
        [Networked] public Vector2 MoveAxis { get; private set; }

        /// <summary>
        /// **실제로 움직인 결과**를 몸 기준으로 담은 값. 애니메이터가 이 값으로 돈다.
        ///
        /// <b>입력이 아니라 결과를 보내는 이유.</b> 벽에 대고 W 를 누르고 있으면 입력은
        /// 계속 1 이지만 몸은 한 발짝도 안 나간다. 입력을 그리면 그 자리에서 열심히
        /// 걷는 모습이 된다. 서버가 잰 <b>실제 이동량</b>을 보내면 막힌 순간 Idle 로 선다.
        ///
        /// 걷기 속도로 나눠 두어서 1 이 곧 제 속도로 걷는 상태다.
        /// </summary>
        [Networked] public Vector2 AnimAxis { get; private set; }

        /// <summary>달리는 중인가. 애니메이터의 State 로 간다.</summary>
        [Networked] public NetworkBool Running { get; private set; }

        /// <summary>
        /// 지금 공중에 떠 있는가. <b>점프 애니메이션을 모는 값이다.</b>
        /// 서버가 몸을 옮기는 그 틱에 정해 복제한다(<see cref="Step"/>).
        ///
        /// 서버에는 그릴 화면이 없으므로 복제하지 않으면 클라이언트에서 폴짝 동작이 안 나온다.
        /// </summary>
        [Networked] public NetworkBool Airborne { get; private set; }

        /// <summary>
        /// 공중 상태를 <b>켜 둘 시한</b>(<c>Runner.SimulationTime</c> 기준). 둘이 켠다.
        ///
        ///   <see cref="Step"/>        실제로 공중인 틱이 있으면 (점프 · 턱에서 떨어짐)
        ///   <see cref="RequestHop"/>  칸이 깨지면 — 몸이 뜨든 말든
        ///
        /// ⚠ 공중 틱은 하나뿐일 때가 많다. 틱 하나(15.6ms)는 클라이언트가 한 프레임에
        ///   틱 두 개를 넘길 때 통째로 빠지고, 애니메이터도 그 길이로는 동작을 못 시작한다.
        ///   그래서 한 번 켜면 잠깐 붙잡아 둔다.
        ///
        ///   애니메이션만 모는 값이라 이동에는 영향이 없다.
        /// </summary>
        private float _airHoldUntil = -1f;

        /// <summary>한 번 본 공중 상태를 얼마나 붙잡아 둘 것인가(초).</summary>
        private const float AirHoldSeconds = 0.12f;

        private CharacterMover _mover;
        private MineNetPlayer _who;

        private CharacterController _capsule;
        private Animator _animator;

        // CharacterMover.MovementHandler 가 들고 있던 상태. 이름만 우리 식으로 바꿨다.
        /// <summary>
        /// 중력과 점프로 쌓인 세로 속도. 땅에 서 있으면 중력 그대로다.
        /// ⚠ 필드 초기값으로 <c>Physics.gravity</c> 를 못 읽는다(생성자에서 금지). <c>Spawned</c> 에서 넣는다.
        /// </summary>
        private Vector3 _fall;

        /// <summary>다음 점프까지 남은 시간(초).</summary>
        private float _jumpTimer;

        /// <summary>몸을 돌리는 중인가. 서 있을 때는 많이 틀어졌을 때만 돈다.</summary>
        private bool _isRotating;

        /// <summary>돌아야 할 남은 각도(도).</summary>
        private float _targetAngle;

        /// <summary>
        /// 서 있을 때 이 각도(도)보다 적게 틀어졌으면 몸을 돌리지 않는다.
        /// CharacterMover 안의 <c>m_Luft</c> 와 같은 값이다.
        /// </summary>
        private const float TurnSlack = 75f;

        /// <summary>직전 틱의 자리. 실제로 얼마나 움직였는지를 여기서 잰다.</summary>
        private Vector3 _lastTickPos;
        private bool _hasLastTick;

        // 클라이언트가 애니메이터에 밀어 넣는 값. 각자의 화면 것이라 복제하지 않는다.
        private Vector2 _flowAxis;
        private float _flowState;

        private static readonly int HorId = Animator.StringToHash("Hor");
        private static readonly int VertId = Animator.StringToHash("Vert");
        private static readonly int StateId = Animator.StringToHash("State");
        private static readonly int JumpId = Animator.StringToHash("IsJump");

        /// <summary>
        /// 지금 사람끼리 부딪히게 해 두었는가. null 이면 <b>아직 안 정했다</b>는 뜻이라
        /// 다음 틱에 짝을 다시 건다. 씬 전체를 훑는 일이라 바뀔 때만 한다.
        /// </summary>
        private bool? _crowdBump;

        public override void Spawned()
        {
            _mover = GetComponent<CharacterMover>();
            _who = GetComponent<MineNetPlayer>();
            _capsule = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();

            // 스스로 키보드를 읽지 않게 한다. 켜 두면 남의 캐릭터까지 내 키보드로 움직인다.
            MineMoveInput local = GetComponent<MineMoveInput>();
            if (local != null) local.enabled = false;

            // 원본 입력도 확실히 끈다. MineMoveInput 이 Awake 에서 끄지만,
            // 그 컴포넌트를 우리가 꺼 버리면 Awake 가 안 돌 수도 있다.
            MovePlayerInput stock = GetComponent<MovePlayerInput>();
            if (stock != null) stock.enabled = false;

            // CharacterMover 는 어디서도 돌리지 않는다. 서버는 Step 이 틱마다 옮긴다.
            // (클래스 주석 — 프레임마다 옮기면 떨린다)
            if (_mover != null) _mover.enabled = false;

            _fall = Physics.gravity;

            // 클라이언트에서는 몸을 굴리지 않는다. 자리는 NetworkTransform 이 보내 준다.
            //
            // ⚠ 여기서만 CharacterController 를 끈다. 클라이언트에서는 켜 두면
            //    NetworkTransform 이 보내 준 자리와 싸우고, 다시 켤 일도 없어
            //    자리를 잃을 걱정이 없다.
            //
            // ⚠ 서버에서는 **CharacterController 를 끄지 않는다.** 껐다 켜면 그 부품이 들고
            //    있던 옛 자리로 transform 을 되돌린다. 실측에서 (-1.75, 0.5, 0.5) 에 스폰한
            //    사람이 입력도 없이 월드 원점 (0, 0, 0) 으로 옮겨져 있었다.
            //    움직이지 못하는 사람은 Step 을 부르지 않는 것으로 멈춘다.
            if (!HasStateAuthority)
            {
                if (_capsule != null) _capsule.enabled = false;
                return;
            }

            // 새 몸이 하나 늘었으니 **모두가 짝을 다시 맞춘다.** 실제로 켜고 끄는 것은
            // 다음 틱의 ApplyCrowdCollision 이다. 여기서는 "다시 정해라" 고만 한다.
            ForgetCrowdCollision();

            foreach (MineNetPlayerMover other in FindObjectsByType<MineNetPlayerMover>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other != null && other != this) other.ForgetCrowdCollision();
            }
        }

        /// <summary>
        /// 발밑을 깨졌으니 폴짝 뛰게 한다. <c>MineNetPlayerActions.Dig</c> 가 부른다.
        ///
        /// 손맛일 뿐 규칙은 아니다. 파이는 칸도 점수도 바뀌지 않는다.
        ///
        /// ⚠ <b>몸을 띄우지 않고 폴짝 동작만 켠다.</b> 예전에는 점프를 넣고 몸이 뜨기를
        ///   기다렸는데, 폴짝은 원래 몸이 거의 안 뜬다(점프 높이 0.2 는 땅의 중력을 못 이긴다).
        ///   보이던 폴짝은 발밑 칸이 내려앉을 때 몸이 잠깐 뜬 것이라, 칸 한가운데서 파면 나오고
        ///   걸으면서 파면 캡슐이 옆 칸에 걸쳐 안 나왔다. "칸이 깨졌다" 에 직접 묶어 늘 나오게 한다.
        /// </summary>
        public void RequestHop()
        {
            if (Runner == null) return;
            _airHoldUntil = Runner.SimulationTime + AirHoldSeconds;
        }

        /// <summary>
        /// **사람끼리 부딪히게 할 것인가.** 몸이 보이는 동안만 참이다.
        /// (<see cref="MineMatchState.CrewOnBoard"/> — 카운트다운 · 공개 · 턴)
        ///
        /// <b>보이지 않는 몸은 길을 막으면 안 된다.</b> 대기와 결과 화면에는 몸이
        /// 숨는데, 그 캡슐이 물리적으로 남아 있으면 <b>보이지 않는 벽에 걸려
        /// 넘어간다.</b> 예전에 "남의 머리 위를 밟고 지나가는" 모습으로 드러났던 것이
        /// 이것이다. 그때는 아예 영구히 꺼 두었지만, 이제 넷이 같이 서 있는 시간이
        /// 생겨서 <b>보일 때만 켠다.</b>
        ///
        /// 굳어 있는 것과는 상관없다 — 공개 7초에 서 있는 셋은 움직이지 못해도
        /// 보이므로, 첫 턴 예정자가 그 몸에 막히는 것이 맞다.
        ///
        /// <b>왜 <c>detectCollisions</c> 로는 안 되는가.</b> 그 값은 "다른 것이 나를
        /// <b>밀 수 있는가</b>" 를 정한다. <c>CharacterController.Move()</c> 가 스스로
        /// 부딪히는 것은 막지 못한다. 실제로 꺼 두었는데도 걸렸다.
        ///
        /// <b>왜 컨트롤러를 끄지 않는가.</b> 껐다 켜면 그 부품이 들고 있던 옛 자리로
        /// transform 을 되돌린다. 스폰 자리를 잃고 월드 원점으로 튀는 문제를 이미 겪었다.
        ///
        /// ⚠ <c>Physics.IgnoreCollision</c> 은 <b>짝마다</b> 걸리고, 콜라이더를 껐다 켜면
        ///    풀린다. 그래서 새 몸이 생기면(<c>Spawned</c>) <see cref="ForgetCrowdCollision"/>
        ///    로 기억을 지워 다음 틱에 전부 다시 걸게 한다.
        /// </summary>
        private void ApplyCrowdCollision(bool bump)
        {
            if (_crowdBump == bump) return;
            if (_capsule == null || !_capsule.enabled) return;

            _crowdBump = bump;

            foreach (MineNetPlayerMover other in FindObjectsByType<MineNetPlayerMover>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == null || other == this) continue;

                CharacterController partner = other.GetComponent<CharacterController>();
                if (partner == null || !partner.enabled) continue;

                Physics.IgnoreCollision(_capsule, partner, !bump);
            }
        }

        /// <summary>
        /// 짝을 **다시 맞춰야 한다**고 알린다. 실제로 거는 것은 다음 틱이다.
        ///
        /// 새 사람이 들어왔을 때(짝이 하나 늘었다)와 자리를 옮긴 뒤(콜라이더를 껐다 켜
        /// 짝이 풀렸다) 부른다.
        /// </summary>
        public void ForgetCrowdCollision()
        {
            _crowdBump = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _capsule == null) return;

            // 실제로 얼마나 움직였는지는 입력과 상관없이 매 틱 잰다.
            MeasureMotion();

            MineMatchState match = MineMatchState.Current;

            // ⚠ **움직일 수 있는 사람만 몸을 굴린다.** 카운트다운과 턴에는 넷 다 통과하고
            //   (MineMatchState.FreeRoam), 목표 공개(7초)에는 첫 턴 예정자만 통과한다.
            //   파는 것은 MineNetPlayerActions 가 IsMyTurn 으로 따로 막는다.
            //
            // ⚠ **판이 들려 있으면 아무도 안 움직인다.** 정답 보기가 파인 칸을 0.25m
            //   끌어올려 캐릭터를 떠밀기 때문이다. 떠밀리는 것은 그 판 위를 걷는 모두다.
            //   (MineMatchState.BoardLifted 주석 — 전용 서버에서는 늘 거짓이다)
            //
            //   **힌트 중에도 모두 걷는다.** 힌트 동안 파는 것만 막는다
            //   (MineNetPlayerActions 가 ShowingTarget 으로 막는다). 예전에는 힌트를 쓴 사람이
            //   탑뷰에서 발밑이 안 보이니 멈췄는데, 다 같이 보면서 다음에 팔 자리로 걸어가는
            //   편이 낫다고 정했다. 탑뷰 기준으로 걷게 하는 것은 아래 yaw 에서 한다.
            //   정답 보기로 올라오는 칸은 각자 화면에서만 그려지고, 몸은 서버가 굴리므로
            //   화면의 블록이 몸을 떠밀지 않는다. 파인 칸 위에서 발이 잠겨 보이는 것은 받아들인다.
            //   (혼자 하는 판은 같은 PC 에서 둘 다 일어나서 떠밀린다 — 그래서 MineGame 은 여전히 멈춘다)
            bool frozen = match != null && match.BoardLifted;

            bool mine = _who != null && _who.CanMoveNow && !frozen;

            // ⚠ **부딪히는 것은 보이는 것을 따라간다.** 숨은 몸이 길을 막으면
            //   보이지 않는 벽이 된다. 굳어 있어도 보이면 몸이다 — 공개 7초에
            //   서 있는 셋은 첫 턴 예정자를 막아도 된다. (ApplyCrowdCollision 주석)
            ApplyCrowdCollision(match != null && match.CrewOnBoard);

            float yaw = _who != null ? _who.CameraYaw : 0f;

            if (GetInput(out MineInputData input))
            {
                // 시점은 턴과 상관없이 기록한다. 관전자의 카메라는 잠겨 있어 값이 안 움직이고,
                // 턴 주인의 값만 실제로 바뀐다.
                if (_who != null) _who.RecordLook(input.LookYaw, input.LookPitch);
                yaw = input.LookYaw;
            }

            // 힌트 동안은 모두의 화면이 탑뷰라 위쪽이 늘 +Z 다. 이동도 그 기준으로 푼다.
            // 안 그러면 W 가 화면 위가 아니라 아까 3인칭에서 보던 쪽으로 간다.
            // 기록(RecordLook)은 그대로 둔다 — 힌트가 끝나면 원래 시점으로 돌아가야 한다.
            if (match != null && match.HintLeft > 0f) yaw = 0f;

            if (!mine)
            {
                // ⚠ 움직일 수 없는 몸은 **Step 조차 부르지 않는다.** 중력도 안 받고 그 자리에 선다.
                //    "중력 때문에 빈 입력으로도 Step 을 부른다" 는 처리는 움직일 수 있는 사람에게만 해당한다.
                MoveAxis = Vector2.zero;
                Running = false;
                Airborne = false;
                return;
            }

            Vector2 axis = Vector2.zero;
            bool run = false;
            bool jump = false;

            if (GetInput(out MineInputData turn))
            {
                axis = Vector2.ClampMagnitude(turn.Move, 1f);
                run = turn.Buttons.IsSet((int)MineButton.Run);
                jump = turn.Buttons.IsSet((int)MineButton.Jump);
            }

            Step(Runner.DeltaTime, axis, LookTarget(yaw), run, jump);
            MoveAxis = axis;
            Running = run;
        }

        /// <summary>
        /// **몸을 한 틱만큼 옮긴다.** <c>CharacterMover.MovementHandler.Move</c> 를 그대로 옮겨 왔다.
        /// 다른 점은 <c>Time.deltaTime</c> 대신 틱 간격을 받는다는 것 하나다.
        ///
        /// 원본에서 뺀 것: 발 디딘 면의 기울기로 이동을 꺾는 <c>SetSurface</c>. 원본은
        /// <c>hit.normal.y &gt; stepOffset</c> 일 때만 면을 기억하는데, 이 캡슐은
        /// <c>stepOffset</c> 이 1.2 라 조건이 늘 거짓이다. 기억하는 면이 없으면 꺾지 않으므로
        /// 빼도 움직임이 똑같다.
        /// </summary>
        private void Step(float dt, Vector2 axis, Vector3 target, bool run, bool jump)
        {
            // CharacterMover.SetInput
            bool moving = axis.sqrMagnitude >= Mathf.Epsilon;
            axis = moving ? Vector2.ClampMagnitude(axis, 1f) : Vector2.zero;

            // ConvertMovement — 바라보는 쪽이 앞이다 (Space = Self)
            Vector3 targetForward = Vector3.Normalize(target - transform.position);
            Vector3 forward = new Vector3(targetForward.x, 0f, targetForward.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 movement = axis.x * right + axis.y * forward;

            // CaculateGravity
            _jumpTimer = Mathf.Max(_jumpTimer - dt, 0f);
            bool air;

            if (_capsule.isGrounded)
            {
                if (jump && _jumpTimer <= 0f)
                {
                    Vector3 gravity = Physics.gravity;
                    float length = gravity.magnitude;

                    _fall += -(gravity / length) * Mathf.Sqrt(jumpHeight * 6f * length);
                    _jumpTimer = jumpReload;
                    air = true;
                }
                else
                {
                    _fall = Physics.gravity;
                    air = false;
                }
            }
            else
            {
                air = true;
                _fall += Physics.gravity * dt;
            }

            // Displace
            _capsule.Move(((run ? runSpeed : walkSpeed) * movement + _fall) * dt);

            // Turn — 걸을 때는 늘 돌고, 서 있을 때는 많이 틀어졌을 때만 돈다
            float angle = Vector3.SignedAngle(
                transform.forward, Vector3.ProjectOnPlane(targetForward, Vector3.up), Vector3.up);

            if (_isRotating || moving || Mathf.Abs(angle) >= TurnSlack)
            {
                _isRotating = true;
                _targetAngle = angle;
            }

            // UpdateRotation — 원본과 같은 식이다. 남은 각도를 한 번에 채울 수 있으면 멈춘다.
            if (_isRotating)
            {
                float rotDelta = rotateSpeed * dt;
                if (rotDelta + Mathf.PI * 2f + Mathf.Epsilon >= Mathf.Abs(_targetAngle))
                {
                    rotDelta = _targetAngle;
                    _isRotating = false;
                }
                else
                {
                    rotDelta *= Mathf.Sign(_targetAngle);
                }

                transform.Rotate(Vector3.up, rotDelta);
            }

            if (air) _airHoldUntil = Runner.SimulationTime + AirHoldSeconds;
            Airborne = Runner.SimulationTime < _airHoldUntil;
        }

        /// <summary>
        /// **실제로 움직인 양**을 재서 복제한다. 서버에서만 돈다.
        ///
        /// 틱과 틱 사이의 자리 차이가 곧 그 틱 동안 실제로 걸은 거리다. 벽에 막혔으면 0 이 된다.
        ///
        /// 몸 기준(오른쪽 · 앞)으로 바꿔 두는 이유는 애니메이터가 그 축을 쓰기 때문이다.
        /// <c>CharacterMover</c> 안의 <c>GenAnimationAxis</c> 와 같은 계산이다.
        /// </summary>
        private void MeasureMotion()
        {
            Vector3 here = transform.position;

            if (!_hasLastTick)
            {
                _hasLastTick = true;
                _lastTickPos = here;
                return;
            }

            Vector3 step = here - _lastTickPos;
            _lastTickPos = here;

            step.y = 0f;   // 중력과 점프는 걷기 애니메이션과 상관없다

            float dt = Runner.DeltaTime;
            Vector3 velocity = dt > 0.0001f ? step / dt : Vector3.zero;

            Vector2 body = new Vector2(
                Vector3.Dot(velocity, transform.right),
                Vector3.Dot(velocity, transform.forward));

            AnimAxis = Vector2.ClampMagnitude(body / Mathf.Max(0.1f, walkSpeed), 1f);
        }

        /// <summary>
        /// **모든 화면에서 같은 걷기 · Idle 을 낸다.** 내 캐릭터도, 남의 캐릭터도.
        ///
        /// 클라이언트에서는 <c>CharacterMover</c> 가 꺼져 있어 애니메이터를 채울 사람이
        /// 없다. 여기서 복제된 값으로 직접 채운다. 값을 부드럽게 따라가는 방식까지
        /// <c>CharacterMover</c> 안의 것과 같게 맞춰서, 혼자 하는 씬과 같은 모습이 나온다.
        ///
        /// ⚠ 서버에서는 돌지 않는다. 창이 없어서 그릴 것이 없다.
        /// </summary>
        public override void Render()
        {
            if (HasStateAuthority || _animator == null) return;

            Vector2 wanted = AnimAxis;
            float wantedState = Running ? 1f : 0f;
            float step = animFlow * Time.deltaTime;

            Vector2 gap = wanted - _flowAxis;
            if (gap.sqrMagnitude > 0.000001f)
            {
                _flowAxis = Vector2.ClampMagnitude(_flowAxis + step * gap.normalized, 1f);

                // 지나쳐 흔들리지 않게 목표를 넘으면 딱 맞춘다.
                if (Vector2.Dot(wanted - _flowAxis, gap) < 0f) _flowAxis = wanted;
            }

            _flowState = Mathf.MoveTowards(_flowState, wantedState, step);

            _animator.SetFloat(HorId, _flowAxis.x);
            _animator.SetFloat(VertId, _flowAxis.y);
            _animator.SetFloat(StateId, Mathf.Clamp01(_flowState));
            _animator.SetBool(JumpId, Airborne);
        }

        /// <summary>
        /// 캐릭터가 바라볼 지점. <c>MineMoveInput.LookTarget()</c> 과 같은 계산이다.
        ///
        /// <see cref="Step"/> 이 <c>Space = Self</c> 로 이 점을 향해 축을 풀고
        /// 같은 쪽으로 몸을 돌린다. 그래서 각도 하나만 넘기면 이동과 회전이 한꺼번에
        /// 화면 기준이 된다.
        /// </summary>
        private Vector3 LookTarget(float yaw)
        {
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            return transform.position + Vector3.up * lookHeight + forward * lookAhead;
        }

        /// <summary>
        /// **서버가 이 사람을 정해진 자리로 옮긴다.**
        ///
        /// 카운트다운이 켜질 때 판 위에 흩뿌리는 데 쓴다.
        /// (<c>MineMatchState.ScatterCrew</c>) <b>턴이 넘어갈 때는 부르지 않는다</b> —
        /// 걷던 사람을 끌어오지 않고 서 있던 자리에서 바로 판다.
        ///
        /// ⚠ <c>CharacterController</c> 가 켜져 있으면 <c>transform.position</c> 대입을
        ///    되돌린다. 그 부품은 자기가 아는 자리를 따로 들고 있어서, 끄고 옮긴 뒤
        ///    다시 켜야 실제로 옮겨진다.
        /// </summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (!HasStateAuthority) return;

            bool wasEnabled = _capsule != null && _capsule.enabled;
            if (wasEnabled) _capsule.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            if (wasEnabled)
            {
                _capsule.enabled = true;

                // ⚠ 껐다 켜면 짝지어 둔 것이 풀린다. 다음 틱에 다시 걸게 한다.
                ForgetCrowdCollision();
            }

            MoveAxis = Vector2.zero;
        }
    }
}
