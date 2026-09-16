using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IoT 기기 없이 키보드와 마우스로 게임을 만들고 테스트하기 위한 컨트롤러.
///
/// ⚠ 임시 구현입니다. IoT 담당자의 진짜 구현이 나오면 이 컴포넌트만 갈아끼우면 됩니다.
///    게임 로직은 IPlayerController 만 쓰기 때문에 고칠 필요가 없습니다.
///
/// **부품 하나에 키 하나**로 맞춰 두었습니다. 키보드로 확인한 것이 기기에서 그대로 됩니다.
/// 배치를 바꾸려면 IOT_INPUT.md 를 먼저 고치세요. 네 미니게임이 함께 지키는 표입니다.
///
///   [왼손 기기]
///     W A S D       조이스틱     이동
///     C             면버튼 1     도움 요청
///     Shift         면버튼 2     달리기 (토글)
///
///   [오른손 기기]
///     마우스 우클릭 드래그  조이스틱   카메라 좌우
///     Space         면버튼 1     상호작용 (붙기 · 집기 · 놓기 · 장전)
///     K             면버튼 2     발사 · 망치질
///
///   [양손 공통]
///     J / L         IMU 기울기 · 회전   조타, 돛
///
///   [기기 1대로 볼 때 — twoDevices 를 끈 경우]
///     W A S D       조이스틱     이동
///     Space         면버튼 1     상호작용
///     K             면버튼 2     발사 · 망치질
///
///   1대에는 면버튼이 2개뿐이라 도움 요청(C)과 달리기(Shift)가 없습니다.
///   무엇을 살릴지는 게임이 이미 정해 두었습니다. (ShipCoopInput.ConsumeHelpCall · Sprint)
///   스틱도 1개뿐이고 걷기가 쓰므로 카메라가 고정됩니다.
///
/// 키보드로는 두 손을 따로 기울일 수 없으므로 J / L 을 양손이 함께 씁니다.
/// 실제 기기에서는 왼손과 오른손의 IMU 가 따로 들어옵니다.
///
/// 이동(W A S D)과 작업 키(J K L)를 손 단위로 갈라 두었습니다. 둘이 겹치면
/// 작업하려고 누른 키에 캐릭터가 걸어나가 자리에서 떨어집니다.
/// 실제 기기에서는 스틱과 IMU 가 물리적으로 나뉘어 있어 이 문제가 없습니다.
/// </summary>
public class KeyboardPlayerController : MonoBehaviour, IPlayerController
{
    [Header("축 입력이 0 에서 1 까지 가는 속도")]
    [Tooltip("실제 기기는 손목을 기울인 만큼 값이 들어오지만, 키보드는 켜짐/꺼짐뿐이라 " +
             "천천히 차오르게 만들어야 조작감이 비슷해진다.")]
    [SerializeField, Range(0.5f, 20f)] private float axisSpeed = 4f;

    [Header("마우스 드래그 감도")]
    [Tooltip("픽셀당 스틱 값. 드래그한 만큼 카메라가 돈다.")]
    [SerializeField, Range(0.01f, 1f)] private float lookSensitivity = 0.15f;

    [Header("기기를 2대 든 것으로 볼지")]
    [Tooltip("끄면 1대만 든 상태를 흉내낸다. 양손 조작이 없어도 게임이 되는지 확인할 때 쓴다.")]
    [SerializeField] private bool twoDevices = true;

    [Header("기기 출력 로그")]
    [Tooltip("켜면 진동 호출을 콘솔에 찍는다. 실제 기기가 붙기 전에 확인용으로 쓴다.")]
    [SerializeField] private bool logDeviceOutput = false;

    private KeyboardHand _left;
    private KeyboardHand _right;

    /// <summary>
    /// 기기를 1대만 들었을 때 쓰는 **그 한 대**.
    ///
    /// 왼손 것도 오른손 것도 아닙니다. 1대면 면버튼이 2개뿐이라 무엇을 살릴지 골라야 하고,
    /// 게임은 이미 **상호작용과 발사**를 골라 두었습니다.
    /// (ShipCoopInput 의 ConsumeHelpCall 과 Sprint 가 1대면 스스로 물러납니다)
    ///
    /// 그래서 이 한 대는 스틱은 왼손 것(걷기), 면버튼은 오른손 것(Space · K)을 답니다.
    ///
    /// ⚠ 왼손 기기를 그대로 돌려쓰면 안 됩니다. 그러면 상호작용이 Space 가 아니라
    ///    도움 요청 키(C)에 붙고, 발사는 왼손 면버튼 2 가 달리기 토글이라 아예 안 됩니다.
    /// </summary>
    private KeyboardHand _single;

