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
}
