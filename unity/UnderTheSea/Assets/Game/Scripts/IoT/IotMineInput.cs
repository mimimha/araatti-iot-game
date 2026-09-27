using Fusion;
using Mine.Net;
using UnityEngine;

/// <summary>
/// 완드로 광산의 <b>네트워크</b> 판을 하게 만드는 다리.
///
/// **광산 폴더에 한 줄만 들어오면 됩니다.** <c>MineInputProvider.OnInput</c> 의
/// <c>input.Set(data)</c> 바로 앞입니다.
///
/// <code>
///     IotMineInput.Fill(ref data);        // ← 이 한 줄
///     input.Set(data);
/// </code>
///
/// 그때까지는 이 파일이 아무 일도 하지 않습니다. 아무도 <see cref="Fill"/> 를 안 부르면
/// 조용히 있고 키보드만으로 정상 동작합니다. (무쌍의 <c>WarriorsIoTSetup</c> 이
/// *"가짜 장치를 만들지 않는다. 자리만 만든다"* 고 적은 것과 같은 생각입니다)
///
/// <b>왜 무쌍처럼 못 했는가.</b> 무쌍에는 <c>WarriorsIoTInput.OnSwing</c> 이라는
/// **공개 입구가 이미 있어서** IoT 가 부르기만 하면 됐습니다(<see cref="IotWarriorsBridge"/>).
/// 광산은 <c>MineInputProvider</c> 가 <c>Keyboard.current</c> 를 직접 읽고 그 자리에서
/// <c>data</c> 를 채워 <c>input.Set</c> 으로 넘겨 버려서, **바깥에서 끼어들 자리가 없습니다.**
/// 그래서 채울 함수만 여기 만들어 두고 부르는 한 줄을 광산 담당에게 부탁합니다.
///
/// ⚠ **채굴은 "사건" 이라 한 틱 물고 있습니다.** 키보드는 <c>Space</c> 를 누르는 동안 계속
///   <c>Swing</c> 을 참으로 싣지만(레벨), 완드의 세로 내리치기는 **한 순간** 입니다.
///   그대로 넘기면 <c>OnInput</c> 이 그 프레임에 안 불릴 때 통째로 사라집니다.
///   그래서 동작이 들어오면 걸어 두었다가 <b>다음 <see cref="Fill"/> 한 번</b>만 참으로 싣고 내립니다.
///
/// ⚠ **키보드를 덮어쓰지 않고 더합니다.** 둘 중 하나라도 참이면 참입니다. 키보드로 확인하던
///   사람이 완드를 꽂아도 그대로 됩니다.
///
/// ⚠ **완드가 실제로 붙어 있을 때만 더합니다.** <c>IotPlayerController.IsWandLive</c> 로 봅니다.
///   안 그러면 키보드 폴백 값이 광산의 키보드 읽기와 겹쳐 **한 번 누른 것이 두 번** 들어갑니다.
///   로비에서 실제로 났던 문제입니다. (7-7 2번)
///
/// 서버 빌드에서는 컨트롤러가 <c>null</c> 이라 아무것도 하지 않습니다.
/// </summary>
public static class IotMineInput
{
    /// <summary>
    /// 세로 내리치기가 들어왔다. 다음 <see cref="Fill"/> 한 번이 가져간다.
    ///
    /// 두 번 연달아 들어오면 하나로 합쳐집니다. <c>OnInput</c> 은 틱마다 불리고
    /// 완드 쿨다운은 250ms 라, 한 틱 안에 두 번 들어올 일은 사실상 없습니다.
    /// </summary>
    private static bool digLatched;

    /// <summary>
    /// 광산의 입력 묶음에 완드 값을 더한다. <c>input.Set(data)</c> 바로 앞에서 부른다.
    /// </summary>
    public static void Fill(ref MineInputData data)
    {
        IotPlayerController controller = IotPlayerController.Persistent;

        // 광산 배치로 바꾼다. 로비 · 배의 Shared 는 왼손 버튼 2 를 달리기 토글로 잠그는데,
        // 광산에서 그 자리는 힌트라 토글 상태가 "힌트를 누르고 있음" 으로 서버에 간다.
        // 로비로 돌아가면 IotLobbyInstaller 가 Shared 로 되돌린다.
        if (controller != null && controller.ControlProfile != IotControlProfile.Mine)
        {
            controller.SetControlProfile(IotControlProfile.Mine);
        }

        // 완드가 안 붙어 있으면 아무것도 안 한다. 키보드 폴백 값을 더하면 두 번 들어간다.
        if (controller == null || !IotPlayerController.IsWandLive(controller))
        {
            digLatched = false;
            return;
        }

        PollMotions(controller);

        // 걸린 채굴을 이번 한 틱만 싣는다.
        if (digLatched)
        {
            digLatched = false;
            data.Buttons.Set((int)MineButton.Swing, true);
        }

        IHandDevice left = controller.Left;
        IHandDevice right = controller.Right;

        // 스틱은 더해서 길이만 자른다. 키보드로 걷던 사람이 완드를 들어도 이어진다.
        Vector2 move = data.Move + controller.Move;
        data.Move = Vector2.ClampMagnitude(move, 1f);

        // 버튼은 OR. 배치는 IOT_INPUT.md 3장 광산 표를 따른다.
        //   왼손 버튼 1  땅 복구
        //   왼손 버튼 2  힌트
        //   오른손 버튼 2  달리기 (배와 달리 토글이 아니라 누르고 있기)
        Or(ref data.Buttons, MineButton.Restore, left != null && left.Button1);
        Or(ref data.Buttons, MineButton.Hint, left != null && left.Button2);
        Or(ref data.Buttons, MineButton.Run, right != null && right.Button2);
    }

    /// <summary>
    /// 양손의 대기 중인 동작을 꺼내 세로 내리치기만 걸어 둔다.
    ///
    /// **양손을 다 훑어야 합니다.** 어느 손으로 곡괭이를 쥐든 파여야 합니다.
    /// 그리고 동작은 읽으면 사라지므로, 안 꺼내면 세로가 아닌 동작이 대기열에 쌓여
    /// 진짜 내리치기를 막습니다. (<c>MineDigger</c> 가 <c>|</c> 로 양손을 다 부르는 것과 같은 이유)
    /// </summary>
    private static void PollMotions(IPlayerController controller)
    {
        Poll(controller.Left);

        // 기기가 1대면 Left 와 Right 가 같은 객체라 두 번째는 빈손으로 돌아온다.
        Poll(controller.Right);
    }

    private static void Poll(IHandDevice hand)
    {
        if (hand == null)
        {
            return;
        }

        while (hand.TryConsumeMotion(out HandMotion motion))
        {
            if (motion.Type == HandMotionType.VerticalSwing)
            {
                digLatched = true;
            }
        }
    }

    private static void Or(ref NetworkButtons buttons, MineButton button, bool value)
    {
        if (value)
        {
            buttons.Set((int)button, true);
        }
    }
}
