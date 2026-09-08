using System;
using System.Collections;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FishingMiniGame.Tests
{
    public sealed class FishingRodPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator RodFocusedView_UsesControllerInputAndKeepsLineAttached()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);

            facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, controller.Config.BuildSessionFishProfile());
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "rod-presentation-test",
                CountdownSeconds = 0f,
                Seed = 59
            });
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            facade.BeginRound();
            yield return null;

            Camera camera = Camera.main;
            Transform rodPose = FindTransform("RodPosePivot");
            Transform rodGrip = rodPose != null ? rodPose.parent : null;
            Transform rodTip = FindTransform("Rod Tip");
            Transform bobber = FindTransform("Ocean Bobber");
            Transform angler = FindTransform("Coastal Angler");
            LineRenderer line = FindLine("Fishing Line");
            FishingWorldView worldView = Object.FindAnyObjectByType<FishingWorldView>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(rodGrip, Is.Not.Null);
            Assert.That(rodPose, Is.Not.Null);
            Assert.That(rodTip, Is.Not.Null);
            Assert.That(bobber, Is.Not.Null);
            Assert.That(angler, Is.Not.Null);
            Assert.That(line, Is.Not.Null);
            Assert.That(worldView, Is.Not.Null);

            Quaternion cameraRotation = camera.transform.rotation;
            Vector3 neutralTipPosition = rodTip.position;
            input.SetNextFrame(new FishingInputFrame
            {
                RodPitch = 0.8f,
                RodYaw = -0.65f,
                MotionStrength = 0.8f,
                TensionNormalized = 0.5f,
                IsDeviceConnected = true
            });
            yield return new WaitForSecondsRealtime(0.35f);

            Assert.That(controller.LastInputFrame.RodPitch, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(controller.LastInputFrame.RodYaw, Is.EqualTo(-0.65f).Within(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.identity, rodPose.localRotation), Is.GreaterThan(8f));
            Assert.That(Vector3.Distance(neutralTipPosition, rodTip.position), Is.GreaterThan(0.35f));
            Assert.That(Quaternion.Angle(cameraRotation, camera.transform.rotation), Is.LessThan(0.01f));
            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(0.01f));
            Assert.That(line.positionCount, Is.EqualTo(3));
            Assert.That(Vector3.Distance(line.GetPosition(0), rodTip.position), Is.LessThan(0.002f));
            Assert.That(Vector3.Distance(line.GetPosition(2), bobber.position), Is.LessThan(0.002f));

            Vector3 gripViewport = camera.WorldToViewportPoint(rodGrip.position);
            Vector3 tipViewport = camera.WorldToViewportPoint(rodTip.position);
            AssertViewportVisible(gripViewport, "Rod grip");
            AssertViewportVisible(tipViewport, "Rod tip");

            Renderer[] bodyRenderers = angler.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Assert.That(bodyRenderers[i].enabled, Is.False,
                    $"Full-body renderer '{bodyRenderers[i].name}' should be hidden in SingleFishSession view.");
            }
        }

        [UnityTest]
        public IEnumerator V2LineGeometry_ShowsLowAndHighTensionWithoutMovingEndpoints()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            LineRenderer line = FindLine("Fishing Line");
            Transform rodTip = FindTransform("Rod Tip");
            Transform bobber = FindTransform("Ocean Bobber");
            Assert.That(line, Is.Not.Null);
            Assert.That(rodTip, Is.Not.Null);
            Assert.That(bobber, Is.Not.Null);

            FishingV2PresentationInput lowInput = FightInput(0.10f);
            FishingV2PresentationInput highInput = FightInput(0.92f);
            float lowSag = new FishingV2PresentationFeedback().Tick(lowInput, 0f, false).LineSagMeters;
            float highSag = new FishingV2PresentationFeedback().Tick(highInput, 0f, false).LineSagMeters;

            FishingWorldView.ApplyLineGeometry(line, rodTip.position, bobber.position, lowSag);
            Vector3 lowMidpoint = line.GetPosition(1);
            FishingWorldView.ApplyLineGeometry(line, rodTip.position, bobber.position, highSag);
            Vector3 highMidpoint = line.GetPosition(1);

            Assert.That(lowMidpoint.y, Is.LessThan(highMidpoint.y));
            Assert.That(Vector3.Distance(line.GetPosition(0), rodTip.position), Is.LessThan(0.002f));
            Assert.That(Vector3.Distance(line.GetPosition(2), bobber.position), Is.LessThan(0.002f));
        }

        [UnityTest]
        public IEnumerator V2Flow_DrivesDistinctVisualsAndCameraReturnsWithoutDrift()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = Object.FindAnyObjectByType<FishingMiniGameFacade>();
            FishingWorldView worldView = Object.FindAnyObjectByType<FishingWorldView>();
            Camera camera = Camera.main;
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);
            Assert.That(worldView, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);

            facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, controller.Config.BuildSessionFishProfile());
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "v2-presentation-flow",
                CountdownSeconds = 0f,
                Seed = 59
            });
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            facade.BeginRound();
            yield return WaitUntilOrFail(
                () => controller.SessionSnapshot.State == FishingSessionState.Playing &&
                      controller.Snapshot.State == FishingPlayerState.Idle,
                2f,
                "The V2 presentation session did not become playable.");

            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            yield return WaitUntilOrFail(() => controller.Snapshot.IsNibbling, 6f,
                "The V2 presentation did not observe a nibble.");
            yield return null;
            FishingV2VisualFrame nibble = worldView.CurrentV2Visual;
            Assert.That(nibble.NibblePulseNormalized, Is.GreaterThan(0f));

            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.BiteWindow,
                3f,
                "The V2 presentation did not observe a bite.");
            yield return null;
            FishingV2VisualFrame bite = worldView.CurrentV2Visual;
            Assert.That(bite.BitePulseNormalized, Is.GreaterThan(0f));
            Assert.That(bite.RodKickNormalized, Is.GreaterThan(nibble.RodKickNormalized));
            Assert.That(bite.SurfaceDisturbanceNormalized, Is.GreaterThan(nibble.SurfaceDisturbanceNormalized));
            Assert.That(Vector3.Distance(camera.transform.position, worldView.BaseCameraPosition), Is.GreaterThan(0.001f));

            yield return new WaitForSecondsRealtime(0.42f);
            Assert.That(Vector3.Distance(camera.transform.position, worldView.BaseCameraPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, worldView.BaseCameraRotation), Is.LessThan(0.001f));

            input.SetNextFrame(Frame(rodPitch: 0.8f, rodYaw: -0.65f));
            yield return null;
            Assert.That(Vector3.Distance(camera.transform.position, worldView.BaseCameraPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, worldView.BaseCameraRotation), Is.LessThan(0.001f));

            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Hooked));
            yield return null;
            Assert.That(worldView.CurrentV2Visual.RodLoadNormalized, Is.GreaterThan(0f));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                "The hooked fish did not reach the existing T4 fight presentation.");
            yield return null;
            Assert.That(worldView.CurrentV2Visual.RodLoadNormalized, Is.GreaterThan(0f));
        }

        private static Transform FindTransform(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName) return transforms[i];
            }
            return null;
        }

        private static LineRenderer FindLine(string objectName)
        {
            LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].name == objectName) return lines[i];
            }
            return null;
        }

        private static void AssertViewportVisible(Vector3 point, string label)
        {
            Assert.That(point.z, Is.GreaterThan(0f), $"{label} is behind the camera.");
            Assert.That(point.x, Is.InRange(0f, 1f), $"{label} is outside the horizontal Game View.");
            Assert.That(point.y, Is.InRange(0f, 1f), $"{label} is outside the vertical Game View.");
        }

        private static FishingV2PresentationInput FightInput(float tension)
        {
            return new FishingV2PresentationInput
            {
                PlayerState = FishingPlayerState.Fighting,
                BehaviorState = FishingV2BehaviorState.Fight,
                FishForceNormalized = 0.6f,
                FishDirectionNormalized = 1f,
                VirtualLineTensionNormalized = tension
            };
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float rodPitch = 0f,
            float rodYaw = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                RodPitch = rodPitch,
                RodYaw = rodYaw,
                TensionNormalized = 0.5f,
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
