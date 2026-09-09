using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Transport-independent game request for reel resistance. A normalized value of
    /// zero is a valid active-control command and is intentionally distinct from Stop().
    /// </summary>
    public struct FishingResistanceCommand
    {
        public static FishingResistanceCommand Zero => new FishingResistanceCommand(
            0f,
            0f,
            0f,
            false,
            FishingV2BehaviorState.None,
            false);

        public float ResistanceNormalized { get; }
        public float BaseResistanceNormalized { get; }
        public float HeadShakeOverlayNormalized { get; }
        public bool HeadShakePulseActive { get; }
        public FishingV2BehaviorState SourceBehavior { get; }
        public bool IsFinalRun { get; }

        public FishingResistanceCommand(
            float resistanceNormalized,
            float baseResistanceNormalized = 0f,
            float headShakeOverlayNormalized = 0f,
            bool headShakePulseActive = false,
            FishingV2BehaviorState sourceBehavior = FishingV2BehaviorState.None,
            bool isFinalRun = false)
        {
            ResistanceNormalized = SanitizeNormalized(resistanceNormalized);
            BaseResistanceNormalized = SanitizeNormalized(baseResistanceNormalized);
            HeadShakeOverlayNormalized = SanitizeNormalized(headShakeOverlayNormalized);
            HeadShakePulseActive = headShakePulseActive;
            SourceBehavior = sourceBehavior;
            IsFinalRun = isFinalRun;
        }

        private static float SanitizeNormalized(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return FishingMath.Clamp01(value);
        }
    }

    public interface IFishingResistanceOutput
    {
        /// <summary>
        /// Applies an active-control command. ResistanceNormalized == 0 remains an
        /// active command and must not be interpreted as a safety stop.
        /// </summary>
        void ApplyCommand(FishingResistanceCommand command);

        /// <summary>
        /// Immediately enters the separate safety-stop state, bypassing all slew.
        /// </summary>
        void Stop();
    }
}
