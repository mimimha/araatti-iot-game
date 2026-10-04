using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// PRD 08-1 QA 용 빌드를 한 번에 만든다.
    ///
    /// 두 빌드는 <b>같은 씬</b>을 쓰지만 <b>서브타깃이 다르다.</b>
    ///   서버 → Dedicated Server (UNITY_SERVER 정의됨, 그래픽 없음)
    ///   클라 → 일반 Player
    ///
    /// ⚠ 일반 클라이언트 Scene List(`ProjectSettings/EditorBuildSettings.asset`)는 건드리지 않는다.
    ///    거기에 테스트 씬을 넣으면 제품 빌드에 개발용 씬이 섞인다.
    ///    대신 <see cref="BuildPlayerOptions.scenes"/> 로 이 빌드에만 쓸 씬을 직접 넘긴다.
    ///
    /// <b>왜 Build Profile 로 빌드하지 않는가.</b>
    /// `Assets/Settings/Build Profiles/Windows Server Test.asset` 은 이름과 달리 Dedicated Server
    /// 프로필이 아니다. `m_Subtarget: 2` 가 붙어 있지만 `m_PlatformId` 가 일반 Windows Player 와 같고
    /// 설정 객체도 `WindowsPlatformSettings` 다. Dedicated Server 모듈이 없던 때 만들어졌기 때문이다.
    /// 그래서 그 프로필로 `BuildPipeline.BuildPlayer(BuildPlayerWithProfileOptions)` 를 부르면
    /// 모듈을 설치한 뒤에도 계속 일반 플레이어가 나온다. 실제 빌드 산출물로 확인했다.
    /// (서버·클라 둘 다 115 MB, D3D12 폴더 포함)
    ///
    /// 그래서 서브타깃을 <see cref="BuildPlayerOptions.subtarget"/> 으로 직접 지정한다.
    ///
    /// 결과물은 `Builds/` 아래에 생긴다. (`.gitignore` 의 `[Bb]uilds/` 로 제외된다)
    ///
    /// 커맨드라인에서도 쓸 수 있다.
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -standaloneBuildSubtarget Server ^
    ///       -executeMethod UnderTheSea.Network.Editor.FusionTestBuilds.BuildServerFromCommandLine
    ///
    /// ⚠ 커맨드라인에서는 `-standaloneBuildSubtarget` 을 같이 준다.
    ///    서브타깃을 바꾸면 스크립팅 정의가 달라져 재컴파일이 필요한데,
    ///    실행 도중에 바꾸면 도메인 리로드가 -executeMethod 를 끊을 수 있다.
    ///    프로세스 시작 시점에 맞춰 두는 편이 안전하다.
    ///      서버 → -standaloneBuildSubtarget Server
    ///      클라 → -standaloneBuildSubtarget Player
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-1)
    /// </summary>
    public static class FusionTestBuilds
    {
        private const string MenuRoot = "Tools/아라아띠/";

        /// <summary>
        /// QA 용 씬. 서버·클라 빌드 모두 이 씬 하나만 담는다.
        ///
        /// PRD 08-2 부터 대상이 <b>Lobby</b> 다. 서버가 이 씬을 로드해 유지하고
        /// 클라이언트도 이 씬으로 바로 뜬다.
        ///
        /// ⚠ 이 상수가 <b>실제 빌드의 유일한 기준</b>이다.
        ///    `Assets/Settings/Build Profiles/*.asset` 의 Scene List 는 이 스크립트가 읽지 않는다.
        ///    사람이 Build Profiles 창에서 직접 빌드할 때만 쓰인다.
        ///    (`Windows Server Test.asset` 은 이름과 달리 Dedicated Server 프로필이 아니다. 아래 설명 참고)
        ///
        /// 일반 제품 Scene List(`ProjectSettings/EditorBuildSettings.asset`)에는 Lobby 가 이미 들어 있어
        /// 이 변경 때문에 제품 빌드가 달라지지 않는다.
        /// </summary>
        private const string TestScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";

        private const string ServerOutput = "Builds/Server/AraAtti-Server.exe";

        /// <summary>
        /// Profiler 연결용 로비 서버. 평소 서버(<see cref="ServerOutput"/>)와 폴더를 나눠
        /// 잘 도는 빌드를 덮지 않는다. 자세한 것은 <see cref="BuildServerForProfiler"/> 참고.
        /// </summary>
        private const string ServerProfileOutput = "Builds/ServerProfile/AraAtti-Server.exe";
        private const string ClientOutput = "Builds/Client/AraAtti-Client.exe";

        /// <summary>
        /// ShipCoop Dedicated Server 전환용 씬.
        ///
        /// ⚠ Lobby 를 거치지 않는다. 서버와 클라이언트 모두 <b>이 씬 하나만</b> 담는다.
        ///    그래서 두 빌드의 Scene List 가 같은 경로 하나로 맞는다.
        ///    (Fusion 은 씬을 경로가 아니라 목록 번호로 주고받는다)
        /// </summary>
        /// <summary>
        /// ShipCoop 빌드에 담을 씬. <b>순서가 중요하다.</b>
        ///
        /// 첫 씬이 시작 씬(<c>ShipCoopBoot</c>)이어야 한다. 게임 씬에서 바로 시작하면
        /// Fusion 이 같은 씬을 한 벌 더 열어 배와 HUD 가 두 개가 된다.
        /// </summary>
        private static readonly string[] ShipCoopScenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/ShipCoopBoot.unity",
            "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity",
        };

        /// <summary>
        /// 로비에서 배 게임 입장까지 확인하는 개발 클라이언트 씬 목록.
        /// 첫 씬은 직접 접속용 Lobby 여야 하고, 매칭 후 전환할 두 배 씬도 함께 들어 있어야 한다.
        /// </summary>
        private static readonly string[] LobbyShipCoopScenes =
        {
            TestScenePath,
            "Assets/Game/Scenes/Main/MiniGames/ShipCoopBoot.unity",
            "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity",
        };

        private const string ShipCoopServerOutput = "Builds/ShipCoopServer/AraAtti-ShipCoopServer.exe";
        private const string ShipCoopClientOutput = "Builds/ShipCoopClient/AraAtti-ShipCoopClient.exe";

        /// <summary>정상 흐름(Boot → Title → Login → ChannelSelect → Lobby) 확인용 빌드.</summary>
        private const string FlowOutput = "Builds/FlowClient/AraAtti-Flow.exe";

        /// <summary>
        /// <b>시연용 클라이언트.</b> <see cref="FlowOutput"/> 과 씬은 같고 Development 만 뺀다.
        ///
        /// 사람 앞에서 도는 빌드라 화면 구석의 "Development Build" 워터마크가 남으면 안 되고,
        /// 로그마다 스택 트레이스를 뜨느라 느려질 이유도 없다.
        ///
        /// ⚠ Development 가 빠지면 <c>-devjoin</c> 같은 개발용 경로와 개발자 모드 패널이
        ///    <b>같이 사라진다.</b> 시연은 정상 로그인 경로만 쓴다.
        /// </summary>
        private const string ShowcaseOutput = "Builds/Showcase/AraAtti-Flow.exe";

        /// <summary>
        /// Warriors 전환용 씬. <b>순서가 중요하다.</b>
        ///
        /// 첫 씬이 시작 씬(<c>WarriorsBoot</c>)이어야 한다. 게임 씬에서 바로 시작하면
        /// Fusion 이 같은 씬을 한 벌 더 열어 아레나와 HUD 가 두 개가 된다.
        /// </summary>
        private static readonly string[] WarriorsScenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/WarriorsBoot.unity",
            "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity",
        };

        private const string WarriorsServerOutput = "Builds/WarriorsServer/AraAtti-WarriorsServer.exe";
        private const string WarriorsClientOutput = "Builds/WarriorsClient/AraAtti-WarriorsClient.exe";
        /// 광산 전환용 씬. <b>순서가 중요하다.</b>
        ///
        /// 첫 씬이 시작 씬(<c>MineBoot</c>)이어야 한다. 게임 씬에서 바로 시작하면
        /// <c>PeerMode.Multiple</c> 이 동굴과 격자를 두 벌로 만든다.
        /// </summary>
        private static readonly string[] MineScenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/MineBoot.unity",
            "Assets/Game/Scenes/Main/MiniGames/MineNet.unity",
        };

        private const string MineServerOutput = "Builds/MineServer/AraAtti-MineServer.exe";
        private const string MineClientOutput = "Builds/MineClient/AraAtti-MineClient.exe";

        /// <summary>
        /// QA 클라이언트는 <b>반드시 Development Build</b> 로 만든다.
        ///
        /// <c>-devjoin</c> 은 <c>FusionDevEntry</c> 안에서 <c>DEVELOPMENT_BUILD</c> 로 막혀 있다.
        /// Release 로 만들면 그 인자가 조용히 무시되고, Lobby 만 연 것으로 취급되어
        /// "Lobby 에 바로 들어올 수 없습니다" 로 떨어진다. 실제로 그렇게 되어 있었다.
        /// </summary>
        private const BuildOptions ClientOptions = BuildOptions.Development;

        [MenuItem(MenuRoot + "Fusion 서버 빌드 (Dedicated Server)")]
        public static void BuildServer()
        {
            Build(ServerOutput, StandaloneBuildSubtarget.Server);
        }

        /// <summary>
        /// <b>Profiler 를 붙일 수 있는 로비 서버.</b> 원인 조사용이고 평소에는 쓰지 않는다.
        ///
        /// <b>왜 따로 만드는가.</b> 서버 빌드는 <c>BuildOptions.None</c> 이라 Unity Profiler 가
        /// 붙지 않는다. 붙이려면 <c>Development</c> 가 필요한데, 그렇다고 <see cref="ServerOutput"/>
        /// 을 개발 빌드로 덮으면 평소 테스트·시연에 쓰는 서버가 바뀐다. 그래서 출력 폴더를
        /// 나눠 <b>지금 잘 도는 빌드를 건드리지 않는다.</b>
        ///
        /// <b>무엇을 쫓고 있나.</b> 접속자 0명인 로비 DS 가 코어 1.67개를 태운다(실측 7.57%).
        /// 같은 방식으로 띄운 광산·검 DS 는 0.43~0.48% 다. <b>17배 차이</b>다.
        /// 씬을 비교하면 로비에만 Terrain 1개와 ReflectionProbe 1개가 있고 Transform 이
        /// 5,601개(광산 1,013 · 검 10)인데, <b>어느 것이 범인인지는 아직 모른다.</b>
        /// 이 건은 이미 그럴듯한 가설 두 개가 데이터로 깨진 적이 있어
        /// (파티클·Canvas 설, MineCrystalTint 설) 추측으로 고치지 않기로 했다.
        ///
        /// <b>쓰는 법.</b> 빌드한 뒤 이렇게 띄우고 Profiler 창에서 이 프로세스를 고른다.
        /// <code>
        /// Builds\ServerProfile\AraAtti-Server.exe -batchmode -nographics
        ///   -session prof-lobby -port 27015 -region kr
        /// </code>
        /// <c>ConnectWithProfiler</c> 를 켜 두면 실행 즉시 에디터 Profiler 를 찾아 붙는다.
        ///
        /// ⚠ Deep Profile 은 켜지 않는다. 모든 메서드에 계측이 붙어 <b>수치가 왜곡된다.</b>
        ///    먼저 어느 단계(PlayerLoop 의 어디)가 비싼지부터 보고, 좁혀진 뒤에 필요하면 켠다.
        /// </summary>
        [MenuItem(MenuRoot + "로비 서버 빌드 — Profiler 연결용 (원인 조사)")]
        public static void BuildServerForProfiler()
        {
            Build(
                ServerProfileOutput,
                StandaloneBuildSubtarget.Server,
                new[] { TestScenePath },
                BuildOptions.Development | BuildOptions.ConnectWithProfiler);
        }

        [MenuItem(MenuRoot + "Fusion 클라이언트 테스트 빌드")]
        public static void BuildClient()
        {
            Build(ClientOutput, StandaloneBuildSubtarget.Player, LobbyShipCoopScenes, ClientOptions);
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildServerFromCommandLine()
        {
            ExitWith(Build(ServerOutput, StandaloneBuildSubtarget.Server));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildClientFromCommandLine()
        {
            ExitWith(Build(ClientOutput, StandaloneBuildSubtarget.Player, LobbyShipCoopScenes, ClientOptions));
        }

        [MenuItem(MenuRoot + "Warriors 서버 빌드 (Dedicated Server)")]
        public static void BuildWarriorsServer()
        {
            ExitIfCommandLine(Build(
                WarriorsServerOutput, StandaloneBuildSubtarget.Server, WarriorsScenes, BuildOptions.None));
        }

        [MenuItem(MenuRoot + "Warriors 클라이언트 빌드")]
        public static void BuildWarriorsClient()
        {
            ExitIfCommandLine(Build(
                WarriorsClientOutput, StandaloneBuildSubtarget.Player, WarriorsScenes, ClientOptions));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildWarriorsServerFromCommandLine()
        {
            ExitWith(Build(
                WarriorsServerOutput, StandaloneBuildSubtarget.Server, WarriorsScenes, BuildOptions.None));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildWarriorsClientFromCommandLine()
        {
            ExitWith(Build(
                WarriorsClientOutput, StandaloneBuildSubtarget.Player, WarriorsScenes, ClientOptions));
        }

        /// <summary>
        /// **커맨드라인용 — 서버와 클라이언트를 한 번의 Unity 실행에서 잇따라 만든다.**
        ///
        /// 왜 합치는가. 실측으로 Warriors 빌드 한 번은 Unity 실행 하나당 약 80~104초가 드는데
        /// 그중 <b>실제 플레이어 빌드 작업은 41초</b>뿐이다. 나머지는 Unity 부팅 · 에셋 DB 로드 ·
        /// 도메인 리로드 · 종료다. 서버와 클라를 따로 부르면 그 오버헤드를 <b>두 번</b> 낸다.
        /// 한 프로세스 안에서 두 번 빌드하면 그 절반이 사라진다.
        ///
        /// 서버가 실패하면 클라는 만들지 않고 바로 1 로 빠진다 — 어차피 같은 코드가 안 되는 것이다.
        /// </summary>
        public static void BuildWarriorsBothFromCommandLine()
        {
            BuildReport server = Build(
                WarriorsServerOutput, StandaloneBuildSubtarget.Server, WarriorsScenes, BuildOptions.None);

            if (server == null || server.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[FusionTestBuilds] 서버 빌드가 실패해 클라이언트는 건너뜁니다.");
                EditorApplication.Exit(1);
                return;
            }

            ExitWith(Build(
                WarriorsClientOutput, StandaloneBuildSubtarget.Player, WarriorsScenes, ClientOptions));
        }

        /// <summary>
        /// 🧰 <b>QA 한 벌을 한 번에 만든다.</b> 정상 흐름 클라이언트 + 서버 셋.
        ///
        /// <see cref="BuildWarriorsBothFromCommandLine"/> 과 같은 이유다. 빌드 하나를 부를 때마다
        /// Unity 를 새로 띄우면 <b>부팅 · 에셋 DB 로드 · 도메인 리로드 · 종료</b>를 매번 낸다.
        /// 실측으로 그 고정 비용이 빌드 작업보다 컸다. 네 번 부르면 네 번 낸다.
        ///
        /// <code>
        ///   FlowClient        Login 부터 도는 정상 흐름 클라이언트
        ///   Server            Lobby Dedicated Server
        ///   ShipCoopServer    배 게임 Dedicated Server
        ///   WarriorsServer    검 게임 Dedicated Server
        /// </code>
        ///
        /// 하나라도 실패하면 거기서 멈추고 1 로 빠진다. 반쯤 만들어진 한 벌로 QA 하면
        /// 어느 것이 옛 빌드인지 몰라 문제를 잘못 짚는다.
        /// </summary>
        public static void BuildQaSetFromCommandLine()
        {
            // ⚠ **결과는 빌드 직후에 봐야 한다.**
            //
            //    처음에는 넷을 다 만들고 나서 보고서를 한꺼번에 확인했다. 그랬더니 네 개가
            //    모두 "빌드 성공" 을 찍었는데도 첫 번째가 실패로 판정됐다. BuildReport 는
            //    유니티 오브젝트라 다음 빌드가 돌면서 앞의 것이 정리돼 버린다.
            //    들고 있다가 나중에 읽으면 빈 값이 나온다.
            if (!Ok("FlowClient", BuildNormalFlow())) return;
            if (!Ok("Lobby DS", Build(ServerOutput, StandaloneBuildSubtarget.Server))) return;

            if (!Ok("ShipCoop DS", Build(
                    ShipCoopServerOutput, StandaloneBuildSubtarget.Server,
                    ShipCoopScenes, BuildOptions.None))) return;

            if (!Ok("Warriors DS", Build(
                    WarriorsServerOutput, StandaloneBuildSubtarget.Server,
                    WarriorsScenes, BuildOptions.None))) return;

            Debug.Log("[FusionTestBuilds] QA 한 벌을 모두 만들었습니다. (클라이언트 1 · 서버 3)");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// <b>시연 한 벌.</b> Release 클라이언트 1 + Dedicated Server 3.
        ///
        /// <see cref="BuildQaSetFromCommandLine"/> 과 같은 구성이되 클라이언트만 Release 다.
        /// 서버는 창이 없어 워터마크가 없고 로그는 오히려 남아야 하므로 그대로 둔다.
        /// </summary>
        public static void BuildShowcaseSetFromCommandLine()
        {
            if (!Ok("시연 클라이언트(Release)", BuildShowcaseClient())) return;
            if (!Ok("Lobby DS", Build(ServerOutput, StandaloneBuildSubtarget.Server))) return;

            if (!Ok("ShipCoop DS", Build(
                    ShipCoopServerOutput, StandaloneBuildSubtarget.Server,
                    ShipCoopScenes, BuildOptions.None))) return;

            if (!Ok("Warriors DS", Build(
                    WarriorsServerOutput, StandaloneBuildSubtarget.Server,
                    WarriorsScenes, BuildOptions.None))) return;

            Debug.Log("[FusionTestBuilds] 시연 한 벌을 모두 만들었습니다. (Release 클라이언트 1 · 서버 3)");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 🚀 <b>EC2 에 올릴 서버 한 벌.</b> Dedicated Server 넷을 한 번의 Unity 실행으로 만든다.
        ///
        /// <code>
        ///   Server           로비   lobby-ch1
        ///   MineServer       광산   mine-1 · mine-2
        ///   WarriorsServer   검     warriors-1 · warriors-2
        ///   ShipCoopServer   배     shipcoop-1 · shipcoop-2
        /// </code>
        ///
        /// <b>왜 QA 한 벌로는 안 되는가.</b> <see cref="BuildQaSetFromCommandLine"/> 은 광산이
        /// 빠져 있고 클라이언트를 하나 같이 굽는다. EC2 에 올리는 것은 서버뿐이고 광산도
        /// 돌고 있어서 구성이 맞지 않는다. 그렇다고 광산만 따로 부르면 Unity 기동 비용을 또 낸다.
        ///
        /// ⚠ <b>리눅스로 구우려면 플랫폼을 미리 맞춰 둬야 한다.</b> 출력 경로는
        ///    <see cref="Platform"/> 을 따라간다. <c>-buildTarget Linux64</c> 를 같이 주면
        ///    <c>Builds/Linux/</c> 아래로 나가고, 안 주면 윈도우 빌드 자리에 덮어써 버린다.
        ///
        /// <code>
        ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
        ///     -buildTarget Linux64 -standaloneBuildSubtarget Server ^
        ///     -executeMethod UnderTheSea.Network.Editor.FusionTestBuilds.BuildDeployServerSetFromCommandLine
        /// </code>
        ///
        /// 하나라도 실패하면 거기서 멈추고 1 로 빠진다. <b>반만 새 빌드인 채로 올리면 안 된다.</b>
        /// 클라이언트와 <c>Behaviour count mismatch</c> 가 나는데, 어느 서버가 옛것인지
        /// 로그만 봐서는 알기 어렵다. 실제로 그렇게 한 번 헤맸다.
        /// </summary>
        public static void BuildDeployServerSetFromCommandLine()
        {
            if (!Ok("Lobby DS", Build(ServerOutput, StandaloneBuildSubtarget.Server))) return;

            if (!Ok("Mine DS", Build(
                    MineServerOutput, StandaloneBuildSubtarget.Server,
                    MineScenes, BuildOptions.None))) return;

            if (!Ok("Warriors DS", Build(
                    WarriorsServerOutput, StandaloneBuildSubtarget.Server,
                    WarriorsScenes, BuildOptions.None))) return;

            if (!Ok("ShipCoop DS", Build(
                    ShipCoopServerOutput, StandaloneBuildSubtarget.Server,
                    ShipCoopScenes, BuildOptions.None))) return;

            Debug.Log("[FusionTestBuilds] 배포용 서버 한 벌을 모두 만들었습니다. (Dedicated Server 4)");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 커맨드라인용. <b>시연 클라이언트만</b> 굽는다. 실패하면 종료 코드 1 로 빠진다.
        ///
        /// <see cref="BuildShowcaseSetFromCommandLine"/> 은 서버 셋을 같이 굽는다.
        /// EC2 에 올릴 때는 서버를 리눅스로 따로 구우므로 윈도우 서버가 필요 없다.
        /// </summary>
        public static void BuildShowcaseClientFromCommandLine()
        {
            ExitWith(BuildShowcaseClient());
        }
        /// <summary>시연용 Release 클라이언트. 씬 목록은 정상 흐름 빌드와 같다.</summary>
        private static BuildReport BuildShowcaseClient()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError(
                    "[FusionTestBuilds] 제품 Scene List 가 비어 있습니다. " +
                    "File > Build Profiles 의 Scene List 를 확인해 주세요.");
                return null;
            }

            Debug.Log(
                $"[FusionTestBuilds] 시연 클라이언트(Release) — 제품 Scene List {scenes.Length}개: " +
                string.Join(", ", scenes));

            return Build(ShowcaseOutput, StandaloneBuildSubtarget.Player, scenes, BuildOptions.None);
        }

        /// <summary>
        /// 빌드하느라 돌려놓은 서브타깃을 제자리로 되돌린다.
        ///
        /// ⚠ <b>배치 모드에서는 되돌리지 않는다.</b> 서브타깃을 바꾸면 스크립팅 정의가 달라져
        ///    재컴파일이 필요한데, <c>-executeMethod</c> 가 도는 도중에 도메인 리로드가 끼어들면
        ///    <b>남은 빌드가 끊긴다.</b> QA 한 벌처럼 한 실행에서 여럿을 굽는 경우가 특히 그렇다.
        ///    배치는 어차피 끝나고 프로세스가 죽으므로 에디터에 남는 피해가 없다.
        ///
        ///    <b>되돌리는 것이 필요한 쪽은 에디터 메뉴로 부른 경우다.</b> 사람이 그 에디터로
        ///    이어서 Play 하기 때문이다.
        /// </summary>
        private static void RestoreSubtarget(StandaloneBuildSubtarget before)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorUserBuildSettings.standaloneBuildSubtarget == before)
            {
                return;
            }

            EditorUserBuildSettings.standaloneBuildSubtarget = before;

            Debug.Log(
                $"[FusionTestBuilds] 빌드 대상을 {before} 로 되돌렸습니다. " +
                "이걸 안 하면 에디터가 자기를 서버로 여겨 Play 할 때 로비에 접속하지 못합니다.");
        }

        /// <summary>방금 끝난 빌드가 성공했는가. 실패하면 거기서 멈추고 1 로 빠진다.</summary>
        private static bool Ok(string name, BuildReport report)
        {
            if (report != null && report.summary.result == BuildResult.Succeeded) return true;

            Debug.LogError($"[FusionTestBuilds] 한 벌 굽기 — '{name}' 에서 실패했습니다. 나머지는 굽지 않습니다.");
            EditorApplication.Exit(1);
            return false;
        }

        [MenuItem(MenuRoot + "ShipCoop 서버 빌드 (Dedicated Server)")]
        public static void BuildShipCoopServer()
        {
            ExitIfCommandLine(Build(
                ShipCoopServerOutput, StandaloneBuildSubtarget.Server,
                ShipCoopScenes, BuildOptions.None));
        }

        [MenuItem(MenuRoot + "ShipCoop 클라이언트 빌드")]
        public static void BuildShipCoopClient()
        {
            ExitIfCommandLine(Build(
                ShipCoopClientOutput, StandaloneBuildSubtarget.Player,
                ShipCoopScenes, ClientOptions));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildShipCoopServerFromCommandLine()
        {
            ExitWith(Build(
                ShipCoopServerOutput, StandaloneBuildSubtarget.Server,
                ShipCoopScenes, BuildOptions.None));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildShipCoopClientFromCommandLine()
        {
            ExitWith(Build(
                ShipCoopClientOutput, StandaloneBuildSubtarget.Player,
                ShipCoopScenes, ClientOptions));
        }

        [MenuItem(MenuRoot + "광산 서버 빌드 (Dedicated Server)")]
        public static void BuildMineServer()
        {
            ExitIfCommandLine(Build(
                MineServerOutput, StandaloneBuildSubtarget.Server, MineScenes, BuildOptions.None));
        }

        /// <summary>
        /// <b>한 줄 평 장애 재현용 광산 서버.</b> 평소에는 쓰지 않는다. (MINE.md 7장 "장애 재현")
        ///
        /// <c>-minereviewfault</c> 를 읽는 <c>FaultMineReviewService</c> 는 <c>DEVELOPMENT_BUILD</c> 에만
        /// 들어가서 <see cref="BuildMineServer"/>(None) 로 만든 서버는 그 인자를 모른다. 그렇다고 그것을
        /// 개발 빌드로 바꾸면 평소 서버가 바뀌므로 출력 폴더를 나눈다. (Profiler 서버와 같은 이유)
        /// </summary>
        [MenuItem(MenuRoot + "광산 서버 빌드 — 한 줄 평 장애 재현용 (Development)")]
        public static void BuildMineServerForReviewFault()
        {
            Build(MineServerFaultOutput, StandaloneBuildSubtarget.Server, MineScenes, BuildOptions.Development);
        }

        private const string MineServerFaultOutput = "Builds/MineServerFault/AraAtti-MineServer.exe";

        [MenuItem(MenuRoot + "광산 클라이언트 빌드")]
        public static void BuildMineClient()
        {
            ExitIfCommandLine(Build(
                MineClientOutput, StandaloneBuildSubtarget.Player, MineScenes, ClientOptions));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildMineServerFromCommandLine()
        {
            ExitWith(Build(
                MineServerOutput, StandaloneBuildSubtarget.Server, MineScenes, BuildOptions.None));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildMineClientFromCommandLine()
        {
            ExitWith(Build(
                MineClientOutput, StandaloneBuildSubtarget.Player, MineScenes, ClientOptions));
        }

        /// <summary>
        /// <b>광산 한 쌍.</b> 서버 1 + 클라이언트 1 을 한 번의 Unity 실행으로 만든다.
        ///
        /// 광산 흐름을 붙이는 동안 둘을 계속 같이 다시 굽게 된다. 따로 부르면 Unity 기동
        /// 비용(도메인 리로드 · 에셋 후처리)을 두 번 낸다. 실측으로 그 고정 비용이
        /// 빌드 작업 자체보다 컸다.
        /// </summary>
        public static void BuildMinePairFromCommandLine()
        {
            if (!Ok("Mine DS", Build(
                    MineServerOutput, StandaloneBuildSubtarget.Server,
                    MineScenes, BuildOptions.None))) return;

            if (!Ok("Mine 클라이언트", Build(
                    MineClientOutput, StandaloneBuildSubtarget.Player,
                    MineScenes, ClientOptions))) return;

            Debug.Log("[FusionTestBuilds] 광산 한 쌍을 만들었습니다. (서버 1 · 클라이언트 1)");
            EditorApplication.Exit(0);
        }

        [MenuItem(MenuRoot + "정상 흐름 클라이언트 빌드 (Boot 부터)")]
        public static void BuildNormalFlowClient()
        {
            ExitIfCommandLine(BuildNormalFlow());
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildNormalFlowClientFromCommandLine()
        {
            ExitWith(BuildNormalFlow());
        }

        /// <summary>
        /// 정상 게임 흐름을 처음부터 확인하기 위한 빌드.
        ///
        /// Lobby 만 담은 QA 빌드로는 Login · ChannelSelect 를 지나갈 수 없다.
        /// 그래서 <b>제품 Scene List 를 그대로 읽어</b> Boot 부터 시작하는 빌드를 따로 만든다.
        ///
        /// ⚠ 제품 설정을 <b>읽기만 한다.</b>
        ///    <c>EditorBuildSettings.scenes</c> 나 Build Profile 을 고치지 않는다.
        ///    씬 목록은 <see cref="BuildPlayerOptions.scenes"/> 로만 넘긴다.
        /// </summary>
        private static BuildReport BuildNormalFlow()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError(
                    "[FusionTestBuilds] 제품 Scene List 가 비어 있습니다. " +
                    "File > Build Profiles 의 Scene List 를 확인해 주세요.");
                return null;
            }

            Debug.Log(
                $"[FusionTestBuilds] 정상 흐름 빌드 — 제품 Scene List {scenes.Length}개를 그대로 씁니다.\n  " +
                string.Join("\n  ", scenes));

            // Development Build 로 만든다. -devjoin 같은 개발용 경로가 살아 있어야
            // 같은 빌드로 개발자 직접 접속도 확인할 수 있다.
            return Build(FlowOutput, StandaloneBuildSubtarget.Player, scenes, BuildOptions.Development);
        }

        /// <summary>
        /// **어느 운영체제용으로 만드는가.** 에디터가 지금 켜 둔 플랫폼을 그대로 따른다.
        ///
        /// <code>
        ///   에디터 플랫폼이 Linux  →  StandaloneLinux64     EC2 에 올릴 것
        ///   그 밖의 모든 경우      →  StandaloneWindows64   지금까지와 같다
        /// </code>
        ///
        /// <b>왜 인자를 새로 만들지 않고 에디터 플랫폼을 보는가.</b> 서브타깃과 같은 이유다.
        /// 플랫폼을 바꾸면 에셋을 전부 다시 임포트하고 스크립트를 다시 컴파일한다. 빌드가
        /// 도는 도중에 그 일이 벌어지면 도메인 리로드가 <c>-executeMethod</c> 를 끊는다.
        /// 그래서 <b>프로세스가 시작할 때</b> Unity 자신의 인자로 맞춰 둔다.
        ///
        /// <code>
        ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
        ///     -buildTarget Linux64 ^
        ///     -standaloneBuildSubtarget Server ^
        ///     -executeMethod UnderTheSea.Network.Editor.FusionTestBuilds.BuildServerFromCommandLine
        /// </code>
        ///
        /// ⚠ <b>에디터를 리눅스로 바꿔 둔 채로 두지 말 것.</b> 되돌릴 때 전체 임포트가 다시 돌고,
        ///    그사이 에디터는 리눅스용 정의로 돌아 Play 결과가 달라질 수 있다.
        ///    리눅스 빌드는 위처럼 <b>따로 띄운 프로세스</b>에서만 만든다.
        ///    (그래서 메뉴 항목을 두지 않았다. 메뉴로 부르면 켜 둔 에디터의 플랫폼이 바뀐다)
        /// </summary>
        private static BuildTarget Platform =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64
                ? BuildTarget.StandaloneLinux64
                : BuildTarget.StandaloneWindows64;

        /// <summary>
        /// 윈도우용으로 적어 둔 출력 경로를 리눅스용으로 바꾼다.
        ///
        /// <code>
        ///   Builds/Server/AraAtti-Server.exe  →  Builds/Linux/Server/AraAtti-Server.x86_64
        /// </code>
        ///
        /// <b>폴더를 나누는 이유.</b> 같은 자리에 쓰면 리눅스 빌드가 윈도우 빌드를 덮는다.
        /// 로컬 QA 는 계속 윈도우 빌드로 하고 EC2 에는 리눅스 빌드를 올리므로, 둘이 동시에
        /// 있어야 한다.
        ///
        /// <b>확장자.</b> 리눅스 플레이어의 실행 파일은 <c>.x86_64</c> 다. Unity 가 이 이름으로
        /// 만들고 옆에 <c>&lt;이름&gt;_Data/</c> 를 같이 놓는다.
        /// </summary>
        private static string Retarget(string relativeOutput)
        {
            if (Platform != BuildTarget.StandaloneLinux64) return relativeOutput;

            string folder = (Path.GetDirectoryName(relativeOutput) ?? string.Empty)
                .Replace(Separator, '/');
            string name = Path.GetFileNameWithoutExtension(relativeOutput);

            const string Root = "Builds/";
            if (folder.StartsWith(Root)) folder = Root + "Linux/" + folder.Substring(Root.Length);

            return $"{folder}/{name}.x86_64";
        }

        /// <summary>윈도우 경로 구분자. 리터럴을 직접 쓰면 읽기 어려워 이름을 붙였다.</summary>
        private const char Separator = '\\';

        private static BuildReport Build(string relativeOutput, StandaloneBuildSubtarget subtarget)
        {
            return Build(relativeOutput, subtarget, new[] { TestScenePath }, BuildOptions.None);
        }

        private static BuildReport Build(
            string relativeOutput, StandaloneBuildSubtarget subtarget, string[] scenes, BuildOptions options)
        {
            System.DateTime buildMethodEnteredUtc = System.DateTime.UtcNow;
            // 프로젝트 폴더 기준 상대 경로를 절대 경로로 바꾼다.
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string output = Path.Combine(projectRoot, Retarget(relativeOutput));

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);

            // ⚠ **빌드가 끝나면 이 스위치를 되돌려야 한다.** 아래 finally 에서 한다.
            //
            //    서브타깃은 프로젝트 전체 설정이고, 한 번 Server 로 돌려 두면 그대로 남는다.
            //    그러면 Unity 가 UNITY_SERVER 를 붙인 채로 에디터가 돌고,
            //    FusionLaunchArguments.IsDedicatedServerProcess() 가 **에디터에서도 true** 가 된다.
            //    그 상태로 Play 하면 NetworkServiceBootstrap 이 "서버는 서비스가 필요 없다" 며
            //    통째로 건너뛰어, 채널 선택 화면이 조용히 예시 목록을 띄우고 접속이 안 된다.
            //
            //        [ChannelSelect] 네트워크 서비스가 없어 예시 채널 목록을 표시합니다.
            //
            //    증상이 원인을 전혀 가리키지 않는다. "어제 서버를 빌드했기 때문" 이라고
            //    아무도 떠올리지 못한다. 실제로 한 번 겪었다.
            StandaloneBuildSubtarget before = EditorUserBuildSettings.standaloneBuildSubtarget;

            // 에디터 메뉴로 부른 경우를 위해 여기서도 맞춰 준다.
            // 커맨드라인은 -standaloneBuildSubtarget 으로 이미 맞춰져 있어 이 줄이 무해하게 넘어간다.
            EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;

            Debug.Log(
                $"[FusionTestBuilds] 빌드 시작 — 서브타깃 {subtarget}, {Platform}\n" +
                $"  씬 {scenes.Length}개, 첫 씬: {scenes[0]}\n" +
                $"  출력: {output}");

            // ⚠ **평문 HTTP 를 허용해 둔다.**
            //
            //    Unity 는 http:// 요청을 기본으로 막는다. localhost 만 예외라서, 개발 중에는
            //    아무도 이것을 만나지 않는다. 클라이언트가 다른 PC 의 API 를 가리키는 순간
            //    로그인에서 이렇게 터진다.
            //
            //        InvalidOperationException: Insecure connection not allowed
            //
            //    ProjectSettings.asset 을 손으로 고쳐서는 빌드에 반영되지 않았다.
            //    빌드하는 그 세션에서 직접 지정해야 확실히 들어간다.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            System.DateTime buildPlayerCallStartedUtc =
                System.DateTime.UtcNow;

            BuildReport report;

            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = Platform,
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)subtarget,
                    options = options
                });
            }
            finally
            {
                RestoreSubtarget(before);
            }

            System.DateTime buildPlayerCallEndedUtc =
                System.DateTime.UtcNow;

            BuildSummary summary = report.summary;


            WriteBuildReportSummary(
                projectRoot,
                subtarget,
                output,
                scenes,
                report,
                buildMethodEnteredUtc,
                buildPlayerCallStartedUtc,
                buildPlayerCallEndedUtc);

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"[FusionTestBuilds] 빌드 성공 — 서브타깃 {subtarget}, {Platform} → {output}\n" +
                    $"  크기 {summary.totalSize / (1024 * 1024)} MB, 걸린 시간 {summary.totalTime}");
            }
            else
            {
                Debug.LogError(
                    $"[FusionTestBuilds] 빌드 실패 — 서브타깃 {subtarget}: {summary.result}, " +
                    $"오류 {summary.totalErrors}건");
            }

            return report;
        }

        private static void WriteBuildReportSummary(
            string projectRoot,
            StandaloneBuildSubtarget subtarget,
            string output,
            string[] scenes,
            BuildReport report,
            System.DateTime buildMethodEnteredUtc,
            System.DateTime buildPlayerCallStartedUtc,
            System.DateTime buildPlayerCallEndedUtc)
        {
            try
            {
                BuildSummary summary = report.summary;

                string logDirectory = Path.Combine(
                    projectRoot,
                    "Builds",
                    "_logs");

                Directory.CreateDirectory(logDirectory);

                string summaryPath = Path.Combine(
                    logDirectory,
                    $"latest-build-{subtarget}-summary.txt");

                StringBuilder text = new StringBuilder();

                text.AppendLine("[Build Summary]");
                text.AppendLine($"Subtarget: {subtarget}");
                text.AppendLine($"Output: {output}");

                text.AppendLine($"BuildMethodEnteredUtc: {buildMethodEnteredUtc:O}");
                text.AppendLine($"BuildPlayerCallStartedUtc: {buildPlayerCallStartedUtc:O}");
                text.AppendLine($"BuildPlayerCallEndedUtc: {buildPlayerCallEndedUtc:O}");
                
                text.AppendLine($"StartedAt: {summary.buildStartedAt:O}");
                text.AppendLine($"EndedAt: {summary.buildEndedAt:O}");
                text.AppendLine(
                    $"TotalTimeSec: {summary.totalTime.TotalSeconds:F3}");
                text.AppendLine($"BuildSizeBytes: {summary.totalSize}");
                text.AppendLine($"Warnings: {summary.totalWarnings}");
                text.AppendLine($"Errors: {summary.totalErrors}");
                text.AppendLine($"Result: {summary.result}");

                text.AppendLine();
                text.AppendLine("[Scenes]");

                foreach (string scene in scenes)
                {
                    text.AppendLine(scene);
                }

                text.AppendLine();
                text.AppendLine("[Build Steps - Slowest First]");

                int rank = 1;

                foreach (BuildStep step in report.steps
                            .OrderByDescending(item => item.duration))
                {
                    text.AppendLine(
                        $"{rank}. {step.duration.TotalSeconds:F3} sec | " +
                        step.name);

                    rank++;
                }

                File.WriteAllText(
                    summaryPath,
                    text.ToString(),
                    Encoding.UTF8);

                Debug.Log(
                    $"[FusionTestBuilds] 상세 빌드 기록 저장: " +
                    summaryPath);
            }
            catch (System.Exception exception)
            {
                // 측정 파일 저장 실패 때문에 정상 빌드까지 실패 처리하지 않는다.
                Debug.LogWarning(
                    "[FusionTestBuilds] 상세 빌드 기록 저장 실패: " +
                    exception);
            }
        }

        /// <summary>메뉴에서 부른 경우에는 에디터를 닫지 않는다.</summary>
        private static void ExitIfCommandLine(BuildReport report)
        {
            if (Application.isBatchMode)
            {
                ExitWith(report);
            }
        }

        private static void ExitWith(BuildReport report)
        {
            bool ok = report != null && report.summary.result == BuildResult.Succeeded;
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
