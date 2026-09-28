using System;

namespace FishingMiniGame.Core
{
    public interface IFishingInputSource
    {
        bool IsConnected { get; }
        FishingInputFrame ReadFrame();
        void ResetState();
    }

    public interface IFishingFeedbackOutput
    {
        void ApplyFeedback(FishingFeedbackFrame frame);
        void StopFeedback();
    }

    public interface IFishingAuthority
    {
        FishingSnapshot Current { get; }
        FishingCycleResult LastResult { get; }
        event Action<FishingCycleResult> CycleFinished;
        void Initialize(FishingRoundContext context);
        void PrepareCycle(FishProfile fish, FishingRules rules = null);
        void Tick(FishingInputFrame input, float deltaTime);
        void ResetCycle();
    }
}
