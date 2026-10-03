using System.Linq;
using Fusion;
using UnityEngine;
using UnderTheSea.MiniGames.ShipCoop.Net;

namespace UnderTheSea.MiniGames.ShipCoop.AI
{
    /// <summary>도착한 봇이 사람과 같은 버튼·손 입력으로 실제 작업을 수행한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipCoopBotBrain))]
    [RequireComponent(typeof(ShipCoopBotNavigator))]
    [RequireComponent(typeof(ShipCoopBotController))]
    public sealed class ShipCoopBotAction : MonoBehaviour
    {
        [Header("사람보다 약간 느린 손")]
        [SerializeField, Min(0.2f)] private float minHammerInterval = 0.65f;
        [SerializeField, Min(0.2f)] private float maxHammerInterval = 1.05f;
        [SerializeField, Min(0.2f)] private float cannonInterval = 1.1f;
        [SerializeField, Range(0.1f, 1f)] private float sailPull = 0.72f;
        [SerializeField, Range(0.1f, 1f)] private float desiredSailPower = 0.82f;

        private NetworkObject networkObject;
        private ShipCoopBotBrain brain;
        private ShipCoopBotNavigator navigator;
        private ShipCoopBotController input;
        private TaskWorker worker;
        private CarryTask carry;

        private float releaseInteractAt = -1f;
        private float releaseFireAt = -1f;
        private float nextActionAt;
        private bool wasCarryingCargo;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            brain = GetComponent<ShipCoopBotBrain>();
            navigator = GetComponent<ShipCoopBotNavigator>();
            input = GetComponent<ShipCoopBotController>();
            worker = GetComponent<TaskWorker>();
            carry = GetComponent<CarryTask>();
        }

        private void Start()
        {
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

            ReleasePulses();

            if (IsCargoJob(brain.CurrentJob))
            {
                UpdateCargo();
                return;
            }

            navigator.SetOverrideTarget(null);

            if (releaseInteractAt <= 0f)
            {
                input.SetButton(ShipCoopButton.RightButton1, false);
            }

            if (brain.CurrentTarget == null)
            {
                ClearWorkAxes();
                return;
            }

            TaskBase task = brain.CurrentTarget as TaskBase;

            // Navigator의 도착 판정과 별개로 게임 규칙상 실제 상호작용 범위에 들어왔다면
            // 즉시 작업을 시작한다. 큰 대포 콜라이더에 막혀 중심점까지 못 가는 경우를 포함한다.
            if (task == null || !task.IsInRange(transform.position))
            {
                ClearWorkAxes();
                return;
            }

            if (worker.Current != task)
            {
                ClearWorkAxes();
                PulseInteract();
                return;
            }

            switch (brain.CurrentJob)
            {
                case ShipCoopBotJob.Repair:
                    Hammer();
                    break;

                case ShipCoopBotJob.Helm:
                    Steer(task as HelmTask);
                    break;

                case ShipCoopBotJob.Sail:
                    PullSail(task as SailTask);
                    break;

                case ShipCoopBotJob.Cannon:
                    FireCannon(task as CannonTask);
                    break;

                default:
                    ClearWorkAxes();
                    break;
            }
        }

        private void UpdateCargo()
        {
            if (carry == null)
            {
                return;
            }

            Cargo expected = ExpectedCargo(brain.CurrentJob);

            if (!carry.IsCarrying)
            {
                navigator.SetOverrideTarget(null);

                // 직전 프레임까지 들고 있었다면 방금 배달·장전·배수를 끝낸 것이다.
                // 기존 운반 작업으로 상자를 다시 집기 전에 즉시 다음 작업을 고른다.
                if (wasCarryingCargo)
                {
                    wasCarryingCargo = false;
                    input.SetButton(ShipCoopButton.RightButton1, false);
                    brain.ReconsiderNow();
                    return;
                }

                // 상자 중심까지의 내비게이션 거리보다 실제 집기 사거리가 더 넓다.
                // 캐릭터 캡슐이 상자 콜라이더에 닿으면 중심 1.25m까지 갈 수 없으므로,
                // 기존 CarryTask가 쓰는 판정으로 집을 수 있는 순간 바로 버튼을 누른다.
                AmmoBox reachable = carry.FindReachableBox();
                bool canPickUp = reachable != null && reachable.Kind == expected;

                if (!canPickUp)
                {
                    input.SetButton(ShipCoopButton.RightButton1, false);
                    return;
                }

                // 집는 순간부터 목적지까지 계속 쥐고 있어야 한다.
                input.SetButton(ShipCoopButton.RightButton1, true);
                return;
            }

            wasCarryingCargo = true;

            if (carry.Carrying != expected)
            {
                // 예상과 다른 물건을 주웠다면 그 자리에서 놓고 다시 찾는다.
                input.SetButton(ShipCoopButton.RightButton1, false);
                return;
            }

            navigator.SetOverrideTarget(brain.Destination);

            // 배달도 목적지 중심 거리가 아니라 게임 규칙의 실제 전달 사거리로 판단한다.
            // 판자·물·포탄이 각자 다른 범위를 쓰므로 CarryTask의 기존 탐색을 그대로 따른다.
            if (!CanDeliver(expected))
            {
                input.SetButton(ShipCoopButton.RightButton1, true);
                return;
            }

            // 목적지에서 손을 떼면 기존 CarryTask가 전달·장전·배수를 판정한다.
            input.SetButton(ShipCoopButton.RightButton1, false);
        }

