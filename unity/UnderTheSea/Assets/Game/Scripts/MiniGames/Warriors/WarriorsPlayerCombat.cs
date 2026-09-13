using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsPlayerCombat : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private Animator animator;
        [SerializeField] private WarriorsWeaponEquipper weaponEquipper;
        [SerializeField, Min(1)] private int damage = 25;
        [SerializeField, Min(1f)] private float attackRadius = 5.8f;
        [SerializeField, Min(1f)] private float thrustLength = 8f;
        [SerializeField, Min(.5f)] private float thrustHalfWidth = 1.65f;
        [SerializeField, Range(30f, 180f)] private float verticalAttackAngle = 110f;
        [SerializeField, Range(30f, 180f)] private float horizontalAttackAngle = 150f;
        [SerializeField, Range(20f, 100f)] private float thrustAttackAngle = 55f;
        [SerializeField, Range(0, 3)] private int playerId;
        [SerializeField] private WarriorsIoTFeedbackHub iotFeedback;
        [SerializeField] private WarriorsThirdPersonCamera combatCamera;
        [SerializeField] private WarriorsRhythmBattle rhythmBattle;

        private static readonly int AttackAHash = Animator.StringToHash("WarriorsAttackA");
        private static readonly int AttackBHash = Animator.StringToHash("WarriorsAttackB");
        private IWarriorsInputSource InputSource => inputSource as IWarriorsInputSource;
        private WarriorsAttackDirection activeDirection;
        private float activeStrength = 1f;
        private Coroutine attackRoutine;
        private TrailRenderer attackTrail;
        private readonly Collider[] areaHits = new Collider[48];
        private readonly HashSet<WarriorsTarget> hitThisAttack = new();

        public WarriorsAttackDirection LastAttackDirection { get; private set; }
        public bool IsAttacking { get; private set; }
        public event Action<WarriorsAttackDirection, int> AttackResolved;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (weaponEquipper == null) weaponEquipper = GetComponent<WarriorsWeaponEquipper>();
            if (iotFeedback == null)
                iotFeedback = UnityEngine.Object.FindFirstObjectByType<WarriorsIoTFeedbackHub>(FindObjectsInactive.Include);
            if (combatCamera == null)
                combatCamera = UnityEngine.Object.FindFirstObjectByType<WarriorsThirdPersonCamera>(FindObjectsInactive.Include);
            if (rhythmBattle == null)
                rhythmBattle = UnityEngine.Object.FindFirstObjectByType<WarriorsRhythmBattle>(FindObjectsInactive.Include);
        }

        private void OnEnable() { if (InputSource != null) InputSource.AttackRequested += HandleAttackRequested; }
        private void OnDisable() { if (InputSource != null) InputSource.AttackRequested -= HandleAttackRequested; EndAttackHitbox(); }
        public void BindInputSource(MonoBehaviour source)
        {
            if (isActiveAndEnabled && InputSource != null) InputSource.AttackRequested -= HandleAttackRequested;
            inputSource = source;
            if (isActiveAndEnabled && InputSource != null) InputSource.AttackRequested += HandleAttackRequested;
        }

        public void RequestAttack(WarriorsAttackDirection direction) => StartAttack(direction, 1f);

        private void HandleAttackRequested(WarriorsAttackDirection direction, float strength)
        {
            // ROUND 3 input is first judged by WarriorsRhythmBattle. Only a
            // successful judgement calls RequestAttack and plays the animation.
            if (rhythmBattle != null && rhythmBattle.IsActive) return;
            StartAttack(direction, strength);
        }

        private void StartAttack(WarriorsAttackDirection direction, float strength)
        {
            if (IsAttacking) return;
            IsAttacking = true;
            activeDirection = direction;
            activeStrength = Mathf.Clamp01(strength);
            LastAttackDirection = direction;
            if (animator != null)
            {
                animator.ResetTrigger(AttackAHash);
                animator.ResetTrigger(AttackBHash);
                animator.SetTrigger(direction == WarriorsAttackDirection.HorizontalSlash ? AttackBHash : AttackAHash);
            }
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(PlayDistinctWeaponAttack(direction));
        }

        private IEnumerator PlayDistinctWeaponAttack(WarriorsAttackDirection direction)
        {
            yield return null;
            Transform weapon = weaponEquipper != null && weaponEquipper.EquippedWeapon != null
                ? weaponEquipper.EquippedWeapon.transform : null;
            if (weapon == null) { ApplyAreaAttack(direction); FinishAttack(); yield break; }
            EnsureAttackTrail(weapon);
            attackTrail.Clear();
            attackTrail.enabled = true;

            Quaternion rest = weapon.localRotation;
            Vector3 restPosition = weapon.localPosition;
            if (direction == WarriorsAttackDirection.Thrust)
            {
                bool thrustDamageApplied = false;
                for (float elapsed = 0f; elapsed < .36f; elapsed += Time.deltaTime)
                {
                    float t = elapsed / .36f;
                    float push = Mathf.Sin(t * Mathf.PI) * .85f;
                    weapon.localPosition = restPosition + Vector3.forward * push;
                    if (!thrustDamageApplied && t >= .38f)
                    {
                        ApplyAreaAttack(direction);
                        thrustDamageApplied = true;
                    }
                    yield return null;
                }
                if (!thrustDamageApplied) ApplyAreaAttack(direction);
                weapon.localPosition = restPosition;
                attackTrail.enabled = false;
                FinishAttack();
                attackRoutine = null;
                yield break;
            }

            Vector3 axis = direction == WarriorsAttackDirection.HorizontalSlash ? Vector3.up : Vector3.forward;
            float start = direction == WarriorsAttackDirection.VerticalSlash ? 85f : -85f;
            float end = -start;
            bool damageApplied = false;
            for (float elapsed = 0f; elapsed < .42f; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / .42f);
                weapon.localRotation = rest * Quaternion.AngleAxis(Mathf.Lerp(start, end, t), axis);
                if (!damageApplied && t >= .38f)
                {
                    ApplyAreaAttack(direction);
                    damageApplied = true;
                }
                yield return null;
            }
            if (!damageApplied) ApplyAreaAttack(direction);
            weapon.localRotation = rest;
            attackTrail.enabled = false;
            FinishAttack();
            attackRoutine = null;
        }

        private void EnsureAttackTrail(Transform weapon)
        {
            if (attackTrail != null) return;
            attackTrail = weapon.GetComponent<TrailRenderer>();
            if (attackTrail == null) attackTrail = weapon.gameObject.AddComponent<TrailRenderer>();
            attackTrail.time = .16f;
            attackTrail.minVertexDistance = .025f;
            attackTrail.startWidth = .18f;
            attackTrail.endWidth = .015f;
            attackTrail.alignment = LineAlignment.View;
            attackTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            attackTrail.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (attackTrail.sharedMaterial == null && shader != null)
            {
                Material trailMaterial = new(shader) { hideFlags = HideFlags.HideAndDontSave };
                trailMaterial.color = new Color(.25f, .82f, 1f, .9f);
                attackTrail.sharedMaterial = trailMaterial;
            }
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.75f, .95f, 1f), 0f), new GradientColorKey(new Color(.2f, .55f, 1f), 1f) },
                new[] { new GradientAlphaKey(.95f, 0f), new GradientAlphaKey(0f, 1f) });
            attackTrail.colorGradient = gradient;
            attackTrail.enabled = false;
        }

        private void ApplyAreaAttack(WarriorsAttackDirection direction)
        {
            hitThisAttack.Clear();
            bool thrust = direction == WarriorsAttackDirection.Thrust;
            Vector3 center = transform.position + Vector3.up + transform.forward * (thrust ? thrustLength * .5f : 2.25f);
            float queryRadius = thrust ? thrustLength * .55f : attackRadius;
            int count = Physics.OverlapSphereNonAlloc(center, queryRadius, areaHits, ~0, QueryTriggerInteraction.Collide);
            float attackAngle = direction == WarriorsAttackDirection.HorizontalSlash ? horizontalAttackAngle
                : direction == WarriorsAttackDirection.Thrust ? thrustAttackAngle : verticalAttackAngle;
            float halfAngle = attackAngle * .5f;
            bool targetInAttackArea = false;
            int acceptedCount = 0;
            for (int i = 0; i < count; i++)
            {
                WarriorsTarget target = areaHits[i] != null ? areaHits[i].GetComponentInParent<WarriorsTarget>() : null;
                if (target == null || target.IsDefeated || !hitThisAttack.Add(target)) continue;
                Vector3 toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;
                if (thrust)
                {
                    float forwardDistance = Vector3.Dot(toTarget, transform.forward);
                    float sideDistance = Mathf.Abs(Vector3.Dot(toTarget, transform.right));
                    if (forwardDistance < 0f || forwardDistance > thrustLength || sideDistance > thrustHalfWidth) continue;
                }
                else if (toTarget.sqrMagnitude > .01f && Vector3.Angle(transform.forward, toTarget) > halfAngle) continue;
                targetInAttackArea = true;
                bool accepted = target.TryReceiveAttack(direction, damage);
                if (accepted)
                {
                    acceptedCount++;
                    target.GetComponent<WarriorsTargetFeedback>()?.PlayHit();
                }
            }
            if (acceptedCount > 0)
            {
                iotFeedback?.Request(playerId, WarriorsIoTFeedbackType.CorrectAttack, activeStrength);
                combatCamera?.Shake(Mathf.Clamp(acceptedCount * .08f, .08f, .32f));
            }
            else if (targetInAttackArea)
                iotFeedback?.Request(playerId, WarriorsIoTFeedbackType.WrongAttack, .45f);
            AttackResolved?.Invoke(direction, acceptedCount);
        }

        public void BeginAttackHitbox() { }
        public void EndAttackHitbox()
        {
            if (attackTrail != null) attackTrail.enabled = false;
            weaponEquipper?.Hitbox?.End();
        }
        public void FinishAttack() { EndAttackHitbox(); IsAttacking = false; }
    }
}
