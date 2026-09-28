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
    ///   배 위의 자리       클라이언트가 자기 화면의 배에 맞춰 놓는다 (아래)
    /// </code>
    ///
    /// ⚠ 클라이언트는 <c>Complete</c> 를 부르지 않는다. 다 고쳤는지는 서버만 정하고,
    ///    다 고쳐진 구멍은 서버가 치운다. 각자 정하면 내 화면에선 막았는데
    ///    옆 사람 화면에선 아직 물이 들어온다.
    ///
    /// <b>왜 자리를 따로 보내는가.</b> 배(갑판 · 자리 · 상자)는 피어마다 조타 값으로 <b>각자</b> 돌린다
    /// (<see cref="ShipCoopShipTurn"/>). 그런데 구멍은 NetworkTransform 으로 <b>서버가 돌린 월드 자리</b>를
    /// 받았다. 그 자리는 왕복 지연 · 보간만큼 늦게 도착하므로, 배가 트는 동안 갑판은 지금 각도에 있고
    /// 구멍은 조금 전 각도에 있어서 <b>갑판 위를 슬금슬금 미끄러졌다.</b> (EC2 에서 보였다. 같은 PC 에서는
    /// 지연이 거의 없어 안 보였다) 구멍은 배에 박혀 움직이지 않으므로, 배 기준 자리를 한 번만 받아
    /// 이 화면의 배 각도로 놓으면 갑판과 딱 붙어 돈다.
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

        /// <summary>안 튼 배 기준의 자리. 서버가 스폰할 때 한 번 정한다.</summary>
        [Networked] private Vector3 ShipPosition { get; set; }

        /// <summary>안 튼 배 기준의 방향. 조각이 솟는 방향이 배와 같이 돌아야 한다.</summary>
        [Networked] private Quaternion ShipRotation { get; set; }

        private RepairTask hole;
        private ShipCoopShipTurn ship;

        public override void Spawned()
        {
            hole = GetComponent<RepairTask>();
            ship = FindAnyObjectByType<ShipCoopShipTurn>(FindObjectsInactive.Include);

            if (HasStateAuthority)
            {
                // 스폰된 자리는 서버의 배가 지금 튼 만큼 돌아가 있다. 그만큼 되돌려 안 튼 배 기준으로 적는다.
                // 서버는 계속 ShipCoopRider 로 배에 태워 두고, 거리 판정은 그 월드 자리로 한다.
                Matrix4x4 undo = ship != null ? ship.Applied.inverse : Matrix4x4.identity;
                ShipPosition = undo.MultiplyPoint3x4(transform.position);
                ShipRotation = undo.rotation * transform.rotation;
                return;
            }

            // ⚠ NetworkTransform 이 Render 에서 서버 자리를 덮어쓴다. 그게 이 화면의 배보다 늦다.
            //    Fusion 의 Render 와 배의 LateUpdate 중 어느 것이 먼저 도는지에 기대지 않으려고,
            //    둘 다 끝난 **그리기 직전**에 한 번 더 놓는다.
            Application.onBeforeRender += FollowShip;
            FollowShip();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Application.onBeforeRender -= FollowShip;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= FollowShip;
        }

        /// <summary>안 튼 배 기준 자리를 이 화면의 배가 지금 튼 만큼 돌려 놓는다. 클라이언트에서만.</summary>
        private void FollowShip()
        {
            if (this == null || Object == null || !Object.IsValid)
            {
                return;
            }

            Matrix4x4 turned = ship != null ? ship.Applied : Matrix4x4.identity;
            transform.SetPositionAndRotation(turned.MultiplyPoint3x4(ShipPosition), turned.rotation * ShipRotation);
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
