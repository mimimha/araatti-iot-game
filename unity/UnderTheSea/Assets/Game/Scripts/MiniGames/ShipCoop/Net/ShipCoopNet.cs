using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// ShipCoop 네트워크 쪽에서 함께 쓰는 상수와 작은 도우미.
    ///
    /// 세션 이름과 씬 경로를 여기 한 곳에 둔다. 런처 · 스포너 · 빌드 도구가 같은 값을 봐야
    /// "서버는 A 씬을 열었는데 클라이언트는 B 를 찾는" 일이 생기지 않는다.
    ///
    /// 문서: SHIPCOOP.md 11장 (네트워크에 붙일 때)
    /// </summary>
    public static class ShipCoopNet
    {
        /// <summary>
        /// 네트워크 전용 ShipCoop 씬.
        ///
        /// ⚠ 민화님의 <c>Scenes/Develop/MinHwa/ShipCoopTest.unity</c> 와 **다른 씬이다.**
        ///    원본은 혼자 키보드로 확인하는 용도로 그대로 남겨 둔다.
        ///
        /// ⚠ 서버 빌드와 클라이언트 빌드의 Scene List 에 **이 경로가 모두 있어야 한다.**
        ///    Fusion 은 <c>SceneRef.FromPath</c> 를 각 피어가 자기 Build Settings 에서
        ///    같은 경로로 찾아 인덱스로 바꾼다. 한쪽에 없으면 그 피어만 씬을 못 연다.
        /// </summary>
        public const string ScenePath = "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity";

        /// <summary>
        /// 세션을 여는 씬. <b>거의 비어 있다.</b> NetworkRunner 와 런처만 있다.
        ///
        /// ⚠ <b>왜 게임 씬에서 바로 시작하지 않는가.</b>
        ///    <c>PeerMode.Multiple</c> 에서 Fusion 은 <c>StartGameArgs.Scene</c> 을
        ///    <b>러너 전용 씬으로 새로 로드한다.</b> 이미 열려 있는 같은 씬을 재사용하지 않는다.
        ///    그래서 게임 씬에서 세션을 시작하면 <b>배도 HUD 도 ShipCoopGame 도 두 벌</b>이 된다.
        ///    실측했다 — 서버에서 <c>ShipCoopServerCleanup</c> 이 14개를 끄고 곧이어 28개를 껐다.
        ///    같은 자리에 배가 두 척이라 발밑 갑판 레이도 남의 배를 맞는다.
        ///
        ///    빈 씬에서 시작하면 게임 씬은 Fusion 이 <b>한 번만</b> 연다.
        /// </summary>
        public const string BootScenePath = "Assets/Game/Scenes/Main/MiniGames/ShipCoopBoot.unity";

        /// <summary>기본 세션 이름. 실행 인자 <c>-session</c> 이 있으면 그쪽이 이긴다.</summary>
        public const string DefaultSession = "shipcoop-1";

        /// <summary>이 프로세스가 쓸 세션 이름.</summary>
        /// <summary>
        /// **다음에 들어갈 세션.** Lobby 에서 미니게임으로 넘어가기 직전에 채운다.
        ///
        /// <b>왜 필요한가.</b> 세션 이름은 지금까지 실행 인자 <c>-session</c> 으로만 정할 수
        /// 있었다. 그것은 <b>프로세스가 뜰 때 고정</b>되므로, 매칭이 "너희는 이 방으로" 라고
        /// 정해 줘도 클라이언트가 받을 자리가 없다.
        ///
        /// 비워 두면 예전 그대로 실행 인자를 따르므로, 지금까지의 실행 방법이 그대로 살아 있다.
        ///
        /// ⚠ 이번 단계에서는 늘 <see cref="DefaultSession"/> 하나를 넣는다. 진짜 매칭이
        ///    붙기 전까지는 고정 세션이고, 그 사실을 감추지 않는다.
        ///
        /// 실제 저장은 게임을 가리지 않는 <see cref="MiniGameSessionRequest"/> 가 한다.
        /// </summary>
        public static string PendingSession
        {
            get => MiniGameSessionRequest.Pending;
            set => MiniGameSessionRequest.Pending = value;
        }

        /// <summary>
        /// 이 프로세스가 쓸 세션 이름.
        ///
        /// <code>
        ///   PendingSession 이 있으면  그것   (Lobby 에서 넘어온 경우)
        ///   없으면 실행 인자 -session        (서버 · 단독 실행)
        ///   그것도 없으면 기본값
        /// </code>
        /// </summary>
        public static string ResolveSession()
        {
            if (!string.IsNullOrWhiteSpace(PendingSession))
            {
                return PendingSession;
            }

            return FusionLaunchArguments.GetString(FusionLaunchArguments.SessionKey, DefaultSession);
        }

        /// <summary>
        /// 지금 네트워크 세션 안에서 돌고 있는가.
        ///
        /// <c>ShipCoopTest.unity</c>(민화님 원본)처럼 Runner 가 아예 없는 씬에서는 거짓이다.
        /// 그래서 기존 싱글 테스트 씬은 아무것도 바뀌지 않는다.
        /// </summary>
        public static bool IsNetworked => FindLiveRunner() != null;

        /// <summary>
        /// **이 컴퓨터가 게임 규칙을 계산하는 쪽인가.** (SHIPCOOP.md 11장)
        ///
        /// <code>
        ///   Runner 없음 (싱글 테스트 씬)  →  참   — 혼자 다 계산한다
        ///   Dedicated Server             →  참
        ///   Client                       →  거짓 — 서버가 보내 주는 결과만 그린다
        /// </code>
        ///
        /// ⚠ 화면 쪽(HUD · 카메라 · 소리)은 이 값을 보면 안 된다. 전원이 다 그려야 한다.
        ///    보는 것은 <b>공유 상태를 바꾸는 쪽</b>뿐이다.
        /// </summary>
        public static bool IsAuthorityHere
        {
            get
            {
                NetworkRunner runner = FindLiveRunner();
                return runner == null || runner.IsServer;
            }
        }

        /// <summary>이 프로세스에서 실제로 돌고 있는 Runner. 없으면 null.</summary>
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

        /// <summary>
        /// 배 모델과 부딪히지 않게 한다.
        ///
        /// 배의 갑판 메시는 콜라이더가 켜져 있지만 <b>걸으라고 켠 것이 아니다.</b>
        /// <c>ShipCoopCharacter</c> 가 보이는 갑판 높이를 레이로 찾으려고 켜 둔 것이다.
        /// 그대로 두면 울퉁불퉁한 배 위를 걷게 되어, 평평한 큐브를 깔아 둔 뜻이 사라진다.
        ///
        /// <c>DebugPlayerMover.IgnoreTheShip</c> 과 같은 처리다. 네트워크 이동에도 똑같이 필요하다.
        /// </summary>
        public static void IgnoreShipColliders(CharacterController body, string shipName = "PirateShip")
        {
            if (body == null)
            {
                return;
            }

            GameObject ship = GameObject.Find(shipName);
            if (ship == null)
            {
                return;
            }

            Collider[] parts = ship.GetComponentsInChildren<Collider>(true);
            int ignored = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && parts[i].enabled)
                {
                    Physics.IgnoreCollision(body, parts[i], true);
                    ignored++;
                }
            }

            if (ignored > 0)
            {
                Debug.Log($"[ShipCoopNet] 배 콜라이더 {ignored}개를 통과하도록 설정했습니다.", body);
            }
        }
    }
}
