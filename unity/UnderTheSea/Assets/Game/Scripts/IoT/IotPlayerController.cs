using System;
using System.Globalization;
using UnityEngine;

// 시리얼은 **플레이어 PC 에만** 있습니다. Dedicated Server 빌드에는 완드도 동글도 없고,
// 서버는 클라이언트가 네트워크로 보내온 숫자를 ShipCoopNetworkedController 로 되살려 씁니다.
// 그래서 포트를 만지는 것들은 서버 빌드에서 통째로 뺍니다.
//
// ⚠ 클래스 자체는 남깁니다. 씬·프리팹에 이미 붙어 있어서, 형째로 빼면 서버 빌드가
//    "missing script" 로 읽습니다. 서버에서는 값이 전부 0 · false 로 나가고,
//    이는 포트를 못 열었을 때와 같은 상태라 게임 코드가 이미 감당합니다.
#if !UNITY_SERVER
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;
using System.Threading;
#endif

/// <summary>
/// 완드가 **같은 버튼을 게임마다 다르게 내놓아야 하는 경우**를 가른다.
///
/// 게임별 행동 이름(힌트 · 발사 · 조타)은 여기에 없습니다. 그건 각 미니게임이 정하는
/// 것이고, 이 프로필은 **장치가 값을 내놓는 방식**만 정합니다. (IOT_INPUT.md 3장)
///
///   Shared    배 협동. 왼손 버튼 2 를 달리기 토글로 잠근다
///   Warriors  무쌍. 잠그지 않는다
///   Mine      광산. 잠그지 않는다 — 그 자리가 힌트고, 달리기는 오른손 버튼 2 다
///
/// <see cref="KeyboardControlProfile"/> 과 값이 **1:1 이어야 합니다.** 키보드로 확인한
/// 것이 완드에서 그대로 되게 하려는 것입니다. 저쪽에 프로필이 늘면 여기도 늘립니다.
/// </summary>
public enum IotControlProfile
{
    Shared,
    Warriors,
    Mine,
}

/// <summary>
/// 무선 완드(ESP32-S3)가 USB 동글로 흘려보내는 시리얼 값을 읽어
/// <see cref="IPlayerController"/> 로 내놓는 진짜 장치 구현.
///
/// <see cref="KeyboardPlayerController"/> 를 갈아끼우는 자리입니다.
/// 게임 로직은 둘 중 무엇이 꽂혀 있는지 모릅니다. (GAME_STRUCTURE.md 9장)
///
/// 받는 줄의 형식 — 완드 1대가 1줄을 보낸다. 개행으로 끝난다.
///
///     id,x,y,buttons,tilt,rot,mcount,mtype,strength,ms
///     0,-127,64,2,-90,35,7,2,180,56407
///
///   id        완드 고유번호 0~3. **손이 아니다.** 손 배정은 인스펙터에서 한다
///   x, y      조이스틱        -127 ~ 127
///   buttons   비트 플래그     bit0 = 버튼1, bit1 = 버튼2
///   tilt      IMU roll        -127 ~ 127
///   rot       IMU yaw         -127 ~ 127
///   mcount    동작 누적 카운터 0~255 에서 순환. 값이 아니라 **늘어난 만큼**이 동작 횟수다
///   mtype     동작 종류        0=None 1=HorizontalSwing 2=VerticalSwing 3=Thrust
///   strength  동작 세기        0 ~ 255
///   ms        완드 millis() 의 하위 16비트. 지금은 보관만 한다
///
/// `#` 으로 시작하는 줄은 동글이 찍는 로그입니다. 입력이 아니므로 조용히 버립니다.
/// 필드가 10개보다 많으면 초과분은 무시합니다. 펌웨어가 값을 덧붙여도 여기를 안 고치게 하려는 것입니다.
///
/// 보내는 줄의 형식 — 진동. 동글이 받아 ESP-NOW 로 그 완드에 넘긴다.
///
///     V,&lt;완드번호 0~3&gt;,&lt;세기 0~255&gt;,&lt;지속 ms&gt;
///     V,1,200,120
///
/// ⚠ **이 파일은 Api Compatibility Level 이 .NET Framework 여야 컴파일됩니다.**
///    .NET Standard 에는 System.IO.Ports 가 없습니다.
///    Project Settings → Player → Other Settings 에서 바꿉니다.
///
/// 붙이는 곳
///   로컬 테스트 씬(MineTest · ShipCoopTest)  → Player 오브젝트
///   네트워크 배 협동                          → NetworkRunner 오브젝트
///   ShipCoopInputProvider 가 같은 오브젝트에서 GetComponent 로 찾고,
///   못 찾으면 KeyboardPlayerController 를 스스로 붙입니다. 그래서 **먼저 붙어 있어야** 합니다.
///
/// 키 배치 프로필
///   게임마다 같은 버튼이 다르게 나가야 하는 것이 **하나** 있습니다 — 달리기 토글입니다.
///   배만 왼손 버튼 2 를 토글로 잠급니다. 광산은 그 자리가 힌트라 단발이어야 하고,
///   무쌍도 아직 안 정한 자리라 누른 그대로 내보냅니다.
///   <see cref="IotControlProfile"/> 로 가릅니다. 그 밖의 값은 네 게임이 똑같이 씁니다.
///
///   스틱 · IMU · Look 은 프로필이 없습니다. 카메라가 세로를 쓸지는 **받는 쪽**이 이미
///   갈라 놨습니다. 배는 ShipCoopCamera 가 Look.x 만 떼어 쓰고, 무쌍은 x · y 를 다 씁니다.
///
/// 하드웨어가 없어도 게임은 그대로 돌아갑니다. 포트를 못 열면 조용히 비활성이 되고
/// 모든 값이 0 · false 로 나갑니다.
///
/// 규격 문서는 IOT_INPUT.md 입니다. 특히 6장(하지 말아야 할 것)을 지킵니다.
/// </summary>
public class IotPlayerController : MonoBehaviour, IPlayerController
{
    /// <summary>완드 고유번호가 가질 수 있는 개수. 0 ~ 3.</summary>
    private const int WandCount = 4;

    /// <summary>동작 카운터가 순환하는 지점. 펌웨어가 0~255 로 보낸다.</summary>
    private const int CounterWrap = 256;

    /// <summary>
    /// 완드 시각이 순환하는 지점. millis() 의 하위 16비트라 약 65초마다 0 으로 돌아온다.
    /// 그 순환과 완드가 다시 켜진 것을 갈라내는 데 쓴다.
    /// </summary>
    private const int MillisecondsWrap = 65536;

    /// <summary>
    /// 한 손이 쌓아둘 수 있는 동작의 최대 개수. 넘치면 오래된 것부터 버린다.
    ///
    /// 창을 잠깐 내려놨다 돌아오면 줄이 한꺼번에 밀려든다. 막지 않으면
    /// 그때 쌓인 동작이 전부 터져서 광산 바닥이 한 번에 파인다.
    /// </summary>
    private const int MaxPendingMotions = 4;

    /// <summary>한 프레임에 처리할 줄의 최대 개수. 밀린 줄 때문에 프레임이 멎지 않게 한다.</summary>
    private const int MaxLinesPerFrame = 64;

    /// <summary>큐에 쌓아둘 줄의 최대 개수. 넘으면 오래된 것부터 버린다.</summary>
    private const int MaxQueuedLines = 512;

    /// <summary>줄을 조립하는 버퍼의 최대 길이. 개행이 영영 안 오는 경우를 대비한다.</summary>
    private const int MaxLineBufferLength = 4096;

    /// <summary>한 번에 시리얼에서 걷어올 바이트 수. 50Hz × 10필드면 한참 남는다.</summary>
    private const int ReadChunkBytes = 4096;

