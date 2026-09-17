using System.Text;
using Fusion;
using UnityEngine;

namespace UnderTheSea.Network
{
    /// <summary>
    /// 채팅 한 줄을 모두에게 돌린다. 플레이어 오브젝트에 붙는다.
    ///
    /// <code>
    ///   내 화면        Rpc_Send   ──▶  서버        Rpc_Receive  ──▶  모두의 화면
    ///   (보낸 글)      (주인만)         (다듬는다)    (서버만)          (LobbyChatView)
    /// </code>
    ///
    /// <b>왜 서버를 한 번 거치는가.</b> 클라이언트가 곧바로 모두에게 뿌리면
    /// 길이 · 도배 · 태그를 아무도 못 막는다. 남의 화면에 그대로 찍히는 값이라
    /// 한 번은 서버가 봐야 한다.
    ///
    /// <b>이름은 어디서 오는가.</b> <see cref="NetworkPlayerIdentity"/> 가 캐릭터에
    /// 올려 둔 것을 서버가 읽는다. 보낸 쪽이 자기 이름을 같이 보내지 않는다 —
    /// 그러면 남의 이름을 사칭할 수 있다.
    ///
    /// <b>확성기(전체 공지).</b> 같은 길로 가되 <c>global</c> 표시가 하나 붙는다.
    /// 서버가 그 표시를 보고 도배 간격을 훨씬 길게 잰 뒤, 받는 쪽에서
    /// <see cref="GlobalAnnouncementView"/> 에 한 번 더 넘겨 화면 위에 띄운다.
    /// 공지도 채팅 목록과 말풍선에는 똑같이 남는다 — 놓친 사람이 나중에 찾아볼 수 있어야 한다.
    ///
    /// ⚠ 이 컴포넌트는 화면을 모른다. 받은 줄을 <see cref="LobbyChatView.Current"/> 에
    ///    넘길 뿐이고, 채팅창이 없으면 (미니게임 씬 등) 조용히 버린다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class LobbyChatRelay : NetworkBehaviour
    {
        /// <summary>한 번에 보낼 수 있는 글자 수. 화면 칸과 맞춰 둔다.</summary>
        private const int MaxLength = 100;

        /// <summary>도배를 막는 최소 간격(초). 서버가 잰다.</summary>
        private const float MinInterval = 0.5f;

        /// <summary>
        /// 전체 공지의 최소 간격(초). 평소 채팅보다 훨씬 길다.
        ///
        /// ⚠ 공지는 **남의 화면 한가운데**에 뜬다. 0.5초마다 띄울 수 있으면
        ///    한 사람이 로비 전체를 못 쓰게 만들 수 있다.
        ///    채팅과 같은 잣대로 재면 안 된다.
        /// </summary>
        private const float GlobalMinInterval = 15f;

        private float nextAllowedTime;

        private float nextGlobalAllowedTime;

        private LobbyChatView boundView;

        public override void Spawned()
        {
            // 내 캐릭터만 화면과 이어 둔다. 남의 캐릭터에도 이 컴포넌트가 있지만
            // 보내는 것은 나뿐이다. 받는 것은 어느 캐릭터의 RPC 로도 들어온다.
            if (!HasInputAuthority)
            {
                return;
            }

            boundView = LobbyChatView.Current;

            if (boundView == null)
            {
                // 채팅창이 아직 안 만들어졌을 수 있다. 그때는 나중에 다시 본다.
                return;
            }

            boundView.Submitted += Say;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (boundView != null)
            {
                boundView.Submitted -= Say;
                boundView = null;
            }
        }

        private void Update()
        {
            // 채팅창이 캐릭터보다 늦게 생기는 경우를 따라잡는다.
            if (!HasInputAuthority || boundView != null)
            {
                return;
            }

            LobbyChatView view = LobbyChatView.Current;

            if (view == null)
            {
                return;
            }

            boundView = view;
            boundView.Submitted += Say;
        }

        /// <summary>내가 친 글을 보낸다. 화면이 부른다.</summary>
        private void Say(string message, bool global)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Rpc_Send(message, global);
        }

        /// <summary>클라이언트 → 서버.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_Send(string message, bool global, RpcInfo info = default)
        {
            if (info.Source != Object.InputAuthority)
            {
                return;
            }

            // 도배를 막는다. 잰 시각은 서버 것이라 클라이언트가 못 속인다.
            if (Time.time < nextAllowedTime)
            {
                return;
            }

            // ⚠ 공지 간격이 안 찼으면 **평범한 채팅으로 낮춰서** 보낸다. 통째로 버리지 않는다.
            //    글자를 다 쳐서 보냈는데 아무 일도 안 일어나면 눌린 줄 알고 다시 친다.
            //    말은 전해지고 띠만 안 뜨는 편이 낫다.
            if (global && Time.time < nextGlobalAllowedTime)
            {
                global = false;
            }

            nextAllowedTime = Time.time + MinInterval;

            if (global)
            {
                nextGlobalAllowedTime = Time.time + GlobalMinInterval;
            }

            string cleaned = Sanitize(message);

            if (cleaned.Length == 0)
            {
                return;
            }

            // 이름은 서버가 붙인다. 보낸 쪽 말을 믿으면 사칭할 수 있다.
            var identity = GetComponent<NetworkPlayerIdentity>();
            string speaker = identity != null ? identity.DisplayName : "이름 없음";

            Rpc_Receive(speaker, cleaned, global);
        }

        /// <summary>서버 → 모두.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_Receive(string speaker, string message, bool global)
        {
            // 말한 사람 머리 위에 띄운다.
            //
            // 이 RPC 는 **말한 사람의 오브젝트에서** 불린다. 그래서 여기서 GetComponent 하면
            // 곧 그 사람이다. 누가 말했는지 따로 찾을 필요가 없다.
            var bubble = GetComponent<PlayerSpeechBubble>();

            if (bubble != null)
            {
                bubble.Show(speaker, message);
            }

            if (global && GlobalAnnouncementView.Current != null)
            {
                GlobalAnnouncementView.Current.Show(speaker, message);
            }

            LobbyChatView view = LobbyChatView.Current;

            if (view == null)
            {
                // 채팅창이 없는 화면(미니게임 등)에서는 목록만 건너뛴다.
                return;
            }

            view.Append(speaker, message);
        }

        /// <summary>
        /// 남의 화면에 찍히기 전에 다듬는다.
        ///
        /// ⚠ <b>꺾쇠를 지우는 것이 핵심이다.</b> 메시지는 TMP 로 그려지는데,
        ///    누가 <c>&lt;color=red&gt;</c> 나 <c>&lt;size=400%&gt;</c> 를 치면
        ///    그대로 먹혀서 남의 화면을 망가뜨릴 수 있다. 채팅에 꺾쇠가 필요한 일은
        ///    드물고, 막는 값은 크다.
        ///
        /// 줄바꿈도 없앤다. 한 줄에 여러 줄을 밀어 넣어 목록을 밀어내는 것을 막는다.
        /// </summary>
        private static string Sanitize(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(message.Length);

            foreach (char c in message)
            {
                if (c == '<' || c == '>')
                {
                    continue;
                }

                // 줄바꿈 · 탭은 공백 하나로 접는다.
                builder.Append(char.IsControl(c) ? ' ' : c);
            }

            string cleaned = builder.ToString().Trim();

            if (cleaned.Length > MaxLength)
            {
                cleaned = cleaned.Substring(0, MaxLength);
            }

            return cleaned;
        }
    }
}
