using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 이 씬에서 Fusion 세션을 시작한다.
///
/// 같은 컴포넌트가 **Dedicated Server 와 클라이언트 양쪽에서** 쓰인다.
/// 어느 쪽으로 뜰지는 <see cref="mode"/> 가 정한다. 기본값 <see cref="LaunchMode.AutoDetect"/> 는
/// 빌드 종류를 보고 스스로 고른다.
///
///   서버 빌드(Dedicated Server)  → GameMode.Server   플레이어를 스폰만 하고 자신은 참가하지 않는다
///   클라이언트 빌드              → GameMode.Client   이미 떠 있는 서버에 붙는다
///   에디터                       → AutoHostOrClient  혼자 테스트하던 기존 흐름 그대로
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-1)
/// </summary>
[RequireComponent(typeof(NetworkRunner))]
public class FusionLauncher : MonoBehaviour
{
    public enum LaunchMode
    {
        /// <summary>빌드 종류를 보고 고른다. 서버 빌드면 Server, 아니면 AutoHostOrClient.</summary>
        AutoDetect,

        /// <summary>전용 서버. 자신은 플레이어가 되지 않는다.</summary>
        Server,

        /// <summary>이미 떠 있는 서버에 붙는다. 혼자서는 세션을 만들지 않는다.</summary>
        Client,

        /// <summary>먼저 켠 쪽이 Host 가 된다. **전용 서버 구조가 아니다.** 혼자 테스트할 때만 쓴다.</summary>
        AutoHostOrClient
    }

    [Header("기동 모드")]
    [Tooltip("AutoDetect 를 권한다. 커맨드라인 -mode 로 덮어쓸 수 있다.")]
    [SerializeField] private LaunchMode mode = LaunchMode.AutoDetect;

    [Header("세션")]
    [Tooltip("붙을 방 이름. 커맨드라인 -session 이 있으면 그쪽이 이긴다.")]
    [SerializeField] private string sessionName = "AraAtti-Test";

    [Header("서버 전용")]
    [Tooltip("Server 모드에서 열 포트. 커맨드라인 -port 가 있으면 그쪽이 이긴다.")]
    [SerializeField] private ushort serverPort = 27015;

    /// <summary>
    /// 이 프로세스에서 세션을 실제로 시작한 인스턴스.
    ///
    /// ⚠ 아래 <see cref="StartGameArgs.Scene"/> 이 **지금 씬**을 가리키므로,
    ///    Fusion 은 세션을 시작한 직후 그 씬을 다시 로드한다.
    ///    그러면 씬 데이터에서 NetworkManager 오브젝트의 **새 복사본**이 하나 더 만들어진다.
    ///    먼저 뜬 Runner 는 Fusion 전용 씬으로 옮겨져 살아 있으므로,
    ///    복사본이 같은 세션 이름으로 StartGame 을 또 부르면
    ///    GameIdAlreadyExists(32766) 로 실패한다. 실제 서버 로그로 확인했다.
    ///
    ///    복사본에는 PlayerSpawner 도 같이 딸려 오므로 그냥 두면 플레이어가 두 번 스폰될 수 있다.
    ///    그래서 비활성화가 아니라 오브젝트째 지운다.
    /// </summary>
    private static FusionLauncher active;

    /// <summary>이 인스턴스가 실제로 StartGame 을 부른 Runner. 아직 안 불렀으면 null.</summary>
    private NetworkRunner startedRunner;

    /// <summary>
    /// 아직 세션을 쥐고 있는가.
    ///
    /// 가드를 "인스턴스가 살아 있는가"가 아니라 "세션이 살아 있는가"로 판단한다.
    /// 그래야 세션이 끝난 뒤 같은 프로세스에서 다시 기동할 때 가드가 스스로 풀린다.
    /// StartGame 이 실패해도 Runner 가 Shutdown 상태가 되므로 마찬가지로 풀린다.
    /// </summary>
    private bool StillHoldsSession => startedRunner != null && !startedRunner.IsShutdown;

