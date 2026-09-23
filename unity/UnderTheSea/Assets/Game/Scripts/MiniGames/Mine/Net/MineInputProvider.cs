using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mine.Net
{
    /// <summary>
    /// 이 컴퓨터의 입력을 모아 매 틱 서버로 보낸다. **NetworkRunner 와 같은 곳에 붙는다.**
    ///
    /// <b>왜 캐릭터가 아니라 러너에 붙는가.</b> 사람은 이 컴퓨터에 한 명이다.
    /// 캐릭터에 붙이면 화면에 있는 <b>남의 캐릭터 복사본</b>도 내 키보드를 읽는다.
    ///
    /// 키 배치는 원본과 같게 맞춘다. (MINE.md 8장)
    /// <code>
    ///   이동    WASD        (MineMoveInput 과 같은 축 이름)
    ///   달리기  LeftShift
    ///   점프    Space
    ///   휘두름  F           (2단계에서 격자에 닿는다)
    ///   되메움  C           (2단계)
    ///   힌트    V           (2단계)
    /// </code>
    ///
    /// ⚠ 관전 중에도 **빈 입력을 보낸다.** 아예 안 보내면 서버가 직전 입력을 그대로
    ///   재사용해 캐릭터가 혼자 계속 걸어간다. ShipCoop 에서 실측했다.
    ///   관전자의 입력을 버리는 일은 서버가 <c>CurrentSlot</c> 으로 한다.
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class MineInputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        private MineCamera _camera;

        private bool _reported;

        /// <summary>직전에 휘두름 키가 눌려 있었는가. 여기서 "새로 눌린 순간" 을 만든다.</summary>
        private bool _swingHeld;

        /// <summary>
        /// 이 PC 의 사람이 방금 휘두름 키를 눌렀다. 서버 판정과 무관한 **입력 순간**의 신호다.
        /// 헛스윙 소리가 쓴다(<see cref="MineAudio"/>) — 서버는 턴이 아닌 사람의 휘두름을 조용히 버리므로
        /// 복제되는 값으로는 이 순간을 알 수 없다.
        /// </summary>
        public static event Action LocalSwing;

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // 입력이 안 먹을 때 원인을 바로 가리도록 한 번만 남긴다.
            // 키보드 장치가 없거나 창이 포커스를 못 받으면 여기서 드러난다.
            if (!_reported)
            {
                _reported = true;
                Debug.Log($"[MineInput] 입력 준비 — 키보드 {(Keyboard.current != null ? "있음" : "없음")}, " +
                          $"마우스 {(Mouse.current != null ? "있음" : "없음")}, 포커스 {Application.isFocused}");
            }

            MineInputData data = new MineInputData();

            // ⚠ 이 창에 포커스가 있을 때만 읽는다. 한 PC 에서 클라이언트를 여럿 띄워
            //    확인할 때 서로 간섭하지 않는다.
            //
            // ⚠ **키는 새 Input System 으로 읽는다.** 레거시 `Input.GetAxis` 를 쓰면
            //    같은 프로젝트 안에서도 빌드에 따라 조용히 0 이 나오는 일이 있다.
            //    Lobby · ShipCoop · Warriors 가 모두 이 경로로 수동 QA 를 통과했으므로
            //    광산도 같은 길을 쓴다. `Keyboard.current` 는 창이 없으면 null 이다.
            Keyboard keys = Keyboard.current;

            bool swing = keys != null && Application.isFocused && keys.spaceKey.isPressed;
            if (swing && !_swingHeld) LocalSwing?.Invoke();
            _swingHeld = swing;

            if (keys != null && Application.isFocused)
            {
                Vector2 move = Vector2.zero;

                if (keys.wKey.isPressed) move.y += 1f;
                if (keys.sKey.isPressed) move.y -= 1f;
                if (keys.dKey.isPressed) move.x += 1f;
                if (keys.aKey.isPressed) move.x -= 1f;

                data.Move = move;

                data.Buttons.Set((int)MineButton.Run, keys.leftShiftKey.isPressed);
                data.Buttons.Set((int)MineButton.Swing, keys.spaceKey.isPressed);
                data.Buttons.Set((int)MineButton.Restore, keys.cKey.isPressed);
                data.Buttons.Set((int)MineButton.Hint, keys.jKey.isPressed);

                // ⚠ 점프는 키를 빼 둔다. 채굴이 Space 를 쓰면서 한 키에
                //   두 동작이 걸리기 때문이다. 조작 안내에도 점프는 없다.
                //   다시 쓰려면 안 겹치는 키로 아래를 살린다.
                //   data.Buttons.Set((int)MineButton.Jump, keys.<다른키>.isPressed);
            }

            // 각도는 포커스와 상관없이 채운다. 서버가 이 값으로 이동을 풀고,
            // 관전자들이 같은 시점을 본다.
            MineCamera view = ResolveCamera();

            if (view != null)
            {
                data.LookYaw = view.Yaw;
                data.LookPitch = view.Pitch;
            }

            input.Set(data);
        }

        /// <summary>
        /// 카메라는 **게임 씬**에 있고 이 부품은 시작 씬의 러너에 있다.
        /// 씬을 넘는 참조는 저장되지 않으므로 실행 중에 찾는다. 한 번 찾으면 들고 있는다.
        /// </summary>
        private MineCamera ResolveCamera()
        {
            if (_camera == null) _camera = FindAnyObjectByType<MineCamera>();
            return _camera;
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
