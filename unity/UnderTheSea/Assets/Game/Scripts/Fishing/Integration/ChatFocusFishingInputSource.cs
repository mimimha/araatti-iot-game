using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Keeps the reusable fishing input source independent from lobby UI while
    /// enforcing the shared gameplay-input lock at the integration boundary.
    /// </summary>
    public sealed class ChatFocusFishingInputSource : IFishingInputSource
    {
        private readonly IFishingInputSource _source;

        public ChatFocusFishingInputSource(IFishingInputSource source)
        {
            _source = source ?? throw new System.ArgumentNullException(nameof(source));
        }

        public bool IsConnected => _source.IsConnected;

        public FishingInputFrame ReadFrame()
        {
            FishingInputFrame frame = _source.ReadFrame();
            if (!ChatFocus.Typing)
            {
                return frame;
            }

            frame.HookPressed = false;
            frame.TimingPressed = false;
            return frame;
        }

        public void ResetState()
        {
            _source.ResetState();
        }
    }
}