    private void Awake()
    {
        // 파괴된 오브젝트는 UnityEngine.Object 의 == 가 null 로 봐 준다.
        if (active != null && active != this && active.StillHoldsSession)
        {
            Debug.Log("[Fusion] 이미 세션을 시작했습니다. 씬 재로드로 생긴 NetworkManager 복사본을 정리합니다.");
            Destroy(gameObject);
            return;
        }

        active = this;
    }

    private void OnDestroy()
    {
        // 세션이 끝나 원본이 사라지면 다음 기동을 막지 않도록 자리를 비워 준다.
        if (active == this)
        {
            active = null;
        }
    }

    private async void Start()
    {
        // Awake 에서 Destroy 한 오브젝트에도 같은 프레임에 Start 가 불릴 수 있다.
        if (active != this)
        {
            return;
        }

        NetworkRunner runner = GetComponent<NetworkRunner>();

        // 커맨드라인이 Inspector 값을 이긴다. 서버는 창이 없어 Inspector 로 바꿀 수 없기 때문이다.
        LaunchMode resolvedMode = ResolveMode();
        string resolvedSession = FusionLaunchArguments.GetString(FusionLaunchArguments.SessionKey, sessionName);
        ushort resolvedPort = FusionLaunchArguments.GetPort(FusionLaunchArguments.PortKey, serverPort);

        bool isDedicatedServer = resolvedMode == LaunchMode.Server;

        Debug.Log(
            $"[Fusion] 기동 준비 — 모드 {resolvedMode}, 세션 \"{resolvedSession}\"" +
            (isDedicatedServer ? $", 포트 {resolvedPort}" : string.Empty) +
            $" / 실행 인자: {FusionLaunchArguments.Describe()}");

        // 사람이 보는 쪽에서는 여기서부터 화면을 가린다.
        // 접속이 끝나도 내 캐릭터가 스폰되고 카메라가 자리를 잡기 전까지는 보여 줄 화면이 아니다.
        // 가림막을 실제로 어떻게 그릴지는 UI 쪽이 정한다. (TransitionStatus 주석 참고)
        if (!isDedicatedServer)
        {
            TransitionStatus.SetLoading("Lobby에 접속 중...");
        }

        // ⚠ 전용 서버는 입력을 만들지 않는다. 서버에는 조작하는 사람이 없다.
        //    ProvideInput 을 켜두면 서버가 자기 입력을 보내려 해서 불필요한 일이 생긴다.
        runner.ProvideInput = !isDedicatedServer;

        if (!isDedicatedServer)
        {
            // 로컬 입력 제공자를 등록한다. 컴포넌트가 없으면 자동으로 붙여서 Inspector 작업을 줄인다.
            PlayerInputProvider inputProvider = GetComponent<PlayerInputProvider>();
            if (inputProvider == null)
            {
                inputProvider = gameObject.AddComponent<PlayerInputProvider>();
            }

            runner.AddCallbacks(inputProvider);
        }

        // ⚠ 씬을 **경로**로 가리킨다. 인덱스로 가리키면 안 된다.
        //    서버 빌드와 클라이언트 빌드는 Scene List 가 서로 달라 같은 씬의 인덱스가 다르다.
        //    (서버 프로필은 ServerTestScene 하나, 클라이언트는 6개 씬)
        //    Fusion 의 NetworkSceneManagerDefault 는 경로 기반 SceneRef 를 받으면
        //    **각 피어가 자기 Build Settings 에서 같은 경로를 찾아** 자기 인덱스로 바꾼다.
        //    (NetworkSceneManagerDefault.LoadSceneCoroutine — sceneRef.IsPath 비교)
        Scene activeScene = SceneManager.GetActiveScene();
        string scenePath = activeScene.path;

        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogError(
                $"[Fusion] '{activeScene.name}' 씬의 경로를 얻지 못했습니다. " +
                "저장되지 않은 씬에서는 접속할 수 없습니다.");
            return;
        }

        if (activeScene.buildIndex < 0)
        {
            // 경로로 넘기더라도 각 피어의 Build Settings 에 그 경로가 있어야 로드된다.
            Debug.LogError(
                $"[Fusion] '{scenePath}' 씬이 이 빌드의 Scene List 에 없습니다. " +
                "Build Profile 의 Scene List 를 확인해 주세요.");
            return;
        }

