using UnityEngine;

/// <summary>
/// 갑판 위를 걸어다니는 이동. (SHIPCOOP.md 4장)
///
/// ⚠ 임시 구현입니다. 네트워크가 붙으면 이동은 서버 쪽(`PlayerMovement`)이 맡습니다. (11장)
///    다만 **여기서 정한 숫자(걷기 · 달리기 속도, 계단 높이)는 그대로 넘어갑니다.**
///    밸런싱이 이 숫자에 걸려 있으므로, 옮길 때 값을 같이 가져가야 합니다.
///
/// 왜 CharacterController 인가
///   갑판이 3층이 되면서 **계단을 오르내려야** 합니다. (4장 · 6장)
///   예전처럼 transform.position 에 더하기만 하면 계단을 뚫고 지나가거나 허공에 섭니다.
///   CharacterController 는 계단(stepOffset)과 경사(slopeLimit)를 알아서 처리하고,
///   나중에 Fusion 의 NetworkCharacterController 로 거의 그대로 옮겨집니다.
///
/// 키보드를 직접 읽지 않고 IPlayerController 를 씁니다.
/// 이동은 **왼손 스틱**, 달리기는 **왼손 버튼 2**(키보드 V)입니다. (7장)
///
/// 사용법
///   1. Player 에 IPlayerController 구현체와 TaskWorker 를 붙인다.
///   2. 같은 오브젝트에 이 스크립트를 붙인다. CharacterController 는 자동으로 붙는다.
///   3. 갑판에 콜라이더가 있어야 한다. 없으면 바닥을 뚫고 떨어진다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DebugPlayerMover : MonoBehaviour
{
    // ------------------------------------------------------------
    // 속도를 올릴 때의 한계 (4장 원칙 3 — 일 하나에 10~15초)
    //
    //   4.0 / 7.0   달려가기 3.1초 + 들고 걷기 5.5초 + 작업 3초 = 11.6초   목표 정중앙
    //   5.0 / 8.5   달려가기 2.6초 + 들고 걷기 4.4초 + 작업 3초 = 10.0초   목표 아래 끝
    //
    // **여기가 한계입니다.** 더 올리면 일이 너무 빨리 끝나서 4명이 멀뚱히 서 있게 됩니다.
    // 그때는 속도가 아니라 사건 간격을 좁혀야 합니다.
    //
    // ⚠ 느리게 느껴지면 속도보다 **카메라를 먼저** 보세요. 체감 속도는
    //    실제 속도 ÷ 화면에서 차지하는 크기라, 카메라를 당기면 밸런스를
    //    건드리지 않고도 빨라 보입니다.
    //
    // 걷기 5.0 은 서버 쪽 PlayerMovement 의 기본값과 같습니다.
    // 네트워크로 옮길 때 값이 싸우지 않게 맞춰 두었습니다. (11장)
    // ------------------------------------------------------------

    [Header("속도 (m/s)")]
    [Tooltip("걷는 속도. 물건을 들고 있을 때도 이 속도다.")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 4f;

    [Tooltip("달릴 때 속도. 왼손 버튼 2(키보드 V)를 누르고 있는 동안.\n\n" +
             "걷기의 1.7배로 둔다. 이 비율이 줄면 '들면 못 달린다' 는 제약이 약해진다.\n" +
             "둘 중 하나만 올리지 않는다.")]
    [SerializeField, Min(0.1f)] private float sprintSpeed = 7f;

    [Header("돌아보는 속도 (도/초)")]
    [Tooltip("가는 방향으로 몸을 돌린다. 0 이면 안 돌아본다.")]
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Header("계단과 경사")]
    [Tooltip("이 높이까지는 걸어 올라간다. 갑판 사이 계단 한 칸보다 크게 잡는다.")]
    [SerializeField, Min(0f)] private float stepHeight = 0.4f;

    [Tooltip("이 각도까지는 걸어 올라간다.\n\n" +
             "배의 뒷갑판 계단이 48.2° 다. 그보다 커야 올라간다.\n" +
             "낮추려면 계단 모델부터 바꿔야 한다.")]
    [SerializeField, Range(0f, 80f)] private float slopeLimit = 55f;

    [Header("중력")]
    [Tooltip("갑판에서 떨어지거나 계단을 내려올 때 쓴다. 음수.")]
    [SerializeField] private float gravity = -20f;

    [Header("몸 크기 (m)")]
    [Tooltip("CharacterController 가 없을 때 이 값으로 만들어 붙인다.")]
    [SerializeField, Min(0.1f)] private float bodyHeight = 1.6f;

    [SerializeField, Min(0.05f)] private float bodyRadius = 0.35f;

    [Header("화면 기준으로 움직일지")]
    [Tooltip("켜면 **보이는 대로** 움직인다. 위를 누르면 화면 위쪽(카메라에서 먼 쪽)으로 간다.\n\n" +
             "카메라는 오른손 스틱으로 좌우 100도까지 돈다. 끄면 월드 +z 로 고정이라,\n" +
             "카메라를 돌린 채 위를 누르면 캐릭터가 비스듬히 간다.\n" +
             "화면과 손이 어긋나면 우당탕탕하는 중에 엉뚱한 데로 뛴다.")]
    [SerializeField] private bool cameraRelative = true;

    [Header("작업 중에는 못 움직이게 할지")]
    [Tooltip("끄면 작업에 붙은 채로도 움직일 수 있다.\n" +
             "켜면 오버쿡처럼 자리에 고정된다. 다만 걸어나가서 떨어지는 것을 확인할 수 없다.")]
    [SerializeField] private bool lockWhileWorking = false;

    /// <summary>지금 달리고 있는지. HUD 와 애니메이션이 읽어간다.</summary>
    public bool IsSprinting { get; private set; }

    /// <summary>지금 속도 (m/s). 달리는지 걷는지가 여기 들어 있다.</summary>
    public float CurrentSpeed { get; private set; }

    private IPlayerController _input;
    private TaskWorker _worker;
    private CharacterController _body;

    /// <summary>화면 기준 이동에 쓰는 카메라. 한 번 찾아 두고 다시 안 찾는다.</summary>
    private Camera _view;

    /// <summary>아래로 쌓이는 속도. 땅에 닿으면 초기화된다.</summary>
    private float _fallSpeed;

    private void Awake()
    {
        _input = GetComponent<IPlayerController>();
        _worker = GetComponent<TaskWorker>();
        _body = GetComponent<CharacterController>();

        if (_input == null)
        {
            Debug.LogError(
                $"[{name}] IPlayerController 를 찾을 수 없습니다. " +
                "KeyboardPlayerController 를 같은 오브젝트에 붙이세요.", this);
        }

        SetUpBody();
        DisableBlockingColliders();
    }

    /// <summary>
    /// 몸통을 갑판에 맞게 맞춘다.
    ///
    /// 씬에 이미 있던 Player 는 큐브라서 CharacterController 가 없습니다.
    /// RequireComponent 는 **새로 붙일 때만** 만들어주므로 여기서 값을 채웁니다.
    /// </summary>
    private void SetUpBody()
    {
        _body.height = bodyHeight;
        _body.radius = bodyRadius;
        _body.center = new Vector3(0f, bodyHeight * 0.5f, 0f);
        _body.stepOffset = stepHeight;
        _body.slopeLimit = slopeLimit;
    }

    /// <summary>
    /// 큐브에 딸려온 콜라이더를 끈다.
    ///
    /// 기본 도형(Cube)으로 만든 Player 에는 BoxCollider 가 붙어 있습니다.
    /// CharacterController 의 캡슐과 둘 다 살아 있으면 **자기 자신에 걸려**
    /// 계단 앞에서 멈추거나 갑판 위에서 떨립니다. 진짜 캐릭터가 오면 사라질 문제입니다.
    /// </summary>
    private void DisableBlockingColliders()
    {
        Collider[] colliders = GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger || colliders[i] is CharacterController)
            {
                continue;
            }

            colliders[i].enabled = false;
            Debug.Log($"[{name}] {colliders[i].GetType().Name} 를 껐습니다. 이동은 CharacterController 가 합니다.", this);
        }
    }

    private void Update()
    {
        if (_input == null || _body == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        if (lockWhileWorking && _worker != null && _worker.Current != null)
        {
            IsSprinting = false;
            CurrentSpeed = 0f;

            // 자리에 묶여 있어도 중력은 받는다. 안 그러면 자리가 사라졌을 때 허공에 선다.
            Fall(Vector3.zero, deltaTime);
            return;
        }

        Vector2 move = _input.Move;
        Vector3 direction = ToWorld(new Vector3(move.x, 0f, move.y));

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        IsSprinting = CanSprint() && direction.sqrMagnitude > 0.0001f;
        CurrentSpeed = (IsSprinting ? sprintSpeed : walkSpeed) * direction.magnitude;

        Fall(direction * (IsSprinting ? sprintSpeed : walkSpeed), deltaTime);
        FaceMoveDirection(direction, deltaTime);
    }

    /// <summary>
    /// 스틱이 가리킨 방향을 **화면 기준에서 월드 기준으로** 바꾼다.
    ///
    /// 카메라는 오른손 스틱으로 좌우 100도까지 돕니다. (ShipCoopCamera)
    /// 그 상태에서 스틱 위를 월드 +z 로 그대로 쓰면, 화면에서는 **비스듬히** 갑니다.
    /// 우당탕탕하는 중에 화면과 손이 어긋나면 엉뚱한 데로 뜁니다.
    ///
    /// 카메라의 좌우 각도(yaw)만 씁니다. 위아래로 기울어진 것은 빼야
    /// 갑판을 기어오르지 않습니다.
    /// </summary>
    /// <remarks>
    /// ⚠ **네트워크에서는 이 변환을 각자 자기 쪽에서 해야 합니다.** (11장)
    ///    카메라는 사람마다 다르게 돌아가 있어서 호스트가 알 수 없습니다.
    ///    입력을 보낼 때 **돌린 뒤의 방향**을 보내거나, 카메라 각도를 같이 보내야 합니다.
    /// </remarks>
    private Vector3 ToWorld(Vector3 stick)
    {
        if (!cameraRelative)
        {
            return stick;
        }

        Camera view = _view != null ? _view : (_view = Camera.main);

        if (view == null)
        {
            return stick;
        }

        return Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f) * stick;
    }

    /// <summary>
    /// 🏃 지금 달릴 수 있는지.
    ///
    /// **양손이 묶여 있으면 못 달립니다.** 포탄 · 자재 · 양동이를 들고 있을 때입니다.
    /// 이 한 줄이 운반을 느리게 만들고, 그래서 "누가 가까이 있어?" 가 나옵니다. (4장)
    /// </summary>
    private bool CanSprint()
    {
        if (_worker != null && _worker.HandsBusy)
        {
            return false;
        }

        return ShipCoopInput.Sprint(_input);
    }

    /// <summary>수평 이동과 중력을 한 번에 적용한다.</summary>
    private void Fall(Vector3 horizontalVelocity, float deltaTime)
    {
        if (_body.isGrounded && _fallSpeed < 0f)
        {
            // 땅에 붙어 있게 살짝 눌러둔다. 0 으로 두면 계단을 내려갈 때 통통 튄다.
            _fallSpeed = -2f;
        }
        else
        {
            _fallSpeed += gravity * deltaTime;
        }

        Vector3 velocity = horizontalVelocity;
        velocity.y = _fallSpeed;

        _body.Move(velocity * deltaTime);
    }

    /// <summary>가는 방향으로 몸을 돌린다.</summary>
    private void FaceMoveDirection(Vector3 direction, float deltaTime)
    {
        if (turnSpeed <= 0f || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * deltaTime);
    }

    /// <summary>인스펙터에서 값을 바꾸면 바로 반영한다.</summary>
    private void OnValidate()
    {
        if (_body != null && Application.isPlaying)
        {
            SetUpBody();
        }
    }
}
