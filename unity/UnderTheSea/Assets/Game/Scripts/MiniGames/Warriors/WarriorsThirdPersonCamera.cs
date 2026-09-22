using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 7f;
        [SerializeField] private float height = 3.2f;
        [SerializeField] private float turnSpeed = 90f;
        [SerializeField] private float followSharpness = 12f;
        [SerializeField] private float yaw;
        [SerializeField] private float pitch = 12f;
        [Tooltip("IPlayerController 구현체. 비우면 추적 대상에서 찾는다.")]
        [SerializeField] private MonoBehaviour playerControllerSource;
        private IPlayerController playerController;
        private float shakeAmount;

        /// <summary>
        /// 고정 구도로 잡혀 있는가. 켜져 있으면 대상을 따라가지도, 화살표로 돌지도 않는다.
        ///
        /// 2 · 3페이즈는 두 사람이 정해진 자리에 서서 크라켄을 상대하는 화면이다. 그런데 카메라가
        /// <b>자기 캐릭터</b>를 따라가면 두 클라이언트의 구도가 서로 달라진다 — 내 캐릭터는 늘 가운데,
        /// 상대는 한쪽 끝. 그래서 화면에 고정된 리듬 트랙이 어느 화면에서도 캐릭터 위에 오지 않았다.
        /// </summary>
        private bool fixedShot;

        private Vector3 fixedFocus;

        /// <summary>
        /// **아레나를 고정 구도로 잡는다.** 두 사람이 대칭으로 보이고 크라켄이 위를 채운다.
        ///
        /// <paramref name="centre"/> 는 사람들의 한가운데다. 바라보는 점을 그보다 높이 두면
        /// 캐릭터가 화면 아래쪽으로 내려가, 그 위에 리듬 트랙이 들어갈 자리가 생긴다.
        /// </summary>
        public void FocusArena(Vector3 centre, float back, float up, float aimUp)
        {
            fixedShot = true;
            fixedFocus = centre + Vector3.up * aimUp;

            transform.position = centre + new Vector3(0f, up, -back);
            transform.LookAt(fixedFocus);
        }

        /// <summary>다시 캐릭터를 따라가게 한다. (1페이즈)</summary>
        public void ReleaseFixed() => fixedShot = false;

        private void LateUpdate()
        {
            if (fixedShot)
            {
                // 자리는 FocusArena 가 이미 잡았다. 흔들림만 얹는다.
                if (shakeAmount > .001f)
                {
                    transform.position += UnityEngine.Random.insideUnitSphere * shakeAmount;
                    shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.deltaTime * 2.4f);
                }

                transform.LookAt(fixedFocus);
                return;
            }

            if (target == null) return;
            if (playerController == null) ResolvePlayerController();

            // IoT 검의 오른손 스틱. 기기가 없으면 0 이다. 스틱은 "얼마나 기울였나" 라
            // 프레임 시간을 곱해 속도로 바꾼다.
            Vector2 look = playerController != null ? playerController.Look : Vector2.zero;
            RotateOrbit(look.x, look.y, Time.deltaTime);

            // 마우스 우클릭 드래그. **로비와 같은 규격을 그대로 쓴다.** (CameraDragLook)
            // 마우스가 주는 것은 이미 지나간 거리라 프레임 시간을 곱하지 않는다.
            ApplyDragDegrees(CameraDragLook.ReadDegrees());

            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * 1.4f;
            Vector3 desired = focus + orbit * new Vector3(0f, height * .2f, -distance);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            if (shakeAmount > .001f)
            {
                transform.position += UnityEngine.Random.insideUnitSphere * shakeAmount;
                shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.deltaTime * 2.4f);
            }
            transform.LookAt(focus);
        }

        public void Configure(Transform value)
        {
            target = value;
            ResolvePlayerController();
        }

        public void ConfigurePlayerController(MonoBehaviour source)
        {
            playerControllerSource = source;
            ResolvePlayerController();
        }

        private void ResolvePlayerController()
        {
            playerController = playerControllerSource as IPlayerController;
            if (playerController != null || target == null) return;

            playerController = target.GetComponent<IPlayerController>()
                ?? target.GetComponentInParent<IPlayerController>()
                ?? target.GetComponentInChildren<IPlayerController>();
        }

        public void SnapToTarget(float targetYaw)
        {
            if (target == null) return;
            yaw = targetYaw;
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * 1.4f;
            transform.position = focus + orbit * new Vector3(0f, height * .2f, -distance);
            transform.LookAt(focus);
        }

        public void RotateOrbit(float horizontal, float vertical, float deltaTime)
        {
            yaw += horizontal * turnSpeed * deltaTime;
            pitch = Mathf.Clamp(pitch + vertical * turnSpeed * .45f * deltaTime, MinPitch, MaxPitch);
        }

        /// <summary>
        /// 위아래로 볼 수 있는 범위. **로비와 같게 맞춰 두었다.**
        ///
        /// 예전에는 5°~35° 였다. 로비에서 무쌍으로 넘어오면 같은 만큼 끌어도 화면이 덜 움직여
        /// 조작이 다른 게임처럼 느껴졌다. (<c>PlayerCamera.m_MinAngle</c> · <c>m_MaxAngle</c>)
        /// </summary>
        private const float MinPitch = 0f;

        private const float MaxPitch = 50f;

        /// <summary>
        /// **마우스 드래그로 화면을 돌린다.** 값은 이미 도 단위라 그대로 더한다.
        ///
        /// <b>부호는 로비를 따른다.</b> 로비는 <c>m_Angles.x += delta.y</c> 이고, 두 카메라 모두
        /// <c>Euler(pitch, yaw, 0)</c> 으로 뒤쪽 오프셋을 돌리므로 <b>pitch 가 커지면 카메라가
        /// 올라가 내려다본다.</b> 그래서 여기서도 더한다 — 위로 끌면 위에서 내려다보는 그림이다.
        ///
        /// ⚠ 뒤집지 마세요. 로비와 반대로 돌면 두 공간을 오갈 때마다 손이 헷갈립니다.
        ///    상하 반전이 필요하면 네 게임이 함께 정해 <c>IOT_INPUT.md</c> 에 적습니다.
        /// </summary>
        private void ApplyDragDegrees(Vector2 degrees)
        {
            if (degrees.sqrMagnitude <= 0f) return;

            yaw += degrees.x;
            pitch = Mathf.Clamp(pitch + degrees.y, MinPitch, MaxPitch);
        }

        public void Shake(float amount) => shakeAmount = Mathf.Max(shakeAmount, amount);
    }
}
