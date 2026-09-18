using UnityEngine;

/// <summary>
/// 기기가 주는 값을 **배 협동 게임의 행동**으로 해석하는 곳.
///
/// 여기가 있는 이유
///   `IPlayerController` 는 무쌍 · 광산과 함께 쓰는 공용 경계라서
///   "조타", "발사" 같은 우리 게임 사정을 넣을 수 없습니다.
///   그 해석을 이 파일 한 곳에 모아둡니다.
///
///     IHandDevice        기기가 주는 것 (스틱, IMU, 압력, 버튼)
///          ↑
///     IPlayerController  왼손 + 오른손
///          ↑
///     ShipCoopInput      ← 여기. "조타는 양손 기울기 평균"
///
/// 버튼을 어디에 넣을지 바꾸고 싶으면 **이 파일만 고치면 됩니다.**
/// 작업 스크립트들은 손대지 않아도 됩니다.
/// </summary>
public static class ShipCoopInput
{
    // ------------------------------------------------------------
    // 축 — 계속 조절하는 것
    // ------------------------------------------------------------

    /// <summary>
    /// 🛞 조타. -1(좌) ~ +1(우)
    ///
    /// 조타륜을 잡고 돌리면 **두 손이 같은 방향으로 함께 기웁니다.**
    /// 그래서 양손 기울기의 평균으로 읽습니다. 한 손만 기울여도 절반은 돕니다.
    ///
    /// (양손의 "높이 차이"로는 못 읽습니다. IMU 는 자세를 알려줄 뿐
    ///  손이 얼마나 높이 있는지는 알려주지 않습니다.)
    /// </summary>
    public static float Steer(IPlayerController controller)
    {
        if (controller == null)
        {
            return 0f;
        }

        if (!controller.HasTwoDevices)
        {
            return controller.Left.Tilt;
        }

        return (controller.Left.Tilt + controller.Right.Tilt) * 0.5f;
    }

    /// <summary>
    /// ⛵ 돛 장력. -1 ~ +1
    /// 밧줄을 감듯 손목을 비트는 동작이라 회전(yaw)으로 읽습니다.
    /// </summary>
    public static float SailPull(IPlayerController controller)
    {
        if (controller == null)
        {
            return 0f;
        }

        if (!controller.HasTwoDevices)
        {
            return controller.Left.Rotation;
        }

        return (controller.Left.Rotation + controller.Right.Rotation) * 0.5f;
    }

    // ------------------------------------------------------------
    // 달리기
    // ------------------------------------------------------------

    /// <summary>
    /// 🏃 달리기. 켜져 있는 동안 빨라진다. 왼손 버튼 2. 키보드는 Shift.
    ///
    /// **켜고 끄는 것은 장치가 들고 있습니다.** 엄지는 스틱과 면버튼 중 하나만 잡을 수 있어,
    /// 누르고 있는 방식으로는 달리면서 걸을 수가 없습니다. 그래서 한 번 눌러 켜고 다시 눌러 끕니다.
    /// 여기는 켜져 있는지만 보면 되므로 토글인 것을 몰라도 됩니다. (IOT_INPUT.md 2장)
    ///
    /// ⚠ **물건을 들고 있으면 달릴 수 없습니다.** (DebugPlayerMover 가 막습니다)
    /// 양손으로 포탄을 안고 뛸 수는 없고, 그래야 운반이 진짜 대가를 치릅니다. (4장)
    ///
    /// <code>
    /// 빈손     →  달린다   →  빠르다
    /// 들고 감  →  못 달린다 →  느리다
    /// </code>
    /// </summary>
    public static bool Sprint(IPlayerController controller)
    {
        if (controller == null)
        {
            return false;
        }

        // 기기를 1대만 들면 왼손이 곧 오른손이라, 발사 버튼과 겹친다.
        // 그 경우에는 달리기를 빼고 늘 걷는다. 겹쳐서 오발하는 것보다 낫다.
        return controller.HasTwoDevices && controller.Left.Button2;
    }

    // ------------------------------------------------------------
    // 버튼 — 한 번 누르는 것
    //
    // ⚠ Consume 계열은 부른 쪽이 값을 가져가면서 지웁니다.
    //    같은 버튼을 한 프레임에 두 곳에서 부르면 한쪽이 놓칩니다.
    // ------------------------------------------------------------

    /// <summary>
    /// 자리에 붙기 · 집기 · 놓기 · 장전. 오른손 버튼 1. 키보드는 Space.
    ///
    /// **버튼 하나가 다 합니다.** 빈손이고 손 닿는 곳에 집을 것이 있으면 집고,
    /// 들고 있으면 놓거나 넘기고, 그 밖에는 가까운 자리에 붙습니다.
    /// 무엇이 될지는 상황이 정하므로 부르는 쪽은 한 곳이어야 합니다.
    /// </summary>
    public static bool ConsumeInteract(IPlayerController controller)
    {
        return controller != null && controller.Right.ConsumeButton1Press();
    }

