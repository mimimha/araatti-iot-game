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
    ///   가로베기  J      물고기
    ///   세로베기  K      게
    ///   찌르기    L      해파리
    ///   회피      Shift
    ///   카메라    마우스 우클릭 드래그
    /// </code>
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class WarriorsInputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // ⚠ **IoT 장치를 여기서 매번 다시 찾는다.** 한 번만 찾으면 영영 못 찾는다.
            //
            //    이 부품은 WarriorsLauncher 가 WarriorsBoot 에서 붙인다. 그 순간에는
            //    Fusion 이 WarriorsNet 을 아직 열지 않아 WarriorsIoTInput 이 존재하지 않는다.
            //    OnEnable 에서 한 번 찾고 마는 구조였을 때는 null 을 받고 끝이라,
            //    **장치를 아무리 정확히 만들어도 네트워크 판에서는 아무 일도 일어나지 않았다.**
            //    오류도 나지 않아 장치 쪽을 의심하게 된다.
            //
            //    이미 걸려 있으면 곧바로 빠져나오므로 매 틱 부담은 없다.
            HookDevice();

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

                // 공격은 J K L 하나씩이다. (IOT_INPUT.md 1장 게임별 배치)
                // 예전 1 2 3 과 숫자 키패드는 뺐다 — 오른손 홈 위치에서 손을 떼지 않게 하고,
                // 키패드 없는 노트북에서 키마다 동작이 달라지던 문제도 함께 없앤다.
                data.Buttons.Set((int)WarriorsButton.HorizontalSlash, keyboard.jKey.isPressed);
                data.Buttons.Set((int)WarriorsButton.VerticalSlash, keyboard.kKey.isPressed);
                data.Buttons.Set((int)WarriorsButton.Thrust, keyboard.lKey.isPressed);
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
        /// 이 컴퓨터에 IoT 검 입력원이 있으면 붙는다. <b>붙을 때까지 매 틱 다시 찾는다.</b>
        ///
        /// <b>왜 한 번으로 끝내면 안 되는가.</b> 이 부품은 <c>WarriorsBoot</c> 에서 만들어지고
        /// <c>WarriorsIoTInput</c> 은 Fusion 이 나중에 여는 <c>WarriorsNet</c> 에 있다.
        /// 처음에는 반드시 못 찾는다. 그 한 번으로 포기하면 장치는 영영 연결되지 않는다.
        ///
        /// ⚠ <b>들고 있던 것이 사라졌으면 다시 찾는다.</b> 씬이 바뀌면 파괴된 객체가 남는데,
        ///    <c>deviceHooked</c> 만 보고 건너뛰면 그때도 영영 연결되지 않는다.
        ///    유니티의 <c>==</c> 는 파괴된 객체를 <c>null</c> 로 알려주므로 그것으로 가린다.
        ///
        /// ⚠ <b>가짜 장치를 만들지 않는다.</b> 실제 전송 계층이 없으면 <c>WarriorsIoTInput</c> 이
        ///    씬에 없을 뿐이고, 그때는 키보드만으로 정상 동작한다. 장치가 붙는 쪽에서
        ///    <c>WarriorsIoTInput.OnSwing</c> 을 부르면 그 순간부터 이 경로로 흘러든다.
        /// </summary>
        private void HookDevice()
        {
            if (deviceHooked && device != null) return;

            device = FindFirstObjectByType<WarriorsIoTInput>(FindObjectsInactive.Include);

            if (device == null)
            {
                deviceHooked = false;
                return;
            }

            // 두 번 붙는 것을 막는다. 같은 대상에 두 번 더하면 스윙이 두 번 들어간다.
            device.AttackRequested -= HandleDeviceSwing;
            device.AttackRequested += HandleDeviceSwing;
            deviceHooked = true;

            // QA 에서 이 줄이 안 보이면 장치 입력이 게임에 닿지 않는다는 뜻이다.
            Debug.Log($"[Warriors 입력] IoT 검 입력원에 연결했습니다. ({device.name})", device);
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

            bool h = keyboard.jKey.isPressed;
            bool v = keyboard.kKey.isPressed;
            bool t = keyboard.lKey.isPressed;

            bool anyHeld = h || v || t;

            // 누르고 있는 동안 계속 나가지 않게 눌린 순간만 잡는다.
            if (!anyHeld) { keyboardHeld = false; return; }
            if (keyboardHeld) return;

            keyboardHeld = true;

            PushSwing(h ? WarriorsAttackDirection.HorizontalSlash
                : v ? WarriorsAttackDirection.VerticalSlash
                : WarriorsAttackDirection.Thrust, 1f);
        }

        /// <summary>
        /// 이 PC 의 사람이 방금 휘두른 공격. HUD 가 해당 카드에 임팩트를 주는 데 쓴다.
        /// 서버 판정과 무관한 **입력 순간**의 신호라 클라이언트에서 바로 터진다.
        /// </summary>
        public static event System.Action<WarriorsAttackDirection> LocalSwing;

        private void PushSwing(WarriorsAttackDirection direction, float strength)
        {
            LocalSwing?.Invoke(direction);

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
