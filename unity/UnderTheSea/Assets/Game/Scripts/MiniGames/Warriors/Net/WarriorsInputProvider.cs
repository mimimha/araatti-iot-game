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

                // 윗줄 숫자키와 오른쪽 숫자 키패드 둘 다 받는다. WarriorsKeyboardInput(싱글)과 같다 —
                // 노트북 키패드로 치던 사람이 빌드에서만 공격이 안 되던 원인이다.
                data.Buttons.Set((int)WarriorsButton.HorizontalSlash,
                    keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed);
                data.Buttons.Set((int)WarriorsButton.VerticalSlash,
                    keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed);
                data.Buttons.Set((int)WarriorsButton.Thrust,
                    keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed);
                data.Buttons.Set((int)WarriorsButton.Dodge, keyboard.leftShiftKey.isPressed);
            }

            // 카메라 각도는 포커스와 상관없이 채운다. 서버가 이 각도로 이동을 돌린다.
            Camera view = Camera.main;
            if (view != null) data.LookYaw = view.transform.eulerAngles.y;

            // **스윙은 버튼이 아니라 사건으로 보낸다.**
            //
            // IoT 검은 "눌려 있음" 이 아니라 "휘둘렀다" 를 한 번 보낸다. 버튼 비트로만 받으면
            // 두 틱 사이에 일어난 스윙이 통째로 사라진다. 그래서 스윙이 생길 때마다 번호를
            // 올리고, 서버는 <b>번호가 바뀌었는가</b>로 새 스윙을 판단한다.
            // 키보드도 같은 규칙을 쓰므로 개발 입력이 그대로 살아 있다.
            CollectKeyboardSwing(keyboard);

            data.SwingSerial = swingSerial;
            data.SwingType = pendingType;
            data.SwingStrength = pendingStrength;
            data.SwingTick = pendingTick;

            input.Set(data);
        }

        // ------------------------------------------------------------
        // 스윙 모으기 — IoT 검과 키보드가 같은 자리로 들어온다
        // ------------------------------------------------------------

        private byte swingSerial;
        private byte pendingType;
        private byte pendingStrength = 255;
        private int pendingTick;
        private bool keyboardHeld;

        private WarriorsIoTInput device;
        private bool deviceHooked;

        private void OnEnable() => HookDevice();

        private void OnDisable()
        {
            if (device != null) device.AttackRequested -= HandleDeviceSwing;
            deviceHooked = false;
        }

        /// <summary>
        /// 이 컴퓨터에 IoT 검 입력원이 있으면 붙는다.
        ///
        /// ⚠ <b>가짜 장치를 만들지 않는다.</b> 실제 전송 계층이 없으면 <c>WarriorsIoTInput</c> 이
        ///    씬에 없을 뿐이고, 그때는 키보드만으로 정상 동작한다. 장치가 붙는 쪽에서
        ///    <c>WarriorsIoTInput.OnSwing</c> 을 부르면 그 순간부터 이 경로로 흘러든다.
        /// </summary>
        private void HookDevice()
        {
            if (deviceHooked) return;

            device = FindFirstObjectByType<WarriorsIoTInput>(FindObjectsInactive.Include);
            if (device == null) return;

            device.AttackRequested += HandleDeviceSwing;
            deviceHooked = true;
        }

        /// <summary>실제 검이 보낸 스윙. 세기가 그대로 실린다.</summary>
        private void HandleDeviceSwing(WarriorsAttackDirection direction, float strength)
        {
            PushSwing(direction, strength);
        }

        /// <summary>키보드 숫자키. 세기는 늘 1이고 타이밍 보정도 하지 않는다.</summary>
        private void CollectKeyboardSwing(Keyboard keyboard)
        {
            if (keyboard == null || !Application.isFocused) { keyboardHeld = false; return; }

            bool h = keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed;
            bool v = keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed;
            bool t = keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed;

            bool anyHeld = h || v || t;

            // 누르고 있는 동안 계속 나가지 않게 눌린 순간만 잡는다.
            if (!anyHeld) { keyboardHeld = false; return; }
            if (keyboardHeld) return;

            keyboardHeld = true;

            PushSwing(h ? WarriorsAttackDirection.HorizontalSlash
                : v ? WarriorsAttackDirection.VerticalSlash
                : WarriorsAttackDirection.Thrust, 1f);
        }

        private void PushSwing(WarriorsAttackDirection direction, float strength)
        {
            swingSerial++;
            if (swingSerial == 0) swingSerial = 1;   // 0 은 "아직 없음" 이라 건너뛴다

            pendingType = (byte)direction;
            pendingStrength = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(strength) * 255f), 1, 255);

            NetworkRunner runner = GetComponent<NetworkRunner>();
            pendingTick = runner != null && runner.IsRunning ? runner.Tick : 0;
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
