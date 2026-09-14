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
            maxAliveEnemies = 12 + extra * 6;
            spawnCount = new Vector2Int(2 + extra, 5 + extra * 2);
        }
        private readonly List<WarriorsTarget> alive = new();

        public void BindPlayer(Transform target) => player = target;

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

                int room = Mathf.Min(maxAliveEnemies - alive.Count, WarriorsRun.Range(spawnCount.x, spawnCount.y + 1));
                if (room <= 0) continue;

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
                Transform point = spawnPoints[WarriorsRun.Range(0, spawnPoints.Length)];
                Vector3 position = point.position + new Vector3(WarriorsRun.Range(-2.2f, 2.2f), 0f, WarriorsRun.Range(-1f, 1f));
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
