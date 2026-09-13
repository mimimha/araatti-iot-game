using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsComboSystem : MonoBehaviour
    {
        [SerializeField] private WarriorsPlayerCombat combat;
        [SerializeField] private WarriorsHealth playerHealth;
        [SerializeField] private WarriorsBattleScore score;
        [SerializeField] private WarriorsIoTFeedbackHub feedback;
        [SerializeField, Min(.5f)] private float resetAfterSeconds = 3f;
        private readonly Queue<WarriorsAttackDirection> sequence = new();
        private float lastSuccessTime;
        private float specialUntil;

        public int Combo { get; private set; }
        public float Multiplier => Combo >= 20 ? 2f : Combo >= 10 ? 1.5f : Combo >= 5 ? 1.2f : 1f;
        public string LastSpecial { get; private set; } = string.Empty;
        public string ActiveSpecial => Time.time < specialUntil ? LastSpecial : string.Empty;
        public event Action<string> SpecialTriggered;

        private void Awake()
        {
            if (combat == null) combat = UnityEngine.Object.FindFirstObjectByType<WarriorsPlayerCombat>(FindObjectsInactive.Include);
            if (playerHealth == null && combat != null) playerHealth = combat.GetComponent<WarriorsHealth>();
            if (score == null) score = UnityEngine.Object.FindFirstObjectByType<WarriorsBattleScore>(FindObjectsInactive.Include);
            if (feedback == null) feedback = UnityEngine.Object.FindFirstObjectByType<WarriorsIoTFeedbackHub>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (combat != null) combat.AttackResolved += HandleAttack;
            if (playerHealth != null) playerHealth.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (combat != null) combat.AttackResolved -= HandleAttack;
            if (playerHealth != null) playerHealth.Damaged -= HandleDamaged;
        }

        private void Update()
        {
            if (Combo > 0 && Time.time - lastSuccessTime >= resetAfterSeconds) ResetCombo();
        }

        private void HandleAttack(WarriorsAttackDirection direction, int defeatedCount)
        {
            if (defeatedCount <= 0) { ResetCombo(); return; }
            Combo += defeatedCount;
            lastSuccessTime = Time.time;
            score?.SetScoreMultiplier(Multiplier);
            sequence.Enqueue(direction);
            while (sequence.Count > 3) sequence.Dequeue();
            EvaluateSpecial();
            if (Combo == 5 || Combo == 10 || Combo == 20)
                feedback?.Request(0, WarriorsIoTFeedbackType.ComboMilestone, Mathf.Clamp01(Combo / 20f));
        }

        private void EvaluateSpecial()
        {
            if (sequence.Count < 3) return;
            WarriorsAttackDirection[] attacks = sequence.ToArray();
            if (attacks[0] == WarriorsAttackDirection.HorizontalSlash &&
                attacks[1] == WarriorsAttackDirection.VerticalSlash &&
                attacks[2] == WarriorsAttackDirection.Thrust)
                TriggerSpecial("TRIPLE SLASH");
            else if (attacks[0] == WarriorsAttackDirection.HorizontalSlash &&
                     attacks[1] == WarriorsAttackDirection.HorizontalSlash &&
                     attacks[2] == WarriorsAttackDirection.VerticalSlash)
                TriggerSpecial("POWER SLASH");
        }

        private void TriggerSpecial(string special)
        {
            LastSpecial = special;
            specialUntil = Time.time + 1.2f;
            sequence.Clear();
            SpecialTriggered?.Invoke(special);
        }

        private void HandleDamaged(int amount)
        {
            ResetCombo();
            feedback?.Request(0, WarriorsIoTFeedbackType.PlayerDamaged, Mathf.Clamp01(amount / 15f));
        }

        private void ResetCombo()
        {
            Combo = 0;
            LastSpecial = string.Empty;
            specialUntil = 0f;
            sequence.Clear();
            score?.SetScoreMultiplier(1f);
        }
    }
}
