using Fusion;
using ithappy.Cute_Characters.Controller;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 광산을 걷는 이동. **서버가 확정한다.**
    ///
    /// <b>효진님 코드를 고치지 않는다.</b> <c>MineMoveInput</c> 에는 이미
    /// <c>mover.SetInput(axis, lookTarget, run, jump)</c> 한 줄로 끝나는 경로가 있어,
    /// 그 컴포넌트를 꺼 두고 서버가 같은 함수를 불러 주면 이동 규칙이 그대로 살아난다.
    ///
    /// <code>
    ///   서버    지금 턴인 사람의 입력만 받아 SetInput 을 부른다  -> 자리가 확정된다
    ///   모두    NetworkTransform 이 그 자리를 받아 그린다
    ///   모두    복제된 MoveAxis 로 애니메이터가 돈다
    /// </code>
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

        [Header("애니메이션")]
        [Tooltip("이 속도(m/s)로 걸을 때를 1 로 본다. CharacterMover 의 Walk Speed 와 같게 둔다. " +
                 "그 값은 남의 에셋의 private 이라 여기에 따로 적는다.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2f;

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
        ///
        /// <c>CharacterMover</c> 는 이 값을 Animator 의 "IsJump" 에 넣는데,
        /// 네트워크에서 그쪽은 <b>서버에서만</b> 돌고 서버에는 그릴 화면이 없다.
        /// 복제하지 않으면 클라이언트에서 폴짝 동작이 아예 안 나온다.
        /// </summary>
        [Networked] public NetworkBool Airborne { get; private set; }

        /// <summary>
        /// 프레임에서 본 공중 상태를 <b>붙잡아 둔 시한.</b>
        ///
        /// ⚠ <c>CharacterMover.IsAir</c> 는 <b>프레임마다</b> 바뀜다(서버 120fps).
        ///   그런데 <c>Airborne</c> 은 <b>틱마다</b> 한 번만 쓴다(60Hz).
        ///   순간값을 그대로 읽으면 틱과 틱 사이에 켜졌다 꺼진 것이
        ///   통째로 사라진다. 몸이 안 뜼 때 그 폭은 <b>한 프레임</b>이다.
        ///   그래서 프레임에서 붙잡아 둔 뒤 틱에 실어 보낸다.
        /// </summary>
        private float _airHoldUntil = -1f;

        /// <summary>한 번 본 공중 상태를 얼마나 붙잡아 둘 것인가(초).</summary>
        private const float AirHoldSeconds = 0.12f;

        private CharacterMover _mover;
        private MineNetPlayer _who;

        /// <summary>
        /// 발밑을 깨서 폴짝 뛰라는 요청. <b>프레임 단위로 들고 있는다.</b>
        ///
        /// ⚠ <c>MineJump</c> 가 직접 <c>CharacterMover</c> 에 써도 이쪽이 같은 프레임에
        ///   <c>jump=false</c> 로 덮어쓰면 그만이다. 점프는 매 프레임 읽히는 bool
        ///   하나라 나중에 쓴 쪽이 이긴다. 그래서 <b>요청을 여기서 들고 있다가</b>
        ///   이 부품이 직접 <c>jump=true</c> 로 넣는다. 순서 싸움을 없앤다.
        /// </summary>
        private bool _hopRequest;

        /// <summary>이 요청을 몇 번째 프레임에 받았는가. 한 프레임만 유지한다.</summary>
        private int _hopFrame = -1;

        /// <summary>몇 번째 프레임까지 <c>jump=true</c> 를 유지할 것인가.</summary>
        private int _hopUntilFrame = -1;

        /// <summary>내려설 때까지 폴짝 요청을 몇 프레임까지 들고 있을 것인가.</summary>
        private const int HopWaitFrames = 60;
        private CharacterController _capsule;
        private Animator _animator;

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

        /// <summary>지금 이 몸을 실제로 굴리고 있는가. 같은 값을 두 번 넣지 않으려고 기억한다.</summary>
        private bool? _simulated;

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

            // 클라이언트에서는 몸을 굴리지 않는다. 자리는 NetworkTransform 이 보내 준다.
            //
            // ⚠ 여기서만 CharacterController 를 끈다. 클라이언트에서는 켜 두면
            //    NetworkTransform 이 보내 준 자리와 싸우고, 다시 켤 일도 없어
            //    자리를 잃을 걱정이 없다.
            if (!HasStateAuthority)
            {
                SetSimulated(false);
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
        /// **이 몸을 여기서 굴릴 것인가.** <c>CharacterController</c> 와
        /// <c>CharacterMover</c> 를 <b>항상 같이</b> 켜고 끈다.
        ///
        /// ⚠ 둘을 따로 다루면 안 된다. <c>CharacterMover.Update</c> 는 입력이 없어도
        ///    매 프레임 <c>controller.Move(displacement)</c> 를 부른다(중력 때문이다).
        ///    컨트롤러만 끄면 그 호출이 그대로 살아 있어
        ///    <c>"CharacterController.Move called on inactive controller"</c> 가
        ///    <b>프레임마다</b> 쏟아진다. 실측으로 한 판에 서버 43만 건, 클라이언트 2만 건이었다.
        /// </summary>
        /// <summary>
        /// 발밑을 깨졌으니 폴짝 뛰게 한다. <c>MineNetPlayerActions.Dig</c> 가 부른다.
        ///
        /// 손맛일 뿐 규칙은 아니다. 파이는 칸도 점수도 바뀌지 않는다.
        /// </summary>
        public void RequestHop()
        {
            _hopRequest = true;
            _hopFrame = Time.frameCount;
        }

        public void SetSimulated(bool on)
        {
            if (_simulated == on) return;
            _simulated = on;

            // ⚠ **CharacterController 는 끄지 않는다.** 껐다 켜면 그 부품이 들고 있던
            //    옛 자리로 transform 을 되돌린다. 실측에서 (-1.75, 0.5, 0.5) 에 스폰한
            //    사람이 입력도 없이 월드 원점 (0, 0, 0) 으로 옮겨져 있었다.
            //
            //    끌 것은 <c>CharacterMover</c> 하나면 충분하다. 매 프레임
            //    <c>controller.Move(...)</c> 를 부르는 것이 그쪽이기 때문이다.
            //    Mover 가 꺼져 있으면 컨트롤러는 살아 있어도 아무 일도 하지 않는다.
            if (_mover != null) _mover.enabled = on;

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

        /// <summary>
        /// 공중 상태를 <b>프레임마다</b> 붙잡는다. 서버에서만 돌면 된다.
        /// 이유는 <see cref="_airHoldUntil"/> 에 적어 두었다.
        /// </summary>
        private void Update()
        {
            if (!HasStateAuthority || _mover == null) return;

            if (_mover.IsAir) _airHoldUntil = Time.time + AirHoldSeconds;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _mover == null) return;

            // 실제로 얼마나 움직였는지는 입력과 상관없이 매 틱 잰다.
            MeasureMotion();

            MineMatchState match = MineMatchState.Current;

            // ⚠ **움직일 수 있는 사람만 몸을 굴린다.** 카운트다운과 턴에는 넷 다 통과하고
            //   (MineMatchState.FreeRoam), 목표 공개(7초)에는 첫 턴 예정자만 통과한다.
            //   파는 것은 MineNetPlayerActions 가 IsMyTurn 으로 따로 막는다.
            //
            // ⚠ **판이 들려 있으면 아무도 안 움직인다.** 정답 보기가 파인 칸을 0.25m
            //   끌어올려 캐릭터를 떠밀기 때문이다. 자기 힌트를 보는 사람만 멈추는 것으로는
            //   부족하다 — 떠밀리는 것은 그 판 위를 걷는 <b>나머지 셋</b>이다.
            //   (MineMatchState.BoardLifted 주석)
            bool frozen = (_who != null && _who.WatchingOwnHint)
                          || (match != null && match.BoardLifted);

            bool mine = _who != null && _who.CanMoveNow && !frozen;

            SetSimulated(mine);

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

            if (!mine)
            {
                // ⚠ 꺼진 Mover 에는 **SetInput 조차 부르지 않는다.**
                //    "중력 때문에 빈 입력을 계속 넣는다" 는 처리는 턴 주인에게만 해당한다.
                MoveAxis = Vector2.zero;
                Running = false;
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

            // ⚠ 폴짝은 <b>땅에 붙었을 때만</b> 들어간다.
            //
            //   <c>CharacterMover.CaculateGravity</c> 는 <c>m_Controller.isGrounded</c> 가
            //   참일 때만 점프를 받는다. 그런데 칸을 파면 발밑 블록이
            //   <c>digDepth</c> 만큼 내려가면서 <b>바로 그 순간 공중에 뜨게 된다.</b>
            //   그때 요청을 버리면 내려설 때는 이미 요청이 없다.
            //   그래서 <b>내려설 때까지 들고 있다가</b> 그때 넣는다.
            //
            //   한 프레임에 틱이 여러 번 돌 수 있고 <c>CharacterMover</c> 는
            //   틱이 아니라 프레임마다 읽으므로, 넣기로 정한 뒤에는
            //   다음 프레임까지 계속 true 를 유지한다.
            if (_hopRequest)
            {
                if (_capsule != null && _capsule.isGrounded)
                {
                    _hopRequest = false;
                    _hopUntilFrame = Time.frameCount + 1;
                }
                else if (Time.frameCount - _hopFrame > HopWaitFrames)
                {
                    // 너무 오래 기다리지는 않는다. 한참 뒤에 뛰면 어색하다.
                    _hopRequest = false;
                }
            }

            if (Time.frameCount <= _hopUntilFrame) jump = true;

            _mover.SetInput(axis, LookTarget(yaw), run, jump);
            MoveAxis = axis;
            Running = run;
        }

        /// <summary>
        /// **실제로 움직인 양**을 재서 복제한다. 서버에서만 돈다.
        ///
        /// <c>CharacterMover</c> 는 틱이 아니라 프레임마다 몸을 옮기므로, 틱과 틱 사이의
        /// 자리 차이가 곧 그 틱 동안 실제로 걸은 거리다. 벽에 막혔으면 0 이 된다.
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

            // 점프 애니메이션은 <c>CharacterMover</c> 가 정한 공중 상태를 그대로 따른다.
            // 그래야 혼자 하는 씬과 같은 타이밍으로 나온다.
            Airborne = Time.time < _airHoldUntil;
        }

        /// <summary>
        /// **모든 화면에서 같은 걷기 · Idle 을 낸다.** 내 캐릭터도, 남의 캐릭터도.
        ///
        /// 클라이언트에서는 <c>CharacterMover</c> 가 꺼져 있어 애니메이터를 채울 사람이
        /// 없다. 여기서 복제된 값으로 직접 채운다. 값을 부드럽게 따라가는 방식까지
        /// <c>CharacterMover</c> 안의 것과 같게 맞춰서, 혼자 하는 씬과 같은 모습이 나온다.
        ///
        /// ⚠ 서버에서는 돌지 않는다. 그쪽은 <c>CharacterMover</c> 가 이미 채우고 있고,
        ///    창도 없어서 그릴 것이 없다.
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
        /// <c>CharacterMover</c> 가 <c>Space = Self</c> 로 이 점을 향해 축을 풀고
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
