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
    public sealed class FishingV2FightMetricsPlayModeTests
    {
        [UnityTest]
        public IEnumerator Fighting_ExposesV2MetricsIgnoresRawTensionAndHasNoFightTimeout()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSession(fixture, CreateFish(FishingV2BehaviorState.Fight));
            yield return EnterFighting(fixture);

            fixture.Input.SetNextFrame(Frame(tension: 0f, rodPitch: 0.35f));
            yield return null;
            fixture.Input.SetNextFrame(Frame(tension: 1f, rodPitch: 0.35f));
            yield return null;

            FishingSnapshot snapshot = fixture.Controller.Snapshot;
            Assert.That(snapshot.RawTensionNormalized, Is.EqualTo(1f));
            Assert.That(snapshot.VirtualLineTensionNormalized, Is.InRange(0f, 1f));
            Assert.That(snapshot.VirtualLineTensionNormalized, Is.Not.EqualTo(1f).Within(0.01f));
            Assert.That(snapshot.FishDistanceMeters, Is.GreaterThan(0f));
            Assert.That(snapshot.FishStaminaNormalized, Is.InRange(0f, 1f));
            Assert.That(snapshot.BreakStressNormalized, Is.InRange(0f, 1f));
            Assert.That(snapshot.HookLooseRiskNormalized, Is.InRange(0f, 1f));
            Assert.That(snapshot.RodResponseQualityNormalized, Is.InRange(0f, 1f));
            Assert.That(snapshot.V2BehaviorState, Is.EqualTo(FishingV2BehaviorState.Fight));
            Assert.That(snapshot.FightElapsedSeconds, Is.GreaterThan(0f));
            Assert.That(snapshot.FightRemainingSeconds, Is.Zero);

            float deadline = Time.realtimeSinceStartup + 1.25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                fixture.Input.SetNextFrame(Frame(rodPitch: 0.35f));
                yield return null;
            }

            Assert.That(fixture.Controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Fighting),
                "Single-fish V2 must not use the legacy one-second fight timeout.");
        }

        [UnityTest]
        public IEnumerator V2CatchCondition_CompletesSingleFishSession()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSession(fixture, CreateFish(FishingV2BehaviorState.Fight));
            yield return EnterFighting(fixture);

            float deadline = Time.realtimeSinceStartup + 12f;
            while (fixture.Controller.Snapshot.State == FishingPlayerState.Fighting &&
                   Time.realtimeSinceStartup < deadline)
            {
                FishingSnapshot snapshot = fixture.Controller.Snapshot;
                bool preparingForRun = snapshot.IsRunTelegraphing;
                bool running = snapshot.V2BehaviorState == FishingV2BehaviorState.Run;
                float direction = preparingForRun
                    ? snapshot.RunTelegraphDirectionNormalized
                    : snapshot.V2FishDirectionNormalized;
                fixture.Input.SetNextFrame(Frame(
                    reel: preparingForRun || running ? 0f : 1f,
                    rodPitch: 0.35f,
                    rodYaw: preparingForRun || running ? direction : 0f));
                yield return null;
            }

            AssertTerminal(fixture, FishingSessionOutcome.Caught, FishingEscapeReason.None);
            Assert.That(fixture.Controller.Snapshot.FishDistanceMeters, Is.LessThanOrEqualTo(2f));
            Assert.That(fixture.Controller.Snapshot.FishStaminaNormalized, Is.LessThanOrEqualTo(0.15f));
        }

        [UnityTest]
        public IEnumerator SustainedDanger_MapsToTerminalLineBrokenEscape()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSession(fixture, CreateFish(FishingV2BehaviorState.Run));
            yield return EnterFighting(fixture);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (fixture.Controller.Snapshot.State == FishingPlayerState.Fighting &&
                   Time.realtimeSinceStartup < deadline)
            {
                float opposeYaw = -fixture.Controller.Snapshot.V2FishDirectionNormalized;
                fixture.Input.SetNextFrame(Frame(reel: 1f, rodYaw: opposeYaw));
                yield return null;
            }

            AssertTerminal(fixture, FishingSessionOutcome.Escaped, FishingEscapeReason.LineBroken);
        }

        [UnityTest]
        public IEnumerator SustainedSlack_MapsToTerminalSlackLineEscape()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSession(fixture, CreateFish(FishingV2BehaviorState.Rest));
            yield return EnterFighting(fixture);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (fixture.Controller.Snapshot.State == FishingPlayerState.Fighting &&
                   Time.realtimeSinceStartup < deadline)
            {
                fixture.Input.SetNextFrame(Frame(rodPitch: -1f));
                yield return null;
            }

            AssertTerminal(fixture, FishingSessionOutcome.Escaped, FishingEscapeReason.SlackLine);
        }

        private static IEnumerator LoadSession(SessionFixture fixture, FishProfile fish)
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            fixture.Controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            fixture.Facade = UnityEngine.Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(fixture.Controller, Is.Not.Null);
            Assert.That(fixture.Facade, Is.Not.Null);

            fixture.Input = new MockFishingInputSource();
            fixture.Controller.SetInputSource(fixture.Input);
            fixture.Facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, fish);
            fixture.Facade.Initialize(new FishingLaunchContext
            {
                RoundId = "v2-fight-metrics-playmode",
                LocalParticipantId = "tester",
                RoundDurationSeconds = 0.1f,
                CountdownSeconds = 0f,
                Seed = 83
            });
            fixture.Facade.BeginRound();

            yield return WaitUntilOrFail(
                () => fixture.Controller.SessionSnapshot.State == FishingSessionState.Playing &&
                      fixture.Controller.Snapshot.State == FishingPlayerState.Idle,
                2f,
                "The V2 single-fish session did not become playable.");
        }

        private static IEnumerator EnterFighting(SessionFixture fixture)
        {
            fixture.Input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            fixture.Input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            yield return WaitUntilOrFail(
                () => fixture.Controller.Snapshot.State == FishingPlayerState.BiteWindow,
                2f,
                "The deterministic V2 test fish did not bite.");
            fixture.Input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            yield return WaitUntilOrFail(
                () => fixture.Controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                "The deterministic V2 test fish did not enter Fighting.");
        }

        private static FishProfile CreateFish(FishingV2BehaviorState behavior)
        {
            float runChance = behavior == FishingV2BehaviorState.Run ? 1f : 0f;
            float restChance = behavior == FishingV2BehaviorState.Rest ? 1f : 0f;
            return new FishProfile
            {
                FishId = "v2_metrics_fish",
                DisplayName = "V2 Metrics Fish",
                UseSpeciesRuleOverrides = true,
                MinBiteDelaySeconds = 0.05f,
                MaxBiteDelaySeconds = 0.05f,
                HookWindowSeconds = 1f,
                FightTimeoutSeconds = 1f,
                MinBehaviorPhaseSeconds = 30f,
                MaxBehaviorPhaseSeconds = 30f,
                RunChance = runChance,
                RestChance = restChance,
                PullStrength = 0.7f,
                DirectionChangeChance = 0f
            };
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

        private static void AssertTerminal(
            SessionFixture fixture,
            FishingSessionOutcome expectedOutcome,
            FishingEscapeReason expectedEscape)
        {
            Assert.That(fixture.Controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(fixture.Controller.LastSessionResult, Is.Not.Null);
            Assert.That(fixture.Controller.LastSessionResult.Outcome, Is.EqualTo(expectedOutcome));
            Assert.That(fixture.Controller.LastSessionResult.CycleResult.EscapeReason, Is.EqualTo(expectedEscape));
        }

        private static IEnumerator WaitUntilOrFail(Func<bool> condition, float timeoutSeconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private sealed class SessionFixture
        {
            public FishingGameController Controller;
            public FishingMiniGameFacade Facade;
            public MockFishingInputSource Input;
        }
    }
}
