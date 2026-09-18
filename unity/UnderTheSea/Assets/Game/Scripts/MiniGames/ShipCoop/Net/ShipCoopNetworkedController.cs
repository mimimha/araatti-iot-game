using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 네트워크로 온 입력을 <see cref="IPlayerController"/> 인 척 내놓는다.
    ///
    /// <b>이 파일이 있는 이유.</b> 민화님의 작업 스크립트(조타 · 대포 · 돛 · 수리 · 양수)는
    /// 전부 <c>TaskWorker.Input</c> 을 통해 <c>IPlayerController</c> 만 본다.
    /// 그 인터페이스를 서버에서 그대로 만들어 주면 <b>작업 스크립트를 한 줄도 안 고치고</b>
    /// 서버 권위로 옮길 수 있다. 11장이 설계해 둔 이음매다.
    ///
    /// <code>
    ///   서버        받은 입력 → 이 컴포넌트 → TaskWorker → 작업들 (판정은 여기서만)
    ///   내 캐릭터   Fusion 이 내가 방금 보낸 입력을 그대로 돌려준다 (왕복을 기다리지 않는다)
    ///   남의 캐릭터 입력이 없다. 화면만 그린다
    /// </code>
    ///
    /// <b>카메라가 느려지지 않는 이유.</b>
    /// 카메라 좌우 회전(<c>ShipCoopCamera</c>)은 <c>LocalWorker.Input.Look.x</c> 를 본다.
    /// <c>GetInput</c> 은 <b>입력 권한을 가진 쪽에도</b> 자기 입력을 준다. 서버 응답을
    /// 기다리는 값이 아니라 이번 틱에 내가 넣은 값이라, 내 화면은 바로 돈다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopNetworkedController : NetworkBehaviour, IPlayerController
    {
        private readonly Hand left = new Hand();
        private readonly Hand right = new Hand();

        /// <summary>직전 틱에 눌려 있던 것. 여기서 "새로 눌린 순간" 을 만들어 낸다.</summary>
        [Networked]
        private NetworkButtons PreviousButtons { get; set; }

        private bool twoDevices = true;

        public IHandDevice Left => left;

        public IHandDevice Right => twoDevices ? right : left;

        public bool HasTwoDevices => twoDevices;

        public Vector2 Move { get; private set; }

        public Vector2 Look { get; private set; }

        public override void FixedUpdateNetwork()
        {
            // 서버와 내 캐릭터만 입력을 받는다. 남의 캐릭터 복사본은 여기서 걸러진다.
            if (!GetInput(out ShipCoopInputData input))
            {
                return;
            }

            Move = input.Move;
            Look = input.Look;
            twoDevices = input.Buttons.IsSet((int)ShipCoopButton.TwoDevices);

            left.Apply(input.LeftTilt, input.LeftRotation,
                button1: input.Buttons.IsSet((int)ShipCoopButton.LeftButton1),
                button2: input.Buttons.IsSet((int)ShipCoopButton.LeftButton2),
                stick: input.Move);

            right.Apply(input.RightTilt, input.RightRotation,
                button1: input.Buttons.IsSet((int)ShipCoopButton.RightButton1),
                button2: input.Buttons.IsSet((int)ShipCoopButton.RightButton2),
                stick: input.Look);

            // ⚠ 되돌려 다시 계산하는 틱에서는 "눌린 순간" 을 만들지 않는다.
            //    Fusion 은 같은 틱을 여러 번 굴린다. 그대로 두면 한 번 누른 것이
            //    여러 번 눌린 것으로 처리되어 포탄이 두 발 나간다.
            if (!Runner.IsForward)
            {
                return;
            }

            NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
            PreviousButtons = input.Buttons;

            left.Push(
                button1: pressed.IsSet((int)ShipCoopButton.LeftButton1),
                button2: pressed.IsSet((int)ShipCoopButton.LeftButton2),
                swing: false);

            right.Push(
                button1: pressed.IsSet((int)ShipCoopButton.RightButton1),
                button2: pressed.IsSet((int)ShipCoopButton.RightButton2),
                // 내리치기는 클라이언트가 이미 순간으로 보낸다. 여기서 또 가장자리를 잡지 않는다.
                swing: input.Buttons.IsSet((int)ShipCoopButton.RightSwing));
        }

        public void VibrateBoth(float strength, float seconds)
        {
            left.Vibrate(strength, seconds);

            if (twoDevices)
            {
                right.Vibrate(strength, seconds);
            }
        }

        /// <summary>
        /// 진동은 <b>그 사람 손에 있는 기기</b>가 울려야 한다. 서버에는 기기가 없다.
        ///
        /// 그래서 서버가 부르면 주인 클라이언트로 보내고, 받은 쪽이 자기 기기를 울린다.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void Rpc_Vibrate(int hand, float strength, float seconds)
        {
            IPlayerController devices = ShipCoopInputProvider.LocalDevices;

            if (devices == null)
            {
                return;
            }

            if (hand == 0)
            {
                devices.Left.Vibrate(strength, seconds);
            }
            else
            {
                devices.Right.Vibrate(strength, seconds);
            }
        }

        /// <summary>
        /// 손 하나. 네트워크로 받은 값을 담아 두고 <see cref="IHandDevice"/> 로 내놓는다.
        ///
        /// <c>Consume</c> 계열은 부른 쪽이 가져가면서 지운다. 원래 규격 그대로다.
        /// </summary>
        private sealed class Hand : IHandDevice
        {
            private ShipCoopNetworkedController owner;
            private bool isLeft;

            private bool press1;
            private bool press2;
            private bool swing;

            public Vector2 Stick { get; private set; }

            public float Tilt { get; private set; }

            public float Rotation { get; private set; }

            public bool Button1 { get; private set; }

            public bool Button2 { get; private set; }

            public void Bind(ShipCoopNetworkedController controller, bool left)
            {
                owner = controller;
                isLeft = left;
            }

            public void Apply(float tilt, float rotation, bool button1, bool button2, Vector2 stick)
            {
                Tilt = tilt;
                Rotation = rotation;
                Button1 = button1;
                Button2 = button2;
                Stick = stick;
            }

            /// <summary>이번 틱에 새로 일어난 것들. 누가 가져갈 때까지 남아 있는다.</summary>
            public void Push(bool button1, bool button2, bool swing)
            {
                press1 |= button1;
                press2 |= button2;
                this.swing |= swing;
            }

            public bool ConsumeButton1Press()
            {
                bool had = press1;
                press1 = false;
                return had;
            }

            public bool ConsumeButton2Press()
            {
                bool had = press2;
                press2 = false;
                return had;
            }

            public bool TryConsumeMotion(out HandMotion motion)
            {
                bool had = swing;
                swing = false;
                motion = had
                    ? new HandMotion(HandMotionType.VerticalSwing, 1f)
                    : default;
                return had;
            }

            public void Vibrate(float strength, float seconds)
            {
                if (owner != null)
                {
                    owner.Rpc_Vibrate(isLeft ? 0 : 1, strength, seconds);
                }
            }
        }

        private void Awake()
        {
            left.Bind(this, true);
            right.Bind(this, false);
        }
    }
}
