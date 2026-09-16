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
        [SerializeField, Min(0f)] private float holdInsideReach = .55f;
        [SerializeField, Min(0f)] private float seawardMargin = .45f;

        private float elapsed;
        private Vector3 targetOffset;
        private float retreatUntil;

        /// <summary>내 몸 반지름(월드 기준). <see cref="PushOutOfBodies"/> 가 쓴다.</summary>
        private float bodyRadius = .6f;

        /// <summary>쫓는 사람의 몸 반지름. 캐릭터의 <c>CharacterController</c> 에서 읽는다.</summary>
        private float playerRadius = .35f;

        /// <summary>
        /// Where this enemy holds its line. The lane offset used to be added on top of
        /// this, which counted the spread twice: the lane already walks each enemy to one
        /// side, and adding it again pushed the ring out to four metres - well past the
        /// reach these enemies strike from. A wave would surround the player and stand
        /// there, unable to land a single hit.
        /// </summary>
        private float HoldDistance => stoppingDistance;

        private void OnEnable()
        {
            ActiveEnemies.Add(this);
            elapsed = 0f;
            // Give each enemy its own lane around the player instead of converging
            // on the exact same point.  Keeping the offset in front also prevents
            // a fresh wave from immediately surrounding the player's feet.
            // A tight ring around the player. The old spread reached 5.5m to the side,
            // so enemies slid past instead of closing in; spacing is separation's job.
            targetOffset = new Vector3(WarriorsRun.Range(-2.2f, 2.2f), 0f, WarriorsRun.Range(-0.9f, 0.9f));
            if (spawnPoint != null) transform.position = spawnPoint.position;

            CacheBodyRadius();
            CachePlayerRadius();
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

            // 맞고 밀려나는 구간. 걷는 속도의 세 배로 물러나야 "맞았다" 가 눈에 보인다.
            // 1.2배였을 때는 그냥 잠깐 멈춘 것처럼 보였다. 시간이 짧아(최대 0.25초) 멀리 날아가진 않는다.
            if (Time.time < retreatUntil)
            {
                position -= toPlayer * (moveSpeed * 3.2f * deltaTime);
                position.y = groundHeight;
                transform.position = position;
                return;
            }

            Vector3 offset = player.position + targetOffset - position;
            offset.y = 0f;
            if (playerDistance > HoldDistance && offset.magnitude > .35f)
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

            // Pursuit switches off at the hold distance, so separation is the only force
            // still acting near the player.  Left alone it shoved the crowd outwards and
            // the enemies read as running away; stripping only the outward half then let
            // the inward half push members through the player.  So: outside the hold
            // distance drop just the retreat, inside it drop the whole radial part and
            // let the crowd slide sideways around each other.
            float radial = Vector3.Dot(separation, toPlayer);
            if (playerDistance <= HoldDistance || radial < 0f) separation -= toPlayer * radial;

            position += separation * (2.1f * deltaTime);
            position.y = groundHeight;

            // Everything on this beach comes out of the sea, which is the way the camera is
            // already looking. Letting the crowd wander round the back put attackers behind
            // the player where they could not be seen or answered, so they hold the seaward
            // side. Side to side they still spread as far as the lane offset takes them.
            if (player != null) position.z = Mathf.Max(position.z, player.position.z + seawardMargin);

            transform.position = PushOutOfBodies(position);
        }

        /// <summary>
        /// **겹친 몸을 마지막에 밀어낸다.**
        ///
        /// 이 부품은 자리를 <c>transform.position</c> 에 직접 대입한다. 물리 스윕을 거치지 않으므로
        /// 몬스터에 <c>SphereCollider</c> 가 달려 있어도 <b>아무것도 막아 주지 않는다.</b>
        /// 실제 2인 플레이에서 몬스터가 플레이어 몸 안까지 들어오고 서로 한 덩어리로 겹쳐 보인 것이
        /// 이 때문이다. 쫓아가는 힘과 <c>separation</c> 은 둘 다 "미는 힘"이라 겹침을 막아 주지 못한다.
        ///
        /// 그래서 계산이 다 끝난 자리에서 한 번 더, <b>겹친 만큼 딱 밀어낸다.</b>
        /// 힘이 아니라 자리 보정이라 프레임률과 무관하게 겹치지 않는다.
        ///
        /// 몬스터끼리는 서로 절반씩만 물러난다. 각자 자기 <c>Advance</c> 에서 밀어내므로
        /// 양쪽이 전부 물러나면 두 배로 튕겨 나간다.
        /// </summary>
        private Vector3 PushOutOfBodies(Vector3 position)
        {
            if (player != null)
            {
                float need = bodyRadius + playerRadius;
                Vector3 away = position - player.position;
                away.y = 0f;
                float distance = away.magnitude;

                if (distance > .001f && distance < need) position += away / distance * (need - distance);
            }

            foreach (WarriorsBeachEnemyApproach neighbour in ActiveEnemies)
            {
                if (neighbour == null || neighbour == this || !neighbour.isActiveAndEnabled) continue;

                float need = bodyRadius + neighbour.bodyRadius;
                Vector3 away = position - neighbour.transform.position;
                away.y = 0f;
                float distance = away.magnitude;

                if (distance > .001f && distance < need) position += away / distance * ((need - distance) * .5f);
            }

            position.y = groundHeight;
            return position;
        }

        /// <summary>내 몸 반지름. 스폰할 때 몸 콜라이더에서 읽는다.</summary>
        private void CacheBodyRadius()
        {
            SphereCollider body = GetComponentInChildren<SphereCollider>();

            if (body == null) return;

            Vector3 scale = body.transform.lossyScale;
            bodyRadius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        /// <summary>쫓는 사람의 몸 반지름을 기억해 둔다. 매 프레임 찾지 않는다.</summary>
        private void CachePlayerRadius()
        {
            if (player == null) return;

            CharacterController capsule = player.GetComponent<CharacterController>();
            if (capsule != null) playerRadius = capsule.radius;
        }

        /// <summary>
        /// The reach belongs to the attack, so the attack is what decides how close the
        /// walk has to finish. Authoring a stopping distance and a strike range separately
        /// is how the two drifted apart in the first place.
        /// </summary>
        public void ConfigureHoldDistance(float attackRange) =>
            stoppingDistance = Mathf.Max(.8f, attackRange - holdInsideReach);

        public void Configure(Transform playerTarget, Transform point, float height)
        {
            player = playerTarget;
            spawnPoint = point;
            groundHeight = height;
            stoppingDistance = 2.2f;
            CacheBodyRadius();
            CachePlayerRadius();
        }

        /// <summary>
        /// **쫓아갈 사람을 바꾼다.** 서버가 가장 가까운 생존자를 골라 넣는다.
        ///
        /// <c>Configure</c> 를 다시 부르면 스폰 지점과 지면 높이까지 덮어써서
        /// 솟아오르는 연출이 다시 시작된다. 여기서는 대상만 바꾼다.
        /// </summary>
        public void RetargetPlayer(Transform playerTarget)
        {
            player = playerTarget;
            CachePlayerRadius();
        }

        /// <summary>Short hit reaction. Capped so it can never become a retreat.</summary>
        public void Retreat(float duration) => retreatUntil = Time.time + Mathf.Clamp(duration, .05f, .25f);
    }
}
