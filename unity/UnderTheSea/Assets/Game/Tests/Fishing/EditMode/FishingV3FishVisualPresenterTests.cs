using System.Collections.Generic;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3FishVisualPresenterTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject instance in _objects)
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
            _objects.Clear();
        }

        [TestCase("small", "FreeMackerel")]
        [TestCase("normal", "FreeBream")]
        [TestCase("strong", "FreeTuna")]
        public void Catalog_ResolvesV3VisualIdToExpectedExistingPrefab(
            string visualId,
            string expectedPrefabName)
        {
            FishingVisualSet catalog = LoadCatalog();

            Assert.That(catalog.TryGetFish(visualId, out FishingFishVisualEntry entry), Is.True);
            Assert.That(entry.Prefab, Is.Not.Null);
            Assert.That(entry.Prefab.name, Is.EqualTo(expectedPrefabName));
        }

        [TestCase(FishingV3FishProfileId.Small, "small", "FreeMackerel")]
        [TestCase(FishingV3FishProfileId.Normal, "normal", "FreeBream")]
        [TestCase(FishingV3FishProfileId.Strong, "strong", "FreeTuna")]
        public void ProfileVisualId_ResolvesToMatchingPrefab(
            FishingV3FishProfileId profileId,
            string expectedVisualId,
            string expectedPrefabName)
        {
            FishingV3FishProfile profile = FishingV3FishProfiles.Create(profileId);
            FishingVisualSet catalog = LoadCatalog();

            Assert.That(profile.VisualId, Is.EqualTo(expectedVisualId));
            Assert.That(catalog.TryGetFish(profile.VisualId, out FishingFishVisualEntry entry), Is.True);
            Assert.That(entry.Prefab.name, Is.EqualTo(expectedPrefabName));
        }

        [Test]
        public void Catalog_ProfilePresentationScalesAreClearlyOrdered()
        {
            FishingVisualSet catalog = LoadCatalog();

            Assert.That(catalog.TryGetFish("small", out FishingFishVisualEntry small), Is.True);
            Assert.That(catalog.TryGetFish("normal", out FishingFishVisualEntry normal), Is.True);
            Assert.That(catalog.TryGetFish("strong", out FishingFishVisualEntry strong), Is.True);

            Assert.That(small.LocalScale, Is.EqualTo(Vector3.one * 0.6f));
            Assert.That(normal.LocalScale, Is.EqualTo(Vector3.one));
            Assert.That(strong.LocalScale, Is.EqualTo(Vector3.one * 1.15f));
            Assert.That(small.LocalScale.magnitude, Is.LessThan(normal.LocalScale.magnitude));
            Assert.That(normal.LocalScale.magnitude, Is.LessThan(strong.LocalScale.magnitude));
        }

        [Test]
        public void MotionSettings_OrderCalmFightRunAndPreserveProfileDifferences()
        {
            FishingV3FishVisualMotionSettings calm =
                FishingV3FishVisualPresenter.GetMotionSettings(
                    FishingV3FishState.Calm,
                    FishingV3FishProfileId.Normal);
            FishingV3FishVisualMotionSettings fight =
                FishingV3FishVisualPresenter.GetMotionSettings(
                    FishingV3FishState.Fight,
                    FishingV3FishProfileId.Normal);
            FishingV3FishVisualMotionSettings run =
                FishingV3FishVisualPresenter.GetMotionSettings(
                    FishingV3FishState.Run,
                    FishingV3FishProfileId.Normal);
            FishingV3FishVisualMotionSettings small =
                FishingV3FishVisualPresenter.GetMotionSettings(
                    FishingV3FishState.Fight,
                    FishingV3FishProfileId.Small);
            FishingV3FishVisualMotionSettings strong =
                FishingV3FishVisualPresenter.GetMotionSettings(
                    FishingV3FishState.Fight,
                    FishingV3FishProfileId.Strong);

            Assert.That(calm.LateralAmplitude, Is.LessThan(fight.LateralAmplitude));
            Assert.That(fight.LateralAmplitude, Is.LessThan(run.LateralAmplitude));
            Assert.That(calm.FrequencyHz, Is.LessThan(fight.FrequencyHz));
            Assert.That(fight.FrequencyHz, Is.LessThan(run.FrequencyHz));
            Assert.That(run.ForwardAmplitude, Is.GreaterThan(fight.ForwardAmplitude));
            Assert.That(small.LateralAmplitude, Is.LessThan(fight.LateralAmplitude));
            Assert.That(small.FrequencyHz, Is.GreaterThan(fight.FrequencyHz));
            Assert.That(strong.LateralAmplitude, Is.GreaterThan(fight.LateralAmplitude));
        }

        [Test]
        public void MotionEvaluation_IsDeterministicAndBoundedForEveryStateAndProfile()
        {
            foreach (FishingV3FishProfileId profile in
                     System.Enum.GetValues(typeof(FishingV3FishProfileId)))
            {
                foreach (FishingV3FishState state in
                         System.Enum.GetValues(typeof(FishingV3FishState)))
                {
                    for (int sample = 0; sample <= 500; sample++)
                    {
                        float time = sample * 0.02f;
                        FishingV3FishVisualMotionPose first =
                            FishingV3FishVisualPresenter.EvaluateStateMotion(
                                state, profile, time);
                        FishingV3FishVisualMotionPose second =
                            FishingV3FishVisualPresenter.EvaluateStateMotion(
                                state, profile, time);

                        Assert.That(first.LocalPositionOffset,
                            Is.EqualTo(second.LocalPositionOffset));
                        Assert.That(Mathf.Abs(first.LocalPositionOffset.x),
                            Is.LessThanOrEqualTo(
                                FishingV3FishVisualPresenter.MaximumLateralOffset));
                        Assert.That(Mathf.Abs(first.LocalPositionOffset.y),
                            Is.LessThanOrEqualTo(
                                FishingV3FishVisualPresenter.MaximumVerticalOffset));
                        Assert.That(Mathf.Abs(first.LocalPositionOffset.z),
                            Is.LessThanOrEqualTo(
                                FishingV3FishVisualPresenter.MaximumForwardOffset));
                    }
                }
            }
        }

        [Test]
        public void StateTransition_ChangesMotionWithoutChangingGameplayState()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Normal);
            EnterFighting(fixture);
            fixture.Presenter.StepPresentation(0.2f);
            Vector3 fightPose = fixture.Presenter.CurrentMotionOffset;

            fixture.Controller.SetV3FishState(FishingV3FishState.Run);
            fixture.Presenter.StepPresentation(0f);
            Assert.That(fixture.Presenter.CurrentMotionState,
                Is.EqualTo(FishingV3FishState.Run));
            Assert.That(fixture.Controller.V3Snapshot.FishState,
                Is.EqualTo(FishingV3FishState.Run));

            fixture.Presenter.StepPresentation(
                FishingV3FishVisualPresenter.StateTransitionSeconds);

            Assert.That(Vector3.Distance(
                    fightPose,
                    fixture.Presenter.CurrentMotionOffset),
                Is.GreaterThan(0.01f));
            Assert.That(fixture.Controller.V3Snapshot.FishState,
                Is.EqualTo(FishingV3FishState.Run));
        }

        [Test]
        public void HeadShakeSequence_TriggersOnceThenReturnsToRunMotion()
        {
            FishingV3FishBehaviorTuning behavior = new FishingV3FishBehaviorTuning
            {
                CalmDurationMinSeconds = 10f,
                CalmDurationMaxSeconds = 10f,
                FightDurationMinSeconds = 10f,
                FightDurationMaxSeconds = 10f,
                RunDurationMinSeconds = 10f,
                RunDurationMaxSeconds = 10f,
                RunHeadShakeDelayMinSeconds = 0.01f,
                RunHeadShakeDelayMaxSeconds = 0.01f,
                HeadShakeIntensityMinNormalized = 1f,
                HeadShakeIntensityMaxNormalized = 1f
            };
            Fixture fixture = CreateFixture(
                FishingV3FishProfileId.Strong,
                behaviorTuning: behavior);
            EnterFighting(fixture);
            fixture.Controller.SetV3FishState(FishingV3FishState.Run);
            fixture.Input.SetNextFrame(ConnectedFrame());
            fixture.Controller.TickRuntime(0.02f);

            int sequence = fixture.Controller.V3Snapshot.HeadShakeEventSequence;
            fixture.Presenter.StepPresentation(0.01f);

            Assert.That(sequence, Is.GreaterThan(0));
            Assert.That(fixture.Presenter.IsHeadShakeActive, Is.True);
            Assert.That(fixture.Controller.V3Snapshot.FishState,
                Is.EqualTo(FishingV3FishState.Run));

            fixture.Presenter.StepPresentation(
                FishingV3FishVisualPresenter.HeadShakeDurationSeconds);
            fixture.Presenter.StepPresentation(0.05f);

            Assert.That(fixture.Presenter.IsHeadShakeActive, Is.False);
            Assert.That(fixture.Controller.V3Snapshot.HeadShakeEventSequence,
                Is.EqualTo(sequence));
            Assert.That(fixture.Presenter.CurrentMotionState,
                Is.EqualTo(FishingV3FishState.Run));
        }

        [Test]
        public void PausedRuntime_HoldsMotionAndTransientPhase()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Normal);
            EnterFighting(fixture);
            fixture.Presenter.StepPresentation(0.2f);
            float elapsed = fixture.Presenter.MotionElapsedSeconds;
            Vector3 pose = fixture.Presenter.CurrentMotionOffset;

            fixture.Controller.SetPaused(true);
            fixture.Presenter.StepPresentation(1f);

            Assert.That(fixture.Presenter.MotionElapsedSeconds,
                Is.EqualTo(elapsed).Within(0.000001f));
            Assert.That(fixture.Presenter.CurrentMotionOffset, Is.EqualTo(pose));
        }

        [Test]
        public void BiteHookLifecycle_ShowsExactlyOneFishOnlyDuringFighting()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Normal);

            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Controller.V3Snapshot.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.WaitingForBite));
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);

            EnterFighting(fixture);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.EqualTo(1));
            Assert.That(fixture.Presenter.ActiveVisual.name, Does.StartWith("FreeBream"));

            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.EqualTo(1));
        }

        [Test]
        public void TerminalAndAbort_RemoveFishWithoutLosingCaughtIdentity()
        {
            Fixture caught = CreateFixture(FishingV3FishProfileId.Strong);
            EnterFighting(caught);
            caught.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            caught.Controller.TickRuntime(1f);
            caught.Presenter.RefreshNow();

            Assert.That(caught.Controller.V3Snapshot.Result, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(caught.Presenter.ActiveVisualCount, Is.Zero);
            Assert.That(caught.Controller.V3Snapshot.FishVisualId, Is.EqualTo("strong"));
            Assert.That(caught.Presenter.LastCaughtVisualId, Is.EqualTo("strong"));
            Assert.That(caught.Presenter.HasCaughtVisual, Is.True);

            Fixture aborted = CreateFixture(FishingV3FishProfileId.Small);
            EnterFighting(aborted);
            aborted.Controller.AbortRound();
            aborted.Presenter.RefreshNow();

            Assert.That(aborted.Presenter.ActiveVisualCount, Is.Zero);
            Assert.That(aborted.Presenter.HasCaughtVisual, Is.False);
            Assert.That(aborted.Presenter.LastSessionVisualId, Is.EqualTo("small"));
        }

        [TestCase(FishingV3FishProfileId.Small, "small", "FreeMackerel")]
        [TestCase(FishingV3FishProfileId.Normal, "normal", "FreeBream")]
        [TestCase(FishingV3FishProfileId.Strong, "strong", "FreeTuna")]
        public void Caught_LiftsMatchingVisualThenAttachesToPreferredHandAnchor(
            FishingV3FishProfileId profileId,
            string expectedVisualId,
            string expectedPrefabName)
        {
            Fixture fixture = CreateFixture(profileId);
            EnterFighting(fixture);
            fixture.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.Caught));
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);
            Assert.That(fixture.Presenter.CaughtVisualCount, Is.EqualTo(1));
            Assert.That(fixture.Presenter.CaughtVisualId, Is.EqualTo(expectedVisualId));
            Assert.That(fixture.Presenter.CaughtVisual.name,
                Does.StartWith(expectedPrefabName));
            Assert.That(fixture.Presenter.IsCaughtLiftActive, Is.True);
            Assert.That(fixture.Presenter.CaughtVisual.transform.parent,
                Is.Not.SameAs(fixture.HandAnchor));

            fixture.Presenter.StepPresentation(
                FishingV3FishVisualPresenter.DefaultCaughtLiftSeconds);

            Assert.That(fixture.Presenter.CaughtVisual.transform.parent,
                Is.SameAs(fixture.HandAnchor));
            Assert.That(fixture.Presenter.CaughtHandAnchor,
                Is.SameAs(fixture.HandAnchor));
            Assert.That(fixture.Presenter.IsCaughtHoldActive, Is.True);
        }

        [Test]
        public void CaughtLift_StartsAtLastFightingPoseWithoutFirstFrameTeleport()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Normal);
            EnterFighting(fixture);
            fixture.Presenter.StepPresentation(0.23f);
            Transform fightingVisual = fixture.Presenter.ActiveVisual.transform;
            Vector3 expectedPosition = fightingVisual.position;
            Quaternion expectedRotation = fightingVisual.rotation;
            Vector3 expectedScale = fightingVisual.lossyScale;
            fixture.PlayerRoot.position = new Vector3(8f, 2f, -5f);
            fixture.HandAnchor.localPosition = new Vector3(0.4f, 1.2f, 0.25f);

            fixture.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Transform caughtVisual = fixture.Presenter.CaughtVisual.transform;
            Assert.That(Vector3.Distance(caughtVisual.position, expectedPosition),
                Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(caughtVisual.rotation, expectedRotation),
                Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(caughtVisual.lossyScale, expectedScale),
                Is.LessThan(0.0001f));
            Assert.That(fixture.Presenter.CaughtLiftStartWorldPosition,
                Is.EqualTo(expectedPosition));
            Assert.That(fixture.Presenter.CaughtLiftProgressNormalized, Is.Zero);
            Assert.That(caughtVisual.parent, Is.Not.SameAs(fixture.HandAnchor));
            Assert.That(Vector3.Distance(
                    caughtVisual.position,
                    fixture.HandAnchor.position),
                Is.GreaterThan(1f));
        }

        [Test]
        public void CaughtLift_UsesUpwardArcAndAttachesOnlyAfterTravelCompletes()
        {
            const float liftSeconds = 1f;
            const float holdSeconds = 0.75f;
            const float arcHeight = 0.6f;
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Strong);
            fixture.Presenter.ConfigureCaughtTransition(
                fixture.PlayerRoot,
                liftSeconds,
                holdSeconds,
                arcHeight);
            fixture.PlayerRoot.position = new Vector3(3f, 1f, -2f);
            fixture.HandAnchor.localPosition = new Vector3(0.5f, 1.1f, 0.2f);
            EnterFighting(fixture);
            Vector3 startPosition = fixture.Presenter.ActiveVisual.transform.position;

            fixture.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();
            fixture.Presenter.StepPresentation(0.5f);

            Vector3 targetPosition = fixture.HandAnchor.TransformPoint(Vector3.zero);
            float easedProgress = Mathf.SmoothStep(0f, 1f, 0.5f);
            float directPathY = Vector3.Lerp(
                startPosition,
                targetPosition,
                easedProgress).y;
            Assert.That(fixture.Presenter.CaughtLiftProgressNormalized,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Presenter.CaughtVisual.transform.position.y,
                Is.EqualTo(directPathY + arcHeight).Within(0.0001f));
            Assert.That(fixture.Presenter.CaughtVisual.transform.parent,
                Is.Not.SameAs(fixture.HandAnchor));

            fixture.Presenter.StepPresentation(0.5f);

            Assert.That(fixture.Presenter.CaughtLiftProgressNormalized, Is.EqualTo(1f));
            Assert.That(fixture.Presenter.CaughtVisual.transform.parent,
                Is.SameAs(fixture.HandAnchor));
            Assert.That(fixture.Presenter.CaughtVisual.transform.localPosition,
                Is.EqualTo(Vector3.zero));
            Assert.That(fixture.Presenter.IsCaughtHoldActive, Is.True);
        }

        [Test]
        public void CaughtLift_RemovesRodBeforeCaughtFishUsesSameHandAnchor()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Normal);
            FishingV3PlayerPresentation playerPresentation =
                fixture.Presenter.gameObject.AddComponent<FishingV3PlayerPresentation>();
            playerPresentation.Configure(null, null, fixture.Presenter);
            playerPresentation.ConfigureLocalPlayer(
                fixture.PlayerRoot,
                fixture.Presenter.PresentationAnchor);
            EnterFighting(fixture);
            playerPresentation.StepPresentation(
                new FishingV3PlayerPresentationFrame(
                    true,
                    false,
                    FishingV3GameplayPhase.Fighting,
                    FishingV3Result.Active,
                    0.5f,
                    0,
                    FishingV3TimingGrade.None),
                0f);
            Assert.That(playerPresentation.HasRodVisual, Is.True);

            fixture.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Assert.That(playerPresentation.HasRodVisual, Is.False);
            Assert.That(fixture.Presenter.HasCaughtVisual, Is.True);
            Assert.That(fixture.Presenter.CaughtHandAnchor,
                Is.SameAs(fixture.HandAnchor));
        }

        [Test]
        public void CaughtVisual_ExpiresOnceAfterConfiguredDisplayDuration()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Normal);
            fixture.Presenter.ConfigureCaughtPresentation(fixture.PlayerRoot, 1.75f);
            EnterFighting(fixture);
            fixture.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();
            GameObject caughtVisual = fixture.Presenter.CaughtVisual;

            fixture.Presenter.StepPresentation(1.74f);
            Assert.That(fixture.Presenter.HasCaughtVisual, Is.True);
            Assert.That(fixture.Presenter.CaughtVisual, Is.SameAs(caughtVisual));

            fixture.Presenter.StepPresentation(0.02f);
            Assert.That(fixture.Presenter.HasCaughtVisual, Is.False);

            fixture.Presenter.StepPresentation(1f);
            Assert.That(fixture.Presenter.CaughtVisualCount, Is.Zero);
        }

        [Test]
        public void NonCaughtTerminalAndAbort_NeverCreateCaughtVisual()
        {
            FishingV3Tuning lineBreakTuning = StableTuning();
            lineBreakTuning.InitialTensionNormalized = 0.95f;
            lineBreakTuning.CalmBaseTension = 0.95f;
            lineBreakTuning.FightBaseTension = 0.95f;
            lineBreakTuning.RunBaseTension = 0.95f;
            lineBreakTuning.CaptureScale = 0f;
            lineBreakTuning.BreakStressPerSecond = 2f;
            Fixture lineBroken = CreateFixture(
                FishingV3FishProfileId.Normal,
                modelTuning: lineBreakTuning);
            EnterFighting(lineBroken);
            lineBroken.Input.SetNextFrame(ConnectedFrame());
            lineBroken.Controller.TickRuntime(0.5f);
            lineBroken.Presenter.RefreshNow();

            Assert.That(lineBroken.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.LineBroken));
            Assert.That(lineBroken.Presenter.HasCaughtVisual, Is.False);

            FishingV3Tuning escapeTuning = StableTuning();
            escapeTuning.InitialTensionNormalized = 0.05f;
            escapeTuning.CalmBaseTension = 0.05f;
            escapeTuning.FightBaseTension = 0.05f;
            escapeTuning.RunBaseTension = 0.05f;
            escapeTuning.CaptureScale = 0f;
            escapeTuning.EscapeRiskPerSecond = 2f;
            Fixture escaped = CreateFixture(
                FishingV3FishProfileId.Normal,
                modelTuning: escapeTuning);
            EnterFighting(escaped);
            escaped.Input.SetNextFrame(ConnectedFrame());
            escaped.Controller.TickRuntime(0.5f);
            escaped.Presenter.RefreshNow();

            Assert.That(escaped.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.FishEscaped));
            Assert.That(escaped.Presenter.HasCaughtVisual, Is.False);

            Fixture aborted = CreateFixture(FishingV3FishProfileId.Normal);
            EnterFighting(aborted);
            aborted.Controller.AbortRound();
            aborted.Presenter.RefreshNow();
            Assert.That(aborted.Presenter.HasCaughtVisual, Is.False);
        }

        [Test]
        public void LineBroken_SnapsFromLastFightingPoseThenRemovesVisualOnce()
        {
            Fixture fixture = CreateFixture(
                FishingV3FishProfileId.Normal,
                modelTuning: LineBrokenTuning());
            EnterFighting(fixture);
            fixture.Presenter.StepPresentation(0.2f);
            Vector3 lastFightingPosition =
                fixture.Presenter.ActiveVisual.transform.position;

            fixture.Input.SetNextFrame(ConnectedFrame());
            fixture.Controller.TickRuntime(0.5f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.LineBroken));
            Assert.That(fixture.Presenter.IsLineBrokenPresentationActive, Is.True);
            Assert.That(fixture.Presenter.IsFishEscapedPresentationActive, Is.False);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.EqualTo(1));
            Assert.That(fixture.Presenter.FailureStartWorldPosition,
                Is.EqualTo(lastFightingPosition));
            Assert.That(fixture.Presenter.ActiveVisual.transform.position,
                Is.EqualTo(lastFightingPosition));

            fixture.Presenter.StepPresentation(
                FishingV3FishVisualPresenter.DefaultLineBrokenMotionSeconds * 0.5f +
                0.01f);

            Assert.That(Vector3.Distance(
                    fixture.Presenter.ActiveVisual.transform.position,
                    lastFightingPosition),
                Is.GreaterThan(0.5f));
            Assert.That(fixture.Presenter.ActiveVisual.transform.position.y,
                Is.GreaterThan(lastFightingPosition.y));

            fixture.Presenter.StepPresentation(
                FishingV3FishVisualPresenter.DefaultLineBrokenMotionSeconds * 0.5f);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);

            fixture.Presenter.StepPresentation(1f);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);
            Assert.That(fixture.Presenter.IsFailurePresentationActive, Is.False);
        }

        [Test]
        public void FishEscaped_SwimsDownAndAwayThenRemovesVisualOnce()
        {
            Fixture fixture = CreateFixture(
                FishingV3FishProfileId.Normal,
                modelTuning: FishEscapedTuning());
            EnterFighting(fixture);
            fixture.Presenter.StepPresentation(0.2f);
            Vector3 lastFightingPosition =
                fixture.Presenter.ActiveVisual.transform.position;

            fixture.Input.SetNextFrame(ConnectedFrame());
            fixture.Controller.TickRuntime(0.5f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.FishEscaped));
            Assert.That(fixture.Presenter.IsFishEscapedPresentationActive, Is.True);
            Assert.That(fixture.Presenter.IsLineBrokenPresentationActive, Is.False);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.EqualTo(1));
            Assert.That(fixture.Presenter.FailureStartWorldPosition,
                Is.EqualTo(lastFightingPosition));
            Assert.That(fixture.Presenter.FailureTargetWorldPosition.y,
                Is.LessThan(lastFightingPosition.y));

            fixture.Presenter.StepPresentation(
                FishingV3FishVisualPresenter.DefaultFishEscapedMotionSeconds * 0.5f +
                0.01f);

            Assert.That(Vector3.Distance(
                    fixture.Presenter.ActiveVisual.transform.position,
                    lastFightingPosition),
                Is.GreaterThan(0.5f));
            Assert.That(fixture.Presenter.ActiveVisual.transform.position.y,
                Is.LessThan(lastFightingPosition.y));

            fixture.Presenter.StepPresentation(
                FishingV3FishVisualPresenter.DefaultFishEscapedMotionSeconds * 0.5f);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);

            fixture.Presenter.StepPresentation(1f);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);
            Assert.That(fixture.Presenter.IsFailurePresentationActive, Is.False);
        }

        [Test]
        public void CaughtAndAbort_NeverStartFailurePresentation()
        {
            Fixture caught = CreateFixture(FishingV3FishProfileId.Small);
            EnterFighting(caught);
            caught.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            caught.Controller.TickRuntime(1f);
            caught.Presenter.RefreshNow();

            Assert.That(caught.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.Caught));
            Assert.That(caught.Presenter.IsFailurePresentationActive, Is.False);
            Assert.That(caught.Presenter.HasCaughtVisual, Is.True);

            Fixture aborted = CreateFixture(FishingV3FishProfileId.Strong);
            EnterFighting(aborted);
            aborted.Controller.AbortRound();
            aborted.Presenter.RefreshNow();

            Assert.That(aborted.Presenter.IsFailurePresentationActive, Is.False);
            Assert.That(aborted.Presenter.ActiveVisualCount, Is.Zero);
        }

        [Test]
        public void NewSession_CleansRunningFailurePresentationImmediately()
        {
            Fixture fixture = CreateFixture(
                FishingV3FishProfileId.Normal,
                modelTuning: FishEscapedTuning());
            EnterFighting(fixture);
            fixture.Input.SetNextFrame(ConnectedFrame());
            fixture.Controller.TickRuntime(0.5f);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.IsFailurePresentationActive, Is.True);

            fixture.Controller.BeginRound();
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.Active));
            Assert.That(fixture.Presenter.IsFailurePresentationActive, Is.False);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);
        }

        [Test]
        public void NewSession_CleansCaughtVisualImmediatelyAndAllowsNextCatch()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Small);
            EnterFighting(fixture);
            fixture.Input.SetNextFrame(ConnectedFrame(reelDelta: 1f));
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.CaughtVisualCount, Is.EqualTo(1));

            fixture.Controller.BeginRound();
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.Active));
            Assert.That(fixture.Presenter.CaughtVisualCount, Is.Zero);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);
        }

        [Test]
        public void NewSession_ClearsOldVisualAndNeverDuplicatesInstance()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Small);
            EnterFighting(fixture);
            fixture.Presenter.StepPresentation(0.5f);
            GameObject first = fixture.Presenter.ActiveVisual;
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.EqualTo(1));

            fixture.Controller.AbortRound();
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);
            Assert.That(fixture.Presenter.MotionElapsedSeconds, Is.Zero);

            fixture.Controller.BeginRound();
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Controller.V3Snapshot.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.WaitingForBite));
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);

            EnterFighting(fixture);
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.EqualTo(1));
            Assert.That(fixture.Presenter.ActiveVisual, Is.Not.SameAs(first));
        }

        [Test]
        public void PresentationAnchor_OverridesLegacySpotLocalFallback()
        {
            GameObject anchorObject = new GameObject("FishVisualAnchor");
            _objects.Add(anchorObject);
            anchorObject.transform.position = new Vector3(-77.3f, -0.25f, 125.8f);
            anchorObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Fixture fixture = CreateFixture(
                FishingV3FishProfileId.Normal,
                anchorObject.transform);
            fixture.Presenter.ConfigurePlacement(
                new Vector3(50f, 50f, 50f),
                new Vector3(45f, 45f, 45f),
                Vector3.one);

            EnterFighting(fixture);

            Assert.That(fixture.Presenter.ActiveVisual.transform.parent,
                Is.SameAs(anchorObject.transform));
            Assert.That(fixture.Presenter.ActiveVisual.transform.localPosition,
                Is.EqualTo(Vector3.zero));
            Assert.That(
                Quaternion.Angle(
                    fixture.Presenter.ActiveVisual.transform.localRotation,
                    Quaternion.identity),
                Is.LessThan(0.001f));
        }

        [Test]
        public void MissingPresentationAnchor_UsesLegacySpotLocalFallback()
        {
            Fixture fixture = CreateFixture(FishingV3FishProfileId.Normal);
            Vector3 fallback = new Vector3(1f, -0.75f, 2.5f);
            fixture.Presenter.ConfigurePlacement(fallback, Vector3.zero, Vector3.one);

            EnterFighting(fixture);

            Assert.That(fixture.Presenter.HasPresentationAnchor, Is.False);
            Assert.That(fixture.Presenter.ActiveVisual.transform.localPosition,
                Is.EqualTo(fallback));
        }

        [Test]
        public void PlayerIntegrationScene_WiresFishPresenterToCatalogAndSpot()
        {
            const string scenePath =
                "Assets/Game/Scenes/Develop/Yongju/FishingScenes/FishingV3PlayerIntegration.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                FishingV3FishVisualPresenter presenter = FindInScene<FishingV3FishVisualPresenter>(scene);
                Assert.That(presenter, Is.Not.Null);
                Assert.That(presenter.HasConfiguredCatalog, Is.True);
                Assert.That(presenter.HasPresentationAnchor, Is.True);
                Assert.That(presenter.PresentationAnchor.name, Is.EqualTo("FishVisualAnchor"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private Fixture CreateFixture(
            FishingV3FishProfileId profileId,
            Transform presentationAnchor = null,
            FishingV3FishBehaviorTuning behaviorTuning = null,
            FishingV3Tuning modelTuning = null)
        {
            GameObject host = new GameObject("FishingV3FishVisualPresenterTests");
            GameObject spotObject = new GameObject("FishingSpot");
            GameObject playerObject = new GameObject("LocalPlayer");
            GameObject handObject = new GameObject("RightHandProp");
            handObject.transform.SetParent(playerObject.transform, false);
            _objects.Add(host);
            _objects.Add(spotObject);
            _objects.Add(playerObject);

            FishingGameController controller = host.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
            SerializedObject facadeObject = new SerializedObject(facade);
            facadeObject.FindProperty("controller").objectReferenceValue = controller;
            facadeObject.ApplyModifiedPropertiesWithoutUndo();

            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            controller.ConfigureV3Runtime(
                modelTuning ?? StableTuning(),
                new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                },
                behaviorTuning: behaviorTuning,
                reelControlMode: FishingV3ReelControlMode.LegacyHold,
                biteHookTuning: new FishingV3BiteHookTuning
                {
                    BiteDelayMinSeconds = 0.05f,
                    BiteDelayMaxSeconds = 0.05f,
                    HookWindowMinSeconds = 0.5f,
                    HookWindowMaxSeconds = 0.5f
                },
                sessionFlowMode: FishingV3SessionFlowMode.BiteHook,
                fishProfile: FishingV3FishProfiles.Create(profileId));
            controller.SetV3FishState(FishingV3FishState.Fight);
            controller.BeginRound();

            FishingSpot spot = spotObject.AddComponent<FishingSpot>();
            FishingV3FishVisualPresenter presenter =
                host.AddComponent<FishingV3FishVisualPresenter>();
            presenter.Configure(
                facade,
                spot,
                LoadCatalog(),
                anchor: presentationAnchor);
            presenter.ConfigureCaughtPresentation(playerObject.transform);
            return new Fixture(
                controller,
                input,
                presenter,
                playerObject.transform,
                handObject.transform);
        }

        private static void EnterFighting(Fixture fixture)
        {
            fixture.Input.SetNextFrame(ConnectedFrame());
            fixture.Controller.TickRuntime(0.05f);
            Assert.That(fixture.Controller.V3Snapshot.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.HookWindow));
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.ActiveVisualCount, Is.Zero);

            fixture.Input.SetNextFrame(ConnectedFrame(hookPressed: true));
            fixture.Controller.TickRuntime(0.01f);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Controller.V3Snapshot.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.Fighting));
        }

        private static FishingInputFrame ConnectedFrame(
            bool hookPressed = false,
            float reelDelta = 0f)
        {
            return new FishingInputFrame
            {
                HookPressed = hookPressed,
                ReelDelta = reelDelta,
                TensionNormalized = 0.5f,
                IsDeviceConnected = true
            };
        }

        private static FishingV3Tuning StableTuning()
        {
            return new FishingV3Tuning
            {
                InitialTensionNormalized = 0.5f,
                CalmBaseTension = 0.5f,
                FightBaseTension = 0.5f,
                RunBaseTension = 0.5f,
                ReelTensionGain = 0f,
                CaptureScale = 2f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
        }

        private static FishingV3Tuning LineBrokenTuning()
        {
            FishingV3Tuning tuning = StableTuning();
            tuning.InitialTensionNormalized = 0.95f;
            tuning.CalmBaseTension = 0.95f;
            tuning.FightBaseTension = 0.95f;
            tuning.RunBaseTension = 0.95f;
            tuning.CaptureScale = 0f;
            tuning.BreakStressPerSecond = 2f;
            return tuning;
        }

        private static FishingV3Tuning FishEscapedTuning()
        {
            FishingV3Tuning tuning = StableTuning();
            tuning.InitialTensionNormalized = 0.05f;
            tuning.CalmBaseTension = 0.05f;
            tuning.FightBaseTension = 0.05f;
            tuning.RunBaseTension = 0.05f;
            tuning.CaptureScale = 0f;
            tuning.EscapeRiskPerSecond = 2f;
            return tuning;
        }

        private static FishingVisualSet LoadCatalog()
        {
            FishingVisualSet catalog = AssetDatabase.LoadAssetAtPath<FishingVisualSet>(
                "Assets/Game/Resources/Fishing/FishingVisualSet.asset");
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private sealed class Fixture
        {
            public FishingGameController Controller { get; }
            public MockFishingInputSource Input { get; }
            public FishingV3FishVisualPresenter Presenter { get; }
            public Transform PlayerRoot { get; }
            public Transform HandAnchor { get; }

            public Fixture(
                FishingGameController controller,
                MockFishingInputSource input,
                FishingV3FishVisualPresenter presenter,
                Transform playerRoot,
                Transform handAnchor)
            {
                Controller = controller;
                Input = input;
                Presenter = presenter;
                PlayerRoot = playerRoot;
                HandAnchor = handAnchor;
            }
        }
    }
}
