using System;
using System.Collections.Generic;
using Fusion;
using MiniGames.Common;
using UnityEngine;

/// <summary>
/// **누구를 누구와 묶어 어느 방으로 보낼지 정한다.** 로비 DS 에서만 돈다.
///
/// <code>
///   신청  게임 + 인원          →  같은 인원을 기다리는 일행이 있으면 합류
///                                 없으면 빈 방을 잡아 새 일행을 연다
///                                 빈 방이 없으면 줄을 선다
///   충원  인원이 정확히 차면    →  다 같이 그 방으로 출발
///   취소  언제든                →  일행에서 빠진다. 마지막 한 명이 나가면 방을 돌려준다
/// </code>
///
/// <b>네트워크를 모른다.</b> 여기는 규칙만 있다. RPC 로 받고 보내는 일은
/// <see cref="PlayerMatchRelay"/> 가 한다. 그래서 규칙을 읽을 때 Fusion 을 몰라도 된다.
///
/// <b>방장이 없다.</b> 인원이 차면 자동으로 출발하므로 시작 버튼을 누를 사람이 필요 없다.
/// 방장이 있으면 방장이 나갈 때 누구에게 넘길지, 안 누르고 버티면 어쩔지를 전부 정해야 한다.
///
/// ⚠ <b>인원은 범위가 아니라 딱 하나다.</b> 3인을 고른 사람은 3인을 고른 사람하고만 묶인다.
///    "2~4명" 처럼 범위로 받으면 2명이 모인 순간 출발해 버려서, 4명이 하고 싶던 사람이
///    원하지 않는 판에 끌려 들어간다.
///
/// ⚠ <b>방은 일행을 열 때 잡는다.</b> 인원이 다 찬 뒤에 잡으면 "4명 다 모였는데 왜 안 가"
///    가 생긴다. 대신 한 게임에 방이 <see cref="DsPool.SizePerGame"/> 대뿐이라, 서로 다른
///    인원으로 일행 두 개가 열려 있으면 세 번째 사람은 줄을 서게 된다. 그때 보여 줄 화면이
///    "빈 서버를 기다리는 중" 이다.
/// </summary>
public sealed class LobbyMatchmaker
{
    /// <summary>지금 이 사람이 매칭의 어디에 있는가.</summary>
    public enum Phase
    {
        /// <summary>매칭에 들어와 있지 않다.</summary>
        None = 0,

        /// <summary>일행은 만들었는데 들어갈 방이 없어 기다린다.</summary>
        Waiting = 1,

        /// <summary>방을 잡았고 사람을 모으는 중이다.</summary>
        Gathering = 2,

        /// <summary>인원이 다 찼다. 곧 출발한다.</summary>
        Launching = 3,
    }

    /// <summary>같은 방으로 함께 갈 사람들.</summary>
    public sealed class Party
    {
        public MiniGameId Game;

        /// <summary>이 인원이 <b>정확히</b> 차면 출발한다.</summary>
        public int Crew;

        /// <summary>배정받은 방. 비어 있으면 아직 못 받았다.</summary>
        public string Session = string.Empty;

        public readonly List<PlayerRef> Members = new List<PlayerRef>();

        /// <summary>이미 출발시켰는가. 출발한 일행은 새 사람을 받지 않는다.</summary>
        public bool Launched;

        public bool Full => Members.Count >= Crew;

        /// <summary>새 사람을 받을 수 있는가.</summary>
        public bool Accepting => !Launched && !Full && Session.Length != 0;
    }

    /// <summary>
    /// 출발시킨 뒤에도 방을 붙잡고 있는 시간(초).
    ///
    /// ⚠ <b>왜 필요한가.</b> 세션 목록은 몇 초 늦게 갱신된다. 사람들을 보내자마자 방을
    ///    놓아 주면, 그들이 아직 접속하지 않아 <b>빈 방으로 보이는</b> 동안 다음 일행이
    ///    같은 방을 배정받는다. 두 팀이 한 방에서 만난다.
    /// </summary>
    private const double HoldAfterLaunchSeconds = 20.0;

