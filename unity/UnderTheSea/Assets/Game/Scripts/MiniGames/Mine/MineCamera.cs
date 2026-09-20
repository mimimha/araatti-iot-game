using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>시점을 돌릴 때 누르고 있어야 하는 마우스 버튼.</summary>
public enum MouseDragButton
{
    Left,
    Right,
    Middle,
}

/// <summary>
/// 광산의 카메라. 시점이 셋이다. (MINE.md 6장 · 3장 7번)
///
///   목표 공개 7초  탑뷰        그림 전체를 봐야 외울 수 있다
///   턴 (채굴)      낮은 3인칭  광산 안. 전체가 안 보인다
///   힌트 3초       탑뷰        공개를 3초 더 사는 것이니 같아야 한다
///   끝 (결과)      탑뷰        플레이 내내 못 보던 전체를 한 번에 공개한다
///
/// ⚠ 힌트를 빠뜨리면 안 된다. 도안을 켜고 판을 밝혀도 **카메라가 낮으면 내 주변
///   도안만 보인다.** 어둠을 넣었을 때와 똑같은 함정이다.
///
/// **낮은 3인칭에서는 마우스로 시점을 돌린다.** 판 전체가 안 보이는 게임이라,
/// 이미 판 곳을 확인하려면 몸을 옮기는 것 말고 둘러볼 방법이 있어야 한다.
///
/// ⚠ 시점을 돌리면 **이동도 화면 기준이어야 한다.** 카메라만 돌리면 뒤를 본
///   순간 "앞으로" 가 화면 아래로 걸어간다. 그 일은 <see cref="MineMoveInput"/>
///   이 맡는다 — 여기 <see cref="Yaw"/> 를 캐릭터가 바라볼 방향으로 넘긴다.
///   **둘은 같이 움직인다.** 하나만 떼면 조작이 꼬인다.
///
/// 캐릭터는 보고 있는 쪽으로 몸을 돌린다. W 로 그쪽으로 직진하고 A · D 는 옆걸음이다.
/// 조준은 그래도 발밑이라, 어디를 보든 파는 칸은 같다. (MINE.md 6장)
///
/// 탑뷰에서는 마우스를 안 받는다. 그림을 외우는 시간이라 화면이 흔들리면 안 된다.
///
/// 탑뷰 높이는 **박아두지 않고 계산한다.** 판 크기나 FOV 를 바꾸면 따라 바뀌어야 한다.
///
/// Main Camera 에 붙인다.
/// </summary>
public class MineCamera : MonoBehaviour
{
    [Header("낮은 3인칭 (턴 중)")]
    [Tooltip("플레이어 기준 카메라 위치. 기본값은 뒤로 7.5m · 높이 4.5m (약 25°).\n" +
             "MINE.md 12장의 조정 항목이다.")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 4.5f, -7.5f);

    [Tooltip("플레이어의 어느 높이를 바라볼 것인가(m). 발밑을 보면 화면이 답답하다.")]
    [SerializeField, Min(0f)] private float lookHeight = 1f;

    [Tooltip("따라가는 부드러움(초). 0 이면 딱 붙어 움직여 흔들릴 수 있다.")]
    [SerializeField, Min(0f)] private float followSmoothing = 0.12f;

    [Header("마우스 시점 (낮은 3인칭에서만)")]
    [Tooltip("끄면 예전처럼 고정된 각도로만 따라간다.")]
    [SerializeField] private bool mouseLook = true;

    [Tooltip("마우스 입력 1 당 몇 도를 돌 것인가. 좌우.\n" +
             "Mouse X 는 픽셀의 0.1 배로 들어온다. 값 × 0.1 이 곧 도/픽셀 이다.\n" +
             "1.5 면 800DPI 마우스로 1인치에 약 120도 돌아간다.")]
    [SerializeField, Range(0.5f, 15f)] private float sensitivityX = 1.5f;

    [Tooltip("위아래 감도. 좌우보다 낮게 둔다 — 위아래는 minPitch~maxPitch 만큼만\n" +
             "움직이므로(기본 68도), 가로와 같은 감도면 금방 한계에 부딪힌다.")]
    [SerializeField, Range(0.5f, 15f)] private float sensitivityY = 1f;

    [Tooltip("켜면 마우스를 위로 밀 때 화면이 아래를 본다.")]
    [SerializeField] private bool invertY;

