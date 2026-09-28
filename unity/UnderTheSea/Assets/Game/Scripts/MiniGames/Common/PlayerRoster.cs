using System;
using System.Collections.Generic;

namespace MiniGames.Common
{
    /// <summary>이 사람이 지금 붙어 있는가. 네트워크가 붙으면 Runner 가 갱신한다.</summary>
    public enum ConnectionState
    {
        Connecting = 0,
        Connected = 1,
        Disconnected = 2,
    }

    /// <summary>
    /// 매칭에 들어와 있는 한 사람.
    ///
    /// 캐릭터 외형은 id 만 들고 있는다. 이 값을 읽어 실제 모델을 세우는 일은 스폰하는
    /// 쪽 몫이고, 여기서는 "누가 어떤 모습이기로 했는지" 만 기억한다.
    /// </summary>
    public sealed class PlayerEntry
    {
        public int PlayerId { get; }

        /// <summary>이 기기에서 조작하는 사람인가.</summary>
        public bool IsLocal { get; }

        public string DisplayName { get; internal set; }

        /// <summary>준비를 눌렀는가. 시작 조건은 이 수를 센다.</summary>
        public bool IsReady { get; internal set; }

        /// <summary>캐릭터 커스터마이즈 프리셋 id. 스폰할 때 읽는다. 비어 있으면 기본 외형.</summary>
        public string CharacterPresetId { get; internal set; }

        public ConnectionState ConnectionState { get; internal set; }

