using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV2PresentationFeedbackTests
    {
        [Test]
        public void NibbleSequence_TriggersOnceAndResetDoesNotTrigger()
        {
            FishingV2PresentationFeedback feedback = new FishingV2PresentationFeedback();
            FishingV2PresentationInput input = Waiting();
            input.NibbleEventSequence = 1;

            Assert.That(feedback.Tick(input, 0f, false).NibbleStarted, Is.True);
            Assert.That(feedback.Tick(input, 0f, false).NibbleStarted, Is.False);

            input.NibbleEventSequence = 0;
            FishingV2VisualFrame reset = feedback.Tick(input, 0f, false);
            Assert.That(reset.NibbleStarted, Is.False);

            input.NibbleEventSequence = 1;
            Assert.That(feedback.Tick(input, 0f, false).NibbleStarted, Is.True);
        }

        [Test]
        public void BiteSequence_TriggersOnceAndResetDoesNotTrigger()
        {
            FishingV2PresentationFeedback feedback = new FishingV2PresentationFeedback();
            FishingV2PresentationInput input = Waiting();
            input.PlayerState = FishingPlayerState.BiteWindow;
            input.BiteEventSequence = 4;

            Assert.That(feedback.Tick(input, 0f, false).BiteStarted, Is.True);
            Assert.That(feedback.Tick(input, 0f, false).BiteStarted, Is.False);
            input.BiteEventSequence = 0;
            Assert.That(feedback.Tick(input, 0f, false).BiteStarted, Is.False);
        }

        [Test]
        public void HeadShakeSequence_TriggersOnceAndResetDoesNotTrigger()
        {
            FishingV2PresentationFeedback feedback = new FishingV2PresentationFeedback();
            FishingV2PresentationInput input = Fighting(FishingV2BehaviorState.Fight, 0.5f);
            input.HeadShakeActive = true;
            input.HeadShakeIntensityNormalized = 0.8f;
            input.HeadShakeEventSequence = 2;

            FishingV2VisualFrame started = feedback.Tick(input, 0f, false);
            Assert.That(started.HeadShakeStarted, Is.True);
            Assert.That(started.RodKickNormalized, Is.GreaterThan(0f));
            Assert.That(started.LineJitterNormalized, Is.GreaterThan(0f));
            Assert.That(started.SurfaceDisturbanceNormalized, Is.GreaterThan(0.38f));
            Assert.That(feedback.Tick(input, 0f, false).HeadShakeStarted, Is.False);
            input.HeadShakeEventSequence = 0;
            Assert.That(feedback.Tick(input, 0f, false).HeadShakeStarted, Is.False);
        }

        [Test]
        public void BiteReaction_IsStrongerThanNibbleReaction()
        {
            FishingV2PresentationInput nibble = Waiting();
            nibble.IsNibbling = true;
            nibble.NibbleIntensityNormalized = 0.55f;
            nibble.NibbleEventSequence = 1;
            FishingV2VisualFrame nibbleFrame = new FishingV2PresentationFeedback().Tick(nibble, 0f, false);

            FishingV2PresentationInput bite = Waiting();
            bite.PlayerState = FishingPlayerState.BiteWindow;
            bite.BiteEventSequence = 1;
            FishingV2VisualFrame biteFrame = new FishingV2PresentationFeedback().Tick(bite, 0f, false);

            Assert.That(biteFrame.RodKickNormalized, Is.GreaterThan(nibbleFrame.RodKickNormalized));
            Assert.That(biteFrame.SurfaceDisturbanceNormalized, Is.GreaterThan(nibbleFrame.SurfaceDisturbanceNormalized));
            Assert.That(biteFrame.BobberDipMeters, Is.GreaterThan(nibbleFrame.BobberDipMeters));
        }

        [Test]
        public void BehaviorLoad_IsOrderedRestFightRun()
        {
            FishingV2VisualFrame rest = Evaluate(Fighting(FishingV2BehaviorState.Rest, 0.5f));
            FishingV2VisualFrame fight = Evaluate(Fighting(FishingV2BehaviorState.Fight, 0.5f));
            FishingV2VisualFrame run = Evaluate(Fighting(FishingV2BehaviorState.Run, 0.5f));

            Assert.That(rest.RodLoadNormalized, Is.LessThan(fight.RodLoadNormalized));
            Assert.That(fight.RodLoadNormalized, Is.LessThan(run.RodLoadNormalized));
            Assert.That(rest.SurfaceDisturbanceNormalized, Is.LessThan(fight.SurfaceDisturbanceNormalized));
            Assert.That(fight.SurfaceDisturbanceNormalized, Is.LessThan(run.SurfaceDisturbanceNormalized));
        }

        [Test]
        public void FinalRun_StrengthensRunWithoutExceedingCap()
        {
            FishingV2PresentationInput normalInput = Fighting(FishingV2BehaviorState.Run, 0.70f);
            FishingV2PresentationInput finalInput = normalInput;
            finalInput.IsFinalRun = true;

            FishingV2VisualFrame normal = Evaluate(normalInput);
            FishingV2VisualFrame finalRun = Evaluate(finalInput);

            Assert.That(finalRun.RodLoadNormalized, Is.GreaterThanOrEqualTo(normal.RodLoadNormalized));
            Assert.That(finalRun.SurfaceDisturbanceNormalized, Is.GreaterThanOrEqualTo(normal.SurfaceDisturbanceNormalized));
            Assert.That(finalRun.RodLoadNormalized, Is.LessThanOrEqualTo(0.96f));
            Assert.That(finalRun.SurfaceDisturbanceNormalized, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void LineSag_LowTensionIsGreaterThanHighTension()
        {
            FishingV2VisualFrame low = Evaluate(Fighting(FishingV2BehaviorState.Fight, 0.10f));
            FishingV2VisualFrame high = Evaluate(Fighting(FishingV2BehaviorState.Fight, 0.92f));

            Assert.That(low.LineSagMeters, Is.GreaterThan(high.LineSagMeters));
        }

        [Test]
        public void PreFightLinePolicy_DoesNotInterpretVirtualTension()
        {
            FishingV2PresentationInput low = Waiting();
            low.VirtualLineTensionNormalized = 0f;
            FishingV2PresentationInput high = low;
            high.VirtualLineTensionNormalized = 1f;

            Assert.That(Evaluate(low).LineSagMeters, Is.EqualTo(Evaluate(high).LineSagMeters).Within(0.0001f));

            FishingV2PresentationInput nibble = high;
            nibble.IsNibbling = true;
            FishingV2PresentationInput bite = high;
            bite.PlayerState = FishingPlayerState.BiteWindow;
            bite.BiteEventSequence = 1;
            Assert.That(Evaluate(low).LineSagMeters, Is.GreaterThan(Evaluate(nibble).LineSagMeters));
            Assert.That(Evaluate(nibble).LineSagMeters, Is.GreaterThan(Evaluate(bite).LineSagMeters));
        }

        [Test]
        public void Pause_FreezesPulsePhaseAndCameraImpulse()
        {
            FishingV2PresentationFeedback feedback = new FishingV2PresentationFeedback();
            FishingV2PresentationInput bite = Waiting();
            bite.PlayerState = FishingPlayerState.BiteWindow;
            bite.BiteEventSequence = 1;
            FishingV2VisualFrame initial = feedback.Tick(bite, 0f, false);

            FishingV2VisualFrame pausedA = feedback.Tick(bite, 0.1f, true);
            FishingV2VisualFrame pausedB = feedback.Tick(bite, 0.1f, true);

            Assert.That(pausedB.BitePulseNormalized, Is.EqualTo(pausedA.BitePulseNormalized).Within(0.0001f));
            Assert.That(pausedB.AnimationPhaseSeconds, Is.EqualTo(pausedA.AnimationPhaseSeconds).Within(0.0001f));
            Assert.That(pausedB.CameraPositionOffset, Is.EqualTo(pausedA.CameraPositionOffset));
            Assert.That(pausedB.CameraRotationEuler, Is.EqualTo(pausedA.CameraRotationEuler));
            Assert.That(initial.BitePulseNormalized, Is.EqualTo(pausedA.BitePulseNormalized).Within(0.0001f));

            FishingV2VisualFrame resumed = feedback.Tick(bite, 0.1f, false);
            FishingV2VisualFrame advanced = feedback.Tick(bite, 0.1f, false);
            Assert.That(advanced.BitePulseNormalized, Is.LessThan(resumed.BitePulseNormalized));
            Assert.That(advanced.AnimationPhaseSeconds, Is.GreaterThan(resumed.AnimationPhaseSeconds));
        }

        [Test]
        public void Outputs_AreFiniteAndNormalizedForInvalidInput()
        {
            FishingV2PresentationInput input = Fighting(FishingV2BehaviorState.Run, float.PositiveInfinity);
            input.FishForceNormalized = float.NaN;
            input.FishDirectionNormalized = float.NegativeInfinity;
            input.HeadShakeActive = true;
            input.HeadShakeIntensityNormalized = float.PositiveInfinity;
            input.IsFinalRun = true;

            FishingV2VisualFrame frame = Evaluate(input);

            AssertNormalizedFinite(frame.RodLoadNormalized);
            AssertNormalizedFinite(frame.RodKickNormalized);
            Assert.That(frame.LateralPullNormalized, Is.InRange(-1f, 1f));
            Assert.That(float.IsNaN(frame.LateralPullNormalized) || float.IsInfinity(frame.LateralPullNormalized), Is.False);
            AssertNormalizedFinite(frame.SurfaceDisturbanceNormalized);
            AssertNormalizedFinite(frame.FishMotionNormalized);
            Assert.That(frame.LineSagMeters, Is.InRange(0f, 2f));
            Assert.That(float.IsNaN(frame.LineSagMeters) || float.IsInfinity(frame.LineSagMeters), Is.False);
        }

        private static FishingV2VisualFrame Evaluate(FishingV2PresentationInput input)
        {
            return new FishingV2PresentationFeedback().Tick(input, 0f, false);
        }

        private static FishingV2PresentationInput Waiting()
        {
            return new FishingV2PresentationInput
            {
                PlayerState = FishingPlayerState.Waiting,
                VirtualLineTensionNormalized = 0.5f
            };
        }

        private static FishingV2PresentationInput Fighting(FishingV2BehaviorState behavior, float tension)
        {
            return new FishingV2PresentationInput
            {
                PlayerState = FishingPlayerState.Fighting,
                BehaviorState = behavior,
                FishForceNormalized = 0.75f,
                FishDirectionNormalized = 1f,
                VirtualLineTensionNormalized = tension,
                VirtualTensionZone = FishingV2TensionZone.Good
            };
        }

        private static void AssertNormalizedFinite(float value)
        {
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
            Assert.That(value, Is.InRange(0f, 1f));
        }
    }
}