    [Tooltip("내려다보는 각도의 한계(도). 작을수록 수평, 클수록 위에서 본다.\n" +
             "0 아래로 내려가면 바닥을 뚫고 올려다보게 된다.")]
    [SerializeField, Range(-10f, 40f)] private float minPitch = 2f;

    [SerializeField, Range(40f, 89f)] private float maxPitch = 70f;

    [Tooltip("휠로 당기고 미는 거리의 한계(m).")]
    [SerializeField, Min(1f)] private float minDistance = 3f;

    [SerializeField, Min(1f)] private float maxDistance = 14f;

    [Tooltip("휠 한 칸에 움직이는 거리(m). 0 이면 줌을 안 쓴다.")]
    [SerializeField, Min(0f)] private float zoomStep = 12f;

    [Tooltip("시점을 돌리려면 누르고 있어야 하는 마우스 버튼. 오른쪽이 기본이다. 왼쪽은 채팅창·버튼 같은 화면 요소가 쓴다.")]
    [SerializeField] private MouseDragButton dragButton = MouseDragButton.Right;

    [Header("탑뷰 (공개 · 힌트 · 결과)")]
    [Tooltip("판 바깥으로 남길 여유. 1.15 면 판보다 15% 넓게 잡는다.")]
    [SerializeField, Range(1f, 1.6f)] private float boardMargin = 1.15f;

    [Tooltip("탑뷰 최소 높이(m). 판이 아주 작을 때 카메라가 바닥에 박히는 것을 막는다.")]
    [SerializeField, Min(1f)] private float minBoardHeight = 5f;

    [Header("전환 시간(초)")]
    [Tooltip("평소 시점 전환. 힌트는 3초뿐이라 길면 보는 시간을 깎는다.")]
    [SerializeField, Min(0f)] private float transitionSeconds = 0.5f;

    [Tooltip("판이 끝날 때 올라가는 시간. 이 게임의 하이라이트라 조금 길게. (3장 7번)")]
    [SerializeField, Min(0f)] private float finaleSeconds = 1.5f;

    [Header("연결")]
    [Tooltip("비워두면 이 오브젝트의 Camera, 없으면 Main Camera 를 쓴다.")]
    [SerializeField] private Camera cam;

    [Tooltip("비워두면 씬에서 찾는다. 판 크기를 읽어 탑뷰 높이를 계산한다.")]
    [SerializeField] private MineGrid grid;

    private Transform _follow;
    private bool _boardView = true;

    private Vector3 _fromPos;
    private Quaternion _fromRot;
    private float _elapsed;
    private float _duration;
    private bool _snapNext = true;

    private Vector3 _velocity;

    /// <summary>카메라가 따라가는 지점. 플레이어보다 살짝 늦게 움직인다.</summary>
    private Vector3 _followPoint;

    /// <summary>지금 탑뷰인가.</summary>
    public bool IsBoardView => _boardView;

    /// <summary>계산된 탑뷰 높이(m). 화면에 띄워 확인할 때 쓴다.</summary>
    public float BoardHeight => CalcBoardHeight();

    // 마우스로 돌린 시점. followOffset 에서 뽑은 값으로 시작한다.
    private float _yaw;
    private float _pitch;
    private float _distance;
    private bool _orbitReady;

    /// <summary>
    /// 지금 보고 있는 좌우 각도(도). <see cref="MineMoveInput"/> 이 이걸 보고
    /// 방향키를 화면 기준으로 돌린다. 0 이면 카메라가 월드 -Z 쪽에 있다.
    /// </summary>
    public float Yaw
    {
        get { EnsureOrbit(); return _yaw; }
    }

    // ------------------------------------------------------------
    // 네트워크 전환용 덧붙임 (광산 서버화 1단계)
    // 아래 셋은 **읽기와 대입만** 한다. 위의 궤도 계산은 하나도 건드리지 않았다.
    // ------------------------------------------------------------

    /// <summary>
    /// 지금 보고 있는 상하 각도(도). 관전자가 같은 시점을 보려면 이것도 필요하다.
    ///
    /// 좌우(<see cref="Yaw"/>)만 맞추면 고개 높이가 서로 달라, 같은 곳을 본다고 해도
    /// 화면에 잡히는 범위가 다르다.
    /// </summary>
    public float Pitch
    {
        get { EnsureOrbit(); return _pitch; }
    }

