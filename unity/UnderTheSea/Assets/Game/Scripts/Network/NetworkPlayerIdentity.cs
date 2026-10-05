using Fusion;
using UnderTheSea.Account;
using UnityEngine;

namespace UnderTheSea.Network
{
    /// <summary>
    /// 이 캐릭터가 **누구인지**. 지금은 닉네임 하나뿐이다.
    ///
    /// <b>왜 따로 올리는가.</b> 접속할 때 닉네임을 넘기긴 하지만
    /// (<c>INetworkService.Connect</c>) 그 값은 세션을 여는 데만 쓰이고
    /// 플레이어 오브젝트까지 오지 않는다. 그래서 남의 이름을 알 방법이 없었다.
    ///
    /// <b>왜 메시지에 이름을 같이 싣지 않는가.</b> 당장은 그게 간단하다. 그런데
    /// 머리 위 이름표 · 귓속말 · 차단처럼 "이 캐릭터가 누구냐" 를 묻는 것이 하나만
    /// 더 생겨도 같은 일을 다시 해야 한다. 캐릭터에 한 번 붙여 두면 전부 그것을 본다.
    ///
    /// <b>구조는 <see cref="NetworkPlayerAppearance"/> 와 같다.</b>
    ///   내 것이면 → RPC 로 제출 → 서버가 다듬어 [Networked] 에 기록 → 모두가 읽는다
    ///
    /// RPC 로만 보내면 **늦게 들어온 사람은 못 받는다.** 이미 지나간 호출이기 때문이다.
    /// 그래서 전달은 RPC 로 한 번, 보관과 전파는 <c>[Networked]</c> 로 한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerIdentity : NetworkBehaviour
    {
        /// <summary>
        /// 이름이 너무 길면 채팅 한 줄을 다 잡아먹는다. 서버에서 자른다.
        ///
        /// <c>NetworkString&lt;_16&gt;</c> 은 16 글자까지 담는다. 한글도 한 글자로 센다.
        /// </summary>
        private const int MaxNicknameLength = 16;

        /// <summary>서버가 확정한 닉네임. 비어 있으면 아직 안 왔다.</summary>
        [Networked]
        public NetworkString<_16> Nickname { get; private set; }

        /// <summary>화면에 쓸 이름. 아직 안 왔으면 빈 문자열이 아니라 대신할 말을 준다.</summary>
        public string DisplayName
        {
            get
            {
                string value = Nickname.Value;
                return string.IsNullOrWhiteSpace(value) ? "이름 없음" : value;
            }
        }

        private bool submitted;

        /// <summary>입력 주인이 없는 NPC의 이름을 서버에서 정하고 모든 클라이언트에 복제한다.</summary>
        public void SetNpcNickname(string nickname)
        {
            if (!HasStateAuthority || Object.InputAuthority != PlayerRef.None)
            {
                return;
            }

            string trimmed = (nickname ?? string.Empty).Trim();
            if (trimmed.Length > 0)
            {
                Nickname = trimmed.Length > MaxNicknameLength
                    ? trimmed.Substring(0, MaxNicknameLength)
                    : trimmed;
            }
        }

        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                SubmitMine();
            }
        }

        /// <summary>
        /// 내 닉네임을 서버로 보낸다. **내 캐릭터에서만 부른다.**
        ///
        /// 서버가 원본이므로 현재 캐릭터(<see cref="ICharacterService.CurrentCharacter"/>)에서
        /// 가져온다. 로그인을 거치지 않은 경로에서는 없을 수 있고, 그때는 보내지 않는다.
        /// 받는 쪽은 빈 값을 "아직 안 왔다" 로 다루므로 화면이 깨지지 않는다.
        /// </summary>
        private void SubmitMine()
        {
            if (submitted)
            {
                return;
            }

            submitted = true;

            CharacterDto current = AccountServiceLocator.IsReady && AccountServiceLocator.Characters != null
                ? AccountServiceLocator.Characters.CurrentCharacter
                : null;

            string nickname = current != null ? current.nickname : null;

            if (string.IsNullOrWhiteSpace(nickname))
            {
                Debug.LogWarning(
                    "[NetworkPlayerIdentity] 보낼 닉네임이 없습니다. " +
                    "로그인을 거치지 않은 경로일 수 있습니다.", this);
                return;
            }

            Rpc_SubmitNickname(nickname);
        }

        /// <summary>
        /// 클라이언트 → 서버. 서버가 다듬어 <see cref="Nickname"/> 에 기록한다.
        ///
        /// ⚠ 보낸 값을 그대로 믿지 않는다. 길이를 자르고 앞뒤 공백을 턴다.
        ///    이름은 남의 화면에 그대로 찍히는 값이라 서버가 한 번은 봐야 한다.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_SubmitNickname(string nickname, RpcInfo info = default)
        {
            // 이 오브젝트의 주인이 맞는지 한 번 더 본다. (NetworkPlayerAppearance 와 같은 이유)
            if (info.Source != Object.InputAuthority)
            {
                Debug.LogWarning(
                    $"[NetworkPlayerIdentity] 주인이 아닌 곳에서 온 닉네임을 버립니다. " +
                    $"보낸 쪽 {info.Source}, 주인 {Object.InputAuthority}", this);
                return;
            }

            string trimmed = (nickname ?? string.Empty).Trim();

            if (trimmed.Length > MaxNicknameLength)
            {
                trimmed = trimmed.Substring(0, MaxNicknameLength);
            }

            if (trimmed.Length == 0)
            {
                return;
            }

            Nickname = trimmed;
        }
    }
}
