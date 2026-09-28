using System.Collections;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV3HudPresenterPlayModeTests
    {
        [UnityTest]
        public IEnumerator PresenterUpdate_TracksFacadeAndLifecycleWithoutDrivingGameplay()
        {
            GameObject host = new GameObject("FishingV3HudPresenterPlayModeTests");
            try
            {
                FishingGameController controller = host.AddComponent<FishingGameController>();
                FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
                CountingInputSource input = new CountingInputSource();
                controller.SetInputSource(input);
                controller.ConfigureV3Runtime(
                    StableTuning(),
                    new FishingV3ReelInputTuning
                    {
                        VirtualReelSpeedRevolutionsPerSecond = 1f
                    });
                controller.SetV3FishState(FishingV3FishState.Fight);

                GameObject hudRoot = new GameObject("HudRoot", typeof(RectTransform));
                hudRoot.transform.SetParent(host.transform, false);
                Image tensionFill = CreateImage("TensionFill", hudRoot.transform);
                Image captureFill = CreateImage("CaptureFill", hudRoot.transform);
                FishingV3HudPresenter presenter = host.AddComponent<FishingV3HudPresenter>();
                presenter.Configure(facade, hudRoot, tensionFill, captureFill);
                presenter.ConfigureResultDisplayDuration(0.1f);
                presenter.ConfigureCaughtResultDelay(0f);
                controller.enabled = false;
                controller.BeginRound();

                yield return null;

                Assert.That(presenter.IsHudVisible, Is.True);
                Assert.That(tensionFill.fillAmount, Is.EqualTo(0.5f).Within(0.000001f));
                Assert.That(presenter.DisplayedTensionMarkerNormalized,
                    Is.EqualTo(0.5f).Within(0.000001f));
                Assert.That(captureFill.fillAmount, Is.Zero);
                Assert.That(input.ReadCount, Is.Zero);

                controller.TickRuntime(0.5f);
                int readsAfterGameplayTick = input.ReadCount;
                yield return null;

                Assert.That(captureFill.fillAmount, Is.GreaterThan(0f));
                Assert.That(input.ReadCount, Is.EqualTo(readsAfterGameplayTick));

                controller.SetPaused(true);
                float pausedCapture = captureFill.fillAmount;
                yield return null;
                Assert.That(presenter.IsHudVisible, Is.True);
                Assert.That(captureFill.fillAmount, Is.EqualTo(pausedCapture));

                controller.SetPaused(false);
                yield return null;
                Assert.That(presenter.IsHudVisible, Is.True);

                controller.TickRuntime(0.5f);
                yield return null;
                Assert.That(controller.V3Snapshot.Result, Is.EqualTo(FishingV3Result.Caught));
                Assert.That(controller.V3Snapshot.RuntimeState,
                    Is.EqualTo(FishingV3RuntimeState.Completed));
                Assert.That(presenter.IsHudVisible, Is.True);
                Assert.That(presenter.AreFightGaugesVisible, Is.False);
                Assert.That(presenter.IsTensionStatusVisible, Is.False);
                Assert.That(presenter.IsResultOverlayVisible, Is.True);
                Assert.That(presenter.DisplayedResultTitle, Is.EqualTo("잡았다!"));
                Assert.That(tensionFill.fillAmount, Is.Zero);
                Assert.That(presenter.DisplayedTensionMarkerNormalized, Is.Zero);
                Assert.That(captureFill.fillAmount, Is.Zero);

                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(presenter.IsResultOverlayVisible, Is.False);
                Assert.That(presenter.IsHudVisible, Is.False);

                controller.BeginRound();
                controller.SetV3FishState(FishingV3FishState.Fight);
                yield return null;
                Assert.That(presenter.IsHudVisible, Is.True);
                Assert.That(presenter.IsResultOverlayVisible, Is.False);
                Assert.That(presenter.DisplayedTensionMarkerNormalized,
                    Is.EqualTo(controller.V3Snapshot.TensionNormalized).Within(0.000001f));
                Assert.That(captureFill.fillAmount, Is.Zero);

                controller.AbortRound();
                yield return null;
                Assert.That(presenter.IsHudVisible, Is.False);
                Assert.That(presenter.IsResultOverlayVisible, Is.False);
                Assert.That(tensionFill.fillAmount, Is.Zero);
                Assert.That(presenter.DisplayedTensionMarkerNormalized, Is.Zero);
                Assert.That(captureFill.fillAmount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static Image CreateImage(string name, Transform parent)
        {
            GameObject instance = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<Image>();
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
                CaptureScale = 1f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
        }

        private sealed class CountingInputSource : IFishingInputSource
        {
            public bool IsConnected => true;
            public int ReadCount { get; private set; }

            public FishingInputFrame ReadFrame()
            {
                ReadCount++;
                return new FishingInputFrame
                {
                    ReelDelta = 1f,
                    IsDeviceConnected = true
                };
            }

            public void ResetState()
            {
            }
        }
    }
}
