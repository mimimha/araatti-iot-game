using System.Text;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// <see cref="PlayerRoster"/> 를 하이어라키에 보이게 하는 자리. Systems/PlayerRoster 에 붙어 있다.
    ///
    /// 명단 자체는 static 이라 씬을 넘어가도 살아남는다 (매칭 화면에서 센 인원을 미니게임
    /// 씬이 그대로 써야 하므로). 그 대신 인스펙터에서 보이지 않고 UnityEvent 로 부를 수도
    /// 없어서, 같은 진입 함수를 인스턴스 함수로 한 번 더 열어 두고 지금 명단을 인스펙터에
    /// 적어 준다. 하는 일은 전부 <see cref="PlayerRoster"/> 로 넘긴다.
    ///
    /// 네트워크 담당자는 이 컴포넌트 참조를 잡아도 되고, <see cref="PlayerRoster"/> 를 바로 불러도 된다.
    /// </summary>
    public sealed class PlayerRosterHost : MonoBehaviour
    {
        [Tooltip("지금 명단. 읽기 전용 — 코드가 채운다.")]
        [SerializeField, TextArea(3, 8)] private string snapshot;

        public int ActivePlayerCount => PlayerRoster.ActivePlayerCount;
        public int ReadyCount => PlayerRoster.ReadyCount;

        private void OnEnable()
        {
            PlayerRoster.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => PlayerRoster.Changed -= Refresh;

        // ------------------------------------------------------------
        // 네트워크가 부를 것 — PlayerRoster 와 같은 이름, 같은 뜻
        // ------------------------------------------------------------

        public PlayerEntry RegisterPlayer(int playerId, string displayName, bool isLocal = false,
            string characterPresetId = null, bool isReady = true) =>
            PlayerRoster.RegisterPlayer(playerId, displayName, isLocal, characterPresetId, isReady);

        public void UnregisterPlayer(int playerId) => PlayerRoster.UnregisterPlayer(playerId);

        public void SetPlayerReady(int playerId, bool ready) => PlayerRoster.SetPlayerReady(playerId, ready);

        public void SetConnectionState(int playerId, ConnectionState state) =>
            PlayerRoster.SetConnectionState(playerId, state);

        public void ClearPlayers() => PlayerRoster.ClearPlayers();

        // ------------------------------------------------------------

        private void Refresh()
        {
            var sb = new StringBuilder();
            sb.Append(PlayerRoster.ActivePlayerCount).Append("명 · 준비 ").Append(PlayerRoster.ReadyCount).AppendLine();

            foreach (PlayerEntry e in PlayerRoster.All)
            {
                sb.Append('#').Append(e.PlayerId).Append(' ').Append(e.DisplayName);
                if (e.IsLocal) sb.Append(" (나)");
                sb.Append(e.IsReady ? "  READY" : "  WAIT");
                sb.Append("  ").Append(e.ConnectionState);
                if (!string.IsNullOrEmpty(e.CharacterPresetId)) sb.Append("  preset=").Append(e.CharacterPresetId);
                sb.AppendLine();
            }

            snapshot = sb.ToString().TrimEnd();
        }
    }
}
