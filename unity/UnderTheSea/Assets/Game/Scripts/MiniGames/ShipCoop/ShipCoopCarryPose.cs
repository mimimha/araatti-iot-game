using UnityEngine;

/// <summary>
/// 🙌 무언가를 들면 **두 팔로 안는 자세**를 만든다. (SHIPCOOP.md 4장)
///
/// ⚠ **머리 위에 띄워두면 안 됩니다.**
///
///    처음에는 들고 있는 표시를 머리 위에 띄웠습니다. 보이기는 하는데
///    **팔은 가만히 있고 물건만 떠다녀서** 드는 것으로 안 읽힙니다.
///
/// ⚠ **드는 애니메이션이 없습니다.**
///
///    캐릭터 컨트롤러(`Character_Movement`)에 있는 클립은 걷기 · 달리기 ·
///    점프 · 공격뿐입니다. 드는 동작이 없습니다.
///    그래서 클립 대신 **IK 로 손을 끌어다** 자세를 만듭니다.
///
///    걷는 애니메이션은 그대로 돌고, 팔만 앞으로 모입니다. 클립을 새로
///    만들지 않아도 되고, 걸으면서 드는 것도 저절로 됩니다.
///
/// ⚠ **애니메이터 레이어에 IK Pass 가 켜져 있어야 합니다.**
///    꺼져 있으면 <see cref="OnAnimatorIK"/> 자체가 안 불립니다.
///    배치 도구가 켜줍니다. (`ShipCoopDeckLayout`)
/// </summary>
[RequireComponent(typeof(Animator))]
public class ShipCoopCarryPose : MonoBehaviour
{
    [Header("연결 — 비워두면 부모에서 찾는다")]
    [SerializeField] private CarryTask carry;

    [Tooltip("손에 들릴 것. 비워두면 CarryTask 가 쓰는 것을 그대로 쓴다.")]
    [SerializeField] private Transform held;

    [Header("자세")]
    [Tooltip("가슴에서 앞으로 이만큼 떨어진 곳을 잡는다 (사람 키 대비 비율).")]
    [SerializeField, Range(0.05f, 0.5f)] private float reach = 0.16f;

    [Tooltip("가슴에서 아래로 이만큼 내린 곳을 잡는다 (사람 키 대비 비율).")]
    [SerializeField, Range(-0.3f, 0.3f)] private float drop = 0.04f;

    [Tooltip("두 손을 이만큼 벌린다 (사람 키 대비 비율).")]
    [SerializeField, Range(0.02f, 0.3f)] private float apart = 0.09f;

    // ⚠ 1 로 두면 팔이 **딱 굳습니다.** 걸을 때 몸이 흔들려도 손만 제자리라
    //    어색합니다. 0.85 쯤 두면 원래 동작이 조금 배어 나옵니다.
    [Tooltip("IK 를 얼마나 강하게 걸지. 1 이면 팔이 완전히 굳는다.")]
    [SerializeField, Range(0f, 1f)] private float strength = 0.85f;

    private Animator _animator;
    private Transform _chest;
    private Transform _leftHand;
    private Transform _rightHand;
    private float _tall = 2.7f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        if (carry == null)
        {
            carry = GetComponentInParent<CarryTask>();
        }

        if (!_animator.isHuman)
        {
            Debug.LogWarning($"[{name}] 휴머노이드 리그가 아닙니다. 드는 자세를 못 만듭니다.", this);
            enabled = false;
            return;
        }

        _chest = _animator.GetBoneTransform(HumanBodyBones.Chest)
                 ?? _animator.GetBoneTransform(HumanBodyBones.Spine);

        // 물건은 IK 목표점이 아니라 **실제 손뼈 사이**에 놓는다. (BetweenHands 참고)
        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        // 사람 키를 재서 거리들을 거기에 맞춘다. 캐릭터를 키워도 따라간다.
        Renderer[] draws = GetComponentsInChildren<Renderer>();

