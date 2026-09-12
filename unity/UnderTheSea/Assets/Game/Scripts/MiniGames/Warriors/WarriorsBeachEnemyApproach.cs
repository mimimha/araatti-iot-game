using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsBeachEnemyApproach : MonoBehaviour
    {
        private static readonly HashSet<WarriorsBeachEnemyApproach> ActiveEnemies = new();
        [SerializeField] private Transform player;
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(0.5f)] private float stoppingDistance = 2.5f;
        [SerializeField, Min(0.1f)] private float emergeDuration = 0.7f;
        [SerializeField] private float groundHeight = 0.65f;

        private float elapsed;
        private Vector3 targetOffset;
        private float retreatUntil;

        private void OnEnable()
        {
            ActiveEnemies.Add(this);
            elapsed = 0f;
            // Give each enemy its own lane around the player instead of converging
            // on the exact same point.  Keeping the offset in front also prevents
            // a fresh wave from immediately surrounding the player's feet.
            targetOffset = new Vector3(Random.Range(-5.5f, 5.5f), 0f, Random.Range(0.4f, 2.4f));
            if (spawnPoint != null) transform.position = spawnPoint.position;
        }

        private void OnDisable() => ActiveEnemies.Remove(this);

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (player == null) return;

            elapsed += deltaTime;
            Vector3 position = transform.position;
            position.y = Mathf.Lerp(spawnPoint != null ? spawnPoint.position.y : position.y, groundHeight,
                Mathf.Clamp01(elapsed / emergeDuration));

            Vector3 playerOffset = player.position - position;
            playerOffset.y = 0f;
            if (Time.time < retreatUntil)
            {
                if (playerOffset.sqrMagnitude > .01f)
                    position -= playerOffset.normalized * (moveSpeed * 1.6f * deltaTime);
                transform.position = position;
                return;
            }
            Vector3 offset = player.position + targetOffset - position;
            offset.y = 0f;
            if (playerOffset.magnitude > stoppingDistance && offset.magnitude > .35f)
            {
                position += offset.normalized * (moveSpeed * deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(offset.normalized), 360f * deltaTime);
            }

            transform.position = position;

            const float separationRadius = 2.25f;
            Vector3 separation = Vector3.zero;
            foreach (WarriorsBeachEnemyApproach neighbour in ActiveEnemies)
            {
                if (neighbour == null || neighbour == this || !neighbour.isActiveAndEnabled) continue;
                Vector3 away = position - neighbour.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance > .001f)
                    separation += away.normalized * Mathf.Clamp01((separationRadius - distance) / separationRadius);
            }
            // Separation applies only to other WarriorsTarget roots.  Clamp the
            // correction so it opens readable gaps without overpowering pursuit.
            position += Vector3.ClampMagnitude(separation, 1.4f) * (2.1f * deltaTime);
            position.y = groundHeight;
            transform.position = position;
        }

        public void Configure(Transform playerTarget, Transform point, float height)
        {
            player = playerTarget;
            spawnPoint = point;
            groundHeight = height;
            stoppingDistance = 2.2f;
        }

        public void Retreat(float duration) => retreatUntil = Time.time + Mathf.Max(.1f, duration);
    }
}
