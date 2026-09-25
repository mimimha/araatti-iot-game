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
    [Header("IoT")]
    [Tooltip("IPlayerController 를 구현한 컴포넌트. 비워두면 씬에서 찾는다. 없으면 키보드만 쓴다.")]
    [SerializeField] private MonoBehaviour playerControllerSource;

    /// <summary>완드. 없으면 null 이고, 그때는 예전처럼 키보드만 읽는다.</summary>
    private IPlayerController _wand;

    /// <summary>다음에 완드를 다시 찾아볼 시각. 못 찾은 동안 매 tick 씬을 뒤지지 않으려고 둔다.</summary>
    private float _nextWandSearchTime;

    public bool IsMovementLocked { get; private set; }

    public void SetMovementLocked(bool locked)
    {
        if (IsMovementLocked == locked)
        {
            return;
        }

        IsMovementLocked = locked;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData();
        Vector2 rawDirection = Vector2.zero;

        // 이 창에 포커스가 있을 때만 입력을 보낸다.
        // Editor Host와 standalone Client를 한 PC에서 같이 띄워도 서로 간섭하지 않는다.
        //
        // ⚠ **글자를 치는 동안에는 아무것도 보내지 않는다.** 채팅창에 "안녕" 을 치면
        //    그 사이 W · A · S · D · Space 가 그대로 조작으로 들어가 캐릭터가 걸어가고 뛴다.
        //    채팅이 있는지는 여기서 몰라도 된다. ChatFocus 한 곳만 본다.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && Application.isFocused && !ChatFocus.Typing)
        {
            if (keyboard.wKey.isPressed) rawDirection.y += 1f;
            if (keyboard.sKey.isPressed) rawDirection.y -= 1f;
            if (keyboard.dKey.isPressed) rawDirection.x += 1f;
            if (keyboard.aKey.isPressed) rawDirection.x -= 1f;

            // 눌린 순간이 아니라 **누르고 있는 상태**를 보낸다.
            // 순간을 보내면 그 한 틱이 유실될 때 점프가 통째로 사라진다.
            // 서버가 GetPressed(직전) 로 순간을 스스로 만들어 낸다. (NetworkPlayerMover)
            data.Buttons.Set((int)LobbyButton.Jump,
                !IsMovementLocked && keyboard.spaceKey.isPressed);

            // 달리기는 누르고 있는 내내 유효하다. 서버가 그대로 속도에 쓴다.
            data.Buttons.Set((int)LobbyButton.Sprint,
                !IsMovementLocked &&
                (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));
        }

        // 완드도 같은 자리로 들어온다. **키보드를 대체하지 않고 더한다.**
        //
        // 장치가 없으면 _wand 가 null 이라 아래가 통째로 비고, 예전과 완전히 같아진다.
        // 둘 다 있으면 둘 다 먹는다 — 완드를 들고도 키보드로 확인할 수 있어야 한다.
        //
        // ⚠ **네트워크는 손대지 않는다.** 완드는 여기까지만 오고, 서버로 실어 보내는 것은
        //   예전 그대로 이 함수가 한다. 장치용 네트워크 경로를 따로 내면 같은 tick 에
        //   입력이 두 번 들어간다. (IOT_INPUT.md 5장 "장치는 로컬에서 자기 값만 채운다")
        //
        // ⚠ 채팅 잠금은 완드에도 그대로 건다. 글자를 치는 동안 스틱으로 걸어가면
        //   키보드만 막은 의미가 없다.
        IPlayerController wand = ResolveWand();
        if (wand != null && Application.isFocused && !ChatFocus.Typing)
        {
            rawDirection += wand.Move;

            // 점프는 오른손 면버튼 1 (키보드 Space 자리), 달리기는 왼손 면버튼 2 (Shift 자리).
            // IOT_INPUT.md 1장 표의 "Space = 그 게임의 주된 행동" 을 로비에 적용한 것이다.
            //
            // ⚠ 기기가 1대면 Right 가 Left 와 **같은 객체**라 점프와 상호작용이 겹친다.
            //   그래서 점프는 2대일 때만 받는다. 광산이 달리기를 1대에서 빼는 것과 같은 이유다.
            if (wand.HasTwoDevices && !IsMovementLocked && wand.Right.Button1)
            {
                data.Buttons.Set((int)LobbyButton.Jump, true);
            }

            if (!IsMovementLocked && wand.Left.Button2)
            {
                data.Buttons.Set((int)LobbyButton.Sprint, true);
            }
        }

        // 키보드와 완드를 더했으므로 대각선이 1 을 넘을 수 있다. 서버가 이 값을 그대로
        // 속도에 쓰므로 자르지 않으면 두 입력을 겹쳤을 때만 빨라진다.
        if (rawDirection.sqrMagnitude > 1f)
        {
            rawDirection = rawDirection.normalized;
        }

        data.Direction = ApplyMovementLock(rawDirection);

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

    /// <summary>
    /// 완드를 찾아 둔다. 없으면 null 이고 로비는 예전처럼 키보드로만 돈다.
    ///
    /// ⚠ <b>Awake 에서 찾을 수 없다.</b> 이 부품은 NetworkRunner 와 함께 있고
    ///   FusionLauncher 가 실행 중에 만들기도 해서, 로비 씬의 완드보다 먼저 깨어날 수 있다.
    ///   그래서 찾을 때까지 이따금 다시 본다. 매 tick 씬을 뒤지면 비싸므로 1초에 한 번만 본다.
    /// </summary>
    private IPlayerController ResolveWand()
    {
        if (_wand != null)
        {
            return _wand;
        }

        if (playerControllerSource is IPlayerController assigned)
        {
            _wand = assigned;
            return _wand;
        }

        if (Time.unscaledTime < _nextWandSearchTime)
        {
            return null;
        }

        _nextWandSearchTime = Time.unscaledTime + 1f;

        // ⚠ **KeyboardPlayerController 는 일부러 찾지 않는다.** 그것은 키보드를 장치처럼
        //   흉내내는 부품이라, 여기 끼면 같은 W 가 위쪽 키보드 블록과 여기로 두 번 들어온다.
        //   로비는 이미 자기 키보드 경로를 갖고 있어서 흉내가 필요 없다.
        //   완드 경로를 장치 없이 확인해야 한다면 위 칸에 직접 지정한다.
        _wand = FindAnyObjectByType<IotPlayerController>();

        return _wand;
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
