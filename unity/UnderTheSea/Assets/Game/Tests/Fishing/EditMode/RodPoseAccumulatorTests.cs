using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class RodPoseAccumulatorTests
    {
        [Test]
        public void Pose_IsClampedToNormalizedRange()
        {
            RodPoseAccumulator pose = new RodPoseAccumulator();

            for (int i = 0; i < 20; i++) pose.Step(1f, -1f, 0.25f);
            Assert.That(pose.Pitch, Is.EqualTo(1f));
            Assert.That(pose.Yaw, Is.EqualTo(-1f));

            for (int i = 0; i < 40; i++) pose.Step(-1f, 1f, 0.25f);
            Assert.That(pose.Pitch, Is.EqualTo(-1f));
            Assert.That(pose.Yaw, Is.EqualTo(1f));
        }

        [Test]
        public void Recenter_ResetsPoseWithoutExternalState()
        {
            RodPoseAccumulator pose = new RodPoseAccumulator();
            pose.Step(1f, -1f, 0.5f);

            pose.Recenter();

            Assert.That(pose.Pitch, Is.Zero);
            Assert.That(pose.Yaw, Is.Zero);
            Assert.That(pose.MotionStrength, Is.Zero);
        }

        [Test]
        public void Pose_RemainsWhereInputStopped()
        {
            RodPoseAccumulator pose = new RodPoseAccumulator();
            pose.Step(1f, 0.5f, 0.4f);
            float pitch = pose.Pitch;
            float yaw = pose.Yaw;

            for (int i = 0; i < 8; i++) pose.Step(0f, 0f, 0.1f);

            Assert.That(pose.Pitch, Is.EqualTo(pitch).Within(0.0001f));
            Assert.That(pose.Yaw, Is.EqualTo(yaw).Within(0.0001f));
        }

        [Test]
        public void MotionStrength_TracksPoseDeltaAndDecaysAfterMotionStops()
        {
            RodPoseAccumulator pose = new RodPoseAccumulator();
            pose.Step(1f, 0f, 0.05f);
            float whileMoving = pose.MotionStrength;

            pose.Step(0f, 0f, 0.05f);
            float afterStopping = pose.MotionStrength;
            for (int i = 0; i < 10; i++) pose.Step(0f, 0f, 0.05f);

            Assert.That(whileMoving, Is.GreaterThan(0f));
            Assert.That(afterStopping, Is.LessThan(whileMoving));
            Assert.That(pose.MotionStrength, Is.Zero.Within(0.0001f));
        }
    }
}