    /// <summary>
    /// 읽기가 걸려 있을 수 있는 최대 시간(ms).
    ///
    /// <c>BytesToRead</c> 로 와 있는 만큼만 달라고 하므로 평소에는 곧바로 돌아온다.
    /// 그래도 무한 대기로 두지 않는다. 그러면 포트를 닫을 때 스레드가 안 빠져나온다.
    /// </summary>
    private const int ReadTimeoutMs = 100;

    /// <summary>
    /// 쓰기가 걸려 있을 수 있는 최대 시간(ms).
    ///
    /// 진동 명령은 14바이트뿐이라 115200baud 에서 1.2ms 면 나간다. 그래도 무한 대기로
    /// 두지 않는다. 쓰는 쪽은 <b>메인 스레드</b>라, 동글이 멈춰 출력 버퍼가 차면
    /// 게임이 통째로 언다. 진동은 한 번 놓쳐도 되는 신호다.
    /// </summary>
    private const int WriteTimeoutMs = 20;

    [Header("시리얼 포트")]
    [Tooltip("동글이 잡힌 포트 이름. 장치 관리자에서 확인한다. 예: COM3")]
    [SerializeField] private string portName = "COM3";

    [Tooltip("펌웨어와 같은 값이어야 한다.")]
    [SerializeField] private int baudRate = 115200;

    [Tooltip("받은 것이 있는지 확인하는 간격.\n\n" +
             "ReadExisting 은 기다려 주지 않고 바로 돌아오므로, 쉬지 않으면 코어 하나를 다 태운다.\n" +
             "짧을수록 입력이 빨리 들어오고 CPU 를 더 쓴다.")]
    [SerializeField, Range(1, 50)] private int pollIntervalMs = 5;

    [Tooltip("DTR 신호를 올린다. ESP32-S3 의 USB CDC 는 보통 켜야 값이 나온다.\n\n" +
             "⚠ CP2102 · CH340 같은 변환 칩을 쓰는 보드는 DTR 이 리셋 회로에 물려 있어서, " +
             "켜는 순간 완드가 재부팅될 수 있다. 값이 안 나오면 이것부터 뒤집어 본다.")]
    [SerializeField] private bool dtrEnable = true;

    [Tooltip("RTS 신호를 올린다. DTR 과 같은 이유로 보드에 따라 리셋을 일으킬 수 있다.")]
    [SerializeField] private bool rtsEnable = false;

    [Header("손 배정")]
    [Tooltip("왼손으로 쓸 완드의 고유번호.\n\n" +
             "id 는 완드에 붙은 번호일 뿐 손이 아니다. 기획이 바뀌어도 펌웨어를 " +
             "다시 굽지 않도록 여기서 바꾼다.")]
    [SerializeField, Range(0, WandCount - 1)] private int leftHandId = 0;

    [Tooltip("오른손으로 쓸 완드의 고유번호.\n\n" +
             "왼손과 같은 번호로 두면 기기 1대를 든 것과 같아진다.")]
    [SerializeField, Range(0, WandCount - 1)] private int rightHandId = 1;

    [Tooltip("이 시간 동안 줄이 안 오면 그 완드가 끊긴 것으로 본다.\n\n" +
             "짧게 잡으면 줄 한두 개만 유실돼도 HasTwoDevices 가 false 로 떨어지고, " +
             "그 순간 카메라가 이동 스틱에 붙어 화면이 튄다. 넉넉히 잡는다.")]
    [SerializeField, Min(0.05f)] private float handTimeoutSeconds = 0.5f;

    [Header("키 배치 프로필")]
    [Tooltip("장치가 값을 내놓는 방식을 그 게임에 맞춘다. 행동 이름은 각 미니게임이 정한다.\n\n" +
             "Shared 는 광산 · 배 협동, Warriors 는 무쌍이다. " +
             "KeyboardPlayerController 의 프로필과 같은 값이고, 씬 부트스트랩이 " +
             "SetControlProfile 로 덮어쓸 수 있다.")]
    [SerializeField] private IotControlProfile controlProfile = IotControlProfile.Shared;

    [Header("조타 (양손 휠)")]
    [Tooltip("양손이 완드를 서로 마주 보게 잡는지.\n\n" +
             "조타륜을 잡듯 손바닥을 마주 보게 쥐면 두 완드가 180도 돌아간 상태가 된다. " +
             "휠을 한 방향으로 돌려도 각 완드가 재는 roll 은 **부호가 반대로** 나오고, " +
             "ShipCoopInput.Steer 가 둘을 평균 내는 순간 서로 상쇄돼 조타가 죽는다.\n\n" +
             "켜면 왼손 완드의 Tilt 부호를 뒤집어 둘을 같은 방향으로 맞춘다. " +
             "두 완드를 같은 방향으로 쥐는 배치라면 끈다.\n\n" +
             "⚠ 어느 쪽인지는 눈으로 알 수 없다. firmware/tools/tilt_sign.ps1 로 " +
             "실제로 돌려서 정한다.\n\n" +
             "2026-09-23 실측(완드 0·1)에서는 두 손의 roll 이 같은 방향이라 " +
             "**끔** 이 맞았다. 잡는 방식을 바꾸면 다시 재야 한다.")]
    [SerializeField] private bool mirroredGrip = false;

    [Header("키보드 폴백")]
    [Tooltip("완드가 하나도 안 붙어 있으면 키보드로 대신 논다.\n\n" +
             "장치를 꽂으면 완드로, 빼면 키보드로 자동으로 넘어간다. 게임 코드는 " +
             "이 컴포넌트 하나만 보므로 무엇이 값을 채우는지 모른다.\n\n" +
             "끄면 완드가 없을 때 모든 값이 0 이다. 장치 없이 게임이 어떻게 보이는지 " +
             "확인할 때만 끈다.")]
    [SerializeField] private bool keyboardFallback = true;

    [Header("달리기")]
    [Tooltip("왼손 버튼 2 를 토글로 바꾼다. 한 번 눌러 켜고 다시 눌러 끈다.\n\n" +
             "기기에서 엄지는 스틱과 면버튼 중 하나만 잡는다. 누르고 있는 방식으로는 " +
             "달리면서 걸을 수가 없어서 토글이다. (IOT_INPUT.md 2장)\n\n" +
             "끄면 버튼을 누르고 있는 동안에만 참이 된다. 규정이 바뀌거나 확인할 때 쓴다.\n\n" +
             "Warriors 프로필에서는 이 값과 무관하게 걸지 않는다. 그 자리가 회피다.")]
    [SerializeField] private bool sprintToggle = true;

    [Header("진단")]
    [Tooltip("받은 줄을 그대로 콘솔에 찍는다. 펌웨어를 고치는 동안에만 켠다. 매우 시끄럽다.")]
    [SerializeField] private bool logRawLines = false;

    [Tooltip("해석하지 못한 줄을 찍는다. 조용히 버리면 포맷이 어긋난 것을 눈치채지 못한다.")]
    [SerializeField] private bool logParseFailures = false;

    [Tooltip("동글이 보내는 '#' 줄을 찍는다. 버리는 것은 그대로이고 보기만 한다.\n\n" +
             "위의 원본 줄 로그와는 따로 논다. 펌웨어를 고치는 동안 동글이 하는 말만 " +
             "골라 보고 싶을 때 이것만 켠다.")]
    [SerializeField] private bool logDongleMessages = false;

    [Tooltip("각 완드가 연결되고 끊기는 순간을 찍는다. 접촉 불량을 찾을 때 쓴다.")]
    [SerializeField] private bool logConnectionChanges = false;

