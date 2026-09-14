using UnityEngine;
using UnityEngine.InputSystem;

namespace Warriors
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WarriorsLocalPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 720f;
        [SerializeField] private Transform cameraTransform;

        // Anything under this is treated as no input at all, so a stick resting slightly
        // off centre - or a single frame of leftover input - cannot flicker the blend tree
        // between idle and running.
        private const float InputDeadZone = .15f;

        private CharacterController controller;
        private Animator animator;

        public Vector2 LastMoveInput { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            LastMoveInput = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

            ApplyMovement(LastMoveInput, Time.deltaTime);
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
