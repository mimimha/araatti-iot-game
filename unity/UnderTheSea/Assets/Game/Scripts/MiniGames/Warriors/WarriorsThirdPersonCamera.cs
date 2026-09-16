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

        private void LateUpdate()
        {
            if (target == null) return;
            if (playerController == null) ResolvePlayerController();
            Vector2 look = playerController != null ? playerController.Look : Vector2.zero;
            RotateOrbit(look.x, look.y, Time.deltaTime);

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
            pitch = Mathf.Clamp(pitch + vertical * turnSpeed * .45f * deltaTime, 5f, 35f);
        }

        public void Shake(float amount) => shakeAmount = Mathf.Max(shakeAmount, amount);
    }
}
