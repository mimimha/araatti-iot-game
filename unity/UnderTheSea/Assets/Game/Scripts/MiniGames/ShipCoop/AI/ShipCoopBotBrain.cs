using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnderTheSea.MiniGames.ShipCoop.Net;

namespace UnderTheSea.MiniGames.ShipCoop.AI
{
    public enum ShipCoopBotJob
    {
        Idle,
        DeliverPlank,
        Repair,
        Helm,
        BailWater,
        DeliverAmmo,
        Cannon,
        Sail,
    }

    /// <summary>
    /// 봇이 맡을 작업을 고른다. 이동과 실제 조작은 다음 단계가 담당한다.
    /// 서버에만 동적으로 붙으며 클라이언트에서는 실행되지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ShipCoopBotController))]
    public sealed class ShipCoopBotBrain : MonoBehaviour
    {
        [Header("사람처럼 잠깐 생각한다")]
        [SerializeField, Min(0.1f)] private float minReactionSeconds = 0.8f;
        [SerializeField, Min(0.1f)] private float maxReactionSeconds = 1.8f;
        [SerializeField, Min(0f)] private float minimumCommitSeconds = 2.5f;

        [Header("완벽하지 않은 판단")]
        [SerializeField, Range(0f, 0.5f)] private float nonCriticalMistakeChance = 0.12f;
        [SerializeField, Min(0f)] private float distancePenaltyPerMeter = 1.5f;

        private readonly List<Candidate> candidates = new List<Candidate>(16);

        private NetworkObject networkObject;
        private ShipFlooding flooding;
        private CarryTask carry;
        private TaskWorker worker;
        private float nextThinkAt;
        private float committedUntil;
        private UnityEngine.Object reservedTarget;

        public int Slot { get; private set; }
        public ShipCoopBotJob CurrentJob { get; private set; }
        public UnityEngine.Object CurrentTarget { get; private set; }
        public Transform PickupTarget { get; private set; }
        public Transform Destination { get; private set; }
        public float CurrentPriority { get; private set; }

        public event Action<ShipCoopBotJob, UnityEngine.Object> JobChanged;

        /// <summary>
        /// 운반처럼 한 단계가 끝난 직후에는 반응 지연을 기다리지 않고 다음 단계로 넘어간다.
        /// 판자를 전달한 뒤 같은 판자를 또 집는 틈을 없애고 곧바로 수리를 선택하게 한다.
        /// </summary>
        public void ReconsiderNow()
        {
            nextThinkAt = 0f;
            committedUntil = 0f;
        }

        public void Configure(int slot)
        {
            Slot = slot;
        }

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
        }

        private void Start()
        {
            flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
            carry = GetComponent<CarryTask>();
            worker = GetComponent<TaskWorker>();
            nextThinkAt = Time.time + UnityEngine.Random.Range(minReactionSeconds, maxReactionSeconds);
        }

        private void OnDisable()
        {
            ReleaseReservation();
        }

        private void Update()
        {
            if (networkObject == null || networkObject.Runner == null || !networkObject.Runner.IsServer)
            {
                return;
            }

            if (Time.time < nextThinkAt)
            {
                return;
            }

            // 운반 중에 다른 일을 고르면 들고 있던 물건을 갑판에 버리게 된다. 배달을 끝낸 뒤 다시 판단한다.
            if (carry != null && carry.IsCarrying)
            {
                return;
            }

            nextThinkAt = Time.time + UnityEngine.Random.Range(minReactionSeconds, maxReactionSeconds);

            bool currentGone = CurrentTarget == null;
            bool emergencyAvailable = HasUnclaimedEmergency();

            if (!currentGone && Time.time < committedUntil && !emergencyAvailable)
            {
                return;
            }

            ChooseJob();
        }

        private void ChooseJob()
        {
            ReleaseReservation();
            candidates.Clear();

            AddRepairJobs();
            AddHelmJobs();
            AddWaterJobs();
            AddCombatJobs();
            AddSailJobs();

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            Candidate chosen = candidates.Count > 0 ? candidates[0] : default;

            // 생존 작업에서는 실수하지 않는다. 평상시에만 가끔 두 번째 선택을 한다.
            if (candidates.Count > 1 && chosen.Score < 850f &&
                UnityEngine.Random.value < nonCriticalMistakeChance)
            {
                chosen = candidates[1];
            }

            Apply(chosen);
        }

