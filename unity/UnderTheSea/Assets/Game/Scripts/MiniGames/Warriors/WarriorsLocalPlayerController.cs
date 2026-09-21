using UnityEngine;

namespace Warriors
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WarriorsLocalPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 720f;
        [SerializeField] private Transform cameraTransform;
        [Tooltip("IPlayerController 구현체. 비우면 같은 오브젝트와 부모에서 찾는다.")]
        [SerializeField] private MonoBehaviour playerControllerSource;

        // Anything under this is treated as no input at all, so a stick resting slightly
        // off centre - or a single frame of leftover input - cannot flicker the blend tree
        // between idle and running.
        private const float InputDeadZone = .15f;

        private CharacterController controller;
        private Animator animator;
        private IPlayerController playerController;

        public Vector2 LastMoveInput { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            ResolvePlayerController();
        }

        private void Update()
        {
            if (playerController == null) ResolvePlayerController();
            ApplyMovement(playerController != null ? playerController.Move : Vector2.zero, Time.deltaTime);
        }

        public void ApplyMovement(Vector2 input, float deltaTime)
        {
            LastMoveInput = Vector2.ClampMagnitude(input, 1f);
            if (LastMoveInput.sqrMagnitude < InputDeadZone * InputDeadZone) LastMoveInput = Vector2.zero;
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f; right.y = 0f; forward.Normalize(); right.Normalize();
            Vector3 movement = Vector3.ClampMagnitude(forward * LastMoveInput.y + right * LastMoveInput.x, 1f);

            if (movement.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(movement), rotationSpeed * deltaTime);
            }

            Vector3 velocity = movement * moveSpeed;
            velocity.y = controller.isGrounded ? -2f : Physics.gravity.y;
            controller.Move(velocity * deltaTime);

            if (animator != null)
            {
                animator.SetFloat("Hor", LastMoveInput.x);
                animator.SetFloat("Vert", LastMoveInput.y);
                bool moving = movement.sqrMagnitude > InputDeadZone * InputDeadZone;
                animator.SetFloat("State", moving ? 1f : 0f);
                animator.SetFloat("Speed", moving ? movement.magnitude : 0f);
                animator.SetBool("IsGrounded", controller.isGrounded);
            }
        }

        public void ConfigureCamera(Transform value) => cameraTransform = value;

        public void ConfigurePlayerController(MonoBehaviour source)
        {
            playerControllerSource = source;
            ResolvePlayerController();
        }

        private void ResolvePlayerController()
        {
            playerController = playerControllerSource as IPlayerController
                ?? GetComponent<IPlayerController>()
                ?? GetComponentInParent<IPlayerController>();

            // 완드가 꽂혀 있으면 무쌍 배치를 알려준다. 기본값(Shared)은 왼손 버튼 2 를
            // 달리기 토글로 잠그는데, 그 조작은 배에만 있다. 무쌍은 누른 그대로 내보낸다.
            (playerController as IotPlayerController)?.SetControlProfile(IotControlProfile.Warriors);

            if (playerController != null || !Application.isPlaying) return;

            // WarriorsTest처럼 SceneBootstrap 없이 기존 플레이어가 씬에 직접 배치된
            // 테스트 씬도 동작해야 한다. 실제 IoT 컨트롤러가 붙어 있으면 위에서
            // 선택되므로, 아무 구현체도 없을 때에만 키보드 폴백을 생성한다.
            KeyboardPlayerController keyboardController =
                GetComponent<KeyboardPlayerController>()
                ?? gameObject.AddComponent<KeyboardPlayerController>();
            keyboardController.SetControlProfile(KeyboardControlProfile.Warriors);
            playerControllerSource = keyboardController;
            playerController = keyboardController;
        }

        public void RespawnAt(Transform point)
        {
            if (point == null) return;
            RespawnAt(point.position, point.rotation);
        }

        public void RespawnAt(Vector3 position, Quaternion rotation)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            LastMoveInput = Vector2.zero;
            if (animator == null) animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetFloat("Hor", 0f);
                animator.SetFloat("Vert", 0f);
                animator.SetFloat("State", 0f);
                animator.SetFloat("Speed", 0f);
            }
        }
    }
}
