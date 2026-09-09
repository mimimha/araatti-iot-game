using System;
using System.Reflection;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV2ResistanceMapperTests
    {
        [Test]
        public void NonFightingStates_ReturnZero()
        {
            foreach (FishingPlayerState state in Enum.GetValues(typeof(FishingPlayerState)))
            {
                if (state == FishingPlayerState.Fighting) continue;
                FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
                FishingResistanceCommand command = mapper.Tick(CreateSnapshot(state), 0.1f);
                Assert.That(command.ResistanceNormalized, Is.Zero, state.ToString());
            }
        }

        [Test]
        public void BaseResistance_IsOrderedRestFightRunForAllForces()
        {
            foreach (float force in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                float rest = Settled(FishingV2BehaviorState.Rest, force).BaseResistanceNormalized;
                float fight = Settled(FishingV2BehaviorState.Fight, force).BaseResistanceNormalized;
                float run = Settled(FishingV2BehaviorState.Run, force).BaseResistanceNormalized;
                Assert.That(rest, Is.LessThan(fight), $"force={force}");
                Assert.That(fight, Is.LessThan(run), $"force={force}");
            }
        }

        [Test]
        public void BaseResistance_DoesNotDecreaseAsFishForceIncreases()
        {
            foreach (FishingV2BehaviorState behavior in new[]
                     {
                         FishingV2BehaviorState.Rest,
                         FishingV2BehaviorState.Fight,
                         FishingV2BehaviorState.Run
                     })
            {
                float low = Settled(behavior, 0f).BaseResistanceNormalized;
                float mid = Settled(behavior, 0.5f).BaseResistanceNormalized;
                float high = Settled(behavior, 1f).BaseResistanceNormalized;
                Assert.That(mid, Is.GreaterThanOrEqualTo(low), behavior.ToString());
                Assert.That(high, Is.GreaterThanOrEqualTo(mid), behavior.ToString());
            }
        }

        [Test]
        public void VirtualLineTension_DoesNotChangeBaseResistance()
        {
            FishingSnapshot low = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Fight, 0.7f);
            FishingSnapshot high = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Fight, 0.7f);
            Set(low, nameof(FishingSnapshot.VirtualLineTensionNormalized), 0.1f);
            Set(high, nameof(FishingSnapshot.VirtualLineTensionNormalized), 0.9f);

            Assert.That(Settle(low).BaseResistanceNormalized,
                Is.EqualTo(Settle(high).BaseResistanceNormalized).Within(0.0001f));
        }

        [Test]
        public void FinalRun_StrengthensNormalRunWithoutExceedingCap()
        {
            FishingSnapshot normal = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Run, 1f);
            FishingSnapshot finalRun = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Run, 1f);
            Set(finalRun, nameof(FishingSnapshot.IsFinalRun), true);

            float normalBase = Settle(normal).BaseResistanceNormalized;
            float finalBase = Settle(finalRun).BaseResistanceNormalized;
            Assert.That(finalBase, Is.GreaterThanOrEqualTo(normalBase));
            Assert.That(finalBase, Is.LessThanOrEqualTo(FishingV2ResistanceMapper.FinalRunBaseCap));
        }

        [Test]
        public void FinalRunPending_DoesNotBoostRun()
        {
            FishingSnapshot normal = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Run, 0.8f);
            FishingSnapshot pending = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Run, 0.8f);
            Set(pending, nameof(FishingSnapshot.FinalRunPending), true);

            Assert.That(Settle(pending).BaseResistanceNormalized,
                Is.EqualTo(Settle(normal).BaseResistanceNormalized).Within(0.0001f));
        }

        [Test]
        public void HeadShakeSequence_TriggersOnceAndSameSequenceDoesNotRestart()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot snapshot = CreateSnapshot();
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 1);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeIntensityNormalized), 1f);

            FishingResistanceCommand started = mapper.Tick(snapshot, 0.02f);
            Assert.That(started.HeadShakePulseActive, Is.True);
            Assert.That(started.HeadShakeOverlayNormalized, Is.GreaterThan(0f));

            for (int i = 0; i < 4; i++) mapper.Tick(snapshot, 0.1f);
            Assert.That(mapper.CurrentCommand.HeadShakePulseActive, Is.False);
            Assert.That(mapper.CurrentCommand.HeadShakeOverlayNormalized, Is.Zero);

            FishingResistanceCommand repeated = mapper.Tick(snapshot, 0.1f);
            Assert.That(repeated.HeadShakePulseActive, Is.False);
            Assert.That(repeated.HeadShakeOverlayNormalized, Is.Zero);
        }

        [Test]
        public void HeadShakeSequence_DecreaseAndZeroResetDoNotPulse_ButNewIncreaseDoes()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot snapshot = CreateSnapshot();
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 3);
            mapper.ResetToSafe(snapshot);

            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 2);
            Assert.That(mapper.Tick(snapshot, 0.02f).HeadShakePulseActive, Is.False);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 0);
            Assert.That(mapper.Tick(snapshot, 0.02f).HeadShakePulseActive, Is.False);

            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 1);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeIntensityNormalized), 1f);
            Assert.That(mapper.Tick(snapshot, 0.02f).HeadShakePulseActive, Is.True);
        }

        [Test]
        public void HeadShakePulse_IsPositiveFastOverlayAndReturnsExactlyToZero()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot snapshot = CreateSnapshot();
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 1);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeIntensityNormalized), 0.8f);

            FishingResistanceCommand pulse = mapper.Tick(snapshot, 0.04f);
            Assert.That(pulse.HeadShakeOverlayNormalized, Is.GreaterThan(0f));
            Assert.That(pulse.ResistanceNormalized, Is.GreaterThan(pulse.BaseResistanceNormalized));
            for (int i = 0; i < 4; i++) mapper.Tick(snapshot, 0.1f);
            Assert.That(mapper.CurrentCommand.HeadShakeOverlayNormalized, Is.Zero);
            Assert.That(mapper.HeadShakePulseRemainingSeconds, Is.Zero);
        }

        [Test]
        public void InvalidFloatInputsAndDeltaTimes_AlwaysProduceFiniteNormalizedOutput()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot snapshot = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Run);
            Set(snapshot, nameof(FishingSnapshot.V2FishForceNormalized), float.NaN);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeIntensityNormalized), float.PositiveInfinity);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 1);

            AssertSafe(mapper.Tick(snapshot, float.NaN), mapper);
            AssertSafe(mapper.Tick(snapshot, float.PositiveInfinity), mapper);
            Set(snapshot, nameof(FishingSnapshot.V2FishForceNormalized), float.NegativeInfinity);
            AssertSafe(mapper.Tick(snapshot, 0.1f), mapper);
        }

        [Test]
        public void RiseAndFallSlew_AreDeterministic_AndNormalZeroUsesFallSlew()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot run = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Run, 1f);

            FishingResistanceCommand first = mapper.Tick(run, 0.1f);
            Assert.That(first.BaseResistanceNormalized, Is.EqualTo(0.18f).Within(0.0001f));
            for (int i = 0; i < 5; i++) mapper.Tick(run, 0.1f);
            float settledRun = mapper.CurrentCommand.BaseResistanceNormalized;

            Set(run, nameof(FishingSnapshot.V2BehaviorState), FishingV2BehaviorState.None);
            FishingResistanceCommand falling = mapper.Tick(run, 0.1f);
            Assert.That(falling.BaseResistanceNormalized, Is.LessThan(settledRun));
            Assert.That(falling.BaseResistanceNormalized, Is.GreaterThan(0f));

            for (int i = 0; i < 3; i++) mapper.Tick(run, 0.1f);
            Assert.That(mapper.CurrentCommand.BaseResistanceNormalized, Is.Zero);
        }

        [Test]
        public void NonFightingSafetyReset_BypassesSlewAndReturnsImmediateZero()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot snapshot = CreateSnapshot(FishingPlayerState.Fighting, FishingV2BehaviorState.Run, 1f);
            for (int i = 0; i < 6; i++) mapper.Tick(snapshot, 0.1f);
            Assert.That(mapper.CurrentCommand.ResistanceNormalized, Is.GreaterThan(0f));

            Set(snapshot, nameof(FishingSnapshot.State), FishingPlayerState.Caught);
            Assert.That(mapper.Tick(snapshot, 0.001f).ResistanceNormalized, Is.Zero);
            Assert.That(mapper.SmoothedBaseResistanceNormalized, Is.Zero);
        }

        [Test]
        public void DisconnectResetReconnectWithSameSequence_DoesNotReplayStalePulse()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot snapshot = CreateSnapshot();
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 8);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeIntensityNormalized), 1f);
            Assert.That(mapper.Tick(snapshot, 0.02f).HeadShakePulseActive, Is.True);

            mapper.ResetToSafe(snapshot);
            Assert.That(mapper.CurrentCommand.ResistanceNormalized, Is.Zero);
            Assert.That(mapper.HeadShakePulseRemainingSeconds, Is.Zero);

            FishingResistanceCommand resumed = mapper.Tick(snapshot, 0.02f);
            Assert.That(resumed.HeadShakePulseActive, Is.False);
            Assert.That(resumed.HeadShakeOverlayNormalized, Is.Zero);
        }

        [Test]
        public void ResetToSafe_AcceptsNullAndClearsEveryTemporalValue()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingSnapshot snapshot = CreateSnapshot();
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 2);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeIntensityNormalized), 1f);
            mapper.Tick(snapshot, 0.02f);

            Assert.DoesNotThrow(() => mapper.ResetToSafe(null));
            Assert.That(mapper.CurrentCommand.ResistanceNormalized, Is.Zero);
            Assert.That(mapper.SmoothedBaseResistanceNormalized, Is.Zero);
            Assert.That(mapper.HeadShakePulseRemainingSeconds, Is.Zero);
            Assert.That(mapper.LastHeadShakeSequence, Is.Zero);
        }

        [Test]
        public void MockOutput_SeparatesActiveZeroFromSafetyStop()
        {
            MockFishingResistanceOutput output = new MockFishingResistanceOutput();
            output.ApplyCommand(FishingResistanceCommand.Zero);
            Assert.That(output.LastCommand.ResistanceNormalized, Is.Zero);
            Assert.That(output.IsStopped, Is.False);

            output.ApplyCommand(new FishingResistanceCommand(0.8f));
            output.Stop();
            Assert.That(output.LastCommand.ResistanceNormalized, Is.Zero);
            Assert.That(output.IsStopped, Is.True);
        }

        [Test]
        public void FightingZeroCommand_UsesActiveApplyAndNotSafetyStop()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            MockFishingResistanceOutput output = new MockFishingResistanceOutput();
            FishingSnapshot snapshot = CreateSnapshot(
                FishingPlayerState.Fighting,
                FishingV2BehaviorState.Run,
                1f);
            for (int i = 0; i < 6; i++) mapper.Tick(snapshot, 0.1f);

            Set(snapshot, nameof(FishingSnapshot.V2BehaviorState), FishingV2BehaviorState.None);
            FishingResistanceCommand command = mapper.CurrentCommand;
            for (int i = 0; i < 3; i++) command = mapper.Tick(snapshot, 0.1f);
            Assert.That(snapshot.State, Is.EqualTo(FishingPlayerState.Fighting));
            Assert.That(command.ResistanceNormalized, Is.Zero);

            output.ApplyCommand(command);
            Assert.That(output.LastCommand.ResistanceNormalized, Is.Zero);
            Assert.That(output.IsStopped, Is.False);
        }

        [Test]
        public void StopThenResume_LeavesStopStateWithoutReplayingStaleHeadShake()
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            MockFishingResistanceOutput output = new MockFishingResistanceOutput();
            FishingSnapshot snapshot = CreateSnapshot();
            Set(snapshot, nameof(FishingSnapshot.HeadShakeEventSequence), 5);
            Set(snapshot, nameof(FishingSnapshot.HeadShakeIntensityNormalized), 1f);
            output.ApplyCommand(mapper.Tick(snapshot, 0.02f));
            Assert.That(output.LastCommand.HeadShakePulseActive, Is.True);

            mapper.ResetToSafe(snapshot);
            output.Stop();
            Assert.That(output.IsStopped, Is.True);

            FishingResistanceCommand resumed = mapper.Tick(snapshot, 0.02f);
            output.ApplyCommand(resumed);
            Assert.That(output.IsStopped, Is.False);
            Assert.That(output.LastCommand.HeadShakePulseActive, Is.False);
            Assert.That(output.LastCommand.HeadShakeOverlayNormalized, Is.Zero);
        }

        [Test]
        public void MockOutput_SanitizesInvalidCommands()
        {
            MockFishingResistanceOutput output = new MockFishingResistanceOutput();
            output.ApplyCommand(new FishingResistanceCommand(
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity));

            Assert.That(output.IsStopped, Is.False);
            Assert.That(output.LastCommand.ResistanceNormalized, Is.Zero);
            Assert.That(output.LastCommand.BaseResistanceNormalized, Is.Zero);
            Assert.That(output.LastCommand.HeadShakeOverlayNormalized, Is.Zero);
        }

        private static FishingResistanceCommand Settled(FishingV2BehaviorState behavior, float force)
        {
            return Settle(CreateSnapshot(FishingPlayerState.Fighting, behavior, force));
        }

        private static FishingResistanceCommand Settle(FishingSnapshot snapshot)
        {
            FishingV2ResistanceMapper mapper = new FishingV2ResistanceMapper();
            FishingResistanceCommand command = FishingResistanceCommand.Zero;
            for (int i = 0; i < 10; i++) command = mapper.Tick(snapshot, 0.1f);
            return command;
        }

        private static FishingSnapshot CreateSnapshot(
            FishingPlayerState state = FishingPlayerState.Fighting,
            FishingV2BehaviorState behavior = FishingV2BehaviorState.Fight,
            float force = 0.6f)
        {
            FishingSnapshot snapshot = new FishingSnapshot();
            Set(snapshot, nameof(FishingSnapshot.State), state);
            Set(snapshot, nameof(FishingSnapshot.V2BehaviorState), behavior);
            Set(snapshot, nameof(FishingSnapshot.V2FishForceNormalized), force);
            return snapshot;
        }

        private static void Set<T>(FishingSnapshot snapshot, string propertyName, T value)
        {
            PropertyInfo property = typeof(FishingSnapshot).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, propertyName);
            setter.Invoke(snapshot, new object[] { value });
        }

        private static void AssertSafe(FishingResistanceCommand command, FishingV2ResistanceMapper mapper)
        {
            AssertFiniteNormalized(command.ResistanceNormalized);
            AssertFiniteNormalized(command.BaseResistanceNormalized);
            AssertFiniteNormalized(command.HeadShakeOverlayNormalized);
            AssertFiniteNormalized(mapper.SmoothedBaseResistanceNormalized);
            Assert.That(float.IsNaN(mapper.HeadShakePulseRemainingSeconds) ||
                        float.IsInfinity(mapper.HeadShakePulseRemainingSeconds), Is.False);
        }

        private static void AssertFiniteNormalized(float value)
        {
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
            Assert.That(value, Is.InRange(0f, 1f));
        }
    }
}
