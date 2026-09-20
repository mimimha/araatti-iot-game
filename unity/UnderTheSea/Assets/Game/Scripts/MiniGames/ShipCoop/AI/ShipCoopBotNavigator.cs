using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnderTheSea.MiniGames.ShipCoop.Net;

namespace UnderTheSea.MiniGames.ShipCoop.AI
{
    /// <summary>
    /// 봇 두뇌가 고른 목적지까지 기존 플레이어 이동 입력으로 걸어간다.
    /// 씬의 네 경사로를 웨이포인트로 사용하므로 NavMesh와 별도 베이크가 필요 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipCoopBotBrain))]
    [RequireComponent(typeof(ShipCoopBotController))]
    public sealed class ShipCoopBotNavigator : MonoBehaviour
    {
        private enum Deck
        {
            Main,
            Fore,
            Aft,
        }

        [Header("도착 판정")]
        [SerializeField, Min(0.1f)] private float waypointReach = 0.65f;
        [SerializeField, Min(0.1f)] private float destinationReach = 1.25f;

        [Header("막힘 회피")]
        [SerializeField, Min(0.2f)] private float obstacleProbe = 1.4f;
        [SerializeField, Min(0.1f)] private float sideProbe = 1.5f;
        [SerializeField, Min(0.5f)] private float stuckSeconds = 1.2f;

        private readonly List<Waypoint> route = new List<Waypoint>(6);
        private readonly List<Stair> stairs = new List<Stair>(4);
        private readonly RaycastHit[] obstacleHits = new RaycastHit[12];

        private NetworkObject networkObject;
        private ShipCoopBotBrain brain;
        private ShipCoopBotController input;
        private Transform routedTarget;
        private Transform overrideTarget;
        private int routeIndex;
        private Vector3 lastPosition;
        private float stuckFor;
        private int avoidanceSign = 1;
        private Transform shipVisual;
        private bool logMotion;
        private float nextMotionLogAt;

        public bool HasArrived { get; private set; }
        public Transform CurrentNavigationTarget => routedTarget;

        /// <summary>운반 중에는 두뇌의 집기 위치 대신 배달 위치로 이동한다.</summary>
        public void SetOverrideTarget(Transform target)
        {
            if (overrideTarget == target)
            {
                return;
            }

            overrideTarget = target;
            BuildRoute(ResolveTarget());
        }

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            brain = GetComponent<ShipCoopBotBrain>();
            input = GetComponent<ShipCoopBotController>();
        }

        private void Start()
        {
            ResolveStairs();
            GameObject ship = GameObject.Find("PirateShip");
            shipVisual = ship != null ? ship.transform : null;
            logMotion = FusionLaunchArguments.HasFlag(FusionLaunchArguments.LogMovesKey);
            brain.JobChanged += OnJobChanged;
        }

        private void OnDestroy()
        {
            if (brain != null)
            {
                brain.JobChanged -= OnJobChanged;
            }
        }

        private void Update()
        {
            if (networkObject == null || networkObject.Runner == null || !networkObject.Runner.IsServer)
            {
                return;
            }

            Transform target = ResolveTarget();

            if (target == null)
            {
                Stop();
                return;
            }

            if (target != routedTarget)
            {
                BuildRoute(target);
            }

            if (logMotion && Time.time >= nextMotionLogAt)
            {
                nextMotionLogAt = Time.time + 1f;
                Debug.Log($"[ShipCoopBotNav {brain.Slot + 1}] {brain.CurrentJob} — " +
                          $"현재 {transform.position}, 목표 {target.position}, " +
                          $"경로 {routeIndex}/{route.Count}, 도착 {HasArrived}", this);
            }

            FollowRoute();
        }

        private void OnJobChanged(ShipCoopBotJob job, Object target)
        {
            overrideTarget = null;
            Transform destination = ResolveTarget();
            BuildRoute(destination);
        }

        private Transform ResolveTarget() => overrideTarget != null
            ? overrideTarget
            : brain.PickupTarget != null ? brain.PickupTarget : brain.Destination;

