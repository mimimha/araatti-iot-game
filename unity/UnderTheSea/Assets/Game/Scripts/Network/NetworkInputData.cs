using Fusion;
using UnityEngine;

/// <summary>
/// 매 tick 로컬 클라이언트가 Host로 보내는 입력 구조체.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    /// <summary>WASD 이동 입력. x = 좌우(A/D), y = 전후(S/W).</summary>
    public Vector2 Direction;
}
