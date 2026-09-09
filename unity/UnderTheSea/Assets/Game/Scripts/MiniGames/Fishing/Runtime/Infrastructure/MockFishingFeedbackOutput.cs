using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    public sealed class MockFishingFeedbackOutput : IFishingFeedbackOutput
    {
        public FishingFeedbackFrame LastFrame { get; private set; }
        public event Action<FishingFeedbackFrame> FeedbackChanged;

        public void ApplyFeedback(FishingFeedbackFrame frame)
        {
            frame.Intensity = FishingMath.Clamp01(frame.Intensity);
            bool changed = frame.State != LastFrame.State || Math.Abs(frame.Intensity - LastFrame.Intensity) > 0.01f;
            LastFrame = frame;
            if (changed) FeedbackChanged?.Invoke(frame);
        }

        public void StopFeedback()
        {
            ApplyFeedback(new FishingFeedbackFrame(FishingFeedbackState.None, 0f));
        }
    }
}
