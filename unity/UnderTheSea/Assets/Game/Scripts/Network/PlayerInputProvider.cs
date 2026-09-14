using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로컬 입력을 모아 매 tick Fusion에 전달한다.
/// NetworkRunner와 같은 GameObject에 있으며 FusionLauncher가 AddCallbacks로 등록한다.
/// </summary>
public class PlayerInputProvider : MonoBehaviour, INetworkRunnerCallbacks
{
    public bool IsMovementLocked { get; private set; }

    public void SetMovementLocked(bool locked)
    {
        IsMovementLocked = locked;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData();

        // 이 창에 포커스가 있을 때만 입력을 보낸다.
        // Editor Host와 standalone Client를 한 PC에서 같이 띄워도 서로 간섭하지 않는다.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && Application.isFocused)
        {
            Vector2 direction = Vector2.zero;

            if (keyboard.wKey.isPressed) direction.y += 1f;
            if (keyboard.sKey.isPressed) direction.y -= 1f;
            if (keyboard.dKey.isPressed) direction.x += 1f;
            if (keyboard.aKey.isPressed) direction.x -= 1f;

            data.Direction = ApplyMovementLock(direction);
        }

        // 이동을 카메라 기준으로 돌리기 위해 로컬 카메라의 Y 각도를 함께 보낸다.
        // 서버에는 카메라가 없어서 이 값을 스스로 알 수 없다. (NetworkPlayerMover 가 쓴다)
        // 카메라가 없는 씬(ServerTestScene 등)에서는 0 이 가고, 그러면 월드 기준 이동이 된다.
        Camera view = Camera.main;
        if (view != null)
        {
            data.LookYaw = view.transform.eulerAngles.y;
        }

        // 포커스가 없으면 Direction이 zero인 채로 전달된다. 입력을 아예 보내지 않으면
        // Host가 이전 tick 입력을 재사용해 캐릭터가 계속 미끄러진다.
        input.Set(data);
    }

    private Vector2 ApplyMovementLock(Vector2 direction)
    {
        return IsMovementLocked ? Vector2.zero : direction;
    }

    #region Unused

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
