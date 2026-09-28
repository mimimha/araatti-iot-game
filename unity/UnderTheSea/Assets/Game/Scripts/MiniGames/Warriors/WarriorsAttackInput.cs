using System;

namespace Warriors
{
    [Serializable]
    public readonly struct WarriorsAttackInput
    {
        public int PlayerId { get; }
        public WarriorsAttackDirection AttackType { get; }
        public float Strength { get; }
        public double Timestamp { get; }

        public WarriorsAttackInput(int playerId, WarriorsAttackDirection attackType, float strength, double timestamp)
        {
            PlayerId = Math.Max(0, playerId);
            AttackType = attackType;
            Strength = Math.Clamp(strength, 0f, 1f);
            Timestamp = timestamp;
        }
    }
}
