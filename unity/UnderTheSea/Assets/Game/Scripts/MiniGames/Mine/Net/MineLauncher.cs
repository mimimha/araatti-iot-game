using Fusion;
using Fusion.Sockets;
using MiniGames.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mine.Net
{
    /// <summary>
    /// 광산 네트워크 씬에서 Fusion 세션을 시작한다.
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
    public sealed class MineLauncher : MonoBehaviour
    {
        [Header("서버 전용")]
        [Tooltip("Server 모드에서 열 포트. 실행 인자 -port 가 있으면 그쪽이 이긴다. "
                 + "Lobby(27015) · Warriors(27031) 와 겹치지 않게 광산은 27032 를 쓴다. "
                 + "한 PC 에서 여러 미니게임 서버를 같이 띄울 수 있어야 한다.")]
        [SerializeField] private ushort serverPort = MineNet.DefaultPort;

        [Tooltip("정원과 표시 이름이 들어 있는 설정 에셋. MiniGame_Mining 을 꽂는다. "
                 + "비워 두면 입장 관문이 상태만 보고 정원은 못 막는다.")]
        [SerializeField] private MiniGameConfig config;

        /// <summary>이 프로세스에서 세션을 시작한 인스턴스. 씬 재로드로 생긴 복사본을 막는다.</summary>
        private static MineLauncher active;

        private NetworkRunner startedRunner;

        private bool StillHoldsSession => startedRunner != null && !startedRunner.IsShutdown;

        private void Awake()
        {
            if (active != null && active != this && active.StillHoldsSession)
            {
                Debug.Log("[Mine] 이미 세션을 시작했습니다. 씬 재로드로 생긴 복사본을 정리합니다.");
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
                Debug.Log("[Mine] 이미 돌고 있는 Runner 가 있어 이 씬에서는 세션을 시작하지 않습니다.");
                return;
            }

            NetworkRunner runner = GetComponent<NetworkRunner>();

            bool isServer = FusionLaunchArguments.IsDedicatedServerProcess();
            string session = MineNet.ResolveSession();
            ushort port = FusionLaunchArguments.GetPort(FusionLaunchArguments.PortKey, serverPort);

            Debug.Log(
                $"[Mine] 기동 준비 - 모드 {(isServer ? "Server" : "Client")}, 세션 \"{session}\"" +
                (isServer ? $", 포트 {port}" : string.Empty) +
                $" / 실행 인자: {FusionLaunchArguments.Describe()}");

            // 서버에는 조작하는 사람이 없다. 입력을 만들지 않는다.
            runner.ProvideInput = !isServer;

            if (isServer)
            {
                // ⚠ **콜백 등록은 서버 분기 안에 둔다.**
                //    입력 제공자는 아래 if (!isServer) 안에서 등록된다. 거기에 입장 판정을 같이
                //    넣으면 서버에서는 OnConnectRequest 가 **아예 불리지 않는다.**
                //    배 게임에서 실측으로 겪은 사고라 검 게임과 같은 자리에 같은 주석을 남긴다.
                MiniGameAdmission admission = GetComponent<MiniGameAdmission>();

                if (admission == null)
                {
                    admission = gameObject.AddComponent<MiniGameAdmission>();
                }

                admission.Configure(config);
                runner.AddCallbacks(admission);
            }

            if (!isServer)
            {
                MineInputProvider provider = GetComponent<MineInputProvider>();
                if (provider == null) provider = gameObject.AddComponent<MineInputProvider>();
                runner.AddCallbacks(provider);
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.buildIndex < 0)
            {
                Debug.LogError(
                    $"[Mine] '{activeScene.path}' 씬이 이 빌드의 Scene List 에 없습니다. " +
                    "Build Profile 의 Scene List 를 확인해 주세요.");
                return;
            }

            StartGameArgs args = new StartGameArgs
            {
                GameMode = isServer ? GameMode.Server : GameMode.Client,
                SessionName = session,
                Scene = SceneRef.FromPath(MineNet.ScenePath),
                SceneManager = GetComponent<NetworkSceneManagerDefault>(),

                // 인자가 없으면 null 이고, Fusion 은 null 을 공용 설정으로 읽는다.
                CustomPhotonAppSettings = FusionSessionIsolation.PhotonSettings
            };

            if (isServer)
            {
                args.Address = NetAddress.Any(port);

                // 정원을 Photon 에게도 알린다. **동시에 두드리는 경쟁은 여기서만 막을 수 있다.**
                // 우리 쪽 OnConnectRequest 는 승인됐지만 아직 합류하지 않은 사람을 세지 못한다.
                if (config != null)
                {
                    args.PlayerCount = config.MaxPlayers;
                    Debug.Log($"[Mine] 세션 정원을 {config.MaxPlayers}명으로 엽니다. ({config.DisplayName})");
                }
                else
                {
                    Debug.LogWarning(
                        "[Mine] 설정 에셋이 비어 있어 정원을 정하지 못했습니다. " +
                        "MineBoot 씬의 MineLauncher 에 MiniGame_Mining 을 연결해 주세요.");
                }
            }

            startedRunner = runner;

            StartGameResult result = await runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[Mine] 접속 실패: {result.ShutdownReason} - {result.ErrorMessage}");

                if (!isServer)
                {
                    string told = Describe(result.ShutdownReason, session);
                    Debug.LogError(told);

                    // ⚠ **Lobby 에서 넘어온 경우 반드시 알려야 한다.**
                    //
                    //    알리지 않으면 MiniGameTransition 은 아직 입장 중인 줄 알고 로딩 화면을
                    //    영원히 띄운다. 실제로 그렇게 갇혔다 — 서버가 방을 잃은 사이에 들어가서
                    //    "게임에 입장 중..." 에서 나오지 못했다.
                    //
                    //    듣는 사람이 없으면 아무 일도 일어나지 않는다. 단독 실행은 그대로다.
                    MiniGameEntry.ReportFailed(told);
                }
                return;
            }

            Debug.Log(isServer
                ? $"[Mine] Dedicated Server 준비 완료. 세션 \"{session}\", 포트 {port}. 클라이언트를 기다립니다."
                : $"[Mine] '{session}' 세션 접속 성공 (Client)");
        }

        /// <summary>서버가 없을 때 사람이 읽을 문장. 혼자 Host 가 되지 않으므로 반드시 여기로 온다.</summary>
        private static string Describe(ShutdownReason reason, string session)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound:
                    return $"[Mine] Dedicated Server 를 먼저 실행하세요. \"{session}\" 세션이 열려 있지 않습니다.";

                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                    return $"[Mine] Dedicated Server 를 먼저 실행하세요. \"{session}\" 에 연결하지 못했습니다. ({reason})";

                case ShutdownReason.GameIsFull:
                    return $"[Mine] \"{session}\" 세션이 가득 찼습니다.";

                default:
                    return $"[Mine] 접속에 실패했습니다. ({reason}) Dedicated Server 가 실행 중인지 확인해 주세요.";
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
