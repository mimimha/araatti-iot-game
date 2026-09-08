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
    public sealed class FishingResistanceFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator SingleFishSession_UsesResistanceOnlyDuringSafeFighting()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = UnityEngine.Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);

            facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, controller.Config.BuildSessionFishProfile());
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "t7-single-session",
                LocalParticipantId = "tester",
                CountdownSeconds = 0f,
                Seed = 71
            });
            MockFishingInputSource input = new MockFishingInputSource();
            MockFishingResistanceOutput output = new MockFishingResistanceOutput();
            controller.SetInputSource(input);
            controller.SetResistanceOutput(output);
            facade.BeginRound();

            yield return WaitUntilOrFail(
                () => controller.SessionSnapshot.State == FishingSessionState.Playing,
                2f,
                "Single-fish session did not enter Playing.");
            AssertStopped(controller, output);

            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));
            AssertStopped(controller, output);

            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.BiteWindow,
                6f,
                "Single-fish session did not reach BiteWindow.");
            AssertStopped(controller, output);

            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Fighting &&
                      !controller.IsResistanceStopped &&
                      controller.LastResistanceCommand.ResistanceNormalized > 0f,
                2f,
                "Fighting did not produce an active resistance command.");
            Assert.That(output.IsStopped, Is.False);
            Assert.That(output.LastCommand.ResistanceNormalized, Is.GreaterThan(0f));

            controller.SetPaused(true);
            AssertStopped(controller, output);

            controller.SetPaused(false);
            input.SetNextFrame(Frame(rodPitch: 0.2f));
            yield return WaitUntilOrFail(
                () => !controller.IsResistanceStopped &&
                      controller.LastResistanceCommand.ResistanceNormalized > 0f,
                2f,
                "Resistance did not resume through ApplyCommand after pause.");
            Assert.That(output.IsStopped, Is.False);

            controller.enabled = false;
            AssertStopped(controller, output);
            controller.enabled = true;
            input.SetNextFrame(Frame(rodPitch: 0.2f));
            yield return WaitUntilOrFail(
                () => !controller.IsResistanceStopped,
                2f,
                "Resistance did not resume after component re-enable.");

            input.IsConnected = false;
            yield return null;
            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Completed));
            AssertStopped(controller, output);
        }

        [UnityTest]
        public IEnumerator LegacyRound_RemainsStoppedEvenWhenFishingStateIsFighting()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = UnityEngine.Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);

            facade.ConfigureFlowMode(FishingGameMode.LegacyRound);
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "t7-legacy",
                LocalParticipantId = "tester",
                RoundDurationSeconds = 30f,
                CountdownSeconds = 0f,
                Seed = 72
            });
            MockFishingInputSource input = new MockFishingInputSource();
            MockFishingResistanceOutput output = new MockFishingResistanceOutput();
            controller.SetInputSource(input);
            controller.SetResistanceOutput(output);
            facade.BeginRound();

            yield return WaitUntilOrFail(
                () => controller.RoundSnapshot.State == FishingRoundState.Playing,
                2f,
                "Legacy round did not enter Playing.");
            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.BiteWindow,
                6f,
                "Legacy round did not reach BiteWindow.");
            input.SetNextFrame(Frame(hookPressed: true));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                "Legacy round did not reach Fighting.");

            AssertStopped(controller, output);
            controller.AbortRound();
            AssertStopped(controller, output);
        }

        private static void AssertStopped(
            FishingGameController controller,
            MockFishingResistanceOutput output)
        {
            Assert.That(controller.LastResistanceCommand.ResistanceNormalized, Is.Zero);
            Assert.That(controller.IsResistanceStopped, Is.True);
            Assert.That(output.LastCommand.ResistanceNormalized, Is.Zero);
            Assert.That(output.IsStopped, Is.True);
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float rodPitch = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                TensionNormalized = 0.5f,
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
