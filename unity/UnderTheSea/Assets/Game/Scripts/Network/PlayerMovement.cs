using Fusion;
using UnityEngine;

/// <summary>
/// PlayerCapsule 이동. Host(State Authority)만 위치를 확정하고,
/// Client는 NetworkTransform으로 결과를 받는다.
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    public override void FixedUpdateNetwork()
    {
        // Client는 transform을 직접 건드리지 않는다.
        if (!HasStateAuthority)
            return;

        // 이 오브젝트의 InputAuthority가 보낸 입력만 가져온다.
        if (!GetInput(out NetworkInputData input))
            return;

        Vector3 direction = new Vector3(input.Direction.x, 0f, input.Direction.y);

        // 대각선 이동이 빨라지지 않게 한다.
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        transform.position += direction * (moveSpeed * Runner.DeltaTime);
    }
}