    /// <summary>
    /// 달리기는 **토글**입니다. 기기에서 엄지는 스틱과 면버튼 중 하나만 잡을 수 있어,
    /// 누르고 있는 방식으로는 달리면서 걸을 수가 없습니다. (IOT_INPUT.md 2장)
    ///
    /// 그 토글을 장치가 들고 있어야 하므로 여기서도 장치처럼 들고 있습니다.
    /// 게임 쪽은 켜져 있는지만 보고, 토글인 것을 모릅니다.
    /// </summary>
    private bool _sprintOn;

    // 1대만 들면 양손이 같은 기기를 본다. 게임 코드는 개수를 몰라도 된다.
    public IHandDevice Left => twoDevices ? _left : _single;

    public IHandDevice Right => twoDevices ? _right : _single;

    public bool HasTwoDevices => twoDevices;

    public Vector2 Move => Left.Stick;

    /// <summary>
    /// 카메라. 오른손 스틱이 돌린다.
    ///
    /// ⚠ 기기가 1대면 **0 이다.** 스틱이 1개뿐이고 그것은 걷기가 쓰고 있다.
    ///    여기서 Right.Stick 을 그대로 돌려주면 걷는 스틱이 카메라까지 돌려서,
    ///    앞으로 한 발짝 걸을 때마다 화면이 같이 돌아간다.
    ///    1대에서는 카메라가 고정이고, 같은 이유로 대포 조준도 못 한다.
    /// </summary>
    public Vector2 Look => twoDevices ? _right.Stick : Vector2.zero;

    private void Awake()
    {
        _left = new KeyboardHand(
            axisSpeed,
            logDeviceOutput,
            "왼손",
            stickUp: Key.W, stickDown: Key.S,
            stickLeft: Key.A, stickRight: Key.D,
            button1: Key.C, button2: Key.None,
            swing: Key.None);

        _right = new KeyboardHand(
            axisSpeed,
            logDeviceOutput,
            "오른손",
            stickUp: Key.None, stickDown: Key.None,
            stickLeft: Key.None, stickRight: Key.None,
            button1: Key.Space, button2: Key.K,
            swing: Key.None);

        // 1대만 들었을 때. 스틱은 왼손 것, 면버튼은 오른손 것을 단다.
        _single = new KeyboardHand(
            axisSpeed,
            logDeviceOutput,
            "한 대",
            stickUp: Key.W, stickDown: Key.S,
            stickLeft: Key.A, stickRight: Key.D,
            button1: Key.Space, button2: Key.K,
            swing: Key.None);
    }

    private void Update()
    {
        // 플레이 중에 스크립트를 고치면 도메인 리로드로 두 손이 날아가는데
        // Awake 는 다시 불리지 않는다. 그때 콘솔이 예외로 도배되는 것을 막는다.
        if (_left == null || _right == null || _single == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        // 키보드로는 두 손을 따로 기울일 수 없다. J / L 을 양손이 함께 쓴다.
        float target = 0f;

        if (keyboard != null)
        {
            if (keyboard.jKey.isPressed) { target -= 1f; }
            if (keyboard.lKey.isPressed) { target += 1f; }

            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
            {
                _sprintOn = !_sprintOn;
            }
        }

        // ⚠ 지금 쓰는 기기만 Tick 한다. 안 쓰는 쪽까지 돌리면 아무도 가져가지 않는
        //    "눌린 순간"이 그 안에 쌓이고, 인스펙터에서 기기 수를 바꾸는 순간
        //    쌓여 있던 것이 한꺼번에 터져 나온다.
        if (twoDevices)
        {
            _left.Tick(keyboard, target, Time.deltaTime, forceButton2: _sprintOn, stickOverride: null);
            _right.Tick(keyboard, target, Time.deltaTime, forceButton2: false, stickOverride: ReadDrag());
            return;
        }

        // 1대에는 달리기가 없다. 왼손 면버튼 2 에 해당하는 자리를 발사가 쓰고 있다.
        _single.Tick(keyboard, target, Time.deltaTime, forceButton2: false, stickOverride: null);
    }

    /// <summary>
    /// 오른손 스틱을 마우스 드래그로 흉내낸다. 누르고 끄는 동안만 값이 나온다.
    ///
    /// **우클릭인 이유** — 좌클릭은 채팅창 · 버튼 같은 화면 요소가 써야 합니다.
    /// 커서를 화면에 가두는 방식(로비 · 광산의 옛 방식)을 안 쓰는 것도 같은 이유입니다.
    /// 커서가 갇히면 채팅을 클릭할 수가 없습니다.
    ///
    /// ⚠ 마우스는 **위치가 아니라 이동량**을 준다. 스틱은 −1 ~ 1 로 유지되는 값이라
    ///    감도를 곱해 잘라 넣고, 손을 떼면 0 으로 떨어뜨려야 한다.
    ///    안 그러면 드래그를 멈춰도 카메라가 계속 돈다.
    /// </summary>
    private Vector2? ReadDrag()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.rightButton.isPressed)
        {
            return Vector2.zero;
        }