    /// <summary>
    /// 마우스로 시점을 돌릴 수 있는가. **관전 중에는 꺼진다.**
    ///
    /// <c>mouseLook</c> 은 인스펙터 값이라 "이 씬이 마우스 시점을 쓰는가" 를 정하고,
    /// 이쪽은 "지금 이 사람이 돌려도 되는가" 를 정한다. 둘 다 켜져야 돈다.
    ///
    /// 혼자 하는 씬에서는 아무도 내리지 않으므로 예전 그대로다.
    /// </summary>
    public bool AcceptsMouse { get; set; } = true;

    /// <summary>
    /// 시점을 밖에서 정해 준다. **관전용이다.**
    ///
    /// 지금 턴인 사람의 각도를 복제받아 그대로 넣으면 네 명이 같은 화면을 본다.
    /// 자리(<see cref="FollowPlayer"/>)만 맞추고 각도를 안 맞추면, 같은 사람을
    /// 따라다니면서도 저마다 다른 쪽을 보게 된다.
    ///
    /// 거리(줌)는 건드리지 않는다. 각자 편한 거리로 두어도 시점은 같다.
    /// </summary>
    public void ApplyOrbit(float yaw, float pitch)
    {
        EnsureOrbit();

        _yaw = yaw;
        _pitch = Mathf.Clamp(pitch, minPitch, Mathf.Max(minPitch, maxPitch));
    }

    /// <summary>
    /// 시점을 followOffset 이 말하는 기본 각도로 되돌린다.
    ///
    /// ⚠ followOffset 은 **발밑 기준**이고 회전은 **바라보는 점 기준**이다.
    ///   lookHeight 만큼 빼고 계산해야 기본값이 예전 화면과 정확히 같아진다.
    ///   기본값 (0, 4.5, -7.5) · lookHeight 1 이면 거리 8.28m · 내림각 25도다.
    /// </summary>
    public void ResetOrbit()
    {
        Vector3 rel = followOffset - Vector3.up * lookHeight;
        float flat = new Vector2(rel.x, rel.z).magnitude;

        _distance = Mathf.Clamp(rel.magnitude, minDistance, maxDistance);
        _pitch = Mathf.Clamp(Mathf.Atan2(rel.y, flat) * Mathf.Rad2Deg, minPitch, maxPitch);
        _yaw = Mathf.Atan2(-rel.x, -rel.z) * Mathf.Rad2Deg;

        _orbitReady = true;
    }

    // Awake 를 기다리지 않는다. MineMoveInput 이 -100 이라 여기 Awake 보다
    // 먼저 Yaw 를 물어볼 수 있다. 그때 0 을 돌려주면 첫 프레임에 이동이 튄다.
    private void EnsureOrbit()
    {
        if (!_orbitReady) ResetOrbit();
    }

