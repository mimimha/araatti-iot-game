using System.IO;
using System.Linq;
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

        [MenuItem(MenuRoot + "Fusion 클라이언트 테스트 빌드")]
        public static void BuildClient()
        {
            Build(ClientOutput, StandaloneBuildSubtarget.Player, new[] { TestScenePath }, ClientOptions);
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildServerFromCommandLine()
        {
            ExitWith(Build(ServerOutput, StandaloneBuildSubtarget.Server));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildClientFromCommandLine()
        {
            ExitWith(Build(ClientOutput, StandaloneBuildSubtarget.Player, new[] { TestScenePath }, ClientOptions));
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

            Debug.LogError($"[FusionTestBuilds] QA 한 벌 — '{name}' 에서 실패했습니다.");
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

        private static BuildReport Build(string relativeOutput, StandaloneBuildSubtarget subtarget)
        {
            return Build(relativeOutput, subtarget, new[] { TestScenePath }, BuildOptions.None);
        }

        private static BuildReport Build(
            string relativeOutput, StandaloneBuildSubtarget subtarget, string[] scenes, BuildOptions options)
        {
            // 프로젝트 폴더 기준 상대 경로를 절대 경로로 바꾼다.
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string output = Path.Combine(projectRoot, relativeOutput);

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
                $"[FusionTestBuilds] 빌드 시작 — 서브타깃 {subtarget}\n" +
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

            BuildReport report;

            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)subtarget,
                    options = options
                });
            }
            finally
            {
                RestoreSubtarget(before);
            }

            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"[FusionTestBuilds] 빌드 성공 — 서브타깃 {subtarget} → {output}\n" +
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
