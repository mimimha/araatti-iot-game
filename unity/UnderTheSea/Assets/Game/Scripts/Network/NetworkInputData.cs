using Fusion;
using UnityEngine;

/// <summary>
/// 매 tick 로컬 클라이언트가 Host로 보내는 입력 구조체.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    /// <summary>WASD 이동 입력. x = 좌우(A/D), y = 전후(S/W).</summary>
    public Vector2 Direction;

    /// <summary>
    /// 로컬 카메라가 바라보는 방향(Y축 각도, degree).
    ///
    /// 이동을 카메라 기준으로 돌리기 위해 함께 보낸다.
    /// 카메라는 각자 클라이언트에만 있고 서버에는 없으므로, 서버가 알 방법이 이것뿐이다.
    /// (PlayerCapsule 처럼 이 값을 쓰지 않는 기존 이동 코드는 그대로 동작한다)
    /// </summary>
    public float LookYaw;

    /// <summary>
    /// 누르고 있는 것들. <see cref="LobbyButton"/> 순서다.
    ///
    /// <b>"눌린 순간" 을 보내지 않고 "누르고 있는 상태" 를 보낸다.</b>
    /// 순간을 보내면 그 한 틱이 유실될 때 입력이 통째로 사라진다.
    /// 서버가 <c>GetPressed(직전)</c> 로 순간을 스스로 만들어 낸다.
    /// </summary>
    public NetworkButtons Buttons;
}

/// <summary>
/// <see cref="NetworkInputData.Buttons"/> 의 비트 자리.
///
/// ⚠ <b>순서를 바꾸지 않는다.</b> 서버와 클라이언트가 같은 자리를 봐야 한다.
///    가운데에 끼워 넣지 말고 뒤에 붙인다. 순서가 어긋나면 버전이 다른 빌드끼리
///    붙었을 때 조용히 엉뚱한 버튼이 눌린다.
/// </summary>
public enum LobbyButton
{
    /// <summary>점프. 키보드는 Space. (IOT_INPUT.md 1장 — 네 게임 공통 표)</summary>
    Jump = 0,

    /// <summary>달리기. 키보드는 Shift. 누르고 있는 동안 빨라진다.</summary>
    Sprint = 1,
}