    private readonly List<Party> parties = new List<Party>();

    /// <summary>출발한 일행이 아직 붙잡고 있는 방 → 놓아 줄 시각.</summary>
    private readonly Dictionary<string, double> held = new Dictionary<string, double>();

    /// <summary>
    /// 지금 아무도 없는 방들을 알려 주는 곳. 시험에서 갈아 끼울 수 있게 밖에서 넣는다.
    ///
    /// ⚠ <b>하나가 아니라 전부를 받는다.</b> 하나만 받으면 그 방이 이미 다른 일행의
    ///    것일 때 다음 후보로 넘어갈 수가 없다 — 몇 번을 물어도 같은 방을 알려 준다.
    /// </summary>
    private readonly Func<MiniGameId, int, List<string>> emptyRooms;

    /// <summary>지금 몇 초인가. 시험에서 시간을 직접 돌리려고 밖에서 넣는다.</summary>
    private readonly Func<double> now;

    /// <summary>한 일행의 사정이 바뀌었다. 그 사람들의 화면을 새로 그릴 때 쓴다.</summary>
    public event Action<Party> PartyChanged;

    /// <summary>이 일행을 방으로 보낸다.</summary>
    public event Action<Party> PartyLaunched;

    public LobbyMatchmaker(Func<MiniGameId, int, List<string>> emptyRooms, Func<double> now = null)
    {
        this.emptyRooms = emptyRooms ?? throw new ArgumentNullException(nameof(emptyRooms));
        this.now = now ?? (() => Time.timeAsDouble);
    }

    /// <summary>지금 살아 있는 일행들. 읽기용이다.</summary>
    public IReadOnlyList<Party> Parties => parties;

    /// <summary>이 사람이 속한 일행. 없으면 null.</summary>
    public Party PartyOf(PlayerRef player)
    {
        foreach (Party party in parties)
        {
            if (party.Members.Contains(player)) return party;
        }

        return null;
    }

    /// <summary>이 사람이 지금 어느 단계에 있는가.</summary>
    public Phase PhaseOf(PlayerRef player)
    {
        Party party = PartyOf(player);

        if (party == null) return Phase.None;
        if (party.Launched) return Phase.Launching;
        return party.Session.Length == 0 ? Phase.Waiting : Phase.Gathering;
    }

    /// <summary>
    /// **매칭 신청.** 이미 어딘가에 들어가 있으면 거기서 빼고 새로 넣는다.
    ///
    /// <paramref name="crew"/> 가 그 게임이 허용하는 범위를 벗어나면 신청을 받지 않는다.
    /// 화면이 막아야 할 일이지만, <b>서버가 한 번 더 본다</b> — 화면은 고쳐 보낼 수 있다.
    /// </summary>
    public bool Join(PlayerRef player, MiniGameConfig config, int crew)
    {
        if (config == null) return false;

        if (crew < config.MinPlayers || crew > config.MaxPlayers)
        {
            Debug.LogWarning(
                $"[매칭] {player} 가 {config.DisplayName} 을(를) {crew}명으로 신청했습니다. " +
                $"허용 범위는 {config.MinPlayers}~{config.MaxPlayers}명입니다. 받지 않습니다.");
            return false;
        }

        Leave(player);

        // 같은 게임 · 같은 인원을 기다리는 일행이 있으면 거기로 간다.
        foreach (Party open in parties)
        {
            if (open.Game != config.GameId || open.Crew != crew || !open.Accepting) continue;

            open.Members.Add(player);
            Settle(open);
            return true;
        }

        var party = new Party { Game = config.GameId, Crew = crew };
        party.Members.Add(player);
        parties.Add(party);

        TryAssignRoom(party);
        Settle(party);
        return true;
    }

