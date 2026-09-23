using System;
using System.Linq;
using System.Reflection;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3ControllerWiringTests
    {
        private GameObject _gameObject;
        private FishingGameController _controller;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("FishingV3ControllerWiringTests");
            _controller = _gameObject.AddComponent<FishingGameController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void V3Tick_SamplesInputExactlyOnceAndDoesNotAdvanceV2()
        {
            CountingInputSource input = new CountingInputSource(Frame(0.5f));
            _controller.SetInputSource(input);
            _controller.ConfigureV3Runtime(StableSafeTuning(), ReelTuning());
            _controller.BeginRound();
            _controller.SetV3FishState(FishingV3FishState.Fight);
            FishingPlayerState v2StateBefore = _controller.Snapshot.State;

            _controller.TickRuntime(0.5f);

            Assert.That(input.ReadCount, Is.EqualTo(1));
            Assert.That(_controller.V3Snapshot.CaptureProgressNormalized,
                Is.EqualTo(0.25f).Within(0.000001f));
            Assert.That(_controller.Snapshot.State, Is.EqualTo(v2StateBefore));
        }

        [Test]
        public void V3Runtime_DoesNotOwnOrAcceptAnInputSource()
        {
            Type inputType = typeof(IFishingInputSource);
            Type runtimeType = typeof(FishingV3Runtime);

            bool hasInputSourceField = runtimeType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => inputType.IsAssignableFrom(field.FieldType));
            bool hasInputSourceParameter = runtimeType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(method => method.GetParameters())
                .Any(parameter => inputType.IsAssignableFrom(parameter.ParameterType));

            Assert.That(hasInputSourceField, Is.False);
            Assert.That(hasInputSourceParameter, Is.False);
        }

        [Test]
        public void DefaultControllerPath_RemainsLegacyV2()
        {
            CountingInputSource input = new CountingInputSource(new FishingInputFrame
            {
                CastPressed = true,
                IsDeviceConnected = true,
                TensionNormalized = 0.5f
            });
            _controller.ConfigureLaunchContext(new FishingLaunchContext
            {
                CountdownSeconds = 0f,
                RoundDurationSeconds = 30f
            });
            _controller.SetInputSource(input);

            _controller.BeginRound();
            _controller.TickRuntime(0.1f);

            Assert.That(_controller.GameplayRuntimeMode,
                Is.EqualTo(FishingGameplayRuntimeMode.LegacyV2));
            Assert.That(_controller.V3Snapshot, Is.Null);
            Assert.That(input.ReadCount, Is.EqualTo(1));
            Assert.That(_controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Casting));
        }

        [Test]
        public void ControllerPauseResumeAbortAndShutdown_DelegateToV3Only()
        {
            CountingInputSource input = new CountingInputSource(Frame(0.2f));
            _controller.SetInputSource(input);
            _controller.ConfigureV3Runtime(StableSafeTuning(), ReelTuning());
            _controller.BeginRound();
            _controller.SetV3FishState(FishingV3FishState.Fight);
            _controller.TickRuntime(0.5f);
            float beforePause = _controller.V3Snapshot.CaptureProgressNormalized;
            int readsBeforePause = input.ReadCount;

            _controller.SetPaused(true);
            _controller.TickRuntime(1f);
            Assert.That(input.ReadCount, Is.EqualTo(readsBeforePause));
            Assert.That(_controller.V3Snapshot.CaptureProgressNormalized, Is.EqualTo(beforePause));

            _controller.SetPaused(false);
            _controller.TickRuntime(0.5f);
            Assert.That(_controller.V3Snapshot.CaptureProgressNormalized, Is.GreaterThan(beforePause));

            _controller.AbortRound();
            float beforeAbortedTick = _controller.V3Snapshot.CaptureProgressNormalized;
            _controller.TickRuntime(1f);
            Assert.That(_controller.V3Snapshot.RuntimeState, Is.EqualTo(FishingV3RuntimeState.Aborted));
            Assert.That(_controller.V3Snapshot.CaptureProgressNormalized, Is.EqualTo(beforeAbortedTick));

            _controller.ConfigureV3Runtime(StableSafeTuning(), ReelTuning());
            _controller.BeginRound();
            _controller.ShutdownRuntime();
            _controller.TickRuntime(1f);
            Assert.That(_controller.V3Snapshot.RuntimeState, Is.EqualTo(FishingV3RuntimeState.Shutdown));
            Assert.That(_controller.V3Snapshot.CaptureProgressNormalized, Is.Zero);
        }

        [TestCase(FishingV3Result.Caught, 0.5f, 0.5f, 0f, 2f)]
        [TestCase(FishingV3Result.LineBroken, 0.95f, 0f, 2f, 0f)]
        [TestCase(FishingV3Result.FishEscaped, 0.05f, 0f, 0f, 2f)]
        public void ControllerExposesAllV3TerminalResults(
            FishingV3Result expected,
            float tension,
            float reelInput,
            float breakRate,
            float escapeRate)
        {
            FishingV3Tuning tuning = StableSafeTuning();
            tuning.InitialTensionNormalized = tension;
            tuning.CalmBaseTension = tension;
            tuning.FightBaseTension = tension;
            tuning.RunBaseTension = tension;
            tuning.BreakStressPerSecond = breakRate;
            tuning.EscapeRiskPerSecond = escapeRate;
            _controller.SetInputSource(new CountingInputSource(Frame(reelInput)));
            _controller.ConfigureV3Runtime(tuning, new FishingV3ReelInputTuning
            {
                VirtualReelSpeedRevolutionsPerSecond = 2f
            });
            _controller.BeginRound();

            _controller.TickRuntime(1f);

            Assert.That(_controller.V3Snapshot.Result, Is.EqualTo(expected));
            Assert.That(_controller.V3Snapshot.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Completed));
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

        private static FishingV3ReelInputTuning ReelTuning()
        {
            return new FishingV3ReelInputTuning
            {
                VirtualReelSpeedRevolutionsPerSecond = 1f
            };
        }

        private static FishingInputFrame Frame(float normalizedReelInput)
        {
            return new FishingInputFrame
            {
                ReelDelta = normalizedReelInput,
                IsDeviceConnected = true
            };
        }

        private sealed class CountingInputSource : IFishingInputSource
        {
            private readonly FishingInputFrame _frame;

            public bool IsConnected => true;
            public int ReadCount { get; private set; }

            public CountingInputSource(FishingInputFrame frame)
            {
                _frame = frame;
            }

            public FishingInputFrame ReadFrame()
            {
                ReadCount++;
                return _frame;
            }

            public void ResetState()
            {
            }
        }
    }
}