        // 세로는 버린다. 배 카메라는 좌우로만 돈다.
        float x = mouse.delta.ReadValue().x * lookSensitivity;

        return new Vector2(Mathf.Clamp(x, -1f, 1f), 0f);
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
        private readonly Key _button1;
        private readonly Key _button2;
        private readonly Key _swing;

        private float _axis;
        private Vector2 _stick;
        private bool _button1Held;
        private bool _button2Held;
        private bool _button1Pressed;
        private bool _button2Pressed;
        private bool _swung;

        public KeyboardHand(
            float axisSpeed, bool log, string label,
            Key stickUp, Key stickDown, Key stickLeft, Key stickRight,
            Key button1, Key button2, Key swing)
        {
            _axisSpeed = axisSpeed;
            _log = log;
            _label = label;
            _stickUp = stickUp;
            _stickDown = stickDown;
            _stickLeft = stickLeft;
            _stickRight = stickRight;
            _button1 = button1;
            _button2 = button2;
            _swing = swing;
        }

        public Vector2 Stick => _stick;

        // 키보드에서는 기울기와 회전을 구분할 수 없다.
        // 한 사람이 한 번에 한 작업만 하므로 테스트에는 문제가 없다.
        public float Tilt => _axis;
        public float Rotation => _axis;

        public bool Button1 => _button1Held;
        public bool Button2 => _button2Held;

        /// <param name="forceButton2">
        /// 키가 아니라 밖에서 정해 주는 버튼 2. 달리기 토글이 이 길로 들어온다.
        /// </param>
        /// <param name="stickOverride">
        /// 키가 아니라 밖에서 정해 주는 스틱. 마우스 드래그가 이 길로 들어온다.
        /// null 이면 자기 키로 읽는다.
        /// </param>
        public void Tick(Keyboard keyboard, float axisTarget, float deltaTime,
            bool forceButton2, Vector2? stickOverride)
        {
            // 키보드가 없는 환경(빌드 서버 등)에서도 예외 없이 동작해야 한다.
            if (keyboard == null)
            {
                _axis = Mathf.MoveTowards(_axis, 0f, _axisSpeed * deltaTime);
                _stick = stickOverride ?? Vector2.zero;
                _button1Held = false;
                _button2Held = forceButton2;
                return;
            }

            _axis = Mathf.MoveTowards(_axis, axisTarget, _axisSpeed * deltaTime);

            if (stickOverride.HasValue)
            {
                _stick = stickOverride.Value;
            }
            else
            {
                float x = 0f;
                float y = 0f;
                if (IsPressed(keyboard, _stickLeft)) { x -= 1f; }
                if (IsPressed(keyboard, _stickRight)) { x += 1f; }
                if (IsPressed(keyboard, _stickDown)) { y -= 1f; }
                if (IsPressed(keyboard, _stickUp)) { y += 1f; }

                Vector2 stick = new Vector2(x, y);

                // 대각선이 더 빨라지지 않도록 길이를 1 로 자른다.
                _stick = stick.sqrMagnitude > 1f ? stick.normalized : stick;
            }

            _button1Held = IsPressed(keyboard, _button1);
            _button2Held = forceButton2 || IsPressed(keyboard, _button2);

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