        if (draws.Length > 0)
        {
            Bounds box = draws[0].bounds;

            for (int i = 1; i < draws.Length; i++)
            {
                box.Encapsulate(draws[i].bounds);
            }

            _tall = Mathf.Max(box.size.y, 0.5f);
        }
    }

    /// <summary>지금 들고 있는지. 들고 있을 때만 팔을 모은다.</summary>
    private bool Carrying => carry != null && carry.IsCarrying;

    /// <summary>두 손이 만나는 곳. 물건은 여기에 놓인다.</summary>
    private Vector3 HoldPoint
    {
        get
        {
            Transform from = _chest != null ? _chest : transform;

            return from.position
                   + transform.forward * (_tall * reach)
                   + Vector3.down * (_tall * drop);
        }
    }

    private void OnAnimatorIK(int layer)
    {
        if (!Carrying)
        {
            // 안 들고 있으면 IK 를 풀어야 한다. 안 풀면 빈손으로도 팔이 모인 채 남는다.
            Release(AvatarIKGoal.LeftHand);
            Release(AvatarIKGoal.RightHand);
            return;
        }

        Vector3 middle = HoldPoint;
        Vector3 side = transform.right * (_tall * apart);

        // 왼손은 오른쪽(가운데)을, 오른손은 왼쪽(가운데)을 향한다.
        Reach(AvatarIKGoal.LeftHand, middle - side, transform.right);
        Reach(AvatarIKGoal.RightHand, middle + side, -transform.right);
    }

    /// <param name="palmToward">손바닥이 향할 방향. 두 손이 물건을 양옆에서 감싸도록 서로 마주 본다.</param>
    private void Reach(AvatarIKGoal goal, Vector3 at, Vector3 palmToward)
    {
        _animator.SetIKPositionWeight(goal, strength);
        _animator.SetIKRotationWeight(goal, strength);
        _animator.SetIKPosition(goal, at);

        // ⚠ 두 손에 같은 회전을 주면 손바닥이 둘 다 위를 봐서 쟁반처럼 받치는 모양이 된다.
        //    손가락은 앞으로, 손바닥은 가운데(서로)를 향하게 손마다 따로 돌린다.
        _animator.SetIKRotation(goal, Quaternion.LookRotation(transform.forward, palmToward));
    }

    private void Release(AvatarIKGoal goal)
    {
        _animator.SetIKPositionWeight(goal, 0f);
        _animator.SetIKRotationWeight(goal, 0f);
    }

    /// <summary>
    /// 물건을 놓을 곳 — **실제 두 손뼈의 가운데.**
    ///
    /// ⚠ IK 목표점(<see cref="HoldPoint"/>)에 놓으면 안 됩니다.
    ///    IK 는 <see cref="strength"/> 만큼만 끌어가서 손이 목표점에 딱 닿지 않고,
    ///    팔 길이가 모자라면 더 못 갑니다. 그러면 물건이 손 아래나 앞에 떠 있게 됩니다.
    ///    실제로 그렇게 보였습니다. 손이 어디에 멈췄든 그 사이에 놓아야 "들고 있다" 로 읽힙니다.
    /// </summary>
    private Vector3 BetweenHands
    {
        get
        {
            if (_leftHand != null && _rightHand != null)
            {
                return (_leftHand.position + _rightHand.position) * 0.5f;
            }

            return HoldPoint;
        }
    }

    // ⚠ LateUpdate 에서 옮깁니다. 애니메이터가 뼈를 다 쓴 **뒤**여야
    //    손 자리가 확정됩니다. Update 에서 옮기면 한 프레임씩 늦게 따라옵니다.
    private void LateUpdate()
    {
        Transform show = held != null ? held : CurrentHeld();

        if (show == null || !show.gameObject.activeInHierarchy)
        {
            return;
        }

        // 물건의 **가운데**가 손 사이에 오게 한다. 모델 원점이 바닥에 있는 것(양동이)도 손에 매달리지 않는다.
        show.rotation = transform.rotation;

        Renderer[] draws = show.GetComponentsInChildren<Renderer>();

        if (draws.Length == 0)
        {
            show.position = BetweenHands;
            return;
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        show.position += BetweenHands - box.center;
    }

    /// <summary>
    /// CarryTask 가 지금 보여 주는 표시. **매 프레임 다시 묻는다** — 물이면 양동이, 그 밖은 캡슐로
    /// 종류에 따라 바뀌기 때문이다. Inspector 에 <c>held</c> 를 직접 넣었으면 그것이 이긴다.
    /// </summary>
    private Transform CurrentHeld()
    {
        if (carry == null || carry.HeldVisual == null)
        {
            return null;
        }

        return carry.HeldVisual.transform;
    }
}
