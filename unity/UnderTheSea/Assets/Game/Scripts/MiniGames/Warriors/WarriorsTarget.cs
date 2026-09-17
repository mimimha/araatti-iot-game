using System;
using UnityEngine;

namespace Warriors
{
    [RequireComponent(typeof(WarriorsHealth))]
    public sealed class WarriorsTarget : MonoBehaviour
    {
        [SerializeField] private WarriorsAttackDirection requiredDirection;
        [SerializeField] private WarriorsHealth health;
        [SerializeField, Min(0)] private int scoreValue = 100;
        [SerializeField, Min(0f)] private float directionalHitPadding;
        [SerializeField, Min(1)] private int bossHitsRequired = 2;
        private WarriorsBattleScore battleScore;
        private bool deathHandled;
        private bool showScorePopup = true;
        private bool acceptsAttacks = true;
        private bool destroyOnDefeat = true;
        private int bossHitsRemaining;
        private bool isBossPart;
        private float nextBossHitTime;

        public WarriorsAttackDirection RequiredDirection => requiredDirection;
        public bool IsDefeated => health != null && health.IsDead;
        public float DirectionalHitPadding => directionalHitPadding;
        public int BossHitsRemaining => bossHitsRemaining;

        /// <summary>
        /// True for kraken tentacles.  A swing resolves against boss parts one at a
        /// time, so this is what separates the ROUND 1 sweep from the ROUND 2 puzzle.
        /// </summary>
        public bool IsBossPart => isBossPart;

        /// <summary>
        /// How much of this tentacle's strike window is left, 1 down to 0, or -1 when it
        /// has none. A solo player has to answer a two arm pattern one arm after the
        /// other, so knowing how long is left is the difference between a decision and a
        /// guess. Display only - the strike itself is the boss's business.
        /// </summary>
        public float StrikeWindowNormalized =>
            strikeWindowEnd < 0f || strikeWindowLength <= 0f
                ? -1f
                : Mathf.Clamp01((strikeWindowEnd - Time.time) / strikeWindowLength);

        private float strikeWindowEnd = -1f;
        private float strikeWindowLength;

        public void BeginStrikeWindow(float seconds)
        {
            strikeWindowLength = Mathf.Max(.01f, seconds);
            strikeWindowEnd = Time.time + strikeWindowLength;
        }

        public void ClearStrikeWindow() => strikeWindowEnd = -1f;

