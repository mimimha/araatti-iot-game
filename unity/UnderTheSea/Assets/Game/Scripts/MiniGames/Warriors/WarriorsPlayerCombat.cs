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
        [SerializeField, Range(.4f, 2f)] private float thrustHeight = 1.05f;
        [SerializeField, Range(0f, 1.2f)] private float thrustSideOffset = .5f;
        [SerializeField, Range(30f, 180f)] private float verticalAttackAngle = 110f;
        [SerializeField, Range(30f, 180f)] private float horizontalAttackAngle = 150f;
        [SerializeField, Range(20f, 100f)] private float thrustAttackAngle = 55f;
        [SerializeField, Range(.2f, .3f)] private float inputBufferSeconds = .3f;
        [SerializeField, Range(0, 1)] private int playerId;
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
        private Transform activeWeapon;
        private Quaternion activeWeaponRestRotation;
        private Vector3 activeWeaponRestPosition;
        private bool hasBufferedAttack;
        private WarriorsAttackDirection bufferedDirection;
        private float bufferedStrength;
        private float bufferedUntil;
        private readonly Collider[] areaHits = new Collider[48];
        private readonly HashSet<WarriorsTarget> hitThisAttack = new();
        private readonly List<WarriorsTarget> attackCandidates = new();

        public WarriorsAttackDirection LastAttackDirection { get; private set; }
        public bool IsAttacking { get; private set; }
        public int PlayerId => playerId;

        /// <summary>
        /// Everything this player has personally connected with. Two player runs share
        /// one health pool, so the player strip used to show the same bar twice; this is
        /// something that actually differs between the two of them.
        /// </summary>
        public int LandedHits { get; private set; }
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

        private void OnEnable()
        {
            WarriorsPlayers.Register(this);
            if (InputSource != null) InputSource.AttackRequested += HandleAttackRequested;
        }
        private void OnDisable()
        {
            WarriorsPlayers.Unregister(this);
            if (InputSource != null) InputSource.AttackRequested -= HandleAttackRequested;
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;
            hasBufferedAttack = false;
            RestoreWeaponRestPose();
            EndAttackHitbox();
            IsAttacking = false;
        }
        public void BindInputSource(MonoBehaviour source)
        {
            if (isActiveAndEnabled && InputSource != null) InputSource.AttackRequested -= HandleAttackRequested;
            inputSource = source;
            if (isActiveAndEnabled && InputSource != null) InputSource.AttackRequested += HandleAttackRequested;
        }

        public void RequestAttack(WarriorsAttackDirection direction) => StartAttack(direction, 1f);
        public void RequestAttack(WarriorsAttackDirection direction, float strength) => StartAttack(direction, strength);

        private void HandleAttackRequested(WarriorsAttackDirection direction, float strength)
        {
            // ROUND 3 input is first judged by WarriorsRhythmBattle. Only a
            // successful judgement calls RequestAttack and plays the animation.
            if (rhythmBattle != null && rhythmBattle.IsActive) return;
            StartAttack(direction, strength);
        }

        private void StartAttack(WarriorsAttackDirection direction, float strength)
        {
            if (IsAttacking)
            {
                bufferedDirection = direction;
                bufferedStrength = Mathf.Clamp(strength, .5f, 1.5f);
                bufferedUntil = Time.time + Mathf.Max(.3f, inputBufferSeconds);
                hasBufferedAttack = true;
                return;
            }
            IsAttacking = true;
            activeDirection = direction;
            activeStrength = Mathf.Clamp(strength, .5f, 1.5f);
            LastAttackDirection = direction;
            // The body swing and the procedural blade swing used to run at the same time and
            // visually accumulated into a spin, so only one of them ever drives an attack:
            // the two authored swings play on the Animator, and thrust - which has no clip -
            // is the only one the blade animates by hand.
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(PlayDistinctWeaponAttack(direction));
        }

        private IEnumerator PlayDistinctWeaponAttack(WarriorsAttackDirection direction)
        {
            // Horizontal and vertical have authored full-body clips; firing the trigger here
            // is what actually makes the character swing rather than stand still.
            if (animator != null && direction != WarriorsAttackDirection.Thrust)
            {
                animator.ResetTrigger(AttackAHash);
                animator.ResetTrigger(AttackBHash);
                animator.SetTrigger(direction == WarriorsAttackDirection.HorizontalSlash ? AttackAHash : AttackBHash);
            }
            yield return null;
            Transform weapon = weaponEquipper != null && weaponEquipper.EquippedWeapon != null
                ? weaponEquipper.EquippedWeapon.transform : null;
            if (weapon == null) { ApplyAreaAttack(direction); CompleteAttack(); yield break; }
            activeWeapon = weapon;
            activeWeaponRestRotation = weapon.localRotation;
            activeWeaponRestPosition = weapon.localPosition;
            EnsureAttackTrail(weapon);
            attackTrail.Clear();
            attackTrail.time = activeStrength > 1f ? .24f : .16f;
            attackTrail.startWidth = activeStrength > 1f ? .32f : .18f;
            attackTrail.enabled = true;

            Quaternion rest = activeWeaponRestRotation;
            Vector3 restPosition = activeWeaponRestPosition;
            if (direction == WarriorsAttackDirection.Thrust)
            {
                bool thrustDamageApplied = false;
                // Thrust is the one attack with no authored clip, so the blade is posed by
                // hand. Working in world space avoids guessing at the hand bone's axes: the
                // sword model runs along its own -X, so that is the axis aimed down the
                // character's facing, and the stab is carried at chest height where it reads
                // as a stab rather than a sword being walked forward.
                Quaternion aimed = Quaternion.LookRotation(transform.forward, Vector3.up)
                    * Quaternion.Euler(0f, 90f, 0f);
                // The camera sits behind the player, so a stab straight down the centre line is
                // seen end-on and reads as nothing happening. Offsetting it to the sword arm
                // keeps the whole blade in frame.
                Vector3 chest = transform.position + Vector3.up * thrustHeight
                    + transform.right * thrustSideOffset;
                for (float elapsed = 0f; elapsed < .36f; elapsed += Time.deltaTime)
                {
                    float t = elapsed / .36f;
                    float push = Mathf.Sin(t * Mathf.PI);
                    weapon.rotation = aimed;
                    weapon.position = chest + transform.forward * (.3f + push * 1.2f);
                    if (!thrustDamageApplied && t >= .38f)
                    {
                        ApplyAreaAttack(direction);
                        thrustDamageApplied = true;
                    }
                    if (t >= .6f && TryChainBufferedAttack()) yield break;
                    yield return null;
                }
                if (!thrustDamageApplied) ApplyAreaAttack(direction);
                weapon.localRotation = rest;
                weapon.localPosition = restPosition;
                attackTrail.enabled = false;
                CompleteAttack();
                yield break;
            }

            // The clip moves the arms, so the blade is left alone here - rotating it as well
            // is what used to read as the weapon spinning a full turn on every swing. Only
            // the trail and the damage window are driven from this side.
            bool damageApplied = false;
            for (float elapsed = 0f; elapsed < .42f; elapsed += Time.deltaTime)
            {
                float t = elapsed / .42f;
                if (!damageApplied && t >= .38f)
                {
                    ApplyAreaAttack(direction);
                    damageApplied = true;
                }
                if (t >= .6f && TryChainBufferedAttack()) yield break;
                yield return null;
            }
            if (!damageApplied) ApplyAreaAttack(direction);
            weapon.localRotation = rest;
            attackTrail.enabled = false;
            CompleteAttack();
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
            attackCandidates.Clear();
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
                    if (forwardDistance < 0f || forwardDistance > thrustLength ||
                        sideDistance > thrustHalfWidth + target.DirectionalHitPadding) continue;
                }
                else if (toTarget.sqrMagnitude > .01f && Vector3.Angle(transform.forward, toTarget) > halfAngle) continue;
                // Non-matching types are neutral bystanders: no damage, no rejection
                // feedback, and no WRONG vibration or text.
                if (target.RequiredDirection != direction) continue;
                attackCandidates.Add(target);
            }

            int acceptedCount = 0;
            WarriorsTarget tentacle = NearestBossPart(attackCandidates, direction);
            if (tentacle != null)
            {
                // ROUND 2 is a pattern puzzle rather than a sweep.  One swing may only ever
                // take the single nearest tentacle, so two tentacles that happen to share a
                // weakness can never fall to the same slash.
                if (tentacle.TryReceiveAttack(direction, damage))
                {
                    acceptedCount++;
                    tentacle.GetComponent<WarriorsTargetFeedback>()?.PlayHit();
                }
            }
            else
            {
                // ROUND 1 keeps the sweep - four fish going down on one swing is the whole
                // point of the round.
                foreach (WarriorsTarget target in attackCandidates)
                {
                    if (!target.TryReceiveAttack(direction, damage)) continue;
                    acceptedCount++;
                    target.GetComponent<WarriorsTargetFeedback>()?.PlayHit();
                }
            }
            if (acceptedCount > 0)
            {
                iotFeedback?.Request(playerId, WarriorsIoTFeedbackType.CorrectAttack, activeStrength);
                combatCamera?.Shake(Mathf.Clamp(acceptedCount * .08f, .08f, .32f));
            }
            LandedHits += acceptedCount;
            AttackResolved?.Invoke(direction, acceptedCount);
        }

        private WarriorsTarget NearestBossPart(List<WarriorsTarget> candidates, WarriorsAttackDirection direction)
        {
            WarriorsTarget nearest = null;
            float nearestDistance = float.MaxValue;
            WarriorsTarget fallback = null;
            float fallbackDistance = float.MaxValue;
            foreach (WarriorsTarget candidate in candidates)
            {
                if (!candidate.IsBossPart) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                // A tentacle that was just struck is still inside its hit cooldown. Picking it
                // again would swallow the swing and leave the other tentacle untouchable, so
                // the nearest one that can actually take the hit wins.
                if (candidate.CanReceiveAttack(direction))
                {
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    nearest = candidate;
                }
                else if (distance < fallbackDistance)
                {
                    fallbackDistance = distance;
                    fallback = candidate;
                }
            }
            return nearest != null ? nearest : fallback;
        }

        public void BeginAttackHitbox() { }
        public void EndAttackHitbox()
        {
            if (attackTrail != null) attackTrail.enabled = false;
            weaponEquipper?.Hitbox?.End();
        }
        // Animation clips may still contain the legacy FinishAttack event. The
        // procedural weapon routine owns attack timing so the event must not make
        // IsAttacking false while that routine is still restoring the weapon.
        public void FinishAttack() => EndAttackHitbox();

        private void CompleteAttack()
        {
            RestoreWeaponRestPose();
            EndAttackHitbox();
            attackRoutine = null;
            IsAttacking = false;

            if (!hasBufferedAttack) return;
            bool isFresh = Time.time <= bufferedUntil;
            WarriorsAttackDirection nextDirection = bufferedDirection;
            float nextStrength = bufferedStrength;
            hasBufferedAttack = false;
            if (isFresh) StartAttack(nextDirection, nextStrength);
        }

        private bool TryChainBufferedAttack()
        {
            if (!hasBufferedAttack || Time.time > bufferedUntil) return false;
            WarriorsAttackDirection nextDirection = bufferedDirection;
            float nextStrength = bufferedStrength;
            hasBufferedAttack = false;
            RestoreWeaponRestPose();
            EndAttackHitbox();
            attackRoutine = null;
            IsAttacking = false;
            StartAttack(nextDirection, nextStrength);
            return true;
        }

        private void RestoreWeaponRestPose()
        {
            if (activeWeapon == null) return;
            activeWeapon.localPosition = activeWeaponRestPosition;
            activeWeapon.localRotation = activeWeaponRestRotation;
            activeWeapon = null;
        }
    }
}