    /// <summary>
    /// 마우스를 읽어 시점을 돌린다. 탑뷰에서는 받지 않는다.
    ///
    /// ⚠ Mouse X · Y 는 **이미 프레임당 변화량**이다. deltaTime 을 곱하면
    ///   프레임이 빠를수록 덜 도는 반대 결과가 나온다.
    /// </summary>
    private void Update()
    {
        EnsureOrbit();

        // AcceptsMouse 가 꺼져 있으면 관전 중이다. 시점은 복제로 들어온다.
        if (!mouseLook || !AcceptsMouse || _boardView || _follow == null) return;

        // ⚠ 마우스도 새 Input System 으로 읽는다. 레거시 축은 같은 프로젝트 안에서도
        //   빌드에 따라 조용히 0 이 나오는 일이 있어, 화면이 안 돌아가는 원인이 된다.
        //   장치가 없으면(서버 · 창 없는 실행) 예전 경로로 떨어진다. (광산 서버화)
        Mouse mouse = Mouse.current;

        float mx = 0f, my = 0f;

        // ⚠ **버튼을 누르고 있는 동안에만 돈다.** (IOT_INPUT.md 1장 — 네 게임 공통)
        //
        //   예전에는 커서를 화면에 가두고 마우스를 움직이기만 해도 돌았다. 그러면
        //   채팅창이나 버튼을 누르려고 커서를 옮기는 동안에도 화면이 돌아간다.
        //   좌클릭은 그 화면 요소들이 써야 하므로 기본은 우클릭이다.
        //
        //   줌(휠)은 버튼과 무관하다. 로비(LocalPlayerView)와 같은 규칙이다.
        if (IsDragging(mouse))
        {
            if (mouse != null)
            {
                // 레거시 Mouse X/Y 는 픽셀의 0.1 배로 들어온다. 감도 값을 그대로 쓰려면 맞춰준다.
                Vector2 delta = mouse.delta.ReadValue() * 0.1f;
                mx = delta.x;
                my = delta.y;
            }
            else
            {
                mx = Input.GetAxis("Mouse X");
                my = Input.GetAxis("Mouse Y");
            }
        }

        _yaw += mx * sensitivityX;
        _pitch += (invertY ? my : -my) * sensitivityY;
        _pitch = Mathf.Clamp(_pitch, minPitch, Mathf.Max(minPitch, maxPitch));

        if (zoomStep > 0f)
        {
            float scroll = mouse != null
                ? mouse.scroll.ReadValue().y * 0.01f
                : Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _distance = Mathf.Clamp(_distance - scroll * zoomStep,
                                        minDistance, Mathf.Max(minDistance, maxDistance));
            }
        }
    }

    /// <summary>
    /// 시점을 돌리는 버튼을 누르고 있는가.
    ///
    /// 장치가 없으면(서버 · 창 없는 실행) 레거시 경로로 떨어진다.
    /// </summary>
    private bool IsDragging(Mouse mouse)
    {
        if (mouse == null)
        {
            return Input.GetMouseButton(dragButton == MouseDragButton.Left ? 0
                                      : dragButton == MouseDragButton.Right ? 1 : 2);
        }

        switch (dragButton)
        {
            case MouseDragButton.Left: return mouse.leftButton.isPressed;
            case MouseDragButton.Middle: return mouse.middleButton.isPressed;
            default: return mouse.rightButton.isPressed;
        }
    }

    private void Awake()
    {
        // ⚠ 커서를 잠그지 않는다. 드래그로 도므로 커서가 자유로워야 채팅창·버튼을 누른다.
        //   (IOT_INPUT.md 1장 — "ESC 로 커서를 푸는 장치도 필요 없어집니다")
        //
        //   여기서 한 번 풀어주는 이유는 커서 상태가 **전역**이기 때문이다. 커서를
        //   잠그는 다른 씬에서 넘어오면 잠긴 채로 들어와 광산에서 커서가 안 보인다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (grid == null) grid = FindAnyObjectByType<MineGrid>();

        if (cam == null)
            Debug.LogError($"{nameof(MineCamera)}: Camera 를 찾지 못했습니다. " +
                           $"Main Camera 에 붙이세요.", this);

        if (grid == null)
            Debug.LogError($"{nameof(MineCamera)}: MineGrid 를 찾지 못했습니다. " +
                           $"탑뷰 높이를 계산할 수 없습니다.", this);
    }

    /// <summary>판 전체가 보이는 탑뷰로. 공개 · 힌트 · 결과에 쓴다.</summary>
    /// <param name="finale">판이 끝나서 올라가는 것인가. 켜면 천천히 움직인다.</param>
    public void ShowBoard(bool finale = false)
    {
        _follow = null;
        _boardView = true;
        BeginMove(finale ? finaleSeconds : transitionSeconds);
    }

    /// <summary>낮은 3인칭으로 이 사람을 따라간다. null 이면 지금 자리에 머문다.</summary>
    public void FollowPlayer(Transform player)
    {
        if (player == null) return;

        _follow = player;
        _boardView = false;

        // 새 사람을 잡을 때는 지점을 바로 옮긴다. 안 그러면 앞 사람 자리에서
        // 슬금슬금 기어오는 모양이 된다.
        _followPoint = player.position;

        BeginMove(transitionSeconds);
    }

    private void BeginMove(float seconds)
    {
        _fromPos = transform.position;
        _fromRot = transform.rotation;
        _elapsed = 0f;
        _duration = _snapNext ? 0f : Mathf.Max(0f, seconds);
        _snapNext = false;
        _velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        // 캐릭터는 Update 에서 움직인다. LateUpdate 에서 따라가야 한 프레임 안 밀린다.
        TrackFollow();

        if (!Desired(out Vector3 pos, out Quaternion rot)) return;

        // 시점을 바꾼 직후에는 예전 자리에서 새 자리로 부드럽게 넘어간다.
        if (_duration > 0f && _elapsed < _duration)
        {
            _elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(_elapsed / _duration);
            t = t * t * (3f - 2f * t);          // 시작과 끝을 완만하게

            transform.SetPositionAndRotation(
                Vector3.Lerp(_fromPos, pos, t),
                Quaternion.Slerp(_fromRot, rot, t));
            return;
        }

        transform.SetPositionAndRotation(pos, rot);
    }

    /// <summary>
    /// 따라가는 지점을 부드럽게 옮긴다.
    ///
    /// ⚠ 늦추는 것은 **카메라 위치가 아니라 따라가는 지점**이다.
    ///   카메라 위치를 SmoothDamp 하면 마우스로 시점을 돌릴 때도 목표가 매 프레임
    ///   크게 움직여서, 화면이 고무줄처럼 늘어졌다가 따라온다.
    ///   지점만 늦추면 걸음은 부드럽고 시점은 마우스에 딱 붙는다.
    /// </summary>
    private void TrackFollow()
    {
        if (_follow == null) return;

        if (_boardView || followSmoothing <= 0f)
        {
            _followPoint = _follow.position;
            _velocity = Vector3.zero;
            return;
        }

        _followPoint = Vector3.SmoothDamp(
            _followPoint, _follow.position, ref _velocity, followSmoothing);
    }

    /// <summary>지금 있어야 할 자리와 방향. 정할 수 없으면 false.</summary>
    private bool Desired(out Vector3 pos, out Quaternion rot)
    {
        pos = transform.position;
        rot = transform.rotation;

        if (_boardView)
        {
            if (grid == null) return false;

            Vector3 center = grid.transform.position;
            pos = center + Vector3.up * CalcBoardHeight();

            // 똑바로 내려다본다. 화면 위쪽이 월드 +Z 라서
            // 도안을 rows 에 적은 그대로 보인다. (MineDrawingTarget 의 행 뒤집기와 맞음)
            rot = Quaternion.Euler(90f, 0f, 0f);
            return true;
        }

        if (_follow == null) return false;

        EnsureOrbit();

        // 바라보는 점을 중심으로 돌린다. 마우스를 안 움직이면
        // followOffset 이 말하던 그 자리 그대로다.
        Vector3 pivot = _followPoint + Vector3.up * lookHeight;
        rot = Quaternion.Euler(_pitch, _yaw, 0f);
        pos = pivot + rot * new Vector3(0f, 0f, -_distance);
        return true;
    }

    /// <summary>
    /// 판 전체가 화면에 들어오는 높이.
    ///
    ///     높이 = (판 절반 크기) / tan(FOV 절반)
    ///
    /// 세로로 긴 화면에서는 가로가 먼저 걸리므로 둘 중 큰 값을 쓴다.
    /// 20×20 판에 FOV 60 이면 17.3m, 여유 15% 를 주면 약 20m 다.
    /// </summary>
    private float CalcBoardHeight()
    {
        // ⚠ Awake 를 기다리지 않는다. 여기 오는 길이 둘이다.
        //   하나는 Awake 를 거친 재생 중이고, 다른 하나는 아직 Awake 가 안 돈
        //   **다른 컴포넌트의 Awake** 다. MineVision 이 그 경우인데, 순서가 밀리면
        //   최소 높이(5m)로 안개 거리를 잡아 판 바깥 칸이 먹힌다.
        //   에디터에서 값을 확인할 때도 같은 이유로 여기서 찾는다.
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (grid == null) grid = FindAnyObjectByType<MineGrid>();

        if (grid == null || cam == null) return minBoardHeight;

        // 테두리까지 화면에 들어와야 한다. 판 크기만 보면 테두리가 밖으로 밀려나
        // 판이 꽉 차 보이고 HUD 를 가린다.
        float half = grid.Size * grid.CellSize * 0.5f;

        MineGridView view = grid.GetComponent<MineGridView>();
        if (view != null && view.BoardHalfExtent > 0f) half = view.BoardHalfExtent;
        float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        if (tan <= 0.0001f) return minBoardHeight;

        float byHeight = half / tan;
        float byWidth = half / (tan * Mathf.Max(0.01f, cam.aspect));

        return Mathf.Max(minBoardHeight, Mathf.Max(byHeight, byWidth) * boardMargin);
    }
}