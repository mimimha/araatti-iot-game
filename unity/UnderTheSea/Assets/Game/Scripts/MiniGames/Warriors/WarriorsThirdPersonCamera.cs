using UnityEngine;
using UnityEngine.InputSystem;

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
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                RotateOrbit((keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                    (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f), Time.deltaTime);
            }

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

        public void Configure(Transform value) => target = value;

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
            pitch = Mathf.Clamp(pitch + vertical * turnSpeed * .45f * deltaTime, 5f, 35f);
        }

        public void Shake(float amount) => shakeAmount = Mathf.Max(shakeAmount, amount);
    }
}
