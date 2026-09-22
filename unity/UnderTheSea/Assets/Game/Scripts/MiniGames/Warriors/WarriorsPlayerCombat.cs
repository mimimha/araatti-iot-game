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

        /// <summary>
        /// 찌르기. 예전에는 클립이 없어 칼만 코드로 돌렸고, 그래서 <b>몸은 가만히 있었다.</b>
        /// (WarriorsAnimatorSetup 이 이 상태를 컨트롤러에 넣는다)
        /// </summary>
        private static readonly int AttackCHash = Animator.StringToHash("WarriorsAttackC");
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
        /// 네트워크에서 서버가 정한 사람 번호(0 = 1P)를 받는다. (Warriors 네트워크 전환)
        ///
        /// 프리팹에는 0 이 박혀 있어 두 사람이 모두 1P 로 등록되던 것을 바로잡는다.
        /// <see cref="WarriorsPlayers"/> 는 번호순으로 정렬해 두므로 다시 등록해 순서를 맞춘다.
        /// 혼자 하는 씬에서는 아무도 부르지 않는다.
        /// </summary>
        public void ConfigurePlayerId(int id)
        {
            if (playerId == id) return;
            bool registered = isActiveAndEnabled;
            if (registered) WarriorsPlayers.Unregister(this);
            playerId = id;
            if (registered) WarriorsPlayers.Register(this);
        }

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
            // 세 공격 모두 몸이 움직인다. 트리거를 넣는 이 줄이 실제로 캐릭터를 휘두르게 한다.
            //
            // ⚠ 예전에는 찌르기만 빠져 있었다. 클립이 없어서였는데, 그동안 찌르기는
            //    **칼만 혼자 움직이고 사람은 서 있었다.**
            //    먼저 셋을 다 내린 뒤 하나만 올린다. 남아 있는 트리거가 다음 공격에
            //    묻어 들어가면 휘두르지 않은 동작이 한 번 더 나온다.
            if (animator != null)
            {
                animator.ResetTrigger(AttackAHash);
                animator.ResetTrigger(AttackBHash);
                animator.ResetTrigger(AttackCHash);

                animator.SetTrigger(direction switch
                {
                    WarriorsAttackDirection.HorizontalSlash => AttackAHash,
                    WarriorsAttackDirection.VerticalSlash => AttackBHash,
                    _ => AttackCHash,
                });
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

            // ⚠ **칼은 손에 붙어 있다. 여기서 옮기지 않는다.**
            //
            //    예전에는 찌르기만 칼을 **월드 좌표로** 따로 몰았다. 클립이 없어 몸이 가만히
            //    있었기 때문인데, 그래서 <b>칼이 손보다 훨씬 앞으로 날아가</b> 손에서 떨어져
            //    보였다. 이제 찌르기도 팔이 움직이는 클립(WarriorsAttackC)이 있으므로,
            //    칼은 손을 따라가기만 하면 된다.
            //
            //    세 공격 모두 같은 규칙이다 — 몸은 클립이 움직이고, 이쪽은 **궤적과 피해 창**만
            //    맡는다. 칼까지 여기서 돌리면 휘두를 때마다 무기가 한 바퀴 도는 것처럼 보인다.
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

            // 칼을 손 기준 제자리로 되돌린다. 휘두르는 동안 아무도 건드리지 않았으므로
            // 보통은 이미 같은 값이지만, 이전 공격이 중간에 끊겼을 때를 위해 맞춰 둔다.
            weapon.localRotation = activeWeaponRestRotation;
            weapon.localPosition = activeWeaponRestPosition;
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

        /// <summary>이번 스윙의 사거리 안에 **종류가 안 맞는** 대상이 있었는가.</summary>
        private bool mismatchedInReach;

        private void ApplyAreaAttack(WarriorsAttackDirection direction)
        {
            hitThisAttack.Clear();
            mismatchedInReach = false;
            bool thrust = direction == WarriorsAttackDirection.Thrust;
            Vector3 center = transform.position + Vector3.up + transform.forward * (thrust ? thrustLength * .5f : 2.25f);
            float queryRadius = thrust ? thrustLength * .55f : attackRadius;
            // ⚠ 정적 Physics.* 는 **기본 물리 씬**에만 묻는다. 네트워크 세션에서는 게임 씬이
            //    러너 전용 물리 씬에 있어 결과가 늘 0 이 된다. (Warriors 네트워크 전환)
            //    러너가 없는 싱글 씬에서는 안에서 예전 함수를 그대로 부른다.
            int count = Warriors.Net.WarriorsNet.OverlapSphere(
                center, queryRadius, areaHits, ~0, QueryTriggerInteraction.Collide);
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
                // 종류가 안 맞으면 피해는 없다. 다만 **휘둘렀는데 아무 일도 안 일어난 것**과
                // **틀린 공격으로 빗나간 것**은 다르다. 앞엣것은 허공이고 뒤엣것은 실수다.
                // 그 차이를 알려 주지 않으면 "왜 안 맞지" 만 남는다. 아래에서 신호를 낸다.
                if (target.RequiredDirection != direction)
                {
                    // ⚠ **해변 몬스터는 오답으로 치지 않는다.** 1라운드는 물고기·게·해파리가 섞여
                    //    서 있어서, 가로베기 한 번에도 게와 해파리가 늘 사거리 안에 있다.
                    //    그것을 오답으로 울리면 정타를 냈는데도 매번 진동이 울린다.
                    //    원래 주석대로 <b>딴 종류는 그냥 구경꾼</b>이다.
                    //
                    //    답이 하나로 정해진 촉수(2라운드)만 오답으로 본다. 표시된 약점과
                    //    다른 방향으로 벤 것은 명백한 실수다. 3라운드 노트의 오답은
                    //    <c>WarriorsPhase3Director</c> 가 따로 판정한다.
                    if (target.IsBossPart) mismatchedInReach = true;
                    continue;
                }

                attackCandidates.Add(target);
            }

            int acceptedCount = 0;
            WarriorsTarget tentacle = NearestBossPart(attackCandidates, direction);
            if (tentacle != null)
            {
                // ROUND 2 is a pattern puzzle rather than a sweep.  One swing may only ever
                // take the single nearest tentacle, so two tentacles that happen to share a
                // weakness can never fall to the same slash.
                if (tentacle.TryReceiveAttack(direction, damage, gameObject))
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
                    if (!target.TryReceiveAttack(direction, damage, gameObject)) continue;
                    acceptedCount++;
                    target.GetComponent<WarriorsTargetFeedback>()?.PlayHit();
                }
            }
            if (acceptedCount > 0)
            {
                iotFeedback?.Request(playerId, WarriorsIoTFeedbackType.CorrectAttack, activeStrength);
                combatCamera?.Shake(Mathf.Clamp(acceptedCount * .08f, .08f, .32f));
            }
            else if (mismatchedInReach)
            {
                // **틀린 종류로 휘둘렀다.** 적은 반응하지 않는다(규칙 그대로). 대신 친 사람에게만
                // 짧은 두 번 진동으로 알린다 — 화면의 적을 건드리지 않으므로 상대에게는 아무것도
                // 보이지 않고, 자기 손에서만 "그거 아니다" 가 온다.
                iotFeedback?.Request(playerId, WarriorsIoTFeedbackType.WrongAttack, .5f);
            }

            mismatchedInReach = false;
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
                if (candidate.CanReceiveAttack(direction, gameObject))
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
