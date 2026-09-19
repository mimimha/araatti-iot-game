using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 갑판 위를 걷는 이동. **서버가 확정한다.**
    ///
    /// <c>DebugPlayerMover</c>(민화님, 네트워크 전까지 쓰던 것)를 서버 권위로 옮긴 것이다.
    /// <b>밸런싱 숫자를 그대로 가져왔다.</b> SHIPCOOP.md 4장이 그 숫자에 걸려 있다.
    ///
    /// <code>
    ///   걷기 4.0 / 달리기 7.0   →  달려가기 3.1초 + 들고 걷기 5.5초 + 작업 3초 = 11.6초
    ///   계단 0.4 · 경사 50도    →  뒷갑판 계단이 48.2도라 그보다 커야 오른다
    ///   키 2.8575 · 반지름 0.5  →  모델(1.27m)을 2.25배로 키운 높이. 계단 폭 1.9m
    /// </code>
    ///
    /// <b>Lobby 의 <c>NetworkPlayerMover</c> 를 쓰지 않는 이유</b>
    ///   지형 보행용이라 계단·경사 설정이 다르고, 배의 갑판 콜라이더를 통과시키는
    ///   처리가 없다. 숫자도 Lobby 기준이라 ShipCoop 밸런스와 싸운다.
    ///
    /// ⚠ <b>화면 기준 이동은 각자 자기 쪽 카메라 각도로 돌려야 한다.</b> (11장)
    ///    카메라는 사람마다 다르게 돌아가 있어 서버가 알 수 없다.
    ///    그래서 클라이언트가 <c>LookYaw</c> 를 입력에 실어 보내고 서버가 그 각도로 돌린다.
    ///    (<c>ShipCoopInputProvider</c> 가 <c>Camera.main</c> 의 y 각도를 넣는다)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class ShipCoopPlayerMover : NetworkBehaviour
    {
        [Header("속도 (m/s) — DebugPlayerMover 와 같은 값")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 4f;

        [Tooltip("달리기. 빈손일 때만 낼 수 있다 — 물건을 들면 걷는다. (SHIPCOOP.md 4장)")]
        [SerializeField, Min(0.1f)] private float sprintSpeed = 7f;

        [Header("돌아보는 속도 (도/초)")]
        [SerializeField, Min(0f)] private float turnSpeed = 720f;

        [Header("계단과 경사")]
        [Tooltip("갑판 사이 계단 한 칸보다 크게.")]
        [SerializeField, Min(0f)] private float stepHeight = 0.4f;

        [Tooltip("배의 뒷갑판 계단이 48.2도다. 그보다 커야 올라간다.")]
        [SerializeField, Range(0f, 80f)] private float slopeLimit = 50f;

        [Header("중력")]
        [SerializeField] private float gravity = -20f;

        [Header("몸 크기 (m)")]
        [Tooltip("배가 키 3m 사람에 맞춰져 있다. 캐릭터는 2.25배로 키워 쓴다.")]
        [SerializeField, Min(0.1f)] private float bodyHeight = 2.8575f;

        [Tooltip("계단 폭이 1.9m 라 0.95 를 넘으면 계단에 못 들어간다.")]
        [SerializeField, Min(0.05f)] private float bodyRadius = 0.5f;

        /// <summary>
        /// **서버가 확정한 수평 속도** (m/s, 월드 기준).
        ///
        /// <c>NetworkTransform</c> 은 자리와 방향만 보낸다. 그래서 받는 쪽은
        /// "지금 걷는 중인가" 를 알 길이 없고, 프레임 간 차이로 짐작하면
        /// <b>보간된 자리</b>를 재게 되어 남의 화면에서만 다리가 멈춘다.
        ///
        /// 그래서 속도 자체를 보낸다. 애니메이션은 <b>모든 화면에서 같은 값</b>으로 돈다.
        /// </summary>
        [Networked]
        public Vector3 Motion { get; private set; }

        [Header("자리에 붙는 순간 정위치로")]
        [Tooltip("정위치로 미끄러져 가는 속도 (m/s). 걷기보다 빨라야 '붙었다' 로 보인다.")]
        [SerializeField, Min(1f)] private float snapSpeed = 9f;

        [Tooltip("정위치를 향해 몸을 돌리는 속도 (도/초).")]
        [SerializeField, Min(90f)] private float snapTurnSpeed = 1080f;

        [Tooltip("이 시간 안에 못 가면 포기한다 (초). 무엇에 막혔을 때 영영 끌려가지 않게.")]
        [SerializeField, Range(0.1f, 2f)] private float snapTimeout = 0.6f;

        private CharacterController body;
        private ShipCoopCharacter look;
        private ShipCoopNetworkedController controller;
        private TaskWorker worker;
        private float fallSpeed;
        private bool logMotion;

        // 붙는 순간 정위치로 미끄러지기 (ShipCoopStationStand). 서버에서만 돈다.
        private bool snapping;
        private Vector3 snapTo;
        private Vector3 snapFacing;
        private float snapDeadline;

        /// <summary>-logmoves 진단용. 프레임 간 차이와 서버 확정값을 견줘 본다.</summary>
        private Vector3 lastRenderAt;

        public override void Spawned()
        {
            body = GetComponent<CharacterController>();
            look = GetComponent<ShipCoopCharacter>();
            controller = GetComponent<ShipCoopNetworkedController>();
            worker = GetComponent<TaskWorker>();
            logMotion = FusionLaunchArguments.HasFlag(FusionLaunchArguments.LogMovesKey);

            body.height = bodyHeight;
            body.radius = bodyRadius;
            body.center = new Vector3(0f, bodyHeight * 0.5f, 0f);
            body.stepOffset = stepHeight;
            body.slopeLimit = slopeLimit;

            // 캡슐이 갑판에 파묻히지 않게 남기는 여유. 유니티 권장대로 반지름의 10%.
            body.skinWidth = bodyRadius * 0.1f;

            DisableBlockingColliders();
            ShipCoopNet.IgnoreShipColliders(body);

            // 서버만 움직인다. 클라이언트에서 켜 두면 NetworkTransform 이 보내 준 위치와 싸운다.
            body.enabled = HasStateAuthority;

            if (HasStateAuthority && worker != null)
            {
                worker.CurrentChanged += OnCurrentChanged;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (worker != null)
            {
                worker.CurrentChanged -= OnCurrentChanged;
            }
        }

        /// <summary>
        /// 자리에 붙었다 — **정위치로 미끄러져 간다.** 떨어지면 그만둔다.
        ///
        /// 어디서 스페이스를 눌렀든 대포 뒤 · 돛대 오른쪽 · 바퀴 뒤로 가서 그쪽을 본다.
        /// 위치는 서버가 확정하므로 여기(서버)서 옮기고, 클라이언트는 NetworkTransform 으로 받는다.
        /// </summary>
        private void OnCurrentChanged(TaskBase task)
        {
            if (task == null || !ShipCoopStationStand.TryGet(task, worker, transform.position.y, out snapTo, out snapFacing))
            {
                snapping = false;
                return;
            }

            snapping = true;
            snapDeadline = Time.time + snapTimeout;
        }

        /// <summary>
        /// 모델에 딸려온 콜라이더를 끈다.
        ///
        /// 캐릭터 모델의 콜라이더와 CharacterController 캡슐이 둘 다 살아 있으면
        /// **자기 자신에 걸려** 계단 앞에서 멈추거나 갑판 위에서 떨린다.
        /// </summary>
        private void DisableBlockingColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i].isTrigger || colliders[i] is CharacterController)
                {
                    continue;
                }

                colliders[i].enabled = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // 서버만 위치를 확정한다. 클라이언트는 NetworkTransform 으로 결과만 받는다.
            if (!HasStateAuthority || body == null || !body.enabled)
            {
                return;
            }

            Vector3 direction = Vector3.zero;
            float speed = walkSpeed;

            if (snapping)
            {
                if (SnapStep(Runner.DeltaTime))
                {
                    return;
                }
            }

            if (GetInput(out ShipCoopInputData input))
            {
                Vector2 axis = input.Move;

                if (axis.sqrMagnitude > 1f)
                {
                    axis.Normalize();
                }

                // ⚠ **자리에 붙은 채로 움직이면 떨어진다.** 다시 붙으려면 스페이스를 눌러야 한다.
                //    예전에는 붙기 범위(2~3m) 안이면 걸어 다니면서도 J · L · K 가 먹었다. 정위치에서
                //    벗어나 팔만 뒤로 꺾인 채 조작하는 모양이 나왔고, 무엇에 붙어 있는지도 헷갈렸다.
                //    스틱이 살짝 흔들리는 것(IoT 기울기)은 걸음으로 치지 않는다.
                if (!snapping && worker != null && worker.Current != null && axis.sqrMagnitude > 0.04f)
                {
                    worker.LeaveCurrent();
                }

                // 클라이언트가 보내 준 카메라 각도로 돌린다. 서버에는 카메라가 없다.
                Quaternion lookRotation = Quaternion.Euler(0f, input.LookYaw, 0f);
                direction = lookRotation * new Vector3(axis.x, 0f, axis.y);

                if (direction.sqrMagnitude > 1f)
                {
                    direction.Normalize();
                }

                // ⚠ 물건을 들고 있으면 못 달린다. 양손으로 포탄을 안고 뛸 수는 없고,
                //    그래야 운반이 진짜 대가를 치른다. (SHIPCOOP.md 4장)
                bool carrying = worker != null && worker.HandsBusy;

                if (!carrying && ShipCoopInput.Sprint(controller))
                {
                    speed = sprintSpeed;
                }
            }

            float deltaTime = Runner.DeltaTime;

            Fall(direction * speed, deltaTime);
            FaceMoveDirection(direction, deltaTime);

            // 실제로 움직인 만큼을 보낸다. 벽에 막혔으면 입력이 있어도 0 이다.
            // 그래야 벽에 붙어 제자리걸음 하는 모습이 남의 화면에서도 똑같이 보인다.
            Vector3 actual = body.velocity;
            actual.y = 0f;
            Motion = actual;
        }

        /// <summary>
        /// 모든 화면에서 같은 값으로 애니메이션을 굴린다.
        ///
        /// 서버 · 내 캐릭터 · 남의 캐릭터 구분 없이 <see cref="Motion"/> 하나만 본다.
        /// 서버에도 불리지만 <c>ShipCoopServerCleanup</c> 이 그릴 것을 다 꺼 두었으므로 헛돌지 않는다.
        /// </summary>
        public override void Render()
        {
            if (look == null)
            {
                return;
            }

            look.DriveMotion(Motion);

            if (logMotion)
            {
                Vector3 measured = (transform.position - lastRenderAt) / Mathf.Max(Time.deltaTime, 0.0001f);
                measured.y = 0f;
                lastRenderAt = transform.position;

                Debug.Log(
                    $"[ShipCoopMover] {Object.InputAuthority} 내것={HasInputAuthority} " +
                    $"서버확정={Motion.magnitude:F2}m/s 프레임차={measured.magnitude:F2}m/s");
            }
        }

        /// <summary>
        /// 정위치로 한 걸음. 도착했거나 시간이 지났거나 자리에서 떨어졌으면 false 를 돌려 보통 이동으로 넘긴다.
        /// 미끄러지는 동안에는 입력을 받지 않는다 — 그 잠깐이 "붙었다" 를 보여준다.
        /// </summary>
        private bool SnapStep(float deltaTime)
        {
            if (worker == null || worker.Current == null || Time.time > snapDeadline)
            {
                snapping = false;
                return false;
            }

            Vector3 to = snapTo - transform.position;
            to.y = 0f;
            float distance = to.magnitude;

            // 몸을 정위치 방향으로 돌린다. 자리에 도착해도 방향은 끝까지 맞춘다.
            if (snapFacing.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(snapFacing, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, snapTurnSpeed * deltaTime);

                if (distance < 0.03f && Quaternion.Angle(transform.rotation, want) < 1f)
                {
                    snapping = false;
                    Fall(Vector3.zero, deltaTime);
                    Motion = Vector3.zero;
                    return true;
                }
            }
            else if (distance < 0.03f)
            {
                snapping = false;
            }

            // 남은 거리를 한 번에 넘지 않게. 마지막 걸음은 딱 맞춰 선다.
            float step = Mathf.Min(snapSpeed, distance / Mathf.Max(deltaTime, 0.0001f));
            Vector3 velocity = distance > 0.0001f ? to / distance * step : Vector3.zero;

            // ⚠ **충돌 없이 옮긴다.** CharacterController.Move 로 갔더니 포구 쪽에서 붙은 사람이
            //    대포 콜라이더에 막혀 뒤로 못 넘어가고, 0.6초 뒤 포기해 포구 앞에 그대로 서 있었다.
            //    정위치는 늘 갑판 위 빈 자리라 그냥 옮겨도 파묻힐 곳이 없다. 높이는 중력이 맡는다.
            //
            // ⚠ **컨트롤러를 잠깐 끄고 옮긴다.** CharacterController 는 자기 위치를 따로 들고 있어서,
            //    transform 만 옮기면 다음 Move 가 예전 자리로 되돌려 쓴다. 그래서 매 틱 옮기고 도로
            //    돌아와 제자리였다 — 대포 앞에 그대로 서 있던 두 번째 이유다. 끄고 켜면 동기화된다.
            body.enabled = false;
            transform.position += velocity * deltaTime;
            body.enabled = true;
            Fall(Vector3.zero, deltaTime);

            // 미끄러지는 걸음도 애니메이션이 따라오게 속도를 보낸다.
            Motion = velocity;
            return true;
        }

        /// <summary>수평 이동과 중력을 한 번에 적용한다.</summary>
        private void Fall(Vector3 horizontalVelocity, float deltaTime)
        {
            if (body.isGrounded && fallSpeed < 0f)
            {
                // 땅에 붙어 있게 살짝 눌러둔다. 0 으로 두면 계단을 내려갈 때 통통 튄다.
                fallSpeed = -2f;
            }
            else
            {
                fallSpeed += gravity * deltaTime;
            }

            Vector3 velocity = horizontalVelocity;
            velocity.y = fallSpeed;

            body.Move(velocity * deltaTime);
        }

        /// <summary>가는 방향으로 몸을 돌린다.</summary>
        private void FaceMoveDirection(Vector3 direction, float deltaTime)
        {
            if (turnSpeed <= 0f || direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, want, turnSpeed * deltaTime);
        }
    }
}
