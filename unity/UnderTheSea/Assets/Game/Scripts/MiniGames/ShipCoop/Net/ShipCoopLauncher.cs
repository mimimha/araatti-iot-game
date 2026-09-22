using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using MiniGames.Common;
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
        [SerializeField] private ushort serverPort = 27016;

        [Header("이 미니게임의 설정")]
        [Tooltip("MiniGame_Ship 에셋을 연결한다. 정원(MaxPlayers)의 단일 출처다. " +
                 "Lobby 나 PlayerRoster 를 거치지 않는다 — Dedicated Server 는 Lobby 없이 바로 뜨므로 " +
                 "그쪽에 기대면 서버에서는 늘 비어 있다. 씬에서 직접 잇는다.")]
        [SerializeField] private MiniGameConfig config;

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

            if (isServer)
            {
                // 판이 끝났거나 항해 중이면 새 접속을 거절한다. **서버에서만 등록한다.**
                //
                // ⚠ 입력 제공자는 바로 아래 if (!isServer) 안에서 등록된다. 그래서 서버에서는
                //    그쪽 OnConnectRequest 가 **아예 불리지 않는다.** 거기에 거절 코드를 넣으면
                //    조용히 아무 일도 안 일어난다. 실측으로 확인했다.
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
                SceneManager = GetComponent<NetworkSceneManagerDefault>(),

                // 인자가 없으면 null 이고, Fusion 은 null 을 공용 설정으로 읽는다.
                CustomPhotonAppSettings = FusionSessionIsolation.PhotonSettings
            };

            if (!isServer)
            {
                // 이번 판의 인원을 서버에 들고 간다. 로비 서버와 미니게임 서버는 서로
                // 말을 걸지 않으므로, 접속하는 사람이 나르는 것이 유일한 길이다.
                args.ConnectionToken = MatchCrewToken.Write(MiniGameSessionRequest.Crew);
            }

            if (isServer)
            {
                args.Address = NetAddress.Any(port);

                // 정원을 Photon 에게도 알린다. **동시에 두드리는 경쟁은 여기서만 막을 수 있다.**
                // 우리 쪽 OnConnectRequest 는 승인됐지만 아직 합류하지 않은 사람을 세지 못한다.
                if (config != null)
                {
                    args.PlayerCount = config.MaxPlayers;
                    Debug.Log($"[ShipCoop] 세션 정원을 {config.MaxPlayers}명으로 엽니다. ({config.DisplayName})");
                }
                else
                {
                    Debug.LogWarning(
                        "[ShipCoop] 설정 에셋이 비어 있어 정원을 정하지 못했습니다. " +
                        "ShipCoopBoot 씬의 ShipCoopLauncher 에 MiniGame_Ship 을 연결해 주세요.");
                }
            }

            startedRunner = runner;

            StartGameResult result = await runner.StartGame(args);

            if (!result.Ok)
            {
                // 거절은 고장이 아니다. 서버가 "지금은 안 받는다"고 제대로 답한 것이라
                // 빨간 에러로 남기면 진짜 오류를 찾을 때 방해가 된다. 경고로 낮춘다.
                bool refused = result.ShutdownReason == ShutdownReason.ConnectionRefused;

                string line = $"[ShipCoop] 접속 실패: {result.ShutdownReason} — {result.ErrorMessage}";
                if (refused) Debug.LogWarning(line); else Debug.LogError(line);

                if (!isServer)
                {
                    string told = Describe(result.ShutdownReason, session);
                    if (refused) Debug.LogWarning(told); else Debug.LogError(told);

                    // Lobby 에서 넘어온 경우라면 화면에 갇히지 않게 알린다.
                    // 듣는 사람이 없으면 아무 일도 일어나지 않는다 — 단독 실행은 그대로다.
                    MiniGameEntry.ReportFailed(told);
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

                case ShutdownReason.ConnectionRefused:
                    // 서버는 살아 있는데 지금은 안 받는 것이다. 이유는 서버 로그에만 있다 —
                    // Fusion 의 Refuse() 는 사유를 실어 보내지 못한다. 그래서 한 문장으로 통일한다.
                    return "[ShipCoop] 지금은 이 미니게임에 입장할 수 없습니다. 잠시 후 다시 시도해 주세요.";

                case ShutdownReason.ConnectionTimeout:
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
