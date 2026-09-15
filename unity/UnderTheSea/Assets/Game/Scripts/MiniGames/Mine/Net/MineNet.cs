using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 광산 네트워크 쪽에서 함께 쓰는 상수와 작은 도우미.
    ///
    /// 세션 이름 · 씬 경로 · 포트를 여기 한 곳에 둔다. 런처 · 스포너 · 빌드 도구가
    /// 같은 값을 봐야 "서버는 A 씬을 열었는데 클라이언트는 B 를 찾는" 일이 없다.
    ///
    /// 문서: MINE.md 11장 10단계 (네트워크 동기화)
    /// </summary>
    public static class MineNet
    {
        /// <summary>
        /// 네트워크 전용 광산 씬.
        ///
        /// ⚠ 효진님의 <c>Scenes/Develop/HyoJin/MineTest.unity</c> 와 **다른 씬이다.**
        ///    원본은 혼자 4턴을 도는 검증용이라 그대로 남겨 둔다.
        /// </summary>
        public const string ScenePath = "Assets/Game/Scenes/Main/MiniGames/MineNet.unity";

        /// <summary>
        /// 세션을 여는 씬. <b>거의 비어 있다.</b> NetworkRunner 와 런처만 있다.
        ///
        /// ⚠ <c>PeerMode.Multiple</c> 에서 Fusion 은 <c>StartGameArgs.Scene</c> 을
        ///    <b>러너 전용 씬으로 새로 로드한다.</b> 게임 씬에서 세션을 시작하면
        ///    동굴도 격자도 두 벌이 된다. ShipCoop 에서 실측한 함정이다.
        /// </summary>
        public const string BootScenePath = "Assets/Game/Scenes/Main/MiniGames/MineBoot.unity";

        /// <summary>기본 세션 이름. 실행 인자 <c>-session</c> 이 있으면 그쪽이 이긴다.</summary>
        public const string DefaultSession = "mine-1";

        /// <summary>
        /// 기본 포트. Lobby(27015) · Warriors(27031) 와 겹치지 않게 잡았다.
        /// 실행 인자 <c>-port</c> 가 있으면 그쪽이 이긴다.
        /// </summary>
        public const ushort DefaultPort = 27032;

        /// <summary>한 판의 최대 인원. P1~P4.</summary>
        public const int MaxCrew = 4;

        /// <summary>시작에 필요한 기본 인원. 정식 기본값은 4인 릴레이다.</summary>
        public const int DefaultCrewToStart = MaxCrew;

        /// <summary>QA 용 실행 인자. <c>-crew 2</c> 로 두 명만 모여도 시작한다.</summary>
        public const string CrewKey = "-crew";

        /// <summary>이 프로세스가 쓸 세션 이름.</summary>
        public static string ResolveSession()
        {
            return FusionLaunchArguments.GetString(FusionLaunchArguments.SessionKey, DefaultSession);
        }

        /// <summary>
        /// 시작에 필요한 인원. <b>실행 인자가 인스펙터 값을 이긴다.</b>
        ///
        /// 팀원 테스트에서 네 명을 못 모을 때 빌드를 다시 만들지 않고 바꾸기 위한 것이다.
        /// 2명으로 시작하면 P1 → P2 까지만 돌고 끝난다. 없는 P3 · P4 를 기다리지 않는다.
        /// </summary>
        public static int ResolveCrewToStart(int fromInspector)
        {
            string raw = FusionLaunchArguments.GetString(CrewKey, null);

            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int wanted))
            {
                return Mathf.Clamp(wanted, 1, MaxCrew);
            }

            return Mathf.Clamp(fromInspector, 1, MaxCrew);
        }

        /// <summary>
        /// 지금 네트워크 세션 안에서 돌고 있는가.
        ///
        /// <c>MineTest.unity</c> 처럼 Runner 가 아예 없는 씬에서는 거짓이다.
        /// 그래서 기존 혼자 하는 경로는 아무것도 바뀌지 않는다.
        /// </summary>
        public static bool IsNetworked => FindLiveRunner() != null;

        /// <summary>
        /// **이 컴퓨터가 게임 규칙을 계산하는 쪽인가.**
        ///
        /// <code>
        ///   Runner 없음 (혼자 하는 씬)  →  참   — 혼자 다 계산한다
        ///   Dedicated Server            →  참
        ///   Client                      →  거짓 — 서버가 보내 주는 결과만 그린다
        /// </code>
        /// </summary>
        public static bool IsAuthorityHere
        {
            get
            {
                NetworkRunner runner = FindLiveRunner();
                return runner == null || runner.IsServer;
            }
        }

        private static NetworkRunner FindLiveRunner()
        {
            foreach (NetworkRunner candidate in NetworkRunner.Instances)
            {
                if (candidate != null && candidate.IsRunning) return candidate;
            }

            return null;
        }
    }
}
