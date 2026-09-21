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
///   그 밖(에디터 · 클라이언트 빌드) → GameMode.Client   이미 떠 있는 서버에 붙는다
///
/// <b>혼자 Host 가 되는 길은 없다.</b> 예전에는 에디터에서 Play 하면 AutoHostOrClient 로 떠서
/// 서버 없이도 혼자 로비에 들어가졌다. 그 흐름은 두 가지 문제를 만들었다.
///   · 에디터에서 "되는" 것이 Dedicated Server 에서도 되는지 알 수 없었다
///   · 서버를 안 띄운 줄 모르고 혼자 놀다가 뒤늦게 발견했다
/// 그래서 **플레이 모드 테스트도 Dedicated Server 에 붙는 것으로 통일**했다.
/// 서버가 없으면 혼자 Host 를 만들지 않고 접속 실패를 화면에 알린다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-1)
/// </summary>
[RequireComponent(typeof(NetworkRunner))]
public class FusionLauncher : MonoBehaviour
{
    public enum LaunchMode
    {
        /// <summary>빌드 종류를 보고 고른다. 서버 빌드면 Server, 그 밖은 Client.</summary>
        AutoDetect,

        /// <summary>전용 서버. 자신은 플레이어가 되지 않는다.</summary>
        Server,

        /// <summary>이미 떠 있는 서버에 붙는다. 혼자서는 세션을 만들지 않는다.</summary>
        Client

        // ⚠ AutoHostOrClient 는 일부러 없앴다. 값이 남아 있으면 Inspector 에서 고를 수 있고,
        //    한 번 고르면 서버 없이도 화면이 떠 버려 "왜 서버에서는 안 되지" 로 이어진다.
        //    전용 서버 구조를 쓰기로 한 이상 혼자 Host 가 되는 선택지를 두지 않는다.
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

        // ⚠ 이 프로세스에서 이미 다른 Runner 가 돌고 있으면 내가 시작하지 않는다.
        //    정상 경로에서는 ChannelSelect 가 FusionNetworkService 로 먼저 접속하고,
        //    Fusion 이 그 세션으로 이 Lobby 씬을 올린다. 여기서 또 StartGame 을 부르면
        //    세션이 둘이 된다.
        NetworkRunner existing = FindRunningRunner();
        if (existing != null)
        {
            Debug.Log(
                $"[Fusion] 이미 돌고 있는 Runner 가 있어 이 씬에서는 세션을 시작하지 않습니다. " +
                $"(세션 \"{existing.SessionInfo.Name}\")");

            // 씬에 있는 PlayerSpawner 를 그 Runner 에 붙여 준다.
            // 스폰 포인트 배선은 이 씬에 있으므로 Spawner 를 옮길 수 없다.
            AttachSpawnerTo(existing);
            return;
        }

        NetworkRunner runner = GetComponent<NetworkRunner>();

        // 커맨드라인이 Inspector 값을 이긴다. 서버는 창이 없어 Inspector 로 바꿀 수 없기 때문이다.
        LaunchMode resolvedMode = ResolveMode();
        string resolvedSession = FusionLaunchArguments.GetString(FusionLaunchArguments.SessionKey, sessionName);
        ushort resolvedPort = FusionLaunchArguments.GetPort(FusionLaunchArguments.PortKey, serverPort);

        bool isDedicatedServer = resolvedMode == LaunchMode.Server;

        if (IsStandaloneSceneLoad())
        {
            // 정상 경로도 개발용 경로도 아닌데 Lobby 만 열렸다. 검은 화면에 갇히지 않게 알린다.
            Debug.LogError(
                "[Fusion] 세션 없이 Lobby 씬만 열렸습니다. " +
                "정상 경로는 ChannelSelect 에서 채널을 골라 접속하는 것입니다. " +
                "에디터에서는 Lobby 를 그냥 Play 하면 개발용 Client 로 접속됩니다. " +
                "개발 빌드에서 바로 들어오려면 -devjoin 을 주세요. (Development Build 여야 합니다)");

            TransitionStatus.SetFailed(
                "Lobby 에 바로 들어올 수 없습니다.\nChannelSelect 에서 채널을 골라 주세요.");
            return;
        }

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
            SceneManager = GetComponent<NetworkSceneManagerDefault>(),

