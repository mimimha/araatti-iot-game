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

        private void LateUpdate()
        {
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
