using System;
using System.Collections;
using System.Reflection;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingCaughtVisibilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator CaughtFish_IsVisibleBeforeSingleSessionResult()
        {
            Fixture fixture = new Fixture();
            yield return LoadSession(fixture);
            yield return EnterFighting(fixture);

            float deadline = Time.realtimeSinceStartup + 15f;
            while (fixture.Controller.Snapshot.State == FishingPlayerState.Fighting &&
                   Time.realtimeSinceStartup < deadline)
            {
                FishingSnapshot snapshot = fixture.Controller.Snapshot;
                bool evade = snapshot.IsRunTelegraphing ||
                    snapshot.V2BehaviorState == FishingV2BehaviorState.Run;
                float direction = snapshot.IsRunTelegraphing
                    ? snapshot.RunTelegraphDirectionNormalized
                    : snapshot.V2FishDirectionNormalized;
                fixture.Input.SetNextFrame(Frame(
                    reel: evade ? 0f : 1f,
                    rodPitch: 0.35f,
                    rodYaw: evade ? direction : 0f));
                yield return null;
            }

            Assert.That(fixture.Controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught));
            Assert.That(fixture.Controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(fixture.Controller.LastSessionResult.Outcome, Is.EqualTo(FishingSessionOutcome.Caught));

            Transform fish = GetPrivateField<Transform>(fixture.WorldView, "_fish");
            GameObject externalFish = GetPrivateField<GameObject>(fixture.WorldView, "_externalFishModel");
            GameObject resultPanel = GetPrivateField<GameObject>(fixture.Ui, "_resultPanel");
            GameObject gameplayRoot = GetPrivateField<GameObject>(fixture.Ui, "_gameplayRoot");

            Assert.That(fish.gameObject.activeInHierarchy, Is.True);
            Assert.That(externalFish, Is.Not.Null);
            Assert.That(externalFish.activeInHierarchy, Is.True);
            Assert.That(HasEnabledRenderer(fish), Is.True);
            Assert.That(IsInsideCameraFrustum(fish, Camera.main), Is.True);
            Assert.That(resultPanel.activeInHierarchy, Is.False,
                "The result overlay must not cover the caught-fish presentation.");
            Assert.That(gameplayRoot.activeInHierarchy, Is.True);
            Assert.That(fixture.Controller.IsResistanceStopped, Is.True);

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(resultPanel.activeInHierarchy, Is.False,
                "The caught fish should remain unobscured during the presentation hold.");

            yield return WaitUntilOrFail(
                () => resultPanel.activeInHierarchy,
                1.5f,
                "The result overlay did not appear after the caught-fish presentation.");
            Assert.That(gameplayRoot.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator EscapedSession_ShowsResultWithoutCaughtPresentationHold()
        {
            Fixture fixture = new Fixture();
            yield return LoadSession(fixture);
            yield return EnterFighting(fixture);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (fixture.Controller.Snapshot.State == FishingPlayerState.Fighting &&
                   Time.realtimeSinceStartup < deadline)
            {
                fixture.Input.SetNextFrame(Frame(rodPitch: -1f));
                yield return null;
            }

            Assert.That(fixture.Controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(fixture.Controller.LastSessionResult.Outcome, Is.EqualTo(FishingSessionOutcome.Escaped));

            GameObject resultPanel = GetPrivateField<GameObject>(fixture.Ui, "_resultPanel");
            yield return WaitUntilOrFail(
                () => resultPanel.activeInHierarchy,
                0.5f,
                "Escape results should not use the caught-fish presentation hold.");
        }

        private static IEnumerator LoadSession(Fixture fixture)
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            fixture.Controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            fixture.Facade = UnityEngine.Object.FindAnyObjectByType<FishingMiniGameFacade>();
            fixture.WorldView = UnityEngine.Object.FindAnyObjectByType<FishingWorldView>();
            fixture.Ui = UnityEngine.Object.FindAnyObjectByType<FishingDebugUI>();
            fixture.Input = new MockFishingInputSource();

            Assert.That(fixture.Controller, Is.Not.Null);
            Assert.That(fixture.Facade, Is.Not.Null);
            Assert.That(fixture.WorldView, Is.Not.Null);
            Assert.That(fixture.Ui, Is.Not.Null);

            fixture.Controller.SetInputSource(fixture.Input);
            fixture.Facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, CreateFish());
            fixture.Facade.Initialize(new FishingLaunchContext
            {
                RoundId = "caught-visibility-playmode",
                LocalParticipantId = "tester",
                CountdownSeconds = 0f,
                Seed = 521
            });
            fixture.Facade.BeginRound();

            yield return WaitUntilOrFail(
                () => fixture.Controller.SessionSnapshot.State == FishingSessionState.Playing &&
                      fixture.Controller.Snapshot.State == FishingPlayerState.Idle,
                2f,
                "The single-fish session did not become playable.");
        }

        private static IEnumerator EnterFighting(Fixture fixture)
        {
            fixture.Input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            fixture.Input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            yield return WaitUntilOrFail(
                () => fixture.Controller.Snapshot.State == FishingPlayerState.BiteWindow,
                2f,
                "The test fish did not enter the bite window.");
            fixture.Input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            yield return WaitUntilOrFail(
                () => fixture.Controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                "The test fish did not enter Fighting.");
        }

        private static FishProfile CreateFish()
        {
            return new FishProfile
            {
                FishId = "red_sea_bream",
                DisplayName = "Red Sea Bream",
                DifficultyLabel = "Normal",
                VisualKey = "red_sea_bream",
                PullStrength = 0.58f,
                UseSpeciesRuleOverrides = true,
                MinBiteDelaySeconds = 0.05f,
                MaxBiteDelaySeconds = 0.05f,
                HookWindowSeconds = 1f
            };
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float reel = 0f,
            float rodPitch = 0f,
            float rodYaw = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                ReelDelta = reel,
                RodPitch = rodPitch,
                RodYaw = rodYaw,
                TensionNormalized = 0.5f,
                IsDeviceConnected = true
            };
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Missing private field {name}.");
            return field.GetValue(target) as T;
        }

        private static bool HasEnabledRenderer(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled && renderers[i].gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        private static bool IsInsideCameraFrustum(Transform root, Camera camera)
        {
            if (camera == null) return false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled || !renderers[i].gameObject.activeInHierarchy) continue;
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!hasBounds) return false;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        private static IEnumerator WaitUntilOrFail(Func<bool> condition, float timeoutSeconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private sealed class Fixture
        {
            public FishingGameController Controller;
            public FishingMiniGameFacade Facade;
            public FishingWorldView WorldView;
            public FishingDebugUI Ui;
            public MockFishingInputSource Input;
        }
    }
}
