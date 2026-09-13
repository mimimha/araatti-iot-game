using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3ResistanceMapperTests
    {
        [Test]
        public void BaseResistance_IsNormalizedAndMonotonicFromTensionOnly()
        {
            FishingV3ResistanceMapper mapper = CreateMapper(
                minimum: 0.1f,
                maximum: 0.9f);
            float[] tensions = { 0f, 0.25f, 0.5f, 0.75f, 1f };
            float[] values = tensions.Select(mapper.CalculateBaseResistance).ToArray();

            Assert.That(values[0], Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(values[values.Length - 1], Is.EqualTo(0.9f).Within(0.000001f));
            for (int i = 0; i < values.Length; i++)
            {
                Assert.That(values[i], Is.InRange(0f, 1f));
                if (i > 0) Assert.That(values[i], Is.GreaterThanOrEqualTo(values[i - 1]));
            }

            bool ownsFishState = typeof(FishingV3ResistanceMapper)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(FishingV3FishState));
            bool acceptsFishState = typeof(FishingV3ResistanceMapper)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(method => method.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(FishingV3FishState));
            Assert.That(ownsFishState || acceptsFishState, Is.False);
        }

        [Test]
        public void InvalidTension_AlwaysProducesFiniteNormalizedResistance()
        {
            FishingV3ResistanceMapper mapper = CreateMapper();

            float[] inputs =
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
                -1f,
                2f
            };
            foreach (float input in inputs)
            {
                FishingResistanceCommand command = mapper.Tick(input, 0.1f);
                AssertFiniteNormalized(command.ResistanceNormalized);
                AssertFiniteNormalized(command.BaseResistanceNormalized);
                AssertFiniteNormalized(command.HeadShakeOverlayNormalized);
            }
        }

        [Test]
        public void BaseResistanceRise_UsesConfiguredDeltaTimeSlew()
        {
            FishingV3ResistanceMapper mapper = CreateMapper(rise: 0.5f);

            FishingResistanceCommand first = mapper.Tick(1f, 0.5f);
            FishingResistanceCommand second = mapper.Tick(1f, 0.5f);

            Assert.That(first.BaseResistanceNormalized, Is.EqualTo(0.25f).Within(0.000001f));
            Assert.That(second.BaseResistanceNormalized, Is.EqualTo(0.5f).Within(0.000001f));
        }

        [Test]
        public void BaseResistanceFall_UsesConfiguredDeltaTimeSlew()
        {
            FishingV3ResistanceMapper mapper = CreateMapper(rise: 1f, fall: 0.25f);
            mapper.Tick(1f, 1f);

            FishingResistanceCommand falling = mapper.Tick(0f, 0.5f);

            Assert.That(falling.BaseResistanceNormalized, Is.EqualTo(0.875f).Within(0.000001f));
        }

        [Test]
        public void Slew_IsConsistentAcrossEquivalentDeltaTimeSlices()
        {
            FishingV3ResistanceMapper whole = CreateMapper(rise: 0.5f);
            FishingV3ResistanceMapper sliced = CreateMapper(rise: 0.5f);

            whole.Tick(0.8f, 1f);
            for (int i = 0; i < 10; i++) sliced.Tick(0.8f, 0.1f);

            Assert.That(sliced.CurrentCommand.BaseResistanceNormalized,
                Is.EqualTo(whole.CurrentCommand.BaseResistanceNormalized).Within(0.000001f));
        }

        [Test]
        public void HeadShakeOff_FinalResistanceEqualsSmoothedBase()
        {
            FishingV3ResistanceMapper mapper = CreateMapper(rise: 10f);

            FishingResistanceCommand command = mapper.Tick(0.4f, 0.1f);

            Assert.That(command.HeadShakeOverlayNormalized, Is.Zero);
            Assert.That(command.HeadShakePulseActive, Is.False);
            Assert.That(command.ResistanceNormalized,
                Is.EqualTo(command.BaseResistanceNormalized).Within(0.000001f));
        }

        [Test]
        public void HeadShake_IsPositiveOverlayAppliedAfterBaseSlewAndClamped()
        {
            FishingV3ResistanceMapper baseline = CreateMapper(
                rise: 0.5f,
                headShakeAmplitude: 0.8f);
            FishingV3ResistanceMapper pulsed = CreateMapper(
                rise: 0.5f,
                headShakeAmplitude: 0.8f);
            pulsed.TriggerHeadShake(1f);

            FishingResistanceCommand baseCommand = baseline.Tick(0.8f, 0.2f);
            FishingResistanceCommand pulseCommand = pulsed.Tick(0.8f, 0.2f);

            Assert.That(pulseCommand.BaseResistanceNormalized,
                Is.EqualTo(baseCommand.BaseResistanceNormalized).Within(0.000001f));
            Assert.That(pulseCommand.HeadShakeOverlayNormalized, Is.GreaterThan(0f));
            Assert.That(pulseCommand.ResistanceNormalized,
                Is.EqualTo(Math.Min(
                    1f,
                    pulseCommand.BaseResistanceNormalized +
                    pulseCommand.HeadShakeOverlayNormalized)).Within(0.000001f));
            Assert.That(pulseCommand.ResistanceNormalized, Is.InRange(0f, 1f));
        }

        [Test]
        public void HeadShake_NeverCreatesNegativeResistanceAndExpires()
        {
            FishingV3ResistanceMapper mapper = CreateMapper(
                rise: 10f,
                headShakeAmplitude: 0.3f,
                headShakeDuration: 0.1f);
            mapper.TriggerHeadShake(float.NegativeInfinity);
            Assert.That(mapper.Tick(0f, 0.01f).HeadShakePulseActive, Is.False);

            mapper.TriggerHeadShake(1f);
            FishingResistanceCommand pulse = mapper.Tick(0f, 0.1f);
            FishingResistanceCommand expired = mapper.Tick(0f, 0.01f);

            Assert.That(pulse.HeadShakeOverlayNormalized, Is.GreaterThan(0f));
            Assert.That(pulse.ResistanceNormalized, Is.GreaterThanOrEqualTo(0f));
            Assert.That(expired.HeadShakeOverlayNormalized, Is.Zero);
            Assert.That(expired.HeadShakePulseActive, Is.False);
        }

        [Test]
        public void Reset_ClearsSmoothingPulseAndCurrentCommand()
        {
            FishingV3ResistanceMapper mapper = CreateMapper(rise: 10f);
            mapper.TriggerHeadShake(1f);
            mapper.Tick(1f, 0.05f);

            mapper.Reset();

            Assert.That(mapper.SmoothedBaseResistanceNormalized, Is.Zero);
            Assert.That(mapper.HeadShakePulseRemainingSeconds, Is.Zero);
            Assert.That(mapper.CurrentCommand.ResistanceNormalized, Is.Zero);
            Assert.That(mapper.CurrentCommand.HeadShakePulseActive, Is.False);
        }

        [Test]
        public void HeadShakeMapper_DoesNotMutateV3CoreStateOrResult()
        {
            FishingV3Model model = new FishingV3Model(ActiveModelTuning(0.5f));
            model.Tick(FishingV3FishState.Fight, 0.1f, 0.1f);
            float tension = model.TensionNormalized;
            float capture = model.CaptureProgressNormalized;
            float breakStress = model.BreakStressNormalized;
            float escapeRisk = model.EscapeRiskNormalized;
            FishingV3Result result = model.Result;
            FishingV3ResistanceMapper mapper = CreateMapper();

            mapper.TriggerHeadShake(1f);
            mapper.Tick(tension, 0.1f);

            Assert.That(model.TensionNormalized, Is.EqualTo(tension));
            Assert.That(model.CaptureProgressNormalized, Is.EqualTo(capture));
            Assert.That(model.BreakStressNormalized, Is.EqualTo(breakStress));
            Assert.That(model.EscapeRiskNormalized, Is.EqualTo(escapeRisk));
            Assert.That(model.Result, Is.EqualTo(result));
        }

        [Test]
        public void ActiveZeroCommand_RemainsDistinctFromSafetyStop()
        {
            FishingV3ResistanceMapper mapper = CreateMapper();
            MockFishingResistanceOutput output = new MockFishingResistanceOutput();

            output.ApplyCommand(mapper.Tick(0f, 0.1f));

            Assert.That(output.LastCommand.ResistanceNormalized, Is.Zero);
            Assert.That(output.IsStopped, Is.False);
            output.Stop();
            Assert.That(output.IsStopped, Is.True);
        }

        private static FishingV3ResistanceMapper CreateMapper(
            float minimum = 0f,
            float maximum = 1f,
            float rise = 1f,
            float fall = 1f,
            float headShakeAmplitude = 0.2f,
            float headShakeDuration = 0.25f)
        {
            return new FishingV3ResistanceMapper(new FishingV3ResistanceTuning
            {
                MinimumResistanceNormalized = minimum,
                MaximumResistanceNormalized = maximum,
                RiseRatePerSecond = rise,
                FallRatePerSecond = fall,
                HeadShakeAmplitudeNormalized = headShakeAmplitude,
                HeadShakeDurationSeconds = headShakeDuration
            });
        }

        internal static FishingV3Tuning ActiveModelTuning(float tension)
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

        private static void AssertFiniteNormalized(float value)
        {
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
            Assert.That(value, Is.InRange(0f, 1f));
        }
    }

    public sealed class FishingV3ResistanceControllerTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }
            _objects.Clear();
        }

        [Test]
        public void ActiveV3Tick_AppliesExactlyOneResistanceCommand()
        {
            Fixture fixture = CreateFixture(
                FishingV3ResistanceMapperTests.ActiveModelTuning(0.5f),
                Frame(0f));
            fixture.Output.ResetCounts();

            fixture.Controller.TickRuntime(0.1f);

            Assert.That(fixture.Output.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Output.StopCount, Is.Zero);
            Assert.That(fixture.Output.IsStopped, Is.False);
            Assert.That(fixture.Controller.LastResistanceCommand.SourceBehavior,
                Is.EqualTo(FishingV2BehaviorState.None));
        }

        [Test]
        public void ActiveV3ZeroResistance_UsesApplyInsteadOfStop()
        {
            Fixture fixture = CreateFixture(
                FishingV3ResistanceMapperTests.ActiveModelTuning(0f),
                Frame(0f));
            fixture.Output.ResetCounts();

            fixture.Controller.TickRuntime(0.1f);

            Assert.That(fixture.Output.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Output.StopCount, Is.Zero);
            Assert.That(fixture.Output.LastCommand.ResistanceNormalized, Is.Zero);
            Assert.That(fixture.Controller.IsResistanceStopped, Is.False);
        }

        [Test]
        public void ControllerHeadShake_OverlaysResistanceWithoutChangingSnapshot()
        {
            Fixture fixture = CreateFixture(
                FishingV3ResistanceMapperTests.ActiveModelTuning(0.5f),
                Frame(0f));
            float tension = fixture.Controller.V3Snapshot.TensionNormalized;
            float capture = fixture.Controller.V3Snapshot.CaptureProgressNormalized;
            fixture.Output.ResetCounts();

            fixture.Controller.TriggerV3HeadShake(1f);
            fixture.Controller.TickRuntime(0.05f);

            Assert.That(fixture.Output.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Output.LastCommand.HeadShakeOverlayNormalized,
                Is.GreaterThan(0f));
            Assert.That(fixture.Controller.V3Snapshot.TensionNormalized, Is.EqualTo(tension));
            Assert.That(fixture.Controller.V3Snapshot.CaptureProgressNormalized, Is.EqualTo(capture));
        }

        [Test]
        public void PauseStopsAndFreezesPulse_ResumeAppliesWithoutStalePulse()
        {
            Fixture fixture = CreateFixture(
                FishingV3ResistanceMapperTests.ActiveModelTuning(0.8f),
                Frame(0f));
            fixture.Controller.TriggerV3HeadShake(1f);
            fixture.Output.ResetCounts();

            fixture.Controller.SetPaused(true);
            fixture.Controller.TickRuntime(1f);

            Assert.That(fixture.Output.ApplyCount, Is.Zero);
            Assert.That(fixture.Output.StopCount, Is.EqualTo(1));
            Assert.That(fixture.Output.IsStopped, Is.True);
            Assert.That(fixture.Controller.V3Snapshot.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Paused));

            fixture.Controller.SetPaused(false);
            fixture.Output.ResetCounts();
            fixture.Controller.TickRuntime(0.1f);

            Assert.That(fixture.Output.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Output.StopCount, Is.Zero);
            Assert.That(fixture.Output.LastCommand.HeadShakeOverlayNormalized, Is.Zero);
            Assert.That(fixture.Output.IsStopped, Is.False);
        }

        [Test]
        public void ResetCycle_StopsAndClearsPriorMapperState()
        {
            Fixture fixture = CreateFixture(
                FishingV3ResistanceMapperTests.ActiveModelTuning(1f),
                Frame(0f),
                ResistanceTuning(rise: 0.5f));
            fixture.Controller.TickRuntime(1f);
            Assert.That(fixture.Output.LastCommand.BaseResistanceNormalized,
                Is.EqualTo(0.5f).Within(0.000001f));
            fixture.Controller.TriggerV3HeadShake(1f);
            fixture.Output.ResetCounts();

            fixture.Controller.ResetCycle();

            Assert.That(fixture.Output.StopCount, Is.EqualTo(1));
            Assert.That(fixture.Controller.IsResistanceStopped, Is.True);
            fixture.Controller.BeginRound();
            fixture.Output.ResetCounts();
            fixture.Controller.TickRuntime(0.1f);
            Assert.That(fixture.Output.LastCommand.BaseResistanceNormalized,
                Is.EqualTo(0.05f).Within(0.000001f));
            Assert.That(fixture.Output.LastCommand.HeadShakeOverlayNormalized, Is.Zero);
        }

        [Test]
        public void Abort_StopsOnceAndPreventsFurtherApply()
        {
            Fixture fixture = CreateFixture(
                FishingV3ResistanceMapperTests.ActiveModelTuning(0.5f),
                Frame(0f));
            fixture.Output.ResetCounts();

            fixture.Controller.AbortRound();

            Assert.That(fixture.Output.StopCount, Is.EqualTo(1));
            Assert.That(fixture.Output.ApplyCount, Is.Zero);
            fixture.Output.ResetCounts();
            fixture.Controller.TickRuntime(1f);
            Assert.That(fixture.Output.TotalActions, Is.Zero);
        }

        [Test]
        public void Shutdown_StopsOnceAndPreventsFurtherApply()
        {
            Fixture fixture = CreateFixture(
                FishingV3ResistanceMapperTests.ActiveModelTuning(0.5f),
                Frame(0f));
            fixture.Output.ResetCounts();

            fixture.Controller.ShutdownRuntime();

            Assert.That(fixture.Output.StopCount, Is.EqualTo(1));
            Assert.That(fixture.Output.ApplyCount, Is.Zero);
            fixture.Output.ResetCounts();
            fixture.Controller.TickRuntime(1f);
            Assert.That(fixture.Output.TotalActions, Is.Zero);
        }

        [TestCase(FishingV3Result.Caught)]
        [TestCase(FishingV3Result.LineBroken)]
        [TestCase(FishingV3Result.FishEscaped)]
        public void TerminalTick_UsesStopOnlyAndNeverRestarts(FishingV3Result expected)
        {
            FishingV3Tuning tuning;
            FishingInputFrame input;
            switch (expected)
            {
                case FishingV3Result.LineBroken:
                    tuning = TerminalTuning(0.95f, breakRate: 2f);
                    input = Frame(0f);
                    break;
                case FishingV3Result.FishEscaped:
                    tuning = TerminalTuning(0.05f, escapeRate: 2f);
                    input = Frame(0f);
                    break;
                default:
                    tuning = TerminalTuning(0.5f, captureScale: 1f);
                    input = Frame(1f);
                    break;
            }

            Fixture fixture = CreateFixture(tuning, input);
            fixture.Controller.TriggerV3HeadShake(1f);
            fixture.Output.ResetCounts();

            fixture.Controller.TickRuntime(1f);

            Assert.That(fixture.Controller.V3Snapshot.Result, Is.EqualTo(expected));
            Assert.That(fixture.Output.ApplyCount, Is.Zero);
            Assert.That(fixture.Output.StopCount, Is.EqualTo(1));
            Assert.That(fixture.Output.IsStopped, Is.True);
            Assert.That(fixture.Output.LastCommand.HeadShakePulseActive, Is.False);

            fixture.Output.ResetCounts();
            fixture.Controller.TickRuntime(1f);
            Assert.That(fixture.Output.TotalActions, Is.Zero);
        }

        private Fixture CreateFixture(
            FishingV3Tuning modelTuning,
            FishingInputFrame input,
            FishingV3ResistanceTuning resistanceTuning = null)
        {
            GameObject gameObject = new GameObject("FishingV3ResistanceControllerTests");
            _objects.Add(gameObject);
            FishingGameController controller = gameObject.AddComponent<FishingGameController>();
            FixedInputSource inputSource = new FixedInputSource(input);
            CountingResistanceOutput output = new CountingResistanceOutput();
            controller.SetInputSource(inputSource);
            controller.SetResistanceOutput(output);
            controller.ConfigureV3Runtime(
                modelTuning,
                new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                },
                resistanceTuning ?? ResistanceTuning());
            controller.BeginRound();
            return new Fixture(controller, output);
        }

        private static FishingV3ResistanceTuning ResistanceTuning(float rise = 10f)
        {
            return new FishingV3ResistanceTuning
            {
                MinimumResistanceNormalized = 0f,
                MaximumResistanceNormalized = 1f,
                RiseRatePerSecond = rise,
                FallRatePerSecond = 10f,
                HeadShakeAmplitudeNormalized = 0.2f,
                HeadShakeDurationSeconds = 0.25f
            };
        }

        private static FishingV3Tuning TerminalTuning(
            float tension,
            float captureScale = 0f,
            float breakRate = 0f,
            float escapeRate = 0f)
        {
            FishingV3Tuning tuning =
                FishingV3ResistanceMapperTests.ActiveModelTuning(tension);
            tuning.CaptureScale = captureScale;
            tuning.BreakStressPerSecond = breakRate;
            tuning.EscapeRiskPerSecond = escapeRate;
            return tuning;
        }

        private static FishingInputFrame Frame(float reelInput)
        {
            return new FishingInputFrame
            {
                ReelDelta = reelInput,
                IsDeviceConnected = true
            };
        }

        private readonly struct Fixture
        {
            public FishingGameController Controller { get; }
            public CountingResistanceOutput Output { get; }

            public Fixture(
                FishingGameController controller,
                CountingResistanceOutput output)
            {
                Controller = controller;
                Output = output;
            }
        }

        private sealed class FixedInputSource : IFishingInputSource
        {
            private readonly FishingInputFrame _frame;
            public bool IsConnected => _frame.IsDeviceConnected;

            public FixedInputSource(FishingInputFrame frame)
            {
                _frame = frame;
            }

            public FishingInputFrame ReadFrame()
            {
                return _frame;
            }

            public void ResetState()
            {
            }
        }

        internal sealed class CountingResistanceOutput : IFishingResistanceOutput
        {
            public FishingResistanceCommand LastCommand { get; private set; } =
                FishingResistanceCommand.Zero;
            public bool IsStopped { get; private set; } = true;
            public int ApplyCount { get; private set; }
            public int StopCount { get; private set; }
            public int TotalActions => ApplyCount + StopCount;

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