        internal PlayerEntry(int playerId, string displayName, bool isLocal, string characterPresetId)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            IsLocal = isLocal;
            CharacterPresetId = characterPresetId;
            ConnectionState = ConnectionState.Connected;
        }
    }

    /// <summary>
    /// 지금 같이 하기로 모인 사람들.
    ///
    /// 매칭 화면에서 센 인원을 미니게임 씬에서도 그대로 써야 한다. 그래서 씬을 넘어가도
    /// 살아남도록 static 으로 둔다. 들고 나는 것을 여기에 알리면 <see cref="Changed"/> 가
    /// 울리고, 화면과 흐름이 그때마다 다시 계산한다. 씬이 열릴 때 한 번 세고 마는
    /// 구조였다면 사람이 들어온 뒤에도 예전 숫자로 남았을 것이다.
    ///
    /// ── 서버·네트워크 담당자가 붙일 곳 ───────────────────────────────
    ///
    /// 이 파일은 <b>Photon/Fusion 을 전혀 알지 못한다.</b> 네트워크 콜백에서 아래 네 함수만
    /// 불러 주면 화면과 시작 조건이 알아서 따라온다. UI 코드는 건드릴 것이 없다.
    ///
    ///     플레이어 입장  → <see cref="RegisterPlayer"/>
    ///     플레이어 퇴장  → <see cref="UnregisterPlayer"/>
    ///     준비 상태 변경 → <see cref="SetPlayerReady"/>
    ///     연결 끊김/복구 → <see cref="SetConnectionState"/>
    ///     방 해산        → <see cref="ClearPlayers"/>
    ///
    /// 지금은 테스트 버튼이 같은 함수를 부르고 있다. 부르는 쪽만 바뀌면 된다.
    /// </summary>
    public static class PlayerRoster
    {
        private static readonly List<PlayerEntry> Entries = new();
        private static int nextTestId = 1;

        /// <summary>인원·준비 상태가 바뀔 때마다 울린다.</summary>
        public static event Action Changed;

        public static IReadOnlyList<PlayerEntry> All => Entries;

        /// <summary>지금 방에 있는 사람 수.</summary>
        public static int ActivePlayerCount => Entries.Count;

        /// <summary>준비를 마친 사람 수. 시작 조건은 이 수를 본다.</summary>
        public static int ReadyCount
        {
            get
            {
                int n = 0;
                foreach (PlayerEntry e in Entries)
                    if (e.IsReady) n++;
                return n;
            }
        }

        /// <summary>지금 고른 미니게임. 결과 화면과 [다시 하기] 가 이것을 본다.</summary>
        public static MiniGameConfig CurrentGame { get; private set; }

        public static void SelectGame(MiniGameConfig config)
        {
            CurrentGame = config;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------
        // 네트워크가 부를 것
        // ------------------------------------------------------------

        /// <summary>
        /// 사람을 넣는다. 이미 있는 id 면 새로 만들지 않고 이름·외형만 갱신한다 —
        /// 재접속이 새 사람으로 세어지면 정원이 넘치기 때문이다.
        /// </summary>
        public static PlayerEntry RegisterPlayer(
            int playerId, string displayName, bool isLocal = false,
            string characterPresetId = null, bool isReady = true)
        {
            PlayerEntry existing = ForId(playerId);
            if (existing != null)
            {
                existing.DisplayName = displayName;
                existing.CharacterPresetId = characterPresetId;
                existing.ConnectionState = ConnectionState.Connected;
                Changed?.Invoke();
                return existing;
            }

            var entry = new PlayerEntry(playerId, displayName, isLocal, characterPresetId)
            {
                IsReady = isReady,
            };
            Entries.Add(entry);
            Changed?.Invoke();
            return entry;
        }

        public static void UnregisterPlayer(int playerId)
        {
            int index = Entries.FindIndex(e => e.PlayerId == playerId);
            if (index < 0) return;

            Entries.RemoveAt(index);
            Changed?.Invoke();
        }

        public static void SetPlayerReady(int playerId, bool ready)
        {
            PlayerEntry entry = ForId(playerId);
            if (entry == null || entry.IsReady == ready) return;

            entry.IsReady = ready;
            Changed?.Invoke();
        }

        /// <summary><see cref="SetPlayerReady"/> 와 같다. 예전 이름.</summary>
        public static void SetReady(int playerId, bool ready) => SetPlayerReady(playerId, ready);

        public static void SetConnectionState(int playerId, ConnectionState state)
        {
            PlayerEntry entry = ForId(playerId);
            if (entry == null || entry.ConnectionState == state) return;

            entry.ConnectionState = state;
            Changed?.Invoke();
        }

        public static void SetCharacterPreset(int playerId, string characterPresetId)
        {
            PlayerEntry entry = ForId(playerId);
            if (entry == null) return;

            entry.CharacterPresetId = characterPresetId;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------
        // 읽기
        // ------------------------------------------------------------

        public static PlayerEntry ForId(int playerId) =>
            Entries.Find(e => e.PlayerId == playerId);

        /// <summary>슬롯 순서대로 n 번째 사람. 없으면 null.</summary>
        public static PlayerEntry AtSlot(int index) =>
            index >= 0 && index < Entries.Count ? Entries[index] : null;

        public static PlayerEntry Local => Entries.Find(e => e.IsLocal);

        /// <summary>전부 내보낸다. 매칭 취소, 방 해산, 로비 복귀에서 부른다.</summary>
        public static void ClearPlayers()
        {
            if (Entries.Count == 0) return;

            Entries.Clear();
            nextTestId = 1;
            Changed?.Invoke();
        }

        /// <summary><see cref="ClearPlayers"/> 와 같다. 예전 이름.</summary>
        public static void Clear() => ClearPlayers();

        // ------------------------------------------------------------
        // 테스트 전용 — 네트워크가 붙으면 쓰지 않는다
        // ------------------------------------------------------------

        /// <summary>번호를 알아서 붙여 한 명 넣는다.</summary>
        public static PlayerEntry RegisterNextTestPlayer(bool isLocal = false)
        {
            int id = nextTestId++;
            return RegisterPlayer(id, $"Player{id}", isLocal);
        }

        /// <summary>가장 마지막에 들어온 사람을 뺀다.</summary>
        public static void UnregisterLast()
        {
            if (Entries.Count == 0) return;

            Entries.RemoveAt(Entries.Count - 1);
            Changed?.Invoke();
        }
    }
}
