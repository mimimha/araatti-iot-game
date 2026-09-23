using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Canonical V3 reel input for one simulation tick.
    /// The value is measured in revolutions of the reel/crank output shaft.
    /// </summary>
    public readonly struct FishingV3ReelInput
    {
        public static readonly FishingV3ReelInput Zero = new FishingV3ReelInput(0f);

        public float ReelDeltaRevolutions { get; }

        public FishingV3ReelInput(float reelDeltaRevolutions)
        {
            ReelDeltaRevolutions = IsFinite(reelDeltaRevolutions) && reelDeltaRevolutions > 0f
                ? reelDeltaRevolutions
                : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Tuning for virtual input devices that expose normalized reel intensity
    /// instead of physical reel revolutions.
    /// </summary>
    public sealed class FishingV3ReelInputTuning
    {
        public const float DefaultVirtualReelSpeedRevolutionsPerSecond = 0.5f;

        public float VirtualReelSpeedRevolutionsPerSecond =
            DefaultVirtualReelSpeedRevolutionsPerSecond;

        public FishingV3ReelInputTuning Copy()
        {
            return (FishingV3ReelInputTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            if (!IsFinite(VirtualReelSpeedRevolutionsPerSecond) ||
                VirtualReelSpeedRevolutionsPerSecond < 0f)
            {
                VirtualReelSpeedRevolutionsPerSecond =
                    DefaultVirtualReelSpeedRevolutionsPerSecond;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Converts a legacy normalized reel intensity into the canonical V3
    /// per-tick reel rotation amount. Physical input adapters can provide
    /// FishingV3ReelInput directly once they have converted their own units.
    /// </summary>
    public sealed class FishingV3ReelInputAdapter
    {
        private readonly FishingV3ReelInputTuning _tuning;

        public float VirtualReelSpeedRevolutionsPerSecond =>
            _tuning.VirtualReelSpeedRevolutionsPerSecond;

        public FishingV3ReelInputAdapter(FishingV3ReelInputTuning tuning = null)
        {
            _tuning = (tuning ?? new FishingV3ReelInputTuning()).Copy();
            _tuning.Sanitize();
        }

        public FishingV3ReelInput ConvertNormalizedInput(
            float normalizedInput,
            float deltaTime)
        {
            if (!IsFinite(deltaTime) || deltaTime <= 0f) return FishingV3ReelInput.Zero;

            float intensity = IsFinite(normalizedInput)
                ? FishingMath.Clamp01(normalizedInput)
                : 0f;
            if (intensity <= 0f || VirtualReelSpeedRevolutionsPerSecond <= 0f)
            {
                return FishingV3ReelInput.Zero;
            }

            double reelDeltaRevolutions =
                (double)intensity * VirtualReelSpeedRevolutionsPerSecond * deltaTime;
            float finiteReelDelta = reelDeltaRevolutions >= float.MaxValue
                ? float.MaxValue
                : (float)reelDeltaRevolutions;
            return new FishingV3ReelInput(finiteReelDelta);
        }

        public FishingV3ReelInput ConvertLegacyFrame(
            FishingInputFrame legacyInput,
            float deltaTime)
        {
            return ConvertNormalizedInput(legacyInput.ReelDelta, deltaTime);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
