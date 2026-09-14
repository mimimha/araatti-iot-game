using UnityEngine;

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
/// **카메라를 회전시키지 않는다.** 캐릭터가 회전하지 않기 때문이다
/// (Rotate Speed = 0, Space = World). 방향키가 월드 기준이라 "캐릭터 뒤쪽" 이
/// 고정된 방향이고, 카메라는 위치만 따라가면 된다.
/// 마우스로 시점을 돌리는 기능은 넣지 않는다 — 6장에서 숄더뷰를 기각한 이유와 같다.
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

    /// <summary>지금 탑뷰인가.</summary>
    public bool IsBoardView => _boardView;

    /// <summary>계산된 탑뷰 높이(m). 화면에 띄워 확인할 때 쓴다.</summary>
    public float BoardHeight => CalcBoardHeight();

    private void Awake()
    {
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

        // 전환이 끝난 뒤 — 탑뷰는 고정, 3인칭은 계속 부드럽게 따라간다.
        if (_boardView || followSmoothing <= 0f)
        {
            transform.SetPositionAndRotation(pos, rot);
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position, pos, ref _velocity, followSmoothing);
        transform.rotation = rot;
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

        pos = _follow.position + followOffset;
        Vector3 pivot = _follow.position + Vector3.up * lookHeight;
        rot = Quaternion.LookRotation(pivot - pos, Vector3.up);
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
        if (grid == null || cam == null) return minBoardHeight;

        float half = grid.Size * grid.CellSize * 0.5f;
        float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        if (tan <= 0.0001f) return minBoardHeight;

        float byHeight = half / tan;
        float byWidth = half / (tan * Mathf.Max(0.01f, cam.aspect));

        return Mathf.Max(minBoardHeight, Mathf.Max(byHeight, byWidth) * boardMargin);
    }
}