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
            bool accepted = target.TryReceiveAttack(direction, damage);
            if (accepted) target.GetComponent<WarriorsTargetFeedback>()?.PlayHit();
            return accepted;
        }

        private void OnDisable() => End();
    }
}
