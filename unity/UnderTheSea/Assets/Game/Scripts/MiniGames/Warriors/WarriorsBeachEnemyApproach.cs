using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsBeachEnemyApproach : MonoBehaviour
    {
        private static readonly HashSet<WarriorsBeachEnemyApproach> ActiveEnemies = new();
        [SerializeField] private Transform player;
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.2f;
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

            // **맞고 밀려나는 구간.**
            //
            // ⚠ 예전에는 <b>일정한 속도로</b> 0.12초 밀었다. 그러면 몬스터가 등속으로 미끄러져
            //   "얻어맞았다" 가 아니라 "밀려났다" 로 읽힌다. 때린 순간의 충격이 없다.
            //
            // 지금은 <b>처음에 확 튕기고 급격히 잦아든다.</b> 시작 속도를 걷는 속도의 9배로 두고
            // 남은 시간에 비례해 제곱으로 줄인다. 같은 거리를 가도 앞머리에서 거의 다 가므로
            // 맞는 순간이 눈에 박힌다.
            //
            //   moveSpeed 4.2 × 9 = 37.8 u/s 에서 시작해 0 으로 감속
            //   평균 속도는 시작의 1/3 이므로
            //     일반 타격(0.16초)  약 2.0 world unit
            //     처치(0.25초)       약 3.2 world unit
            //   걷는 속도가 4.2 라 일반 타격분은 0.48초면 따라붙는다 — 날아가 버리지 않는다.
            if (Time.time < retreatUntil)
            {
                float left = Mathf.Clamp01((retreatUntil - Time.time) / Mathf.Max(.0001f, retreatSpan));
                float speed = moveSpeed * 9f * left * left;

                position -= toPlayer * (speed * deltaTime);
                position.y = groundHeight;
                transform.position = position;
                return;
            }

            // **밀린 뒤 잠깐 멈춘다(경직).**
            //
            // ⚠ 밀리자마자 같은 속도로 다시 걸어 들어오면 "밀렸다" 가 아니라 "덜컥했다" 로만 보인다.
            //    맞은 자리에 잠시 서 있어야 밀려난 거리가 눈에 남는다.
            //    걷는 속도를 1.8 → 4.2 로 올렸으므로(무쌍처럼 몰려들게) 경직이 더 중요해졌다.
            if (Time.time < stunUntil)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(toPlayer), 180f * deltaTime);
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
        /// <summary>
        /// 맞고 뒤로 밀린다. <paramref name="duration"/> 만큼 물러난 뒤 그 1.5배 동안 굳는다.
        ///
        /// 밀린 거리(0.12초면 약 0.69 world unit)가 눈에 남으려면 곧바로 다시 걸어오면 안 된다.
        /// </summary>
        public void Retreat(float duration)
        {
            float held = Mathf.Clamp(duration, .05f, .25f);

            retreatSpan = held;
            retreatUntil = Time.time + held;

            // 경직을 밀린 시간의 1.5배 → 2.2배로 늘렸다. 튕겨 나간 거리가 눈에 남으려면
            // 그 자리에 조금 더 서 있어야 한다.
            stunUntil = retreatUntil + held * 2.2f;
        }

        /// <summary>밀리는 구간의 전체 길이(초). 감속 곡선을 그리는 데 쓴다.</summary>
        private float retreatSpan = .12f;

        /// <summary>밀린 뒤 굳어 있는 시각. 이때는 다가오지 않는다.</summary>
        private float stunUntil;
    }
}