        StartGameArgs args = new StartGameArgs
        {
            GameMode = ToGameMode(resolvedMode),
            SessionName = resolvedSession,
            Scene = SceneRef.FromPath(scenePath),
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        };

        if (isDedicatedServer)
        {
            // 서버는 들어오는 연결을 받을 주소를 직접 연다.
            // 공식 샘플과 같은 방식이다. (Photon/Fusion/Runtime/FusionBootstrap.cs)
            args.Address = NetAddress.Any(resolvedPort);
        }

        // 씬 재로드는 StartGame 안에서 일어난다. 복사본의 Awake 가 돌기 전에 표시해 둬야 한다.
        startedRunner = runner;

        StartGameResult result = await runner.StartGame(args);

        if (!result.Ok)
        {
            Debug.LogError($"[Fusion] 접속 실패: {result.ShutdownReason} — {result.ErrorMessage}");

            // 화면이 검은 채로 멈추지 않게 사유를 알린다. (서버가 안 떠 있을 때가 대표적이다)
            if (!isDedicatedServer)
            {
                TransitionStatus.SetFailed(
                    $"Lobby 접속에 실패했습니다.\n{result.ShutdownReason}");
            }

            return;
        }

        if (isDedicatedServer)
        {
            Debug.Log(
                $"[Fusion] Dedicated Server 준비 완료. 세션 \"{resolvedSession}\", 포트 {resolvedPort}. " +
                "클라이언트를 기다립니다.");
        }
        else
        {
            Debug.Log($"[Fusion] '{resolvedSession}' 세션 접속 성공 ({resolvedMode})");
        }
    }

    /// <summary>
    /// 실제로 쓸 모드를 정한다. 우선순위: 커맨드라인 &gt; Inspector &gt; 빌드 종류.
    /// </summary>
    private LaunchMode ResolveMode()
    {
        string raw = FusionLaunchArguments.GetString(FusionLaunchArguments.ModeKey, null);

        if (!string.IsNullOrEmpty(raw))
        {
            if (System.Enum.TryParse(raw, ignoreCase: true, out LaunchMode fromArgs))
            {
                return fromArgs == LaunchMode.AutoDetect ? DetectFromBuild() : fromArgs;
            }

            Debug.LogWarning(
                $"[Fusion] -mode \"{raw}\" 를 알아듣지 못했습니다. " +
                $"쓸 수 있는 값: Server, Client, AutoHostOrClient. Inspector 설정을 씁니다.");
        }

        return mode == LaunchMode.AutoDetect ? DetectFromBuild() : mode;
    }

    /// <summary>
    /// 빌드 종류로 모드를 고른다.
    ///
    /// UNITY_SERVER 는 Build Profile 의 타깃이 Dedicated Server 일 때 Unity 가 자동으로 붙이는 정의다.
    /// 그래서 서버 프로필로 빌드하면 별도 설정 없이 Server 로 뜬다.
    /// </summary>
    private static LaunchMode DetectFromBuild()
    {
#if UNITY_SERVER
        return LaunchMode.Server;
#else
        // 개발용 직접 접속(PRD 08-2). 에디터 메뉴나 -devjoin 으로 켠다.
        // 혼자 호스트가 되어 버리지 않고 이미 떠 있는 Dedicated Server 에 붙는다.
        // Release 빌드에서는 FusionDevEntry 가 항상 false 라 이 분기를 타지 않는다.
        if (FusionDevEntry.WantsClientJoin)
        {
            return LaunchMode.Client;
        }

        // 에디터와 일반 클라이언트 빌드.
        // 기존처럼 혼자 켜서 테스트하던 흐름을 깨지 않으려고 AutoHostOrClient 로 둔다.
        // 전용 서버에 붙여 확인하려면 Inspector 를 Client 로 바꾸거나 -mode client 를 준다.
        return LaunchMode.AutoHostOrClient;
#endif
    }

    private static GameMode ToGameMode(LaunchMode value)
    {
        switch (value)
        {
            case LaunchMode.Server:
                return GameMode.Server;
            case LaunchMode.Client:
                return GameMode.Client;
            default:
                return GameMode.AutoHostOrClient;
        }
    }
}
