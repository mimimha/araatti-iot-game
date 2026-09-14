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
    // 쥐기 — 압력센서
    // ------------------------------------------------------------

    /// <summary>💪 버티기. 한 손이라도 쥐고 있으면 된다. (돛 붙잡기, 파도 때 조타륜)</summary>
    public static bool Hold(IPlayerController controller)
    {
        return controller != null && (controller.Left.Grip || controller.Right.Grip);
    }

    // ------------------------------------------------------------
    // 달리기
    // ------------------------------------------------------------

    /// <summary>
    /// 🏃 달리기. 누르고 있는 동안 빨라진다. 왼손 버튼 2. 키보드는 V.
    ///
    /// **새 부품을 쓰지 않습니다.** 7장 표에서 왼손 버튼 2 는 "(여유)" 로 비어 있었고,
    /// 갑판이 3층으로 넓어지면서 그 자리가 채워졌습니다. 조작 개수는 그대로입니다.
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

    /// <summary>
    /// ⚫ 양손으로 들기. 무거운 포탄을 나를 때.
    /// 한 손이라도 놓으면 떨어뜨립니다. 그래서 나르는 동안 다른 일을 못 합니다.
    /// 기기를 1대만 들었으면 한 손으로도 인정합니다.
    /// </summary>
    public static bool HoldBoth(IPlayerController controller)
    {
        if (controller == null)
        {
            return false;
        }

        if (!controller.HasTwoDevices)
        {
            return controller.Left.Grip;
        }

        return controller.Left.Grip && controller.Right.Grip;
    }

    // ------------------------------------------------------------
    // 버튼 — 한 번 누르는 것
    //
    // ⚠ Consume 계열은 부른 쪽이 값을 가져가면서 지웁니다.
    //    같은 버튼을 한 프레임에 두 곳에서 부르면 한쪽이 놓칩니다.
    // ------------------------------------------------------------

    /// <summary>자리에 붙기 · 포탄 집기 / 놓기. 오른손 버튼 1.</summary>
    public static bool ConsumeInteract(IPlayerController controller)
    {
        return controller != null && controller.Right.ConsumeButton1Press();
    }

    /// <summary>💥 대포 발사. 오른손 버튼 2.</summary>
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

    /// <summary>🔨 망치질. 오른손을 내리치는 동작.</summary>
    public static bool ConsumeSwing(IPlayerController controller)
    {
        return controller != null && controller.Right.ConsumeSwing();
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
