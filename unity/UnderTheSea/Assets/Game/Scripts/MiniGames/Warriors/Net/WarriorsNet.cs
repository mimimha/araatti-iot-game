using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// Warriors 네트워크 쪽에서 함께 쓰는 상수와 작은 도우미.
    ///
    /// 세션 이름과 씬 경로를 여기 한 곳에 둔다. 런처 · 스포너 · 빌드 도구가 같은 값을 봐야
    /// "서버는 A 씬을 열었는데 클라이언트는 B 를 찾는" 일이 생기지 않는다.
    ///
    /// 문서: WARRIORS.md 4장 (서버 연동)
    /// </summary>
    public static class WarriorsNet
    {
        /// <summary>
        /// 네트워크 전용 Warriors 게임 씬.
        ///
        /// ⚠ 서연님의 <c>Scenes/Main/MiniGames/Warriors.unity</c> 와 **다른 씬이다.**
        ///    원본과 검증용 <c>Develop/SeoYeon/WarriorsTest.unity</c> 는 그대로 남겨 둔다.
        /// </summary>
        public const string ScenePath = "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity";

        /// <summary>
        /// 세션을 여는 씬. <b>거의 비어 있다.</b> NetworkRunner 와 런처만 있다.
        ///
        /// ⚠ <c>PeerMode.Multiple</c> 에서 Fusion 은 <c>StartGameArgs.Scene</c> 을
        ///    <b>러너 전용 씬으로 새로 로드한다.</b> 게임 씬에서 세션을 시작하면
        ///    아레나도 HUD 도 두 벌이 된다. ShipCoop 에서 실측한 함정이다.
        /// </summary>
        public const string BootScenePath = "Assets/Game/Scenes/Main/MiniGames/WarriorsBoot.unity";

        /// <summary>
        /// 이 게임의 방 이름 앞부분. 방 번호를 붙여 <c>warriors-1</c> · <c>warriors-2</c> 가 된다.
        ///
        /// <b>왜 상수로 쪼개 두는가.</b> 매칭은 DS Pool 에서 빈 방을 찾을 때 세션 목록을
        /// 이 앞부분으로 걸러 낸다. 그때 쓰려고 <c>"warriors"</c> 를 다른 곳에 또 적으면 이름이
        /// 두 벌이 되고, 한쪽만 바꾸는 날 조용히 빈 방을 못 찾게 된다.
        /// </summary>
        public const string SessionPrefix = "warriors";

        /// <summary>기본 방. 실행 인자 <c>-session</c> 이 있으면 그쪽이 이긴다.</summary>
        public const string DefaultSession = SessionPrefix + "-1";

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
        /// 고정된다. 매칭이 "너희는 warriors-2 로" 라고 정해 줘도 클라이언트가 받을 자리가 없다.
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
        /// 지금 네트워크 세션 안에서 돌고 있는가.
        ///
        /// <c>WarriorsTest.unity</c> 처럼 Runner 가 아예 없는 씬에서는 거짓이다.
        /// 그래서 기존 싱글 씬은 아무것도 바뀌지 않는다.
        /// </summary>
        public static bool IsNetworked => FindLiveRunner() != null;

        /// <summary>
        /// **이 컴퓨터가 게임 규칙을 계산하는 쪽인가.**
        ///
        /// <code>
        ///   Runner 없음 (싱글 씬)  →  참   — 혼자 다 계산한다
        ///   Dedicated Server       →  참
        ///   Client                 →  거짓 — 서버가 보내 주는 결과만 그린다
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

        /// <summary>
        /// 공격을 **누가** 휘둘렀는지까지 보고 한 번 더 거른다. 없으면 아무것도 막지 않는다.
        ///
        /// 지금 이것을 다는 곳은 <c>WarriorsPhase2Director</c> 하나뿐이고,
        /// 거기서도 <b>2페이즈 촉수만</b> 본다. 1페이즈 공유 몬스터와 3페이즈 레인은
        /// 이 걸개를 통과해 그대로 지나간다.
        ///
        /// ⚠ 세션이 끝나면 반드시 <c>null</c> 로 되돌려야 한다. 남겨 두면 같은 에디터에서
        ///    <c>WarriorsTest.unity</c> 를 열었을 때 죽은 상태를 읽고 공격이 막힌다.
        /// </summary>
        public static System.Func<WarriorsTarget, GameObject, bool> AttackOwnerFilter;

        /// <summary>이 사람이 이 대상을 칠 자격이 있는가. 걸개가 없으면 늘 참이다.</summary>
        public static bool OwnsAttack(WarriorsTarget target, GameObject attacker)
        {
            System.Func<WarriorsTarget, GameObject, bool> filter = AttackOwnerFilter;
            return filter == null || filter(target, attacker);
        }

        /// <summary>
        /// **이 타격을 확정해도 되는가.**
        ///
        /// <code>
        ///   싱글 씬        Runner 가 없다 → 참. 예전 그대로다
        ///   Dedicated 서버 담당이 맞으면 참
        ///   Client         늘 거짓. 결과는 서버에서 받는다
        /// </code>
        /// </summary>
        public static bool CanResolveHit(WarriorsTarget target, GameObject attacker)
        {
            return IsAuthorityHere && OwnsAttack(target, attacker);
        }

        /// <summary>
        /// **공격 범위 안의 콜라이더를 찾는다.** 세션 안에서는 러너의 물리 씬에 물어본다.
        ///
        /// <b>왜 <c>Physics.OverlapSphere</c> 를 그대로 쓰면 안 되는가.</b>
        /// Fusion 의 <c>PeerMode</c> 가 <c>Multiple</c> 이라, 러너는 게임 씬을
        /// <b>자기 전용 물리 씬</b>으로 연다. <c>Physics.*</c> 의 정적 함수들은 늘
        /// <b>기본 물리 씬</b>에 묻기 때문에, 바로 앞에 몬스터가 서 있어도
        /// <b>콜라이더를 하나도 찾지 못한다.</b> 자기 캡슐조차 안 잡힌다.
        ///
        /// 실측: 서버·클라이언트 모두 <c>OverlapSphereNonAlloc</c> 결과가 0 이었다.
        /// 입력도 도착했고 공격 코루틴도 돌았는데, 벨 대상이 없어서 조용히 아무 일도
        /// 일어나지 않았다.
        ///
        /// ⚠ <c>WarriorsTest</c> 처럼 러너가 없는 씬에서는 예전 그대로 기본 물리 씬에 묻는다.
        /// </summary>
        public static int OverlapSphere(
            Vector3 center, float radius, Collider[] results,
            int layerMask, QueryTriggerInteraction queryTriggers)
        {
            NetworkRunner runner = FindLiveRunner();

            if (runner != null)
            {
                PhysicsScene scene = runner.GetPhysicsScene();

                if (scene.IsValid())
                {
                    return scene.OverlapSphere(center, radius, results, layerMask, queryTriggers);
                }
            }

            return Physics.OverlapSphereNonAlloc(center, radius, results, layerMask, queryTriggers);
        }

        private static NetworkRunner FindLiveRunner()
        {
            foreach (NetworkRunner candidate in NetworkRunner.Instances)
            {
                if (candidate != null && candidate.IsRunning)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
