using System;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private bool immortalForTesting;
        [SerializeField, Range(.1f, 1f)] private float incomingDamageMultiplier = 1f;

        /// <summary>
        /// 한 번 맞은 뒤 이만큼은 다시 맞지 않는다(초). 0 이면 끄는 것이고 기본값이 0 이다.
        ///
        /// <b>플레이어에게만 켠다.</b> 몬스터는 0 으로 두어야 한 번의 광역 베기로
        /// 같은 종류 여러 마리가 한꺼번에 죽는 1페이즈의 손맛이 그대로 남는다.
        ///
        /// 예전에는 이 창이 아예 없어서, 몬스터가 몸에 붙으면 각자의
        /// <c>cooldownSeconds</c> 만 지키며 여럿이 동시에 때렸다. 목숨을 1개로 줄이면
        /// 그대로 순식간에 녹는다.
        /// </summary>
        [SerializeField, Min(0f)] private float damageCooldownSeconds;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDead => CurrentHealth <= 0;
        public bool ImmortalForTesting => immortalForTesting;

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;

        /// <summary>피격 무적이 풀리는 시각. <see cref="damageCooldownSeconds"/> 가 0 이면 쓰지 않는다.</summary>
        private float nextDamageTime;

        private void Awake()
        {
            ResetHealth();
        }

        /// <summary>
        /// 피해를 넣는다.
        ///
        /// <paramref name="ignoreCooldown"/> 는 <b>화면 맞추기 전용</b>이다.
        /// 클라이언트의 <c>WarriorsPlayerLife.MirrorHealth</c> 가 서버 HP 를 그대로 따라 그릴 때
        /// 쓴다. 거기서 피격 무적 창에 걸리면 서버는 깎였는데 내 화면만 안 깎여 영영 어긋난다.
        /// 실제 판정은 서버에서 이 창을 지켜 이미 한 번 걸러진 뒤다.
        /// </summary>
        public bool TryApplyDamage(int amount, bool ignoreCooldown = false)
        {
            if (amount <= 0 || IsDead)
            {
                return false;
            }

            if (!ignoreCooldown && damageCooldownSeconds > 0f)
            {
                if (Time.time < nextDamageTime) return false;
                nextDamageTime = Time.time + damageCooldownSeconds;
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
