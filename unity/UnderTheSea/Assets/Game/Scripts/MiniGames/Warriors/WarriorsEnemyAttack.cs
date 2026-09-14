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

        /// <summary>
        /// When the wave is allowed to wind up its next swing. Every enemy used to run
        /// its own cooldown, so a ring of twelve emptied a full health bar in about five
        /// seconds and none of it could be read. Sharing one cadence keeps the crowd as
        /// big as it ever was while the threat arrives one telegraphed swing at a time -
        /// which is the part the player is meant to answer.
        /// </summary>
        private static float nextWaveWindUpTime;
        private static float waveInterval = 1.35f;

        public static void ConfigureWaveCadence(float seconds)
        {
            waveInterval = Mathf.Max(.15f, seconds);
            nextWaveWindUpTime = 0f;
        }

        public bool IsWarning => warning;

        private void Update()
        {
            if (playerHealth == null || playerHealth.IsDead) return;
            float distance = Vector3.Distance(transform.position, playerHealth.transform.position);
            if (!warning)
            {
                if (distance <= attackRange && Time.time >= nextAttackTime
                    && Time.time >= nextWaveWindUpTime)
                {
                    warning = true;
                    warningEndsAt = Time.time + warningSeconds;
                    // Claimed for the whole wave, not just for this enemy. Cutting it down
                    // mid wind up spends the turn without taking the hit.
                    nextWaveWindUpTime = Time.time + waveInterval;
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
            // Close to somewhere this enemy can actually swing from.
            approach?.ConfigureHoldDistance(attackRange);
        }

        private void OnDisable() => warning = false;
    }
}
