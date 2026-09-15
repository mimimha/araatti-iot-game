using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 구멍 하나의 **수리 상태**를 복제한다. 네트워크 전용 파손 지점 프리팹에 붙는다.
    ///
    /// 자리와 있고 없음은 Fusion 이 알아서 한다 — 서버가 <c>Runner.Spawn</c> 한 오브젝트는
    /// <b>늦게 들어온 사람에게도</b> 그대로 생기고, <c>Runner.Despawn</c> 하면 모두에게서 사라진다.
    /// 여기서 보내는 것은 그 안의 <b>진행 상태</b>다.
    ///
    /// <code>
    ///   몇 번 두드렸는가   진행도 바가 이 값으로 찬다
    ///   판자가 왔는가      안 오면 두드려도 안 고쳐진다
    ///   다 고쳐졌는가      서버가 정한다. 클라이언트는 스스로 판단하지 않는다
    /// </code>
    ///
    /// ⚠ 클라이언트는 <c>Complete</c> 를 부르지 않는다. 다 고쳤는지는 서버만 정하고,
    ///    다 고쳐진 구멍은 서버가 치운다. 각자 정하면 내 화면에선 막았는데
    ///    옆 사람 화면에선 아직 물이 들어온다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RepairTask))]
    public sealed class ShipCoopHoleSync : NetworkBehaviour
    {
        /// <summary>두드린 횟수. 진행도 바가 이 값으로 찬다.</summary>
        [Networked] private int Hits { get; set; }

        /// <summary>수리 자재가 도착했는가.</summary>
        [Networked] private NetworkBool Plank { get; set; }

        /// <summary>다 고쳐졌는가. 서버가 정한다.</summary>
        [Networked] private NetworkBool Repaired { get; set; }

        private RepairTask hole;

        public override void Spawned()
        {
            hole = GetComponent<RepairTask>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || hole == null)
            {
                return;
            }

            Hits = hole.Hits;
            Plank = hole.HasPlank;
            Repaired = hole.IsRepaired;
        }

        public override void Render()
        {
            // 서버는 자기가 적은 값을 도로 읽을 필요가 없다.
            if (HasStateAuthority || hole == null)
            {
                return;
            }

            hole.ShowRepair(Hits, Plank, Repaired);
        }
    }
}
