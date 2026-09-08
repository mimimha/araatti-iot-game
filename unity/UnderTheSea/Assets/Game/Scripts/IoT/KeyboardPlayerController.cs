using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IoT 기기 없이 키보드로 게임을 만들고 테스트하기 위한 컨트롤러.
///
/// ⚠ 임시 구현입니다. IoT 담당자의 진짜 구현이 나오면 이 컴포넌트만 갈아끼우면 됩니다.
///    게임 로직은 IPlayerController 만 쓰기 때문에 고칠 필요가 없습니다.
///
/// 키 배치 — 실제 기기의 어느 부품을 대신하는지 함께 적는다.
///
///   [왼손 기기]
///     방향키        조이스틱     이동
///     왼쪽 Shift    압력센서     왼손 쥐기
///     C             버튼 1       도움 요청
///     V             버튼 2       (여유)
///
///   [오른손 기기]
///     Q / E         조이스틱     카메라 · 대포 조준
///     오른쪽 Shift  압력센서     오른손 쥐기
///     Space         버튼 1       상호작용 (붙기 · 집기 · 놓기)
///     X             버튼 2       발사
///     F             IMU 내리치기 망치질
///
///   [양손 공통]
///     A / D         IMU 기울기 · 회전   조타, 돛
///
/// 키보드로는 두 손을 따로 기울일 수 없으므로 A / D 를 양손이 함께 씁니다.
/// 실제 기기에서는 왼손과 오른손의 IMU 가 따로 들어옵니다.
///
/// 이동은 방향키, 작업은 문자 자판으로 나눠 두었습니다. 둘이 겹치면
/// 작업하려고 누른 키에 캐릭터가 걸어나가 자리에서 떨어집니다.
/// 실제 기기에서는 스틱과 IMU 가 물리적으로 나뉘어 있어 이 문제가 없습니다.
/// </summary>
public class KeyboardPlayerController : MonoBehaviour, IPlayerController
{
    [Header("축 입력이 0 에서 1 까지 가는 속도")]
    [Tooltip("실제 기기는 손목을 기울인 만큼 값이 들어오지만, 키보드는 켜짐/꺼짐뿐이라 " +
             "천천히 차오르게 만들어야 조작감이 비슷해진다.")]
    [SerializeField, Range(0.5f, 20f)] private float axisSpeed = 4f;

    [Header("기기를 2대 든 것으로 볼지")]
    [Tooltip("끄면 1대만 든 상태를 흉내낸다. 양손 조작이 없어도 게임이 되는지 확인할 때 쓴다.")]
    [SerializeField] private bool twoDevices = true;

    [Header("기기 출력 로그")]
    [Tooltip("켜면 진동 호출을 콘솔에 찍는다. 실제 기기가 붙기 전에 확인용으로 쓴다.")]
    [SerializeField] private bool logDeviceOutput = false;

    private KeyboardHand _left;
    private KeyboardHand _right;

    public IHandDevice Left => _left;

    // 1대만 들면 오른손도 같은 기기를 본다. 게임 코드는 개수를 몰라도 된다.
    public IHandDevice Right => twoDevices ? _right : _left;

    public bool HasTwoDevices => twoDevices;

    public Vector2 Move => Left.Stick;

    public Vector2 Look => Right.Stick;

