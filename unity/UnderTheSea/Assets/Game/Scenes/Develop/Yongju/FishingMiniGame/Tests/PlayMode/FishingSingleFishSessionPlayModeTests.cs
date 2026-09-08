using System;
using System.Collections;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingSingleFishSessionPlayModeTests
    {
        [UnityTest]
        public IEnumerator PreFightFlow_IsObservableAndRetriesInFishingStandalone()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture);
            FishingGameController controller = fixture.Controller;
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            fixture.Facade.BeginRound();
            yield return WaitUntilOrFail(
                () => controller.SessionSnapshot.State == FishingSessionState.Playing &&
                      controller.Snapshot.State == FishingPlayerState.Idle,
                2f,
                "The single-fish session did not become playable.");
            string fishId = controller.Snapshot.FishId;

            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));

            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.EarlyHookCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));
            Assert.That(controller.LastResult, Is.Null);

            yield return WaitUntilOrFail(
                () => controller.Snapshot.IsNibbling,
                6f,
                "The retry nibble was not observable.");
            int firstObservedNibble = controller.Snapshot.NibbleEventSequence;
            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.EarlyHookCount, Is.EqualTo(2));
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));

            yield return WaitUntilOrFail(
                () => controller.Snapshot.NibbleEventSequence > firstObservedNibble,
                6f,
                "A fresh attempt did not produce another nibble.");
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.BiteWindow,
                3f,
                "The observed nibble did not progress to a committed bite.");
            Assert.That(controller.Snapshot.BiteEventSequence, Is.EqualTo(1));

            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Hooked));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                "The successful hook did not hand off to Fighting.");
            Assert.That(controller.Snapshot.V2BehaviorState, Is.EqualTo(FishingV2BehaviorState.Fight));
        }

        [UnityTest]
        public IEnumerator Catch_CompletesSessionAndDoesNotSelectNextFish()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            string fishId = controller.Snapshot.FishId;
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            int completionCount = 0;
            facade.SessionCompleted += _ => completionCount++;
            facade.BeginRound();

            yield return CatchCurrentFish(controller, input, 25f);
            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(controller.LastSessionResult.Outcome, Is.EqualTo(FishingSessionOutcome.Caught));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught));

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PostHookEscape_CompletesSessionAndDoesNotSelectNextFish()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            string fishId = controller.Snapshot.FishId;
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            facade.BeginRound();

            yield return EnterFighting(controller, input);
            input.IsConnected = false;
            yield return null;

            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(controller.LastSessionResult.Outcome, Is.EqualTo(FishingSessionOutcome.Escaped));
            Assert.That(controller.LastSessionResult.CycleResult.EscapeReason, Is.EqualTo(FishingEscapeReason.InputDisconnected));

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));
        }

        [UnityTest]
        public IEnumerator MissedBite_IsInPlaceRetryWithSameFishAndSessionStillPlaying()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            string fishId = controller.Snapshot.FishId;
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            facade.BeginRound();

            yield return CastAndWaitForBite(controller, input);
            int firstNibbleSequence = controller.Snapshot.NibbleEventSequence;
            yield return WaitUntilOrFail(
                () => controller.Snapshot.MissedBiteRetryCount == 1,
                3f,
                "A missed bite did not start an in-place Waiting retry.");

            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Playing));
            Assert.That(controller.LastSessionResult, Is.Null);
            Assert.That(controller.SessionSnapshot.PreHookFailureCount, Is.Zero);
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));

            yield return WaitUntilOrFail(
                () => controller.Snapshot.NibbleEventSequence > firstNibbleSequence,
                3f,
                "The missed-bite retry did not schedule a new nibble.");
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));
        }

        [UnityTest]
        public IEnumerator LegacyRoundDuration_DoesNotEndSingleFishSession()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture, 0.1f);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            facade.BeginRound();

            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Playing));
            Assert.That(controller.LastSessionResult, Is.Null);
            Assert.That(controller.RoundSnapshot.State, Is.EqualTo(FishingRoundState.Ready));
        }

        private static IEnumerator LoadSingleSession(
            SessionFixture fixture,
            float legacyRoundDuration = 180f)
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = UnityEngine.Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);
            fixture.Controller = controller;
            fixture.Facade = facade;
            FishProfile fish = controller.Config.BuildSessionFishProfile();
            facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, fish);
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "single-fish-playmode",
                LocalParticipantId = "tester",
                RoundDurationSeconds = legacyRoundDuration,
                CountdownSeconds = 0f,
                Seed = 31
            });
            Assert.That(controller.Mode, Is.EqualTo(FishingGameMode.SingleFishSession));
            Assert.That(controller.SessionSnapshot.FishId, Is.EqualTo(fish.FishId));
        }

        private sealed class SessionFixture
        {
            public FishingGameController Controller;
            public FishingMiniGameFacade Facade;
        }

        private static IEnumerator CatchCurrentFish(
            FishingGameController controller,
            MockFishingInputSource input,
            float fightTimeout)
        {
            yield return EnterFighting(controller, input);

            float deadline = Time.realtimeSinceStartup + fightTimeout;
            while (controller.Snapshot.State == FishingPlayerState.Fighting && Time.realtimeSinceStartup < deadline)
            {
                bool running = controller.Snapshot.V2BehaviorState == FishingV2BehaviorState.Run;
                float followYaw = running ? controller.Snapshot.V2FishDirectionNormalized : 0f;
                float reel = running ? 0f : 1f;
                float rodPitch = running ? 0f : 0.35f;
                input.SetNextFrame(Frame(reel: reel, rodYaw: followYaw, rodPitch: rodPitch));
                yield return null;
            }

            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught),
                "The configured single fish was not caught with adaptive mock input.");
        }

        private static IEnumerator EnterFighting(
            FishingGameController controller,
            MockFishingInputSource input)
        {
            yield return CastAndWaitForBite(controller, input);
            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Hooked));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                "The hooked fish did not enter Fighting.");
        }

        private static IEnumerator CastAndWaitForBite(
            FishingGameController controller,
            MockFishingInputSource input)
        {
            yield return WaitUntilOrFail(
                () => controller.SessionSnapshot.State == FishingSessionState.Playing &&
                      controller.Snapshot.State == FishingPlayerState.Idle,
                2f,
                "The single-fish session did not become playable.");
            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.BiteWindow,
                6f,
                "The configured fish did not bite.");
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float tension = 0.5f,
            float reel = 0f,
            float rodYaw = 0f,
            float rodPitch = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                TensionNormalized = tension,
                ReelDelta = reel,
                RodYaw = rodYaw,
                RodPitch = rodPitch,
                IsDeviceConnected = true
            };
        }

        private static IEnumerator WaitUntilOrFail(Func<bool> condition, float timeoutSeconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