    /// <summary>
    /// **매칭 취소.** 일행에서 뺀다. 마지막 한 명이 나가면 일행이 사라지고 방이 풀린다.
    ///
    /// ⚠ 출발한 일행에서는 빼지 않는다. 이미 방으로 보낸 사람이라 여기서 지워 봐야
    ///    돌아오지 않고, 방만 일찍 풀려 다음 팀과 겹친다.
    /// </summary>
    public void Leave(PlayerRef player)
    {
        for (int i = parties.Count - 1; i >= 0; i--)
        {
            Party party = parties[i];
            if (party.Launched || !party.Members.Remove(player)) continue;

            if (party.Members.Count == 0)
            {
                parties.RemoveAt(i);
                Debug.Log($"[매칭] {party.Game} {party.Crew}인 일행이 비어 사라집니다. 방 \"{party.Session}\" 을 돌려줍니다.");
            }
            else
            {
                Settle(party);
            }
        }
    }

    /// <summary>
    /// 접속이 끊긴 사람을 치운다. 취소와 같은 일이지만 이유가 달라 따로 부른다.
    /// </summary>
    public void Dropped(PlayerRef player) => Leave(player);

    /// <summary>
    /// **빈 방이 생겼는지 다시 살핀다.** 세션 목록이 갱신될 때마다 부른다.
    ///
    /// 줄을 서 있던 일행이 방을 받으면 화면이 "빈 서버 대기" 에서 "모으는 중" 으로 바뀐다.
    /// </summary>
    public void Poll()
    {
        ReleaseExpiredHolds();

        // ⚠ 뒤에서부터 돈다. Settle 은 다 찬 일행을 목록에서 빼므로,
        //    foreach 로 돌면 순회 도중에 목록이 바뀌어 예외가 난다.
        for (int i = parties.Count - 1; i >= 0; i--)
        {
            if (i >= parties.Count) continue;

            Party party = parties[i];
            if (party.Launched || party.Session.Length != 0) continue;

            if (TryAssignRoom(party)) Settle(party);
        }
    }

    /// <summary>이 방이 지금 어느 일행에게 묶여 있는가. 배정할 때 겹치지 않으려고 본다.</summary>
    public bool IsTaken(string session)
    {
        if (string.IsNullOrEmpty(session)) return false;
        if (held.ContainsKey(session)) return true;

        foreach (Party party in parties)
        {
            if (party.Session == session) return true;
        }

        return false;
    }

    private bool TryAssignRoom(Party party)
    {
        ReleaseExpiredHolds();

        List<string> candidates = emptyRooms(party.Game, party.Crew);
        if (candidates == null) return false;

        foreach (string room in candidates)
        {
            // ⚠ 감시기는 "지금 아무도 없는 방" 을 말할 뿐, 우리가 이미 누구에게 줬는지 모른다.
            //    방금 배정한 방은 아직 아무도 도착하지 않아 여전히 비어 보인다. 그래서
            //    우리가 들고 있는 장부로 한 번 더 거른다.
            if (IsTaken(room)) continue;

            party.Session = room;
            Debug.Log($"[매칭] {party.Game} {party.Crew}인 일행에게 방 \"{room}\" 을 배정했습니다.");
            return true;
        }

        return false;
    }

    /// <summary>인원이 찼는지 보고, 찼으면 출발시킨다. 아니면 화면만 새로 그린다.</summary>
    private void Settle(Party party)
    {
        if (party.Launched) return;

        if (!party.Full || party.Session.Length == 0)
        {
            PartyChanged?.Invoke(party);
            return;
        }

        party.Launched = true;
        held[party.Session] = now() + HoldAfterLaunchSeconds;
        parties.Remove(party);

        Debug.Log(
            $"[매칭] {party.Game} {party.Crew}인 일행이 모두 모였습니다. " +
            $"방 \"{party.Session}\" 으로 보냅니다.");

        PartyLaunched?.Invoke(party);
    }

    private void ReleaseExpiredHolds()
    {
        if (held.Count == 0) return;

        double at = now();
        List<string> done = null;

        foreach (KeyValuePair<string, double> entry in held)
        {
            if (entry.Value > at) continue;

            done ??= new List<string>();
            done.Add(entry.Key);
        }

        if (done == null) return;

        foreach (string session in done) held.Remove(session);
    }
}