        private void AddRepairJobs()
        {
            foreach (TaskBase task in TaskBase.All)
            {
                if (task is not RepairTask repair || repair.IsRepaired)
                {
                    continue;
                }

                if (repair.WantsPlank)
                {
                    AmmoBox source = NearestBox(Cargo.Plank, repair.transform.position);
                    Add(ShipCoopBotJob.DeliverPlank, repair, source != null ? source.transform : null,
                        repair.transform, 1050f + (1f - repair.Progress01) * 100f, 1);
                }
                else if (repair.CanHammer)
                {
                    Add(ShipCoopBotJob.Repair, repair, null, repair.transform,
                        1000f + (1f - repair.Progress01) * 100f, repair.Capacity);
                }
            }
        }

        private void AddHelmJobs()
        {
            bool reefActive = VoyageEvent.Active.Any(step => step is Reef && step.IsActive);

            foreach (TaskBase task in TaskBase.All)
            {
                if (task is not HelmTask helm)
                {
                    continue;
                }

                if (helm.IsPushed)
                {
                    Add(ShipCoopBotJob.Helm, helm, null, helm.transform,
                        helm.NeedsHelp ? 950f : 900f, helm.Capacity);
                }
                else if (reefActive && (helm.IsEmpty || CurrentTarget == helm))
                {
                    // 암초는 ExternalPush를 쓰지 않으므로 별도로 긴급 조타 작업을 만든다.
                    Add(ShipCoopBotJob.Helm, helm, null, helm.transform, 780f, 1);
                }
                else if (Mathf.Abs(helm.Heading) > 5f && (helm.IsEmpty || CurrentTarget == helm))
                {
                    // 암초를 피한 뒤 비스듬한 항로를 그대로 두지 않는다.
                    // 거의 정면으로 돌아올 때까지 조타 담당을 유지한다.
                    Add(ShipCoopBotJob.Helm, helm, null, helm.transform, 650f, 1);
                }
                else if (helm.IsEmpty || CurrentTarget == helm)
                {
                    Add(ShipCoopBotJob.Helm, helm, null, helm.transform, 300f, 1);
                }
            }
        }

        private void AddWaterJobs()
        {
            if (flooding == null || !flooding.BailingHelps)
            {
                return;
            }

            AmmoBox source = NearestBox(Cargo.Water, transform.position);
            WaterDumpPoint destination = FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Exclude)
                // 봇의 현재 위치는 수리를 마친 뒷갑판일 수 있다. 물은 중앙갑판 상자에서
                // 뜨므로 상자와 가까운 뱃전을 골라야 불필요한 층 왕복을 하지 않는다.
                .OrderBy(point => source == null
                    ? float.MaxValue
                    : (point.transform.position - source.transform.position).sqrMagnitude)
                .FirstOrDefault();

