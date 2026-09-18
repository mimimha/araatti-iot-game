using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 이 컴퓨터의 기기 입력을 모아 매 틱 서버로 보낸다. **NetworkRunner 와 같은 곳에 붙는다.**
    ///
    /// <b>Lobby 의 <c>PlayerInputProvider</c> 를 쓰지 않는 이유</b>
    ///   그쪽은 WASD 와 카메라 각도만 보낸다. 배 협동 게임은 손 두 개의
    ///   스틱 · IMU · 압력 · 버튼이 전부 필요하다. (SHIPCOOP.md 7장)
    ///
    /// <b>기기는 여기 하나뿐이다.</b> 컨트롤러는 <b>사람마다</b> 하나지 캐릭터마다 하나가 아니다.
    /// 이 컴퓨터에는 사람이 한 명이므로 기기 묶음도 하나다. 그래서 캐릭터가 아니라
    /// 러너에 붙인다. 캐릭터에 붙이면 화면에 있는 <b>남의 캐릭터 복사본</b>도 내 키보드를 읽는다.
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class ShipCoopInputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        /// <summary>
        /// 이 컴퓨터의 기기 묶음.
        ///
        /// 서버가 진동을 울리라고 보내오면 (<c>ShipCoopNetworkedController</c>) 여기로 온다.
        /// 실제 기기가 붙으면 이 자리만 바뀐다. 게임 코드는 <c>IPlayerController</c> 만 본다.
        /// </summary>
        public static IPlayerController LocalDevices { get; private set; }

        private IPlayerController devices;

        private void Awake()
        {
            devices = GetComponent<IPlayerController>();

            if (devices == null)
            {
                devices = gameObject.AddComponent<KeyboardPlayerController>();
                Debug.Log("[ShipCoopInput] 기기가 없어 키보드 컨트롤러를 붙였습니다.");
            }

            LocalDevices = devices;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(LocalDevices, devices))
            {
                LocalDevices = null;
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            ShipCoopInputData data = new ShipCoopInputData();

            // ⚠ 이 창에 포커스가 있을 때만 보낸다.
            //    한 PC 에서 클라이언트를 두 개 띄워 확인할 때 서로 간섭하지 않는다.
            //    포커스가 없어도 **빈 입력을 보낸다.** 아예 안 보내면 서버가 직전 입력을
            //    그대로 재사용해 캐릭터가 혼자 계속 걸어간다.
            if (devices != null && Application.isFocused)
            {
                Fill(ref data);
            }

            // 카메라 각도는 포커스와 상관없이 채운다. 서버가 이 각도로 이동을 돌린다.
            Camera view = Camera.main;
            if (view != null)
            {
                data.LookYaw = view.transform.eulerAngles.y;
            }

            input.Set(data);
        }

        private void Fill(ref ShipCoopInputData data)
        {
            IHandDevice left = devices.Left;
            IHandDevice right = devices.Right;

            data.Move = devices.Move;
            data.Look = devices.Look;

            data.LeftTilt = left.Tilt;
            data.RightTilt = right.Tilt;
            data.LeftRotation = left.Rotation;
            data.RightRotation = right.Rotation;

            data.Buttons.Set((int)ShipCoopButton.LeftButton1, left.Button1);
            data.Buttons.Set((int)ShipCoopButton.LeftButton2, left.Button2);
            data.Buttons.Set((int)ShipCoopButton.RightButton1, right.Button1);
            data.Buttons.Set((int)ShipCoopButton.RightButton2, right.Button2);
            data.Buttons.Set((int)ShipCoopButton.TwoDevices, devices.HasTwoDevices);

            // 내리치기만 상태가 아니라 사건이다. 감지한 틱에 한 번 실어 보낸다.
            //
            // ⚠ 여기서 가져가면서 지운다. 이 컴퓨터에서 같은 동작을 다른 곳이 또 가져가면
            //    한쪽이 놓친다. 작업 판정은 전부 서버로 넘어갔으므로 가져가는 곳은 여기뿐이다.
            //
            // ⚠ ConsumeSwing 이 아니라 **ConsumeSwingMotion** 이다. 앞엣것은 면버튼 2 도
            //    휘두름으로 쳐서, 대포에서 K 로 쏠 때마다 서버의 휘두름 깃발이 함께 켜진다.
            //    대포 쪽은 그것을 안 가져가므로 그대로 쌓이고, 나중에 파손 지점에 붙는 순간
            //    공짜 망치질로 터진다. 면버튼은 위의 RightButton2 로 이미 따로 간다.
            data.Buttons.Set((int)ShipCoopButton.RightSwing, ShipCoopInput.ConsumeSwingMotion(devices));
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
