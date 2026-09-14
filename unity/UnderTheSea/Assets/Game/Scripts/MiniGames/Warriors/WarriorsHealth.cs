using System;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private bool immortalForTesting;
        [SerializeField, Range(.1f, 1f)] private float incomingDamageMultiplier = 1f;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDead => CurrentHealth <= 0;
        public bool ImmortalForTesting => immortalForTesting;

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;

        private void Awake()
        {
            ResetHealth();
        }

        public bool TryApplyDamage(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return false;
            }

            int effectiveAmount = Mathf.Max(1, Mathf.RoundToInt(amount * incomingDamageMultiplier));
            CurrentHealth = Mathf.Max(immortalForTesting ? 1 : 0, CurrentHealth - effectiveAmount);
            Damaged?.Invoke(effectiveAmount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (IsDead)
            {
                Died?.Invoke();
            }

            return true;
        }

        public void ConfigureTesting(bool immortal, float damageMultiplier = .5f)
        {
            immortalForTesting = immortal;
            incomingDamageMultiplier = Mathf.Clamp(damageMultiplier, .1f, 1f);
            if (immortalForTesting && CurrentHealth <= 0) CurrentHealth = 1;
        }

        public bool TryKill()
        {
            if (IsDead) return false;
            CurrentHealth = 0;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            Died?.Invoke();
            return true;
        }

        /// <summary>
        /// Health is one pool for the whole run, so damage taken while learning ROUND 1
        /// used to follow the player all the way to the kraken with no way back. Clearing
        /// a round hands some of it back.
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead || CurrentHealth >= maxHealth) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            incomingDamageMultiplier = Mathf.Clamp(incomingDamageMultiplier, .1f, 1f);
        }
#endif
    }
}
