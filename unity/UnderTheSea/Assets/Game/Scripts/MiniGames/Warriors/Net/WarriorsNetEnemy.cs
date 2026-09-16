using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 몬스터 하나. **서버가 움직이고 서버가 죽인다.**
    ///
    /// <code>
    ///   서버    가장 가까운 **살아 있는** 사람을 골라 쫓아간다 · 때린다 · 죽으면 치운다
    ///   모두    NetworkTransform 이 준 자리를 그린다 · 쓰러지는 연출을 낸다
    /// </code>
    ///
    /// <b>몬스터에 주인은 없다.</b> 누가 벴는지 기록하지 않는다. 두 사람이 같은 몬스터를
    /// 같이 두들길 수 있고, 마지막 한 대가 누구 것이든 처치 수는 팀 합산으로 하나 오른다.
    ///
    /// ⚠ 클라이언트에서는 이동 · 공격 부품을 꺼 둔다. 켜 두면 각자 자기 화면에서
    ///    몬스터를 다르게 움직이고, <c>WarriorsEnemyAttack</c> 의 정적 웨이브 타이머까지
    ///    따로 돌아 완전히 다른 판이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WarriorsTarget))]
    public sealed class WarriorsNetEnemy : NetworkBehaviour
    {
        [Header("치우기")]
        [Tooltip("쓰러진 뒤 이만큼 있다가 사라진다. 쓰러지는 연출이 끝날 즈음.")]
        [SerializeField, Min(0f)] private float despawnDelay = 0.4f;

        [Header("타겟 고르기")]
        [Tooltip("이 간격(초)마다 쫓을 사람을 다시 고른다. 매 틱 고르면 낭비다.")]
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.25f;

        [Tooltip("이미 그 사람을 노리는 몬스터 한 마리마다 더해지는 가상 거리(m). 클수록 두 사람에게 고르게 나뉜다.")]
        [SerializeField, Min(0f)] private float spreadPerHunter = 3f;

        [Tooltip("지금 대상보다 이만큼(m) 더 나은 후보가 있을 때만 갈아탄다. 두 사람 사이에서 왔다갔다하지 않게.")]
        [SerializeField, Min(0f)] private float switchMargin = 1.5f;

        /// <summary>쓰러졌는가. 서버가 정하고 모두가 같은 순간에 연출을 낸다.</summary>
        [Networked] public NetworkBool Defeated { get; private set; }

        /// <summary>서버에서 살아 움직이는 몬스터들. 누가 누구를 노리는지 세는 데 쓴다.</summary>
        private static readonly List<WarriorsNetEnemy> Hunting = new List<WarriorsNetEnemy>();

        private WarriorsTarget target;
        private WarriorsHealth health;
        private WarriorsBeachEnemyApproach approach;
        private WarriorsEnemyAttack enemyAttack;
        private WarriorsPlayerLife currentTarget;

        private bool shownDefeat;
        private float nextRetargetTime;
        private TickTimer despawnTimer;

        public override void Spawned()
        {
            target = GetComponent<WarriorsTarget>();
            health = GetComponent<WarriorsHealth>();
            approach = GetComponent<WarriorsBeachEnemyApproach>();
            enemyAttack = GetComponent<WarriorsEnemyAttack>();

            if (!HasStateAuthority)
            {
                // 클라이언트는 그리기만 한다. 움직임은 NetworkTransform 이 준다.
                if (approach != null) approach.enabled = false;
                if (enemyAttack != null) enemyAttack.enabled = false;
                return;
            }

            Hunting.Add(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Hunting.Remove(this);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // 일시정지 중에는 대상도 고르지 않는다. 이동 · 공격 자체는 서버의 timeScale 이 세운다.
            if (WarriorsMatchState.PausedNow) return;

            if (!Defeated && health != null && health.IsDead)
            {
                Defeated = true;
                despawnTimer = TickTimer.CreateFromSeconds(Runner, despawnDelay);

                WarriorsMatchState match = WarriorsMatchState.Current;
                if (match != null) match.ReportPhase1Kill();
            }

            if (Defeated)
            {
                if (despawnTimer.Expired(Runner)) Runner.Despawn(Object);
                return;
            }

            if (Time.time < nextRetargetTime) return;
            nextRetargetTime = Time.time + retargetInterval;

            Retarget();
        }

        /// <summary>
        /// 쫓을 사람을 고른다. **가깝되, 이미 많이 몰린 사람은 피한다.**
        ///
        /// 예전에는 가장 가까운 사람만 골랐다. 두 사람이 나란히 서 있으면 모든 몬스터가
        /// 몇 cm 더 가까운 한 사람에게 쏠려, 다른 사람은 구경만 했다.
        /// 그래서 거리에 "이미 그 사람을 노리는 몬스터 수 × <see cref="spreadPerHunter"/>" 를 더해
        /// 비교한다. 여섯 마리가 1P 에 붙어 있으면 2P 가 18m 안에만 있어도 2P 쪽이 이긴다.
        ///
        /// 지금 대상은 <see cref="switchMargin"/> 만큼 유리하게 본다 — 매 0.25초 두 사람 사이를
        /// 오가며 제자리걸음하지 않게.
        ///
        /// 쓰러진 사람은 후보에서 빠진다. 아무도 없으면 대상을 비워 몬스터가 제자리에 서 있게 한다.
        /// 시체를 계속 쫓게 두면 남은 사람이 반대편에서 편하게 정리해 버린다.
        /// </summary>
        private void Retarget()
        {
            WarriorsPlayerLife[] crew = FindObjectsByType<WarriorsPlayerLife>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            WarriorsPlayerLife best = null;
            float bestScore = float.MaxValue;
            float currentScore = float.MaxValue;
            Vector3 here = transform.position;

            bool keepable = currentTarget != null && currentTarget.IsLive && !currentTarget.IsDown;

            foreach (WarriorsPlayerLife one in crew)
            {
                if (one == null || !one.IsLive || one.IsDown) continue;

                float distance = Vector3.Distance(one.transform.position, here);
                int hunters = CountHunting(one);
                float score = distance + hunters * spreadPerHunter;

                if (one == currentTarget) currentScore = score;

                if (score >= bestScore) continue;

                bestScore = score;
                best = one;
            }

            // 지금 대상이 아직 괜찮으면 굳이 갈아타지 않는다.
            if (keepable && best != currentTarget && bestScore > currentScore - switchMargin) best = currentTarget;

            currentTarget = best;

            if (approach != null)
            {
                approach.RetargetPlayer(best != null ? best.transform : null);
            }

            if (enemyAttack != null)
            {
                enemyAttack.RetargetPlayer(best != null ? best.GetComponent<WarriorsHealth>() : null);
            }
        }

        /// <summary>나 말고 이 사람을 노리는 몬스터 수.</summary>
        private int CountHunting(WarriorsPlayerLife life)
        {
            int count = 0;

            foreach (WarriorsNetEnemy other in Hunting)
            {
                if (other == null || other == this || other.Defeated) continue;
                if (other.currentTarget == life) count++;
            }

            return count;
        }

        /// <summary>쓰러지는 모습은 모든 화면에서 같은 순간에 난다.</summary>
        public override void Render()
        {
            if (shownDefeat || !Defeated || target == null) return;

            shownDefeat = true;
            target.ShowDefeated();
        }
    }
}
