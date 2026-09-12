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
        [SerializeField] private Vector2 spawnInterval = new(1.5f, 2.5f);
        [SerializeField] private Vector2Int spawnCount = new(2, 4);
        [SerializeField, Min(1)] private int maxAliveEnemies = 10;
        [SerializeField, Min(.5f)] private float minimumSpawnDistance = 2.2f;
        private readonly List<WarriorsTarget> alive = new();

        public void BindPlayer(Transform target) => player = target;

        private IEnumerator Start()
        {
            if (enemyParent != null)
            {
                foreach (WarriorsTarget staleEnemy in enemyParent.GetComponentsInChildren<WarriorsTarget>(true))
                    Destroy(staleEnemy.gameObject);
            }
            alive.Clear();
            while (enabled)
            {
                yield return new WaitForSeconds(Random.Range(spawnInterval.x, spawnInterval.y));
                if (score != null && !score.IsRunning) continue;
                int count = Random.Range(spawnCount.x, spawnCount.y + 1);
                for (int i = 0; i < count && alive.Count < maxAliveEnemies; i++) TrySpawn();
            }
        }

        private void TrySpawn()
        {
            alive.RemoveAll(x => x == null || x.IsDefeated);
            if (enemyTemplates.Length == 0 || spawnPoints.Length == 0) return;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
                Vector3 position = point.position + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-.8f, .8f));
                bool clear = true;
                foreach (var existingEnemy in alive)
                    if (existingEnemy != null && Vector3.Distance(existingEnemy.transform.position, position) < minimumSpawnDistance) { clear = false; break; }
                if (!clear) continue;
                WarriorsTarget source = enemyTemplates[Random.Range(0, enemyTemplates.Length)];
                WarriorsTarget enemy = Instantiate(source, position, point.rotation, enemyParent);
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
                return;
            }
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