    [Tooltip("동작(가로베기 · 세로베기 · 찌르기)이 들어올 때마다 종류와 세기를 찍는다.\n\n" +
             "휘둘렀는데 게임이 안 움직일 때 어디가 끊겼는지 가른다.\n" +
             "  줄이 안 나온다   → 완드가 감지를 못 했다 (임계값 · 쿨다운)\n" +
             "  '종류 없음' 이 나온다 → 감지는 했는데 mtype 이 0 이다\n" +
             "  정상으로 나온다  → 장치는 다 했다. 게임 쪽을 본다\n\n" +
             "원본 줄 로그와 달리 동작이 있을 때만 찍어서 조용하다.")]
    [SerializeField] private bool logMotions = false;

    [Tooltip("진동 호출을 찍는다. 완드로 'V,번호,세기,지속ms' 한 줄이 나간다.\n\n" +
             "⚠ 아직 모터가 안 달려 있어 실제로 울리지는 않는다. 명령이 닿았는지는 " +
             "동글 로그의 #ECHO 로 본다.")]
    [SerializeField] private bool logDeviceOutput = false;

    /// <summary>
    /// 완드가 없을 때 대신 값을 채우는 키보드. <c>keyboardFallback</c> 이 켜져 있을 때만 만든다.
    ///
    /// ⚠ **자식 오브젝트에 만듭니다.** 같은 오브젝트에 두면 게임 코드의
    ///   <c>GetComponent&lt;IPlayerController&gt;()</c> 가 완드 대신 이쪽을 집을 수 있습니다.
    ///   어느 쪽이 잡힐지는 컴포넌트를 붙인 순서가 정하는데, 씬을 만지다 보면 쉽게 뒤집힙니다.
    ///   자식은 <c>GetComponent</c> 도 <c>GetComponentInParent</c> 도 보지 못하므로
    ///   **플레이어에 남는 구현체가 이것 하나뿐**이 됩니다.
    /// </summary>
    private KeyboardPlayerController _keyboard;

    /// <summary>지금 값을 채우는 것이 키보드인가.</summary>
    private bool _keyboardActive;

    /// <summary>완드 4대분의 상태. 고유번호가 곧 첨자다.</summary>
    private Wand[] _wands;

    /// <summary>지금 왼손 노릇을 하는 완드.</summary>
    private Wand _left;

    /// <summary>지금 오른손 노릇을 하는 완드. 한 대만 붙어 있으면 <see cref="_left"/> 와 같다.</summary>
    private Wand _right;

    private bool _hasTwoDevices;

#if !UNITY_SERVER
    private SerialPort _port;
    private Thread _readThread;

    /// <summary>읽기 스레드가 도는 동안 true. 스레드와 메인이 함께 본다.</summary>
    private volatile bool _running;

    /// <summary>읽기 스레드에서 난 예외. 메인 스레드가 가져다 로그로 옮긴다.</summary>
    private volatile string _threadError;

    /// <summary>읽기 스레드 → 메인 스레드. **완성된 줄만** 넘기고 해석은 메인에서 한다.</summary>
    private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
#endif

    // ------------------------------------------------------------
    // IPlayerController
    // ------------------------------------------------------------

    public IHandDevice Left
    {
        get { EnsureWands(); return _keyboardActive ? _keyboard.Left : _left; }
    }

    /// <summary>오른손 완드. 끊겨 있으면 왼손과 같은 것을 돌려준다. 게임 코드는 개수를 몰라도 된다.</summary>
    public IHandDevice Right
    {
        get { EnsureWands(); return _keyboardActive ? _keyboard.Right : _right; }
    }

    public bool HasTwoDevices
    {
        get { EnsureWands(); return _keyboardActive ? _keyboard.HasTwoDevices : _hasTwoDevices; }
    }

    public Vector2 Move => Left.Stick;

    /// <summary>
    /// 카메라. 오른손 스틱이 돌린다.
    ///
    /// ⚠ 기기가 1대면 **0 이다.** 스틱이 1개뿐이고 그것은 걷기가 쓰고 있다.
    ///    여기서 Right.Stick 을 그대로 돌려주면 걷는 스틱이 카메라까지 돌려서,
    ///    앞으로 한 발짝 걸을 때마다 화면이 같이 돌아간다.
    ///    <see cref="KeyboardPlayerController"/> 와 같은 기준이다. (SHIPCOOP.md 7장)
    /// </summary>
    public Vector2 Look => HasTwoDevices ? Right.Stick : Vector2.zero;

    /// <summary>
    /// 완드가 한 대라도 붙어 있는지. HUD 가 키캡을 완드 버튼으로 바꿀지 정할 때 본다.
    ///
    /// 포트가 열린 것만으로는 부족하다. 동글만 꽂고 완드를 안 켰으면 값이 전부 0 이라
    /// 사람은 여전히 키보드로 하고 있다. 줄이 들어오고 있는 완드가 있어야 참이다.
    /// 왼손 자리에는 살아 있는 완드가 먼저 앉으므로 (<see cref="ResolveHands"/>) 그것만 보면 된다.
    /// </summary>
    public bool AnyWandConnected
    {
        get { EnsureWands(); return _left.Connected; }
    }

    /// <summary>지금 포트가 열려 있는지. 디버그 HUD 가 본다. 서버 빌드에서는 늘 false 다.</summary>
#if !UNITY_SERVER
    public bool PortOpen => _port != null && _port.IsOpen;
#else
    public bool PortOpen => false;
#endif

    // ------------------------------------------------------------
    // 수명
    // ------------------------------------------------------------

    private void Awake()
    {
        // ShipCoopInputProvider.Awake 가 GetComponent<IPlayerController>() 로 찾는다.
        // 여기서 손을 미리 만들어 두지 않으면 그쪽이 먼저 돌 때 빈 손을 쥐게 된다.
        EnsureWands();
    }

    /// <summary>
    /// 키 배치 프로필을 밖에서 정한다. 인스펙터 값이 기본이고 이 호출이 덮어쓴다.
    ///
    /// <see cref="KeyboardPlayerController.SetControlProfile"/> 와 같은 자리입니다.
    /// 무쌍은 <c>WarriorsSceneBootstrap</c> 과 <c>WarriorsLocalPlayerController</c> 가
    /// 키보드 쪽에 이미 이렇게 하고 있어서, 완드도 같은 길로 받습니다.
    ///
    /// ⚠ 씬 부트스트랩이 이 컴포넌트의 <c>Awake</c> 보다 먼저 부를 수 있습니다.
    ///    그래서 손이 아직 없으면 여기서 만듭니다.
    /// </summary>
    public void SetControlProfile(IotControlProfile profile)
    {
        controlProfile = profile;

        EnsureWands();
        ApplySprintToggle();
    }

#if !UNITY_SERVER
    private void OnEnable()
    {
        OpenPort();
    }

    private void OnDisable()
    {
        ClosePort();
    }

    private void OnApplicationQuit()
    {
        // 에디터에서 플레이를 멈췄다 다시 시작할 때 포트가 잠겨 있으면 안 된다.
        ClosePort();
    }
#endif

    private void Update()
    {
        EnsureWands();

#if !UNITY_SERVER
        ReportThreadError();
        DrainLines();
#endif

        float now = Time.unscaledTime;

        // 서버 빌드에서는 줄이 한 줄도 안 들어오므로 여기서 모든 완드가 끊긴 것으로 판정된다.
        // 값은 전부 0 · false 가 되고, 이는 완드를 안 꽂았을 때와 같은 상태다.
        RefreshConnections(now);
        ResolveHands();
        RefreshKeyboardFallback();
    }