        private void BuildRoute(Transform target)
        {
            route.Clear();
            routeIndex = 0;
            routedTarget = target;
            HasArrived = false;
            stuckFor = 0f;

            if (target == null)
            {
                Stop();
                return;
            }

            if (stairs.Count == 0)
            {
                ResolveStairs();
            }

            Deck from = DeckAt(transform.position.y);
            Deck to = DeckAt(target.position.y);
            Vector3 cursor = transform.position;

            // 앞갑판과 뒷갑판은 직접 이어지지 않는다. 항상 중간갑판을 거친다.
            int safety = 0;
            while (from != to && safety++ < 3)
            {
                Deck next = from == Deck.Main ? to : Deck.Main;
                Stair stair = BestStair(from, next, cursor);

                if (stair == null)
                {
                    Debug.LogWarning($"[ShipCoopBotNavigator] {from} → {next} 계단을 찾지 못해 직선 이동합니다.", this);
                    break;
                }

                bool startsLow = from == Deck.Main;
                route.Add(startsLow ? stair.Low : stair.High);
                route.Add(startsLow ? stair.High : stair.Low);
                cursor = route[^1].Position;
                from = next;
            }

            // 중앙갑판 한가운데에는 돛대와 구조물이 있어 좌우를 직선으로 가로지를 수 없다.
            // 긴 횡단은 목표와 가까운 앞/뒤 통로를 한 번 거친다. 짧은 이동과 계단 진입에는
            // 불필요한 우회를 넣지 않는다.
            if (from == Deck.Main && DeckAt(target.position.y) == Deck.Main &&
                Mathf.Abs(target.position.x - cursor.x) > 3f)
            {
                float laneOffset = cursor.z >= target.position.z ? 3.2f : -3.2f;
                float laneZ = target.position.z + laneOffset;
                route.Add(Waypoint.ForWorld(new Vector3(cursor.x, target.position.y, laneZ)));
                route.Add(Waypoint.ForWorld(new Vector3(target.position.x, target.position.y, laneZ)));
            }

            route.Add(Waypoint.ForTarget(target));

            if (logMotion)
            {
                Debug.Log($"[ShipCoopBotNav {brain.Slot + 1}] 경로 생성 — " +
                          $"{transform.position} → {target.name} {target.position}, " +
                          $"{DeckAt(transform.position.y)} → {DeckAt(target.position.y)}, 지점 {route.Count}", this);
            }
        }

        private void FollowRoute()
        {
            if (routeIndex >= route.Count)
            {
                Stop(arrived: true);
                return;
            }

            Waypoint waypoint = route[routeIndex];
            Vector3 target = waypoint.Position;
            Vector3 delta = target - transform.position;
            delta.y = 0f;

            float reach = routeIndex == route.Count - 1 ? destinationReach : waypointReach;

            if (delta.sqrMagnitude <= reach * reach)
            {
                routeIndex++;

                if (routeIndex >= route.Count)
                {
                    Stop(arrived: true);
                }

                return;
            }

            Vector3 direction = delta.normalized;

            // 상자와 수리 지점도 콜라이더를 가진다. 목적지 바로 앞에서도 회피를 켜 두면
            // 봇이 집을 거리까지 다가가지 못하고 상자 둘레만 계속 돈다.
            // 마지막 구간은 작업 사거리 안으로 곧장 들어가고, 그 전 구간에서만 회피한다.
            if (routeIndex < route.Count - 1 || delta.magnitude > destinationReach + obstacleProbe)
            {
                direction = AvoidObstacles(direction);
            }

            // LookYaw 0이면 Move x/y가 월드 x/z와 같다.
            input.SetMove(new Vector2(direction.x, direction.z), 0f, sprint: delta.magnitude > 3f);

            float moved = Vector3.Distance(transform.position, lastPosition);
            stuckFor = moved < 0.015f ? stuckFor + Time.deltaTime : 0f;
            lastPosition = transform.position;

            if (stuckFor >= stuckSeconds)
            {
                avoidanceSign *= -1;
                stuckFor = 0f;
            }
        }

        private Vector3 AvoidObstacles(Vector3 direction)
        {
            Vector3 origin = transform.position + Vector3.up * 1.1f;
            int mask = Physics.DefaultRaycastLayers;

            if (!IsBlocked(origin, direction, obstacleProbe, mask))
            {
                return direction;
            }

            Vector3 side = Vector3.Cross(Vector3.up, direction) * avoidanceSign;
            bool preferredClear = !IsBlocked(origin, side, sideProbe, mask);

            if (!preferredClear)
            {
                side = -side;
            }

            // 전진 성분을 섞으면 CharacterController 캡슐이 장애물에 계속 눌려 옆 입력도
            // 실제 이동으로 이어지지 않는다. 막힌 동안은 확실히 옆으로 빠지고,
            // 다음 프레임 시야가 열리면 원래 목적지를 다시 향한다.
            return side.normalized;
        }

