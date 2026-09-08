using System;
using System.Collections;
using System.Collections.Generic;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingCheckpointBPlayModeTests
    {
        [UnityTest]
        public IEnumerator StandaloneScene_AdaptiveMockInputCatchesThreeRandomFish()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            Assert.That(controller, Is.Not.Null, "FishingStandalone must contain a FishingGameController.");
            controller.ConfigureFlowMode(FishingGameMode.LegacyRound);
            Assert.That(controller.RoundSnapshot.State, Is.EqualTo(FishingRoundState.Ready),
                "The standalone scene must open on its start screen instead of beginning immediately.");
            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            Assert.That(canvas, Is.Not.Null, "The standalone scene must build its responsive Canvas HUD.");
            Transform startButtonTransform = canvas.transform.Find("StartScreen/StartCard/StartButton");
            Assert.That(startButtonTransform, Is.Not.Null);
            Assert.That(startButtonTransform.gameObject.activeInHierarchy, Is.True);

            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            startButtonTransform.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            yield return WaitUntilOrFail(
                () => controller.RoundSnapshot.State == FishingRoundState.Playing,
                5f,
                "The standalone round did not leave its countdown.");

            HashSet<string> allowedFish = new HashSet<string>
            {
                "blue_mackerel",
                "red_sea_bream",
                "greater_amberjack"
            };
            for (int i = 0; i < 3; i++)
            {
                yield return WaitUntilOrFail(
                    () => controller.Snapshot.State == FishingPlayerState.Idle,
                    5f,
                    "The next random fish was not prepared.");
                string selectedFish = controller.Snapshot.FishId;
                Assert.That(allowedFish, Does.Contain(selectedFish));
                yield return CatchCurrentFish(controller, input, selectedFish, 22f);
            }

            Assert.That(controller.RoundSnapshot.Attempts, Is.EqualTo(3));
            Assert.That(controller.RoundSnapshot.CaughtCount, Is.EqualTo(3));
            Assert.That(controller.RoundSnapshot.TotalScore, Is.GreaterThan(0));
            Assert.That(controller.CatchHistory.Count, Is.EqualTo(3));
            for (int i = 0; i < controller.CatchHistory.Count; i++)
            {
                Assert.That(allowedFish, Does.Contain(controller.CatchHistory[i].FishId));
            }
        }

        [UnityTest]
        public IEnumerator IntegrationScene_ShortRoundCompletesOnceAtTimeLimit()
        {
            yield return SceneManager.LoadSceneAsync("FishingMiniGame", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = UnityEngine.Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);
            facade.ConfigureFlowMode(FishingGameMode.LegacyRound);

            int completionCount = 0;
            FishingRoundResult completedResult = null;
            facade.Completed += result =>
            {
                completionCount++;
                completedResult = result;
            };
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "short-integration-round",
                RoundDurationSeconds = 0.35f,
                CountdownSeconds = 0f,
                Seed = 11
            });
            facade.BeginRound();

            yield return WaitUntilOrFail(
                () => controller.RoundSnapshot.State == FishingRoundState.Completed,
                2f,
                "The short integration round did not complete at its time limit.");

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(completedResult, Is.Not.Null);
            Assert.That(completedResult.EndReason, Is.EqualTo(FishingRoundEndReason.TimeExpired));
            Assert.That(completedResult.RoundId, Is.EqualTo("short-integration-round"));
        }

        [UnityTest]
        public IEnumerator IntegrationScene_LoadsAndUnloadsAdditively()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync("FishingMiniGame", LoadSceneMode.Additive);

            Scene integrationScene = SceneManager.GetSceneByName("FishingMiniGame");
            Assert.That(integrationScene.IsValid(), Is.True);
            Assert.That(integrationScene.isLoaded, Is.True);
            FishingMiniGameFacade facade = null;
            GameObject[] roots = integrationScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && facade == null; i++)
            {
                facade = roots[i].GetComponentInChildren<FishingMiniGameFacade>();
            }
            Assert.That(facade, Is.Not.Null, "The additive integration scene must expose a facade root.");

            yield return SceneManager.UnloadSceneAsync(integrationScene);
            Assert.That(SceneManager.GetSceneByName("FishingMiniGame").isLoaded, Is.False);
        }

        private static IEnumerator CatchCurrentFish(
            FishingGameController controller,
            MockFishingInputSource input,
            string expectedFishId,
            float fightTimeout)
        {
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Idle && controller.Snapshot.FishId == expectedFishId,
                5f,
                $"Expected next fish '{expectedFishId}' was not prepared.");

            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Casting));

            input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));

            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.BiteWindow,
                6f,
                $"Fish '{expectedFishId}' did not bite.");

            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Hooked));

            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                $"Fish '{expectedFishId}' did not enter Fighting.");

            float deadline = Time.realtimeSinceStartup + fightTimeout;
            float rawTension = 0.5f;
            while (controller.Snapshot.State == FishingPlayerState.Fighting && Time.realtimeSinceStartup < deadline)
            {
                rawTension = Mathf.Clamp01(rawTension + (0.52f - controller.Snapshot.TensionNormalized) * 0.85f);
                bool running = controller.Snapshot.Feedback.State == FishingFeedbackState.Run;
                float counterYaw = running ? -controller.Snapshot.FightDirection : 0f;
                float reel = running ? 0.35f : 1f;
                input.SetNextFrame(Frame(tension: rawTension, reel: reel, rodYaw: counterYaw));
                yield return null;
            }
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught),
                $"Fish '{expectedFishId}' was not caught with adaptive mock input.");

            Assert.That(controller.LastResult.FishId, Is.EqualTo(expectedFishId));
            Assert.That(controller.LastResult.WasCaught, Is.True);
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Idle,
                5f,
                $"Fish '{expectedFishId}' did not finish its cooldown.");
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float tension = 0.5f,
            float reel = 0f,
            float rodYaw = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                TensionNormalized = tension,
                ReelDelta = reel,
                RodYaw = rodYaw,
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
