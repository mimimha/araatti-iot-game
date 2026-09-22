using System;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>결과가 클라이언트 임시 계산인지, 서버가 확정한 값인지 구분한다.</summary>
    public enum MiniGameResultOrigin
    {
        Local,
        Server,
    }

    /// <summary>
    /// 미니게임/서버와 공통 결과 화면 사이의 씬 독립 경계.
    /// 결과 화면이 아직 없는 미니게임 씬에서 결과가 먼저 와도 한 건을 보관했다가,
    /// MatchFlowController가 준비되는 순간 전달한다.
    /// </summary>
    public static class MiniGameResultGateway
    {
        private static Action<MiniGameResult, MiniGameResultOrigin> receiver;
        private static MiniGameResult pendingResult;
        private static MiniGameResultOrigin pendingOrigin;
        private static bool hasPending;

        public static bool HasPending => hasPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            receiver = null;
            pendingResult = default;
            pendingOrigin = MiniGameResultOrigin.Local;
            hasPending = false;
        }

        /// <summary>서버 없이 돌리는 테스트나 로컬 게임 종료 결과를 제출한다.</summary>
        public static void SubmitLocal(in MiniGameResult result) =>
            Submit(result, MiniGameResultOrigin.Local);

        /// <summary>서버가 검증·보상 판정까지 끝낸 확정 결과를 제출한다.</summary>
        public static void SubmitAuthoritative(in MiniGameResult result) =>
            Submit(result, MiniGameResultOrigin.Server);

        internal static void Register(Action<MiniGameResult, MiniGameResultOrigin> next)
        {
            if (next == null) return;
            if (receiver != null && receiver != next)
                Debug.LogWarning("[MiniGameResultGateway] 결과 수신 화면이 두 개 등록됐습니다.");

            receiver = next;
            if (!hasPending) return;

            MiniGameResult result = pendingResult;
            MiniGameResultOrigin origin = pendingOrigin;
            hasPending = false;
            receiver.Invoke(result, origin);
        }

        internal static void Unregister(Action<MiniGameResult, MiniGameResultOrigin> current)
        {
            if (receiver == current) receiver = null;
        }

        private static void Submit(in MiniGameResult result, MiniGameResultOrigin origin)
        {
            if (receiver != null)
            {
                receiver.Invoke(result, origin);
                return;
            }

            pendingResult = result;
            pendingOrigin = origin;
            hasPending = true;
        }
    }
}
