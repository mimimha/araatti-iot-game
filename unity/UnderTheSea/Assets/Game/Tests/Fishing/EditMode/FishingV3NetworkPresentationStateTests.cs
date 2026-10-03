using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3NetworkPresentationStateTests
    {
        [Test]
        public void DefaultState_HasNoRemotePresentation()
        {
            FishingV3NetworkPresentationState state =
                FishingV3NetworkPresentationState.Inactive;

            Assert.That(state.IsFishing, Is.False);
            Assert.That(state.HasPresentation, Is.False);
            Assert.That(state.FishVisualId, Is.Empty);
        }

        [Test]
        public void EnterAndFightingState_PreserveSemanticPresentationData()
        {
            FishingV3NetworkPresentationState state = Create(
                true,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                FishingV3FishProfileId.Strong,
                "strong",
                FishingV3FishState.Run,
                FishingV3TensionZone.Danger,
                7);

            Assert.That(state.IsFishing, Is.True);
            Assert.That(state.HasPresentation, Is.True);
            Assert.That(state.GameplayPhase, Is.EqualTo(FishingV3GameplayPhase.Fighting));
            Assert.That(state.FishProfileId, Is.EqualTo(FishingV3FishProfileId.Strong));
            Assert.That(state.FishVisualId, Is.EqualTo("strong"));
            Assert.That(state.FishState, Is.EqualTo(FishingV3FishState.Run));
            Assert.That(state.HeadShakeEventSequence, Is.EqualTo(7));
            Assert.That(state.RepresentativeTensionNormalized, Is.EqualTo(0.95f));
        }

        [TestCase(FishingV3Result.Caught)]
        [TestCase(FishingV3Result.LineBroken)]
        [TestCase(FishingV3Result.FishEscaped)]
        public void TerminalState_RemainsPresentableUntilRemoteOneShotCompletes(
            FishingV3Result result)
        {
            FishingV3NetworkPresentationState state = Create(
                false,
                FishingV3GameplayPhase.Terminal,
                result,
                FishingV3FishProfileId.Normal,
                "normal",
                FishingV3FishState.Fight,
                FishingV3TensionZone.Safe,
                0);

            Assert.That(state.IsFishing, Is.False);
            Assert.That(state.HasPresentation, Is.True);
            Assert.That(state.GameplayPhase, Is.EqualTo(FishingV3GameplayPhase.Terminal));
            Assert.That(state.Result, Is.EqualTo(result));
        }

        [Test]
        public void AbortOrCleanup_ProducesNoPresentation()
        {
            FishingV3NetworkPresentationState state = Create(
                false,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                FishingV3FishProfileId.Small,
                "small",
                FishingV3FishState.Fight,
                FishingV3TensionZone.High,
                2);

            Assert.That(state.HasPresentation, Is.False);
            Assert.That(state.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.WaitingForBite));
        }

        [Test]
        public void Reentry_UsesNewSessionSequenceAndDoesNotEqualPreviousSession()
        {
            FishingV3NetworkPresentationState first = Create(
                true,
                FishingV3GameplayPhase.WaitingForBite,
                FishingV3Result.Active,
                FishingV3FishProfileId.Small,
                "small",
                FishingV3FishState.Calm,
                FishingV3TensionZone.Safe,
                0).WithSessionSequence(1);
            FishingV3NetworkPresentationState second =
                first.WithSessionSequence(2);

            Assert.That(second.SessionSequence, Is.EqualTo(2));
            Assert.That(second.Equals(first), Is.False);
        }

        [Test]
        public void RemoteFrame_UsesDiscreteZoneInsteadOfExactGameplayTension()
        {
            FishingV3NetworkPresentationState state = Create(
                true,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                FishingV3FishProfileId.Normal,
                "normal",
                FishingV3FishState.Fight,
                FishingV3TensionZone.High,
                0);

            FishingV3RemotePresentationFrame frame = state.ToRemoteFrame();

            Assert.That(frame.HasPresentation, Is.True);
            Assert.That(frame.TensionZone, Is.EqualTo(FishingV3TensionZone.High));
            Assert.That(state.RepresentativeTensionNormalized, Is.EqualTo(0.75f));
        }

        private static FishingV3NetworkPresentationState Create(
            bool isFishing,
            FishingV3GameplayPhase phase,
            FishingV3Result result,
            FishingV3FishProfileId profile,
            string visualId,
            FishingV3FishState fishState,
            FishingV3TensionZone tensionZone,
            int headShakeSequence)
        {
            return new FishingV3NetworkPresentationState(
                0,
                isFishing,
                false,
                phase,
                result,
                profile,
                visualId,
                fishState,
                tensionZone,
                headShakeSequence,
                0.8f);
        }
    }
}
