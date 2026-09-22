using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 미니게임 Dedicated Server 가 <b>지금 사람을 받아도 되는지</b> 판단해 거절하는 마지막 관문.
    ///
    /// <b>왜 필요한가.</b> 지금은 판이 끝난 서버에도 그냥 들어가진다. 들어가면 곧바로 결과 화면이
    /// 뜨고 아무것도 못 한다. 게다가 그 사람이 있는 동안 <b>서버가 다음 판으로 되돌아가지 못한다</b> —
    /// 리셋 조건이 "아무도 없을 때" 이기 때문이다. 한 사람이 못 나가면 서버가 통째로 묶인다.
    ///
    /// <b>이것이 하지 않는 일.</b> 상태를 보관하지도, 판단하지도 않는다. 여러 서버를 관리하지도 않는다.
    /// <b>물어보고, 아니면 거절한다.</b> 그게 전부다.
    ///
    /// <code>
    ///   Admission   접속 요청이 왔다 → 게임에게 물어봄 → 아니라면 Refuse()
    ///   각 게임      "지금 받아도 되나" 에 답한다            (IMiniGameAdmissionSource)
    /// </code>
    ///
    /// <b>왜 상태를 복사해 두지 않는가.</b> 복사본을 두면 <b>갱신 순서를 지켜야</b> 한다. 한 번만
    /// 어긋나도 그 틈으로 사람이 들어오는데, 오류가 나지 않아 알아채기 어렵다. 접속 요청은 초당
    /// 몇 번씩 오는 일이 아니므로 <b>그때그때 물어보면</b> 낡을 일이 없고 순서 규칙도 필요 없다.
    ///
    /// <b>나중에.</b> DS Pool 과 매칭이 생기면 <b>이 판정을 매칭이 먼저</b> 한다. 그래도 이 관문은
    /// 남아야 한다 — 매칭이 자리를 배정한 순간과 실제로 접속하는 순간 사이에 다른 사람이 먼저 찰
    /// 수 있기 때문이다. 그때 이것이 <b>경쟁을 막는 최종 방어선</b>이 된다.
    ///
    /// ⚠ <b>Lobby 에는 붙이지 않는다.</b> Lobby 는 언제나 사람을 받아야 한다.
    /// </summary>
    public sealed class MiniGameAdmission : MonoBehaviour, INetworkRunnerCallbacks
    {
        private MiniGameConfig config;
        private IMiniGameAdmissionSource source;

        /// <summary>
        /// 정원의 출처를 넘겨 준다. <b>게임마다 다르므로 코드에 박지 않는다.</b>
        ///
        /// 같은 값을 <c>StartGameArgs.PlayerCount</c> 에도 줘서 Photon 이 먼저 막게 한다.
        /// 여기만 막으면 <b>동시에 두드리는 경쟁</b>을 놓친다 — 승인됐지만 아직 합류하지 않은
        /// 사람은 <c>ActivePlayers</c> 에 안 잡히기 때문이다.
        /// </summary>
        public void Configure(MiniGameConfig gameConfig)
        {
            config = gameConfig;

            Debug.Log(
                "[입장 관리] 준비됐습니다. " +
                (config != null
                    ? $"정원 {config.MaxPlayers}명 ({config.DisplayName})"
                    : "⚠ 설정 에셋이 없어 정원을 확인하지 못합니다. 상태만 봅니다."));
        }

        /// <summary>
        /// Fusion 이 접속 요청을 받았을 때 서버에서 불린다. <b>여기서 거절하면 스폰까지 가지 않는다.</b>
        ///
        /// ⚠ <b>이 함수가 안 불리면 아무 일도 일어나지 않는다. 오류도 나지 않는다.</b>
        ///    그래서 불릴 때마다 로그를 남긴다. QA 에서 이 줄이 안 보이면 등록이 안 된 것이다.
        ///    (<c>ShipCoopPlayerSpawner</c> 가 같은 함정을 주석으로 남겨 두었다)
        /// </summary>
        void INetworkRunnerCallbacks.OnConnectRequest(
            NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            int now = runner != null ? runner.ActivePlayers.Count() : 0;

            // 들어오는 사람이 "이번 판은 몇 명" 을 들고 온다. 첫 사람의 값이 이 판을 정한다.
            // 로비 서버가 알려 줄 길이 없어서(둘은 서로 접속하지 않는다) 이렇게 나른다.
            MatchCrew.Remember(MatchCrewToken.Clamp(MatchCrewToken.Read(token), config));

            // ⚠ 정원은 **이번 판의 인원**이다. 게임의 최대 정원이 아니다.
            //    광산은 정원이 4명이어도 3인 판이면 네 번째 사람은 들어오면 안 된다.
            //    매칭이 정해 준 것이 없을 때만 예전처럼 최대 정원으로 막는다.
            int max = MatchCrew.Assigned > 0
                ? MatchCrew.Assigned
                : (config != null ? config.MaxPlayers : 0);

            if (!TryFindSource(out string whyNoSource))
            {
                // 물어볼 상대가 없다. **막지 않는다.** 조용히 막히는 쪽이 더 위험하다 —
                // 단독 테스트 씬이나 아직 설정이 덜 된 구성이 이유 없이 안 들어가지게 된다.
                Debug.LogWarning($"[입장 관리] {whyNoSource} 그대로 받습니다. (현재 {now}명)");
                request.Accept();
                return;
            }

            if (!source.CanAdmitNow(out string why))
            {
                Debug.Log($"[입장 관리] 거절 — {why} (현재 {now}명" + (max > 0 ? $" / 정원 {max}명)" : ")"));
                request.Refuse();
                return;
            }

            if (max > 0 && now >= max)
            {
                Debug.Log($"[입장 관리] 거절 — 정원이 찼습니다. ({now}/{max}명)");
                request.Refuse();
                return;
            }

            Debug.Log($"[입장 관리] 받았습니다. (현재 {now}명" + (max > 0 ? $" / 정원 {max}명)" : ")"));
            request.Accept();
        }

        /// <summary>
        /// 물어볼 상대를 찾는다. 러너가 연 게임 씬에 있으므로 시작할 때는 아직 없을 수 있다.
        ///
        /// 접속 요청은 자주 오는 일이 아니라서 찾는 비용은 문제가 되지 않는다.
        /// 한 번 찾으면 들고 있되, 씬이 바뀌어 사라졌으면 다시 찾는다.
        /// </summary>
        private bool TryFindSource(out string why)
        {
            if (source is MonoBehaviour alive && alive != null)
            {
                why = null;
                return true;
            }

            source = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OfType<IMiniGameAdmissionSource>()
                .FirstOrDefault();

            why = source == null ? "이 서버에서 입장 여부를 답할 게임을 찾지 못했습니다." : null;
            return source != null;
        }

        // ── 나머지 콜백은 이 부품의 일이 아니다 ───────────────────────────────
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        /// <summary>
        /// 마지막 사람이 나가면 "몇 명짜리 판" 을 잊는다.
        ///
        /// ⚠ <b>안 잊으면 다음 팀이 앞 팀의 인원으로 시작한다.</b> 2인 판이 끝난 방에
        ///    4인 팀이 들어오면 둘만 모여도 출발해 버린다. 서버는 판을 거듭 쓰는 자원이라
        ///    한 판이 남긴 것을 다음 판이 물려받지 않게 해야 한다.
        /// </summary>
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner == null) return;

            // 이 콜백은 그 사람이 빠지기 전에 불릴 수 있다. 자기 자신을 빼고 센다.
            int left = runner.ActivePlayers.Count(p => p != player);
            if (left > 0) return;

            MatchCrew.Forget();
        }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }

    /// <summary>
    /// "지금 사람을 받아도 되는가" 에 답하는 쪽. 각 미니게임의 <b>서버 권위 상태</b>를 들고 있는
    /// 부품이 구현한다. (ShipCoop 은 <c>ShipCoopStateSync</c>)
    ///
    /// <b>계약을 이것 하나로 좁힌 이유.</b> 공통 부품이 게임의 단계 이름(<c>Sailed</c> · <c>Sunk</c>)을
    /// 알기 시작하면 그 순간 공통이 아니게 된다. 판단은 각 게임이 하고, 공통 쪽은 <b>결과만</b> 받는다.
    /// </summary>
    public interface IMiniGameAdmissionSource
    {
        /// <summary>
        /// 지금 받아도 되는가.
        /// </summary>
        /// <param name="why">거절할 때 그 이유. 서버 로그에만 쓴다 —
        /// Fusion 의 <c>Refuse()</c> 는 사유를 실어 보내지 못한다.</param>
        bool CanAdmitNow(out string why);
    }
}
