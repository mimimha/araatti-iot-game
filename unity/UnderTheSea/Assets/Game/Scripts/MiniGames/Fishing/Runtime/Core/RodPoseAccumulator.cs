using System;

namespace FishingMiniGame.Core
{
    /// <summary>
    /// Converts directional input into the persistent normalized rod pose shared by
    /// keyboard and future relative-orientation input sources.
    /// </summary>
    public sealed class RodPoseAccumulator
    {
        public const float PoseUnitsPerSecond = 0.9f;
        private const float MotionRisePerSecond = 10f;
        private const float MotionDecayPerSecond = 5f;

        public float Pitch { get; private set; }
        public float Yaw { get; private set; }
        public float MotionStrength { get; private set; }

        public void Step(float pitchDirection, float yawDirection, float deltaTime)
        {
            float dt = FishingMath.Clamp(deltaTime, 0f, 0.25f);
            if (dt <= 0f) return;

            float previousPitch = Pitch;
            float previousYaw = Yaw;
            Pitch = FishingMath.Clamp(
                Pitch + FishingMath.Clamp(pitchDirection, -1f, 1f) * PoseUnitsPerSecond * dt,
                -1f,
                1f);
            Yaw = FishingMath.Clamp(
                Yaw + FishingMath.Clamp(yawDirection, -1f, 1f) * PoseUnitsPerSecond * dt,
                -1f,
                1f);

            float pitchSpeed = Math.Abs(Pitch - previousPitch) / dt;
            float yawSpeed = Math.Abs(Yaw - previousYaw) / dt;
            float targetMotion = FishingMath.Clamp01(
                FishingMath.Max(pitchSpeed, yawSpeed) / PoseUnitsPerSecond);
            float rate = targetMotion > MotionStrength
                ? MotionRisePerSecond
                : MotionDecayPerSecond;
            MotionStrength = MoveTowards(MotionStrength, targetMotion, rate * dt);
        }

        public void Recenter()
        {
            Pitch = 0f;
            Yaw = 0f;
            MotionStrength = 0f;
        }

        public void Reset()
        {
            Recenter();
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + (target > current ? maxDelta : -maxDelta);
        }
    }
}