            if (source != null && destination != null)
            {
                Add(ShipCoopBotJob.BailWater, flooding, source.transform, destination.transform,
                    // 상자와 난간 사이 통로가 좁아 두 봇이 동시에 왕복하면 서로 막는다.
                    // 한 명이 두 번 왕복하는 편이 안정적이고, 나머지는 조타·돛을 지킬 수 있다.
                    850f + flooding.Level01 * 100f, 1);
            }
        }

        private void AddCombatJobs()
        {
            bool enemyActive = VoyageEvent.Active.Any(step => step is EnemyShip && step.IsActive);

            if (!enemyActive)
            {
                return;
            }

            foreach (TaskBase task in TaskBase.All)
            {
                if (task is not CannonTask cannon)
                {
                    continue;
                }

                if (cannon.Ammo <= 0)
                {
                    AmmoBox source = NearestBox(Cargo.Ammo, cannon.transform.position);
                    Add(ShipCoopBotJob.DeliverAmmo, cannon, source != null ? source.transform : null,
                        cannon.transform, 820f, 1);
                }
                else
                {
                    Add(ShipCoopBotJob.Cannon, cannon, null, cannon.transform,
                        760f + (cannon.MaxAmmo - cannon.Ammo) * 5f, cannon.Capacity);
                }
            }
        }

        private void AddSailJobs()
        {
            bool squallActive = VoyageEvent.Active.Any(step => step is Squall && step.IsActive);

            foreach (TaskBase task in TaskBase.All)
            {
                if (task is not SailTask sail)
                {
                    continue;
                }

                float score = squallActive && (sail.IsEmpty || CurrentTarget == sail)
                    ? 740f
                    : sail.IsSlack
                    ? 600f
                    : sail.IsEmpty ? 400f
                    : CurrentTarget == sail ? 380f : 0f;

                if (score > 0f)
                {
                    Add(ShipCoopBotJob.Sail, sail, null, sail.transform, score, sail.Capacity);
                }
            }
        }

        private void Add(ShipCoopBotJob job, UnityEngine.Object target, Transform pickup,
            Transform destination, float baseScore, int capacity)
        {
            if (target == null || destination == null)
            {
                return;
            }

            // 다시 판단할 때 자기 자신을 다른 작업자로 세면 정원 1인 자리에서 스스로 밀려난다.
            int humanWorkers = target is TaskBase task
                ? task.Workers.Count(assigned => assigned != worker)
                : 0;
            int available = Mathf.Max(0, capacity - humanWorkers - ShipCoopBotAssignments.Count(target));

            if (available <= 0)
            {
                return;
            }

            Transform firstStop = pickup != null ? pickup : destination;
            float distance = Vector3.Distance(transform.position, firstStop.position);
            candidates.Add(new Candidate(job, target, pickup, destination,
                baseScore - distance * distancePenaltyPerMeter));
        }

        private void Apply(Candidate chosen)
        {
            ShipCoopBotJob previousJob = CurrentJob;
            UnityEngine.Object previousTarget = CurrentTarget;

            CurrentJob = chosen.Target != null ? chosen.Job : ShipCoopBotJob.Idle;
            CurrentTarget = chosen.Target;
            PickupTarget = chosen.Pickup;
            Destination = chosen.Destination;
            CurrentPriority = chosen.Score;
            committedUntil = Time.time + minimumCommitSeconds;

            if (CurrentTarget != null)
            {
                ShipCoopBotAssignments.Reserve(CurrentTarget);
                reservedTarget = CurrentTarget;
            }

            if (previousJob != CurrentJob || previousTarget != CurrentTarget)
            {
                Debug.Log($"[ShipCoopBot {Slot + 1}] 작업 선택 — {CurrentJob} / " +
                          $"{(CurrentTarget != null ? CurrentTarget.name : "대기")} / 우선순위 {CurrentPriority:0}", this);
                JobChanged?.Invoke(CurrentJob, CurrentTarget);
            }
        }

        private bool HasUnclaimedEmergency()
        {
            if (CurrentPriority >= 850f)
            {
                return false;
            }

            return TaskBase.All.Any(task => task is RepairTask repair && !repair.IsRepaired) ||
                   TaskBase.All.Any(task => task is HelmTask helm && helm.IsPushed) ||
                   (flooding != null && flooding.BailingHelps && flooding.Level01 >= 0.15f);
        }

        private AmmoBox NearestBox(Cargo kind, Vector3 from)
        {
            return FindObjectsByType<AmmoBox>(FindObjectsInactive.Exclude)
                .Where(box => box.Kind == kind && box.HasStock)
                .OrderBy(box => (box.transform.position - from).sqrMagnitude)
                .FirstOrDefault();
        }

        private void ReleaseReservation()
        {
            if (reservedTarget != null)
            {
                ShipCoopBotAssignments.Release(reservedTarget);
            }

            reservedTarget = null;
        }

        private readonly struct Candidate
        {
            public Candidate(ShipCoopBotJob job, UnityEngine.Object target, Transform pickup,
                Transform destination, float score)
            {
                Job = job;
                Target = target;
                Pickup = pickup;
                Destination = destination;
                Score = score;
            }

            public ShipCoopBotJob Job { get; }
            public UnityEngine.Object Target { get; }
            public Transform Pickup { get; }
            public Transform Destination { get; }
            public float Score { get; }
        }
    }

    internal static class ShipCoopBotAssignments
    {
        private static readonly Dictionary<UnityEngine.Object, int> Reservations =
            new Dictionary<UnityEngine.Object, int>();

        public static int Count(UnityEngine.Object target) =>
            target != null && Reservations.TryGetValue(target, out int count) ? count : 0;

        public static void Reserve(UnityEngine.Object target)
        {
            if (target != null)
            {
                Reservations[target] = Count(target) + 1;
            }
        }

        public static void Release(UnityEngine.Object target)
        {
            if (target == null || !Reservations.TryGetValue(target, out int count))
            {
                return;
            }

            if (count <= 1)
            {
                Reservations.Remove(target);
            }
            else
            {
                Reservations[target] = count - 1;
            }
        }
    }
}
