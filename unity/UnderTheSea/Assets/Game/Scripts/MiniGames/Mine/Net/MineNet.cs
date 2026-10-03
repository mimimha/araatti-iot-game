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

        /// <summary>
        /// 이 게임의 방 이름 앞부분. 방 번호를 붙여 <c>mine-1</c> · <c>mine-2</c> 가 된다.
        ///
        /// <b>왜 상수로 쪼개 두는가.</b> 매칭은 DS Pool 에서 빈 방을 찾을 때 세션 목록을
        /// 이 앞부분으로 걸러 낸다. 그때 쓰려고 <c>"mine"</c> 를 다른 곳에 또 적으면 이름이
        /// 두 벌이 되고, 한쪽만 바꾸는 날 조용히 빈 방을 못 찾게 된다.
        /// </summary>
        public const string SessionPrefix = "mine";

        /// <summary>기본 방. 실행 인자 <c>-session</c> 이 있으면 그쪽이 이긴다.</summary>
        public const string DefaultSession = SessionPrefix + "-1";

        /// <summary>
        /// 기본 포트. Lobby(27015) · Warriors(27031) 와 겹치지 않게 잡았다.
        /// 실행 인자 <c>-port</c> 가 있으면 그쪽이 이긴다.
        /// </summary>
        public const ushort DefaultPort = 27032;

        /// <summary>한 판의 최대 인원. P1~P4.</summary>
        public const int MaxCrew = 4;

        /// <summary>
        /// 시작에 필요한 기본 인원.
        ///
        /// <b>2 인 이유는 광산이 릴레이이기 때문이다.</b> 한 사람이 30초 파고 다음 사람이
        /// 이어받는다(<c>AdvanceTurn</c>). 혼자면 이어받을 사람이 없어 협동이 성립하지 않는다.
        /// 둘이 최소 단위다.
        ///
        /// ⚠ <b>이 값은 임시 주인이다.</b> 랜덤 매칭이 붙으면 몇 명으로 시작할지는 매칭이
        ///    정하고, 그 값을 <see cref="CrewKey"/> 로 넘기게 된다. 그때 이 상수는
        ///    "매칭이 아무 말 없을 때" 의 기본값으로만 남는다.
        ///
        /// MINE.md 2장은 <b>1~4명</b>이라고 적고 있다. 혼자서도 한 판이 끝나는 것이 원래
        /// 설계이므로, 2로 올리는 것은 <b>매칭이 붙기 전까지의 잠정 결정</b>이다.
        /// </summary>
        public const int DefaultCrewToStart = 2;

        /// <summary>
        /// 시작 인원을 덮어쓰는 실행 인자. <c>-crew 1</c> 이면 혼자서도 시작한다.
        ///
        /// QA 에서 혼자 한 바퀴를 돌려 볼 때 쓰고, 나중에는 <b>매칭이 정한 인원</b>을
        /// 넘기는 통로가 된다.
        /// </summary>
        public const string CrewKey = "-crew";

        /// <summary>
        /// 이 프로세스가 쓸 세션 이름.
        ///
        /// <code>
        ///   MiniGameSessionRequest.Pending 이 있으면  그것   (Lobby 매칭이 정해 준 방)
        ///   없으면 실행 인자 -session                        (서버 · 단독 실행)
        ///   그것도 없으면 기본값
        /// </code>
        ///
        /// <b>왜 실행 인자만으로는 안 되는가.</b> <c>-session</c> 은 프로세스가 뜰 때
        /// 고정된다. 매칭이 "너희는 mine-2 로" 라고 정해 줘도 클라이언트가 받을 자리가 없다.
        /// 전용 서버는 Pending 이 늘 비어 있으므로 예전 그대로 실행 인자를 따른다.
        ///
        /// 배(<c>ShipCoopNet</c>)가 먼저 쓰던 방식을 그대로 가져왔다. 저장하는 자리는
        /// 게임을 가리지 않는 <see cref="MiniGameSessionRequest"/> 하나다.
        /// </summary>
        public static string ResolveSession()
        {
            string assigned = MiniGameSessionRequest.Pending;
            if (!string.IsNullOrWhiteSpace(assigned)) return assigned;

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
            // ⚠ **매칭이 정해 준 인원이 가장 세다.** 3인 판으로 묶여 온 사람들을
            //    인스펙터의 2명이나 실행 인자로 둘만 모여도 출발시키면, 아직 오는 중인
            //    세 번째 사람이 들어갈 자리가 사라진다.
            if (MatchCrew.Assigned > 0) return Mathf.Clamp(MatchCrew.Assigned, 1, MaxCrew);

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
