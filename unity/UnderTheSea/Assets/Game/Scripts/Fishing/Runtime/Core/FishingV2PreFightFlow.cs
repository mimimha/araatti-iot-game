namespace FishingMiniGame.Core
{
    public enum FishingV2WaitingAttemptReason
    {
        Initial,
        EarlyHook,
        MissedBite
    }

    /// <summary>
    /// Device-agnostic timing and signal tuning for the SingleFish V2 flow before Fighting.
    /// Existing FishingRules remain authoritative for cast, raw bite, hook-window and settle timing.
    /// </summary>
    public sealed class FishingV2PreFightTuning
    {
        public float NibbleLeadMinSeconds = 0.65f;
        public float NibbleLeadMaxSeconds = 1.00f;
        public float NibbleDurationSeconds = 0.28f;
        public float NibbleToBiteGapSeconds = 0.18f;
        public float NibbleIntensityNormalized = 0.28f;
        public float EarlyHookPenaltyMinSeconds = 0.60f;
        public float EarlyHookPenaltyMaxSeconds = 1.00f;
        public float MissedBiteRetryMinSeconds = 0.75f;
        public float MissedBiteRetryMaxSeconds = 1.20f;

        public FishingV2PreFightTuning Copy()
        {
            return (FishingV2PreFightTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            NibbleDurationSeconds = FishingMath.Max(0.01f, NibbleDurationSeconds);
            NibbleToBiteGapSeconds = FishingMath.Max(0f, NibbleToBiteGapSeconds);
            float minimumLead = NibbleDurationSeconds + NibbleToBiteGapSeconds;
            NibbleLeadMinSeconds = FishingMath.Max(minimumLead, NibbleLeadMinSeconds);
            NibbleLeadMaxSeconds = FishingMath.Max(NibbleLeadMinSeconds, NibbleLeadMaxSeconds);
            NibbleIntensityNormalized = FishingMath.Clamp01(NibbleIntensityNormalized);
            EarlyHookPenaltyMinSeconds = FishingMath.Max(0f, EarlyHookPenaltyMinSeconds);
            EarlyHookPenaltyMaxSeconds = FishingMath.Max(
                EarlyHookPenaltyMinSeconds, EarlyHookPenaltyMaxSeconds);
            MissedBiteRetryMinSeconds = FishingMath.Max(0.01f, MissedBiteRetryMinSeconds);
            MissedBiteRetryMaxSeconds = FishingMath.Max(
                MissedBiteRetryMinSeconds, MissedBiteRetryMaxSeconds);
        }
    }
}
