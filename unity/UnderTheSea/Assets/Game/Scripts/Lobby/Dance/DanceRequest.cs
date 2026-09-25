using Fusion;
using UnderTheSea.Network;
using UnityEngine;

namespace UnderTheSea.Lobby.Dance
{
    /// <summary>
    /// **춤 휠에서 고르면 서버에 춤을 청한다.** 휠(<see cref="DanceWheelView"/>)과 캐릭터
    /// (<see cref="NetworkPlayerMover.RpcRequestDance"/>) 사이를 잇기만 한다.
    ///
    /// <code>
    ///   휠에서 N 번 칸 선택  →  DanceSelected(N-1)
    ///     → 내 캐릭터의 RpcRequestDance(N)          클라이언트 → 서버
    ///     → 서버가 Dance = N                        [Networked]
    ///     → 모든 피어의 Render 가 애니메이터에 Dance = N   → 모두가 같은 춤을 본다
    /// </code>
    ///
    /// 멈추는 것도 서버가 한다. 움직이거나 뛰거나 물에 들어가면 서버가 0 으로 되돌린다.
    /// </summary>
    public static class DanceRequest
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            // 플레이 모드에 다시 들어올 때 두 번 걸리지 않게 먼저 뗀다.
            DanceWheelView.DanceSelected -= OnSelected;
            DanceWheelView.DanceSelected += OnSelected;
        }

        private static void OnSelected(int slot)
        {
            NetworkObject me = LocalPlayer.Object;
            NetworkPlayerMover mover = me != null ? me.GetComponent<NetworkPlayerMover>() : null;

            if (mover == null || mover.Object == null || !mover.Object.IsValid)
            {
                Debug.LogWarning("[춤] 내 캐릭터가 아직 없어 춤을 청하지 못했습니다.");
                return;
            }

            mover.RpcRequestDance(slot + 1);
        }
    }
}
