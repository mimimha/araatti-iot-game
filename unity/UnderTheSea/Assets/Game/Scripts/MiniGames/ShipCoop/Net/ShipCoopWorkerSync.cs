using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 이 사람이 **무엇을 하고 있는지**를 서버가 정하고 모두에게 알린다.
    ///
    /// <code>
    ///   서버    TaskWorker · CarryTask · ShipCoopHelp 가 돈다 → 결과를 [Networked] 에 적는다
    ///   클라    그 셋을 꺼 둔다 → 복제받은 값을 화면에 옮긴다
    /// </code>
    ///
    /// <b>왜 클라이언트에서 꺼야 하는가.</b> 그대로 두면 각자 자기 화면에서만 자리에 붙는다.
    /// 내 화면에선 조타륜을 잡았는데 서버는 모르는 상태가 되고, 배는 안 돈다.
    /// 자리가 하나뿐인데 두 사람이 동시에 잡았다고 믿는 일도 생긴다.
    ///
    /// ⚠ <b>가까운 자리 찾기(<c>Nearby</c>)는 클라이언트에서도 계속 돈다.</b>
    ///    "여기서 E" 안내는 화면 쪽 일이라 서버에 물을 필요가 없다.
    ///    물으면 왕복 시간만큼 늦게 떠서 손맛이 나빠진다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TaskWorker))]
    public sealed class ShipCoopWorkerSync : NetworkBehaviour
    {
        /// <summary>붙어 있는 자리의 번호. 0 이면 아무 데도 안 붙었다. (<see cref="ShipCoopTaskIds"/>)</summary>
        [Networked]
        private int CurrentTaskId { get; set; }

        /// <summary>양손이 묶였는가. 운반이 켠다. 달리기를 막고 자리에 못 붙게 한다.</summary>
        [Networked]
        private NetworkBool BusyHands { get; set; }

        /// <summary>들고 있는 것. <c>Cargo</c> 를 숫자로 담는다.</summary>
        [Networked]
        private int CarriedCargo { get; set; }

        /// <summary>도움을 요청한 상태인가.</summary>
        [Networked]
        private NetworkBool CallingHelp { get; set; }

        private TaskWorker worker;
        private CarryTask carry;
        private ShipCoopHelp help;

        private int appliedTaskId;
        private int appliedCargo;
        private bool appliedCalling;

        public override void Spawned()
        {
            worker = GetComponent<TaskWorker>();
            carry = GetComponent<CarryTask>();
            help = GetComponent<ShipCoopHelp>();

            bool server = HasStateAuthority;

            // 판정하는 쪽만 붙기를 정한다. 가까운 자리 찾기는 양쪽 다 계속한다.
            worker.DecidesJoin = server;

            // 집기 판정은 서버에서만 돈다.
            if (carry != null)
            {
                carry.enabled = server;
            }

            // ⚠ ShipCoopHelp 는 **끄지 않는다.** 끄면 정적 목록(Calling)에서 빠져
            //    HUD 가 "누가 부르고 있는지" 를 못 본다.
            //    요청할지 말지의 판정은 그 안에서 권위 가드로 막혀 있다.

            appliedTaskId = 0;
            appliedCargo = 0;
            appliedCalling = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || worker == null)
            {
                return;
            }

            CurrentTaskId = ShipCoopTaskIds.IdOf(worker.Current);
            BusyHands = worker.HandsBusy;
            CarriedCargo = carry != null ? (int)carry.Carrying : 0;
            CallingHelp = help != null && help.IsCalling;
        }

        public override void Render()
        {
            // 서버는 자기가 적은 값을 도로 읽을 필요가 없다. 이미 그 상태다.
            if (HasStateAuthority || worker == null)
            {
                return;
            }

            ApplySeat();
            ApplyHands();
        }

        /// <summary>서버가 정한 자리를 이 화면에도 똑같이 만든다.</summary>
        private void ApplySeat()
        {
            if (CurrentTaskId == appliedTaskId)
            {
                return;
            }

            TaskBase want = ShipCoopTaskIds.Find(CurrentTaskId);

            // 아직 그 자리가 이 화면에 없을 수 있다. (수리 지점처럼 도중에 생기는 자리)
            // 그러면 이번 프레임은 건너뛰고 다음에 다시 본다.
            if (CurrentTaskId != 0 && want == null)
            {
                return;
            }

            if (worker.Current != null)
            {
                worker.LeaveCurrent();
            }

            if (want != null)
            {
                worker.Join(want);
            }

            appliedTaskId = CurrentTaskId;
        }

        /// <summary>손에 든 것과 도움 요청을 화면에 옮긴다.</summary>
        private void ApplyHands()
        {
            worker.HandsBusy = BusyHands;

            if (carry != null && CarriedCargo != appliedCargo)
            {
                carry.ShowCarrying((Cargo)CarriedCargo);
                appliedCargo = CarriedCargo;
            }

            if (help != null && CallingHelp && !appliedCalling)
            {
                help.Call();
            }

            appliedCalling = CallingHelp;
        }
    }
}