            // 인자가 없으면 null 이고, Fusion 은 null 을 공용 설정으로 읽는다.
            CustomPhotonAppSettings = FusionSessionIsolation.PhotonSettings
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
                TransitionStatus.SetFailed(DescribeClientFailure(result.ShutdownReason, resolvedSession));
            }

            return;
        }

        if (isDedicatedServer)
        {
            Debug.Log(
                $"[Fusion] Dedicated Server 준비 완료. 세션 \"{resolvedSession}\", 포트 {resolvedPort}. " +
                "클라이언트를 기다립니다.");

            // 미니게임 서버들이 몇 명을 데리고 있는지 지켜본다. 매칭이 빈 방을 고를 때 쓴다.
            //
            // ⚠ 러너를 하나 더 쓴다. 지금 이 러너는 방(lobby-ch1) 안에 들어가 있어서
            //    세션 목록을 받지 못한다 — Photon 피어는 로비에 있거나 방에 있거나 둘 중 하나다.
            DsPoolWatcher.Begin();
        }
        else
        {
            Debug.Log($"[Fusion] '{resolvedSession}' 세션 접속 성공 ({resolvedMode})");
        }
    }

    /// <summary>
    /// 접속 실패 사유를 사람이 읽을 문장으로 바꾼다.
    ///
    /// 이 경로(에디터 Play · -devjoin 개발 빌드)는 **Dedicated Server 가 떠 있어야만** 성공한다.
    /// 혼자 Host 가 되는 길을 없앴으므로, 서버를 안 띄웠으면 반드시 여기로 온다.
    /// 그래서 가장 흔한 원인을 문장 맨 앞에 둔다.
    ///
    /// 정상 경로(ChannelSelect → 채널 선택)의 문구는 FusionNetworkService 가 따로 만든다.
    /// 그쪽은 채널 이름을 알고 있어 "서버 1 이(가) 열려 있지 않습니다" 처럼 쓸 수 있다.
    /// </summary>
    private static string DescribeClientFailure(ShutdownReason reason, string session)
    {
        switch (reason)
        {
            case ShutdownReason.GameNotFound:
                return $"Dedicated Server 를 먼저 실행하세요.\n\"{session}\" 세션이 열려 있지 않습니다.";

            case ShutdownReason.ConnectionTimeout:
            case ShutdownReason.ConnectionRefused:
                return $"Dedicated Server 를 먼저 실행하세요.\n\"{session}\" 세션에 연결하지 못했습니다. ({reason})";

            case ShutdownReason.GameIsFull:
                return $"\"{session}\" 세션이 가득 찼습니다.";

            default:
                return $"Lobby 접속에 실패했습니다. ({reason})\nDedicated Server 가 실행 중인지 확인해 주세요.";
        }
    }

    /// <summary>
    /// 이 프로세스에서 이미 돌고 있는 Runner. 내 것은 빼고 찾는다.
    ///
    /// 정상 경로(ChannelSelect → Fusion 접속 → Lobby)에서는 이 Runner 가 이미 있다.
    /// 개발용 직접 실행과 서버 빌드에서는 없다.
    /// </summary>
    private NetworkRunner FindRunningRunner()
    {
        NetworkRunner mine = GetComponent<NetworkRunner>();

        foreach (NetworkRunner candidate in NetworkRunner.Instances)
        {
            if (candidate == null || candidate == mine)
            {
                continue;
            }

            if (candidate.IsRunning)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 이 씬의 <see cref="PlayerSpawner"/> 를 남의 Runner 에 등록한다.
    ///
    /// ⚠ Fusion 은 씬을 로드할 때 <b>NetworkObject 만</b> 등록한다.
    ///    (NetworkSceneManagerDefault.RegisterSceneObjects)
    ///    PlayerSpawner 는 NetworkObject 가 없는 SimulationBehaviour 라
    ///    씬에 있다는 것만으로는 PlayerJoined 를 받지 못한다. 조용히 아무 일도 안 일어난다.
    ///
    ///    개발용 경로에서는 Spawner 가 Runner 와 같은 오브젝트에 있어 StartGame 이 알아서
    ///    수집한다. 그때는 이 메서드가 불리지 않으므로 이중 등록이 생기지 않는다.
    /// </summary>
    private void AttachSpawnerTo(NetworkRunner target)
    {
        PlayerSpawner spawner = GetComponent<PlayerSpawner>();

        if (spawner == null)
        {
            Debug.LogWarning("[Fusion] 이 씬에 PlayerSpawner 가 없어 등록하지 못했습니다.");
            return;
        }

        if (spawner.Runner != null)
        {
            // 이미 어딘가에 등록돼 있다. 두 번 등록하면 플레이어가 두 번 스폰된다.
            Debug.Log("[Fusion] PlayerSpawner 가 이미 등록돼 있어 건너뜁니다.");
            return;
        }

        target.AddGlobal(spawner);
        Debug.Log($"[Fusion] PlayerSpawner 를 실행 중인 Runner 에 등록했습니다. (세션 \"{target.SessionInfo.Name}\")");
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
                $"쓸 수 있는 값: Server, Client. Inspector 설정을 씁니다.");
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
        // 에디터 · 개발 빌드 · 일반 클라이언트 빌드 전부 Client 다.
        // 혼자 Host 가 되는 분기는 없다. 서버가 없으면 접속에 실패하고 그 사유를 화면에 알린다.
        //
        // 에디터에서 Play 하는 것도 여기로 온다. 즉 **플레이 모드 테스트에는
        // Dedicated Server 가 떠 있어야 한다.** 씬을 편집만 할 때는 Play 할 필요가 없다.
        return LaunchMode.Client;
#endif
    }

    /// <summary>
    /// 세션 없이 Lobby 씬만 열린 상황인가.
    ///
    /// 정상 경로는 ChannelSelect 에서 접속한 뒤 Fusion 이 이 씬을 올린다.
    /// 개발용 경로는 이 컴포넌트가 직접 세션을 시작한다.
    /// 둘 다 아닌데 Lobby 가 열렸다면 누군가 <c>SceneManager.LoadScene("Lobby")</c> 만 부른 것이다.
    /// 그대로 두면 캐릭터도 없고 아무 일도 일어나지 않는 화면이 되므로 이유를 알린다.
    /// </summary>
    private static bool IsStandaloneSceneLoad()
    {
#if UNITY_SERVER
        // 서버 빌드는 언제나 자기가 세션을 연다.
        return false;
#elif UNITY_EDITOR
        // 에디터에서 Lobby 를 Play 하는 것은 **개발용 Client 접속**으로 본다. 막지 않는다.
        // 서버가 없으면 여기서 막는 대신, 접속을 시도하고 실패 사유를 화면에 알린다.
        // (그래야 "서버를 안 띄웠다" 는 사실이 바로 드러난다)
        return false;
#else
        // 빌드에서는 **의도를 밝힌 경우만** 직접 진입을 허용한다.
        //   -devjoin        개발용 직접 접속 (Development Build)
        //   -mode client    개발용 QA 실행
        //   -mode server    서버
        // 셋 다 없는데 Lobby 가 열렸다면 제품 경로에서 LoadScene("Lobby") 만 부른 것이다.
        if (FusionDevEntry.WantsClientJoin)
        {
            return false;
        }

        string explicitMode = FusionLaunchArguments.GetString(FusionLaunchArguments.ModeKey, null);
        return string.IsNullOrEmpty(explicitMode);
#endif
    }

    private static GameMode ToGameMode(LaunchMode value)
    {
        switch (value)
        {
            case LaunchMode.Server:
                return GameMode.Server;
            // AutoDetect 는 여기 오기 전에 DetectFromBuild 가 Server/Client 로 바꿔 놓는다.
            // 그래도 남는 경우를 대비해 Client 로 떨어뜨린다. 절대 Host 를 만들지 않는다.
            default:
                return GameMode.Client;
        }
    }
}
