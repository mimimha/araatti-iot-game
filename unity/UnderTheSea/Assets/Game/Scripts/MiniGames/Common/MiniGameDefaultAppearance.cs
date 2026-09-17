using Fusion;
using UnderTheSea.Network;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 외형이 끝내 오지 않으면 기본 외형으로 확정한다. **로그인을 거치지 않고 바로 들어오는 미니게임이 쓴다.**
    ///
    /// <b>왜 따로 떼어 두는가.</b>
    /// <see cref="NetworkPlayerAppearance"/> 는 Lobby 와 ShipCoop 이 함께 쓴다.
    /// 거기에 "몇 초 뒤에 포기한다" 는 정책을 넣었더니 <b>Lobby 까지 같이 포기했다.</b>
    /// 로그인해서 들어온 사람의 캐릭터가 3초 만에 기본 외형으로 한 번 바뀌었다가
    /// 진짜 외형이 도착해 되돌아왔다. 서버 로그로 실측했다.
    ///
    /// 값을 0으로 바꾸는 것으로는 부족하다.
    ///   · 인스펙터에서 누가 다시 켜면 같은 일이 난다
    ///   · 프리팹에 값이 저장돼 있지 않으면 그 값이 <b>PC 의 임포트 캐시에 좌우된다</b>
    ///     (같은 커밋인데 사람마다 Lobby 동작이 달랐다. 이것도 실측했다)
    ///
    /// 그래서 <b>정책 자체를 이 파일로 옮겼다.</b> 이 컴포넌트는 미니게임 캐릭터 프리팹
    /// (<c>ShipCoopPlayer</c> · <c>WarriorsNetPlayer</c>)에만 붙어 있다. Lobby 의 <c>NetworkPlayer.prefab</c> 에는 없으므로,
    /// Lobby 에서는 기본 외형 확정을 <b>부르는 코드가 존재하지 않는다.</b>
    ///
    /// <b>기다리는 시간을 왜 늘렸는가.</b>
    /// 서버는 사람이 접속한 순간 캐릭터를 만들고 시계를 켠다. 그런데 클라이언트는
    /// <b>ShipCoop 씬을 다 읽은 뒤에야</b> 자기 외형을 낼 수 있다. 포탈로 들어오는 길
    /// (Lobby → Runner 종료 → 씬 전환 → 접속)이 생기면서 그 사이가 길어졌고,
    /// 3초로는 <b>진짜 외형이 오기 전에 기본 외형으로 확정</b>돼 버렸다. 서버 로그로 실측했다.
    ///
    ///     [Player:2] 를 기본 외형으로 확정합니다. (3초 안에 외형 제출이 오지 않음)
    ///     [Player:2] 외형 반영 - 74바이트, 채택 4개          ← 뒤늦게 도착
    ///
    /// 이 값은 <b>로그인을 거치지 않은 직접 접속</b>을 위한 안전망일 뿐이다. 넉넉해도
    /// 정상 경로에는 아무 영향이 없다 — 진짜 외형이 오면 시계는 그냥 꺼진다.
    ///
    /// <b>미니게임에서는 왜 필요한가.</b>
    /// 로그인을 거치지 않고 바로 들어오므로 <c>CurrentCharacter</c> 가 없을 수 있다.
    /// 그때 <see cref="NetworkPlayerAppearance.AppearanceReady"/> 가 false 로 남으면
    /// 그 사람은 <b>모든 화면에서 투명한 채로</b> 갑판을 돌아다닌다.
    /// 덮어쓸 진짜 외형이 애초에 없으므로 잃을 것도 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayerAppearance))]
    public sealed class MiniGameDefaultAppearance : NetworkBehaviour
    {
        [Header("기다리는 시간 (초)")]
        [Tooltip("이만큼 기다려도 외형이 확정되지 않으면 기본 외형으로 정한다.\n" +
                 "클라이언트가 씬을 로드하고 스폰된 뒤 제출하기까지 걸리는 시간보다 넉넉해야 한다.")]
        [SerializeField, Min(0.1f)] private float waitSeconds = 20f;

        private NetworkPlayerAppearance appearance;

        /// <summary>서버에서만 도는 대기 시간. 복제하지 않는다.</summary>
        private TickTimer wait;

        public override void Spawned()
        {
            // 서버만 확정할 수 있다. 클라이언트에서는 아무것도 하지 않는다.
            if (!HasStateAuthority)
            {
                return;
            }

            appearance = GetComponent<NetworkPlayerAppearance>();

            if (appearance == null)
            {
                Debug.LogWarning(
                    "[MiniGameDefaultAppearance] NetworkPlayerAppearance 가 없습니다. 할 일이 없습니다.", this);
                return;
            }

            // 늦게 들어온 경우처럼 이미 확정돼 있으면 시작하지도 않는다.
            if (appearance.AppearanceReady)
            {
                return;
            }

            wait = TickTimer.CreateFromSeconds(Runner, waitSeconds);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || appearance == null || !wait.IsRunning)
            {
                return;
            }

            // 진짜 외형이 제때 왔다. 손대지 않고 물러난다.
            if (appearance.AppearanceReady)
            {
                wait = default;
                return;
            }

            if (!wait.Expired(Runner))
            {
                return;
            }

            wait = default;
            appearance.ConfirmDefaultAppearance($"{gameObject.name} 직접 접속 — {waitSeconds}초 안에 외형 제출이 오지 않음");
        }
    }
}
