using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsEnemySpawner : MonoBehaviour
    {
        [SerializeField] private WarriorsTarget[] enemyTemplates;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform player;
        [SerializeField] private Transform enemyParent;
        [SerializeField] private WarriorsBattleScore score;
        [SerializeField] private Vector2 spawnInterval = new(.55f, .95f);
        [SerializeField] private Vector2Int spawnCount = new(3, 4);
        [SerializeField, Min(1)] private int maxAliveEnemies = 12;
        [SerializeField, Min(.5f)] private float minimumSpawnDistance = 1.65f;
        [SerializeField, Range(.1f, .9f)] private float midProgress = .35f;
        [SerializeField, Range(.2f, .95f)] private float rushProgress = .75f;
        [SerializeField, Range(.2f, 1f)] private float rushIntervalScale = .55f;
        [SerializeField, Min(.15f)] private float waveAttackInterval = 1.8f;

        /// <summary>
        /// A second player doubles how fast the beach empties, so the beach has to refill
        /// to match or the round turns into waiting. The attack cadence is shared by the
        /// whole wave, so a denser beach is more to cut rather than more to survive.
        /// </summary>
        public void ConfigureForPlayers(int players)
        {
            int extra = Mathf.Clamp(players, 1, WarriorsPlayers.Max) - 1;

            // **2인이면 16마리까지.** 아래 상한 곡선이 이 값의 65% → 82% → 100% 로 올라가므로
            // 화면에 사는 수는 <b>10 → 13 → 16</b> 이 된다. (예전 8 → 10 → 12)
            //
            // 한 번에 나오는 무리도 키운다. 몰려오는 맛은 "총 마릿수" 보다 "한 번에 몇이 밀려오는가"
            // 에서 나온다. 2인이면 한 무리가 4~8 마리다.
            //
            // ⚠ 마릿수를 늘려도 <b>맞는 빈도는 늘지 않는다.</b> 공격 박자는
            //    <see cref="WarriorsEnemyAttack.ConfigureWaveCadence"/> 가 무리 전체에 하나로 주므로
            //    (waveAttackInterval 1.8초) 빽빽해질 뿐 불합리하게 두들겨 맞지는 않는다.
            maxAliveEnemies = 10 + extra * 6;
            spawnCount = new Vector2Int(3 + extra, 6 + extra * 2);
        }
        private readonly List<WarriorsTarget> alive = new();

        public void BindPlayer(Transform target) => player = target;

        /// <summary>
        /// 1라운드가 얼마나 진행됐는가(0~1). **웨이브가 바뀌는 기준이다.**
        ///
        /// ⚠ 네트워크 판에서는 <c>WarriorsBattleScore</c> 가 꺼져 있어 <c>Kills</c> 가 늘지 않는다.
        ///    그래서 진행도가 <b>영영 0</b> 이었고, 도입 그룹만 반복되며 러시 구간이 오지 않았다.
        ///    1라운드가 처음부터 끝까지 같은 밀도로 느껴진 이유다.
        ///    매치가 있으면 서버가 세는 팀 합산 처치로 잰다.
        /// </summary>
        private float RoundProgress()
        {
            Warriors.Net.WarriorsMatchState match = Warriors.Net.WarriorsMatchState.Current;

            if (match != null && match.Object != null && match.Object.IsValid && match.Phase1Target > 0)
                return Mathf.Clamp01(match.Phase1Kills / (float)match.Phase1Target);

            return score != null ? score.Progress : 0f;
        }

        /// <summary>
        /// 살아 있는 몬스터 추적 목록을 비운다.
        ///
        /// 네트워크에서는 서버가 라운드를 넘길 때 몬스터를 <c>Runner.Despawn</c> 으로
        /// 치운다. 그 경로는 <c>Defeated</c> 를 발생시키지 않으므로 이 목록에
        /// 파괴된 참조가 남는다. 그대로 두면 다음 라운드의 마릿수 계산이 어긋난다.
        /// </summary>
        public void ForgetAliveEnemies() => alive.Clear();

        /// <summary>
        /// 몬스터를 **만드는 방법**. 비어 있으면 예전처럼 <c>Instantiate</c> 한다.
        ///
        /// 네트워크에서는 서버가 <c>Runner.Spawn</c> 으로 만들도록 갈아끼운다.
        /// 그래야 두 사람이 <b>같은 몬스터</b>를 보고, 늦게 들어온 사람에게도 그대로 생긴다.
        /// (<c>WarriorsEnemyDirector</c> 가 끼우고 뺀다)
        /// </summary>
        public static System.Func<WarriorsTarget, Vector3, Quaternion, Transform, WarriorsTarget> Factory;

        // Shorthand for the three weaknesses so the composition tables below read the
        // way the design doc writes them.
        private const WarriorsAttackDirection Fish = WarriorsAttackDirection.HorizontalSlash;
        private const WarriorsAttackDirection Crab = WarriorsAttackDirection.VerticalSlash;
        private const WarriorsAttackDirection Jelly = WarriorsAttackDirection.Thrust;

        /// <summary>
        /// ROUND 1 is not a random trickle - it teaches, then mixes, then rushes.  Each
        /// wave draws from its own set of groups, written as the attack that beats each
        /// monster rather than as template indices, so reordering <see cref="enemyTemplates"/>
        /// in the prefab can never silently rewrite a wave.
        /// </summary>
        private static readonly WarriorsAttackDirection[][] EarlyGroups =
        {
            new[] { Fish, Fish },
            new[] { Crab, Crab },
            new[] { Fish, Crab },
            new[] { Jelly },
        };

        private static readonly WarriorsAttackDirection[][] MidGroups =
        {
            new[] { Fish, Fish, Crab },
            new[] { Crab, Crab, Jelly },
            new[] { Fish, Fish, Jelly, Jelly },
        };

        private static readonly WarriorsAttackDirection[][] RushGroups =
        {
            new[] { Fish, Fish, Fish, Crab, Crab },
            new[] { Crab, Crab, Jelly, Jelly },
            new[] { Fish, Crab, Jelly, Fish, Crab },
        };

        private int lastGroupIndex = -1;

        /// <summary>스폰 자리를 한 바퀴 돌 순서. <see cref="NextSpawnPoint"/> 가 관리한다.</summary>
        private int[] pointOrder;

        /// <summary>이번 바퀴에서 몇 번째 자리까지 썼는가.</summary>
        private int pointCursor;

        /// <summary>지금 내보내는 무리가 나올 자리. 무리마다 새로 고른다.</summary>
        private Transform groupPoint;

        /// <summary>
        /// **스폰 루프를 켜질 때마다 다시 시작한다.**
        ///
        /// ⚠ 예전에는 이 루프가 <c>private IEnumerator Start()</c> 였다. <c>Start</c> 는 객체 수명에
        ///    <b>딱 한 번만</b> 돌기 때문에, 아래 사정이 겹치면 다시는 몬스터가 나오지 않았다.
        ///
        /// <code>
        ///   첫 판 1페이즈   WarriorsEnemyDirector 가 spawner.enabled = true  → 루프 시작
        ///   1페이즈 종료    spawner.enabled = false → while(enabled) 탈출 → 코루틴 소멸
        ///   [다시 하기]     spawner.enabled = true  → 그러나 Start 는 다시 돌지 않는다
        /// </code>
        ///
        ///    그래서 "다시 하기를 누르면 게임은 시작되는데 몬스터가 안 나온다" 가 됐다.
        ///    <c>OnEnable</c> 은 켜질 때마다 불리므로 여기서 시작하면 새 판에서도 되살아난다.
        ///
        /// 중복으로 돌지 않게 앞 루프를 반드시 끊는다.
        /// </summary>
        private void OnEnable()
        {
            if (spawnLoop != null) StopCoroutine(spawnLoop);
            spawnLoop = StartCoroutine(SpawnLoop());
        }

        private void OnDisable()
        {
            if (spawnLoop == null) return;

            StopCoroutine(spawnLoop);
            spawnLoop = null;
        }

        private Coroutine spawnLoop;

        private IEnumerator SpawnLoop()
        {
            if (enemyParent != null)
            {
                foreach (WarriorsTarget staleEnemy in enemyParent.GetComponentsInChildren<WarriorsTarget>(true))
                    Destroy(staleEnemy.gameObject);
            }
            alive.Clear();
            lastGroupIndex = -1;
            // The cadence belongs to the wave, so it is set once here rather than fought
            // over by each enemy prefab.
            WarriorsEnemyAttack.ConfigureWaveCadence(waveAttackInterval);
            while (enabled)
            {
                float progress = RoundProgress();
                bool rush = progress >= rushProgress;

                float wait = WarriorsRun.Range(spawnInterval.x, spawnInterval.y);
                // The closing stretch has to read as a swarm, so the gap between groups
                // shrinks rather than the groups themselves getting bigger.
                if (rush) wait *= rushIntervalScale;
                yield return new WaitForSeconds(wait);

                // Disabling a component does not stop a coroutine that is already running,
                // so without this check the spawner woke from its wait after ROUND 1 had
                // ended and dropped one more group onto a beach that had just been cleared -
                // which is how a stray monster ended up floating in front of the kraken.
                if (!enabled) yield break;
                if (score != null && !score.IsRunning) continue;
                alive.RemoveAll(x => x == null || x.IsDefeated);

                // **화면에 살아 있는 수를 라운드가 흐를수록 늘린다.**
                //
                // 2인 기준 <see cref="maxAliveEnemies"/> 가 12 이므로 아래 비율이 그대로 마릿수다.
                //
                // <code>
                //   초반(진행 35% 미만)   0.65 → 10마리
                //   중반                  0.82 → 13마리
                //   후반(진행 75% 이상)   1.00 → 16마리
                // </code>
                //
                // 예전 비율(.5 / .75 / 1)은 6 · 9 · 12 였는데 초반이 휑해서
                // "무쌍처럼 쓸어버리는" 느낌이 나지 않았다. 바닥을 8 로 올린다.
                // 겹침은 <see cref="minimumSpawnDistance"/>(1.65m)와 섞어 도는
                // <see cref="NextSpawnPoint"/> 가 막는다 — 많이 보이되 한 점에 포개지지 않는다.
                int cap = Mathf.RoundToInt(maxAliveEnemies * (rush ? 1f : progress < midProgress ? .65f : .82f));

                int room = Mathf.Min(cap - alive.Count, WarriorsRun.Range(spawnCount.x, spawnCount.y + 1));
                if (room <= 0) continue;

                // 한 무리는 **한 자리에서 같이** 나온다. 무리마다 자리가 달라지므로
                // 왼쪽에서 한 무리, 오른쪽에서 다음 무리가 오는 식이 된다.
                groupPoint = NextSpawnPoint();

                foreach (WarriorsAttackDirection weakness in NextGroup(progress))
                {
                    if (room <= 0) break;
                    if (TrySpawn(TemplateFor(weakness))) room--;
                }
            }
        }

        /// <summary>
        /// Picks the next group for the wave the run is currently in, never repeating the
        /// group it just used.  That no-immediate-repeat rule is what makes the mix read as
        /// controlled rather than random; three identical groups in a row reads as a bug.
        /// </summary>
        private WarriorsAttackDirection[] NextGroup(float progress)
        {
            WarriorsAttackDirection[][] table =
                progress < midProgress ? EarlyGroups :
                progress < rushProgress ? MidGroups : RushGroups;

            int index = WarriorsRun.Range(0, table.Length);
            if (table.Length > 1 && index == lastGroupIndex)
                index = (index + 1) % table.Length;
            lastGroupIndex = index;
            return table[index];
        }

        /// <summary>
        /// 다음에 쓸 스폰 자리. **매번 무작위로 뽑지 않고 자리를 돌아가며 쓴다.**
        ///
        /// 예전에는 자리를 그때그때 무작위로 골랐다. 자리가 6곳이어도 한 무리 3마리가
        /// 같은 자리를 연달아 뽑는 일이 흔해, 실제 플레이에서는 몬스터가 정면 한 구역에
        /// 뭉쳐 나왔다. 그러면 두 사람이 제자리에서 연타만 해도 라운드가 끝난다.
        ///
        /// 한 바퀴를 섞어 두고 순서대로 쓰면, 여섯 자리가 고르게 나오면서도
        /// 매 바퀴 순서가 달라 규칙적으로 보이지 않는다. 자리가 여전히 겹치면
        /// 부르는 쪽의 <see cref="minimumSpawnDistance"/> 검사가 다음 자리로 넘긴다.
        /// </summary>
        private Transform NextSpawnPoint()
        {
            if (pointOrder == null || pointOrder.Length != spawnPoints.Length)
            {
                pointOrder = new int[spawnPoints.Length];
                for (int i = 0; i < pointOrder.Length; i++) pointOrder[i] = i;
                pointCursor = pointOrder.Length;
            }

            if (pointCursor >= pointOrder.Length)
            {
                // Fisher-Yates. 한 바퀴 끝날 때마다 다시 섞는다.
                for (int i = pointOrder.Length - 1; i > 0; i--)
                {
                    int j = WarriorsRun.Range(0, i + 1);
                    (pointOrder[i], pointOrder[j]) = (pointOrder[j], pointOrder[i]);
                }

                pointCursor = 0;
            }

            return spawnPoints[pointOrder[pointCursor++]];
        }

        private WarriorsTarget TemplateFor(WarriorsAttackDirection weakness)
        {
            foreach (WarriorsTarget template in enemyTemplates)
                if (template != null && template.RequiredDirection == weakness) return template;
            return null;
        }

        private bool TrySpawn(WarriorsTarget source)
        {
            alive.RemoveAll(x => x == null || x.IsDefeated);
            if (source == null || spawnPoints.Length == 0 || alive.Count >= maxAliveEnemies) return false;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                // 무리의 자리를 먼저 시도하고, 붐비면 빨리 다른 자리로 넘어간다.
                //
                // ⚠ 예전에는 여기서 12번까지 같은 자리를 고집했다. 그 자리에 이미 몬스터가 있으면
                //    아래 <see cref="minimumSpawnDistance"/> 검사에 계속 걸려 **한 마리도 나오지 않고**
                //    시도만 소진했다. 실측에서 1페이즈 중간에 11초 동안 처치가 0인 공백이 생겼다.
                //    무리는 유지하되 자리는 빨리 양보하는 편이 화면이 비지 않는다.
                Transform point = attempt < 5 && groupPoint != null ? groupPoint : NextSpawnPoint();

                // 무리가 퍼질 넓이. 한 무리가 최대 5마리인데 간격이 1.65m 라 좁으면 자리가 안 난다.
                Vector3 position = point.position + new Vector3(WarriorsRun.Range(-3.2f, 3.2f), 0f, WarriorsRun.Range(-1.6f, 1.6f));
                bool clear = true;
                foreach (var existingEnemy in alive)
                    if (existingEnemy != null && Vector3.Distance(existingEnemy.transform.position, position) < minimumSpawnDistance) { clear = false; break; }
                if (!clear) continue;
                WarriorsTarget enemy = Factory != null
                    ? Factory(source, position, point.rotation, enemyParent)
                    : Instantiate(source, position, point.rotation, enemyParent);

                if (enemy == null) return false;
                var approach = enemy.GetComponent<WarriorsBeachEnemyApproach>();
                if (approach == null) approach = enemy.gameObject.AddComponent<WarriorsBeachEnemyApproach>();
                approach.Configure(player, null, .65f);
                var enemyAttack = enemy.GetComponent<WarriorsEnemyAttack>();
                if (enemyAttack == null) enemyAttack = enemy.gameObject.AddComponent<WarriorsEnemyAttack>();
                enemyAttack.Configure(player != null ? player.GetComponent<WarriorsHealth>() : null, approach, DamageFor(enemy.RequiredDirection));
                foreach (Collider enemyCollider in enemy.GetComponentsInChildren<Collider>(true))
                    enemyCollider.isTrigger = true;
                foreach (Rigidbody enemyBody in enemy.GetComponentsInChildren<Rigidbody>(true))
                {
                    enemyBody.isKinematic = true;
                    enemyBody.useGravity = false;
                }
                enemy.ConfigureScore(score, ScoreFor(enemy.RequiredDirection));
                enemy.Defeated += HandleDefeated;
                alive.Add(enemy);
                enemy.gameObject.SetActive(true);
                return true;
            }
            return false;
        }

        private void HandleDefeated(WarriorsTarget enemy)
        {
            enemy.Defeated -= HandleDefeated;
            alive.Remove(enemy);
        }

        private static int ScoreFor(WarriorsAttackDirection direction) => direction switch
        {
            WarriorsAttackDirection.VerticalSlash => 200,
            WarriorsAttackDirection.Thrust => 150,
            _ => 100
        };

        private static int DamageFor(WarriorsAttackDirection direction) => direction switch
        {
            WarriorsAttackDirection.VerticalSlash => 15,
            WarriorsAttackDirection.Thrust => 10,
            _ => 5
        };
    }
}
