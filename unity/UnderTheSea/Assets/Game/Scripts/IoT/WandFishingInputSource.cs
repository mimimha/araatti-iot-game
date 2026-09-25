using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using UnityEngine;

/// <summary>
/// 키보드 낚시 입력에 완드를 더한다. <see cref="IotFishingBridge"/> 가 들고 쓴다.
///
/// **키보드를 대체하지 않고 더합니다.** 낚시에는 입력원이 하나만 꽂히므로, 완드 입력원을
/// 그냥 꽂으면 팀원의 <c>KeyboardFishingInputSource</c> 가 빠져 J 가 죽습니다. 그래서 그것을
/// 안에 품고 그 프레임을 바탕으로 씁니다. 로비의 이동 · 카메라가 키보드에 완드를 더하는 것과
/// 같은 방식입니다. (KEY_MAPPING.md 로비)
///
/// **완드가 채우는 값은 둘뿐입니다.**
///
///     FishingV3Runtime.cs:271   input.HookPressed    입질 창에서 챔질
///     FishingV3Runtime.cs:312   input.TimingPressed  파이팅 중 타이밍 릴링
///
/// 나머지(<c>CastPressed</c> · <c>ReelDelta</c> · <c>RodPitch</c> · <c>RodYaw</c>)는 V2 시절
/// 값이라 V3 경로에서는 아무도 안 봅니다. 로비는 V3 입니다 —
/// <c>FishingModeController</c> 가 <c>ReelControlMode.Timing</c> 으로 띄웁니다.
/// 그 값들은 키보드 프레임에 있던 그대로 둡니다. 없는 값을 지어내면 나중에 누가 그것을
/// 진짜로 착각합니다.
///
/// 키보드는 J 하나가 <c>HookPressed</c> 와 <c>TimingPressed</c> 를 겸하는데, 여기도 똑같이
/// 오른손 버튼 2 하나가 둘을 겸합니다. 키보드로 확인한 것이 완드에서 그대로 됩니다.
/// </summary>
public sealed class WandFishingInputSource : IFishingInputSource
{
    private readonly IPlayerController _controller;
    private readonly IFishingInputSource _keyboard;
    private bool _hookLatched;
    private bool _button2WasHeld;

    /// <param name="controller">완드.</param>
    /// <param name="keyboard">바탕이 되는 키보드 입력원. 이 프레임에 완드 챔질을 더한다.</param>
    public WandFishingInputSource(IPlayerController controller, IFishingInputSource keyboard)
    {
        _controller = controller;
        _keyboard = keyboard;
    }

    public bool IsConnected => _keyboard.IsConnected;

    /// <summary>
    /// 오른손 버튼 2 가 새로 눌렸는지 본다. <b>매 프레임</b> 불러야 한다.
    ///
    /// ⚠ 장치의 <c>ConsumeButton2Press</c> 를 쓰지 않고 눌림 상태의 모서리를 직접 잡는다.
    ///   낚시를 안 하는 동안에는 낚시 쪽이 <c>ReadFrame</c> 을 부르지 않아서, 장치에
    ///   쌓인 "눌린 순간" 이 낚시를 시작하자마자 한꺼번에 쏟아진다. 입질도 안 왔는데
    ///   챔질이 되어 물고기를 놓친다. 그래서 상태를 여기서 들고, 낚시 중이 아니면 버린다.
    ///
    /// ⚠ 완드가 안 붙어 있으면 읽지 않는다. 그때 <c>IotPlayerController</c> 는 오른손 버튼 2 를
    ///   K 로 대신 채워서, 읽으면 J 에 더해 K 로도 챔질이 된다.
    /// </summary>
    /// <param name="fishing">지금 낚시 중인지. 아니면 눌린 것을 버린다.</param>
    public void PollHook(bool fishing)
    {
        bool held = IotPlayerController.IsWandLive(_controller) && _controller.Right.Button2;
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
        FishingInputFrame frame = _keyboard.ReadFrame();

        if (_hookLatched)
        {
            _hookLatched = false;
            frame.HookPressed = true;
            frame.TimingPressed = true;
        }

        return frame;
    }

    public void ResetState()
    {
        _keyboard.ResetState();
        _hookLatched = false;
        _button2WasHeld = false;
    }
}
