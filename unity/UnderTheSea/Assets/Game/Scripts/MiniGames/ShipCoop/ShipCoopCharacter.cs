using UnityEngine;

/// <summary>
/// 보이는 몸. **움직인 만큼 애니메이션을 굴린다.** (SHIPCOOP.md 4장)
///
/// 캐릭터 모델(`P_JaeYoung`)에는 원래 `CharacterMover` 와 `MovePlayerInput` 이
/// 같이 붙어 있습니다. 그대로 두면 키보드를 **두 군데서 읽어서** 한 번 누를 때
/// 두 배로 걷거나 서로 밀어냅니다. 그래서 그 둘은 꺼두고, 움직이는 일은
/// 우리 쪽(`DebugPlayerMover`, 나중에는 서버의 이동)이 맡습니다.
///
/// 애니메이션만 남습니다. 그게 이 파일이 하는 일입니다.
///
/// <code>
///   Hor · Vert   몸 기준 이동 방향 (-1 ~ 1)
///   State        0 = 걷기, 1 = 달리기
///   IsJump       점프. 이 게임에는 없다
/// </code>
///
/// ⚠ **누가 움직이는지 묻지 않습니다.**
///
///    이동 코드를 붙잡고 "지금 달리는 중?" 을 물어보면, 서버가 붙어서 이동이
///    `PlayerMovement` 로 넘어갈 때 이 파일도 같이 고쳐야 합니다. (11장)
///    대신 **실제로 움직인 거리**를 재서 씁니다. 누가 움직였든 상관없습니다.
///
/// ⚠ 다만 **네트워크에서는 재는 것만으로 모자랍니다.**
///
///    원격 캐릭터의 자리는 Fusion 이 보간해서 채웁니다. 이 스크립트가 재는 프레임 간
///    차이가 실제 이동과 어긋나서, 남의 화면에서는 자리만 움직이고 다리는 가만히 있습니다.
///    그래서 네트워크 쪽에서는 `DriveMotion` 으로 **서버가 확정한 속도**를 넣어 줍니다.
///    넣어 주지 않으면 예전처럼 스스로 잽니다. 혼자 하는 씬은 하나도 안 바뀝니다.
/// </summary>
public class ShipCoopCharacter : MonoBehaviour
{
    [Header("굴릴 애니메이터")]
    [Tooltip("비워두면 자식에서 찾는다.")]
    [SerializeField] private Animator animator;

