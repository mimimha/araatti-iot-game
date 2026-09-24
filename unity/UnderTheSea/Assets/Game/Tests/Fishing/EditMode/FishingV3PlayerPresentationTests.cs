using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3PlayerPresentationTests
    {
        private GameObject _host;
        private GameObject _player;
        private GameObject _remotePlayer;
        private GameObject _target;
        private Transform _handAnchor;
        private Transform _skeleton;
        private Vector3 _skeletonBasePosition;
        private Quaternion _skeletonBaseRotation;
        private FishingV3PlayerPresentation _presenter;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("FishingV3PlayerPresentationTests");
            _player = new GameObject("LocalPlayer");
            _remotePlayer = new GameObject("RemotePlayer");
            _target = new GameObject("FishVisualAnchor");
            GameObject hand = new GameObject("RightHandProp");
            hand.transform.SetParent(_player.transform, false);
            hand.transform.localPosition = new Vector3(0.2f, 1f, 0.25f);
            _handAnchor = hand.transform;
            GameObject skeleton = new GameObject("Skeleton");
            skeleton.transform.SetParent(_player.transform, false);
            skeleton.transform.localPosition = new Vector3(0.03f, 0.05f, -0.02f);
            skeleton.transform.localRotation = Quaternion.Euler(1f, 2f, 3f);
            _skeleton = skeleton.transform;
            _skeletonBasePosition = _skeleton.localPosition;
            _skeletonBaseRotation = _skeleton.localRotation;
            _target.transform.position = new Vector3(0f, -0.3f, 4f);

            _presenter = _host.AddComponent<FishingV3PlayerPresentation>();
            _presenter.Configure(null, null, null);
            _presenter.ConfigureLocalPlayer(_player.transform, _target.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_remotePlayer);
            Object.DestroyImmediate(_target);
        }

        [Test]
        public void RodLifecycle_WaitHookFightTerminalAndNewSession()
        {
            _presenter.StepPresentation(ActiveFrame(FishingV3GameplayPhase.WaitingForBite), 0f);
            GameObject firstRod = _presenter.RodRoot;
            Assert.That(_presenter.HasRodVisual, Is.True);
            Assert.That(firstRod.transform.parent, Is.SameAs(_handAnchor));
            Vector3 directAim = (
                _target.transform.position + Vector3.up * 0.35f -
                _handAnchor.position).normalized;
            Assert.That(Vector3.Angle(directAim, firstRod.transform.forward),
                Is.EqualTo(FishingV3PlayerPresentation.DefaultBaseRodUpwardAngleDegrees)
                    .Within(0.01f));
            Assert.That(firstRod.transform.forward.y, Is.GreaterThan(directAim.y));

            _presenter.StepPresentation(ActiveFrame(FishingV3GameplayPhase.HookWindow), 0.1f);
            Assert.That(_presenter.RodRoot, Is.SameAs(firstRod));

            _presenter.StepPresentation(ActiveFrame(FishingV3GameplayPhase.Fighting), 0.1f);
            Assert.That(_presenter.RodRoot, Is.SameAs(firstRod));

            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                true,
                false,
                FishingV3GameplayPhase.Terminal,
                FishingV3Result.Caught,
                0.5f,
                0,
                FishingV3TimingGrade.None), 0f);
            Assert.That(_presenter.HasRodVisual, Is.False);

            _presenter.StepPresentation(ActiveFrame(FishingV3GameplayPhase.WaitingForBite), 0f);
            Assert.That(_presenter.HasRodVisual, Is.True);
            Assert.That(_presenter.RodRoot, Is.Not.SameAs(firstRod));
        }

        [Test]
        public void TimingSuccess_TriggersOnceWhileMissDoesNotTriggerPullReaction()
        {
            _presenter.StepPresentation(ActiveFrame(FishingV3GameplayPhase.Fighting), 0f);
            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                true,
                false,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                0.5f,
                1,
                FishingV3TimingGrade.Perfect), 0f);

            Assert.That(_presenter.IsTimingReactionActive, Is.True);
            Assert.That(_presenter.TimingReactionCount, Is.EqualTo(1));

            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                true,
                false,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                0.5f,
                1,
                FishingV3TimingGrade.Perfect), 1f);
            Assert.That(_presenter.TimingReactionCount, Is.EqualTo(1));
            Assert.That(_presenter.IsTimingReactionActive, Is.False);

            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                true,
                false,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                0.5f,
                2,
                FishingV3TimingGrade.Miss), 0f);
            Assert.That(_presenter.TimingReactionCount, Is.EqualTo(1));
            Assert.That(_presenter.IsTimingReactionActive, Is.False);
        }

        [Test]
        public void TensionIncrease_ProducesMonotonicBoundedRodBend()
        {
            float slack = FishingV3PlayerPresentation.EvaluateRodBendMeters(0.05f);
            float safe = FishingV3PlayerPresentation.EvaluateRodBendMeters(0.45f);
            float high = FishingV3PlayerPresentation.EvaluateRodBendMeters(0.75f);
            float danger = FishingV3PlayerPresentation.EvaluateRodBendMeters(1f);

            Assert.That(slack, Is.LessThan(safe));
            Assert.That(safe, Is.LessThan(high));
            Assert.That(high, Is.LessThan(danger));
            Assert.That(danger, Is.EqualTo(
                FishingV3PlayerPresentation.DefaultMaximumRodBendMeters).Within(0.0001f));
            Assert.That(danger, Is.InRange(0.45f, 0.50f));

            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 0.1f), 0f);
            float lowBend = _presenter.CurrentRodBendMeters;
            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 0.95f), 0f);
            Assert.That(_presenter.CurrentRodBendMeters, Is.GreaterThan(lowBend));
        }

        [Test]
        public void RemoteOrAbortedSession_NeverKeepsRodVisual()
        {
            _presenter.StepPresentation(ActiveFrame(FishingV3GameplayPhase.Fighting), 0f);
            Assert.That(_presenter.HasRodVisual, Is.True);
            Assert.That(_presenter.LocalPlayerRoot, Is.SameAs(_player.transform));
            Assert.That(_remotePlayer.GetComponentInChildren<MeshRenderer>(), Is.Null);

            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                false,
                false,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                0.5f,
                0,
                FishingV3TimingGrade.None), 0f);
            Assert.That(_presenter.HasRodVisual, Is.False);
        }

        [Test]
        public void DisableAndExplicitCaughtCleanup_RemoveRodWithoutChangingPlayerTransform()
        {
            Vector3 position = _player.transform.position;
            Quaternion rotation = _player.transform.rotation;
            _presenter.StepPresentation(ActiveFrame(FishingV3GameplayPhase.Fighting), 0f);

            _presenter.CleanupForCaughtPresentation();

            Assert.That(_presenter.HasRodVisual, Is.False);
            Assert.That(_player.transform.position, Is.EqualTo(position));
            Assert.That(_player.transform.rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void Tension_ProducesContinuousMonotonicStruggleIntensity()
        {
            float slack = FishingV3PlayerPresentation.EvaluateStruggleTargetIntensity(0f);
            float low = FishingV3PlayerPresentation.EvaluateStruggleTargetIntensity(0.25f);
            float safe = FishingV3PlayerPresentation.EvaluateStruggleTargetIntensity(0.5f);
            float high = FishingV3PlayerPresentation.EvaluateStruggleTargetIntensity(0.8f);
            float danger = FishingV3PlayerPresentation.EvaluateStruggleTargetIntensity(1f);
            float slow = FishingV3PlayerPresentation.EvaluateStruggleTargetFrequencyHz(0f);
            float normal = FishingV3PlayerPresentation.EvaluateStruggleTargetFrequencyHz(0.5f);
            float fast = FishingV3PlayerPresentation.EvaluateStruggleTargetFrequencyHz(0.8f);
            float fastest = FishingV3PlayerPresentation.EvaluateStruggleTargetFrequencyHz(1f);

            Assert.That(slack, Is.GreaterThan(0f));
            Assert.That(slack, Is.LessThan(low));
            Assert.That(low, Is.LessThan(safe));
            Assert.That(safe, Is.LessThan(high));
            Assert.That(high, Is.LessThan(danger));
            Assert.That(slack * FishingV3PlayerPresentation.DefaultMaximumStruggleLeanDegrees,
                Is.EqualTo(3.5f).Within(0.001f));
            Assert.That(low * FishingV3PlayerPresentation.DefaultMaximumStruggleLeanDegrees,
                Is.InRange(3.8f, 4.2f));
            Assert.That(safe * FishingV3PlayerPresentation.DefaultMaximumStruggleLeanDegrees,
                Is.InRange(5f, 7f));
            Assert.That(high * FishingV3PlayerPresentation.DefaultMaximumStruggleLeanDegrees,
                Is.InRange(9f, 12f));
            Assert.That(danger * FishingV3PlayerPresentation.DefaultMaximumStruggleLeanDegrees,
                Is.EqualTo(15f).Within(0.001f));
            Assert.That(slow, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(slow, Is.LessThan(normal));
            Assert.That(normal, Is.LessThan(fast));
            Assert.That(fast, Is.LessThan(fastest));
            Assert.That(fastest, Is.EqualTo(1.55f).Within(0.001f));
        }

        [Test]
        public void FightingAlwaysStrugglesAndHigherTensionIsStrongerWithoutMovingRoot()
        {
            Vector3 playerPosition = _player.transform.position;
            Quaternion playerRotation = _player.transform.rotation;

            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 0f,
                zone: FishingV3TensionZone.Slack), 0.2f);
            float slackIntensity = _presenter.CurrentStruggleIntensity;
            FishingV3StrugglePose weakPose = FishingV3PlayerPresentation.EvaluateStrugglePose(
                slackIntensity,
                _presenter.StrugglePhaseRadians);
            Assert.That(_presenter.IsStruggleActive, Is.True);
            Assert.That(_skeleton.localPosition,
                Is.EqualTo(_skeletonBasePosition + weakPose.LocalPositionOffset));

            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 1f,
                zone: FishingV3TensionZone.Danger), 1f);
            float strongIntensity = _presenter.CurrentStruggleIntensity;
            FishingV3StrugglePose strongPose = FishingV3PlayerPresentation.EvaluateStrugglePose(
                strongIntensity,
                _presenter.StrugglePhaseRadians);
            Assert.That(strongIntensity, Is.GreaterThan(slackIntensity));
            Assert.That(_skeleton.localPosition,
                Is.EqualTo(_skeletonBasePosition + strongPose.LocalPositionOffset));
            Assert.That(strongPose.LocalEulerOffset.magnitude,
                Is.GreaterThan(weakPose.LocalEulerOffset.magnitude));
            Assert.That(strongPose.LocalPositionOffset.magnitude, Is.LessThan(0.06f));
            Assert.That(_player.transform.position, Is.EqualTo(playerPosition));
            Assert.That(_player.transform.rotation, Is.EqualTo(playerRotation));
            Assert.That(_presenter.StruggleTarget, Is.SameAs(_skeleton));
        }

        [Test]
        public void TensionChangesBlendWithoutRestartAndPauseFreezesPhase()
        {
            FishingV3PlayerPresentationFrame safe = ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 0.5f,
                zone: FishingV3TensionZone.Safe);
            _presenter.StepPresentation(safe, 0.1f);
            int activations = _presenter.StruggleActivationCount;
            float phase = _presenter.StrugglePhaseSeconds;
            float phaseRadians = _presenter.StrugglePhaseRadians;

            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 1f,
                zone: FishingV3TensionZone.Danger), 0.15f);
            Assert.That(_presenter.StruggleActivationCount, Is.EqualTo(activations));
            Assert.That(_presenter.StrugglePhaseSeconds, Is.GreaterThan(phase));
            Assert.That(_presenter.StrugglePhaseRadians, Is.GreaterThan(phaseRadians));
            Assert.That(_presenter.CurrentStruggleIntensity,
                Is.LessThan(_presenter.TargetStruggleIntensity));
            Assert.That(_presenter.CurrentStruggleFrequencyHz,
                Is.LessThan(_presenter.TargetStruggleFrequencyHz));

            float risingIntensity = _presenter.CurrentStruggleIntensity;
            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 0f,
                zone: FishingV3TensionZone.Slack), 0.15f);
            Assert.That(_presenter.CurrentStruggleIntensity, Is.LessThan(risingIntensity));
            Assert.That(_presenter.CurrentStruggleIntensity,
                Is.GreaterThan(_presenter.TargetStruggleIntensity));
            Assert.That(_presenter.CurrentStruggleFrequencyHz,
                Is.GreaterThan(_presenter.TargetStruggleFrequencyHz));
            Assert.That(_presenter.StruggleActivationCount, Is.EqualTo(activations));

            FishingV3PlayerPresentationFrame pausedHigh = new(
                true,
                true,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                0.75f,
                0,
                FishingV3TimingGrade.None,
                FishingV3TensionZone.High);
            phase = _presenter.StrugglePhaseSeconds;
            phaseRadians = _presenter.StrugglePhaseRadians;
            float intensity = _presenter.CurrentStruggleIntensity;
            float frequency = _presenter.CurrentStruggleFrequencyHz;
            _presenter.StepPresentation(pausedHigh, 1f);
            Assert.That(_presenter.StrugglePhaseSeconds, Is.EqualTo(phase));
            Assert.That(_presenter.StrugglePhaseRadians, Is.EqualTo(phaseRadians));
            Assert.That(_presenter.CurrentStruggleIntensity, Is.EqualTo(intensity));
            Assert.That(_presenter.CurrentStruggleFrequencyHz, Is.EqualTo(frequency));
        }

        [Test]
        public void FightingForTenSeconds_UsesOneContinuouslyIncreasingPhase()
        {
            for (int index = 0; index < 100; index++)
            {
                float tension = index % 2 == 0 ? 0.45f : 0.55f;
                _presenter.StepPresentation(ActiveFrame(
                    FishingV3GameplayPhase.Fighting,
                    tension,
                    tension < 0.5f
                        ? FishingV3TensionZone.Safe
                        : FishingV3TensionZone.High), 0.1f);
            }

            Assert.That(_presenter.StrugglePhaseSeconds, Is.EqualTo(10f).Within(0.001f));
            Assert.That(_presenter.StrugglePhaseRadians, Is.GreaterThan(50f));
            Assert.That(_presenter.StruggleActivationCount, Is.EqualTo(1));
        }

        [Test]
        public void TimingReactionAndStruggle_CoexistThenTerminalRestoresBasePose()
        {
            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                tension: 0.7f,
                zone: FishingV3TensionZone.Safe), 0.2f);
            float phaseBeforeTiming = _presenter.StrugglePhaseRadians;
            int activations = _presenter.StruggleActivationCount;
            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                true,
                false,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                0.9f,
                1,
                FishingV3TimingGrade.Perfect,
                FishingV3TensionZone.Danger), 0.05f);

            Assert.That(_presenter.IsTimingReactionActive, Is.True);
            Assert.That(_presenter.IsStruggleActive, Is.True);
            Assert.That(_presenter.HasRodVisual, Is.True);
            Assert.That(_presenter.StrugglePhaseRadians, Is.GreaterThan(phaseBeforeTiming));
            Assert.That(_presenter.StruggleActivationCount, Is.EqualTo(activations));

            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                true,
                false,
                FishingV3GameplayPhase.Terminal,
                FishingV3Result.LineBroken,
                1f,
                1,
                FishingV3TimingGrade.Perfect,
                FishingV3TensionZone.Danger), 0.1f);

            Assert.That(_presenter.IsStruggleActive, Is.False);
            Assert.That(_presenter.HasRodVisual, Is.False);
            Assert.That(_skeleton.localPosition, Is.EqualTo(_skeletonBasePosition));
            Assert.That(Quaternion.Angle(_skeleton.localRotation, _skeletonBaseRotation),
                Is.LessThan(0.001f));
        }

        [Test]
        public void WaitingNewSessionAndRemoteSession_NeverApplyStruggle()
        {
            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                zone: FishingV3TensionZone.Danger), 0.1f);
            Assert.That(_presenter.IsStruggleActive, Is.True);

            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.WaitingForBite,
                zone: FishingV3TensionZone.Danger), 0.1f);
            Assert.That(_presenter.IsStruggleActive, Is.False);
            Assert.That(_presenter.StrugglePhaseSeconds, Is.Zero);
            Assert.That(_presenter.StrugglePhaseRadians, Is.Zero);
            Assert.That(_skeleton.localPosition, Is.EqualTo(_skeletonBasePosition));

            _presenter.StepPresentation(ActiveFrame(
                FishingV3GameplayPhase.Fighting,
                zone: FishingV3TensionZone.High), 0.1f);
            Assert.That(_presenter.IsStruggleActive, Is.True);
            Assert.That(_presenter.CurrentStruggleIntensity, Is.GreaterThan(0f));

            _presenter.StepPresentation(new FishingV3PlayerPresentationFrame(
                false,
                false,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                1f,
                0,
                FishingV3TimingGrade.None,
                FishingV3TensionZone.Danger), 0.1f);
            Assert.That(_presenter.IsStruggleActive, Is.False);
            Assert.That(_skeleton.localPosition, Is.EqualTo(_skeletonBasePosition));
        }

        private static FishingV3PlayerPresentationFrame ActiveFrame(
            FishingV3GameplayPhase phase,
            float tension = 0.5f,
            FishingV3TensionZone zone = FishingV3TensionZone.Safe)
        {
            return new FishingV3PlayerPresentationFrame(
                true,
                false,
                phase,
                FishingV3Result.Active,
                tension,
                0,
                FishingV3TimingGrade.None,
                zone);
        }
    }
}
