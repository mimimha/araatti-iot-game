using System.IO;
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

        [MenuItem(MenuRoot + "Fusion 서버 빌드 (Dedicated Server)")]
        public static void BuildServer()
        {
            Build(ServerOutput, StandaloneBuildSubtarget.Server);
        }

        [MenuItem(MenuRoot + "Fusion 클라이언트 테스트 빌드")]
        public static void BuildClient()
        {
            Build(ClientOutput, StandaloneBuildSubtarget.Player);
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildServerFromCommandLine()
        {
            ExitWith(Build(ServerOutput, StandaloneBuildSubtarget.Server));
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void BuildClientFromCommandLine()
        {
            ExitWith(Build(ClientOutput, StandaloneBuildSubtarget.Player));
        }

        private static BuildReport Build(string relativeOutput, StandaloneBuildSubtarget subtarget)
        {
            // 프로젝트 폴더 기준 상대 경로를 절대 경로로 바꾼다.
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string output = Path.Combine(projectRoot, relativeOutput);

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);

            // 에디터 메뉴로 부른 경우를 위해 여기서도 맞춰 준다.
            // 커맨드라인은 -standaloneBuildSubtarget 으로 이미 맞춰져 있어 이 줄이 무해하게 넘어간다.
            EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;

            Debug.Log(
                $"[FusionTestBuilds] 빌드 시작 — 서브타깃 {subtarget}\n" +
                $"  씬: {TestScenePath}\n" +
                $"  출력: {output}");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TestScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)subtarget,
                options = BuildOptions.None
            });

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

        private static void ExitWith(BuildReport report)
        {
            bool ok = report != null && report.summary.result == BuildResult.Succeeded;
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