    [Header("속도 기준 (m/s)")]
    [Tooltip("이 속도면 걷는 애니메이션이 제 속도로 나온다. 이동 쪽 walkSpeed 와 맞춘다.")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 4f;

    [Tooltip("이 속도를 넘으면 달리는 것으로 본다.\n" +
             "걷기 4 와 달리기 7 의 사이에 둔다. 경사로에서 조금 느려져도 안 흔들린다.")]
    [SerializeField, Min(0.1f)] private float runSpeed = 5.5f;

    [Header("애니메이터 파라미터 이름")]
    [Tooltip("에셋 쪽 CharacterMover 가 쓰던 이름 그대로다. 컨트롤러를 바꾸면 여기도 바꾼다.")]
    [SerializeField] private string horizontalId = "Hor";

    [SerializeField] private string verticalId = "Vert";
    [SerializeField] private string stateId = "State";
    [SerializeField] private string isRepairingId = "IsRepairing";

    [Tooltip("수리 자세일 때 몸을 이만큼 더 띄운다 (m).\n\n" +
             "이 캐릭터는 몸통이 유난히 크고 둥글어서, 무릎을 깊이 굽히는 수리 자세를 그대로 두면\n" +
             "몸통이 갑판을 파고들어 보인다. 그만큼 보정해서 띄운다.")]
    [SerializeField, Range(0f, 0.6f)] private float repairLift = 0.3f;

    /// <summary>지금 수리 자세인지. SinkModel 이 이걸 보고 몸을 더 띄운다.</summary>
    private bool _repairing;

    [Header("부드럽게 섞는 속도")]
    [Tooltip("값이 확 바뀌면 다리가 튄다. 에셋 쪽과 같은 4.5 를 쓴다.")]
    [SerializeField, Min(0.1f)] private float blendSpeed = 4.5f;

    // ------------------------------------------------------------
    // 발밑 그림자
    //
    // ⚠ **캐릭터가 떠 보이는 진짜 이유는 자리가 아니라 그림자입니다.**
    //
    //    발끝은 갑판에서 2mm 위에 정확히 놓여 있는데도 떠 보였습니다.
    //    해가 비스듬해서 그림자가 발밑이 아니라 **옆으로 뚝 떨어져** 있기 때문입니다.
    //    발이 닿는 자리에 어두운 자국이 없으면 사람 눈은 떠 있다고 읽습니다.
    //
    //    그래서 발밑에 **둥근 그림자 한 장**을 깔고 바닥에 붙여 따라다니게 합니다.
    //    해 그림자와 별개입니다. 이건 "여기가 닿는 자리" 라고 말해주는 표시입니다.
    // ------------------------------------------------------------

    // ------------------------------------------------------------
    // 발을 **보이는 갑판에** 붙인다
    //
    // ⚠ 왜 필요한가 — 걷는 바닥과 보이는 갑판이 **다른 물건**이기 때문입니다.
    //
    //    걷는 바닥은 보이지 않는 평평한 큐브입니다. 배의 콜라이더가 울퉁불퉁해서
    //    그렇게 해 두었습니다. (ShipCoopDeckLayout)
    //    그런데 배의 **보이는** 갑판은 층마다, 자리마다 조금씩 다릅니다.
    //    한 층에 맞추면 다른 층이 어긋납니다. 실제로 중간갑판 39cm,
    //    앞갑판 2cm 로 **37cm 나 달랐습니다.**
    //
    //    그래서 고정값으로 맞추지 않습니다. **매 프레임 배의 갑판을 찾아서**
    //    거기에 발을 놓습니다. 어느 층 어느 자리든 저절로 맞습니다.
    //
    //    캡슐은 그대로 둡니다. 부딪히는 것과 걷는 것은 하나도 안 바뀝니다.
    //    **보이는 몸만** 오르내립니다.
    // ------------------------------------------------------------

    [Header("발을 보이는 갑판에 붙이기")]
    [Tooltip("보이는 몸. 비워두면 자식 Body 를 찾는다.")]
    [SerializeField] private Transform model;

    [Tooltip("배의 갑판을 못 찾았을 때 쓸 값 (m).\n" +
             "CharacterController 가 바닥에서 띄우는 여유(skinWidth)만큼이다.")]
    [SerializeField, Range(0f, 0.6f)] private float modelSink = 0.05f;

    [Tooltip("이만큼 아래까지 갑판을 찾는다 (m). 계단참이나 상자 위도 잡히게 넉넉히.")]
    [SerializeField, Min(0.1f)] private float floorReach = 1.2f;

    [Tooltip("한 번에 이만큼까지만 움직인다 (m/초).\n\n" +
             "갑판이 갑자기 바뀌면 몸이 순간이동한다. 천천히 따라가게 한다.")]
    [SerializeField, Min(0.1f)] private float sinkSpeed = 6f;

    /// <summary>지금 내려가 있는 깊이. 목표를 향해 천천히 간다.</summary>
    private float _sink;

    /// <summary>보이는 몸. 자리 자세(<c>ShipCoopStationPose</c>)가 몸만 돌려 대포 · 조타륜을 보게 할 때 쓴다.</summary>
    public Transform Model => model;

    /// <summary>
    /// 보이는 몸을 잠깐 밀어 두는 양 (로컬). 대포가 튈 때 몸이 같이 덜컹하는 데 쓴다.
    /// 몸의 로컬 위치는 매 프레임 여기(SinkModel)서 덮어쓰므로, 밖에서 직접 옮기면 지워진다 — 이 값으로 넣는다.
    /// </summary>
    public Vector3 ModelJolt { get; set; }

    [Header("발밑 그림자")]
    [Tooltip("발밑에 깔 둥근 판. 비워두면 이 기능을 쓰지 않는다.")]
    [SerializeField] private Transform footShadow;

    [Tooltip("바닥에서 이만큼 띄운다. 0 이면 갑판과 서로 지지며 깜빡인다.")]
    [SerializeField, Min(0f)] private float shadowLift = 0.02f;

    [Tooltip("이보다 멀리 떨어지면 그림자를 끈다. 떨어지는 중에는 바닥이 없다.")]
    [SerializeField, Min(0.5f)] private float shadowReach = 4f;

    /// <summary>지난 프레임의 자리. 여기서 속도를 낸다.</summary>
    private Vector3 _wasAt;

    private Vector2 _axis;
    private float _state;

    /// <summary>밖에서 넣어 준 속도와 그 프레임. 안 들어오면 스스로 잰다.</summary>
    private Vector3 _drivenVelocity;

    private int _drivenFrame = -1;

    /// <summary>
    /// **이 캐릭터가 지금 어디로 얼마나 빨리 가는지**를 밖에서 알려준다. (m/s, 월드 기준)
    ///
    /// 네트워크에서 쓴다. 서버가 확정한 속도를 모든 화면이 그대로 받아 애니메이션을 굴리면,
    /// 내 화면과 남의 화면에서 같은 사람이 같은 걸음을 걷는다.
    ///
    /// 매 프레임 불러야 한다. 끊기면 다음 프레임부터 스스로 재는 쪽으로 돌아간다.
    /// </summary>
    public void DriveMotion(Vector3 worldVelocity)
    {
        worldVelocity.y = 0f;
        _drivenVelocity = worldVelocity;
        _drivenFrame = Time.frameCount;
    }

    /// <summary>
    /// 수리 자세(웅크려 망치질)를 켜고 끈다. <c>RepairTask</c>가 붙고 뗄 때 부른다.
    /// </summary>
    public void SetRepairing(bool repairing)
    {
        _repairing = repairing;

        if (animator != null)
        {
            animator.SetBool(isRepairingId, repairing);
        }
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        _wasAt = transform.position;
    }

    private void OnEnable()
    {
        // 자리를 옮겨놓고 켜면 첫 프레임에 엄청난 속도로 잡힌다.
        _wasAt = transform.position;
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        // 위아래(계단)는 빼고 바닥에서 움직인 거리만 본다.
        Vector3 moved = transform.position - _wasAt;
        moved.y = 0f;
        _wasAt = transform.position;

        Vector3 heading;
        float speed;

        // 넣어 준 값이 있으면 그것을 믿는다. 없으면 예전처럼 잰 값을 쓴다.
        if (_drivenFrame >= Time.frameCount - 1)
        {
            heading = _drivenVelocity.sqrMagnitude > 0.0001f ? _drivenVelocity.normalized : Vector3.zero;
            speed = _drivenVelocity.magnitude;
        }
        else
        {
            heading = moved.normalized;
            speed = moved.magnitude / deltaTime;
        }

        // 몸 기준으로 바꾼다. 우리 캐릭터는 가는 쪽을 보고 걸으므로 거의 앞(+z)이다.
        Vector3 local = transform.InverseTransformDirection(heading) * Mathf.Clamp01(speed / walkSpeed);

        Vector2 wantAxis = new Vector2(local.x, local.z);
        float wantState = speed > runSpeed ? 1f : 0f;

        _axis = Vector2.MoveTowards(_axis, wantAxis, blendSpeed * deltaTime);
        _state = Mathf.MoveTowards(_state, wantState, blendSpeed * deltaTime);

        animator.SetFloat(horizontalId, _axis.x);
        animator.SetFloat(verticalId, _axis.y);
        animator.SetFloat(stateId, _state);
    }

    private void LateUpdate()
    {
        SinkModel();
        DropShadow();
    }

    /// <summary>
    /// 발을 **배의 보이는 갑판** 높이에 맞춘다.
    ///
    /// 걷는 바닥(보이지 않는 큐브)이 아니라 **눈에 보이는 갑판**을 찾습니다.
    /// 둘이 다르기 때문에 이 파일이 있는 것입니다. (위 주석)
    /// </summary>
    private void SinkModel()
    {
        if (model == null)
        {
            model = transform.Find("Body");

            if (model == null)
            {
                return;
            }
        }

        float want = modelSink;

        // ⚠ **수리 중에는 갑판 찾기를 아예 끈다.**
        //    대포 · 조타륜처럼 자리 근처에 물건이 많은 곳(앞갑판 · 뒷갑판)에서는 발밑 레이가
        //    그 물건에 걸려 자리마다 다른 값을 낸다. 서 있을 때는 안 보이던 차이가, 웅크려서
        //    한참 붙어 있는 수리 자세에서는 그대로 자리 잡아 파고들어 보였다.
        //    수리 중엔 항상 같은 기본값(modelSink)에서 repairLift 만큼만 띄운다.
        if (!_repairing)
        {
            float deck;

            if (FindDeckUnderFoot(out deck))
            {
                // 발(캡슐 밑바닥)은 이 오브젝트의 원점에 있다. 갑판까지의 거리가 곧 내릴 깊이다.
                want = transform.position.y - deck;
            }
        }

        // 갑판이 갑자기 바뀌어도 몸이 순간이동하지 않게 천천히 따라간다.
        _sink = Mathf.MoveTowards(_sink, want, sinkSpeed * Time.deltaTime);

        float lift = _repairing ? repairLift : 0f;
        model.localPosition = new Vector3(0f, -_sink + lift, 0f) + ModelJolt;
    }

    /// <summary>발밑에서 배의 갑판을 찾는다. 걷는 큐브와 내 몸은 빼고 본다.</summary>
    private bool FindDeckUnderFoot(out float deckY)
    {
        deckY = 0f;

        // 발보다 조금 위에서 쏜다. 갑판에 살짝 파묻혀 있어도 잡히게.
        Vector3 from = transform.position + Vector3.up * 0.3f;

        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, floorReach + 0.3f,
                                               ~0, QueryTriggerInteraction.Ignore);

        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform what = hits[i].collider.transform;

            // 내 몸은 뺀다.
            if (what == transform || what.IsChildOf(transform))
            {
                continue;
            }

            // 보이지 않는 것(걷는 큐브·경사로·벽)은 뺀다. 우리가 찾는 것은 **보이는** 갑판이다.
            Renderer draw = hits[i].collider.GetComponent<Renderer>();

            if (draw == null || !draw.enabled)
            {
                continue;
            }

            // ⚠ **밟을 수 있는 면만** 본다.
            //    배 콜라이더가 전부 켜져 있어서 난간 살이나 밧줄도 레이에 걸립니다.
            //    위를 보고 있는 면이 아니면 바닥이 아닙니다.
            if (hits[i].normal.y < 0.7f)
            {
                continue;
            }

            if (!found || hits[i].point.y > deckY)
            {
                deckY = hits[i].point.y;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// 발밑 그림자를 바닥에 붙인다.
    ///
    /// 캐릭터를 따라다니되 **회전은 따라가지 않습니다.** 몸이 돌 때 그림자까지
    /// 같이 돌면 바닥에 붙어 있는 느낌이 사라집니다.
    /// </summary>
    private void DropShadow()
    {
        if (footShadow == null)
        {
            return;
        }

        // 자기 캡슐에 맞지 않게 조금 위에서 쏜다.
        Vector3 from = transform.position + Vector3.up * 0.3f;

        RaycastHit hit;
        bool found = Physics.Raycast(from, Vector3.down, out hit, shadowReach + 0.3f,
                                     ~0, QueryTriggerInteraction.Ignore)
                     && !hit.collider.transform.IsChildOf(transform);

        if (!found)
        {
            if (footShadow.gameObject.activeSelf)
            {
                footShadow.gameObject.SetActive(false);
            }

            return;
        }

        if (!footShadow.gameObject.activeSelf)
        {
            footShadow.gameObject.SetActive(true);
        }

        footShadow.position = hit.point + Vector3.up * shadowLift;

        // ⚠ **눕혀야 한다.** Quad 는 세워진 채로 만들어지므로 90도 눕힌다.
        //    처음에 identity 로 두었더니 바닥에 안 눕고 **서 있어서** 안 보였다.
        //    몸을 따라 돌리지는 않는다. 같이 돌면 바닥에 붙은 느낌이 사라진다.
        footShadow.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