    // ------------------------------------------------------------
    // 시리얼 — 서버 빌드에는 들어가지 않는다
    // ------------------------------------------------------------

#if !UNITY_SERVER

    /// <summary>
    /// 포트를 연다. **실패해도 예외를 밖으로 던지지 않는다.**
    /// 하드웨어가 없는 자리에서도 게임은 그대로 돌아가야 한다.
    /// </summary>
    private void OpenPort()
    {
        if (_port != null)
        {
            return;
        }

        SerialPort port;

        try
        {
            port = new SerialPort(portName, baudRate)
            {
                DtrEnable = dtrEnable,
                RtsEnable = rtsEnable,

                // ⚠ 무한 대기로 두면 포트를 닫을 때 읽기 스레드가 안 빠져나온다.
                ReadTimeout = ReadTimeoutMs,

                // ⚠ 메인 스레드에서 쓴다. 무한 대기로 두면 동글이 멈췄을 때 게임이 통째로 언다.
                WriteTimeout = WriteTimeoutMs,
            };

            port.Open();
        }
        catch (Exception error)
        {
            // 경고 한 줄만 남기고 조용히 비활성이 된다. 모든 값은 0 · false 로 나간다.
            Debug.LogWarning(
                $"[IotPlayerController] {portName} 을(를) 열지 못했습니다. " +
                $"장치 없이 계속합니다. — {error.Message}", this);
            return;
        }

        _port = port;
        _running = true;
        _threadError = null;

        // ⚠ Mono 의 SerialPort.DataReceived 와 ReadLine 은 유니티에서 동작하지 않는다.
        //    ReadExisting 으로 받은 만큼 가져와 줄을 직접 조립한다.
        _readThread = new Thread(() => ReadLoop(port))
        {
            IsBackground = true,
            Name = "IotPlayerController.Read",
        };

        _readThread.Start();
    }

    /// <summary>
    /// 포트와 스레드를 정리한다. 여러 번 불러도 괜찮다.
    ///
    /// **스레드를 먼저 세우고 포트를 닫는다.** 읽기가 더는 블로킹하지 않고
    /// 폴링 간격마다 <see cref="_running"/> 을 확인하므로, 그 몇 배만 기다리면
    /// 스스로 빠져나온다. 닫아서 억지로 예외를 내는 것보다 깨끗하다.
    /// </summary>
    private void ClosePort()
    {
        _running = false;

        Thread thread = _readThread;
        _readThread = null;

        if (thread != null && thread.IsAlive)
        {
            if (!thread.Join(Mathf.Max(200, pollIntervalMs * 4)))
            {
                Debug.LogWarning("[IotPlayerController] 읽기 스레드가 제때 끝나지 않았습니다.", this);
            }
        }

        SerialPort port = _port;
        _port = null;

        if (port != null)
        {
            try
            {
                port.Close();
                port.Dispose();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[IotPlayerController] 포트를 닫는 중 문제가 있었습니다. — {error.Message}", this);
            }
        }

        while (_lines.TryDequeue(out _))
        {
        }

        // 손에 남아 있던 값을 지운다. 다음에 다시 열 때 묵은 값이 한 번 나가지 않게 한다.
        if (_wands != null)
        {
            for (int i = 0; i < _wands.Length; i++)
            {
                _wands[i].Clear();
            }
        }
    }

    /// <summary>
    /// 읽기 스레드.
    ///
    /// <c>ReadExisting</c> 은 지금 와 있는 만큼만 돌려주고 바로 끝납니다. 그래서 줄이
    /// 중간에서 잘려 들어옵니다. 잔여 버퍼에 이어 붙였다가 **개행을 만난 것만** 큐에 넣습니다.
    /// 큐가 "완성된 줄만 담는다" 는 약속을 지켜야 해석하는 쪽
    /// (<see cref="DrainLines"/> · <see cref="TryParse"/>)이 아무것도 몰라도 됩니다.
    /// </summary>
    private void ReadLoop(SerialPort port)
    {
        StringBuilder buffer = new StringBuilder();

        byte[] bytes = new byte[ReadChunkBytes];
        char[] chars = new char[ReadChunkBytes];

        // 동글의 '#' 로그에는 한글이 섞여 온다. UTF-8 은 한 글자가 여러 바이트라
        // 조각 경계에서 잘릴 수 있으므로, 상태를 들고 이어서 푸는 Decoder 를 쓴다.
        // (숫자 줄만 보면 ASCII 라 상관없지만, 로그가 깨지면 진단이 안 된다)
        Decoder decoder = Encoding.UTF8.GetDecoder();

        int sleepMs = Mathf.Max(1, pollIntervalMs);

        while (_running)
        {
            int read;

            try
            {
                int available = port.BytesToRead;

                if (available <= 0)
                {
                    Thread.Sleep(sleepMs);
                    continue;
                }

                read = port.Read(bytes, 0, Mathf.Min(available, bytes.Length));
            }
            catch (TimeoutException)
            {
                // 읽을 것이 없었을 뿐이다. 오류가 아니다.
                continue;
            }
            catch (Exception error)
            {
                if (!_running)
                {
                    break;      // 우리가 닫아서 난 예외다. 정상 종료.
                }

                _threadError = error.Message;
                break;
            }

            if (read > 0)
            {
                int count = decoder.GetChars(bytes, 0, read, chars, 0);
                buffer.Append(chars, 0, count);
                ExtractLines(buffer);
            }

            // 읽기는 기다려 주지 않는다. 쉬지 않으면 코어 하나를 다 태운다.
            Thread.Sleep(sleepMs);
        }
    }

    /// <summary>버퍼에서 개행으로 끝난 줄만 잘라 큐에 넣는다. 마지막 조각은 다음 회차로 넘긴다.</summary>
    private void ExtractLines(StringBuilder buffer)
    {
        int start = 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != '\n')
            {
                continue;
            }

            // Trim 이 '\r' 도 같이 떼어준다. 펌웨어가 "\r\n" 을 보내도 된다.
            string line = buffer.ToString(start, i - start).Trim();
            start = i + 1;

            if (line.Length > 0)
            {
                Enqueue(line);
            }
        }

        if (start > 0)
        {
            buffer.Remove(0, start);
        }

        // 개행이 영영 안 오면 버퍼가 무한히 커진다. 그때는 통째로 버린다.
        if (buffer.Length > MaxLineBufferLength)
        {
            buffer.Clear();
        }
    }

    private void Enqueue(string line)
    {
        _lines.Enqueue(line);

        // 창을 내려놨다 돌아오면 줄이 한꺼번에 밀려든다. 오래된 것부터 버린다.
        while (_lines.Count > MaxQueuedLines && _lines.TryDequeue(out _))
        {
        }
    }

    /// <summary>읽기 스레드에서 난 예외를 메인 스레드에서 한 번만 로그로 옮긴다.</summary>
    private void ReportThreadError()
    {
        string error = _threadError;

        if (error == null)
        {
            return;
        }

        _threadError = null;

        Debug.LogWarning(
            $"[IotPlayerController] 읽기가 멈췄습니다. 동글을 다시 꽂고 컴포넌트를 껐다 켜세요. — {error}", this);
    }

    // ------------------------------------------------------------
    // 줄 해석 — 큐에서 꺼내는 쪽이라 시리얼과 함께 빠진다
    // ------------------------------------------------------------

    private void DrainLines()
    {
        int handled = 0;
        float now = Time.unscaledTime;

        while (handled < MaxLinesPerFrame && _lines.TryDequeue(out string line))
        {
            handled++;

            // 동글이 찍는 로그다. 입력이 아니므로 버린다. 해석 실패로 세지 않는다.
            if (line[0] == '#')
            {
                if (logDongleMessages)
                {
                    Debug.Log($"[IotPlayerController] 동글 — {line}", this);
                }

                continue;
            }

            if (logRawLines)
            {
                Debug.Log($"[IotPlayerController] 수신 — {line}", this);
            }

            if (!TryParse(line, out Packet packet))
            {
                if (logParseFailures)
                {
                    Debug.LogWarning($"[IotPlayerController] 해석 실패 — {line}", this);
                }

                continue;
            }

            _wands[packet.Id].Apply(in packet, now);
        }
    }

