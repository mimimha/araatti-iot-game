using MiniGames.Common.UI;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// **포탈을 누른 순간부터 미니게임으로 떠날 때까지의 화면 순서.**
    ///
    /// <code>
    ///   포탈 F   →  인원 선택      몇 명이서 할지 고른다
    ///            →  로비 서버에 신청
    ///            →  매칭 판        같은 인원을 고른 사람이 모이는 것을 본다
    ///            →  출발           서버가 방을 정해 주면 그 방으로 간다
    ///   [매칭 취소]                화면만 닫는다. 로비에 그대로 서 있는다
    /// </code>
    ///
    /// <b>규칙은 하나도 없다.</b> 누구와 묶일지, 어느 방으로 갈지는 전부 로비 서버가 정하고
    /// <see cref="PlayerMatchRelay"/> 로 알려 준다. 이 부품은 그 소식에 맞춰 화면을 바꾼다.
    ///
    /// ⚠ <b>화면이 판을 시작시키지 않는다.</b> 예전 매칭 화면에는 [게임 시작] 버튼과
    ///    "30초 후 자동 시작" 시계가 있었다. 둘 다 <c>ServerDriven</c> 에서 꺼진다 —
    ///    서버가 정하는 판에서는 눌러도 아무 일이 없어서 거짓말이 되기 때문이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchScreenFlow : MonoBehaviour
    {
        private const string HostName = "[매칭 화면 흐름]";

        private static MatchScreenFlow instance;

        /// <summary>지금 고르고 있는 게임. 취소하거나 출발할 때까지 들고 있는다.</summary>
        private MiniGameConfig config;

        /// <summary>이번 판의 인원. 고르기 전에는 0.</summary>
        private int crew;

        /// <summary>
        /// **포탈이 부르는 곳.** 인원 선택부터 시작한다.
        /// </summary>
        public static void Begin(MiniGameConfig config)
        {
            if (config == null) return;

            Ensure().StartPicking(config);
        }

        private static MatchScreenFlow Ensure()
        {
            if (instance != null) return instance;

            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            instance = host.AddComponent<MatchScreenFlow>();
            return instance;
        }

        private void OnEnable()
        {
            PlayerMatchRelay.StateChanged += OnStateChanged;
            PlayerMatchRelay.Launching += OnLaunching;
        }

        private void OnDisable()
        {
            PlayerMatchRelay.StateChanged -= OnStateChanged;
            PlayerMatchRelay.Launching -= OnLaunching;

            if (boundFlow != null)
            {
                boundFlow.MatchCancelled -= Cancel;
                boundFlow = null;
            }
        }

        // ───────────────────────────── 화면 순서 ─────────────────────────────

        private void StartPicking(MiniGameConfig next)
        {
            config = next;
            crew = 0;

            CrewPickerScreen.Open(config, Request, Cancel);
        }

        private void Request(int chosenCrew)
        {
            crew = chosenCrew;

            // 판을 먼저 띄워 둔다. 서버 답이 오기 전에도 몇 명짜리 판인지는 보여야 한다.
            PlayerRoster.ClearPlayers();
            if (CommonMatchingUI.Current != null)
            {
                CommonMatchingUI.Current.ShowServerMatch(config, crew);
                CommonMatchingUI.Current.SetServerStatus("매칭을 신청하는 중…");
                BindPanelCancel();
            }

            PlayerMatchRelay.Request(config, crew);
        }

        /// <summary>
        /// **매칭 판의 [매칭 취소] 도 이쪽 취소로 모은다.**
        ///
        /// 화면에 취소 버튼이 두 개 있다 — 인원 선택 화면의 것과 매칭 판의 것.
        /// 매칭 판 쪽은 서연님이 만든 <see cref="MatchFlow.CancelMatch"/> 로 이어져 있는데,
        /// 그것만으로는 <b>서버에 취소를 알리지 못하고 화면도 닫히지 않는다.</b>
        ///
        /// ⚠ 예전에는 <c>MatchFlowController.returnToLobbyOnCancel</c> 이 켜져 있어서
        ///    취소하면 <b>로비 씬을 다시 로드</b>했다. 이미 로비에 서 있는데도 타륜 로딩이
        ///    뜨고 재접속했다. 그 설정은 "매칭 창이 미니게임 씬 안에서 돌 때" 를 위한 것이라
        ///    프리팹에서 껐다. 대신 닫는 일을 여기서 맡는다.
        /// </summary>
        private void BindPanelCancel()
        {
            MatchFlow flow = CommonMatchingUI.Current.Flow;
            if (flow == null || flow == boundFlow) return;

            if (boundFlow != null) boundFlow.MatchCancelled -= Cancel;
            boundFlow = flow;
            boundFlow.MatchCancelled += Cancel;
        }

        /// <summary>이미 이어 둔 흐름. 같은 것에 두 번 붙지 않으려고 기억한다.</summary>
        private MatchFlow boundFlow;

        /// <summary>[매칭 취소]. 화면만 닫는다. 로비에 그대로 서 있는다.</summary>
        public static void Cancel()
        {
            PlayerMatchRelay.Cancel();
            CrewPickerScreen.Close();

            if (CommonMatchingUI.Current != null) CommonMatchingUI.Current.Hide();
            PlayerRoster.ClearPlayers();

            if (instance != null)
            {
                instance.config = null;
                instance.crew = 0;
            }
        }

        // ───────────────────────────── 서버 소식 ─────────────────────────────

        private void OnStateChanged(LobbyMatchmaker.Phase phase, MiniGameId game, int partyCrew, int filled)
        {
            if (phase == LobbyMatchmaker.Phase.None)
            {
                // 취소가 받아들여졌다. 화면은 Cancel 이 이미 닫았다.
                return;
            }

            if (config == null || config.GameId != game) return;

            crew = partyCrew;

            if (CommonMatchingUI.Current == null) return;

            CommonMatchingUI.Current.ShowServerMatch(config, crew);
            FillRoster(filled);

            // ⚠ "빈 서버를 기다리는 중" 과 "사람을 기다리는 중" 은 다른 상황이다.
            //    둘을 같은 문구로 뭉뚱그리면, 아무도 안 오는 건지 서버가 없는 건지
            //    알 수가 없어서 계속 기다리게 된다.
            string status = phase == LobbyMatchmaker.Phase.Waiting
                ? "빈 서버를 기다리는 중…"
                : $"{crew}명 중 {filled}명";

            CommonMatchingUI.Current.SetServerStatus(status);
        }

        private void OnLaunching(MiniGameId game, int partyCrew, string session)
        {
            if (config == null || config.GameId != game) return;

            Debug.Log($"[매칭] {config.DisplayName} {partyCrew}인 판이 출발합니다. 방 \"{session}\"");

            if (CommonMatchingUI.Current != null)
            {
                CommonMatchingUI.Current.SetServerStatus("곧 시작합니다");
                CommonMatchingUI.Current.ShowQueueLoading(config);
            }

            // 방 이름과 인원을 함께 넘긴다. 인원은 접속할 때 쪽지에 실려 미니게임 서버로
            // 간다 — 로비 서버가 그쪽에 직접 알려 줄 길이 없다.
            MiniGameSessionRequest.Pending = session;
            MiniGameSessionRequest.Crew = partyCrew;

            MiniGameTransition.Enter(config.SceneName, session, OnEnterFailed);
        }

        private void OnEnterFailed(string reason)
        {
            Debug.LogError($"[매칭] 입장 실패 — {reason}");

            if (CommonMatchingUI.Current != null) CommonMatchingUI.Current.Hide();
            PlayerRoster.ClearPlayers();
            config = null;
            crew = 0;
        }

        /// <summary>
        /// 판에 사람 수만큼 자리를 채운다.
        ///
        /// ⚠ <b>이름은 아직 오지 않는다.</b> 서버는 지금 인원만 보낸다. 누가 있는지까지
        ///    보여 주려면 신청할 때 닉네임을 같이 실어야 하는데, 그 전에 인원이 맞게
        ///    도는지를 먼저 확인하려고 수만 쓴다.
        /// </summary>
        private void FillRoster(int filled)
        {
            PlayerRoster.ClearPlayers();

            for (int i = 0; i < filled; i++)
            {
                bool mine = i == 0;
                PlayerRoster.RegisterPlayer(
                    playerId: i + 1,
                    displayName: mine ? SceneFlow.Nickname : "참가자",
                    isLocal: mine);
            }
        }
    }
}
