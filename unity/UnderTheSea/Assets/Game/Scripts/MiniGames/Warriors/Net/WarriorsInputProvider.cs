using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warriors.Net
{
    /// <summary>
    /// 이 컴퓨터의 입력을 모아 매 틱 서버로 보낸다. **NetworkRunner 와 같은 곳에 붙는다.</b>
    ///
    /// <b>왜 캐릭터가 아니라 러너에 붙는가.</b> 사람은 이 컴퓨터에 한 명이다.
    /// 캐릭터에 붙이면 화면에 있는 <b>남의 캐릭터 복사본</b>도 내 키보드를 읽는다.
    ///
    /// 키 배치는 <c>WarriorsKeyboardInput</c> 과 같게 맞춘다. (WARRIORS.md 2장)
    /// <code>
    ///   이동      WASD
    ///   가로베기  1      물고기
    ///   세로베기  2      게
    ///   찌르기    3      해파리
    /// </code>
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class WarriorsInputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            WarriorsInputData data = new WarriorsInputData();

            Keyboard keyboard = Keyboard.current;

            // ⚠ 이 창에 포커스가 있을 때만 보낸다. 한 PC 에서 클라이언트를 두 개 띄워
            //    확인할 때 서로 간섭하지 않는다.
            //    포커스가 없어도 **빈 입력을 보낸다.** 아예 안 보내면 서버가 직전 입력을
            //    그대로 재사용해 캐릭터가 혼자 계속 걸어간다.
            if (keyboard != null && Application.isFocused)
            {
                Vector2 move = Vector2.zero;

                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;

                data.Move = move;

                data.Buttons.Set((int)WarriorsButton.HorizontalSlash, keyboard.digit1Key.isPressed);
                data.Buttons.Set((int)WarriorsButton.VerticalSlash, keyboard.digit2Key.isPressed);
                data.Buttons.Set((int)WarriorsButton.Thrust, keyboard.digit3Key.isPressed);
                data.Buttons.Set((int)WarriorsButton.Dodge, keyboard.leftShiftKey.isPressed);
            }

            // 카메라 각도는 포커스와 상관없이 채운다. 서버가 이 각도로 이동을 돌린다.
            Camera view = Camera.main;
            if (view != null) data.LookYaw = view.transform.eulerAngles.y;

            input.Set(data);
        }

        #region 쓰지 않는 콜백

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }

        #endregion
    }
}