        private bool CanDeliver(Cargo cargo)
        {
            return cargo switch
            {
                Cargo.Plank => carry.FindPointWantingPlank() != null,
                Cargo.Ammo => carry.FindLoadableCannon() != null,
                Cargo.Water => carry.FindReachableDump() != null,
                _ => false,
            };
        }

        private void Steer(HelmTask helm)
        {
            if (helm == null)
            {
                return;
            }

            Reef reef = VoyageEvent.Active
                .OfType<Reef>()
                .FirstOrDefault(active => active.IsActive);

            float steer;

            if (helm.IsPushed)
            {
                steer = -Mathf.Sign(helm.ExternalPushPerSecond);
            }
            else if (reef != null)
            {
                // 바위가 우현(+1)이면 좌현(-)으로, 좌현이면 우현(+)으로 피한다.
                // 최대각에 계속 박아 두지 않고 48도 부근을 목표로 잡아 사람다운 보정을 한다.
                float desiredHeading = -reef.RockSide * 48f;
                steer = Mathf.Clamp((desiredHeading - helm.Heading) / 12f, -1f, 1f);
            }
            else
            {
                steer = Mathf.Clamp(-helm.Heading / 15f, -1f, 1f);
            }

            input.SetHands(steer, steer, 0f, 0f);
        }

        private void PullSail(SailTask sail)
        {
            if (sail == null)
            {
                return;
            }

            bool squallActive = VoyageEvent.Active
                .Any(step => step is Squall && step.IsActive);

            // 돌풍 예고부터 미리 접고, 부는 동안에는 다시 펴려는 바람보다 강하게
            // 계속 풀어 둔다. 사건이 끝나면 아래 평상시 목표치까지 자동으로 다시 당긴다.
            float pull = squallActive
                ? -1f
                : sail.SailPower01 < desiredSailPower ? sailPull : 0f;
            input.SetHands(0f, 0f, pull, pull);
        }

        private void Hammer()
        {
            ClearWorkAxes();

            if (Time.time < nextActionAt)
            {
                return;
            }

            PulseFire();
            nextActionAt = Time.time + Random.Range(minHammerInterval, maxHammerInterval);
        }

        private void FireCannon(CannonTask cannon)
        {
            ClearWorkAxes();

            // 작업 재선택에는 사람다운 반응 지연이 있다. 그 사이 적선이 이미 격파됐다면
            // 기존 Cannon 작업이 잠깐 남아 있어도 남은 포탄을 허공에 쏘지 않는다.
            bool enemyActive = VoyageEvent.Active
                .Any(step => step is EnemyShip && step.IsActive);

            if (!enemyActive || cannon == null || !cannon.CanFire || Time.time < nextActionAt)
            {
                return;
            }

            PulseFire();
            nextActionAt = Time.time + cannonInterval;
        }

        private void PulseInteract()
        {
            if (Time.time < nextActionAt || releaseInteractAt > 0f)
            {
                return;
            }

            input.SetButton(ShipCoopButton.RightButton1, true);
            releaseInteractAt = Time.time + 0.15f;
            nextActionAt = Time.time + 0.45f;
        }

        private void PulseFire()
        {
            input.SetButton(ShipCoopButton.RightButton2, true);
            releaseFireAt = Time.time + 0.15f;
        }

        private void ReleasePulses()
        {
            if (releaseInteractAt > 0f && Time.time >= releaseInteractAt)
            {
                input.SetButton(ShipCoopButton.RightButton1, false);
                releaseInteractAt = -1f;
            }

            if (releaseFireAt > 0f && Time.time >= releaseFireAt)
            {
                input.SetButton(ShipCoopButton.RightButton2, false);
                releaseFireAt = -1f;
            }
        }

        private void OnJobChanged(ShipCoopBotJob job, Object target)
        {
            if (worker != null && worker.Current != null && worker.Current != target)
            {
                worker.LeaveCurrent();
            }

            navigator.SetOverrideTarget(null);
            wasCarryingCargo = carry != null && carry.IsCarrying;
            input.SetButton(ShipCoopButton.RightButton1, false);
            input.SetButton(ShipCoopButton.RightButton2, false);
            ClearWorkAxes();
            nextActionAt = Time.time + Random.Range(0.2f, 0.55f);
        }

        private void ClearWorkAxes()
        {
            input.SetHands(0f, 0f, 0f, 0f);
            input.SetLook(Vector2.zero);
        }

        private static bool IsCargoJob(ShipCoopBotJob job) =>
            job == ShipCoopBotJob.DeliverPlank ||
            job == ShipCoopBotJob.DeliverAmmo ||
            job == ShipCoopBotJob.BailWater;

        private static Cargo ExpectedCargo(ShipCoopBotJob job)
        {
            return job switch
            {
                ShipCoopBotJob.DeliverPlank => Cargo.Plank,
                ShipCoopBotJob.DeliverAmmo => Cargo.Ammo,
                ShipCoopBotJob.BailWater => Cargo.Water,
                _ => Cargo.None,
            };
        }
    }
}