#endif  // !UNITY_SERVER

    /// <summary>
    /// 줄 하나를 숫자 열 개로 나눈다. 하나라도 어긋나면 false 를 돌려주고 그 줄을 버린다.
    ///
    /// **필드가 열 개보다 많으면 초과분은 그냥 둔다.** 펌웨어가 값을 덧붙였을 때
    /// 유니티까지 같이 고쳐야 하면 둘을 한꺼번에 배포해야 하는데, 그럴 이유가 없다.
    ///
    /// 자릿수와 완드 번호만 여기서 본다. 값의 정규화는 <see cref="Wand.Apply"/> 가 한다.
    /// </summary>
    private static bool TryParse(string line, out Packet packet)
    {
        packet = default;

        string[] parts = line.Split(',');

        if (parts.Length < 10)
        {
            return false;
        }

        if (!TryReadInt(parts[0], out int id) || id < 0 || id >= WandCount) { return false; }
        if (!TryReadInt(parts[1], out int x)) { return false; }
        if (!TryReadInt(parts[2], out int y)) { return false; }
        if (!TryReadInt(parts[3], out int buttons)) { return false; }
        if (!TryReadInt(parts[4], out int tilt)) { return false; }
        if (!TryReadInt(parts[5], out int rotation)) { return false; }
        if (!TryReadInt(parts[6], out int motionCount)) { return false; }
        if (!TryReadInt(parts[7], out int motionType)) { return false; }
        if (!TryReadInt(parts[8], out int strength)) { return false; }
        if (!TryReadInt(parts[9], out int milliseconds)) { return false; }

        packet = new Packet(id, x, y, buttons, tilt, rotation, motionCount, motionType, strength, milliseconds);
        return true;
    }

    private static bool TryReadInt(string text, out int value)
    {
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    // ------------------------------------------------------------
    // 손 배정
    // ------------------------------------------------------------

    private void EnsureWands()
    {
        if (_wands != null)
        {
            return;
        }

        _wands = new Wand[WandCount];

        for (int i = 0; i < WandCount; i++)
        {
            _wands[i] = new Wand(this, i);
        }

        ResolveHands();
        EnsureKeyboardFallback();
    }

    /// <summary>
    /// 키보드 폴백을 만들어 둔다. 실행 중에만, 한 번만 만든다.
    ///
    /// 프로필은 이 컴포넌트의 것을 그대로 넘깁니다. 두 enum 은 값이 1:1 이라
    /// 첨자를 그대로 바꿔 끼웁니다. 어긋나면 키보드가 다른 게임 배치로 돌아갑니다.
    /// </summary>
    private void EnsureKeyboardFallback()
    {
        if (!keyboardFallback || _keyboard != null || !Application.isPlaying)
        {
            return;
        }

        // 위 _keyboard 주석 참고. 반드시 자식이어야 한다.
        GameObject host = new GameObject("[KeyboardFallback]");
        host.transform.SetParent(transform, false);
        host.hideFlags = HideFlags.DontSave;

        _keyboard = host.AddComponent<KeyboardPlayerController>();
        _keyboard.SetControlProfile((KeyboardControlProfile)(int)controlProfile);

        // 쓰지 않는 동안에는 꺼 둔다. 켜 두면 아무도 안 가져가는 "눌린 순간" 이
        // 그 안에 쌓이고, 완드가 끊기는 순간 그것이 한꺼번에 터진다.
        _keyboard.enabled = false;
        _keyboardActive = false;

        RefreshKeyboardFallback();
    }

    /// <summary>
    /// 완드가 하나라도 붙어 있으면 완드, 아니면 키보드로 넘긴다.
    ///
    /// **완드를 들고 있는 동안에는 키보드를 아예 끕니다.** 둘을 같이 켜 두면 한쪽이
    /// 0 을 내놓는 프레임에 값이 튀고, 무엇이 값을 채웠는지 알 수 없게 됩니다.
    /// </summary>
    private void RefreshKeyboardFallback()
    {
        if (_keyboard == null)
        {
            return;
        }

        bool wanted = !AnyWandConnected();

        if (wanted == _keyboardActive)
        {
            return;
        }

        _keyboardActive = wanted;
        _keyboard.enabled = wanted;

        // 넘어가는 순간 양쪽의 묵은 눌림을 버린다. 그러지 않으면 전환 직후에
        // 쌓여 있던 것이 한꺼번에 나가 대포가 저절로 발사되거나 땅이 한 번 더 파인다.
        DrainPending(_keyboard.Left);
        DrainPending(_keyboard.Right);
        DrainPending(_left);
        DrainPending(_right);

        if (logConnectionChanges)
        {
            Debug.Log($"[IotPlayerController] 입력 전환 — {(wanted ? "키보드" : "완드")}", this);
        }
    }

    /// <summary>완드가 한 대라도 붙어 있는가.</summary>
    private bool AnyWandConnected()
    {
        for (int i = 0; i < _wands.Length; i++)
        {
            if (_wands[i].Connected)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>한 손에 쌓여 있는 "눌린 순간" 과 동작을 읽어서 버린다.</summary>
    private static void DrainPending(IHandDevice hand)
    {
        if (hand == null)
        {
            return;
        }

        hand.ConsumeButton1Press();
        hand.ConsumeButton2Press();

        while (hand.TryConsumeMotion(out _))
        {
        }
    }

    /// <summary>끊김을 시간으로 판정하고, 상태가 바뀐 완드만 로그로 알린다.</summary>
    private void RefreshConnections(float now)
    {
        for (int i = 0; i < _wands.Length; i++)
        {
            if (!_wands[i].Refresh(now, handTimeoutSeconds))
            {
                continue;
            }

            if (logConnectionChanges)
            {
                string state = _wands[i].Connected ? "연결됨" : "끊김";
                Debug.Log($"[IotPlayerController] 완드 {i} {state}", this);
            }
        }
    }

    /// <summary>
    /// 지금 붙어 있는 완드로 왼손 · 오른손을 정한다.
    ///
    /// <code>
    /// 둘 다 붙음     왼손 · 오른손 따로       HasTwoDevices = true
    /// 한 대만 붙음   그 한 대가 양손을 겸함   HasTwoDevices = false
    /// 둘 다 끊김     값이 전부 0 인 손        HasTwoDevices = false
    /// </code>
    ///
    /// 한 대만 붙었을 때 오른손이 왼손과 **같은 객체**가 되는 것은
    /// <see cref="KeyboardPlayerController"/> 의 1대 모드와 같은 방식입니다.
    /// 게임 코드는 이미 그쪽에 맞춰져 있습니다. (ShipCoopInput · MineDigger)
    /// </summary>
    private void ResolveHands()
    {
        Wand left = _wands[Mathf.Clamp(leftHandId, 0, WandCount - 1)];
        Wand right = _wands[Mathf.Clamp(rightHandId, 0, WandCount - 1)];

        if (left.Connected && right.Connected && !ReferenceEquals(left, right))
        {
            _left = left;
            _right = right;
            _hasTwoDevices = true;
        }
        else
        {
            // 한 대만 살아 있으면 그 한 대가 양손을 겸한다.
            //
            // 왼손 쪽이 끊기고 오른손만 남은 경우에도 살아 있는 쪽을 왼손 자리에 놓는다.
            // 걷기가 왼손 스틱이라, 그러지 않으면 손에 완드를 들고도 한 발짝도 못 뗀다.
            Wand only = left.Connected ? left : (right.Connected ? right : left);

            _left = only;
            _right = only;
            _hasTwoDevices = false;
        }

        ApplySprintToggle();
        ApplyGripMirror();
    }

    /// <summary>
    /// 마주 잡기 보정을 **왼손 완드에만** 건다.
    ///
    /// 펌웨어는 자기가 어느 손인지 모릅니다. 완드는 중력으로 잰 roll 을 그대로 올릴 뿐이고,
    /// 두 완드가 서로 마주 보게 쥐였는지는 손 배정을 아는 이 스크립트만 압니다.
    /// 달리기 토글을 브리지에서 거는 것과 같은 이유입니다.
    ///
    /// 1대만 들었을 때는 걸지 않습니다. 상쇄될 짝이 없고,
    /// <c>ShipCoopInput.Steer</c> 도 1대면 왼손 값을 그대로 씁니다.
    /// 여기서 뒤집으면 혼자 들었을 때만 조타가 거꾸로 돕니다.
    /// </summary>
    private void ApplyGripMirror()
    {
        bool enabled = mirroredGrip && _hasTwoDevices;

        for (int i = 0; i < _wands.Length; i++)
        {
            _wands[i].SetTiltMirrored(enabled && ReferenceEquals(_wands[i], _left));
        }
    }

    /// <summary>
    /// 달리기 토글을 **왼손 완드에만** 건다.
    ///
    /// 오른손 버튼 2 는 발사 · 망치질이라 누른 순간만 인정해야 합니다.
    /// 거기에 토글을 걸면 한 번 걸러 쏘게 됩니다.
    ///
    /// 기기가 1대일 때도 걸지 않습니다. 그때는 왼손과 오른손이 같은 완드라
    /// 왼손에 건 토글이 그대로 발사 버튼이 됩니다. 문서도 1대에서는 달리기를
    /// 빼고 있고, <c>ShipCoopInput.Sprint</c> 가 이미 1대면 스스로 물러납니다.
    ///
    /// 펌웨어는 자기가 어느 손인지 모릅니다. 손 배정은 이 스크립트의 인스펙터에만
    /// 있으므로, 버튼 레벨을 토글로 바꾸는 일도 여기서 합니다.
    ///
    /// **무쌍(<see cref="IotControlProfile.Warriors"/>)에는 걸지 않습니다.** 토글은 배가
    /// 걷는 내내 달려야 해서 필요한 것이고, 무쌍에는 그런 조작이 없습니다.
    ///
    /// ⚠ 무쌍의 왼손 버튼 2 가 무엇이 될지는 **아직 안 정했습니다.** IOT_INPUT.md 7장이
    ///   회피를 키보드 Shift 로 옮기라고 적고 2장이 Shift 를 왼손 버튼 2 에 두지만,
    ///   무쌍의 장치 배치를 직접 정한 표는 없습니다. (8장이 열어 둔 상태입니다)
    ///   정해지기 전에는 **누른 그대로** 내보내는 쪽이 안전합니다. 토글로 잠가 두면
    ///   그 자리에 단발 조작이 오는 순간 한 번 걸러 먹힙니다.
    ///
    /// **광산(<see cref="IotControlProfile.Mine"/>)에도 걸지 않습니다.** 거기는 왼손
    /// 버튼 2 가 **힌트**고, 달리기는 **오른손 버튼 2** 입니다. (MINE.md 8장,
    /// <c>KeyboardPlayerController</c> 의 Mine 프로필과 같은 배치)
    ///
    /// 힌트 자체는 <c>ConsumeButton2Press</c> 로만 읽어서 잠겨도 눌리지만, 잠가 두면
    /// 힌트를 한 번 누른 뒤 <c>Button2</c> 가 계속 참으로 남습니다. 지금은 그 레벨을
    /// 읽는 곳이 없어 표가 안 나지만, 생기는 순간 조용히 깨집니다.
    /// </summary>
    private void ApplySprintToggle()
    {
        bool enabled = sprintToggle
                       && _hasTwoDevices
                       && controlProfile == IotControlProfile.Shared;

        for (int i = 0; i < _wands.Length; i++)
        {
            _wands[i].SetSprintToggle(enabled && ReferenceEquals(_wands[i], _left));
        }
    }

    // ------------------------------------------------------------
    // 진동 (게임 → 장치)
    // ------------------------------------------------------------

    /// <summary>양손을 함께 울린다. 방향이 없는 충격에 쓴다. (IOT_INPUT.md 5장)</summary>
    public void VibrateBoth(float strength, float seconds)
    {
        Left.Vibrate(strength, seconds);

        if (HasTwoDevices)
        {
            Right.Vibrate(strength, seconds);
        }
    }

    /// <summary>
    /// 진동 명령을 완드로 내려보낸다. 동글이 받아 ESP-NOW 로 그 완드에 넘긴다.
    ///
    ///     V,&lt;완드번호 0~3&gt;,&lt;세기 0~255&gt;,&lt;지속 ms&gt;
    ///
    /// 지속시간은 펌웨어가 16비트로 받으므로 65535ms 에서 자릅니다.
    ///
    /// 포트가 안 열려 있으면 **조용히 넘어갑니다.** 하드웨어가 없는 자리에서도
    /// 게임은 그대로 돌아가야 합니다. (IOT_INPUT.md 5장)
    ///
    /// ⚠ 읽기 스레드와 같은 포트를 씁니다. <c>SerialPort</c> 는 읽는 쪽과 쓰는 쪽이
    ///    각각 한 스레드씩인 동안은 서로를 건드리지 않으므로 락을 걸지 않았습니다.
    ///    닫는 쪽(<c>ClosePort</c>)도 여기와 같은 메인 스레드라 부딪히지 않습니다.
    ///    **읽기 스레드에서는 절대 부르지 마세요.**
    ///
    /// 서버 빌드에서는 포트가 없으므로 로그만 남기고 끝납니다. 진동을 울릴 손은
    /// 그 사람 PC 에 있고, 서버는 <c>Rpc_Vibrate</c> 로 그쪽에 부탁합니다.
    /// </summary>
    /// <param name="wandId">완드 고유번호. 손이 아니라 기기 번호다.</param>
    private void SendVibrate(int wandId, float strength, float seconds)
    {
        if (logDeviceOutput)
        {
            Debug.Log(
                $"[IotPlayerController] 완드 {wandId} 진동 — 세기 {strength:0.00}, {seconds:0.00}초", this);
        }

#if !UNITY_SERVER
        SerialPort port = _port;

        if (port == null || !port.IsOpen)
        {
            return;
        }

        // strength 는 Clamp01, seconds 는 음수 제거를 부르는 쪽(Wand.Vibrate)이 이미 했다.
        int level = Mathf.RoundToInt(strength * 255f);
        int milliseconds = Mathf.Min(Mathf.RoundToInt(seconds * 1000f), 65535);

        try
        {
            port.Write($"V,{wandId},{level},{milliseconds}\n");
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                $"[IotPlayerController] 완드 {wandId} 진동을 보내지 못했습니다. — {error.Message}", this);
        }
#endif
    }

    /// <summary>
    /// 동작 하나가 대기열에 들어갔다고 알린다. <c>logMotions</c> 가 켜져 있을 때만 찍는다.
    ///
    /// 찍는 자리를 <see cref="Wand"/> 안이 아니라 여기로 둔 것은, 인스펙터 토글이
    /// 이 컴포넌트에 있고 완드는 그것을 모르기 때문입니다. 진동 로그와 같은 방식입니다.
    /// </summary>
    internal void ReportMotion(int wandId, HandMotion motion, int repeat)
    {
        if (!logMotions)
        {
            return;
        }

        string times = repeat > 1 ? $" ×{repeat}" : string.Empty;

        Debug.Log(
            $"[IotPlayerController] 완드 {wandId} 동작 — {motion.Type} " +
            $"세기 {motion.Strength:0.00}{times}", this);
    }

    /// <summary>
    /// 동작 카운터는 올랐는데 종류가 <c>None</c> 이라 버렸다고 알린다.
    ///
    /// 이 줄이 보이면 **펌웨어는 감지했는데 mtype 을 0 으로 보낸 것**입니다.
    /// 조용히 버리면 완드가 감지를 못 한 것과 구분이 안 됩니다.
    /// </summary>
    internal void ReportUnknownMotion(int wandId, int rawType)
    {
        if (!logMotions)
        {
            return;
        }

        Debug.LogWarning(
            $"[IotPlayerController] 완드 {wandId} 동작 — 종류 없음 (mtype {rawType}). 버립니다.", this);
    }

    // ------------------------------------------------------------
    // 완드 1대분
    // ------------------------------------------------------------

    /// <summary>완드 1대의 지금 상태. 시리얼 원값을 게임이 쓰는 단위로 바꿔서 들고 있는다.</summary>
    public sealed class Wand : IHandDevice
    {
        private readonly IotPlayerController _owner;
        private readonly int _id;

        /// <summary>
        /// 대기 중인 동작. **종류와 세기를 쌍으로** 들고 있어야 무쌍의 공격 3종이 살아남는다.
        /// 개수만 세면 가로베기인지 찌르기인지가 사라진다.
        /// </summary>
        private readonly HandMotion[] _motions = new HandMotion[MaxPendingMotions];

        /// <summary>다음에 꺼낼 자리.</summary>
        private int _motionHead;

        private int _motionCount;

        private Vector2 _stick;
        private float _tilt;
        private float _rotation;

        private bool _button1Held;
        private bool _button2Held;

        private bool _button1Pressed;
        private bool _button2Pressed;

        /// <summary>버튼 2 를 토글로 내놓을지. 왼손일 때만 켜진다.</summary>
        private bool _useSprintToggle;

        /// <summary>토글이 켜져 있는지. 장치가 들고 있어야 하는 상태다. (IOT_INPUT.md 3장)</summary>
        private bool _sprintOn;

        /// <summary>Tilt 부호를 뒤집어 내놓을지. 마주 잡기일 때 왼손에만 켜진다.</summary>
        private bool _mirrorTilt;

        /// <summary>직전 줄의 동작 카운터. 늘어난 만큼이 동작 횟수다.</summary>
        private int _lastCounter;

        /// <summary>
        /// 직전 줄의 완드 시각. **이 완드가 다시 켜졌는지 보는 데만 쓴다.**
        ///
        /// ⚠ 다른 완드의 시각과 비교하지 않는다. millis() 는 각 완드가 켜진 시점부터
        ///    세기 때문에 원점이 서로 다르다. 같은 완드 안에서 시간이 되감긴 것만 본다.
        /// </summary>
        private int _previousMilliseconds;

        /// <summary>카운터를 한 번이라도 받아본 적 있는지. 첫 줄은 기준만 잡고 넘어간다.</summary>
        private bool _hasCounter;

        private float _lastSeenTime = float.NegativeInfinity;

        public Wand(IotPlayerController owner, int id)
        {
            _owner = owner;
            _id = id;
        }

        /// <summary>이 완드에서 줄이 최근에 들어오고 있는지.</summary>
        public bool Connected { get; private set; }

        /// <summary>완드가 보내온 millis() 하위 16비트. **로직에 쓰지 않는다.** 보관만 한다.</summary>
        public int LastMilliseconds { get; private set; }

        public Vector2 Stick => _stick;

        /// <summary>
        /// IMU roll. 마주 잡기로 배정된 손이면 부호를 뒤집어 내놓는다.
        ///
        /// 원값(<c>_tilt</c>)은 그대로 두고 **내놓을 때만** 뒤집는다.
        /// 손 배정이 바뀌어도 받아둔 값을 다시 계산할 필요가 없다.
        /// </summary>
        public float Tilt => _mirrorTilt ? -_tilt : _tilt;

        public float Rotation => _rotation;

        public bool Button1 => _button1Held;

        /// <summary>
        /// 버튼 2. 왼손이면 **토글 상태**, 오른손이면 누르고 있는 그대로.
        ///
        /// 토글이어도 <see cref="ConsumeButton2Press"/> 는 실제로 누른 순간을 그대로 돌려줍니다.
        /// 발사 · 망치질이 그것을 봅니다.
        /// </summary>
        public bool Button2 => _useSprintToggle ? _sprintOn : _button2Held;

        /// <summary>줄 하나를 받아 상태를 갱신한다.</summary>
        internal void Apply(in Packet packet, float now)
        {
            Vector2 stick = new Vector2(packet.X / 127f, packet.Y / 127f);

            // 대각선이 더 빨라지지 않도록 길이를 1 로 자른다. 축마다 자르면 모서리가 길어진다.
            _stick = stick.sqrMagnitude > 1f ? stick.normalized : stick;

            _tilt = Mathf.Clamp(packet.Tilt / 127f, -1f, 1f);
            _rotation = Mathf.Clamp(packet.Rotation / 127f, -1f, 1f);

            ApplyButtons(packet.Buttons);
            ApplyMotions(in packet);

            LastMilliseconds = packet.Milliseconds;
            _lastSeenTime = now;
        }

        /// <summary>
        /// 버튼은 **누르고 있는 상태**와 **새로 눌린 순간**을 둘 다 만든다.
        ///
        /// Button1 · Button2 는 누르는 동안 계속 참이어야 하고,
        /// Consume 계열은 누름의 시작에서 한 번만 참이어야 한다. (IOT_INPUT.md 3장)
        ///
        /// 달리기 토글은 **누른 순간에만** 뒤집힌다. 내놓을지 말지는
        /// <see cref="Button2"/> 가 손 배정을 보고 정한다.
        /// </summary>
        private void ApplyButtons(int buttons)
        {
            bool button1 = (buttons & 0x1) != 0;
            bool button2 = (buttons & 0x2) != 0;

            if (button1 && !_button1Held)
            {
                _button1Pressed = true;
            }

            if (button2 && !_button2Held)
            {
                _button2Pressed = true;
                _sprintOn = !_sprintOn;
            }

            _button1Held = button1;
            _button2Held = button2;
        }

        /// <summary>
        /// 동작 카운터가 **늘어난 만큼**을 세어 대기열에 쌓는다.
        ///
        /// 값 자체는 뜻이 없다. 0~255 에서 한 바퀴 돌기 때문에 뺄셈을 그대로 쓰면
        /// 255 → 0 일 때 음수가 나온다. 256 으로 감싸서 항상 양수로 만든다.
        ///
        /// 완드가 다시 켜지면 카운터가 0 부터 다시 시작한다. 그것을 그대로 받으면
        /// 재부팅 한 번에 동작이 수백 번 터진다. 두 가지로 막는다.
        ///
        /// <code>
        /// 카운터가 뒤로 감  감싼 값이 절반(128)을 넘는다
        /// 시각이 뒤로 감    감싼 값이 절반(32768)을 넘는다
        /// </code>
        ///
        /// **둘 중 하나라도 걸리면 그 줄은 기준만 새로 잡고 넘어간다.**
        /// 카운터만 보면 200 → 0 처럼 감싼 값이 작게 나오는 재부팅을 놓치는데,
        /// 그때는 시각 쪽이 잡아준다. 255 → 0 같은 진짜 순환은 둘 다 걸리지 않는다.
        ///
        /// 첫 줄에서도 기준만 잡고 넘어간다. 안 그러면 게임을 켜자마자
        /// 완드가 그동안 쌓아둔 횟수가 한꺼번에 휘두른 것이 된다.
        /// </summary>
        private void ApplyMotions(in Packet packet)
        {
            if (!_hasCounter)
            {
                _lastCounter = packet.MotionCount;
                _previousMilliseconds = packet.Milliseconds;
                _hasCounter = true;
                return;
            }

            int delta = (packet.MotionCount - _lastCounter + CounterWrap) % CounterWrap;
            _lastCounter = packet.MotionCount;

            // 시각도 16비트라 약 65초마다 정상적으로 0 으로 돌아온다.
            // 카운터와 같은 방식으로 감싸서, 뒤로 간 폭이 절반을 넘을 때만 재부팅으로 본다.
            int elapsed = (packet.Milliseconds - _previousMilliseconds + MillisecondsWrap) % MillisecondsWrap;
            _previousMilliseconds = packet.Milliseconds;

            bool rebooted = elapsed > MillisecondsWrap / 2;

            if (rebooted || delta == 0 || delta > CounterWrap / 2)
            {
                return;
            }

            HandMotionType type = ToMotionType(packet.MotionType);

            // 종류를 모르는 동작은 게임이 쓸 수 없다. 자리만 차지하므로 넣지 않는다.
            if (type == HandMotionType.None)
            {
                _owner.ReportUnknownMotion(_id, packet.MotionType);
                return;
            }

            HandMotion motion = new HandMotion(type, packet.Strength / 255f);

            // 대기열보다 많이 밀려도 결과는 같다. 헛돌지 않게 여기서 자른다.
            int repeat = Mathf.Min(delta, MaxPendingMotions);

            for (int i = 0; i < repeat; i++)
            {
                EnqueueMotion(motion);
            }

            _owner.ReportMotion(_id, motion, repeat);
        }

        /// <summary>펌웨어가 보낸 숫자를 동작 종류로 바꾼다. 아는 값이 아니면 None 이다.</summary>
        private static HandMotionType ToMotionType(int raw)
        {
            switch (raw)
            {
                case 1: return HandMotionType.HorizontalSwing;
                case 2: return HandMotionType.VerticalSwing;
                case 3: return HandMotionType.Thrust;
                default: return HandMotionType.None;
            }
        }

        /// <summary>가득 차 있으면 가장 오래된 것을 밀어내고 넣는다.</summary>
        private void EnqueueMotion(HandMotion motion)
        {
            int tail = (_motionHead + _motionCount) % MaxPendingMotions;
            _motions[tail] = motion;

            if (_motionCount < MaxPendingMotions)
            {
                _motionCount++;
            }
            else
            {
                _motionHead = (_motionHead + 1) % MaxPendingMotions;
            }
        }

        /// <summary>
        /// 끊김을 시간으로 판정한다. 상태가 바뀌었으면 true.
        ///
        /// 줄 한두 개가 유실돼도 끊긴 것으로 보지 않는다. 그랬다가는
        /// HasTwoDevices 가 깜빡이면서 카메라가 이동 스틱에 붙었다 떨어졌다 한다.
        /// </summary>
        internal bool Refresh(float now, float timeoutSeconds)
        {
            bool connected = now - _lastSeenTime <= timeoutSeconds;

            if (connected == Connected)
            {
                return false;
            }

            Connected = connected;

            if (!connected)
            {
                Clear();
            }

            return true;
        }

        /// <summary>
        /// 이 완드를 달리기 토글로 쓸지 정한다. 왼손일 때만 켜진다.
        ///
        /// 역할이 바뀌면 켜둔 상태를 들고 가지 않는다. 오른손이 된 완드가
        /// 달리던 상태를 그대로 들고 있으면 발사 버튼이 눌린 채로 보인다.
        /// </summary>
        internal void SetSprintToggle(bool enabled)
        {
            if (_useSprintToggle == enabled)
            {
                return;
            }

            _useSprintToggle = enabled;
            _sprintOn = false;
        }

        /// <summary>
        /// 이 완드의 Tilt 를 뒤집어 내놓을지 정한다. 마주 잡기의 왼손일 때만 켜진다.
        ///
        /// 상태가 아니라 해석 방식이라 <see cref="Clear"/> 가 건드리지 않는다.
        /// 끊겼다 다시 붙어도 잡는 방식은 그대로다.
        /// </summary>
        internal void SetTiltMirrored(bool enabled)
        {
            _mirrorTilt = enabled;
        }

        /// <summary>
        /// 값을 전부 0 · false 로 되돌린다.
        ///
        /// 끊겼을 때와 포트를 닫을 때 부른다. 쌓아둔 동작과 달리기 토글도 함께 내린다.
        /// 완드를 껐는데 계속 달리고 있으면 안 되고,
        /// 다시 연결됐을 때 묵은 입력이 한꺼번에 터져도 안 된다.
        /// </summary>
        internal void Clear()
        {
            _stick = Vector2.zero;
            _tilt = 0f;
            _rotation = 0f;

            _button1Held = false;
            _button2Held = false;
            _button1Pressed = false;
            _button2Pressed = false;

            _sprintOn = false;

            _motionHead = 0;
            _motionCount = 0;

            // 다시 붙었을 때 첫 줄로 기준을 새로 잡는다. 시각도 함께 비운다.
            _hasCounter = false;
            _lastCounter = 0;
            _previousMilliseconds = 0;
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

        /// <summary>
        /// 대기 중인 동작 하나를 가져가면서 지운다.
        ///
        /// ⚠ **읽으면 사라집니다.** 한 프레임에 두 곳에서 부르면 한쪽이 못 받습니다.
        ///    받는 쪽은 한 곳이어야 합니다. (IOT_INPUT.md 3장)
        /// </summary>
        public bool TryConsumeMotion(out HandMotion motion)
        {
            if (_motionCount <= 0)
            {
                motion = default;
                return false;
            }

            motion = _motions[_motionHead];
            _motionHead = (_motionHead + 1) % MaxPendingMotions;
            _motionCount--;
            return true;
        }

        public void Vibrate(float strength, float seconds)
        {
            _owner.SendVibrate(_id, Mathf.Clamp01(strength), Mathf.Max(0f, seconds));
        }
    }

    // ------------------------------------------------------------
    // 줄 하나를 담는 것
    // ------------------------------------------------------------

    /// <summary>시리얼 줄 하나를 숫자로 풀어놓은 것. 아직 정규화 전의 원값이다.</summary>
    internal readonly struct Packet
    {
        public readonly int Id;
        public readonly int X;
        public readonly int Y;
        public readonly int Buttons;
        public readonly int Tilt;
        public readonly int Rotation;
        public readonly int MotionCount;
        public readonly int MotionType;
        public readonly int Strength;
        public readonly int Milliseconds;

        public Packet(
            int id, int x, int y, int buttons, int tilt, int rotation,
            int motionCount, int motionType, int strength, int milliseconds)
        {
            Id = id;
            X = x;
            Y = y;
            Buttons = buttons;
            Tilt = tilt;
            Rotation = rotation;
            MotionCount = motionCount;
            MotionType = motionType;
            Strength = strength;
            Milliseconds = milliseconds;
        }
    }
}
