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
            // A tight ring around the player. The old spread reached 5.5m to the side,
            // so enemies slid past instead of closing in; spacing is separation's job.
            targetOffset = new Vector3(Random.Range(-2.2f, 2.2f), 0f, Random.Range(-0.9f, 0.9f));
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
            float playerDistance = playerOffset.magnitude;
            Vector3 toPlayer = playerDistance > .001f ? playerOffset / playerDistance : transform.forward;

            // Brief knockback after a hit, never a sustained retreat.
            if (Time.time < retreatUntil)
            {
                position -= toPlayer * (moveSpeed * 1.2f * deltaTime);
                position.y = groundHeight;
                transform.position = position;
                return;
            }

            Vector3 offset = player.position + targetOffset - position;
            offset.y = 0f;
            if (playerDistance > stoppingDistance && offset.magnitude > .35f)
            {
                position += offset.normalized * (moveSpeed * deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(offset.normalized), 360f * deltaTime);
            }
            else
            {
                // In range: hold the line facing the player instead of drifting off.
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(toPlayer), 360f * deltaTime);
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
            separation = Vector3.ClampMagnitude(separation, 1.4f);

            // Pursuit switches off inside stoppingDistance, so an unfiltered separation
            // push was the one force still acting near the player - a crowd shoved every
            // member outwards and the enemies read as running away the moment the player
            // closed in. Strip the component that points away from the player so the
            // crowd only slides sideways around each other.
            float retreatComponent = Vector3.Dot(separation, toPlayer);
            if (retreatComponent < 0f) separation -= toPlayer * retreatComponent;

            position += separation * (2.1f * deltaTime);
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

        /// <summary>Short hit reaction. Capped so it can never become a retreat.</summary>
        public void Retreat(float duration) => retreatUntil = Time.time + Mathf.Clamp(duration, .05f, .25f);
    }
}