        public event Action<WarriorsTarget> Defeated;
        public event Action<WarriorsTarget, WarriorsAttackDirection> HitAccepted;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<WarriorsHealth>();
            }
        }

        private void OnEnable()
        {
            deathHandled = false;
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        /// <summary>
        /// Whether this target would accept the swing right now. The player's swing uses it
        /// to skip a tentacle that is still inside its hit cooldown and take the next
        /// matching one instead - otherwise a second tentacle sharing a weakness with the
        /// one just hit could never be reached.
        /// </summary>
        public bool CanReceiveAttack(WarriorsAttackDirection direction, GameObject attacker = null)
        {
            // ⚠ 담당이 갈린 대상은 담당자만 고를 수 있다. (Warriors 네트워크 전환)
            //    2페이즈 촉수가 좌우로 나뉘어 있어서다. 여기서 걸러 두면 남의 팔이
            //    '가장 가까운 것' 으로 뽑혀 내 스윙을 삼키는 일이 없다.
            //    걸개가 없는 싱글 씬에서는 늘 참이라 예전 그대로다.
            if (!Warriors.Net.WarriorsNet.OwnsAttack(this, attacker)) return false;

            if (!acceptsAttacks || deathHandled) return false;
            if (direction != requiredDirection) return false;
            return destroyOnDefeat || Time.time >= nextBossHitTime;
        }

        public bool TryReceiveAttack(WarriorsAttackDirection direction, int damage, GameObject attacker = null)
        {
            // ⚠ **맞았는지는 계산하는 쪽만 정한다.** (Warriors 네트워크 전환)
            //    타격은 각자의 물리 콜라이더에서 나오므로, 막지 않으면 클라이언트마다
            //    자기 화면에서만 몬스터를 벤다. 내 화면엔 죽었는데 남의 화면엔 살아 있다.
            //    담당이 갈린 대상(2페이즈 촉수)이면 담당자인지도 함께 본다.
            //    싱글 씬(Runner 없음)에서는 늘 참이라 예전 그대로다.
            if (!Warriors.Net.WarriorsNet.CanResolveHit(this, attacker)) return false;

            if (!acceptsAttacks || deathHandled) return false;
            if (direction != requiredDirection) return false;
            if (!destroyOnDefeat)
            {
                if (Time.time < nextBossHitTime) return false;
                nextBossHitTime = Time.time + .3f;
                bossHitsRemaining = Mathf.Max(0, bossHitsRemaining - 1);
                HitAccepted?.Invoke(this, direction);
                if (bossHitsRemaining > 0) return true;
            }
            return health != null && health.TryKill();
        }

        public void ConfigureRequiredDirection(WarriorsAttackDirection direction)
        {
            requiredDirection = direction;
        }

        public void ConfigureScore(WarriorsBattleScore score, int points)
        {
            battleScore = score;
            scoreValue = Mathf.Max(0, points);
        }

        public void ConfigureAsBossPart(int hitsRequired = 2)
        {
            isBossPart = true;
            RestoreHitboxes();
            // Tentacles stand out to the sides of a player who does not walk around, and the
            // thrust hitbox is a narrow forward box.  Without this a tentacle parked off to
            // one side could never be reached by a thrust at all, so a solo run would stall
            // on any pattern that asked for one.  Only the nearest matching boss part is ever
            // hit, so widening the reach cannot make a swing catch two tentacles at once.
            directionalHitPadding = Mathf.Max(directionalHitPadding, 3f);
            battleScore = null;
            scoreValue = 0;
            showScorePopup = false;
            destroyOnDefeat = false;
            bossHitsRequired = Mathf.Max(1, hitsRequired);
            bossHitsRemaining = bossHitsRequired;
            nextBossHitTime = 0f;
            deathHandled = false;
            health?.ResetHealth();
            GetComponent<WarriorsTargetFeedback>()?.ConfigureAsBossPart();
        }

        public void SetAttackEnabled(bool enabled) => acceptsAttacks = enabled;

        /// <summary>
        /// Puts the hitboxes back. Dying switches every collider off, and a tentacle slot is
        /// reused by later patterns, so without this a tentacle that went down once could
        /// never be struck again - it stood there with its weakness showing and swallowed
        /// every swing.
        /// </summary>
        private void RestoreHitboxes()
        {
            foreach (Collider hitbox in GetComponentsInChildren<Collider>(true)) hitbox.enabled = true;
        }

        public void ReviveBossPart(WarriorsAttackDirection direction)
        {
            requiredDirection = direction;
            deathHandled = false;
            health?.ResetHealth();
            bossHitsRemaining = bossHitsRequired;
            nextBossHitTime = 0f;
            RestoreHitboxes();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// **쓰러지는 모습만 보여준다.** 점수도 세지 않고 사라지지도 않는다.
        ///
        /// 네트워크에서 쓴다. 죽었는지는 서버가 정하고, 클라이언트는 그 결과를 받아
        /// 이 함수로 같은 연출을 낸다. 여러 번 불러도 한 번만 재생된다.
        /// </summary>
        public void ShowDefeated()
        {
            if (deathHandled) return;
            deathHandled = true;

            GetComponent<WarriorsTargetFeedback>()?.PlayDefeat();

            if (showScorePopup) gameObject.AddComponent<WarriorsScorePopup>().Show(scoreValue);

            foreach (Collider hitbox in GetComponentsInChildren<Collider>()) hitbox.enabled = false;

            // 시체가 계속 걸어오면 물고기와 게는 비틀거리는 것처럼 보이지만
            // 해파리는 떠다녀서 아직 살아 있는 것처럼 보인다.
            if (TryGetComponent(out WarriorsBeachEnemyApproach approach)) approach.enabled = false;
            if (TryGetComponent(out WarriorsEnemyAttack enemyAttack)) enemyAttack.enabled = false;
        }

        private void HandleDied()
        {
            if (deathHandled) return;

            // 보이는 것은 모두 여기서 난다. 점수와 사라지는 것만 아래에서 따로 한다.
            ShowDefeated();

            if (showScorePopup) battleScore?.RegisterKill(scoreValue);

            Defeated?.Invoke(this);

            // Just past the defeat animation. The kill has to land the moment the swing
            // does, so there is no window where a beaten monster is still on screen.
            // 네트워크에서는 서버가 Runner.Despawn 으로 치운다. (WarriorsNetEnemy)
            if (destroyOnDefeat && !Warriors.Net.WarriorsNet.IsNetworked) Destroy(gameObject, .4f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (health == null)
            {
                health = GetComponent<WarriorsHealth>();
            }
        }
#endif
    }
}
