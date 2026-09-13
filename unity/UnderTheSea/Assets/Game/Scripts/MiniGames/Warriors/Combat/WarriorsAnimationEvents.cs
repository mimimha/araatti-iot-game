using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsAnimationEvents : MonoBehaviour
    {
        [SerializeField] private WarriorsPlayerCombat combat;
        private void Awake() { if (combat == null) combat = GetComponent<WarriorsPlayerCombat>(); }
        public void BeginAttackHitbox() => combat?.BeginAttackHitbox();
        public void EndAttackHitbox() => combat?.EndAttackHitbox();
        public void FinishAttack() => combat?.FinishAttack();
    }
}
