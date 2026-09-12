using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    [RequireComponent(typeof(Collider))]
    public sealed class WarriorsDamageDealer : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damage = 1;

        private readonly HashSet<WarriorsTarget> damagedTargets = new();
        private WarriorsAttackDirection activeDirection;
        private bool isAttackActive;

        public void BeginAttack(WarriorsAttackDirection direction)
        {
            activeDirection = direction;
            damagedTargets.Clear();
            isAttackActive = true;
        }

        public void EndAttack()
        {
            isAttackActive = false;
            damagedTargets.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isAttackActive)
            {
                return;
            }

            WarriorsTarget target = other.GetComponentInParent<WarriorsTarget>();
            if (target == null || !damagedTargets.Add(target))
            {
                return;
            }

            target.TryReceiveAttack(activeDirection, damage);
        }

        private void OnDisable()
        {
            EndAttack();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            damage = Mathf.Max(1, damage);
            Collider hitbox = GetComponent<Collider>();
            if (hitbox != null)
            {
                hitbox.isTrigger = true;
            }
        }
#endif
    }
}
