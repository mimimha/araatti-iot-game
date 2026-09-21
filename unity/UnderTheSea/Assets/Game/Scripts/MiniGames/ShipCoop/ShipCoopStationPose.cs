using UnityEngine;

/// <summary>
/// 🧍 자리에 붙으면 **그 자리의 물건을 잡는 자세**를 만든다. (SHIPCOOP.md 4장)
///
/// <code>
///   💣 대포     포가 뒤쪽(포미) 위를 두 손으로 잡고 포구 쪽을 본다
///   🪢 돛       가슴 앞에 내려온 밧줄을 두 손으로 잡는다. 당기면(L) 손이 번갈아 아래로,
///               풀면(J) 위로 미끄러진다. 가만히 있으면 잡고만 있다 (ShipCoopSailRope 가 밧줄을 그린다)
///   🛞 조타     바퀴 테의 10시 · 2시를 잡는다. 바퀴가 도는 만큼 손도 테를 따라 돌고,
///               너무 돌면 거기서 멈춰 잡는다 (ShipCoopHelmWheel 이 재 둔 중심 · 반지름 · 축 · 각도를 쓴다)
/// </code>
///
/// <b>왜 필요한가.</b> 자리에 붙어도 캐릭터는 가만히 서 있었다. 대포를 쏘는데 팔이 내려가 있으면
/// "쏜다" 로 안 읽힌다. 남의 화면에서 누가 무엇을 하는지도 이 자세로 안다.
///
/// <b>어떻게 만드나.</b> <see cref="ShipCoopCarryPose"/> 와 같은 방법이다. 잡는 애니메이션이 없어서
/// (컨트롤러에 걷기 · 달리기 · 점프 · 공격만 있다) **IK 로 손을 끌어다** 놓는다. 손 위치는 자리
/// 물건의 렌더러 경계에서 매 프레임 낸다 — 대포가 반동으로 튀면 손도 같이 따라간다.
/// 몸은 루트가 아니라 **보이는 몸(Visual)** 만 돌려 물건을 보게 한다. 루트는 네트워크가 옮기므로
/// 건드리지 않는다.
///
/// <b>누가 붙었는지는 모든 화면이 안다.</b> <c>ShipCoopWorkerSync</c> 가 자리 번호를 복제해
/// <c>TaskWorker.Current</c> 를 맞춰 주므로 클라이언트에서도 남의 자세가 같이 나온다.
///
/// ⚠ 드는 자세(<see cref="ShipCoopCarryPose"/>)와 같은 애니메이터를 쓴다. 들고 있으면 자리에
///    붙을 수 없으니(HandsBusy) 둘이 동시에 켜지는 일은 없지만, 빈손일 때 드는 자세가 IK 를
///    풀어 버리면 이쪽이 지워진다. 그래서 드는 자세는 <see cref="IsPosing"/> 을 보고 비켜 준다.
///
/// ⚠ **애니메이터 레이어에 IK Pass 가 켜져 있어야 한다.** 드는 자세와 같은 전제다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ShipCoopStationPose : MonoBehaviour
{
    [Header("연결 — 비워두면 이 오브젝트에서 찾는다")]
    [SerializeField] private TaskWorker worker;
    [SerializeField] private ShipCoopCharacter character;

    [Header("자세")]
    [Tooltip("IK 를 얼마나 강하게 걸지. 1 이면 팔이 완전히 굳는다.")]
    [SerializeField, Range(0f, 1f)] private float strength = 0.9f;

    [Tooltip("잡을 곳이 이보다 멀면 팔을 안 뻗는다 (m). 정위치 근처에서만 잡는다 — 붙은 채로 걸어 나가면 팔을 내린다.\n" +
             "크게 두면 몸 뒤 2m 대포로 팔을 꺾어 뻗었다.")]
    [SerializeField, Range(0.5f, 5f)] private float reachLimit = 1.4f;

    /// <summary>
    /// 수평 거리. ⚠ 자리 · 잡는 곳은 갑판 위 0.7 ~ 1.6m 높이에 떠 있고 사람 위치는 발이라,
    /// 3차원 거리로 재면 정위치에 서 있어도 1.9m 가 나와 자세가 아예 안 켜졌다. 실제로 그랬다.
    /// </summary>
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    [Tooltip("몸이 물건 쪽으로 도는 속도 (도/초).")]
    [SerializeField, Min(10f)] private float turnSpeed = 540f;

    [Header("대포")]
    [Tooltip("포미(뒤쪽 끝)에서 포구 쪽으로 이만큼 들어간 곳을 잡는다 (포신 길이 대비 비율).")]
    [SerializeField, Range(0f, 0.6f)] private float cannonGripAlong = 0.06f;

    [Tooltip("두 손 사이 거리 (m).")]
    [SerializeField, Range(0.1f, 1f)] private float cannonGripApart = 0.42f;

    [Tooltip("대포 윗면에서 이만큼 위를 잡는다 (m). 0 이면 손이 반쯤 묻힌다.")]
    [SerializeField, Range(0f, 0.3f)] private float cannonGripLift = 0.05f;

    [Header("돛 밧줄")]
    [Tooltip("가슴에서 앞으로 이만큼 떨어진 곳에서 밧줄을 잡는다 (m).")]
    [SerializeField, Range(0.1f, 0.8f)] private float ropeReach = 0.38f;

    [Tooltip("두 손이 밧줄 위에서 벌어지는 거리 (m). 위쪽 손과 아래쪽 손 사이.")]
    [SerializeField, Range(0.1f, 0.8f)] private float ropeHandsApart = 0.34f;

    [Tooltip("당길 때 손이 오르내리는 폭 (m).")]
    [SerializeField, Range(0.05f, 0.6f)] private float ropeStroke = 0.28f;

    [Tooltip("끝까지 당길 때 초당 몇 번 손을 바꿔 잡는가.")]
    [SerializeField, Range(0.2f, 4f)] private float ropeStrokesPerSecond = 1.6f;

    [Tooltip("풀 때는 당길 때의 몇 배 속도로 손이 미끄러지는가. 풀 때는 손을 바꾸지 않고 밧줄이 빠져나간다.")]
    [SerializeField, Range(0.1f, 2f)] private float ropeSlipRate = 0.6f;

    [Header("조타륜")]
    [Tooltip("바퀴 맨 위에서 좌우로 이만큼 벌어진 곳을 잡는다 (도). 50 이면 10시 · 2시.")]
    [SerializeField, Range(10f, 90f)] private float wheelGripAngle = 50f;

    [Tooltip("바퀴가 돌 때 손이 따라 도는 최대 각도 (도). 그 이상은 거기서 멈춰 잡는다 — 팔이 꼬이지 않게.")]
    [SerializeField, Range(0f, 120f)] private float wheelFollowLimit = 70f;

    [Tooltip("테에서 사람 쪽으로 이만큼 띄워 잡는다 (m). 0 이면 손이 테에 반쯤 묻힌다.")]
    [SerializeField, Range(0f, 0.2f)] private float wheelGripOut = 0.06f;

    [Tooltip("테 반지름의 몇 배 되는 곳을 잡는가. 1 이면 테 한가운데 두께 위.")]
    [SerializeField, Range(0.6f, 1.1f)] private float wheelGripRadius = 0.95f;

    [Header("대포가 튈 때 몸도 덜컹")]
    [Tooltip("대포가 튄 뒤 이만큼 있다가 몸이 흔들린다 (초). 다른 물체라 같은 순간이면 붙어 보인다.")]
    [SerializeField, Range(0f, 0.3f)] private float joltDelay = 0.07f;

    [Tooltip("몸이 뒤로 밀리는 거리 (m). 대포(0.28m)보다 훨씬 작아야 한다 — 몸은 발로 버틴다.")]
    [SerializeField, Range(0f, 0.3f)] private float joltDistance = 0.09f;

    [Tooltip("몸이 위로 살짝 뜨는 높이 (m).")]
    [SerializeField, Range(0f, 0.2f)] private float joltLift = 0.03f;

    [Tooltip("밀렸다가 돌아오는 데 걸리는 시간 (초). 대포보다 살짝 길게 — 몸이 더 느리게 가라앉는다.")]
    [SerializeField, Range(0.1f, 1.5f)] private float joltSeconds = 0.55f;

    [Header("손뼈 축 보정 — ShipCoopCarryPose 와 같은 값")]
    [SerializeField] private Vector3 leftHandFixEuler = new Vector3(0f, -90f, -90f);
    [SerializeField] private Vector3 rightHandFixEuler = new Vector3(0f, 90f, 90f);

    private Animator _animator;
    private Transform _model;
    private Quaternion _modelRestLocalRotation;
    private bool _modelTurned;

    /// <summary>이번 프레임에 손을 뻗고 있는지. 드는 자세가 IK 를 풀지 않도록 본다.</summary>
    public bool IsPosing { get; private set; }

    // 이번 프레임의 손 목표. OnAnimatorIK 와 LateUpdate 가 같이 본다.
    private Vector3 _leftAt;
    private Vector3 _rightAt;
    private Vector3 _lookAt;
    private Vector3 _leftPalm = Vector3.down;
    private Vector3 _rightPalm = Vector3.down;
    private Vector3 _leftFingers = Vector3.forward;
    private Vector3 _rightFingers = Vector3.forward;
    private bool _haveTargets;

    private ShipCoopHelmWheel _wheel;

    private CannonTask _cannonTask;
    private Renderer _cannonDraw;
    private ShipCoopCannonRecoil _cannonRecoil;

    /// <summary>대포가 튄 시각. 음수면 없음. 몸은 joltDelay 뒤에 따라 흔들린다.</summary>
    private float _joltAt = -1f;

    /// <summary>덜컹 방향 (월드, 수평) — 포구 반대.</summary>
    private Vector3 _joltBack;

    private ShipCoopSailRope _rope;
    private Transform _chest;
    private Transform _leftHand;
    private Transform _rightHand;
    private Transform _leftUpperArm;
    private Transform _rightUpperArm;
    private Transform _leftLowerArm;
    private Transform _rightLowerArm;

    [Header("계측 — 팔 모양이 이상할 때만 켠다")]
    [Tooltip("자리에 붙어 있는 동안 1초마다 손 목표 · 실제 손뼈 · 어깨 기준 거리 · IK 호출 수를 로그로 남긴다.")]
    [SerializeField] private bool logHands = false;

    private float _nextHandLog;
    private int _ikCalls;
    private int _ikCallsLastFrame;
    private int _ikFrame = -1;

    /// <summary>밧줄 위 손의 위상. 0~1 이 한 번 바꿔 잡기다.</summary>
    private float _ropePhase;

    /// <summary>왼손이 위인가. 손을 바꿔 잡을 때마다 뒤집힌다.</summary>
    private bool _leftIsUpper = true;

    /// <summary>실제 손뼈 자리 (월드, IK 뒤). 밧줄 연출이 이 사이를 지나게 그린다.</summary>
    public Vector3 UpperHand => HandWorld(_leftIsUpper ? _leftHand : _rightHand, _leftIsUpper ? _leftAt : _rightAt);
    public Vector3 LowerHand => HandWorld(_leftIsUpper ? _rightHand : _leftHand, _leftIsUpper ? _rightAt : _leftAt);

    private static Vector3 HandWorld(Transform bone, Vector3 fallback) => bone != null ? bone.position : fallback;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        if (worker == null) worker = GetComponent<TaskWorker>();
        if (character == null) character = GetComponent<ShipCoopCharacter>();

        if (!_animator.isHuman)
        {
            Debug.LogWarning($"[{name}] 휴머노이드 리그가 아닙니다. 자리 자세를 못 만듭니다.", this);
            enabled = false;
            return;
        }

        _model = character != null ? character.Model : null;

        if (_model != null)
        {
            _modelRestLocalRotation = _model.localRotation;
        }

        _chest = _animator.GetBoneTransform(HumanBodyBones.Chest)
                 ?? _animator.GetBoneTransform(HumanBodyBones.Spine);
        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
    }

    /// <summary>
    /// 자리에 붙어 있는 동안 1초마다 **최종 팔 모양**을 찍는다 (LateUpdate — IK 까지 끝난 뒤).
    /// 캐릭터 기준 (앞, 위, 오른쪽) m. 목표와 실제가 다르면 IK 가 못 닿은 것이고,
    /// 어깨→팔꿈치→손 길이가 평소와 다르면 뼈가 늘어난 것이다.
    /// </summary>
    private void LogHands()
    {
        if (!logHands || !_haveTargets || Time.time < _nextHandLog)
        {
            return;
        }

        _nextHandLog = Time.time + 1f;

        string branch = worker != null && worker.Current != null ? worker.Current.GetType().Name : "?";
        Vector3 origin = transform.position;

        Debug.Log(
            $"[자리 자세] {branch} 계측 (캐릭터 기준 앞/위/오른쪽 m)   OnAnimatorIK {_ikCallsLastFrame}회/프레임   IK 세기 {strength:F2}\n" +
            $"  왼손  목표 {Local(_leftAt - origin)}  실제 {Local(HandWorld(_leftHand, _leftAt) - origin)}  차이 {Vector3.Distance(_leftAt, HandWorld(_leftHand, _leftAt)):F2}m" +
            $"   어깨→손 {Reach(_leftUpperArm, _leftHand):F2}m  (위팔 {Reach(_leftUpperArm, _leftLowerArm):F2} + 아래팔 {Reach(_leftLowerArm, _leftHand):F2})\n" +
            $"  오른손 목표 {Local(_rightAt - origin)}  실제 {Local(HandWorld(_rightHand, _rightAt) - origin)}  차이 {Vector3.Distance(_rightAt, HandWorld(_rightHand, _rightAt)):F2}m" +
            $"   어깨→손 {Reach(_rightUpperArm, _rightHand):F2}m  (위팔 {Reach(_rightUpperArm, _rightLowerArm):F2} + 아래팔 {Reach(_rightLowerArm, _rightHand):F2})\n" +
            $"  어깨 자리 왼 {Local((_leftUpperArm != null ? _leftUpperArm.position : origin) - origin)}  오른 {Local((_rightUpperArm != null ? _rightUpperArm.position : origin) - origin)}" +
            $"   가슴 {Local((_chest != null ? _chest.position : origin) - origin)}   몸 회전(로컬 y) {(_model != null ? _model.localEulerAngles.y : 0f):F0}°" +
            $"   리그 배율 {RigScale:F2}   솔버에 넘긴 왼손 {Local(ToSolver(_leftAt) - origin)}", this);
    }

    private static float Reach(Transform a, Transform b) => a != null && b != null ? Vector3.Distance(a.position, b.position) : -1f;

    private string Local(Vector3 world)
    {
        Vector3 v = transform.InverseTransformDirection(world);
        return $"(앞 {v.z:+0.00;-0.00}, 위 {v.y:+0.00;-0.00}, 오른 {v.x:+0.00;-0.00})";
    }

    /// <summary>지금 붙은 자리에 맞는 손 목표를 낸다. 없으면 false.</summary>
    private bool ComputeTargets()
    {
        if (worker == null || worker.Current == null || worker.HandsBusy)
        {
            return false;
        }

        switch (worker.Current)
        {
            case CannonTask cannon:
                return CannonTargets(cannon);

            case SailTask sail:
                return RopeTargets(sail);

            case HelmTask helm:
                return WheelTargets(helm);
        }

        return false;
    }

    /// <summary>
    /// 🛞 바퀴 테 위의 두 점. 바퀴가 도는 규칙(<see cref="ShipCoopHelmWheel"/>)에서 중심 · 반지름 · 축 ·
    /// 각도를 그대로 빌려 쓰므로 바퀴와 손이 어긋나지 않는다.
    ///
    /// 손은 맨 위에서 좌우로 <see cref="wheelGripAngle"/> 씩 벌어진 곳을 잡고, 바퀴가 돈 각도만큼
    /// 같이 돈다. 조타 60° 에 바퀴는 180° 를 돌아 손이 바닥까지 가 버리므로
    /// <see cref="wheelFollowLimit"/> 에서 멈춰 잡는다 — 사람이 바꿔 잡은 셈이다.
    /// </summary>
    private bool WheelTargets(HelmTask helm)
    {
        if (_wheel == null)
        {
            _wheel = FindAnyObjectByType<ShipCoopHelmWheel>(FindObjectsInactive.Include);
        }

        if (_wheel == null)
        {
            return false;
        }

        Vector3 centre = _wheel.Center;
        Vector3 axis = _wheel.Axis;
        float radius = _wheel.Radius * wheelGripRadius;

        // 바퀴 면의 "위" — 월드 위를 축에 수직으로 눌러서.
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis);
        if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(transform.forward, axis);
        up.Normalize();

        // 사람 쪽 — 축의 두 방향 중 사람이 있는 쪽. 손을 이쪽으로 살짝 띄운다.
        Vector3 toMe = Vector3.Dot(transform.position - centre, axis) >= 0f ? axis : -axis;

        float follow = Mathf.Clamp(_wheel.SpinDegrees, -wheelFollowLimit, wheelFollowLimit);

        // 축 기준 양의 회전이 사람 눈에 어느 쪽인지는 축 방향에 달렸다. 바퀴와 같은 식(AngleAxis(각, 축))을
        // 쓰면 손이 바퀴와 같은 쪽으로 돈다. 왼손은 사람 기준 왼쪽(−) 테, 오른손은 오른쪽(+) 테.
        Vector3 right = Vector3.Cross(up, axis).normalized;
        if (Vector3.Dot(right, transform.right) < 0f) right = -right;

        Vector3 leftRest = Quaternion.AngleAxis(-wheelGripAngle, toMe) * up;
        Vector3 rightRest = Quaternion.AngleAxis(wheelGripAngle, toMe) * up;

        // 오른쪽(+)이 right 와 같은 쪽인지 확인해 부호를 맞춘다.
        if (Vector3.Dot(rightRest, right) < 0f)
        {
            Vector3 swap = leftRest;
            leftRest = rightRest;
            rightRest = swap;
        }

        Quaternion spin = Quaternion.AngleAxis(follow, axis);
        Vector3 leftOnRim = centre + spin * leftRest * radius + toMe * wheelGripOut;
        Vector3 rightOnRim = centre + spin * rightRest * radius + toMe * wheelGripOut;

        _leftAt = leftOnRim;
        _rightAt = rightOnRim;

        // 손바닥은 테를 감싸듯 바퀴 중심 쪽을 본다.
        _leftPalm = (centre - leftOnRim).normalized;
        _rightPalm = (centre - rightOnRim).normalized;

        // 손가락은 테를 따라 감아쥔다 — 축 방향(바퀴 면에 수직) 성분을 빼고, 테의 접선 쪽으로.
        _leftFingers = Vector3.Cross(_leftPalm, toMe).normalized;
        _rightFingers = Vector3.Cross(toMe, _rightPalm).normalized;

        // 몸은 바퀴 너머(뱃머리 쪽)를 본다 — 바퀴 뒤에 서서 앞을 보는 조타수.
        _lookAt = centre - toMe * 3f;

        return FlatDistance(transform.position, centre) <= reachLimit + radius;
    }

    /// <summary>
    /// 🪢 가슴 앞으로 내려온 밧줄 위의 두 점.
    ///
    /// 밧줄은 활대(<see cref="ShipCoopSailRope.Anchor"/>)에서 손으로 내려오므로 거의 세로다.
    /// 두 손은 그 선 위에서 <see cref="ropeHandsApart"/> 만큼 떨어져 있고, 당기는 동안 함께
    /// 아래로 내려가다 위쪽 손이 다시 위로 올라가 잡는다(손 바꿔 잡기). 풀 때는 손을 바꾸지 않고
    /// 밧줄이 위로 빠져나가는 것처럼 두 손이 천천히 올라간다.
    /// </summary>
    private bool RopeTargets(SailTask sail)
    {
        if (_rope == null)
        {
            _rope = FindAnyObjectByType<ShipCoopSailRope>(FindObjectsInactive.Include);
        }

        Vector3 chest = _chest != null ? _chest.position : transform.position + Vector3.up * 1.6f;
        Vector3 anchor = _rope != null ? _rope.Anchor : sail.transform.position + Vector3.up * 8f;

        // 몸은 돛대(활대 아래) 쪽을 본다.
        Vector3 toMast = Vector3.ProjectOnPlane(anchor - transform.position, Vector3.up);
        if (toMast.sqrMagnitude < 1e-4f) toMast = transform.forward;
        toMast.Normalize();

        // 잡는 가운데 — 가슴 앞.
        Vector3 hold = chest + toMast * ropeReach;

        // 밧줄 방향 — 활대에서 잡는 곳으로. 손은 이 선 위에서 오르내린다.
        Vector3 along = (anchor - hold).normalized;
        if (along.sqrMagnitude < 1e-6f) along = Vector3.up;

        // 위상 — 당기면(+) 손이 내려가고, 풀면(−) 올라간다.
        float pull = sail.Pull;
        float rate = pull > 0f ? pull * ropeStrokesPerSecond : pull * ropeStrokesPerSecond * ropeSlipRate;
        float before = _ropePhase;
        _ropePhase += rate * Time.deltaTime;

        if (pull > 0f)
        {
            // 당길 때만 손을 바꿔 잡는다. 한 바퀴 넘어갈 때 위아래를 뒤집는다.
            if (Mathf.FloorToInt(_ropePhase) != Mathf.FloorToInt(before))
            {
                _leftIsUpper = !_leftIsUpper;
            }
        }
        else if (pull < 0f)
        {
            // 풀 때는 손을 바꾸지 않는다. 이번 잡기의 맨 위까지만 미끄러져 올라가고 멈춘다.
            _ropePhase = Mathf.Max(_ropePhase, Mathf.Floor(before));
        }

        // 손 자리 — 톱니. 0 이면 맨 위, 1 에 가까울수록 아래. 바꿔 잡으면 다시 위로.
        float t = Mathf.Repeat(_ropePhase, 1f);
        float slide = (0.5f - t) * ropeStroke;   // +위 … −아래

        Vector3 upper = hold + along * (ropeHandsApart * 0.5f + slide);
        Vector3 lower = hold - along * (ropeHandsApart * 0.5f - slide);

        if (_leftIsUpper)
        {
            _leftAt = upper;
            _rightAt = lower;
        }
        else
        {
            _leftAt = lower;
            _rightAt = upper;
        }

        _lookAt = transform.position + toMast * 3f;
        _leftPalm = toMast;    // 손바닥이 밧줄을 감싸듯 돛대 쪽을 본다
        _rightPalm = toMast;

        // 세로 밧줄을 쥐면 손가락은 밧줄을 **가로질러** 감긴다. 왼손은 오른쪽으로, 오른손은 왼쪽으로.
        // (손바닥과 같은 방향으로 주면 LookRotation 이 무너져 손목이 뒤집힌다 — Reach 주석)
        Vector3 across = Vector3.Cross(Vector3.up, toMast).normalized;
        if (Vector3.Dot(across, transform.right) < 0f) across = -across;
        _leftFingers = across;
        _rightFingers = -across;

        // 밧줄이 손에 닿을 만큼 가까운가. 자리에서 멀면 아직 걸어오는 중이다.
        return FlatDistance(transform.position, sail.transform.position) <= reachLimit;
    }

    /// <summary>💣 포미 위 두 점. 포신 축은 반동 연출이 재 둔 것을 빌리고, 없으면 경계에서 본다.</summary>
    private bool CannonTargets(CannonTask cannon)
    {
        if (_cannonTask != cannon || _cannonDraw == null)
        {
            if (_cannonTask != null)
            {
                _cannonTask.Recoiled -= OnCannonRecoiled;
            }

            _cannonTask = cannon;
            _cannonTask.Recoiled += OnCannonRecoiled;
            _cannonRecoil = FindAnyObjectByType<ShipCoopCannonRecoil>(FindObjectsInactive.Include);
            _cannonDraw = null;

            Transform mesh = _cannonRecoil != null ? _cannonRecoil.Cannon : null;

            if (mesh == null)
            {
                // 반동 연출이 없으면 이름으로 찾는다.
                GameObject byName = GameObject.Find("Cannon");
                mesh = byName != null ? byName.transform : null;
            }

            _cannonDraw = mesh != null ? mesh.GetComponentInChildren<Renderer>() : null;
        }

        if (_cannonDraw == null)
        {
            return false;
        }

        Bounds box = _cannonDraw.bounds;

        // 포신 축 — 반동 연출이 잰 것. 없으면 가로로 긴 쪽.
        Vector3 axis;
        if (_cannonRecoil != null)
        {
            axis = _cannonRecoil.MuzzleWorldDirection;
        }
        else
        {
            axis = box.size.x >= box.size.z ? Vector3.right : Vector3.forward;
            // 사람이 있는 쪽이 뒤다.
            if (Vector3.Dot(transform.position - box.center, axis) > 0f) axis = -axis;
        }

        axis = Vector3.ProjectOnPlane(axis, Vector3.up).normalized;
        if (axis.sqrMagnitude < 1e-6f) axis = transform.forward;

        // 포신 길이 — 경계를 축에 투영.
        float halfLength = Mathf.Abs(axis.x) * box.extents.x + Mathf.Abs(axis.z) * box.extents.z;

        // 포미(뒤쪽 끝)에서 조금 들어간 윗면.
        Vector3 grip = box.center - axis * (halfLength * (1f - cannonGripAlong * 2f));
        grip.y = box.max.y + cannonGripLift;

        Vector3 side = Vector3.Cross(Vector3.up, axis).normalized * (cannonGripApart * 0.5f);

        // 사람이 포미 뒤에 서 있다고 보고, 사람 기준 왼쪽 · 오른쪽을 정한다.
        if (Vector3.Dot(side, transform.right) < 0f) side = -side;

        _leftAt = grip - side;
        _rightAt = grip + side;
        _lookAt = box.center + axis * halfLength;   // 포구 쪽을 본다
        _joltBack = -axis;
        _leftPalm = Vector3.down;
        _rightPalm = Vector3.down;
        _leftFingers = axis;     // 포구 쪽으로 손가락을 뻗어 포미 윗면을 덮는다
        _rightFingers = axis;

        // 너무 멀면 안 뻗는다 (아직 걸어오는 중, 또는 복제 지연).
        return FlatDistance(transform.position, grip) <= reachLimit;
    }

    private void Update()
    {
        _haveTargets = ComputeTargets();
        IsPosing = _haveTargets;
    }

    private void OnDisable()
    {
        if (_cannonTask != null)
        {
            _cannonTask.Recoiled -= OnCannonRecoiled;
        }

        if (character != null)
        {
            character.ModelJolt = Vector3.zero;
        }
    }

    /// <summary>대포가 튀었다. 내가 잡고 있을 때만 몸이 따라 흔들린다.</summary>
    private void OnCannonRecoiled()
    {
        if (_haveTargets && worker != null && ReferenceEquals(worker.Current, _cannonTask))
        {
            _joltAt = Time.time;
        }
    }

    /// <summary>
    /// 대포보다 <see cref="joltDelay"/> 늦게, 더 작게(<see cref="joltDistance"/>), 더 길게(<see cref="joltSeconds"/>)
    /// 몸이 뒤로 밀렸다 돌아온다. 대포는 쇠라 즉시 확 튀고, 사람은 발로 버티다 조금 밀리는 차이다.
    /// 값은 <see cref="ShipCoopCharacter.ModelJolt"/> 로 넣는다 — 몸의 로컬 위치는 그쪽이 매 프레임 덮어쓴다.
    /// </summary>
    private void UpdateJolt()
    {
        if (character == null)
        {
            return;
        }

        if (_joltAt < 0f)
        {
            character.ModelJolt = Vector3.zero;
            return;
        }

        float t = Time.time - _joltAt - joltDelay;

        if (t < 0f)
        {
            return;   // 아직 대포만 튀는 중
        }

        if (t >= joltSeconds)
        {
            _joltAt = -1f;
            character.ModelJolt = Vector3.zero;
            return;
        }

        // 빠르게 밀리고(앞 15%) 천천히 돌아온다. 끝은 부드럽게.
        float u = t / joltSeconds;
        float amount = u < 0.15f ? u / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.15f) / 0.85f);

        Vector3 world = _joltBack * (joltDistance * amount) + Vector3.up * (joltLift * amount);
        character.ModelJolt = transform.InverseTransformVector(world);
    }

    private void OnAnimatorIK(int layer)
    {
        if (_ikFrame != Time.frameCount)
        {
            _ikCallsLastFrame = _ikCalls;
            _ikCalls = 0;
            _ikFrame = Time.frameCount;
        }

        _ikCalls++;

        if (!_haveTargets)
        {
            return; // 드는 자세가 풀어 준다. 여기서 또 풀면 드는 자세를 지운다.
        }

        Reach(AvatarIKGoal.LeftHand, _leftAt, _leftFingers, _leftPalm, Quaternion.Euler(leftHandFixEuler));
        Reach(AvatarIKGoal.RightHand, _rightAt, _rightFingers, _rightPalm, Quaternion.Euler(rightHandFixEuler));
    }

    /// <param name="fingersToward">손가락이 뻗는 방향. 손바닥 방향과 **평행이면 안 된다.**</param>
    /// <param name="palmToward">손바닥이 보는 방향.</param>
    /// <summary>
    /// 월드 목표를 **IK 솔버가 보는 자리**로 바꾼다.
    ///
    /// ⚠ <b>휴머노이드 IK 는 아바타 원본 크기 · 원본 자세의 골격으로 푼다.</b> 이 캐릭터는 스킨이 Visual 아래에서
    ///    2.25배로 키워져 있고, 자리 자세가 Visual 을 돌리기까지 한다. 솔버는 그걸 모르고 루트 기준 1배 골격으로
    ///    목표를 해석해서, 어깨가 절반 높이에 있다고 보고 목표를 머리 위로 여겨 **팔을 하늘로 뻗었다.**
    ///    계측: 실제 팔 방향 (0.53, 0.85, 0.10) = 목표 − 어깨/2.25 방향 (0.52, 0.85, 0.09). 딱 맞았다.
    ///    그래서 루트 기준으로 되돌리고(회전 역), 배율로 나눠서 넘긴다. 결과 손 자리는 다시 2.25배 · 회전되어
    ///    원래 목표에 닿는다.
    /// </summary>
    private Vector3 ToSolver(Vector3 worldAt)
    {
        Vector3 fromRoot = worldAt - transform.position;
        return transform.position + Quaternion.Inverse(ModelSpin) * fromRoot / RigScale;
    }

    /// <summary>Visual 이 쉴 때보다 얼마나 더 돌아가 있나 (월드). 자리 자세가 몸을 돌린 만큼이다.</summary>
    private Quaternion ModelSpin
    {
        get
        {
            if (_model == null) return Quaternion.identity;
            Quaternion restWorld = transform.rotation * _modelRestLocalRotation;
            return _model.rotation * Quaternion.Inverse(restWorld);
        }
    }

    /// <summary>스킨이 루트보다 몇 배 큰가. Visual 2.25.</summary>
    private float RigScale
    {
        get
        {
            if (_model == null) return 1f;
            float root = Mathf.Max(transform.lossyScale.y, 1e-4f);
            return Mathf.Max(_model.lossyScale.y / root, 1e-4f);
        }
    }

    private void Reach(AvatarIKGoal goal, Vector3 at, Vector3 fingersToward, Vector3 palmToward, Quaternion boneFix)
    {
        _animator.SetIKPositionWeight(goal, strength);
        _animator.SetIKPosition(goal, ToSolver(at));

        // ⚠ LookRotation(손가락, 손바닥) 은 두 축이 평행이면 정의되지 않는다. 밧줄에서 손가락 · 손바닥을
        //    둘 다 "돛대 쪽" 으로 줬더니 손목이 아무 각도로 돌아가 **팔이 어깨에서 떨어져 보였다.**
        //    평행에 가까우면 회전을 걸지 않고 자리만 잡는다 — 애니메이션 손목이 그대로 남는 쪽이 낫다.
        Vector3 fingers = Vector3.ProjectOnPlane(fingersToward, palmToward);

        if (fingers.sqrMagnitude < 1e-4f)
        {
            _animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        _animator.SetIKRotationWeight(goal, strength);
        // 회전도 솔버 기준으로 — 몸을 돌린 만큼 되돌려 넘긴다. (배율은 회전에 영향 없음)
        Quaternion worldRot = Quaternion.LookRotation(fingers.normalized, palmToward) * boneFix;
        _animator.SetIKRotation(goal, Quaternion.Inverse(ModelSpin) * worldRot);
    }

    private void LateUpdate()
    {
        UpdateJolt();
        LogHands();

        if (_model == null)
        {
            return;
        }

        if (_haveTargets)
        {
            // 보이는 몸만 물건 쪽으로 돈다. 루트는 네트워크가 옮기므로 건드리지 않는다.
            Vector3 flat = Vector3.ProjectOnPlane(_lookAt - transform.position, Vector3.up);

            if (flat.sqrMagnitude > 1e-4f)
            {
                Quaternion want = Quaternion.LookRotation(flat.normalized, Vector3.up);
                _model.rotation = Quaternion.RotateTowards(_model.rotation, want, turnSpeed * Time.deltaTime);
                _modelTurned = true;
            }
        }
        else if (_modelTurned)
        {
            // 자리를 떠났다. 몸을 원래 방향으로 되돌린다.
            _model.localRotation = Quaternion.RotateTowards(_model.localRotation, _modelRestLocalRotation, turnSpeed * Time.deltaTime);

            if (Quaternion.Angle(_model.localRotation, _modelRestLocalRotation) < 0.5f)
            {
                _model.localRotation = _modelRestLocalRotation;
                _modelTurned = false;
            }
        }
    }
}
