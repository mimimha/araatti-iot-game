using System.Collections;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV3ResistanceFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator Facade_RoutesV3ResistanceAndLifecycleSafety()
        {
            GameObject gameObject = new GameObject("FishingV3ResistanceFeedbackPlayModeTests");
            FishingGameController controller = gameObject.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = gameObject.AddComponent<FishingMiniGameFacade>();
            CountingResistanceOutput output = new CountingResistanceOutput();
            try
            {
                controller.SetInputSource(new ConstantInputSource(0f));
                controller.SetResistanceOutput(output);
                facade.ConfigureV3Runtime(
                    ActiveTuning(0.5f),
                    new FishingV3ReelInputTuning(),
                    ResistanceTuning());
                facade.Initialize(new FishingLaunchContext { CountdownSeconds = 0f });
                facade.BeginRound();
                facade.SetV3FishState(FishingV3FishState.Fight);
                output.ResetCounts();

                facade.TriggerV3HeadShake(1f);
                controller.TickRuntime(0.05f);

                Assert.That(output.ApplyCount, Is.EqualTo(1));
                Assert.That(output.StopCount, Is.Zero);
                Assert.That(output.LastCommand.HeadShakeOverlayNormalized, Is.GreaterThan(0f));

                output.ResetCounts();
                facade.SetPaused(true);
                controller.TickRuntime(1f);
                Assert.That(output.ApplyCount, Is.Zero);
                Assert.That(output.StopCount, Is.EqualTo(1));
                Assert.That(output.IsStopped, Is.True);

                facade.SetPaused(false);
                output.ResetCounts();
                controller.TickRuntime(0.05f);
                Assert.That(output.ApplyCount, Is.EqualTo(1));
                Assert.That(output.StopCount, Is.Zero);
                Assert.That(output.LastCommand.HeadShakeOverlayNormalized, Is.Zero);

                output.ResetCounts();
                facade.Abort(FishingAbortReason.UserRequested);
                Assert.That(output.ApplyCount, Is.Zero);
                Assert.That(output.StopCount, Is.EqualTo(1));

                facade.ConfigureV3Runtime(
                    ActiveTuning(0.5f),
                    new FishingV3ReelInputTuning(),
                    ResistanceTuning());
                facade.BeginRound();
                output.ResetCounts();
                facade.Shutdown();
                Assert.That(output.ApplyCount, Is.Zero);
                Assert.That(output.StopCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CaughtTick_UsesStopWithoutActiveApply()
        {
            GameObject gameObject = new GameObject("FishingV3TerminalResistancePlayModeTests");
            FishingGameController controller = gameObject.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = gameObject.AddComponent<FishingMiniGameFacade>();
            CountingResistanceOutput output = new CountingResistanceOutput();
            try
            {
                FishingV3Tuning tuning = ActiveTuning(0.5f);
                tuning.CaptureScale = 1f;
                controller.SetInputSource(new ConstantInputSource(1f));
                controller.SetResistanceOutput(output);
                facade.ConfigureV3Runtime(
                    tuning,
                    new FishingV3ReelInputTuning
                    {
                        VirtualReelSpeedRevolutionsPerSecond = 1f
                    },
                    ResistanceTuning());
                facade.BeginRound();
                output.ResetCounts();
                facade.TriggerV3HeadShake(1f);

                controller.TickRuntime(1f);

                Assert.That(facade.V3Current.Result, Is.EqualTo(FishingV3Result.Caught));
                Assert.That(output.ApplyCount, Is.Zero);
                Assert.That(output.StopCount, Is.EqualTo(1));
                Assert.That(output.IsStopped, Is.True);

                output.ResetCounts();
                controller.TickRuntime(1f);
                Assert.That(output.ApplyCount + output.StopCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            yield return null;
        }

        private static FishingV3Tuning ActiveTuning(float tension)
        {
            return new FishingV3Tuning
            {
                InitialTensionNormalized = tension,
                CalmBaseTension = tension,
                FightBaseTension = tension,
                RunBaseTension = tension,
                ReelTensionGain = 0f,
                CaptureScale = 0f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
        }

        private static FishingV3ResistanceTuning ResistanceTuning()
        {
            return new FishingV3ResistanceTuning
            {
                RiseRatePerSecond = 10f,
                FallRatePerSecond = 10f,
                HeadShakeAmplitudeNormalized = 0.2f,
                HeadShakeDurationSeconds = 0.25f
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

        private sealed class CountingResistanceOutput : IFishingResistanceOutput
        {
            public FishingResistanceCommand LastCommand { get; private set; } =
                FishingResistanceCommand.Zero;
            public bool IsStopped { get; private set; } = true;
            public int ApplyCount { get; private set; }
            public int StopCount { get; private set; }

            public void ApplyCommand(FishingResistanceCommand command)
            {
                LastCommand = command;
                IsStopped = false;
                ApplyCount++;
            }

            public void Stop()
            {
                LastCommand = FishingResistanceCommand.Zero;
                IsStopped = true;
                StopCount++;
            }

            public void ResetCounts()
            {
                ApplyCount = 0;
                StopCount = 0;
            }
        }
    }
}
