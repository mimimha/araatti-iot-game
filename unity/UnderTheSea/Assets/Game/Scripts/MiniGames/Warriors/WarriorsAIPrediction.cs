using System;

namespace Warriors
{
    /// <summary>
    /// Transport-independent contract produced by an external IMU classifier.
    /// AttackType is exchanged by name, not by its numeric enum value.
    /// </summary>
    [Serializable]
    public readonly struct WarriorsAIPrediction
    {
        public int PlayerId { get; }
        public WarriorsAttackDirection AttackType { get; }
        public float Confidence { get; }
        public float Strength { get; }
        public double Timestamp { get; }

        public WarriorsAIPrediction(
            int playerId,
            WarriorsAttackDirection attackType,
            float confidence,
            float strength,
            double timestamp)
        {
            PlayerId = Math.Clamp(playerId, 0, 3);
            AttackType = attackType;
            Confidence = Math.Clamp(confidence, 0f, 1f);
            Strength = Math.Clamp(strength, 0f, 1f);
            Timestamp = timestamp;
        }
    }
}
