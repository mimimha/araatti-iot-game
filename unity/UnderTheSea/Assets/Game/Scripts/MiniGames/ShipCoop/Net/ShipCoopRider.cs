using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 이 사람을 배에 태운다. **서버에서만 태운다.**
    ///
    /// <b>왜 필요한가.</b> 배는 제자리에서 튼다. 갑판 · 작업 자리 · 상자는
    /// <c>ShipCoopShipTurn.carried</c> 에 인스펙터로 묶여 있어 같이 돈다.
    /// 그런데 <b>접속해서 생긴 사람은 씬 파일에 없다.</b> 인스펙터로 묶을 방법이 없어서
    /// 배만 돌고 사람은 제자리에 남는다. 배가 14도 틀면 사람이 갑판 밖으로 밀려난다.
    ///
    /// ⚠ <b>서버에서만 태우는 이유.</b> 클라이언트의 사람은 자리를
    ///    <c>NetworkTransform</c> 으로 받는다. 그 자리에는 <b>서버가 이미 배와 함께 돌린 결과</b>가
    ///    들어 있다. 클라이언트에서 또 돌리면 두 번 돌아가 남의 화면에서만 갑판 밖으로 나간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopRider : NetworkBehaviour
    {
        private ShipCoopShipTurn ship;

        public override void Spawned()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            ship = FindAnyObjectByType<ShipCoopShipTurn>(FindObjectsInactive.Include);

            if (ship == null)
            {
                Debug.LogWarning(
                    "[ShipCoopRider] 배를 찾지 못했습니다. 배가 틀어도 이 사람은 따라 돌지 않습니다.", this);
                return;
            }

            ship.Carry(transform);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (ship != null)
            {
                ship.StopCarrying(transform);
            }
        }
    }
}
