using System;
using UnityEngine;

namespace Warriors
{
    [RequireComponent(typeof(WarriorsHealth))]
    public sealed class WarriorsTarget : MonoBehaviour
    {
        [SerializeField] private WarriorsAttackDirection requiredDirection;
        [SerializeField] private WarriorsHealth health;
        [SerializeField, Min(0)] private int scoreValue = 100;
        [SerializeField, Min(0f)] private float directionalHitPadding;
        [SerializeField, Min(1)] private int bossHitsRequired = 2;
        private WarriorsBattleScore battleScore;
        private bool deathHandled;
        private bool showScorePopup = true;
        private bool acceptsAttacks = true;
        private bool destroyOnDefeat = true;
        private int bossHitsRemaining;
        private bool isBossPart;
        private float nextBossHitTime;

        public WarriorsAttackDirection RequiredDirection => requiredDirection;
        public bool IsDefeated => health != null && health.IsDead;
        public float DirectionalHitPadding => directionalHitPadding;
        public int BossHitsRemaining => bossHitsRemaining;

        /// <summary>
        /// True for kraken tentacles.  A swing resolves against boss parts one at a
        /// time, so this is what separates the ROUND 1 sweep from the ROUND 2 puzzle.
        /// </summary>
        public bool IsBossPart => isBossPart;

        /// <summary>
        /// How much of this tentacle's strike window is left, 1 down to 0, or -1 when it
        /// has none. A solo player has to answer a two arm pattern one arm after the
        /// other, so knowing how long is left is the difference between a decision and a
        /// guess. Display only - the strike itself is the boss's business.
        /// </summary>
        public float StrikeWindowNormalized =>
            strikeWindowEnd < 0f || strikeWindowLength <= 0f
                ? -1f
                : Mathf.Clamp01((strikeWindowEnd - Time.time) / strikeWindowLength);

        private float strikeWindowEnd = -1f;
        private float strikeWindowLength;

        public void BeginStrikeWindow(float seconds)
        {
            strikeWindowLength = Mathf.Max(.01f, seconds);
            strikeWindowEnd = Time.time + strikeWindowLength;
        }

        public void ClearStrikeWindow() => strikeWindowEnd = -1f;

        public event Action<WarriorsTarget> Defeated;
        public event Action<WarriorsTarget, WarriorsAttackDirection> HitAccepted;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<WarriorsHealth>();
            }
        }

        private void OnEnable()
        {
            deathHandled = false;
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        /// <summary>
        /// Whether this target would accept the swing right now. The player's swing uses it
        /// to skip a tentacle that is still inside its hit cooldown and take the next
        /// matching one instead - otherwise a second tentacle sharing a weakness with the
        /// one just hit could never be reached.
        /// </summary>
        public bool CanReceiveAttack(WarriorsAttackDirection direction)
        {
            if (!acceptsAttacks || deathHandled) return false;
            if (direction != requiredDirection) return false;
            return destroyOnDefeat || Time.time >= nextBossHitTime;
        }

        public bool TryReceiveAttack(WarriorsAttackDirection direction, int damage)
        {
            if (!acceptsAttacks || deathHandled) return false;
            if (direction != requiredDirection) return false;
            if (!destroyOnDefeat)
            {
                if (Time.time < nextBossHitTime) return false;
                nextBossHitTime = Time.time + .3f;
                bossHitsRemaining = Mathf.Max(0, bossHitsRemaining - 1);
                HitAccepted?.Invoke(this, direction);
                if (bossHitsRemaining > 0) return true;
            }
            return health != null && health.TryKill();
        }

        public void ConfigureRequiredDirection(WarriorsAttackDirection direction)
        {
            requiredDirection = direction;
        }

        public void ConfigureScore(WarriorsBattleScore score, int points)
        {
            battleScore = score;
            scoreValue = Mathf.Max(0, points);
        }

        public void ConfigureAsBossPart(int hitsRequired = 2)
        {
            isBossPart = true;
            RestoreHitboxes();
            // Tentacles stand out to the sides of a player who does not walk around, and the
            // thrust hitbox is a narrow forward box.  Without this a tentacle parked off to
            // one side could never be reached by a thrust at all, so a solo run would stall
            // on any pattern that asked for one.  Only the nearest matching boss part is ever
            // hit, so widening the reach cannot make a swing catch two tentacles at once.
            directionalHitPadding = Mathf.Max(directionalHitPadding, 3f);
            battleScore = null;
            scoreValue = 0;
            showScorePopup = false;
            destroyOnDefeat = false;
            bossHitsRequired = Mathf.Max(1, hitsRequired);
            bossHitsRemaining = bossHitsRequired;
            nextBossHitTime = 0f;
            deathHandled = false;
            health?.ResetHealth();
            GetComponent<WarriorsTargetFeedback>()?.ConfigureAsBossPart();
        }

        public void SetAttackEnabled(bool enabled) => acceptsAttacks = enabled;

        /// <summary>
        /// Puts the hitboxes back. Dying switches every collider off, and a tentacle slot is
        /// reused by later patterns, so without this a tentacle that went down once could
        /// never be struck again - it stood there with its weakness showing and swallowed
        /// every swing.
        /// </summary>
        private void RestoreHitboxes()
        {
            foreach (Collider hitbox in GetComponentsInChildren<Collider>(true)) hitbox.enabled = true;
        }

        public void ReviveBossPart(WarriorsAttackDirection direction)
        {
            requiredDirection = direction;
            deathHandled = false;
            health?.ResetHealth();
            bossHitsRemaining = bossHitsRequired;
            nextBossHitTime = 0f;
            RestoreHitboxes();
            gameObject.SetActive(true);
        }

        private void HandleDied()
        {
            if (deathHandled) return;
            deathHandled = true;
            GetComponent<WarriorsTargetFeedback>()?.PlayDefeat();
            if (showScorePopup)
            {
                battleScore?.RegisterKill(scoreValue);
                gameObject.AddComponent<WarriorsScorePopup>().Show(scoreValue);
            }
            Defeated?.Invoke(this);
            foreach (Collider hitbox in GetComponentsInChildren<Collider>()) hitbox.enabled = false;

            // A corpse used to keep walking at the player for the whole despawn. On the
            // fish and the crab that reads as a stumble, but the jellyfish floats, so it
            // looked like it was still alive well after the hit landed.
            if (TryGetComponent(out WarriorsBeachEnemyApproach approach)) approach.enabled = false;
            if (TryGetComponent(out WarriorsEnemyAttack enemyAttack)) enemyAttack.enabled = false;
            // Just past the defeat animation. The kill has to land the moment the swing
            // does, so there is no window where a beaten monster is still on screen.
            if (destroyOnDefeat) Destroy(gameObject, .4f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (health == null)
            {
                health = GetComponent<WarriorsHealth>();
            }
        }
#endif
    }
}
