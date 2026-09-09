using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    public sealed class MockFishingInputSource : IFishingInputSource
    {
        private FishingInputFrame _nextFrame;
        private long _sequence;

        public bool IsConnected { get; set; } = true;

        public void SetNextFrame(FishingInputFrame frame)
        {
            _nextFrame = frame;
        }

        public FishingInputFrame ReadFrame()
        {
            FishingInputFrame frame = _nextFrame;
            frame.Sequence = ++_sequence;
            frame.IsDeviceConnected = IsConnected;
            _nextFrame.CastPressed = false;
            _nextFrame.CastReleased = false;
            _nextFrame.HookPressed = false;
            return frame;
        }

        public void ResetState()
        {
            _sequence = 0;
            _nextFrame = new FishingInputFrame
            {
                TensionNormalized = 0.5f,
                IsDeviceConnected = true
            };
        }
    }
}
