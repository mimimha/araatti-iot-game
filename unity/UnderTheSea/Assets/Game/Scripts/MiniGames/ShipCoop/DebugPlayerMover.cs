using UnityEngine;

/// <summary>
/// 큐브를 움직이는 임시 이동 스크립트.
///
/// ⚠ 임시 구현입니다. 진짜 캐릭터 이동(Scripts/Character/)이 준비되면 지웁니다.
///
/// 있는 이유
///   작업 자리에 붙고, 걸어나가서 떨어지는 것을 확인하려면 움직일 수 있어야 합니다.
///   진짜 캐릭터를 기다리지 않고 큐브로 먼저 확인하기 위한 것입니다.
///
/// 키보드를 직접 읽지 않고 IPlayerController.Move 를 씁니다.
/// 이동은 컨트롤러의 **왼쪽 조이스틱**이 담당하므로, 장치가 붙으면
/// 이 스크립트를 고치지 않아도 스틱으로 움직입니다.
///
/// 사용법
///   1. Player 큐브에 IPlayerController 구현체와 TaskWorker 를 붙인다.
///   2. 같은 오브젝트에 이 스크립트를 붙인다.
///   3. 끝. 월드 기준으로 움직인다. (카메라 기준 이동은 진짜 캐릭터 쪽에서 처리)
/// </summary>
public class DebugPlayerMover : MonoBehaviour
{
    [Header("이동 속도 (m/s)")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("작업 중에는 못 움직이게 할지")]
    [Tooltip("끄면 작업에 붙은 채로도 움직일 수 있다.\n" +
             "켜면 오버쿡처럼 자리에 고정된다. 다만 걸어나가서 떨어지는 것을 확인할 수 없다.")]
    [SerializeField] private bool lockWhileWorking = false;

    private IPlayerController _input;
    private TaskWorker _worker;

    private void Awake()
    {
        _input = GetComponent<IPlayerController>();
        _worker = GetComponent<TaskWorker>();

        if (_input == null)
        {
            Debug.LogError(
                $"[{name}] IPlayerController 를 찾을 수 없습니다. " +
                "KeyboardPlayerController 를 같은 오브젝트에 붙이세요.", this);
        }
    }

    private void Update()
    {
        if (_input == null)
        {
            return;
        }

        if (lockWhileWorking && _worker != null && _worker.Current != null)
        {
            return;
        }

        Vector2 move = _input.Move;
        if (move.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // 스틱의 y 는 앞뒤(월드 z)로 간다.
        Vector3 direction = new Vector3(move.x, 0f, move.y);
        transform.position += direction * (moveSpeed * Time.deltaTime);
    }
}
