using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    [RequireComponent(typeof(Collider))]
    public sealed class WarriorsWeaponHitbox : MonoBehaviour
    {
        private readonly HashSet<WarriorsTarget> hitTargets = new();
        private Collider hitbox;
        private WarriorsAttackDirection direction;
        private int damage;

        private void Awake() { hitbox = GetComponent<Collider>(); hitbox.enabled = false; }

        public void Begin(WarriorsAttackDirection attackDirection, int attackDamage)
        {
            direction = attackDirection; damage = attackDamage; hitTargets.Clear(); hitbox.enabled = true;
        }

        public void End() { if (hitbox != null) hitbox.enabled = false; hitTargets.Clear(); }

        private void OnTriggerEnter(Collider other) => TryHit(other);

        public bool TryHit(Collider other)
        {
            WarriorsTarget target = other.GetComponentInParent<WarriorsTarget>();
            if (target == null || !hitTargets.Add(target)) return false;
            bool accepted = target.TryReceiveAttack(direction, damage, ResolveAttacker());
            if (accepted) target.GetComponent<WarriorsTargetFeedback>()?.PlayHit();
            return accepted;
        }

        private void OnDisable() => End();

        /// <summary>
        /// 이 칼을 휘두른 사람. **담당이 갈린 대상**(2페이즈 촉수)을 가릴 때 쓴다.
        ///
        /// 무기는 손 본에 끼워지므로 위로 거슬러 올라가면 사람이 나온다.
        /// 장착 시점이 <c>Awake</c> 보다 늦어서 처음 쓸 때 찾아 기억해 둔다.
        /// </summary>
        private GameObject ResolveAttacker()
        {
            if (attacker == null)
            {
                WarriorsPlayerCombat owner = GetComponentInParent<WarriorsPlayerCombat>();
                if (owner != null) attacker = owner.gameObject;
            }

            return attacker;
        }

        private GameObject attacker;
    }
}
