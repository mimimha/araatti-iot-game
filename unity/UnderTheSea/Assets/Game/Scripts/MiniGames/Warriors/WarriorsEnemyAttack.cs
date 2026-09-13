using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsEnemyAttack : MonoBehaviour
    {
        [SerializeField] private WarriorsHealth playerHealth;
        [SerializeField] private WarriorsBeachEnemyApproach approach;
        [SerializeField, Min(1)] private int damage = 5;
        [SerializeField, Min(.5f)] private float attackRange = 2.3f;
        [SerializeField, Min(.1f)] private float warningSeconds = .75f;
        [SerializeField, Min(.2f)] private float cooldownSeconds = 2.4f;
        private float nextAttackTime;
        private float warningEndsAt;
        private bool warning;

        public bool IsWarning => warning;

        private void Update()
        {
            if (playerHealth == null || playerHealth.IsDead) return;
            float distance = Vector3.Distance(transform.position, playerHealth.transform.position);
            if (!warning)
            {
                if (distance <= attackRange && Time.time >= nextAttackTime)
                {
                    warning = true;
                    warningEndsAt = Time.time + warningSeconds;
                }
                return;
            }

            if (Time.time < warningEndsAt) return;
            warning = false;
            nextAttackTime = Time.time + cooldownSeconds;
            if (distance <= attackRange + .45f)
            {
                playerHealth.TryApplyDamage(damage);
            }
        }

        public void Configure(WarriorsHealth target, WarriorsBeachEnemyApproach movement, int attackDamage)
        {
            playerHealth = target;
            approach = movement;
            damage = Mathf.Max(1, attackDamage);
        }

        private void OnDisable() => warning = false;
    }
}
