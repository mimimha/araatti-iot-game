using System.Collections;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsWeaponController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float attackDuration = 0.48f;

        private Quaternion restRotation;
        private Coroutine attackRoutine;

        public bool IsAttacking { get; private set; }
        public WarriorsAttackDirection LastAttackDirection { get; private set; }

        private void Awake()
        {
            restRotation = transform.localRotation;
        }

        public void PlayAttack(WarriorsAttackDirection direction)
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
            }

            attackRoutine = StartCoroutine(AnimateAttack(direction));
        }

        private IEnumerator AnimateAttack(WarriorsAttackDirection direction)
        {
            IsAttacking = true;
            LastAttackDirection = direction;

            Vector3 axis = direction == WarriorsAttackDirection.HorizontalSlash ? Vector3.up : Vector3.forward;
            float start = direction == WarriorsAttackDirection.VerticalSlash ? 70f : -70f;
            float end = -start;

            for (float elapsed = 0f; elapsed < attackDuration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / attackDuration);
                transform.localRotation = restRotation * Quaternion.AngleAxis(Mathf.Lerp(start, end, t), axis);
                yield return null;
            }

            transform.localRotation = restRotation;
            IsAttacking = false;
            attackRoutine = null;
        }

        private void OnDisable()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            transform.localRotation = restRotation;
            IsAttacking = false;
        }
    }
}
