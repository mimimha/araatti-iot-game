using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **네 게임이 함께 쓰는 마우스 시점 규격.** (IOT_INPUT.md 1장 "카메라는 드래그입니다")
///
/// 오른쪽 버튼을 누르고 있는 동안에만 화면이 돈다. 커서를 가두지 않으므로 그냥 움직일 때는
/// 커서가 자유롭게 UI 를 누른다. **좌클릭은 채팅창 · 버튼 같은 화면 요소가 써야 한다.**
///
/// <b>왜 한곳에 두는가.</b> 게임마다 따로 계산하면 같은 거리를 끌어도 화면이 도는 양이 달라진다.
/// 로비에서 무쌍으로 넘어가는 순간 손이 배운 감각이 어긋나고, 그것은 "느리다 · 빠르다" 가 아니라
/// **조작이 고장 난 느낌**으로 온다. 숫자를 여기 한 곳에만 두면 그런 일이 없다.
///
/// <b>기준은 로비다.</b> 이미 우클릭 드래그로 옮겨져 있고(<c>LocalPlayerView</c>), 모두가 가장
/// 오래 머무는 공간이라 손에 익은 감각이 거기서 만들어진다. 로비의 계산은 이렇다.
///
/// <code>
///   Input.GetAxis("Mouse X")        레거시 축. 원시 픽셀 × 0.1   (InputManager sensitivity 0.1)
///   PlayerCamera.SetInput           각도 += 그 값 × 0.01 × 360   (Lobby 씬의 m_SensitivityX)
///   합치면                           0.36도 / 픽셀
/// </code>
///
/// ⚠ <b>레거시 축이 아니라 새 Input System 으로 읽는다.</b> 레거시 축은 같은 프로젝트 안에서도
///    빌드에 따라 조용히 0 이 나오는 일이 있다(<c>MineCamera</c> 가 같은 이유로 옮겼다).
///    원시 픽셀에 <see cref="DegreesPerPixel"/> 을 곱하면 위 식과 같은 값이 나온다.
///
/// ⚠ <b>프레임 시간을 곱하지 않는다.</b> 마우스가 주는 것은 속도가 아니라 이미 지나간 거리다.
///    여기에 <c>deltaTime</c> 을 또 곱하면 프레임이 낮은 기기에서 화면이 덜 돈다.
///
/// ⚠ <b>값을 자르지 않는다.</b> −1 ~ 1 로 자르면 빠르게 휙 돌릴 때 위쪽이 잘려 나가
///    "손목을 크게 돌렸는데 화면이 덜 돈다" 가 된다. 조이스틱과 달리 마우스에는 상한이 없다.
/// </summary>
public static class CameraDragLook
{
    /// <summary>
    /// 픽셀 하나당 화면이 도는 각도. 로비에서 실측으로 맞춰진 값이다.
    ///
    /// 바꾸려면 <c>IOT_INPUT.md</c> 를 먼저 고치세요. 네 미니게임이 함께 지키는 값입니다.
    /// </summary>
    public const float DegreesPerPixel = 0.36f;

    /// <summary>지금 시점을 돌리는 중인가. 드래그 중에만 참이다.</summary>
    public static bool IsDragging
    {
        get
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }
    }

    /// <summary>
    /// 이번 프레임에 화면을 돌릴 각도. <b>도 단위</b>이고, 드래그 중이 아니면 0 이다.
    ///
    /// <c>x</c> 는 좌우(yaw), <c>y</c> 는 위아래(pitch)다. 화면 좌표의 y 는 위로 갈수록
    /// 커지므로 그대로 쓰면 "위로 끌면 위를 본다" 가 된다. 아래를 보게 하려면 부르는 쪽에서
    /// 부호를 뒤집는다 — 게임마다 기대가 다르고, 뒤집는 자리는 카메라가 정해야 한다.
    ///
    /// ⚠ 마우스가 없으면(데디케이티드 서버 · 창 없는 실행) 0 이다. 예외를 내지 않는다.
    /// </summary>
    public static Vector2 ReadDegrees()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.rightButton.isPressed)
        {
            return Vector2.zero;
        }

        return mouse.delta.ReadValue() * DegreesPerPixel;
    }
}
