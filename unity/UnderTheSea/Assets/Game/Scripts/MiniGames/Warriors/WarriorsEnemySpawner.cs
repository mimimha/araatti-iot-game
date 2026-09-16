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
        [SerializeField] private Vector2 spawnInterval = new(.8f, 1.25f);
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
            // 2인이면 12. 위 상한 곡선이 이 값의 50% -> 75% -> 100% 로 올라간다 (6 -> 9 -> 12).
            maxAliveEnemies = 8 + extra * 4;
            spawnCount = new Vector2Int(2 + extra, 5 + extra * 2);
        }
        private readonly List<WarriorsTarget> alive = new();

        public void BindPlayer(Transform target) => player = target;

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

        private IEnumerator Start()
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
                float progress = score != null ? score.Progress : 0f;
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
                // 예전에는 처음부터 끝까지 같은 상한(2인 18)이었다. 그런데 실제 플레이에서는
                // 초반이 휑하고 후반에 뭉쳤다. 초반 4~6 · 중반 6~9 · 후반 8~12 로 올려 두면
                // 처음부터 화면에 적이 보이고, 뒤로 갈수록 몰아치는 느낌이 생긴다.
                int cap = Mathf.RoundToInt(maxAliveEnemies * (rush ? 1f : progress < midProgress ? .5f : .75f));

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
