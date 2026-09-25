using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsWeaponEquipper : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private WarriorsWeaponDefinition definition;
        [SerializeField] private GameObject weaponPrefab;
        [SerializeField] private Vector3 gripLocalPosition = new(0f, 0.26f, 0f);
        [SerializeField] private Vector3 gripLocalRotation = new(0f, 0f, -90f);
        [SerializeField] private Vector3 localScale = Vector3.one * 0.65f;
        [SerializeField] private Vector3 hitboxCenter = new(-0.45f, 0f, 0f);
        [SerializeField] private Vector3 hitboxSize = new(1.8f, 0.18f, 0.22f);

        public GameObject EquippedWeapon { get; private set; }
        public WarriorsWeaponHitbox Hitbox { get; private set; }

        /// <summary>
        /// Turns the sword trail up for one swing and puts it back afterwards. The trail is
        /// already on the weapon, so a finisher borrows it rather than spawning anything -
        /// and because the authored values are captured on equip, repeated finishers cannot
        /// ratchet it wider and wider.
        /// </summary>
        public void SetTrailBoost(bool boosted)
        {
            TrailRenderer trail = EquippedWeapon != null
                ? EquippedWeapon.GetComponent<TrailRenderer>() : null;
            if (trail == null) return;

            if (!trailCaptured)
            {
                // Captured on the first boost rather than at equip time: the combat script
                // rewrites this trail when the first swing happens, so anything captured
                // earlier would be restored afterwards as the wrong values.
                baseTrailTime = trail.time;
                baseTrailWidth = trail.startWidth;
                baseTrailGradient = trail.colorGradient;
                trailCaptured = true;
            }

            trail.time = boosted ? baseTrailTime * 2.2f : baseTrailTime;
            trail.startWidth = boosted ? baseTrailWidth * 2.4f : baseTrailWidth;
            // The gradient is what actually colours a trail - startColor is ignored while
            // one is assigned, which is why tinting it alone showed nothing.
            trail.colorGradient = boosted ? BoostGradient : baseTrailGradient;
        }

        private bool trailCaptured;
        private float baseTrailTime;
        private float baseTrailWidth;
        private Gradient baseTrailGradient;

        private static Gradient boostGradient;

        private static Gradient BoostGradient
        {
            get
            {
                if (boostGradient != null) return boostGradient;
                boostGradient = new Gradient();
                boostGradient.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, .95f, .7f), 0f),
                            new GradientColorKey(new Color(1f, .72f, .2f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                return boostGradient;
            }
        }

        private void Awake() => Equip();

#if UNITY_EDITOR
        /// <summary>
        /// ⚠ 에디터 전용 — 칼이 손에 붙는 자리를 <b>Play 중에 눈으로 맞추려고</b> 둔다.
        ///
        /// 칼은 <see cref="Equip"/> 에서 한 번 붙고 끝이라, Play 중에 무기 정의 에셋(<c>w_TH_sword.asset</c>)의
        /// 값을 바꿔도 화면이 그대로다. 매번 멈추고 다시 켜야 했다. 그래서 에디터에서는 매 프레임
        /// 에셋 값을 다시 입힌다. ScriptableObject 에셋은 Play 를 멈춰도 되돌아가지 않으므로 맞춘 값이 남는다.
        ///
        /// 빌드에는 들어가지 않는다. 빌드는 예전처럼 붙일 때 한 번만 정한다.
        ///
        /// ⚠ <b>에디터에서 Play 하는 동안에는 칼 위치가 늘 에셋 값을 따른다.</b>
        ///    · 칼 오브젝트(Hierarchy 의 <c>w_TH_sword</c>)의 Transform 을 직접 고치면 다음 프레임에 되돌아간다.
        ///      값은 Project 창의 <c>w_TH_sword.asset</c> 에서 고친다. 이름이 같아서 헷갈리기 쉽다.
        ///    · 게임 중에 칼을 옮기는 기능(다른 손으로 바꿔 쥐기 등)을 넣으면 에디터에서만 먹지 않는다.
        ///      그런 기능을 넣을 때는 이 LateUpdate 를 지운다.
        /// </summary>
        private void LateUpdate()
        {
            if (definition == null || EquippedWeapon == null) return;

            Transform grip = EquippedWeapon.transform;
            grip.localPosition = definition.gripLocalPosition;
            grip.localRotation = Quaternion.Euler(definition.gripLocalRotation);
            grip.localScale = definition.localScale;
        }
#endif

        public void Equip()
        {
            if (definition != null)
            {
                weaponPrefab = definition.weaponPrefab;
                gripLocalPosition = definition.gripLocalPosition;
                gripLocalRotation = definition.gripLocalRotation;
                localScale = definition.localScale;
                hitboxCenter = definition.hitboxCenter;
                hitboxSize = definition.hitboxSize;
            }
            if (animator == null) animator = GetComponent<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null || weaponPrefab == null) return;

            Transform oldSocket = hand.Find("WarriorsWeaponSocket");
            if (oldSocket != null) Destroy(oldSocket.gameObject);
            if (EquippedWeapon != null) Destroy(EquippedWeapon);

            EquippedWeapon = Instantiate(weaponPrefab, hand);
            EquippedWeapon.name = "w_TH_sword";
            EquippedWeapon.transform.localPosition = gripLocalPosition;
            EquippedWeapon.transform.localRotation = Quaternion.Euler(gripLocalRotation);
            EquippedWeapon.transform.localScale = localScale;

            BoxCollider collider = EquippedWeapon.GetComponent<BoxCollider>();
            if (collider == null) collider = EquippedWeapon.AddComponent<BoxCollider>();
            collider.center = hitboxCenter;
            collider.size = hitboxSize;
            collider.isTrigger = true;
            collider.enabled = false;
            Rigidbody body = EquippedWeapon.GetComponent<Rigidbody>();
            if (body == null) body = EquippedHelper.AddKinematicBody(EquippedWeapon);
            Hitbox = EquippedWeapon.GetComponent<WarriorsWeaponHitbox>();
            if (Hitbox == null) Hitbox = EquippedWeapon.AddComponent<WarriorsWeaponHitbox>();

            TrailRenderer trail = EquippedWeapon.GetComponent<TrailRenderer>();
            if (trail == null) trail = EquippedWeapon.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0.01f;
            trail.minVertexDistance = 0.04f;
            // ⚠ Dedicated Server 빌드에는 셰이더가 들어 있지 않다. (Dedicated Server Optimizations)
            //    그대로 두면 Shader.Find 가 null 을 돌려주고 new Material(null) 이 예외를 던져
            //    Awake 가 중간에 끊긴다. 칼이 손에 붙지 않은 채로 남는다.
            //    칼 궤적은 연출이라 서버에는 없어도 된다.
            Shader trailShader = Shader.Find("Sprites/Default");
            if (trailShader != null) trail.material = new Material(trailShader);
            trail.startColor = new Color(0.35f, 0.85f, 1f, 0.8f);
            trail.endColor = new Color(0.7f, 0.95f, 1f, 0f);
        }

        private static class RigidbodyHelper { }
        private static class EquippedHelper
        {
            public static Rigidbody AddKinematicBody(GameObject target)
            {
                Rigidbody body = target.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                return body;
            }
        }
    }
}
