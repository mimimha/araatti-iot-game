using System;

namespace FishingMiniGame.Runtime
{
    public sealed class MockFishingResistanceOutput : IFishingResistanceOutput
    {
        public FishingResistanceCommand LastCommand { get; private set; } = FishingResistanceCommand.Zero;
        public bool IsStopped { get; private set; } = true;
        public event Action<FishingResistanceCommand, bool> CommandChanged;

        public void ApplyCommand(FishingResistanceCommand command)
        {
            LastCommand = new FishingResistanceCommand(
                command.ResistanceNormalized,
                command.BaseResistanceNormalized,
                command.HeadShakeOverlayNormalized,
                command.HeadShakePulseActive,
                command.SourceBehavior,
                command.IsFinalRun);
            IsStopped = false;
            CommandChanged?.Invoke(LastCommand, IsStopped);
        }

        public void Stop()
        {
            LastCommand = FishingResistanceCommand.Zero;
            IsStopped = true;
            CommandChanged?.Invoke(LastCommand, IsStopped);
        }
    }
}