    private void Awake()
    {
        _left = new KeyboardHand(
            axisSpeed,
            logDeviceOutput,
            "왼손",
            stickUp: Key.UpArrow, stickDown: Key.DownArrow,
            stickLeft: Key.LeftArrow, stickRight: Key.RightArrow,
            grip: Key.LeftShift,
            button1: Key.C, button2: Key.V,
            swing: Key.None);

        _right = new KeyboardHand(
            axisSpeed,
            logDeviceOutput,
            "오른손",
            stickUp: Key.None, stickDown: Key.None,
            stickLeft: Key.Q, stickRight: Key.E,
            grip: Key.RightShift,
            button1: Key.Space, button2: Key.X,
            swing: Key.F);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        // 키보드로는 두 손을 따로 기울일 수 없다. A / D 를 양손이 함께 쓴다.
        float target = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) { target -= 1f; }
            if (keyboard.dKey.isPressed) { target += 1f; }
        }

        _left.Tick(keyboard, target, Time.deltaTime);
        _right.Tick(keyboard, target, Time.deltaTime);
    }

    public void VibrateBoth(float strength, float seconds)
    {
        Left.Vibrate(strength, seconds);

        if (twoDevices)
        {
            Right.Vibrate(strength, seconds);
        }
    }

    /// <summary>키보드로 흉내내는 기기 1대.</summary>
    private class KeyboardHand : IHandDevice
    {
        private readonly float _axisSpeed;
        private readonly bool _log;
        private readonly string _label;

        private readonly Key _stickUp;
        private readonly Key _stickDown;
        private readonly Key _stickLeft;
        private readonly Key _stickRight;
        private readonly Key _grip;
        private readonly Key _button1;
        private readonly Key _button2;
        private readonly Key _swing;

        private float _axis;
        private Vector2 _stick;
        private bool _gripHeld;
        private bool _button1Held;
        private bool _button2Held;
        private bool _button1Pressed;
        private bool _button2Pressed;
        private bool _swung;

        public KeyboardHand(
            float axisSpeed, bool log, string label,
            Key stickUp, Key stickDown, Key stickLeft, Key stickRight,
            Key grip, Key button1, Key button2, Key swing)
        {
            _axisSpeed = axisSpeed;
            _log = log;
            _label = label;
            _stickUp = stickUp;
            _stickDown = stickDown;
            _stickLeft = stickLeft;
            _stickRight = stickRight;
            _grip = grip;
            _button1 = button1;
            _button2 = button2;
            _swing = swing;
        }

        public Vector2 Stick => _stick;

        // 키보드에서는 기울기와 회전을 구분할 수 없다.
        // 한 사람이 한 번에 한 작업만 하므로 테스트에는 문제가 없다.
        public float Tilt => _axis;
        public float Rotation => _axis;

        public bool Grip => _gripHeld;
        public bool Button1 => _button1Held;
        public bool Button2 => _button2Held;

        public void Tick(Keyboard keyboard, float axisTarget, float deltaTime)
        {
            // 키보드가 없는 환경(빌드 서버 등)에서도 예외 없이 동작해야 한다.
            if (keyboard == null)
            {
                _axis = Mathf.MoveTowards(_axis, 0f, _axisSpeed * deltaTime);
                _stick = Vector2.zero;
                _gripHeld = false;
                _button1Held = false;
                _button2Held = false;
                return;
            }

            _axis = Mathf.MoveTowards(_axis, axisTarget, _axisSpeed * deltaTime);

            float x = 0f;
            float y = 0f;
            if (IsPressed(keyboard, _stickLeft)) { x -= 1f; }
            if (IsPressed(keyboard, _stickRight)) { x += 1f; }
            if (IsPressed(keyboard, _stickDown)) { y -= 1f; }
            if (IsPressed(keyboard, _stickUp)) { y += 1f; }

            Vector2 stick = new Vector2(x, y);

            // 대각선이 더 빨라지지 않도록 길이를 1 로 자른다.
            _stick = stick.sqrMagnitude > 1f ? stick.normalized : stick;

            _gripHeld = IsPressed(keyboard, _grip);
            _button1Held = IsPressed(keyboard, _button1);
            _button2Held = IsPressed(keyboard, _button2);

            if (WasPressedThisFrame(keyboard, _button1)) { _button1Pressed = true; }
            if (WasPressedThisFrame(keyboard, _button2)) { _button2Pressed = true; }
            if (WasPressedThisFrame(keyboard, _swing)) { _swung = true; }
        }

        public bool ConsumeButton1Press()
        {
            bool pressed = _button1Pressed;
            _button1Pressed = false;
            return pressed;
        }

        public bool ConsumeButton2Press()
        {
            bool pressed = _button2Pressed;
            _button2Pressed = false;
            return pressed;
        }

        public bool ConsumeSwing()
        {
            bool swung = _swung;
            _swung = false;
            return swung;
        }

        // 키보드에는 진동 모터가 없다. 부르는 쪽이 신경 쓰지 않도록 조용히 받아준다.
        public void Vibrate(float strength, float seconds)
        {
            if (_log)
            {
                Debug.Log($"[KeyboardPlayerController] {_label} 진동 — 세기 {strength:0.00}, {seconds:0.00}초");
            }
        }

        private static bool IsPressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].isPressed;
        }

        private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }
    }
}
