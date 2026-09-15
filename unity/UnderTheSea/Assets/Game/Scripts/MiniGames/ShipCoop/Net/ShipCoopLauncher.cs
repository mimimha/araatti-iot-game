using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// ShipCoop 네트워크 씬에서 Fusion 세션을 시작한다.
    ///
    /// <b>Lobby 의 <c>FusionLauncher</c> 를 재사용하지 않는다.</b>
    /// 세션 이름 · 씬 · 스폰 규칙이 모두 다르고, Lobby 쪽을 건드리지 않기로 했기 때문이다.
    /// 대신 Lobby 에서 얻은 교훈은 그대로 가져온다.
    ///
    /// <b>모드</b>
    /// <code>
    ///   Dedicated Server 빌드 (UNITY_SERVER) 또는 -mode server  →  GameMode.Server
    ///   그 밖 (에디터 · 클라이언트 빌드)                          →  GameMode.Client
    /// </code>
    ///
    /// ⚠ <b>혼자 Host 가 되는 길은 없다.</b> <c>AutoHostOrClient</c> 를 쓰지 않는다.
    ///    서버가 안 떠 있으면 접속에 실패하고 그 사유를 화면과 로그에 남긴다.
    ///    (Lobby 에서 "켜는 것을 잊고 혼자 Host 로 놀다가 뒤늦게 발견" 하는 일이 있었다)
    ///
    /// ⚠ <b><c>PeerMode.Multiple</c> 이라 클라이언트도 <c>StartGameArgs.Scene</c> 을 줘야 한다.</b>
    ///    빼면 세션은 붙고 플레이어도 스폰되는데 <b>씬이 로드되지 않고 로그에 오류가 없다.</b>
    ///    PRD 08-3 에서 여기서 한 번 막혔다.
    ///
    /// 문서: SHIPCOOP.md 11장
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class ShipCoopLauncher : MonoBehaviour
    {
        [Header("서버 전용")]
        [Tooltip("Server 모드에서 열 포트. 실행 인자 -port 가 있으면 그쪽이 이긴다.")]
        [SerializeField] private ushort serverPort = 27015;

        /// <summary>이 프로세스에서 세션을 시작한 인스턴스. 씬 재로드로 생긴 복사본을 막는다.</summary>
        private static ShipCoopLauncher active;

        private NetworkRunner startedRunner;

        private bool StillHoldsSession => startedRunner != null && !startedRunner.IsShutdown;

        private void Awake()
        {
            if (active != null && active != this && active.StillHoldsSession)
            {
                // Fusion 이 StartGameArgs.Scene 으로 이 씬을 다시 로드하면 NetworkManager 복사본이 생긴다.
                // 그대로 두면 같은 세션 이름으로 StartGame 을 또 불러 GameIdAlreadyExists 로 실패한다.
                Debug.Log("[ShipCoop] 이미 세션을 시작했습니다. 씬 재로드로 생긴 복사본을 정리합니다.");
                Destroy(gameObject);
                return;
            }

            active = this;
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private async void Start()
        {
            if (active != this)
            {
                return;
            }

            // 이 프로세스에서 이미 다른 Runner 가 돌고 있으면 내가 시작하지 않는다.
            if (FindRunningRunner() != null)
            {
                Debug.Log("[ShipCoop] 이미 돌고 있는 Runner 가 있어 이 씬에서는 세션을 시작하지 않습니다.");
                return;
            }

            NetworkRunner runner = GetComponent<NetworkRunner>();

            bool isServer = FusionLaunchArguments.IsDedicatedServerProcess();
            string session = ShipCoopNet.ResolveSession();
            ushort port = FusionLaunchArguments.GetPort(FusionLaunchArguments.PortKey, serverPort);

            Debug.Log(
                $"[ShipCoop] 기동 준비 — 모드 {(isServer ? "Server" : "Client")}, 세션 \"{session}\"" +
                (isServer ? $", 포트 {port}" : string.Empty) +
                $" / 실행 인자: {FusionLaunchArguments.Describe()}");

            // 서버에는 조작하는 사람이 없다. 입력을 만들지 않는다.
            runner.ProvideInput = !isServer;

            if (!isServer)
            {
                // 배 협동 게임 전용 입력 제공자.
                // Lobby 것(WASD + 카메라 각도)으로는 손 두 개의 IMU · 압력 · 버튼을 못 보낸다.
                ShipCoopInputProvider provider = GetComponent<ShipCoopInputProvider>();
                if (provider == null)
                {
                    provider = gameObject.AddComponent<ShipCoopInputProvider>();
                }

                runner.AddCallbacks(provider);
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.buildIndex < 0)
            {
                Debug.LogError(
                    $"[ShipCoop] '{activeScene.path}' 씬이 이 빌드의 Scene List 에 없습니다. " +
                    "Build Profile 의 Scene List 를 확인해 주세요.");
                return;
            }

            StartGameArgs args = new StartGameArgs
            {
                GameMode = isServer ? GameMode.Server : GameMode.Client,
                SessionName = session,

                // ⚠ PeerMode.Multiple 이라 클라이언트도 반드시 지정해야 한다.
                Scene = SceneRef.FromPath(ShipCoopNet.ScenePath),
                SceneManager = GetComponent<NetworkSceneManagerDefault>()
            };

            if (isServer)
            {
                args.Address = NetAddress.Any(port);
            }

            startedRunner = runner;

            StartGameResult result = await runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[ShipCoop] 접속 실패: {result.ShutdownReason} — {result.ErrorMessage}");

                if (!isServer)
                {
                    Debug.LogError(Describe(result.ShutdownReason, session));
                }

                return;
            }

            Debug.Log(isServer
                ? $"[ShipCoop] Dedicated Server 준비 완료. 세션 \"{session}\", 포트 {port}. 클라이언트를 기다립니다."
                : $"[ShipCoop] '{session}' 세션 접속 성공 (Client)");
        }

        /// <summary>서버가 없을 때 사람이 읽을 문장. 혼자 Host 가 되지 않으므로 반드시 여기로 온다.</summary>
        private static string Describe(ShutdownReason reason, string session)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound:
                    return $"[ShipCoop] Dedicated Server 를 먼저 실행하세요. \"{session}\" 세션이 열려 있지 않습니다.";

                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                    return $"[ShipCoop] Dedicated Server 를 먼저 실행하세요. \"{session}\" 에 연결하지 못했습니다. ({reason})";

                case ShutdownReason.GameIsFull:
                    return $"[ShipCoop] \"{session}\" 세션이 가득 찼습니다.";

                default:
                    return $"[ShipCoop] 접속에 실패했습니다. ({reason}) Dedicated Server 가 실행 중인지 확인해 주세요.";
            }
        }

        private NetworkRunner FindRunningRunner()
        {
            NetworkRunner mine = GetComponent<NetworkRunner>();

            foreach (NetworkRunner candidate in NetworkRunner.Instances)
            {
                if (candidate != null && candidate != mine && candidate.IsRunning)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
