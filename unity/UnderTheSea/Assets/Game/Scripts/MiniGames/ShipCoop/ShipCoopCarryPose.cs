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
/// <code>
///   🪣 양동이   가슴 앞에서 두 손으로 안는다. 손바닥이 서로를 본다
///   ⚫ 포탄     팔을 앞으로 쭉 뻗고, 손가락이 팔뚝을 이어가게(손목 안 꺾이게) 두고 손바닥만 공을 보게 한다
///              (손가락을 감아쥐는 것은 IK 로 안 된다 — 손바닥 방향까지가 한계다)
///   🪵 판자     팔을 굽히지 않고 앞으로 쭉 뻗어, 손바닥을 위로. 판자가 두 손 위에 얹힌다
///              (안으면 팔이 짧아서 1.77m 판자가 몸을 뚫는다)
/// </code>
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

    // ⚠ **판자는 안지 않고 앞으로 쭉 뻗어 받칩니다.**
    //    판자(1.77m)를 가슴 앞에서 두 손으로 안으면, 이 캐릭터는 팔이 짧아서 판자가 몸을 뚫고 나왔다.
    //    그래서 판자일 때는 팔을 굽히지 않고 **어깨 → 팔 길이만큼 앞**에 손을 두고,
    //    손바닥을 위로 돌려 두 손 위에 판자를 얹는다. 팔 길이는 어깨 → 팔꿈치 → 손 뼈 사이 거리로 잰다.
    //
    // ⚠ **어깨 높이 그대로 뻗으면 판자가 얼굴에 묻힙니다.** 어깨는 목 바로 아래라, 손 위에 얹은 판자가
    //    턱 높이로 올라와 화면에서 얼굴을 가렸다. 그래서 어깨에서 plankDrop 만큼 내려 **가슴 아래**로 뻗는다.
    //    사람이 판자를 앞으로 들 때의 높이이기도 하다.
    [Header("판자 — 앞으로 쭉, 손바닥 위로")]
    [Tooltip("판자를 들 때 어깨에서 팔 길이의 몇 배 앞에 손을 둘지. 1 이면 팔이 완전히 펴진다 — 조금 남겨야 팔꿈치가 안 튄다.")]
    [SerializeField, Range(0.5f, 1f)] private float plankStraight = 0.95f;

    [Tooltip("어깨에서 이만큼 아래로 내려 뻗는다 (사람 키 대비 비율). 0 이면 어깨 높이 — 판자가 얼굴에 묻힌다.")]
    [SerializeField, Range(0f, 0.3f)] private float plankDrop = 0.14f;

    [Tooltip("판자 밑면을 손바닥에서 이만큼 띄운다 (m). 0 이면 손 안에 파묻힌다.")]
    [SerializeField, Range(0f, 0.2f)] private float plankRest = 0.03f;

    // ⚠ **포탄은 팔을 쭉 뻗고, 손바닥이 공을 보게 합니다.**
    //
    //    손목 각도로 "공을 감싸는" 모양을 만들어 보려고 한참 돌렸는데 화면이 하나도 안 바뀌었다.
    //    이유는 두 가지였다.
    //
    //      1. **두 손이 공 안에 파묻혀 있었다.** 포탄 지름 0.6m(ShipCoopDeckLayout.HeldSize) → 반지름 0.30m
    //         인데 손 자리는 키 2.86 × apart 0.09 = 가운데에서 0.257m. 0.257 < 0.30 이라 손목이 공 **속**이었다.
    //         각도를 어떻게 줘도 보일 리가 없었다.
    //      2. 파묻히지 않게 벌린 뒤에도, **손가락을 감아쥘 방법이 IK 에는 없다.** 손목을 아무리 돌려도
    //         편 손바닥이 방향만 바꿀 뿐이다.
    //
    //    그래서 감싸는 시도는 접었다. 팔을 앞으로 쭉 뻗고, 손바닥 방향만 맞추고(아래),
    //    공을 두 손 사이에 붙인다. 손 자리는 **들고 있는 것의 실제 크기**에서 내므로 공에 안 묻히고,
    //    포탄 크기를 바꿔도 따라간다.
    [Header("포탄 — 팔을 쭉 뻗고 손에 붙인다")]
    [Tooltip("포탄 표면에서 손목을 이만큼 더 바깥에 둔다 (m). 0 이면 손목이 표면에 딱 붙어 손이 반쯤 묻힌다.")]
    [SerializeField, Range(0f, 0.3f)] private float ammoGrip = 0.04f;

    [Tooltip("포탄을 들 때 어깨에서 팔 길이의 몇 배 앞에 손을 둘지. 1 이면 팔이 완전히 펴진다.")]
    [SerializeField, Range(0.5f, 1f)] private float ammoStraight = 0.9f;

    [Tooltip("어깨에서 이만큼 아래로 내려 뻗는다 (사람 키 대비 비율).")]
    [SerializeField, Range(0f, 0.3f)] private float ammoDrop = 0.1f;

    // ⚠ **손 회전은 IK 로 주지 않는다 — 안 먹는다.**
    //
    //    손목 각도를 부호만 바꿔 가며 여러 번 고쳤는데 화면이 안 바뀌었다. 계측해 보니 이유가 나왔다:
    //    요청한 회전이 결과에 거의 반영되지 않는다. 손가락을 "앞 +0.96" 으로 요청했는데 결과는 "위 +0.97",
    //    손바닥은 "오른 +1.00" 을 요청했는데 결과는 "앞 −0.83" 이었다. 휴머노이드 IK 는 아바타의 관절 한계
    //    안으로 요청을 잘라내는데, 이 리그는 팔이 짧고 한계가 좁아 손 회전이 통째로 뭉개진다.
    //
    //    그래서 **자리만 IK 로 잡고, 손 방향은 LateUpdate 에서 손뼈를 직접 돌린다.** (AimPalms)
    //    직접 돌리면 그 잘라냄을 안 거친다. 각도도 추측하지 않는다 — 손가락 · 엄지 뼈로 지금 손바닥이
    //    어디를 보는지 재고, 공을 보려면 몇 도가 필요한지 계산해서 그만큼만 돌린다.

    // ⚠ **손뼈 축 보정.** SetIKRotation 은 손뼈의 월드 회전을 그대로 정한다. Reach() 는 "손뼈 +Z 가 손가락,
    //    +Y 가 손바닥" 이라고 놓고 LookRotation(앞, 손바닥쪽) 을 주는데, 리그마다 뼈 축이 다르다.
    //    보정 없이 줬을 때 ithappy Cute_Characters 는 두 손이 수평(손바닥 아래)으로 나왔다.
    //    그래서 리그의 실제 축을 재서 (+Z 손가락, +Y 손바닥) 으로 돌려 놓는 회전을 뒤에 곱한다.
    //    왼손 · 오른손은 미러라 값이 다르다. **캐릭터를 바꾸면 다시 잰다** — Tools/ShipCoop/손뼈 축 재기.
    //
    //    기본값의 근거 (ithappy Cute_Characters, 2026-09-15 잼 — ShipCoopHandAxisMeasure):
    //      손가락 방향 = 손뼈 로컬 +Y (0.99), 손바닥 노멀 = 왼손 -X · 오른손 +X (0.88).
    //      즉 이 리그는 "+Y 가 손가락, ±X 가 손바닥" 이다. 코드가 기대하는 "+Z 손가락, +Y 손바닥" 으로 돌려 놓으면
    //        왼손  (+Y→+Z, -X→+Y) = Euler (0, -90, -90)
    //        오른손 (+Y→+Z, +X→+Y) = Euler (0,  90,  90)
    //      엄지뼈로 잰 날것은 (26.7, ∓86.5, ∓81.9) 였다 — 엄지가 손바닥 평면에서 27° 떠 있어서 그만큼 기울었다.
    //      리그 축은 정확히 ±X 에 붙어 있으니 깨끗한 축으로 붙였다. 손이 27° 돌아 보이면 날것으로 바꿔 본다.
    [Header("손뼈 축 보정 — 리그가 바뀌면 다시 잰다 (Tools/ShipCoop/손뼈 축 재기)")]
    [Tooltip("왼손뼈의 (손가락, 손바닥) 축을 (+Z, +Y) 로 돌려 놓는 회전 (Euler). 손뼈 축 재기 도구가 값을 낸다.")]
    [SerializeField] private Vector3 leftHandFixEuler = new Vector3(0f, -90f, -90f);

    [Tooltip("오른손뼈의 (손가락, 손바닥) 축을 (+Z, +Y) 로 돌려 놓는 회전 (Euler). 미러라 왼손과 부호가 다르다.")]
    [SerializeField] private Vector3 rightHandFixEuler = new Vector3(0f, 90f, 90f);

    [Tooltip("Play 중 두 손뼈의 로컬 축을 그린다 (빨강 X · 초록 Y · 파랑 Z). 보정값을 잴 때만 켠다.")]
    [SerializeField] private bool drawHandAxes = false;

    // ⚠ **각도를 추측으로 고치지 않기 위한 계측.**
    //    손목 각도를 부호만 바꿔 가며 몇 번이나 헛짚었다. IK · 뼈 축 보정 · 리그 축이 겹쳐서
    //    "이 값을 올리면 어느 쪽으로 도는지" 를 코드만 보고는 알 수 없다. 그래서 **결과를 찍어 본다.**
    //    포탄을 들고 있는 동안 1초마다, IK 까지 다 끝난 **최종 손 방향**을 캐릭터 기준으로 남긴다.
    [Tooltip("포탄을 들었을 때 최종 손 방향을 1초마다 로그로 남긴다. 각도를 맞출 때만 켠다.")]
    [SerializeField] private bool logAmmoHands = true;

    private float _nextHandLog;

    /// <summary>OnAnimatorIK 가 이번 프레임에 몇 번 불렸나. 0 이면 **IK Pass 가 꺼져 있다는 뜻**이다.</summary>
    private int _ikCalls;
    private int _ikCallsLastFrame;
    private int _ikFrame = -1;

    /// <summary>마지막으로 요청한 왼손 목표(자리 · 손가락 · 손바닥). 실제 결과와 비교해 IK 가 먹었는지 본다.</summary>
    private Vector3 _wantLeftAt;
    private Vector3 _wantFingers;
    private Vector3 _wantPalm;
    private string _branch = "(아직)";

    private Animator _animator;
    private Transform _chest;
    private Transform _leftHand;
    private Transform _rightHand;
    private Transform _leftShoulder;
    private Transform _rightShoulder;
    private Transform _leftLowerArm;
    private Transform _rightLowerArm;

    // 손바닥 방향을 **그 순간에 재기** 위한 손가락 · 엄지 뼈. 각도를 추측하지 않으려면 이 둘이 필요하다.
    private Transform _leftFinger;
    private Transform _rightFinger;
    private Transform _leftThumb;
    private Transform _rightThumb;
    private float _tall = 2.7f;

    /// <summary>어깨(위팔 뼈)에서 손까지 팔 길이 (m). 판자를 앞으로 나란히 들 때 손을 둘 거리.</summary>
    private float _armLength = 0.8f;

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

        // 팔 길이 — 위팔 → 아래팔 → 손 뼈 사이 거리. 뼈 사이 거리는 포즈로 안 바뀌니 어느 자세에서 재도 같다.
        _leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

        // 아래팔 — 팔 길이를 재는 데 쓴다.
        _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

        // 손가락 · 엄지 — 손바닥이 지금 어디를 보는지 **매 프레임 재는** 데 쓴다. (AimPalms)
        _leftFinger = _animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal)
                      ?? _animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
        _rightFinger = _animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal)
                       ?? _animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        _leftThumb = _animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal);
        _rightThumb = _animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);

        if (_leftFinger == null || _leftThumb == null || _rightFinger == null || _rightThumb == null)
        {
            Debug.LogWarning($"[{name}] 손가락 · 엄지 뼈가 없어 포탄을 들 때 손바닥 방향을 못 맞춥니다.", this);
        }

        _armLength = MeasureArm(
            _leftShoulder, _leftLowerArm, _leftHand,
            _rightShoulder, _rightLowerArm, _rightHand);

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

    /// <summary>판자를 들고 있는지. 판자만 앞으로 쭉 뻗어 받친다.</summary>
    private bool CarryingPlank => carry != null && carry.Carrying == Cargo.Plank;

    /// <summary>포탄을 들고 있는지. 포탄만 손 자리를 공 크기에서 낸다.</summary>
    private bool CarryingAmmo => carry != null && carry.Carrying == Cargo.Ammo;

    /// <summary>
    /// 지금 들고 있는 것의 **좌우 반폭** (m). 못 재면 예전 비율(<see cref="apart"/>)로 돌아간다.
    ///
    /// 렌더러 경계로 잰다 — 포탄 크기는 배치 도구(<c>HeldSize</c>)가 정하므로 코드에 숫자를 또 적지 않는다.
    /// </summary>
    private float HeldHalfWidth()
    {
        Transform show = held != null ? held : CurrentHeld();

        if (show == null || !show.gameObject.activeInHierarchy)
        {
            return _tall * apart;
        }

        Renderer[] draws = show.GetComponentsInChildren<Renderer>();

        if (draws.Length == 0)
        {
            return _tall * apart;
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        // 좌우로 얼마나 벌어져 있는지만 본다. 공이라 x · z 가 같지만, 길쭉한 것이 와도 옆폭을 쓴다.
        return Mathf.Max(box.extents.x, 0.05f);
    }

    /// <summary>양팔 길이의 평균. 한쪽 뼈가 없으면 있는 쪽, 둘 다 없으면 키의 30%.</summary>
    private float MeasureArm(Transform lUpper, Transform lLower, Transform lHand,
                             Transform rUpper, Transform rLower, Transform rHand)
    {
        float left = ArmOf(lUpper, lLower, lHand);
        float right = ArmOf(rUpper, rLower, rHand);

        if (left > 0f && right > 0f) return (left + right) * 0.5f;
        if (left > 0f) return left;
        if (right > 0f) return right;
        return _tall * 0.3f;
    }

    private static float ArmOf(Transform upper, Transform lower, Transform hand)
    {
        if (upper == null || lower == null || hand == null)
        {
            return 0f;
        }

        return Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
    }

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
        // 계측 — 이 함수가 불리는지, 한 프레임에 몇 번 불리는지. (IK Pass 가 꺼져 있으면 아예 안 불린다)
        if (_ikFrame != Time.frameCount)
        {
            _ikCallsLastFrame = _ikCalls;
            _ikCalls = 0;
            _ikFrame = Time.frameCount;
        }

        _ikCalls++;

        if (!Carrying)
        {
            // 안 들고 있으면 IK 를 풀어야 한다. 안 풀면 빈손으로도 팔이 모인 채 남는다.
            Release(AvatarIKGoal.LeftHand);
            Release(AvatarIKGoal.RightHand);
            return;
        }

        if (CarryingPlank)
        {
            // 🪵 앞으로 쭉 — 손마다 **자기 어깨에서** 팔 길이만큼 정면으로, 어깨보다 plankDrop 아래(가슴 아래).
            //    어깨 높이 그대로면 손 위의 판자가 턱까지 올라와 얼굴을 가린다.
            //    손바닥은 위. 두 손이 쟁반처럼 받치고 그 위에 판자가 얹힌다. (LateUpdate 의 plankRest)
            Vector3 ahead = transform.forward * (_armLength * plankStraight) + Vector3.down * (_tall * plankDrop);
            Vector3 leftFrom = _leftShoulder != null ? _leftShoulder.position : HoldPoint - transform.right * (_tall * apart);
            Vector3 rightFrom = _rightShoulder != null ? _rightShoulder.position : HoldPoint + transform.right * (_tall * apart);

            Reach(AvatarIKGoal.LeftHand, leftFrom + ahead, Vector3.up, Quaternion.Euler(leftHandFixEuler));
            Reach(AvatarIKGoal.RightHand, rightFrom + ahead, Vector3.up, Quaternion.Euler(rightHandFixEuler));
            return;
        }

        if (CarryingAmmo)
        {
            // ⚫ 팔을 앞으로 쭉 뻗고, 손바닥이 공을 보게 한다. 공은 두 손 사이에 붙는다. (LateUpdate)
            //    손가락을 감아쥐는 것은 IK 로 안 되므로 **손바닥 방향까지**가 이 리그로 할 수 있는 전부다.
            Vector3 ahead = transform.forward * (_armLength * ammoStraight) + Vector3.down * (_tall * ammoDrop);
            Vector3 centre = ShoulderMiddle + ahead;

            // 공 표면까지 벌린다. 좁게 벌리면 손목이 공 속에 묻혀 손이 아예 안 보인다.
            Vector3 spread = transform.right * (HeldHalfWidth() + ammoGrip);

            // ⚠ **손가락은 팔뚝을 그대로 이어간다.** 그래야 손목이 안 꺾인다.
            //    손 방향을 몸 기준(앞)으로 고정하면 팔은 비스듬히 뻗어 있는데 손만 정면을 봐서 **손목이 접힌다.**
            //    팔이 어디로 뻗을지는 어깨 → 목표점 방향으로 알 수 있으니, 그 방향을 손가락 방향으로 쓴다.
            //    손바닥은 그 축을 중심으로 안쪽(공)을 보게 한다.
            Vector3 leftAt = centre - spread;
            Vector3 rightAt = centre + spread;

            _branch = "포탄";
            _wantLeftAt = leftAt;
            _wantPalm = transform.right;

            // ⚠ **회전은 IK 로 주지 않는다.** 계측해 보니 요청한 손 회전이 결과에 거의 반영되지 않았다
            //    (손가락 앞 +0.96 을 요청했는데 결과는 위 +0.97). 휴머노이드 IK 는 아바타의 관절 한계로
            //    요청을 잘라내는데, 이 리그는 팔이 짧고 한계가 좁아 손 회전이 통째로 뭉개진다.
            //    그래서 **자리만 IK 로 잡고, 손 방향은 LateUpdate 에서 뼈를 직접 돌린다.** (AimPalms)
            HoldAt(AvatarIKGoal.LeftHand, leftAt);
            HoldAt(AvatarIKGoal.RightHand, rightAt);
            return;
        }

        Vector3 middle = HoldPoint;
        Vector3 side = transform.right * (_tall * apart);

        // 왼손은 오른쪽(가운데)을, 오른손은 왼쪽(가운데)을 향한다.
        Reach(AvatarIKGoal.LeftHand, middle - side, transform.right, Quaternion.Euler(leftHandFixEuler));
        Reach(AvatarIKGoal.RightHand, middle + side, -transform.right, Quaternion.Euler(rightHandFixEuler));
    }


    /// <summary>
    /// 포탄을 들고 있는 동안 **최종 손 방향**을 캐릭터 기준으로 찍는다. (LateUpdate — IK 까지 끝난 뒤)
    ///
    /// 손가락 = 뼈 로컬 +Y, 손바닥 = 왼손 −X · 오른손 +X. (ShipCoopHandAxisMeasure 가 잰 값)
    /// 읽는 법 — 캐릭터 기준 (앞, 위, 오른쪽) 이다.
    /// <code>
    ///   손가락 앞 +1     팔 방향 그대로 뻗음        손가락 위 −1   완전히 아래를 봄
    ///   손바닥 오른쪽 +1 오른쪽을 봄 (왼손이면 안쪽) 손바닥 위 −1   아래를 봄
    /// </code>
    /// </summary>
    private void LogAmmoHands()
    {
        if (!logAmmoHands || !CarryingAmmo || _leftHand == null || _rightHand == null)
        {
            return;
        }

        if (Time.time < _nextHandLog)
        {
            return;
        }

        _nextHandLog = Time.time + 1f;

        // 손목 위치를 **어깨 기준**으로 본다. 팔이 정말 앞으로 뻗었는지, 아니면 애니메이션대로 내려와 있는지 갈린다.
        Vector3 wantFromShoulder = _leftShoulder != null ? _wantLeftAt - _leftShoulder.position : Vector3.zero;
        Vector3 gotFromShoulder = _leftShoulder != null ? _leftHand.position - _leftShoulder.position : Vector3.zero;

        Debug.Log(
            "[드는 자세] 포탄 계측 (캐릭터 기준 앞/위/오른쪽)\n" +
            $"  OnAnimatorIK 호출 {_ikCallsLastFrame}회/프레임   갈래 {_branch}   IK 세기 {strength:F2}\n" +
            $"  왼손 자리   원한 곳 {LocalPos(wantFromShoulder)}   실제 {LocalPos(gotFromShoulder)}   차이 {Vector3.Distance(_wantLeftAt, _leftHand.position):F2}m\n" +
            $"  왼손 손가락 원한 것 {Local(_wantFingers)}   실제 {Local(_leftHand.rotation * Vector3.up)}\n" +
            $"  왼손 손바닥 원한 것 {Local(_wantPalm)}   실제 {Local(_leftHand.rotation * Vector3.left)}\n" +
            $"  오른손 손가락 실제 {Local(_rightHand.rotation * Vector3.up)}   손바닥 실제 {Local(_rightHand.rotation * Vector3.right)}\n" +
            $"  값: straight {ammoStraight:F2}, drop {ammoDrop:F2}, grip {ammoGrip:F2}", this);
    }

    /// <summary>월드 방향을 캐릭터 기준 (앞, 위, 오른쪽) 으로.</summary>
    private string Local(Vector3 world)
    {
        Vector3 v = transform.InverseTransformDirection(world.normalized);
        return $"(앞 {v.z:+0.00;-0.00}, 위 {v.y:+0.00;-0.00}, 오른 {v.x:+0.00;-0.00})";
    }

    /// <summary>월드 변위를 캐릭터 기준 미터로.</summary>
    private string LocalPos(Vector3 world)
    {
        Vector3 v = transform.InverseTransformDirection(world);
        return $"(앞 {v.z:+0.00;-0.00}, 위 {v.y:+0.00;-0.00}, 오른 {v.x:+0.00;-0.00})m";
    }

    /// <summary>자리만 IK 로 잡는다. 회전은 안 건다 — 손 방향은 <see cref="AimPalms"/> 가 뼈를 직접 돌려 정한다.</summary>
    private void HoldAt(AvatarIKGoal goal, Vector3 at)
    {
        _animator.SetIKPositionWeight(goal, strength);
        _animator.SetIKRotationWeight(goal, 0f);
        _animator.SetIKPosition(goal, at);
    }

    /// <summary>
    /// ⚫ **손바닥이 공을 보게 손뼈를 직접 돌린다.** (LateUpdate — 애니메이터와 IK 가 끝난 뒤)
    ///
    /// <b>왜 IK 가 아니라 직접 돌리나.</b> <c>SetIKRotation</c> 으로 준 회전이 결과에 거의 반영되지 않았다.
    /// 휴머노이드 IK 는 아바타의 관절 한계 안으로 요청을 잘라내는데, 이 리그는 그 한계가 좁다. 뼈를 직접
    /// 돌리면 그 잘라냄을 거치지 않는다. 자리(팔 뻗기)는 여전히 IK 가 맡는다.
    ///
    /// <b>각도를 어떻게 정하나 — 추측하지 않는다.</b> 손가락 축과 손바닥 방향을 **그 순간의 뼈에서 재고**,
    /// 손바닥이 공을 보려면 그 축으로 몇 도를 돌려야 하는지 계산해서 그만큼만 돌린다. 부호를 찍어 맞힐 일이 없다.
    /// 손가락 축을 중심으로만 돌리므로 손목이 접히지 않는다 — 순수한 손목 비틀기다.
    /// </summary>
    private void AimPalms(Vector3 ballCentre)
    {
        AimPalm(_leftHand, _leftFinger, _leftThumb, true, ballCentre);
        AimPalm(_rightHand, _rightFinger, _rightThumb, false, ballCentre);
    }

    private void AimPalm(Transform hand, Transform finger, Transform thumb, bool left, Vector3 ballCentre)
    {
        if (hand == null || finger == null || thumb == null)
        {
            return;
        }

        // 손가락 축 — 손뼈에서 가운뎃손가락 첫 마디로. 뼈 사이 관계라 자세가 바뀌어도 늘 맞다.
        Vector3 axis = finger.position - hand.position;

        if (axis.sqrMagnitude < 1e-8f)
        {
            return;
        }

        axis = axis.normalized;

        // 지금 손바닥이 보는 쪽 — 엄지와 손가락의 외적. 왼손 · 오른손은 미러라 순서가 반대다.
        Vector3 thumbDir = thumb.position - hand.position;
        Vector3 palm = left ? Vector3.Cross(thumbDir, axis) : Vector3.Cross(axis, thumbDir);

        // 손가락 축에 수직인 성분만 비교한다. 그 축으로만 돌릴 것이므로.
        Vector3 now = Vector3.ProjectOnPlane(palm, axis);
        Vector3 want = Vector3.ProjectOnPlane(ballCentre - hand.position, axis);

        if (now.sqrMagnitude < 1e-8f || want.sqrMagnitude < 1e-8f)
        {
            return;
        }

        float turn = Vector3.SignedAngle(now, want, axis);
        hand.rotation = Quaternion.AngleAxis(turn, axis) * hand.rotation;
    }

    /// <summary>두 어깨의 가운데. 어깨 뼈를 못 찾으면 가슴.</summary>
    private Vector3 ShoulderMiddle
    {
        get
        {
            if (_leftShoulder != null && _rightShoulder != null)
            {
                return (_leftShoulder.position + _rightShoulder.position) * 0.5f;
            }

            return _chest != null ? _chest.position : transform.position;
        }
    }


    /// <param name="palmToward">손바닥이 향할 방향. 안을 때는 서로(가운데), 판자를 받칠 때는 위.</param>
    /// <param name="boneFix">손뼈의 실제 (손가락, 손바닥) 축을 (+Z, +Y) 로 돌려 놓는 회전. 리그마다 · 손마다 다르다.</param>
    private void Reach(AvatarIKGoal goal, Vector3 at, Vector3 palmToward, Quaternion boneFix)
    {
        _animator.SetIKPositionWeight(goal, strength);
        _animator.SetIKRotationWeight(goal, strength);
        _animator.SetIKPosition(goal, at);

        // ⚠ 두 손에 같은 회전을 주면 손바닥이 둘 다 위를 봐서 쟁반처럼 받치는 모양이 된다.
        //    손가락은 앞으로, 손바닥은 가운데(서로)를 향하게 손마다 따로 돌린다.
        // ⚠ 뒤에 boneFix 를 곱한다. LookRotation 은 "+Z 가 손가락, +Y 가 손바닥" 인 뼈에만 맞는 회전이라,
        //    이 리그의 뼈 축을 그 기준으로 먼저 돌려 놓아야 목표한 방향이 실제 손가락 · 손바닥에 걸린다.
        //    (회전 목표 = 세상 기준 방향 × 뼈 축 보정. 순서를 바꾸면 보정이 세상 축으로 걸려 틀어진다)
        _animator.SetIKRotation(goal, Quaternion.LookRotation(transform.forward, palmToward) * boneFix);
    }

    /// <summary>보정값을 잴 때 손뼈 축을 눈으로 본다. 빨강 X · 초록 Y · 파랑 Z. 손가락이 파랑, 손바닥이 초록이면 맞다.</summary>
    private void DrawHandAxes()
    {
        DrawAxes(_leftHand);
        DrawAxes(_rightHand);
    }

    private void DrawAxes(Transform bone)
    {
        if (bone == null)
        {
            return;
        }

        float length = _tall * 0.12f;
        Debug.DrawRay(bone.position, bone.right * length, Color.red);
        Debug.DrawRay(bone.position, bone.up * length, Color.green);
        Debug.DrawRay(bone.position, bone.forward * length, Color.blue);
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
        if (drawHandAxes && Carrying)
        {
            DrawHandAxes();
        }

        LogAmmoHands();

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

        if (CarryingPlank)
        {
            // 판자는 손 사이가 아니라 **손바닥 위**에 얹힌다. 밑면이 손뼈 높이에서 plankRest 만큼 위.
            show.position += Vector3.up * (box.extents.y + plankRest);
        }

        if (CarryingAmmo)
        {
            // 공이 놓인 자리를 알았으니, 손바닥이 그쪽을 보도록 손뼈를 직접 돌린다.
            AimPalms(show.position);
        }
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
