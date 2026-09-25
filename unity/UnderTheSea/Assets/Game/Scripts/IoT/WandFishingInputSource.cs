using FishingMiniGame.Core;
using UnityEngine;

/// <summary>
/// 완드 하나를 낚시 입력 프레임으로 옮긴다. <see cref="IotFishingBridge"/> 가 들고 쓴다.
///
/// **낚시가 실제로 읽는 값은 둘뿐입니다.**
///
///     FishingV3Runtime.cs:271   input.HookPressed    입질 창에서 챔질
///     FishingV3Runtime.cs:312   input.TimingPressed  파이팅 중 타이밍 릴링
///
/// 나머지(<c>CastPressed</c> · <c>ReelDelta</c> · <c>RodPitch</c> · <c>RodYaw</c>)는 V2 시절
/// 값이라 V3 경로에서는 아무도 안 봅니다. 로비는 V3 입니다 —
/// <c>FishingModeController</c> 가 <c>ReelControlMode.Timing</c> 으로 띄웁니다.
/// 그래서 채우지 않습니다. 없는 값을 지어내면 나중에 누가 그것을 진짜로 착각합니다.
///
/// <c>TensionNormalized</c> 만 0.5 로 둡니다. V3 는 장력을 릴 속도에서 스스로 계산하므로
/// 이 값을 안 보지만, 0 이 흘러들어가면 옛 V2 경로가 "줄이 늘어졌다" 로 읽습니다.
/// 낚시 쪽의 중립 프레임도 같은 0.5 입니다.
///
/// 키보드 쪽 짝은 <c>KeyboardFishingInputSource</c> 입니다. 거기는 J 하나가
/// <c>HookPressed</c> 와 <c>TimingPressed</c> 를 겸하는데, 여기도 똑같이 오른손 버튼 2
/// 하나가 둘을 겸합니다. 키보드로 확인한 것이 완드에서 그대로 됩니다.
/// </summary>
public sealed class WandFishingInputSource : IFishingInputSource
{
    private readonly IPlayerController _controller;
    private long _sequence;
    private bool _hookLatched;
    private bool _button2WasHeld;

    public WandFishingInputSource(IPlayerController controller)
    {
        _controller = controller;
    }

    public bool IsConnected => _controller != null;

    /// <summary>
    /// 오른손 버튼 2 가 새로 눌렸는지 본다. <b>매 프레임</b> 불러야 한다.
    ///
    /// ⚠ 장치의 <c>ConsumeButton2Press</c> 를 쓰지 않고 눌림 상태의 모서리를 직접 잡는다.
    ///   낚시를 안 하는 동안에는 낚시 쪽이 <c>ReadFrame</c> 을 부르지 않아서, 장치에
    ///   쌓인 "눌린 순간" 이 낚시를 시작하자마자 한꺼번에 쏟아진다. 입질도 안 왔는데
    ///   챔질이 되어 물고기를 놓친다. 그래서 상태를 여기서 들고, 낚시 중이 아니면 버린다.
    /// </summary>
    /// <param name="fishing">지금 낚시 중인지. 아니면 눌린 것을 버린다.</param>
    public void PollHook(bool fishing)
    {
        if (_controller == null) return;

        bool held = _controller.Right.Button2;
        bool pressedNow = held && !_button2WasHeld;
        _button2WasHeld = held;

        if (!fishing)
        {
            _hookLatched = false;
            return;
        }

        if (pressedNow) _hookLatched = true;
    }

    public FishingInputFrame ReadFrame()
    {
        bool hook = _hookLatched;
        _hookLatched = false;

        return new FishingInputFrame
        {
            ParticipantId = "local-player",
            Sequence = ++_sequence,
            TimestampSeconds = Time.unscaledTimeAsDouble,
            HookPressed = hook,
            TimingPressed = hook,
            TensionNormalized = 0.5f,
            IsDeviceConnected = _controller != null,
        };
    }

    public void ResetState()
    {
        _sequence = 0;
        _hookLatched = false;
        _button2WasHeld = false;
    }
}
