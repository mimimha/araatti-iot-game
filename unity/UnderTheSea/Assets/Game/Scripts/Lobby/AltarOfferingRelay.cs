using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnderTheSea.Account;
using UnityEngine;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 등록이 성공했다는 사실을 같은 로비 세션 전체에 알린다. 플레이어 오브젝트에 붙는다.
    /// (<c>LobbyChatRelay</c> 와 같은 자리, 같은 모양)
    ///
    /// <code>
    ///   POST /api/altar/offer 성공
    ///     → Rpc_NotifyOfferingSucceeded(requestId)   내 화면 → Fusion 서버
    ///     → Rpc_OfferingSucceeded(requestId)         Fusion 서버 → 세션 전체
    ///     → 받은 모든 화면에서 같은 판단으로 펄스 1회
    /// </code>
    ///
    /// <b>이 RPC 는 성격이 다른 두 가지를 나른다. 섞으면 안 된다.</b> (설계 9.3절)
    ///
    /// <code>
    ///   ① 상태가 바뀌었다는 사실  → AltarState.RequestRefresh()   지속 상태. 합쳐도 결과가 같다
    ///   ② 등록이 성공했다는 사건  → 파란 펄스 1회                  일회성. 합치면 사건이 사라진다
    /// </code>
    ///
    /// ⚠ <b>권위 상태 숫자를 싣지 않는다.</b> <c>totalOffered</c> · <c>recoveryPercent</c> ·
    ///    <c>myFragments</c> · 등록 수량을 RPC 로 나르면 클라이언트가 그 숫자를 조작할 수 있다.
    ///    받은 쪽이 <c>GET /api/altar/state</c> 로 스스로 확인한다.
    ///    <c>requestId</c> 만 싣는데, 그것은 권위 값이 아니라 <b>사건 식별자</b>다 — 이 값으로
    ///    바뀌는 DB 상태는 하나도 없다. 가짜 RPC 로 할 수 있는 최대치는 잘못된 순간에 연출이
    ///    한 번 터지는 것뿐이다. (설계 12.4절)
    ///
    /// ⚠ <b><c>LobbyChatRelay</c> 의 "0.5초 안에 두 번이면 무시" 를 베끼지 않았다.</b>
    ///    서로 다른 <c>requestId</c> 의 성공 2건은 정상이고, 시간만 보고 두 번째를 버리면
    ///    실제로 일어난 사건이 사라진다. 거르는 기준은 시간이 아니라
    ///    <b>(보낸 사람, requestId)</b> 다. (설계 9.3 · 12.6절)
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 9.3 · 12.4 · 12.6절
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class AltarOfferingRelay : NetworkBehaviour
    {
        /// <summary>
        /// 받아 줄 <c>requestId</c> 의 최대 길이. UI 가 만드는 것은 Guid 문자열(36자)이다.
        ///
        /// ⚠ 남이 보낸 값이 그대로 <see cref="handledRequestIds"/> 의 키가 되므로 상한을 둔다.
        ///    (<c>LobbyChatRelay</c> 가 채팅 길이를 서버에서 자르는 것과 같은 이유다)
        /// </summary>
        private const int MaxRequestIdLength = 64;

        /// <summary>
        /// 상태를 다시 물어보기 전에 기다리는 최대 시간(초).
        ///
        /// ⚠ 100명이 같은 순간에 같은 API 를 때리지 않게 흩는다. 펄스는 <b>지연하지 않는다</b> —
        ///    그것은 API 호출이 아니라 화면 연출이고, 등록한 순간에 보여야 의미가 있다. (설계 9.3절)
        /// </summary>
        private const float MaxRefreshDelaySeconds = 1f;

        /// <summary>
        /// 이 오브젝트(= 이 플레이어)가 보낸 것 중 <b>이미 펄스를 재생한</b> requestId.
        ///
        /// <b>왜 이것으로 (보낸 사람, requestId) 가 되는가.</b> <see cref="Rpc_OfferingSucceeded"/>
        /// 는 언제나 <b>보낸 사람의 오브젝트에서</b> 불린다. 플레이어마다 이 부품이 하나씩 있으니
        /// 이 집합은 곧 그 한 사람의 것이고, 안에 든 requestId 는 그 사람의 것이다.
        /// Fusion Dedicated Server 는 DB 의 user_id 를 모르므로 신원은 Fusion 쪽 것을 쓴다.
        /// (설계 12.4절)
        ///
        /// ⚠ 지나간 것을 되살리는 용도가 아니다. 같은 사건이 두 번 도착했을 때
        ///    두 번 재생하지 않기 위한 것뿐이다.
        /// </summary>
        private readonly HashSet<string> handledRequestIds = new HashSet<string>();

        private bool subscribed;

        // ------------------------------------------------------------
        // 내 등록 성공 → 세션 전체
        // ------------------------------------------------------------

        public override void Spawned()
        {
            // 보내는 것은 나뿐이다. 남의 캐릭터에도 이 부품이 있지만 받기만 한다.
            // (LobbyChatRelay.Spawned 와 같은 판단)
            if (!HasInputAuthority)
            {
                return;
            }

            local = this;

            IAltarService service = AccountServiceLocator.Altar;

            if (service == null)
            {
                Debug.LogWarning(
                    "[AltarVfx] 제단 서비스가 없어 등록 성공을 세션에 알릴 수 없습니다. " +
                    "다른 사람 화면에 펄스가 뜨지 않습니다.", this);
                return;
            }

            service.OnOfferResult += OnOfferResult;
            subscribed = true;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (ReferenceEquals(local, this))
            {
                local = null;
            }

            if (!subscribed)
            {
                return;
            }

            IAltarService service = AccountServiceLocator.Altar;

            if (service != null)
            {
                service.OnOfferResult -= OnOfferResult;
            }

            subscribed = false;
        }

        /// <summary>
        /// 내 등록 요청의 결과를 받는다.
        ///
        /// ⚠ <b>실패에는 아무것도 보내지 않는다.</b> <c>NOT_ENOUGH_FRAGMENTS</c> ·
        ///    <c>OFFERING_AMOUNT_CHANGED</c> · <c>OFFERING_CLOSED</c> 는 펄스 0회다.
        ///
        /// ⚠ <b><c>duplicate == true</c> 라고 건너뛰지 않는다.</b> 그것도 HTTP 성공이다.
        ///    첫 응답이 유실된 뒤의 재시도라면 클라이언트가 <b>처음</b> 확인하는 성공이고,
        ///    여기서 건너뛰면 "등록은 됐는데 연출이 없는" 상태가 된다. 같은 requestId 로
        ///    두 번 알려도 <see cref="Rpc_OfferingSucceeded"/> 가 한 번만 재생하므로
        ///    보내는 쪽은 성공이면 그냥 보낸다. (설계 12.4절, 19장 테스트 C-VFX Case 3 · 4)
        ///
        /// ⚠ <b>여기서 직접 펄스를 재생하지 않는다.</b> 그러면 등록한 본인 화면에서
        ///    로컬 1회 + broadcast 1회 = 2회가 된다. <c>RpcTargets.All</c> 은 보낸 사람도
        ///    포함하므로 모두가 <b>같은 한 경로</b>만 쓴다. (설계 12.4절)
        /// </summary>
        private void OnOfferResult(AltarOfferOutcome outcome)
        {
            if (outcome == null || !outcome.Success)
            {
                return;
            }

            if (string.IsNullOrEmpty(outcome.RequestId))
            {
                return;
            }

            Rpc_NotifyOfferingSucceeded(outcome.RequestId);
        }

        /// <summary>클라이언트 → 서버.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_NotifyOfferingSucceeded(string requestId, RpcInfo info = default)
        {
            // 남의 오브젝트로 보낸 것을 막는다. (LobbyChatRelay.Rpc_Send 와 같은 방어)
            if (info.Source != Object.InputAuthority)
            {
                return;
            }

            if (string.IsNullOrEmpty(requestId) || requestId.Length > MaxRequestIdLength)
            {
                return;
            }

            Rpc_OfferingSucceeded(requestId);
        }

        /// <summary>
        /// 서버 → 모두. <b>보낸 사람 본인도 포함된다.</b>
        ///
        /// 두 가지를 순서대로 한다. 앞의 것은 몇 번을 합쳐도 결과가 같고,
        /// 뒤의 것은 사건이라 합치면 사라진다.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_OfferingSucceeded(string requestId)
        {
            // ① 상태 — 같은 알림이 두 번 와도 다시 물어보는 것은 무해하다.
            //    AltarState 가 중복 요청을 합치고 등록 중이면 미뤄 준다.
            StartCoroutine(RefreshStateSoon());

            // ② 사건 — 이 (보낸 사람, requestId) 에 대한 펄스가 이미 나갔으면 여기서 끝낸다.
            if (!handledRequestIds.Add(requestId))
            {
                Debug.Log($"[AltarVfx] duplicate ignored requestId={requestId}", this);
                return;
            }

            AltarVfxController vfx = AltarVfxController.Current;

            if (vfx == null)
            {
                // 제단이 없는 화면(미니게임 · Dedicated Server)이다. 조용히 넘어간다.
                // (LobbyChatRelay 가 채팅창 없는 화면에서 줄을 버리는 것과 같다)
                return;
            }

            vfx.PlayOnce();
            Debug.Log($"[AltarVfx] pulse played requestId={requestId}", this);
        }

        // ------------------------------------------------------------
        // 🛠 상태만 바뀌었다 → 세션 전체 (펄스 없음)
        // ------------------------------------------------------------

        /// <summary>
        /// 내 플레이어의 relay. <see cref="AnnounceStateChanged"/> 가 쓴다.
        /// 남의 캐릭터에 붙은 것은 보내지 못하므로 담지 않는다.
        /// </summary>
        private static AltarOfferingRelay local;

        /// <summary>
        /// <b>봉헌이 아닌 길로</b> 제단 상태가 바뀌었다고 세션 전체에 알린다.
        /// 지금은 로비 개발자 모드의 섬 회복도 +1 · -1 (<see cref="LobbyDevMode"/>) 이 부른다.
        ///
        /// 받은 화면은 <see cref="Rpc_OfferingSucceeded"/> 의 ① 과 같이 상태만 다시 묻는다.
        /// <b>펄스는 없다</b> — 등록이라는 사건이 없었다.
        ///
        /// ⚠ 이 RPC 도 숫자를 싣지 않는다. 받은 쪽이 <c>GET /api/altar/state</c> 로 확인한다.
        ///    가짜로 보내 봐야 모두가 한 번 더 조회할 뿐이다.
        /// </summary>
        /// <returns>보냈으면 true. 로비 세션에 붙어 있지 않으면 false.</returns>
        public static bool AnnounceStateChanged()
        {
            if (local == null || local.Object == null || !local.Object.IsValid)
            {
                return false;
            }

            local.Rpc_NotifyStateChanged();
            return true;
        }

        /// <summary>클라이언트 → 서버.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_NotifyStateChanged(RpcInfo info = default)
        {
            if (info.Source != Object.InputAuthority)
            {
                return;
            }

            Rpc_StateChanged();
        }

        /// <summary>서버 → 모두. 보낸 사람 본인도 포함된다(이미 최신이지만 다시 물어도 무해하다).</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_StateChanged()
        {
            StartCoroutine(RefreshStateSoon());
        }

        /// <summary>
        /// 조금 기다린 뒤 상태를 다시 물어본다.
        ///
        /// ⚠ 이 조회가 펄스를 만들지 않는다. 조회는 사건이 아니다. (설계 12.5절)
        /// </summary>
        private IEnumerator RefreshStateSoon()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, MaxRefreshDelaySeconds));

            AltarState.RequestRefresh();
        }
    }
}
