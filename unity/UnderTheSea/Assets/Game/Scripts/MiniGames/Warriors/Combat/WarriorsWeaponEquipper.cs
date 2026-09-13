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

        private void Awake() => Equip();

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
            trail.material = new Material(Shader.Find("Sprites/Default"));
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
