using System.Collections;
using UnityEngine;

namespace Warriors
{
    [RequireComponent(typeof(WarriorsHealth))]
    public sealed class WarriorsTargetVisual : MonoBehaviour
    {
        [SerializeField] private WarriorsHealth health;
        [SerializeField] private Transform visualRoot;

        private Vector3 initialScale;
        private Coroutine reactionRoutine;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<WarriorsHealth>();
            }

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            initialScale = visualRoot.localScale;
        }

        private void OnEnable()
        {
            health.HealthChanged += HandleHealthChanged;
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDied;
        }

        private void HandleHealthChanged(int current, int maximum)
        {
            if (current <= 0)
            {
                return;
            }

            if (reactionRoutine != null)
            {
                StopCoroutine(reactionRoutine);
            }

            reactionRoutine = StartCoroutine(PunchScale());
        }

        private void HandleDied()
        {
            if (reactionRoutine != null)
            {
                StopCoroutine(reactionRoutine);
            }

            reactionRoutine = StartCoroutine(Defeat());
        }

        private IEnumerator PunchScale()
        {
            const float duration = 0.16f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float pulse = 1f + Mathf.Sin(elapsed / duration * Mathf.PI) * 0.2f;
                visualRoot.localScale = initialScale * pulse;
                elapsed += Time.deltaTime;
                yield return null;
            }

            visualRoot.localScale = initialScale;
            reactionRoutine = null;
        }

        private IEnumerator Defeat()
        {
            const float duration = 0.35f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float normalized = elapsed / duration;
                visualRoot.localScale = Vector3.Lerp(initialScale, Vector3.zero, normalized);
                visualRoot.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
                elapsed += Time.deltaTime;
                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
