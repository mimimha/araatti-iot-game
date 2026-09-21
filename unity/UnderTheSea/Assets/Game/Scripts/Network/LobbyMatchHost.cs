using System.Collections.Generic;
using Fusion;
using MiniGames.Common;
using UnityEngine;

/// <summary>
/// **로비 전용 서버에서 매칭을 돌리는 자리.** 규칙은 <see cref="LobbyMatchmaker"/> 가 알고,
/// 여기는 그것을 살려 두고 바깥과 이어 준다.
///
/// <code>
///   DsPoolWatcher.Updated  ──▶  Matchmaker.Poll()      빈 방이 생겼나 다시 본다
///   Matchmaker.PartyChanged ──▶ 그 일행에게 화면 갱신
///   Matchmaker.PartyLaunched ──▶ 그 일행에게 "이 방으로 가라"
/// </code>
///
/// <b>왜 <see cref="NetworkBehaviour"/> 가 아닌가.</b> 매칭 상태는 <b>모두에게 복제할
/// 필요가 없다.</b> 각자 자기 일행의 사정만 알면 된다. 네트워크 속성으로 만들면 남의
/// 매칭까지 전부 흘러가고, 그것을 숨기는 코드를 또 써야 한다.
///
/// ⚠ 서버에서만 만들어진다. 클라이언트에는 <see cref="Current"/> 가 늘 null 이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyMatchHost : MonoBehaviour
{
    private const string HostName = "[로비 매칭]";

    /// <summary>돌고 있는 매칭. 로비 DS 가 아니면 null 이다.</summary>
    public static LobbyMatchHost Current { get; private set; }

    /// <summary>규칙을 아는 쪽.</summary>
    public LobbyMatchmaker Matchmaker { get; private set; }

    /// <summary>사람 → 그 사람에게 말을 거는 통로.</summary>
    private readonly Dictionary<PlayerRef, PlayerMatchRelay> relays =
        new Dictionary<PlayerRef, PlayerMatchRelay>();

    private DsPoolWatcher boundWatcher;

    /// <summary>
    /// **없으면 만든다.** 로비 DS 에서 첫 플레이어가 들어올 때 불린다.
    ///
    /// 서버가 기동하는 시점이 아니라 이때 만드는 이유는, 그때는 아직 감시기가 붙지
    /// 않았을 수 있기 때문이다. 어차피 신청할 사람이 없으면 매칭도 필요 없다.
    /// </summary>
    public static LobbyMatchHost Ensure()
    {
        if (Current != null) return Current;

        var host = new GameObject(HostName);
        DontDestroyOnLoad(host);
        Current = host.AddComponent<LobbyMatchHost>();
        return Current;
    }

    private void Awake()
    {
        Matchmaker = new LobbyMatchmaker(EmptyRooms);
        Matchmaker.PartyChanged += OnPartyChanged;
        Matchmaker.PartyLaunched += OnPartyLaunched;

        Debug.Log($"{HostName} 시작했습니다.");
    }

    private void Update()
    {
        // 감시기는 로비 DS 가 기동하면서 따로 뜬다. 준비되는 대로 이어 붙인다.
        if (boundWatcher != null || DsPoolWatcher.Current == null) return;

        boundWatcher = DsPoolWatcher.Current;
        boundWatcher.Updated += OnPoolUpdated;

        Debug.Log($"{HostName} DS 감시기와 이었습니다. 빈 방이 생기면 줄 선 일행부터 넣습니다.");
    }

    private void OnDestroy()
    {
        if (boundWatcher != null) boundWatcher.Updated -= OnPoolUpdated;
        if (Current == this) Current = null;
    }

    private void OnPoolUpdated() => Matchmaker.Poll();

    /// <summary>감시기가 아직 없으면 빈 목록이다. 없는 방에 사람을 보내지 않는다.</summary>
    private List<string> EmptyRooms(MiniGameId game, int crew)
    {
        DsPoolWatcher watcher = DsPoolWatcher.Current;
        return watcher == null ? new List<string>() : watcher.EmptyRooms(game, crew);
    }

    // ───────────────────────────── 통로 등록 ─────────────────────────────

    public void Register(PlayerRef player, PlayerMatchRelay relay) => relays[player] = relay;

    public void Unregister(PlayerRef player)
    {
        relays.Remove(player);

        // 나간 사람을 일행에서 뺀다. 안 그러면 영영 안 찰 인원을 기다린다.
        Matchmaker.Dropped(player);
    }

    // ───────────────────────────── 알리기 ─────────────────────────────

    private void OnPartyChanged(LobbyMatchmaker.Party party)
    {
        LobbyMatchmaker.Phase phase = party.Session.Length == 0
            ? LobbyMatchmaker.Phase.Waiting
            : LobbyMatchmaker.Phase.Gathering;

        foreach (PlayerRef member in party.Members)
        {
            if (relays.TryGetValue(member, out PlayerMatchRelay relay) && relay != null)
            {
                relay.PushState(phase, party.Game, party.Crew, party.Members.Count);
            }
        }
    }

    private void OnPartyLaunched(LobbyMatchmaker.Party party)
    {
        foreach (PlayerRef member in party.Members)
        {
            if (relays.TryGetValue(member, out PlayerMatchRelay relay) && relay != null)
            {
                relay.PushLaunch(party.Game, party.Crew, party.Session);
            }
        }
    }
}
