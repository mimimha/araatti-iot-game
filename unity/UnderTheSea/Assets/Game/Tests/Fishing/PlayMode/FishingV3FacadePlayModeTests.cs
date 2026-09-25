using System.Collections;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV3FacadePlayModeTests
    {
        [UnityTest]
        public IEnumerator Facade_ExposesV3RuntimeResultFromControllerInputPath()
        {
            GameObject gameObject = new GameObject("FishingV3FacadePlayModeTests");
            FishingGameController controller = gameObject.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = gameObject.AddComponent<FishingMiniGameFacade>();
            try
            {
                controller.SetInputSource(new ConstantInputSource(1f));
                facade.ConfigureV3Runtime(StableSafeTuning(), new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                });
                facade.Initialize(new FishingLaunchContext { CountdownSeconds = 0f });
                facade.BeginRound();
                facade.SetV3FishState(FishingV3FishState.Fight);

                controller.TickRuntime(1f);

                Assert.That(facade.GameplayRuntimeMode,
                    Is.EqualTo(FishingGameplayRuntimeMode.V3));
                Assert.That(facade.V3Current.Result, Is.EqualTo(FishingV3Result.Caught));
                Assert.That(facade.V3Current.RuntimeState,
                    Is.EqualTo(FishingV3RuntimeState.Completed));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Facade_PauseResumeAbortAndShutdownControlV3Lifecycle()
        {
            GameObject gameObject = new GameObject("FishingV3FacadeLifecycleTests");
            FishingGameController controller = gameObject.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = gameObject.AddComponent<FishingMiniGameFacade>();
            try
            {
                controller.SetInputSource(new ConstantInputSource(0.2f));
                facade.ConfigureV3Runtime(StableSafeTuning(), new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                });
                facade.Initialize(new FishingLaunchContext { CountdownSeconds = 0f });
                facade.BeginRound();
                facade.SetV3FishState(FishingV3FishState.Fight);
                controller.TickRuntime(0.5f);
                float beforePause = facade.V3Current.CaptureProgressNormalized;

                facade.SetPaused(true);
                controller.TickRuntime(1f);
                Assert.That(facade.V3Current.RuntimeState,
                    Is.EqualTo(FishingV3RuntimeState.Paused));
                Assert.That(facade.V3Current.CaptureProgressNormalized, Is.EqualTo(beforePause));

                facade.SetPaused(false);
                controller.TickRuntime(0.5f);
                Assert.That(facade.V3Current.CaptureProgressNormalized,
                    Is.GreaterThan(beforePause));

                facade.Abort(FishingAbortReason.UserRequested);
                Assert.That(facade.V3Current.RuntimeState,
                    Is.EqualTo(FishingV3RuntimeState.Aborted));

                facade.ConfigureV3Runtime(StableSafeTuning());
                facade.BeginRound();
                facade.Shutdown();
                Assert.That(facade.V3Current.RuntimeState,
                    Is.EqualTo(FishingV3RuntimeState.Shutdown));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            yield return null;
        }

        private static FishingV3Tuning StableSafeTuning()
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

        private sealed class ConstantInputSource : IFishingInputSource
        {
            private readonly float _reelInput;

            public bool IsConnected => true;

            public ConstantInputSource(float reelInput)
            {
                _reelInput = reelInput;
            }

            public FishingInputFrame ReadFrame()
            {
                return new FishingInputFrame
                {
                    ReelDelta = _reelInput,
                    IsDeviceConnected = true
                };
            }

            public void ResetState()
            {
            }
        }
    }
}