    /// <summary>
    /// 그 버튼을 **지금 누르고 있는가.** 누르는 순간이 아니라 눌린 채로 있는 동안 참이다.
    ///
    /// 짐(포탄 · 수리 자재 · 물)을 드는 데 씁니다. 집을 때는 <see cref="ConsumeInteract"/> 로
    /// 순간을 잡고, 드는 동안은 이걸로 계속 확인해서 손을 떼면 놓게 합니다.
    ///
    /// ⚠ Consume 계열과 달리 **값을 지우지 않습니다.** 여러 곳에서 물어봐도 안전합니다.
    ///
    /// 네트워크에서도 그대로 옵니다. 눌린 상태가 `ShipCoopInputData` 의
    /// `RightButton1` 비트로 실려 오기 때문입니다.
    /// </summary>
    public static bool IsInteractHeld(IPlayerController controller)
    {
        return controller != null && controller.Right.Button1;
    }

    /// <summary>💥 대포 발사 · 망치질. 오른손 버튼 2. 키보드는 K.</summary>
    public static bool ConsumeFire(IPlayerController controller)
    {
        return controller != null && controller.Right.ConsumeButton2Press();
    }

    /// <summary>
    /// 🆘 도움 요청. 왼손 버튼 1.
    ///
    /// ⚠ 기기를 1대만 들면 이 기능을 쓸 수 없습니다.
    ///    버튼 2개가 이미 상호작용과 발사에 쓰이기 때문입니다.
    ///    **이 게임이 기기 2대를 권장하는 이유가 이것입니다.**
    /// </summary>
    public static bool ConsumeHelpCall(IPlayerController controller)
    {
        if (controller == null || !controller.HasTwoDevices)
        {
            return false;
        }

        return controller.Left.ConsumeButton1Press();
    }

    /// <summary>
    /// 🔨 망치질. 오른손 버튼 2, 또는 오른손을 내리치는 동작. 키보드는 K.
    ///
    /// **발사와 같은 버튼입니다.** 대포에 붙어 있으면서 동시에 파손 지점에 있을 수는 없어
    /// 서로 다투지 않습니다. 부르는 쪽이 이미 자리로 갈려 있습니다.
    ///
    /// IMU 가 휘두름을 잡아 주면 그쪽으로도 됩니다. 없어도 버튼으로 다 됩니다.
    ///
    /// 동작 판정은 <see cref="IHandDevice.TryConsumeMotion"/> 이 합니다. 어느 손인지로
    /// 추측하지 않고 센서가 내놓은 종류를 그대로 봅니다. 배가 쓰는 것은 **내리치기**뿐입니다.
    /// </summary>
    public static bool ConsumeSwing(IPlayerController controller)
    {
        if (controller == null)
        {
            return false;
        }

        // ⚠ 둘 다 읽는 순간 사라진다. `||` 의 단축 평가에 기대면 안 된다.
        //    버튼이 눌린 프레임에 휘두름이 남아 있으면 다음 프레임에 한 번 더 친다.
        bool swung = ConsumeSwingMotion(controller);
        bool pressed = controller.Right.ConsumeButton2Press();

        return swung || pressed;
    }

    /// <summary>
    /// 🔨 내리치기 **동작만.** 면버튼 2 는 보지 않는다.
    ///
    /// ⚠ 네트워크로 실어 보낼 때는 반드시 이쪽을 쓴다. (<c>ShipCoopInputProvider</c>)
    ///
    /// <see cref="ConsumeSwing"/> 을 실어 보내면 **면버튼 2 까지 휘두름으로 둔갑합니다.**
    /// 그러면 대포에서 K 로 쏠 때마다 서버의 휘두름 깃발이 함께 켜지는데, 대포 쪽은
    /// 그것을 가져가지 않아 그대로 쌓입니다. 나중에 파손 지점에 붙는 순간 쌓여 있던 것이
    /// 공짜 망치질로 터집니다.
    ///
    /// 면버튼은 눌림 상태로 따로 가고, **누른 순간은 서버가 직접 계산합니다.**
    /// 그래서 여기서 같이 보낼 이유가 없습니다.
    /// </summary>
    public static bool ConsumeSwingMotion(IPlayerController controller)
    {
        return controller != null
               && controller.Right.TryConsumeMotion(out HandMotion motion)
               && motion.Type == HandMotionType.VerticalSwing;
    }

    // ------------------------------------------------------------
    // 진동
    // ------------------------------------------------------------

    /// <summary>
    /// 어느 쪽에서 온 충격인지 손으로 알려준다.
    /// 좌현에 포격이 오면 왼손만 울린다. 화면을 안 봐도 어디가 위험한지 안다.
    /// 기기가 1대면 그냥 울린다.
    /// </summary>
    public static void VibrateSide(IPlayerController controller, bool leftSide, float strength, float seconds)
    {
        if (controller == null)
        {
            return;
        }

        if (!controller.HasTwoDevices)
        {
            controller.Left.Vibrate(strength, seconds);
            return;
        }

        IHandDevice hand = leftSide ? controller.Left : controller.Right;
        hand.Vibrate(strength, seconds);
    }
}