        private bool IsBlocked(Vector3 origin, Vector3 direction, float distance, int mask)
        {
            int count = Physics.RaycastNonAlloc(
                origin, direction, obstacleHits, distance, mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = obstacleHits[i].collider;

                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                // 현재 가려는 상자·작업 지점의 콜라이더는 장애물이 아니라 목적지다.
                // 자식 메시가 맞는 경우와 부모 콜라이더가 맞는 경우를 모두 제외한다.
                if (routedTarget != null &&
                    (hit.transform == routedTarget ||
                     hit.transform.IsChildOf(routedTarget) ||
                     routedTarget.IsChildOf(hit.transform)))
                {
                    continue;
                }

                // 보이는 배 메시 콜라이더는 높이 측정용이고 실제 이동에서도 무시한다.
                if (shipVisual != null && hit.transform.IsChildOf(shipVisual))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void Stop(bool arrived = false)
        {
            HasArrived = arrived;
            input.SetMove(Vector2.zero, 0f, sprint: false);
        }

        private void ResolveStairs()
        {
            stairs.Clear();

            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include);

            foreach (Transform ramp in all)
            {
                if (ramp == null || ramp.name != "Ramp" || ramp.parent == null ||
                    !ramp.parent.name.StartsWith("Stairs_"))
                {
                    continue;
                }

                Vector3 a = ramp.TransformPoint(new Vector3(0f, 0f, -0.43f));
                Vector3 b = ramp.TransformPoint(new Vector3(0f, 0f, 0.43f));
                Waypoint low = Waypoint.ForLocal(ramp, a.y <= b.y ? -0.43f : 0.43f);
                Waypoint high = Waypoint.ForLocal(ramp, a.y > b.y ? -0.43f : 0.43f);
                Deck upperDeck = ramp.parent.name.Contains("Aft") ? Deck.Aft : Deck.Fore;

                stairs.Add(new Stair(upperDeck, low, high));
            }

            if (stairs.Count != 4)
            {
                Debug.LogWarning($"[ShipCoopBotNavigator] 계단 4개 중 {stairs.Count}개를 찾았습니다.", this);
            }
        }

        private Stair BestStair(Deck from, Deck to, Vector3 position)
        {
            Deck upper = from == Deck.Main ? to : from;

            return stairs
                .Where(stair => stair.UpperDeck == upper)
                .OrderBy(stair => ((from == Deck.Main ? stair.Low : stair.High).Position - position).sqrMagnitude)
                .FirstOrDefault();
        }

        private static Deck DeckAt(float y)
        {
            if (y >= -0.4f)
            {
                return Deck.Aft;
            }

            return y >= -2.65f ? Deck.Fore : Deck.Main;
        }

        private sealed class Stair
        {
            public Stair(Deck upperDeck, Waypoint low, Waypoint high)
            {
                UpperDeck = upperDeck;
                Low = low;
                High = high;
            }

            public Deck UpperDeck { get; }
            public Waypoint Low { get; }
            public Waypoint High { get; }
        }

        private readonly struct Waypoint
        {
            private Waypoint(Transform anchor, Vector3 localPosition, bool followsTarget)
            {
                Anchor = anchor;
                LocalPosition = localPosition;
                FollowsTarget = followsTarget;
            }

            private Transform Anchor { get; }
            private Vector3 LocalPosition { get; }
            private bool FollowsTarget { get; }

            public Vector3 Position => Anchor == null
                ? LocalPosition
                : FollowsTarget ? Anchor.position : Anchor.TransformPoint(LocalPosition);

            public static Waypoint ForTarget(Transform target) => new Waypoint(target, Vector3.zero, true);

            public static Waypoint ForWorld(Vector3 position) => new Waypoint(null, position, false);

            public static Waypoint ForLocal(Transform ramp, float localZ) =>
                new Waypoint(ramp, new Vector3(0f, 0f, localZ), false);
        }
    }
}
