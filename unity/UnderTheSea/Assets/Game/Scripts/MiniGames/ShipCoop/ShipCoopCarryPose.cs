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

        Reach(AvatarIKGoal.LeftHand, middle - side);
        Reach(AvatarIKGoal.RightHand, middle + side);
    }

    private void Reach(AvatarIKGoal goal, Vector3 at)
    {
        _animator.SetIKPositionWeight(goal, strength);
        _animator.SetIKRotationWeight(goal, strength);
        _animator.SetIKPosition(goal, at);

        // 손바닥이 가운데를 향하도록. 안 돌리면 손등으로 받치는 모양이 된다.
        _animator.SetIKRotation(goal, Quaternion.LookRotation(transform.forward, transform.up));
    }

    private void Release(AvatarIKGoal goal)
    {
        _animator.SetIKPositionWeight(goal, 0f);
        _animator.SetIKRotationWeight(goal, 0f);
    }

    // ⚠ LateUpdate 에서 옮깁니다. 애니메이터가 뼈를 다 쓴 **뒤**여야
    //    손 자리가 확정됩니다. Update 에서 옮기면 한 프레임씩 늦게 따라옵니다.
    private void LateUpdate()
    {
        Transform show = held != null ? held : FindHeld();

        if (show == null || !show.gameObject.activeInHierarchy)
        {
            return;
        }

        show.position = HoldPoint;
        show.rotation = transform.rotation;
    }

    // CarryTask 가 쓰는 표시를 찾아 둔다. 한 번만 찾는다.
    private Transform FindHeld()
    {
        if (carry == null)
        {
            return null;
        }

        held = carry.HeldVisual != null ? carry.HeldVisual.transform : null;
        return held;
    }
}
