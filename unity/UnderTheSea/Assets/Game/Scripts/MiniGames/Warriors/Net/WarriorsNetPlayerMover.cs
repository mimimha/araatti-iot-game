using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 해변을 걷는 이동. **서버가 확정한다.**
    ///
    /// <b>서연님 코드를 고치지 않는다.</b> <c>WarriorsLocalPlayerController</c> 에는
    /// 이미 <c>ApplyMovement(input, deltaTime)</c> 가 <c>Update</c> 와 분리돼 있어,
    /// 그 컴포넌트를 꺼 두고 서버가 이 함수만 불러 주면 이동 규칙이 그대로 살아난다.
    /// 속도 · 회전 속도 · 중력 · 데드존이 전부 원본 값 그대로다.
    ///
    /// <code>
    ///   서버    입력을 받아 ApplyMovement 를 부른다            -> 자리가 확정된다
    ///   모두    NetworkTransform 이 그 자리를 받아 그린다
    ///   모두    복제된 MoveAxis 로 애니메이터를 굴린다
    /// </code>
    ///
    /// ⚠ <b>화면 기준 이동을 서버가 어떻게 아는가.</b>
    ///    <c>ApplyMovement</c> 는 <c>cameraTransform</c> 으로 앞/오른쪽을 정하는데
    ///    <b>서버에는 카메라가 없다.</b> 그래서 서버에서는 그 참조를 비워 월드 축을 쓰게 하고,
    ///    클라이언트가 보내 준 <c>LookYaw</c> 로 입력을 미리 돌려서 넣는다.
    ///    결과는 각자 자기 화면 기준으로 걷는 것과 같다.
    ///
    /// ⚠ <b>애니메이션을 프레임 간 위치 차이로 짐작하지 않는다.</b>
    ///    원격 복제본의 자리는 Fusion 이 보간해서 채우므로, 재서 쓰면 남의 화면에서만
    ///    다리가 멈춘다. ShipCoop 에서 겪은 문제라 처음부터 값을 보낸다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WarriorsLocalPlayerController))]
    public sealed class WarriorsNetPlayerMover : NetworkBehaviour
    {
        /// <summary>서버가 확정한 몸 기준 이동 축. 애니메이터가 이 값으로 돈다.</summary>
        [Networked]
        public Vector2 MoveAxis { get; private set; }

        /// <summary>서버가 확정한 접지 여부. 애니메이터의 IsGrounded 로 간다.</summary>
        [Networked]
        public NetworkBool Grounded { get; private set; }

        private WarriorsLocalPlayerController body;
        private CharacterController capsule;
        private Animator animator;
        private WarriorsPlayerLife life;

        public override void Spawned()
        {
            body = GetComponent<WarriorsLocalPlayerController>();
            capsule = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            life = GetComponent<WarriorsPlayerLife>();

            // 스스로 키보드를 읽지 않게 한다. 켜 두면 남의 캐릭터까지 내 키보드로 움직인다.
            // 컴포넌트를 꺼도 ApplyMovement 는 밖에서 부를 수 있다. Awake 는 이미 돌았다.
            body.enabled = false;

            if (HasStateAuthority)
            {
                // 서버에는 카메라가 없다. 참조를 비워 월드 축을 쓰게 하고,
                // 화면 기준 회전은 우리가 LookYaw 로 직접 넣는다.
                body.ConfigureCamera(null);
            }
            else
            {
                // 클라이언트에서는 CharacterController 를 꺼 둔다.
                // 켜 두면 NetworkTransform 이 보내 준 자리와 싸운다.
                if (capsule != null) capsule.enabled = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // 서버만 자리를 확정한다. 클라이언트는 NetworkTransform 으로 결과만 받는다.
            if (!HasStateAuthority || body == null)
            {
                return;
            }

            // 일시정지 중에는 자리를 그대로 둔다. 중력도 걷기도 쉰다.
            if (WarriorsMatchState.PausedNow) return;

            Vector2 axis = Vector2.zero;

            // 쓰러진 사람은 움직이지 않는다. 부활은 없다.
            bool down = life != null && life.IsDown;

            // 2 · 3페이즈는 정해진 자리에서 한다. 입력을 버리되 ApplyMovement 는
            // 계속 불러야 한다 - 그 안에 중력과 접지가 들어 있어, 건너뛰면 발이 뜬다.
            WarriorsMatchState match = WarriorsMatchState.Current;
            bool held = match != null && match.MovementLocked;

            if (!down && !held && GetInput(out WarriorsInputData input))
            {
                Vector2 raw = Vector2.ClampMagnitude(input.Move, 1f);

                // 카메라 각도만큼 돌려 둔다. 서버는 월드 축으로 걷지만
                // 그 축에 이미 화면 방향이 들어가 있으므로 결과가 같다.
                Vector3 turned = Quaternion.Euler(0f, input.LookYaw, 0f) * new Vector3(raw.x, 0f, raw.y);
                axis = new Vector2(turned.x, turned.z);
            }

            body.ApplyMovement(axis, Runner.DeltaTime);

            // 실제로 움직인 결과를 보낸다. 벽에 막혔으면 입력이 있어도 값이 작다.
            MoveAxis = body.LastMoveInput;
            Grounded = capsule != null && capsule.isGrounded;
        }

        /// <summary>
        /// **서버가 이 사람을 정해진 자리로 옮긴다.**
        ///
        /// 2페이즈가 열릴 때 좌우 담당 자리에 세우는 데 쓴다.
        ///
        /// ⚠ <c>CharacterController</c> 가 켜져 있으면 <c>transform.position</c> 대입을
        ///    되돌린다. 그 부품은 자기가 아는 자리를 따로 들고 있어서, 끄고 옮긴 뒤
        ///    다시 켜야 실제로 옮겨진다.
        ///
        /// 자리는 <c>NetworkTransform</c> 이 실어 나르므로 클라이언트도 따라온다.
        /// </summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (!HasStateAuthority) return;

            bool wasEnabled = capsule != null && capsule.enabled;
            if (wasEnabled) capsule.enabled = false;

            // NetworkTransform 에 "순간이동" 이라고 알린다. 그냥 자리를 대입하면 클라이언트가
            // 해변에서 크라켄 앞까지의 거리를 보간해 캐릭터가 미끄러져 날아간다.
            NetworkTransform net = GetComponent<NetworkTransform>();

            if (net != null) net.Teleport(position, rotation);
            else transform.SetPositionAndRotation(position, rotation);

            if (wasEnabled) capsule.enabled = true;

            MoveAxis = Vector2.zero;
        }

        /// <summary>한 프레임에 이만큼(m) 넘게 움직였으면 걸어온 것이 아니라 옮겨진 것이다.</summary>
        private const float TeleportDistance = 3f;

        private Vector3 lastRenderPosition;
        private bool hasRenderPosition;

        /// <summary>
        /// 순간이동 뒤 칼의 잔상(TrailRenderer)을 지운다. **모든 화면에서.**
        ///
        /// 2페이즈 자리 배치로 캐릭터가 해변에서 크라켄 앞으로 뛰면, 칼끝의 잔상이 옛 자리와 새 자리를
        /// 잇는 긴 선으로 남는다. 실측으로 확인했다. 옮겨진 프레임에 잔상만 비운다.
        /// </summary>
        private void ClearTrailsAfterTeleport()
        {
            Vector3 now = transform.position;

            if (hasRenderPosition && (now - lastRenderPosition).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
                {
                    if (trail != null) trail.Clear();
                }
            }

            lastRenderPosition = now;
            hasRenderPosition = true;
        }

        /// <summary>
        /// 모든 화면에서 같은 값으로 애니메이션을 굴린다.
        ///
        /// 서버가 <c>ApplyMovement</c> 안에서 이미 자기 애니메이터를 채우지만,
        /// 클라이언트는 그 함수를 부르지 않으므로 여기서 같은 파라미터를 넣는다.
        /// 이름은 <c>WarriorsLocalPlayerController</c> 가 쓰는 것과 같아야 한다.
        /// </summary>
        /// <summary>쓰러진 캐릭터가 눕는 각도. 90도까지 가면 바닥을 뚫는다.</summary>
        private const float DownTilt = 78f;

        /// <summary>
        /// **쓰러진 자세를 손으로 눕히던 것을 되돌린다.**
        ///
        /// 예전에는 쓰러짐 클립이 없어 모델을 통째로 기울여 눕혔다. 지금은 진짜 클립이
        /// 있으므로(<c>WarriorsDown</c> 상태, <c>WarriorsPlayerLife</c> 가 <c>IsDown</c> 을
        /// 애니메이터에 넣는다) 그 기울이기가 <b>클립 위에 한 번 더 얹혀</b> 몸이 바닥에서
        /// 뜬 것처럼 보였다.
        ///
        /// 그래서 이제는 눕히지 않고, 예전에 기울여 둔 것이 남아 있으면 <b>똑바로 되돌리기만</b>
        /// 한다. 판이 다시 시작될 때 기울어진 채로 서 있는 것을 막기 위해 남겨 둔다.
        /// </summary>
        private void ClearManualDownTilt()
        {
            if (animator == null) return;

            Transform body = animator.transform;

            // 애니메이터가 루트에 붙어 있으면 여기서 돌릴 수 없다. 루트 회전은 NetworkTransform 이
            // 매 틱 덮어써서 싸우게 된다. 그 경우에는 자세를 바꾸지 않고 넘어간다.
            if (body == transform) return;

            if (body.localRotation == Quaternion.identity) return;

            body.localRotation = Quaternion.RotateTowards(
                body.localRotation, Quaternion.identity, 240f * Time.deltaTime);
        }

        public override void Render()
        {
            ClearTrailsAfterTeleport();
            ClearManualDownTilt();

            if (animator == null || HasStateAuthority)
            {
                return;
            }

            Vector2 axis = MoveAxis;
            bool moving = axis.sqrMagnitude > 0.0001f;

            animator.SetFloat("Hor", axis.x);
            animator.SetFloat("Vert", axis.y);
            animator.SetFloat("State", moving ? 1f : 0f);
            animator.SetFloat("Speed", moving ? axis.magnitude : 0f);
            animator.SetBool("IsGrounded", Grounded);
        }
    }
}
