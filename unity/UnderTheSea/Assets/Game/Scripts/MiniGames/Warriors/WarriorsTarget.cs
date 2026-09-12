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
        private WarriorsBattleScore battleScore;
        private bool deathHandled;
        private bool showScorePopup = true;
        private bool acceptsAttacks = true;
        private bool destroyOnDefeat = true;

        public WarriorsAttackDirection RequiredDirection => requiredDirection;
        public bool IsDefeated => health != null && health.IsDead;

        public event Action<WarriorsTarget> Defeated;

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

        public bool TryReceiveAttack(WarriorsAttackDirection direction, int damage)
        {
            if (!acceptsAttacks || deathHandled) return false;
            if (direction != requiredDirection)
            {
                GetComponent<WarriorsTargetFeedback>()?.PlayRejected();
                return false;
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

        public void ConfigureAsBossPart()
        {
            battleScore = null;
            scoreValue = 0;
            showScorePopup = false;
            destroyOnDefeat = false;
        }

        public void SetAttackEnabled(bool enabled) => acceptsAttacks = enabled;

        public void ReviveBossPart(WarriorsAttackDirection direction)
        {
            requiredDirection = direction;
            deathHandled = false;
            health?.ResetHealth();
            foreach (Collider hitbox in GetComponentsInChildren<Collider>(true)) hitbox.enabled = true;
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
            if (destroyOnDefeat) Destroy(gameObject, .85f);
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
