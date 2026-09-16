using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net
{
    /// <summary>
    /// Warriors 네트워크 씬에서 Fusion 세션을 시작한다.
    ///
    /// <b>모드</b>
    /// <code>
    ///   Dedicated Server 빌드 (UNITY_SERVER) 또는 -mode server  ->  GameMode.Server
    ///   그 밖 (에디터 . 클라이언트 빌드)                          ->  GameMode.Client
    /// </code>
    ///
    /// ⚠ <b>혼자 Host 가 되는 길은 없다.</b> 서버가 안 떠 있으면 접속에 실패하고
    ///    그 사유를 로그에 남긴다.
    ///
    /// ⚠ <c>PeerMode.Multiple</c> 이라 클라이언트도 <c>StartGameArgs.Scene</c> 을 줘야 한다.
    ///    빼면 세션은 붙고 플레이어도 스폰되는데 <b>씬이 로드되지 않고 오류도 없다.</b>
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class WarriorsLauncher : MonoBehaviour
    {
        [Header("서버 전용")]
        [Tooltip("Server 모드에서 열 포트. 실행 인자 -port 가 있으면 그쪽이 이긴다. "
                 + "Lobby(27015) · ShipCoop 과 겹치지 않게 Warriors 는 27031 을 쓴다. "
                 + "한 PC 에서 여러 미니게임 서버를 같이 띄울 수 있어야 한다.")]
        [SerializeField] private ushort serverPort = 27031;

        /// <summary>이 프로세스에서 세션을 시작한 인스턴스. 씬 재로드로 생긴 복사본을 막는다.</summary>
        private static WarriorsLauncher active;

        private NetworkRunner startedRunner;

        private bool StillHoldsSession => startedRunner != null && !startedRunner.IsShutdown;

        private void Awake()
        {
            if (active != null && active != this && active.StillHoldsSession)
            {
                Debug.Log("[Warriors] 이미 세션을 시작했습니다. 씬 재로드로 생긴 복사본을 정리합니다.");
                Destroy(gameObject);
                return;
            }

            active = this;
        }

        private void OnDestroy()
        {
            if (active == this) active = null;
        }

        private async void Start()
        {
            if (active != this) return;

            if (FindRunningRunner() != null)
            {
                Debug.Log("[Warriors] 이미 돌고 있는 Runner 가 있어 이 씬에서는 세션을 시작하지 않습니다.");
                return;
            }

            NetworkRunner runner = GetComponent<NetworkRunner>();

            bool isServer = FusionLaunchArguments.IsDedicatedServerProcess();
            string session = WarriorsNet.ResolveSession();
            ushort port = FusionLaunchArguments.GetPort(FusionLaunchArguments.PortKey, serverPort);

            Debug.Log(
                $"[Warriors] 기동 준비 - 모드 {(isServer ? "Server" : "Client")}, 세션 \"{session}\"" +
                (isServer ? $", 포트 {port}" : string.Empty) +
                $" / 실행 인자: {FusionLaunchArguments.Describe()}");

            // 서버에는 조작하는 사람이 없다. 입력을 만들지 않는다.
            runner.ProvideInput = !isServer;

            if (!isServer)
            {
                WarriorsInputProvider provider = GetComponent<WarriorsInputProvider>();
                if (provider == null) provider = gameObject.AddComponent<WarriorsInputProvider>();
                runner.AddCallbacks(provider);
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.buildIndex < 0)
            {
                Debug.LogError(
                    $"[Warriors] '{activeScene.path}' 씬이 이 빌드의 Scene List 에 없습니다. " +
                    "Build Profile 의 Scene List 를 확인해 주세요.");
                return;
            }

            StartGameArgs args = new StartGameArgs
            {
                GameMode = isServer ? GameMode.Server : GameMode.Client,
                SessionName = session,
                Scene = SceneRef.FromPath(WarriorsNet.ScenePath),
                SceneManager = GetComponent<NetworkSceneManagerDefault>()
            };

            if (isServer) args.Address = NetAddress.Any(port);

            startedRunner = runner;

            StartGameResult result = await runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[Warriors] 접속 실패: {result.ShutdownReason} - {result.ErrorMessage}");

                if (!isServer)
                {
                    Debug.LogError(Describe(result.ShutdownReason, session));
                    return;
                }

                await RetryOrQuitServer(result, session, activeScene);
                return;
            }

            serverAttempts = 0;

            Debug.Log(isServer
                ? $"[Warriors] Dedicated Server 준비 완료. 세션 \"{session}\", 포트 {port}. 클라이언트를 기다립니다."
                : $"[Warriors] '{session}' 세션 접속 성공 (Client)");
        }

        // ------------------------------------------------------------
        // 서버가 못 떴을 때
        // ------------------------------------------------------------

        /// <summary>몇 번째 시도인가. 씬을 다시 올려 재시도하므로 정적으로 센다.</summary>
        private static int serverAttempts;

        private const int MaxServerAttempts = 6;
        private const float ServerRetrySeconds = 5f;

        /// <summary>
        /// **서버가 세션을 열지 못했다.** 재시도할 수 있으면 잠시 뒤 부트 씬을 다시 올리고,
        /// 아니면 프로세스를 끝낸다.
        ///
        /// <b>왜 재시도하는가.</b> 서버를 강제로 끄고 곧바로 다시 켜면 Photon Cloud 에
        /// 이전 세션이 아직 남아 있어 <c>GameIdAlreadyExists</c> 가 난다. 실측으로 확인했다 —
        /// 2초 뒤 재시작이 실패했고 25초 뒤에는 성공했다. 몇 번 기다려 주면 그냥 넘어간다.
        ///
        /// <b>왜 끝내는가.</b> 실패한 채로 두면 아무것도 하지 않는 프로세스가 남는다.
        /// Dedicated Server 는 사람이 보는 창이 없어 그 상태를 알아챌 길이 로그밖에 없다.
        /// 끝내 버리면 실행 스크립트가 종료 코드로 바로 안다.
        ///
        /// ⚠ 같은 <c>NetworkRunner</c> 로 다시 <c>StartGame</c> 하지 않는다. 실패한 러너는
        ///    이미 내려간 상태라 씬을 다시 올려 새 러너로 시작하는 편이 안전하다.
        /// </summary>
        private static async System.Threading.Tasks.Task RetryOrQuitServer(
            StartGameResult result, string session, Scene bootScene)
        {
            serverAttempts++;

            bool retryable = result.ShutdownReason == ShutdownReason.GameIdAlreadyExists;

            if (retryable && serverAttempts < MaxServerAttempts && bootScene.buildIndex >= 0)
            {
                Debug.LogWarning(
                    $"[Warriors] \"{session}\" 세션이 아직 남아 있습니다. " +
                    $"{ServerRetrySeconds:F0}초 뒤 다시 시도합니다. ({serverAttempts}/{MaxServerAttempts})");

                await System.Threading.Tasks.Task.Delay((int)(ServerRetrySeconds * 1000f));

                if (!Application.isPlaying) return;

                SceneManager.LoadScene(bootScene.buildIndex);
                return;
            }

            Debug.LogError(
                $"[Warriors] Dedicated Server 를 시작할 수 없어 프로세스를 종료합니다. " +
                $"({result.ShutdownReason}, 시도 {serverAttempts}회, 종료 코드 1)");

            Application.Quit(1);
        }

        /// <summary>서버가 없을 때 사람이 읽을 문장. 혼자 Host 가 되지 않으므로 반드시 여기로 온다.</summary>
        private static string Describe(ShutdownReason reason, string session)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound:
                    return $"[Warriors] Dedicated Server 를 먼저 실행하세요. \"{session}\" 세션이 열려 있지 않습니다.";

                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                    return $"[Warriors] Dedicated Server 를 먼저 실행하세요. \"{session}\" 에 연결하지 못했습니다. ({reason})";

                case ShutdownReason.GameIsFull:
                    return $"[Warriors] \"{session}\" 세션이 가득 찼습니다.";

                default:
                    return $"[Warriors] 접속에 실패했습니다. ({reason}) Dedicated Server 가 실행 중인지 확인해 주세요.";
            }
        }

        private NetworkRunner FindRunningRunner()
        {
            NetworkRunner mine = GetComponent<NetworkRunner>();

            foreach (NetworkRunner candidate in NetworkRunner.Instances)
                if (candidate != null && candidate != mine && candidate.IsRunning) return candidate;

            return null;
        }
    }
}
